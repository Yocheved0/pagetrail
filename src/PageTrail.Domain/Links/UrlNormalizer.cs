namespace PageTrail.Domain.Links;

public static class UrlNormalizer
{
    public const int MaxUrlLength = 2048;

    public static Uri? Normalize(Uri pageUrl, string href)
    {
        var trimmed = href.Trim();
        if (trimmed.Length == 0 || !Uri.TryCreate(pageUrl, trimmed, out var resolved) || !IsHttp(resolved))
        {
            return null;
        }

        return new Uri(resolved.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped));
    }

    public static string DedupKey(Uri url) => new UriBuilder(url) { Host = Hosts.WithoutWww(url.Host) }.Uri.AbsoluteUri;

    public static bool IsHttp(Uri url) =>
        url.IsAbsoluteUri
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
        && url.Host.Length > 0;

    public static bool FitsMaxLength(Uri url) => url.AbsoluteUri.Length <= MaxUrlLength;
}
