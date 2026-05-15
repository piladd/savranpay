using SavranPay.Domain.Transfers;
using SavranPay.Domain.ValueObjects;
using Xunit;

namespace SavranPay.UnitTests;

public sealed class TransferOrderTests
{
    [Fact]
    public void Create_RejectsZeroTransferAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransfer(Money.Zero("RUB")));
    }

    [Fact]
    public void Confirm_RejectsInvalidState()
    {
        var transfer = CreateTransfer(Money.Rub(100));

        Assert.Throws<InvalidOperationException>(() => transfer.Confirm(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkRiskCheckPassed_MovesToClientConfirmation()
    {
        var transfer = CreateTransfer(Money.Rub(100));

        transfer.SubmitForValidation(DateTimeOffset.UtcNow);
        transfer.MarkValidationPassed(DateTimeOffset.UtcNow);
        transfer.MarkRiskCheckPassed(DateTimeOffset.UtcNow);

        Assert.Equal(TransferStatus.PendingClientConfirmation, transfer.Status);
    }

    [Fact]
    public void Cancel_RejectsTransferAfterClientConfirmation()
    {
        var transfer = CreateTransfer(Money.Rub(100));

        transfer.SubmitForValidation(DateTimeOffset.UtcNow);
        transfer.MarkValidationPassed(DateTimeOffset.UtcNow);
        transfer.MarkRiskCheckPassed(DateTimeOffset.UtcNow);
        transfer.Confirm(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => transfer.Cancel(DateTimeOffset.UtcNow));
    }

    private static TransferOrder CreateTransfer(Money amount)
    {
        return TransferOrder.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipient.Create("Account", "40817810000000000001", "044525225", "Ivan Ivanov"),
            amount,
            "Transfer of own funds",
            new IdempotencyKey(Guid.NewGuid().ToString("N")),
            DateTimeOffset.UtcNow);
    }
}
