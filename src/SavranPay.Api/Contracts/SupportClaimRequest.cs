namespace SavranPay.Api.Contracts;

public sealed record SupportClaimRequest(
    string Category,
    string Comment,
    string ContactComment,
    string AssignedTo,
    string Status);
