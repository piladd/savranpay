namespace SavranPay.Infrastructure.Crypto;

public sealed class ProductionCryptoOptions
{
    public string Provider { get; set; } = "HSM/KMS";
    public string KeyId { get; set; } = string.Empty;
    public string GostProviderName { get; set; } = string.Empty;
    public bool WebAuthnRequired { get; set; } = true;
}
