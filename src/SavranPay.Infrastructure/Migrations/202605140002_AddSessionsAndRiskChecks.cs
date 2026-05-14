using System;
using SavranPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SavranPay.Infrastructure.Migrations;

[DbContext(typeof(SavranPayDbContext))]
[Migration("202605140002_AddSessionsAndRiskChecks")]
public partial class AddSessionsAndRiskChecks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "risk_checks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                CheckType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Decision = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                Details = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_risk_checks", x => x.Id));

        migrationBuilder.CreateTable(
            name: "user_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_sessions", x => x.Id);
                table.ForeignKey("FK_user_sessions_refresh_tokens_RefreshTokenId", x => x.RefreshTokenId, "refresh_tokens", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_user_sessions_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_risk_checks_TransferId", "risk_checks", "TransferId");
        migrationBuilder.CreateIndex("IX_user_sessions_RefreshTokenId", "user_sessions", "RefreshTokenId", unique: true);
        migrationBuilder.CreateIndex("IX_user_sessions_UserId", "user_sessions", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("risk_checks");
        migrationBuilder.DropTable("user_sessions");
    }
}
