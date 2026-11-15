using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using PageTrail.Api.Jobs;
using PageTrail.Domain.Jobs;

namespace PageTrail.IntegrationTests.Jobs;

[Collection(ApiCollection.Name)]
public sealed class JobEndpointsTests(PageTrailApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreatingAJobReturns202AndThePendingJobCanBeRead()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", new { url = "HTTPS://Example.com:443/start#top", maxDepth = 3 });

        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var jobId = (await created.Content.ReadFromJsonAsync<CreateJobResponse>(Json))!.JobId;
        Assert.Equal($"/api/jobs/{jobId}", created.Headers.Location!.OriginalString);

        var job = await _client.GetFromJsonAsync<JobResponse>($"/api/jobs/{jobId}", Json);

        Assert.NotNull(job);
        Assert.Equal(jobId, job.Id);
        Assert.Equal("https://example.com/start", job.Url);
        Assert.Equal(3, job.MaxDepth);
        Assert.Equal(CrawlJobStatus.Pending, job.Status);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Equal(new JobProgress(Queued: 1, Done: 0, Skipped: 0, Failed: 0), job.Progress);
    }

    [Fact]
    public async Task MaxDepthDefaultsToTwo()
    {
        var created = await _client.PostAsJsonAsync("/api/jobs", new { url = "https://example.com/" });
        var jobId = (await created.Content.ReadFromJsonAsync<CreateJobResponse>(Json))!.JobId;

        var job = await _client.GetFromJsonAsync<JobResponse>($"/api/jobs/{jobId}", Json);

        Assert.Equal(2, job!.MaxDepth);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("/relative/path", null)]
    [InlineData("ftp://example.com/", null)]
    [InlineData("mailto:someone@example.com", null)]
    [InlineData("https://example.com/", -1)]
    [InlineData("https://example.com/", 6)]
    public async Task InvalidJobRequestsReturn400ProblemDetails(string? url, int? maxDepth)
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new { url, maxDepth });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task JobUrlLongerThan2048CharactersReturns400ProblemDetails()
    {
        var url = "https://example.com/" + new string('a', 2048);

        var response = await _client.PostAsJsonAsync("/api/jobs", new { url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Json);
        Assert.Contains(nameof(CreateJobRequest.Url), problem!.Errors.Keys);
    }

    [Fact]
    public async Task ReadingAnUnknownJobReturns404()
    {
        var response = await _client.GetAsync(new Uri($"/api/jobs/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
