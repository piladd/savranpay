namespace SavranPay.Infrastructure.Demo;

public sealed record DemoLedgerEntry(
    Guid Id,
    Guid TransferId,
    string AccountNumber,
    long DebitMinorUnits,
    long CreditMinorUnits,
    string Currency,
    DateTimeOffset CreatedAt);
