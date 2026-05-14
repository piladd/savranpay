namespace BankTransfers.Infrastructure.Auth;

public static class SavranPayRole
{
    public const string Customer = "Customer";
    public const string SupportOperator = "SupportOperator";
    public const string AmlOfficer = "AmlOfficer";
    public const string FraudOfficer = "FraudOfficer";
    public const string Admin = "Admin";
    public const string Auditor = "Auditor";

    public static readonly string[] All =
    [
        Customer,
        SupportOperator,
        AmlOfficer,
        FraudOfficer,
        Admin,
        Auditor
    ];
}
