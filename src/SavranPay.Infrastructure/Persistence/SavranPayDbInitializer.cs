using SavranPay.Domain.Accounts;
using SavranPay.Infrastructure.Auth;
using SavranPay.Infrastructure.Demo;
using SavranPay.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace SavranPay.Infrastructure.Persistence;

public static class SavranPayDbInitializer
{
    public static async Task InitializeAsync(SavranPayDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);

        foreach (var roleName in SavranPayRole.All)
        {
            if (!await db.Roles.AnyAsync(role => role.Name == roleName, cancellationToken))
            {
                db.Roles.Add(new RoleEntity { Id = Guid.NewGuid(), Name = roleName });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        await EnsureUserAsync(db, "client@savranpay.local", "Client123!", "Иван Петров", "ivan.petrov@example.test", "+7 900 000-00-00", DemoBankStore.DemoCustomerId, [SavranPayRole.Customer], cancellationToken);
        await EnsureUserAsync(db, "support@savranpay.local", "Support123!", "Support Operator", "support@savranpay.local", "+7 900 000-00-01", null, [SavranPayRole.SupportOperator], cancellationToken);
        await EnsureUserAsync(db, "aml@savranpay.local", "Aml123!", "AML Officer", "aml@savranpay.local", "+7 900 000-00-02", null, [SavranPayRole.AmlOfficer], cancellationToken);
        await EnsureUserAsync(db, "fraud@savranpay.local", "Fraud123!", "Fraud Officer", "fraud@savranpay.local", "+7 900 000-00-03", null, [SavranPayRole.FraudOfficer], cancellationToken);
        await EnsureUserAsync(db, "admin@savranpay.local", "Admin123!", "Administrator", "admin@savranpay.local", "+7 900 000-00-04", null, [SavranPayRole.Admin], cancellationToken);
        await EnsureUserAsync(db, "audit@savranpay.local", "Audit123!", "Auditor", "audit@savranpay.local", "+7 900 000-00-05", null, [SavranPayRole.Auditor], cancellationToken);

        if (!await db.Accounts.AnyAsync(cancellationToken))
        {
            db.Accounts.AddRange(
                new AccountEntity
                {
                    Id = DemoBankStore.PrimaryAccountId,
                    CustomerId = DemoBankStore.DemoCustomerId,
                    Number = "40817810000000000001",
                    AvailableMinorUnits = 1_250_000_00,
                    ReservedMinorUnits = 0,
                    Currency = "RUB",
                    Status = AccountStatus.Active.ToString()
                },
                new AccountEntity
                {
                    Id = DemoBankStore.SavingsAccountId,
                    CustomerId = DemoBankStore.DemoCustomerId,
                    Number = "40817810000000000002",
                    AvailableMinorUnits = 480_000_00,
                    ReservedMinorUnits = 0,
                    Currency = "RUB",
                    Status = AccountStatus.Active.ToString()
                });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureUserAsync(
        SavranPayDbContext db,
        string login,
        string password,
        string fullName,
        string email,
        string phone,
        Guid? customerId,
        string[] roleNames,
        CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(user => user.Login == login, cancellationToken))
        {
            return;
        }

        var roles = await db.Roles.Where(role => roleNames.Contains(role.Name)).ToListAsync(cancellationToken);
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Login = login,
            PasswordHash = PasswordHasher.Hash(password),
            FullName = fullName,
            Email = email,
            Phone = phone,
            CustomerId = customerId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        foreach (var role in roles)
        {
            user.Roles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
        }

        db.Users.Add(user);
    }
}
