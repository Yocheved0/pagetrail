namespace PageTrail.Worker.Processing;

public sealed record DeliveryDecision
{
    private DeliveryDecision(DeliveryAction action, int nextAttempt)
    {
        Action = action;
        NextAttempt = nextAttempt;
    }

    public static DeliveryDecision Ack { get; } = new(DeliveryAction.Ack, 0);

    public static DeliveryDecision DeadLetter { get; } = new(DeliveryAction.DeadLetter, 0);

    public DeliveryAction Action { get; }

    public int NextAttempt { get; }

    public static DeliveryDecision Retry(int nextAttempt) => new(DeliveryAction.Retry, nextAttempt);
}
