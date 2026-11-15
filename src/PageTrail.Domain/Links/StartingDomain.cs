namespace PageTrail.Domain.Links;

public sealed class StartingDomain(Uri jobUrl)
{
    private const string WwwPrefix = "www.";

    private readonly string _host = WithoutWww(jobUrl.Host);

    public bool Contains(Uri url) => string.Equals(WithoutWww(url.Host), _host, StringComparison.OrdinalIgnoreCase);

    private static string WithoutWww(string host) =>
        host.StartsWith(WwwPrefix, StringComparison.OrdinalIgnoreCase) ? host[WwwPrefix.Length..] : host;
}
