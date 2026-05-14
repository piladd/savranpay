namespace SavranPay.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "SavranPay";
    public string Audience { get; set; } = "SavranPay.Web";
    public string SigningKey { get; set; } = "change-this-local-development-signing-key-32-bytes";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
    public bool RequireAuthorization { get; set; }
}
