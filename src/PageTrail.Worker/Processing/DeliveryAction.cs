namespace PageTrail.Worker.Processing;

public enum DeliveryAction
{
    Ack,
    Retry,
    DeadLetter,
}
