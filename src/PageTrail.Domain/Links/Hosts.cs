namespace PageTrail.Domain.Links;

internal static class Hosts
{
    private const string WwwPrefix = "www.";

    public static string WithoutWww(string host) =>
        host.StartsWith(WwwPrefix, StringComparison.OrdinalIgnoreCase) ? host[WwwPrefix.Length..] : host;
}
