namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public List<UserRoleEntity> Roles { get; set; } = [];
    public List<RefreshTokenEntity> RefreshTokens { get; set; } = [];
    public List<UserSessionEntity> Sessions { get; set; } = [];
}
