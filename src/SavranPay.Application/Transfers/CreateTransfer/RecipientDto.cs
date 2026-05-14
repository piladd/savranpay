namespace SavranPay.Application.Transfers.CreateTransfer;

public sealed record RecipientDto(
    string Type,
    string AccountNumber,
    string BankBic,
    string Name);
