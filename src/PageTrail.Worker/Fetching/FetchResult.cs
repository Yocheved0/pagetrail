namespace PageTrail.Worker.Fetching;

public enum FetchOutcome
{
    Html,
    NotHtml,
    Disallowed,
    Failed,
}

public sealed record FetchResult(FetchOutcome Outcome, Uri FinalUrl, string? Html = null, string? FailureReason = null);
