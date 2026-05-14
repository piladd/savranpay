namespace SavranPay.Api.Contracts;

public sealed record UpdateUserRequest(
    string? FullName,
    string? Email,
    string? Phone,
    Guid? CustomerId,
    bool? IsActive);
