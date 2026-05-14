using BankTransfers.Domain.ValueObjects;
using BankTransfers.SharedKernel;

namespace BankTransfers.Domain.Transfers;

public sealed class TransferOrder : Entity
{
    private TransferOrder(
        Guid id,
        Guid customerId,
        Guid fromAccountId,
        Recipient recipient,
        Money amount,
        string purpose,
        IdempotencyKey idempotencyKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        FromAccountId = fromAccountId;
        Recipient = recipient;
        Amount = amount;
        Purpose = purpose;
        IdempotencyKey = idempotencyKey;
        Status = TransferStatus.Draft;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid FromAccountId { get; private set; }
    public Recipient Recipient { get; private set; }
    public Money Amount { get; private set; }
    public string Purpose { get; private set; }
    public IdempotencyKey IdempotencyKey { get; private set; }
    public TransferStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static TransferOrder Create(
        Guid customerId,
        Guid fromAccountId,
        Recipient recipient,
        Money amount,
        string purpose,
        IdempotencyKey idempotencyKey,
        DateTimeOffset createdAt)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (fromAccountId == Guid.Empty)
        {
            throw new ArgumentException("Source account id is required.", nameof(fromAccountId));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Payment purpose is required.", nameof(purpose));
        }

        if (amount.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Transfer amount must be positive.");
        }

        var transfer = new TransferOrder(
            Guid.NewGuid(),
            customerId,
            fromAccountId,
            recipient,
            amount,
            purpose.Trim(),
            idempotencyKey,
            createdAt);

        transfer.AddDomainEvent(new TransferCreatedDomainEvent(transfer.Id, createdAt));
        return transfer;
    }

    public static TransferOrder Rehydrate(
        Guid id,
        Guid customerId,
        Guid fromAccountId,
        Recipient recipient,
        Money amount,
        string purpose,
        IdempotencyKey idempotencyKey,
        TransferStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        var transfer = new TransferOrder(
            id,
            customerId,
            fromAccountId,
            recipient,
            amount,
            purpose,
            idempotencyKey,
            createdAt)
        {
            Status = status,
            UpdatedAt = updatedAt
        };

        return transfer;
    }

    public void SubmitForValidation(DateTimeOffset now)
    {
        MoveTo(TransferStatus.Draft, TransferStatus.PendingValidation, now);
    }

    public void MarkValidationPassed(DateTimeOffset now)
    {
        MoveTo(TransferStatus.PendingValidation, TransferStatus.PendingRiskCheck, now);
    }

    public void MarkRiskCheckPassed(DateTimeOffset now)
    {
        MoveTo(TransferStatus.PendingRiskCheck, TransferStatus.PendingClientConfirmation, now);
    }

    public void Confirm(DateTimeOffset now)
    {
        MoveTo(TransferStatus.PendingClientConfirmation, TransferStatus.Accepted, now);
    }

    public void Reserve(DateTimeOffset now)
    {
        MoveTo(TransferStatus.Accepted, TransferStatus.Reserved, now);
    }

    public void StartProcessing(DateTimeOffset now)
    {
        MoveTo(TransferStatus.Reserved, TransferStatus.Processing, now);
    }

    public void Settle(DateTimeOffset now)
    {
        MoveTo(TransferStatus.Processing, TransferStatus.Settled, now);
    }

    public void MarkDisputed(DateTimeOffset now)
    {
        if (Status is TransferStatus.Draft or TransferStatus.Cancelled)
        {
            throw new InvalidOperationException("Transfer cannot be disputed in the current state.");
        }

        Status = TransferStatus.Disputed;
        UpdatedAt = now;
    }

    public void Fail(DateTimeOffset now)
    {
        if (Status is TransferStatus.Settled or TransferStatus.Reversed)
        {
            throw new InvalidOperationException("Completed transfer cannot be failed.");
        }

        Status = TransferStatus.Failed;
        UpdatedAt = now;
    }

    private void MoveTo(TransferStatus expected, TransferStatus next, DateTimeOffset now)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Invalid transfer state. Expected {expected}, actual {Status}.");
        }

        Status = next;
        UpdatedAt = now;
    }
}
