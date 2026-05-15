using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace SavranPay.Infrastructure.Auth;

public sealed record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    object User);

public sealed record SessionView(
    Guid Id,
    string IpAddress,
    string UserAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset RefreshTokenExpiresAt);

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

    public async Task<LoginResult?> LoginAsync(
        string login,
        string password,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(item => item.Roles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Login == login, cancellationToken);

        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            AddAudit(Guid.Empty, "LoginFailed", $"Login rejected for {login} from {ipAddress}.");
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        AddAudit(user.Id, "LoginSucceeded", $"User {user.Login} signed in from {ipAddress}.");
        return await IssueTokensAsync(user, ipAddress, userAgent, cancellationToken);
    }

    public async Task<LoginResult?> RefreshAsync(
        string refreshToken,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken)
    {
        var tokenHash = JwtTokenService.HashRefreshToken(refreshToken);
        var token = await _db.RefreshTokens
            .Include(item => item.Session)
            .Include(item => item.User)
            .ThenInclude(item => item.Roles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow || !token.User.IsActive)
        {
            AddAudit(Guid.Empty, "RefreshFailed", $"Refresh token rejected from {ipAddress}.");
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        if (token.Session is not null)
        {
            token.Session.LastSeenAt = DateTimeOffset.UtcNow;
            token.Session.RevokedAt = DateTimeOffset.UtcNow;
        }

        AddAudit(token.UserId, "RefreshSucceeded", $"Refresh token rotated from {ipAddress}.");
        return await IssueTokensAsync(token.User, ipAddress, userAgent, cancellationToken);
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = JwtTokenService.HashRefreshToken(refreshToken);
        var token = await _db.RefreshTokens
            .Include(item => item.Session)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            AddAudit(Guid.Empty, "LogoutFailed", "Logout requested for an unknown refresh token.");
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        token.RevokedAt ??= now;
        if (token.Session is not null)
        {
            token.Session.RevokedAt ??= now;
            token.Session.LastSeenAt = now;
        }

        AddAudit(token.UserId, "LogoutSucceeded", "Refresh token and session were revoked.");
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(item => item.Roles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        return user is null ? null : ToUserView(user, user.Roles.Select(item => item.Role.Name).Order().ToArray());
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(item => item.RefreshTokens)
            .Include(item => item.Sessions)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null || !PasswordHasher.Verify(currentPassword, user.PasswordHash))
        {
            AddAudit(userId, "PasswordChangeFailed", "Current password verification failed.");
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        var now = DateTimeOffset.UtcNow;
        foreach (var token in user.RefreshTokens.Where(item => item.RevokedAt is null))
        {
            token.RevokedAt = now;
        }

        foreach (var session in user.Sessions.Where(item => item.RevokedAt is null))
        {
            session.RevokedAt = now;
            session.LastSeenAt = now;
        }

        AddAudit(user.Id, "PasswordChanged", "Password changed and active refresh sessions were revoked.");
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SessionView>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.UserSessions
            .AsNoTracking()
            .Include(item => item.RefreshToken)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.LastSeenAt)
            .Select(item => new SessionView(
                item.Id,
                item.IpAddress,
                item.UserAgent,
                item.CreatedAt,
                item.LastSeenAt,
                item.RevokedAt,
                item.RefreshToken.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<LoginResult> IssueTokensAsync(
        UserEntity user,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken)
    {
        var roles = user.Roles.Select(item => item.Role.Name).Order().ToArray();
        var accessToken = _tokens.CreateAccessToken(user.Id, user.CustomerId, user.Login, roles);
        var refreshToken = JwtTokenService.CreateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);
        var refreshTokenId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = refreshTokenId,
            UserId = user.Id,
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        _db.UserSessions.Add(new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenId = refreshTokenId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = now,
            LastSeenAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            accessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            refreshExpiresAt,
            ToUserView(user, roles));
    }

    private static object ToUserView(UserEntity user, IReadOnlyCollection<string> roles) => new
    {
        user.Id,
        user.Login,
        user.FullName,
        user.Email,
        user.Phone,
        user.CustomerId,
        Roles = roles
    };

    private void AddAudit(Guid operationId, string eventType, string message)
    {
        var now = DateTimeOffset.UtcNow;
        _db.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            EventType = eventType,
            Message = message,
            CreatedAt = now
        });
        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = $"Audit.{eventType}",
            Payload = JsonSerializer.Serialize(new { operationId, eventType, message }),
            CreatedAt = now
        });
    }
}
