namespace PageTrail.Domain.Links;

public sealed class StartingDomain(Uri jobUrl)
{
    private readonly string _host = Hosts.WithoutWww(jobUrl.Host);

    public bool Contains(Uri url) => string.Equals(Hosts.WithoutWww(url.Host), _host, StringComparison.OrdinalIgnoreCase);
}
