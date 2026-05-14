using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Domain.ValueObjects;
using SavranPay.SharedKernel;

namespace SavranPay.Application.Transfers.CreateTransfer;

public sealed class CreateTransferHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ITransferOrderRepository _transfers;
    private readonly ILimitService _limits;
    private readonly IAmlService _aml;
    private readonly IFraudService _fraud;
    private readonly IAuditService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateTransferHandler(
        IAccountRepository accounts,
        ITransferOrderRepository transfers,
        ILimitService limits,
        IAmlService aml,
        IFraudService fraud,
        IAuditService audit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _accounts = accounts;
        _transfers = transfers;
        _limits = limits;
        _aml = aml;
        _fraud = fraud;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<CreateTransferResult> Handle(CreateTransferCommand command, CancellationToken cancellationToken)
    {
        var idempotencyKey = new IdempotencyKey(command.IdempotencyKey);
        var existingTransfer = await _transfers.FindByIdempotencyKeyAsync(
            command.CustomerId,
            idempotencyKey,
            cancellationToken);

        if (existingTransfer is not null)
        {
            return new CreateTransferResult(existingTransfer.Id, existingTransfer.Status);
        }

        var account = await _accounts.GetByIdAsync(command.FromAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Source account was not found.");

        if (account.CustomerId != command.CustomerId)
        {
            throw new InvalidOperationException("Source account does not belong to the current customer.");
        }

        var recipient = Recipient.Create(
            command.Recipient.Type,
            command.Recipient.AccountNumber,
            command.Recipient.BankBic,
            command.Recipient.Name);

        var amount = new Money(command.Amount.MinorUnits, command.Amount.Currency);
        var transfer = TransferOrder.Create(
            command.CustomerId,
            command.FromAccountId,
            recipient,
            amount,
            command.Purpose,
            idempotencyKey,
            _clock.UtcNow);

        transfer.SubmitForValidation(_clock.UtcNow);

        if (!account.CanDebit(amount))
        {
            transfer.Fail(_clock.UtcNow);
            await _transfers.AddAsync(transfer, cancellationToken);
            await _audit.WriteAsync(transfer.Id, "ValidationFailed", "Insufficient funds or inactive account.", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new CreateTransferResult(transfer.Id, transfer.Status);
        }

        await _limits.EnsureTransferAllowedAsync(transfer, cancellationToken);
        transfer.MarkValidationPassed(_clock.UtcNow);

        var amlDecision = await _aml.CheckAsync(transfer, cancellationToken);
        var fraudDecision = await _fraud.CheckAsync(transfer, cancellationToken);

        if (amlDecision == AmlDecision.Blocked || fraudDecision == FraudDecision.CriticalRisk)
        {
            transfer.Fail(_clock.UtcNow);
            await _transfers.AddAsync(transfer, cancellationToken);
            await _audit.WriteAsync(transfer.Id, "RiskCheckFailed", $"AML={amlDecision}; Fraud={fraudDecision}.", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new CreateTransferResult(transfer.Id, transfer.Status);
        }

        transfer.MarkRiskCheckPassed(_clock.UtcNow);
        await _transfers.AddAsync(transfer, cancellationToken);
        await _audit.WriteAsync(transfer.Id, "TransferCreated", $"AML={amlDecision}; Fraud={fraudDecision}.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTransferResult(transfer.Id, transfer.Status);
    }
}
