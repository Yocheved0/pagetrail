namespace PageTrail.Worker.Fetching;

public enum FetchOutcome
{
    Html,
    NotHtml,
    Failed,
}

public sealed record FetchResult(FetchOutcome Outcome, Uri FinalUrl, string? Html = null, string? FailureReason = null);
