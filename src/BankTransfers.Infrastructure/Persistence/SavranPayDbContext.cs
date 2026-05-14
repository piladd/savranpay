using BankTransfers.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankTransfers.Infrastructure.Persistence;

public sealed class SavranPayDbContext : DbContext
{
    public SavranPayDbContext(DbContextOptions<SavranPayDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<TransferEntity> Transfers => Set<TransferEntity>();
    public DbSet<LedgerEntryEntity> Ledger => Set<LedgerEntryEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Login).IsUnique();
            entity.Property(item => item.Login).HasMaxLength(64).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(item => item.FullName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<UserRoleEntity>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(item => new { item.UserId, item.RoleId });
            entity.HasOne(item => item.User).WithMany(item => item.Roles).HasForeignKey(item => item.UserId);
            entity.HasOne(item => item.Role).WithMany(item => item.Users).HasForeignKey(item => item.RoleId);
        });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasOne(item => item.User).WithMany(item => item.RefreshTokens).HasForeignKey(item => item.UserId);
        });

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Number).IsUnique();
            entity.Property(item => item.Number).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<TransferEntity>(entity =>
        {
            entity.ToTable("transfers");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CustomerId, item.IdempotencyKey }).IsUnique();
            entity.Property(item => item.RecipientType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RecipientAccountNumber).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RecipientBankBic).HasMaxLength(16).IsRequired();
            entity.Property(item => item.RecipientName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.Purpose).HasMaxLength(240).IsRequired();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(48).IsRequired();
        });

        modelBuilder.Entity<LedgerEntryEntity>(entity =>
        {
            entity.ToTable("ledger");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountNumber).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        });

        modelBuilder.Entity<AuditEventEntity>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(96).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1024).IsRequired();
        });

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.DispatchedAt);
            entity.Property(item => item.Type).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Payload).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<InboxMessageEntity>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Source, item.MessageId }).IsUnique();
            entity.Property(item => item.Source).HasMaxLength(80).IsRequired();
            entity.Property(item => item.MessageId).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Channel).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Recipient).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
        });
    }
}
