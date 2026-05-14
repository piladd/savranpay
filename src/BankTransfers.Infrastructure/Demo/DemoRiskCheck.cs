namespace BankTransfers.Infrastructure.Demo;

public sealed record DemoRiskCheck(
    Guid Id,
    Guid TransferId,
    string CheckType,
    string Decision,
    string Details,
    DateTimeOffset CreatedAt);
