namespace BankTransfers.Application.Transfers.CreateTransfer;

public sealed record CreateTransferCommand(
    Guid CustomerId,
    Guid FromAccountId,
    RecipientDto Recipient,
    MoneyDto Amount,
    string Purpose,
    string IdempotencyKey);
