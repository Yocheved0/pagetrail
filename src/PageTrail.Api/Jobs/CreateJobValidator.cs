using PageTrail.Domain.Links;

namespace PageTrail.Api.Jobs;

public static class CreateJobValidator
{
    public static Dictionary<string, string[]> Validate(CreateJobRequest request, out Uri? normalizedUrl)
    {
        var errors = new Dictionary<string, string[]>();
        normalizedUrl = ParseUrl(request.Url);
        if (normalizedUrl is null)
        {
            errors[nameof(request.Url)] = ["Url must be an absolute http or https URL."];
        }
        else if (!UrlNormalizer.FitsMaxLength(normalizedUrl))
        {
            errors[nameof(request.Url)] = [$"Url must be at most {UrlNormalizer.MaxUrlLength} characters."];
        }

        if (request.MaxDepth is < CrawlLimitsOptions.MinDepth or > CrawlLimitsOptions.MaxDepth)
        {
            errors[nameof(request.MaxDepth)] = [$"MaxDepth must be between {CrawlLimitsOptions.MinDepth} and {CrawlLimitsOptions.MaxDepth}."];
        }

        return errors;
    }

    private static Uri? ParseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && UrlNormalizer.IsHttp(parsed)
            ? UrlNormalizer.Normalize(parsed, parsed.AbsoluteUri)
            : null;
}
