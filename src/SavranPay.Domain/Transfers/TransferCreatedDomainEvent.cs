using SavranPay.SharedKernel;

namespace SavranPay.Domain.Transfers;

public sealed record TransferCreatedDomainEvent(Guid TransferId, DateTimeOffset OccurredAt) : IDomainEvent;
