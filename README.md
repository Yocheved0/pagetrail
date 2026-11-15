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

Each page waiting to be crawled is one message on a RabbitMQ queue. The message only says which job and which page it is about, for example `{ "JobId": "…", "PageId": 42 }`; the worker looks up everything else in the database.

**The same message can arrive twice, and that's safe.** RabbitMQ sometimes delivers a message more than once. Before saving anything, the worker checks that the page is still waiting to be crawled. If an earlier delivery already handled it, the worker saves nothing. The database also refuses to store the same page or link twice.

**Failures are retried.** If fetching a page fails because of a network problem, a timeout or a server error, the worker tries the request again a few times. If handling the message fails for another reason, for example the database is briefly unavailable, the message is tried again 5 seconds later, up to 3 times in total.

**Messages that keep failing are set aside.** A message that can't be read, points to a page that doesn't exist, or fails all 3 tries is moved to a separate queue, `crawl.pages.dlq` (a "dead-letter" queue), where it stops being retried and can be inspected. Its page is marked as failed so the rest of the job can still finish.

## Assumptions

- `www.example.com` and `example.com` are the same site; other subdomains are external.
- Only same-site links are crawled; external links are counted but not fetched.
- Defaults: depth 2 (max 5), 200 pages per job.

## What I built first, what I cut, and what I'd do next

I started with the rules for cleaning up URLs and calculating the Domain Link Ratio, with tests, because every other part of the app depends on them being right. Then I built one simple path all the way through, from clicking Start Crawl to the API, the queue, the worker and back to the screen, because the messaging was the part most likely to go wrong.

I cut live updates: the job page asks the API for progress every 1.5 seconds instead, which is simpler and fast enough here. I also didn't write a test that runs against a real RabbitMQ server; the retry and dead-letter behaviour is tested against the database instead.

With more time, I would add live updates, a background check that re-sends any page whose message was lost, and a test that runs against a real RabbitMQ server.

## Known limitations

- **The worker needs RabbitMQ running before it starts.** If RabbitMQ isn't reachable at startup, the worker stops instead of waiting. Docker Compose avoids this by starting the worker only once RabbitMQ is ready, but if you run the worker by hand, start RabbitMQ first.
- **A lost message leaves its job unfinished.** If a message disappears without any error, its page stays "waiting" forever, so the job never reaches Completed. The background check under "what I'd do next" would fix this.
- **There is no login.** Anyone who can reach the app can start crawls and see every job. That's fine on your own machine, but it would need authentication before running on a shared server.
