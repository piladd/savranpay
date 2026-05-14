namespace BankTransfers.Infrastructure.Persistence.Entities;

public sealed class RoleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<UserRoleEntity> Users { get; set; } = [];
}
