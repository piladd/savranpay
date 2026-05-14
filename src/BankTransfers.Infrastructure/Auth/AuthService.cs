using BankTransfers.Infrastructure.Persistence;
using BankTransfers.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankTransfers.Infrastructure.Auth;

public sealed record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    object User);

public sealed class AuthService
{
    private readonly SavranPayDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly JwtOptions _options;

    public AuthService(SavranPayDbContext db, JwtTokenService tokens, IOptions<JwtOptions> options)
    {
        _db = db;
        _tokens = tokens;
        _options = options.Value;
    }

    public async Task<LoginResult?> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(item => item.Roles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Login == login, cancellationToken);

        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<LoginResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = JwtTokenService.HashRefreshToken(refreshToken);
        var token = await _db.RefreshTokens
            .Include(item => item.User)
            .ThenInclude(item => item.Roles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow || !token.User.IsActive)
        {
            return null;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        return await IssueTokensAsync(token.User, cancellationToken);
    }

    private async Task<LoginResult> IssueTokensAsync(UserEntity user, CancellationToken cancellationToken)
    {
        var roles = user.Roles.Select(item => item.Role.Name).Order().ToArray();
        var accessToken = _tokens.CreateAccessToken(user.Id, user.CustomerId, user.Login, roles);
        var refreshToken = JwtTokenService.CreateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refreshExpiresAt
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            accessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            refreshExpiresAt,
            new
            {
                user.Id,
                user.Login,
                user.FullName,
                user.Email,
                user.Phone,
                user.CustomerId,
                Roles = roles
            });
    }
}
