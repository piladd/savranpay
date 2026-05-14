using System.Security.Cryptography;
using System.Text;
using BankTransfers.Domain.Transfers;
using BankTransfers.Domain.ValueObjects;
using BankTransfers.Infrastructure.Crypto;
using Xunit;

namespace BankTransfers.SecurityTests;

public sealed class SecurityChecklistTests
{
    [Fact]
    public async Task ReplayAttack_WithReusedNonce_IsRejected()
    {
        var crypto = new DemoCryptoService();
        var transfer = CreateTransfer();
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow;
        var signature = Sign(crypto.CreateTransferPayload(transfer, nonce, timestamp));

        var firstResult = await crypto.VerifyTransferSignatureAsync(transfer, signature, nonce, timestamp, CancellationToken.None);
        var replayResult = await crypto.VerifyTransferSignatureAsync(transfer, signature, nonce, timestamp, CancellationToken.None);

        Assert.True(firstResult);
        Assert.False(replayResult);
    }

    [Fact]
    public async Task PayloadTampering_WithDifferentNonce_IsRejected()
    {
        var crypto = new DemoCryptoService();
        var transfer = CreateTransfer();
        var timestamp = DateTimeOffset.UtcNow;
        var originalNonce = Guid.NewGuid().ToString("N");
        var tamperedNonce = Guid.NewGuid().ToString("N");
        var signature = Sign(crypto.CreateTransferPayload(transfer, originalNonce, timestamp));

        var result = await crypto.VerifyTransferSignatureAsync(transfer, signature, tamperedNonce, timestamp, CancellationToken.None);

        Assert.False(result);
    }

    private static TransferOrder CreateTransfer()
    {
        var transfer = TransferOrder.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipient.Create("Account", "40817810000000000002", "044525225", "Иван Петров"),
            Money.Rub(150000),
            "Перевод собственных средств",
            new IdempotencyKey(Guid.NewGuid().ToString("N")),
            DateTimeOffset.UtcNow);

        transfer.SubmitForValidation(DateTimeOffset.UtcNow);
        transfer.MarkValidationPassed(DateTimeOffset.UtcNow);
        transfer.MarkRiskCheckPassed(DateTimeOffset.UtcNow);
        return transfer;
    }

    private static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(DemoCryptoService.DemoSharedSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
