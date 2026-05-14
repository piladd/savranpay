namespace SavranPay.Api.Contracts;

public sealed record CreateUserRequest(
    string Login,
    string Password,
    string FullName,
    string Email,
    string Phone,
    Guid? CustomerId,
    string[] Roles);
