namespace BankTransfers.Infrastructure.Auth;

using Microsoft.AspNetCore.Http;

public sealed class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public JwtAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, JwtTokenService tokens)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var principal = tokens.Validate(authorization["Bearer ".Length..].Trim());
            if (principal is not null)
            {
                context.User = principal;
            }
        }

        await _next(context);
    }
}
