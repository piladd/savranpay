namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class UserSessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public Guid RefreshTokenId { get; set; }
    public RefreshTokenEntity RefreshToken { get; set; } = null!;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
