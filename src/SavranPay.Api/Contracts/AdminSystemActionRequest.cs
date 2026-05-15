namespace SavranPay.Api.Contracts;

public sealed record AdminSystemActionRequest(string? Reason, long? LimitMinorUnits);
