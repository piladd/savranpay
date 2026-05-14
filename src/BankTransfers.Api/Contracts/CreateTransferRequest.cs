using BankTransfers.Application.Transfers.CreateTransfer;

namespace BankTransfers.Api.Contracts;

public sealed record CreateTransferRequest(
    Guid FromAccountId,
    RecipientDto Recipient,
    MoneyDto Amount,
    string Purpose);
