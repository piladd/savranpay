using SavranPay.Domain.Accounts;
using SavranPay.Domain.Transfers;
using SavranPay.Domain.ValueObjects;

namespace SavranPay.Infrastructure.Demo;

public sealed class DemoBankStore
{
    public static readonly Guid DemoCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PrimaryAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SavingsAccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public DemoBankStore()
    {
        Customer = new DemoCustomer(
            DemoCustomerId,
            "Иван Петров",
            "+7 900 000-00-00",
            "ivan.petrov@example.test",
            "Полная идентификация",
            "Низкий",
            false);

        Accounts[PrimaryAccountId] = new Account(
            PrimaryAccountId,
            DemoCustomerId,
            "40817810000000000001",
            Money.Rub(1_250_000_00),
            AccountStatus.Active);

        Accounts[SavingsAccountId] = new Account(
            SavingsAccountId,
            DemoCustomerId,
            "40817810000000000002",
            Money.Rub(480_000_00),
            AccountStatus.Active);
    }

    public DemoCustomer Customer { get; }
    public Dictionary<Guid, Account> Accounts { get; } = [];
    public Dictionary<Guid, TransferOrder> Transfers { get; } = [];
    public List<DemoAuditEvent> AuditEvents { get; } = [];
    public List<DemoRiskCheck> RiskChecks { get; } = [];
    public List<DemoLedgerEntry> LedgerEntries { get; } = [];
    public List<DemoNotification> Notifications { get; } = [];
}
