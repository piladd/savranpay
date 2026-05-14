using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SavranPay.Infrastructure.Persistence;

public sealed class SavranPayDbContextFactory : IDesignTimeDbContextFactory<SavranPayDbContext>
{
    public SavranPayDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SavranPayDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=savranpay;Username=savranpay;Password=change-me")
            .Options;

        return new SavranPayDbContext(options);
    }
}
