using AngleSharp.Html.Parser;

namespace PageTrail.Worker.Fetching;

public static class LinkExtractor
{
    private static readonly HtmlParser Parser = new();

    public static IReadOnlyList<string> ExtractHrefs(string html)
    {
        using var document = Parser.ParseDocument(html);
        return document.QuerySelectorAll("a[href]").Select(a => a.GetAttribute("href")!).ToList();
    }
}
