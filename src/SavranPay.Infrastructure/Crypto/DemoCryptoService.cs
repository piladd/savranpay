using SavranPay.Application.Abstractions;
using SavranPay.Domain.Transfers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SavranPay.Infrastructure.Crypto;

public sealed class DemoCryptoService : ICryptoService
{
    public const string DemoSharedSecret = "savranpay-demo-secret-change-in-production";

    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _usedNonces = [];

    public string CreateTransferPayload(TransferOrder transfer, string nonce, DateTimeOffset timestamp)
    {
        var payload = new SortedDictionary<string, object?>
        {
            ["operationId"] = transfer.Id,
            ["payerAccountId"] = transfer.FromAccountId,
            ["recipientAccount"] = transfer.Recipient.AccountNumber,
            ["recipientBankBic"] = transfer.Recipient.BankBic,
            ["recipientName"] = transfer.Recipient.Name,
            ["amount"] = transfer.Amount.MinorUnits,
            ["currency"] = transfer.Amount.Currency,
            ["purpose"] = transfer.Purpose,
            ["createdAt"] = transfer.CreatedAt.ToUniversalTime().ToString("O"),
            ["timestamp"] = timestamp.ToUniversalTime().ToString("O"),
            ["nonce"] = nonce
        };

        return JsonSerializer.Serialize(payload);
    }

    public string ComputePayloadHash(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public Task<bool> VerifyTransferSignatureAsync(
        TransferOrder transfer,
        string signature,
        string nonce,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(nonce))
        {
            return Task.FromResult(false);
        }

        var now = DateTimeOffset.UtcNow;
        if (timestamp.ToUniversalTime() < now.Subtract(AllowedClockSkew) ||
            timestamp.ToUniversalTime() > now.Add(AllowedClockSkew))
        {
            return Task.FromResult(false);
        }

        var nonceKey = $"{transfer.Id:N}:{nonce}";
        if (_usedNonces.ContainsKey(nonceKey))
        {
            return Task.FromResult(false);
        }

        var payload = CreateTransferPayload(transfer, nonce, timestamp);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(DemoSharedSecret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        byte[] actualBytes;
        try
        {
            actualBytes = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }

        var isValid = actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);

        if (isValid && !_usedNonces.TryAdd(nonceKey, now))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(isValid);
    }
}
