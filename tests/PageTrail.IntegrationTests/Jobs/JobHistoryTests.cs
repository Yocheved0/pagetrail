using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PageTrail.Api.Jobs;
using PageTrail.Domain.Jobs;

namespace PageTrail.IntegrationTests.Jobs;

[Collection(ApiCollection.Name)]
public sealed class JobHistoryTests(PageTrailApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HistoryListsJobsNewestFirstWithATotalCount()
    {
        var created = new List<Guid>();
        foreach (var path in new[] { "one", "two", "three" })
        {
            created.Add(await CreateJobAsync($"https://history.example/{path}"));
        }

        var history = await _client.GetFromJsonAsync<JobHistoryResponse>("/api/jobs?page=1&pageSize=3", Json);

        Assert.NotNull(history);
        Assert.Equal(1, history.Page);
        Assert.Equal(3, history.PageSize);
        Assert.True(history.TotalCount >= 3);
        Assert.Equal(created.AsEnumerable().Reverse(), history.Items.Select(j => j.Id));
        var newest = history.Items[0];
        Assert.Equal("https://history.example/three", newest.Url);
        Assert.Equal(CrawlJobStatus.Pending, newest.Status);
        Assert.Null(newest.StartedAt);
    }

    [Fact]
    public async Task HistoryPagesThroughOlderJobs()
    {
        var older = await CreateJobAsync("https://history.example/older");
        await CreateJobAsync("https://history.example/newer");

        var secondPage = await _client.GetFromJsonAsync<JobHistoryResponse>("/api/jobs?page=2&pageSize=1", Json);

        Assert.Equal(older, Assert.Single(secondPage!.Items).Id);
    }

    [Fact]
    public async Task HistoryDefaultsToTheFirstPageOfTwenty()
    {
        var history = await _client.GetFromJsonAsync<JobHistoryResponse>("/api/jobs", Json);

        Assert.Equal(1, history!.Page);
        Assert.Equal(20, history.PageSize);
        Assert.True(history.Items.Count <= 20);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task OutOfRangePagingReturns400ProblemDetails(string query)
    {
        var response = await _client.GetAsync(new Uri($"/api/jobs?{query}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    private async Task<Guid> CreateJobAsync(string url)
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new { url });
        return (await response.Content.ReadFromJsonAsync<CreateJobResponse>(Json))!.JobId;
    }
}
