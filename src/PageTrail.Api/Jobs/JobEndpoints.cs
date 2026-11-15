using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;
using PageTrail.Infrastructure.Persistence;

namespace PageTrail.Api.Jobs;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs");
        jobs.MapPost("/", CreateJobAsync);
        jobs.MapGet("/{jobId:guid}", GetJobAsync);
        return app;
    }

    private static async Task<Results<Accepted<CreateJobResponse>, ValidationProblem>> CreateJobAsync(
        CreateJobRequest request,
        PageTrailDbContext db,
        IOptions<CrawlLimitsOptions> limits,
        TimeProvider time,
        ILogger<CrawlJob> logger,
        CancellationToken cancellationToken)
    {
        var errors = CreateJobValidator.Validate(request, out var url);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var job = CrawlJob.Create(url!, request.MaxDepth ?? CrawlLimitsOptions.DefaultDepth, limits.Value.MaxPagesPerJob, time.GetUtcNow());
        db.Jobs.Add(job);
        db.Pages.Add(CrawlPage.Root(job));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Crawl job {JobId} created", job.Id);

        return TypedResults.Accepted($"/api/jobs/{job.Id}", new CreateJobResponse(job.Id));
    }

    private static async Task<Results<Ok<JobResponse>, NotFound>> GetJobAsync(
        Guid jobId,
        PageTrailDbContext db,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        var progress = await ProgressOfAsync(db, jobId, cancellationToken);
        return TypedResults.Ok(new JobResponse(
            job.Id, job.Url, job.MaxDepth, job.Status, job.CreatedAt, job.StartedAt, job.CompletedAt, job.FailureReason, progress));
    }

    private static async Task<JobProgress> ProgressOfAsync(PageTrailDbContext db, Guid jobId, CancellationToken cancellationToken)
    {
        var counts = await db.Pages
            .Where(p => p.JobId == jobId)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        int CountOf(CrawlPageStatus status) => counts.GetValueOrDefault(status);

        return new JobProgress(
            CountOf(CrawlPageStatus.Queued), CountOf(CrawlPageStatus.Done), CountOf(CrawlPageStatus.Skipped), CountOf(CrawlPageStatus.Failed));
    }
}
