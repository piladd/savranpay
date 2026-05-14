using BankTransfers.SharedKernel;

namespace BankTransfers.Domain.Transfers;

public sealed record TransferCreatedDomainEvent(Guid TransferId, DateTimeOffset OccurredAt) : IDomainEvent;
