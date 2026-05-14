using System;
using SavranPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SavranPay.Infrastructure.Migrations;

[DbContext(typeof(SavranPayDbContext))]
[Migration("202605140001_InitialSavranPaySchema")]
public partial class InitialSavranPaySchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AvailableMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                ReservedMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_accounts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "audit_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_audit_events", x => x.Id));

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                MessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_inbox_messages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ledger",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DebitMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                CreditMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ledger", x => x.Id));

        migrationBuilder.CreateTable(
            name: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Recipient = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_notifications", x => x.Id));

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_outbox_messages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_roles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "transfers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                FromAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RecipientAccountNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RecipientBankBic = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                RecipientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Purpose = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_transfers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Login = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                table.ForeignKey("FK_refresh_tokens_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                table.ForeignKey("FK_user_roles_roles_RoleId", x => x.RoleId, "roles", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_user_roles_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_accounts_Number", "accounts", "Number", unique: true);
        migrationBuilder.CreateIndex("IX_inbox_messages_Source_MessageId", "inbox_messages", new[] { "Source", "MessageId" }, unique: true);
        migrationBuilder.CreateIndex("IX_outbox_messages_DispatchedAt", "outbox_messages", "DispatchedAt");
        migrationBuilder.CreateIndex("IX_refresh_tokens_TokenHash", "refresh_tokens", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_refresh_tokens_UserId", "refresh_tokens", "UserId");
        migrationBuilder.CreateIndex("IX_roles_Name", "roles", "Name", unique: true);
        migrationBuilder.CreateIndex("IX_transfers_CustomerId_IdempotencyKey", "transfers", new[] { "CustomerId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_user_roles_RoleId", "user_roles", "RoleId");
        migrationBuilder.CreateIndex("IX_users_Login", "users", "Login", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("accounts");
        migrationBuilder.DropTable("audit_events");
        migrationBuilder.DropTable("inbox_messages");
        migrationBuilder.DropTable("ledger");
        migrationBuilder.DropTable("notifications");
        migrationBuilder.DropTable("outbox_messages");
        migrationBuilder.DropTable("refresh_tokens");
        migrationBuilder.DropTable("transfers");
        migrationBuilder.DropTable("user_roles");
        migrationBuilder.DropTable("roles");
        migrationBuilder.DropTable("users");
    }
}
