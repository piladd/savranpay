using SavranPay.Application.Transfers.CreateTransfer;

namespace SavranPay.Api.Contracts;

public sealed record CreateTransferRequest(
    Guid FromAccountId,
    RecipientDto Recipient,
    MoneyDto Amount,
    string Purpose);
