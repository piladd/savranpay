namespace SavranPay.SharedKernel;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
