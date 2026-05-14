namespace BankTransfers.Infrastructure.Persistence.Entities;

public sealed class UserRoleEntity
{
    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;
}
