using SavranPay.Domain.ValueObjects;
using Xunit;

namespace SavranPay.UnitTests.Unit;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1, "RUB"));
    }

    [Fact]
    public void Constructor_NormalizesCurrency()
    {
        var money = new Money(100, "rub");

        Assert.Equal("RUB", money.Currency);
    }
}
