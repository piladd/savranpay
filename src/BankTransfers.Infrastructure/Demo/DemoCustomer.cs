namespace BankTransfers.Infrastructure.Demo;

public sealed record DemoCustomer(
    Guid Id,
    string FullName,
    string Phone,
    string Email,
    string IdentificationStatus,
    string AmlRiskLevel,
    bool IsBlocked);
