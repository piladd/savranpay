using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BankTransfers.Infrastructure.Auth;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateAccessToken(Guid userId, Guid? customerId, string login, IReadOnlyCollection<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["sub"] = userId.ToString(),
            ["name"] = login,
            ["customer_id"] = customerId?.ToString(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(_options.AccessTokenMinutes).ToUnixTimeSeconds(),
            ["role"] = roles
        };

        return Sign(payload);
    }

    public ClaimsPrincipal? Validate(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var expectedSignature = SignBytes($"{parts[0]}.{parts[1]}");
        var actualSignature = Base64UrlDecode(parts[2]);
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(Base64UrlDecode(parts[1]));
        if (!payload.TryGetProperty("iss", out var issuer) || issuer.GetString() != _options.Issuer ||
            !payload.TryGetProperty("aud", out var audience) || audience.GetString() != _options.Audience ||
            !payload.TryGetProperty("exp", out var exp) || exp.GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }

        var claims = new List<Claim>();
        AddClaim(payload, claims, "sub", ClaimTypes.NameIdentifier);
        AddClaim(payload, claims, "name", ClaimTypes.Name);
        AddClaim(payload, claims, "customer_id", "customer_id");

        if (payload.TryGetProperty("role", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            claims.AddRange(roles.EnumerateArray().Select(role => new Claim(ClaimTypes.Role, role.GetString() ?? string.Empty)));
        }

        var identity = new ClaimsIdentity(claims, "SavranPayJwt", ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    public static string CreateRefreshToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    public static string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string Sign(Dictionary<string, object?> payload)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var headerPart = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadPart = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signaturePart = Base64UrlEncode(SignBytes($"{headerPart}.{payloadPart}"));
        return $"{headerPart}.{payloadPart}.{signaturePart}";
    }

    private byte[] SignBytes(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        return hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
    }

    private static void AddClaim(JsonElement payload, List<Claim> claims, string source, string target)
    {
        if (payload.TryGetProperty(source, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                claims.Add(new Claim(target, text));
            }
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
