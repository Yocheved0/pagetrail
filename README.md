# PageTrail

Enter a website URL and PageTrail crawls it in the background, then shows the pages it found as a tree with each page's **Domain Link Ratio** (links to the same site ÷ all links).

Built with .NET 8, RabbitMQ, SQL Server and React.

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
| RabbitMQ admin | http://localhost:15673 |

## Test it

```bash
dotnet test PageTrail.sln
pnpm --dir src/web install && pnpm --dir src/web run test
```

Integration tests need the Compose SQL Server (`docker compose up -d db`, port 1434) and its connection string in the API's user secrets.

## How it works

```
UI ──▶ API ──▶ SQL Server ◀── Worker
        │                       ▲
        └──▶ RabbitMQ ──────────┘
```

1. Starting a crawl saves the job with its first page as `Queued`.
2. A background loop in the API publishes queued pages to RabbitMQ.
3. The worker fetches a page, saves its links, and saves new same-site pages as `Queued`.
4. The job is done when no page is left `Queued`.

## Messaging

**Message:** `{ "JobId": "…", "PageId": 42 }`. Everything else is read from the database.

**Idempotency.** A message can arrive twice. The worker marks a page done only `WHERE Status = 'Queued'`; if that changes nothing, the page was already processed and nothing is written. Unique indexes make duplicate pages or links impossible.

**Retries.** The HTTP client retries network errors, timeouts and 5xx/408/429 responses. Any other failure while handling a message is tried up to 3 times, 5 seconds apart.

**Dead-letter queue** (`crawl.pages.dlq`) gets messages that can't be read, point at a missing page, or still fail after 3 attempts.

## Assumptions

- `www.example.com` and `example.com` are the same site; other subdomains are external.
- Only same-site links are crawled; external links are counted but not fetched.
- Defaults: depth 2 (max 5), 200 pages per job.

## First, cut, next

**First:** the URL and ratio rules with tests, because everything depends on them; then one thin path from the UI through the queue to the worker, because messaging was the riskiest part.

**Cut:** live updates (polling is enough here) and a test through a real RabbitMQ (retries and dead-lettering are tested against the database).

**Next:** live updates, a check that re-sends pages stuck in `Queued`, and a test through a real broker.

## Known limitations

- The worker exits if RabbitMQ isn't up when it starts; Compose starts it after RabbitMQ.
- No authentication.
