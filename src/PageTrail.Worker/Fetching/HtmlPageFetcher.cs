using System.Net.Mime;
using Polly.Timeout;

namespace PageTrail.Worker.Fetching;

public sealed class HtmlPageFetcher(HttpClient http, TimeProvider time)
{
    public const int MaxBodyBytes = 5 * 1024 * 1024;

    public const string TimedOutReason = "Timed out";

    private const string RobotsPath = "/robots.txt";
    private const int ReadChunkBytes = 81920;
    private static readonly TimeSpan BodyReadTimeout = TimeSpan.FromSeconds(30);

    public async Task<string?> FetchRobotsAsync(Uri origin, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(origin, RobotsPath), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode ? await ReadBodyAsync(response.Content, cancellationToken) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpIOException or BodyLimitException || IsTimeout(ex, cancellationToken))
        {
            return null;
        }
    }

    public async Task<FetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var finalUrl = response.RequestMessage?.RequestUri ?? url;
            if (!response.IsSuccessStatusCode)
            {
                return Failed(finalUrl, $"HTTP {(int)response.StatusCode}");
            }

            if (response.Content.Headers.ContentType?.MediaType != MediaTypeNames.Text.Html)
            {
                return new FetchResult(FetchOutcome.NotHtml, finalUrl);
            }

            return new FetchResult(FetchOutcome.Html, finalUrl, await ReadBodyAsync(response.Content, cancellationToken));
        }
        catch (HttpRequestException ex) when (ex.InnerException is BlockedAddressException)
        {
            return Failed(url, PrivateAddressGuard.BlockedReason);
        }
        catch (HttpRequestException ex)
        {
            return Failed(url, ex.HttpRequestError.ToString());
        }
        catch (HttpIOException ex)
        {
            return Failed(url, ex.HttpRequestError.ToString());
        }
        catch (BodyLimitException ex)
        {
            return Failed(url, ex.Message);
        }
        catch (Exception ex) when (IsTimeout(ex, cancellationToken))
        {
            return Failed(url, TimedOutReason);
        }
    }

    private static FetchResult Failed(Uri url, string reason) => new(FetchOutcome.Failed, url, FailureReason: reason);

    private static bool IsTimeout(Exception ex, CancellationToken cancellationToken) =>
        ex is TimeoutRejectedException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task<string> ReadAtMostAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var body = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                throw new BodyLimitException($"Body larger than {MaxBodyBytes / (1024 * 1024)} MB");
            }

            buffer.Write(chunk, 0, read);
        }

        // WHY: rewrapping the bounded bytes keeps HttpContent's charset and byte-order-mark decoding
        using var buffered = new ByteArrayContent(buffer.GetBuffer(), 0, (int)buffer.Length);
        buffered.Headers.ContentType = content.Headers.ContentType;
        return await buffered.ReadAsStringAsync(cancellationToken);
    }

    private async Task<string> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(BodyReadTimeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await ReadAtMostAsync(content, linked.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new BodyLimitException($"Body not read within {BodyReadTimeout.TotalSeconds} s");
        }
    }

    private sealed class BodyLimitException(string reason) : Exception(reason);
}
