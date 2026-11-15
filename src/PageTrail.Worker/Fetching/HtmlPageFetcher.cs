using System.Net.Mime;

namespace PageTrail.Worker.Fetching;

public sealed class HtmlPageFetcher(HttpClient http)
{
    public async Task<FetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var finalUrl = response.RequestMessage?.RequestUri ?? url;
            if (!response.IsSuccessStatusCode)
            {
                return new FetchResult(FetchOutcome.Failed, finalUrl, FailureReason: $"HTTP {(int)response.StatusCode}");
            }

            if (response.Content.Headers.ContentType?.MediaType != MediaTypeNames.Text.Html)
            {
                return new FetchResult(FetchOutcome.NotHtml, finalUrl);
            }

            return new FetchResult(FetchOutcome.Html, finalUrl, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex) when (ex.InnerException is BlockedAddressException)
        {
            return new FetchResult(FetchOutcome.Failed, url, FailureReason: PrivateAddressGuard.BlockedReason);
        }
        catch (HttpRequestException ex)
        {
            return new FetchResult(FetchOutcome.Failed, url, FailureReason: ex.HttpRequestError.ToString());
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new FetchResult(FetchOutcome.Failed, url, FailureReason: "Timed out");
        }
    }
}
