using PageTrail.Domain.Pages;

namespace PageTrail.Worker.Processing;

public sealed record PageOutcome(IReadOnlyList<PageLink> Links, IReadOnlyList<Uri> ChildCandidates, string? FailureReason)
{
    public static PageOutcome WithoutLinks(string? failureReason = null) => new([], [], failureReason);
}
