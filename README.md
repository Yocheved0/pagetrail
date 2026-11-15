# PageTrail

Submit a website URL, crawl it asynchronously, and browse the discovered pages as a tree with each page's **Domain Link Ratio** (same-domain outgoing links ÷ all outgoing links).

Built with .NET 8 (ASP.NET Core minimal APIs, EF Core 8), RabbitMQ, SQL Server 2025 and React (Vite + TypeScript).

## Run it

You need Docker.

```bash
cp .env.example .env    # then fill in the three values
docker compose up --build
```

| Service | URL |
|---|---|
| UI | http://localhost:3000 |
| API | http://localhost:8080/api/jobs |
| API health | http://localhost:8080/health |
| Worker health | http://localhost:8081/health |
| RabbitMQ management | http://localhost:15673 |

SQL Server is published on host port 1434 and RabbitMQ (AMQP) on 5673.

### Running the API and worker outside Docker

Start the infrastructure with `docker compose up -d db rabbitmq`, then give each project its settings through user-secrets (the values come from your `.env`):

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1434;Database=PageTrail;User Id=sa;Password=<SA_PASSWORD>;TrustServerCertificate=True" --project src/PageTrail.Api
dotnet user-secrets set "RabbitMq:HostName" "localhost" --project src/PageTrail.Api
dotnet user-secrets set "RabbitMq:Port" "5673" --project src/PageTrail.Api
dotnet user-secrets set "RabbitMq:UserName" "<RABBITMQ_USER>" --project src/PageTrail.Api
dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD>" --project src/PageTrail.Api
```

Repeat for `src/PageTrail.Worker`. The API listens on http://localhost:5080 and the worker on http://localhost:5081. For the UI, run `pnpm --dir src/web install` and `pnpm --dir src/web run dev` (http://localhost:5173; `/api` is proxied to the API on 5080).

## Test it

```bash
dotnet test PageTrail.sln
pnpm --dir src/web install && pnpm --dir src/web run test
```

Integration tests use the Compose SQL Server (start it with `docker compose up -d db`) and a separate `PageTrail_IntegrationTests` database, reading the connection string from the API's user-secrets. `node tools/verify.mjs` runs everything the CI pipeline checks: comment and secret scans, `dotnet format`, build with warnings as errors, all tests, and the web lint, typecheck, test and build.

| Suite | Count | Covers |
|---|---|---|
| Unit | 41 | URL normalization (relative links, fragments, case, ports, schemes), starting-domain matching, `www.` de-duplication key, Domain Link Ratio |
| Integration | 71 | API endpoints (create, read, tree, history, cancel, validation, 404/409), worker crawl over local HTML fixtures (cycles, depth, page cap, redirects, non-HTML), idempotent delivery, retry and DLQ decisions, SSRF guard, robots.txt |
| Web | 6 | Start Crawl, Job Details (tree, cancel), History |

## API

| Method | Route | Result |
|---|---|---|
| POST | `/api/jobs` | body `{ "url": "https://…", "maxDepth": 2 }` → `202 { "jobId" }`; 400 ProblemDetails on invalid input |
| GET | `/api/jobs/{id}` | status, timestamps, failure reason, page counts by status; 404 if unknown |
| GET | `/api/jobs/{id}/tree` | nested pages with status, ratio and outgoing links; available in any status |
| GET | `/api/jobs?page=1&pageSize=20` | `{ items, page, pageSize, totalCount }`, newest first; pageSize 1–100 |
| POST | `/api/jobs/{id}/cancel` | 200 with the canceled job; 409 if already finished; 404 if unknown |

`maxDepth` defaults to 2 and must be 0–5. Statuses: `Pending | Running | Completed | Failed | Canceled`.

## How it works

```
 UI (React, nginx) ──/api──▶ Crawl API ──EF Core──▶ SQL Server ◀──EF Core── Crawl Worker
                                │                                               ▲
                                └── outbox dispatcher ──▶ RabbitMQ crawl.pages ─┘
```

1. `POST /api/jobs` saves the job (Pending) and its root page (Queued, unpublished) in one transaction.
2. A dispatcher in the API publishes unpublished pages to RabbitMQ every second and marks them published.
3. The worker consumes one message per page: it checks robots.txt, fetches the page, parses `<a href>` links, computes the ratio, and in one transaction marks the page done, stores its links, and saves new same-domain pages as Queued children.
4. The job completes when no page is left Queued.

### Key architectural choices

- **Two processes, four projects.** `PageTrail.Api` (orchestrator) and `PageTrail.Worker` share `PageTrail.Domain` (entities and crawl rules, no I/O) and `PageTrail.Infrastructure` (EF Core and RabbitMQ setup).
- **One message per page, not per job.** A retry repeats one page, idempotency is checked per page, and work spreads across workers.
- **Transactional outbox.** The worker never publishes; it writes new pages as unpublished rows and the API dispatcher publishes them. A crash between saving a page and publishing it cannot lose that page.
- **The worker writes the crawl results** directly through the shared Infrastructure project; the API owns the schema and applies EF Core migrations on startup.
- **RabbitMQ.Client, not MassTransit**, so retries and dead-lettering are explicit code.
- **Minimal APIs** with typed results and RFC 7807 ProblemDetails for every error.
- **Polling, not push**: Job Details polls every 1.5 s until the job finishes.

### Data model

- `Jobs`: id (GUID), URL, maxDepth, maxPages, status, created/started/completed, failure reason. Index on `CreatedAt DESC` for history.
- `Pages`: id, job, URL, depth, parent page (the first page that discovered it), status (`Queued | Done | Skipped | Failed`), ratio, published flag. A unique index on (job, SHA-256 of the de-duplication key) makes a duplicate page impossible; a filtered index finds unpublished pages for the dispatcher; an index on (job, status) serves progress counts.
- `Links`: source page and target URL, unique on (source page, SHA-256 of the target URL). These are the parent → child edges.

URLs are up to 2048 characters, too long for a SQL Server index key, so uniqueness uses persisted SHA-256 computed columns.

## Messaging

**Message schema** (queue `crawl.pages`, JSON):

```json
{ "JobId": "3f1c…", "PageId": 42 }
```

The message only points at a page row; everything else is read from the database. A retried message carries an `x-attempt` header.

**Idempotency.** The worker fetches the page outside any transaction, then opens one transaction that locks the job row (`UPDLOCK`) and claims the page with `UPDATE … WHERE Status = 'Queued'`. If that updates 0 rows, another delivery already processed the page and nothing is written. Links and child pages are written in the same transaction, and the unique indexes above make duplicate pages or links impossible even if two deliveries race. The job-row lock also serializes per-job commits, so the last page to finish always sees zero Queued pages and completes the job.

**Retry policy.** HTTP fetches use the standard .NET resilience handler: 10 s per attempt, retries with backoff on network errors, timeouts, 5xx, 408 and 429. A page that still fails is recorded as Failed, which is a normal outcome, not a message failure. Any other exception while handling a message (database unavailable, a unique-key race between two workers) is treated as transient: the message is republished to `crawl.pages.retry`, a queue with a 5-second TTL that dead-letters back to `crawl.pages`, up to 3 attempts in total.

**Dead-letter queue** (`crawl.pages.dlq`) receives:
- messages that are not valid JSON;
- messages for a page or job that does not exist;
- messages that still fail after 3 attempts. The page is marked Failed first (a failed root page fails the job with a reason) so the job can still finish.

If routing a message itself fails (for example the database is down while marking the page Failed), the consumer waits 5 s and nacks with requeue, so the message is never left unacknowledged.

## Crawling rules

- **Normalization:** relative links resolve against the page URL; fragments are dropped; scheme and host are lowercased; default ports and an empty path are normalized; path and query are kept as written.
- **Links:** only `<a href>`; `mailto:`, `tel:`, `javascript:` and every non-http(s) scheme are ignored. Each normalized URL counts once per page, and a link to the page itself is not an outgoing link.
- **Starting domain:** the host of the job URL, ignoring one leading `www.` when comparing hosts. Subdomains such as `blog.` count as external.
- **What is crawled:** same-domain links only, down to `maxDepth`, at most 200 pages per job (configurable as `Crawl:MaxPagesPerJob`). External links are stored and counted in the ratio but never fetched.
- **Duplicates:** a page is unique per job by its URL with a leading `www.` removed from the host, so `www.site/a` and `site/a` are crawled once.
- **HTML only:** a response whose content type is not `text/html` is recorded as Skipped. A redirect is followed, but a page whose final host leaves the starting domain is Skipped.
- **Ratio:** zero outgoing links gives 0.
- **robots.txt:** fetched once per job and origin (cached in the worker for 30 minutes), using the `PageTrailBot` group if present, otherwise `*`. It supports `Allow`, `Disallow`, `*` and `$` with longest-match precedence. Disallowed pages are Skipped; a missing or unreachable robots.txt allows everything. Requests send `User-Agent: PageTrailBot/1.0`.

## Observability

- JSON console logs with scopes; every log line for a message carries `JobId` and `PageId`. Logs contain IDs and event names, not page content.
- `/health` on the API (database) and the worker (database and RabbitMQ connection).

## Security

- Input is validated at the API boundary (absolute http(s) URL, `maxDepth` 0–5, paging bounds); errors are ProblemDetails with no stack traces.
- **SSRF guard:** the worker resolves each host before connecting and refuses loopback, private (RFC 1918), link-local (including `169.254.169.254`) and unique-local addresses, including on redirects and child links.
- Secrets come from `.env` (Compose) or user-secrets (local runs); `appsettings.json` holds empty placeholders and `.env` is not committed.
- EF Core only; the one raw SQL statement (the job-row lock) is parameterised.
- No authentication: the data is public URLs and crawl results, and there are no users.

## Assumptions and trade-offs

- "Starting domain" ignores a leading `www.`; any other subdomain is external.
- Depth is capped at 5 to keep a single request from crawling unbounded work; the page cap is 200.
- The tree shows each page once, under the page that discovered it first; all edges are still stored in `Links`.
- The tree endpoint returns partial results while a job runs; the UI shows the tree once the job completes.
- A canceled job keeps its remaining pages as Queued so its progress shows what was left; the worker skips them.

## What I built first, what I cut, what's next

**First:** the URL and ratio rules with unit tests (the core correctness), then a thin vertical slice from Start Crawl through API, outbox, RabbitMQ and worker to Job Details, because the messaging path was the riskiest part. Then the full crawl and tree, reliable messaging (idempotency, retry, DLQ), history, cancel, the SSRF guard and robots.txt.

**Cut:** live updates (SSE/SignalR) in favour of polling; an OpenAPI/Swagger page (routes are listed above); a test that runs through a real RabbitMQ broker (retry and DLQ decisions are tested against the database, and the queue topology was checked by hand).

**Next:**
- An integration test through a real broker (Testcontainers) and a SQL Server service in CI.
- A reconnect loop in the worker so it survives starting before RabbitMQ.
- A sweeper that re-publishes pages stuck in Queued.
- Server-sent events instead of polling.

## Known limitations

- **CI:** the GitHub Actions workflow runs `dotnet test` on the whole solution without a SQL Server service, so the integration tests fail there; they pass locally against the Compose database.
- **Worker startup:** the worker exits if RabbitMQ is unreachable at startup; Compose starts it after RabbitMQ is healthy.
- **Page cap:** checked per page commit, so several workers in parallel could exceed it slightly; Compose runs one worker.
- **Blocked addresses:** a URL blocked by the SSRF guard takes about 8 seconds to fail, because the HTTP retry policy retries the refused connection.
- **robots.txt:** the body has no size limit, and two simultaneous first deliveries for one job may each fetch it once.
- **Outbox duplicates:** if publishing fails midway through a batch, the dispatcher may publish some pages twice; the worker's idempotency makes this harmless.
- **Message format:** message JSON uses PascalCase property names.
