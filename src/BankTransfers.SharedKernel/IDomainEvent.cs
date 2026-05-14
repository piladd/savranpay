namespace BankTransfers.SharedKernel;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
