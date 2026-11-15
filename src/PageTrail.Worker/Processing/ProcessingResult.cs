namespace PageTrail.Worker.Processing;

public enum ProcessingResult
{
    Committed,
    AlreadyProcessed,
    UnknownPage,
}
