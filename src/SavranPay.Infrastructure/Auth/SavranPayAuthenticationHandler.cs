using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace SavranPay.Infrastructure.Auth;

public sealed class SavranPayAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly JwtTokenService _tokens;

    public SavranPayAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtTokenService tokens)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var principal = _tokens.Validate(authorization["Bearer ".Length..].Trim());
        return Task.FromResult(principal is null
            ? AuthenticateResult.Fail("Invalid JWT.")
            : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
