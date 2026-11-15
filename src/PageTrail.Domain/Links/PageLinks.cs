namespace PageTrail.Domain.Links;

public sealed record PageLinks(IReadOnlyList<Uri> Outgoing, double DomainLinkRatio)
{
    public static PageLinks From(Uri pageUrl, IEnumerable<string> hrefs, StartingDomain domain)
    {
        var outgoing = hrefs
            .Select(href => UrlNormalizer.Normalize(pageUrl, href))
            .OfType<Uri>()
            .Where(UrlNormalizer.FitsMaxLength)
            .Where(url => url != pageUrl)
            .Distinct()
            .ToList();

        return new PageLinks(outgoing, RatioOf(outgoing, domain));
    }

    private static double RatioOf(List<Uri> outgoing, StartingDomain domain) =>
        outgoing.Count == 0 ? 0 : (double)outgoing.Count(domain.Contains) / outgoing.Count;
}
