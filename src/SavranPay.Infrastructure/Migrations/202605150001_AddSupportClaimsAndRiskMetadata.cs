using System;
using SavranPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SavranPay.Infrastructure.Migrations;

[DbContext(typeof(SavranPayDbContext))]
[Migration("202605150001_AddSupportClaimsAndRiskMetadata")]
public partial class AddSupportClaimsAndRiskMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeviceFingerprint",
            table: "risk_checks",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "IpAddress",
            table: "risk_checks",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "RiskFactors",
            table: "risk_checks",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "BlockReason",
            table: "risk_checks",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "DocumentsRequested",
            table: "risk_checks",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "StepUpRequired",
            table: "risk_checks",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "support_claims",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Category = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                AssignedTo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Comment = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                ContactComment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_support_claims", x => x.Id));

        migrationBuilder.CreateTable(
            name: "support_claim_comments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SupportClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                AuthorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_claim_comments", x => x.Id);
                table.ForeignKey("FK_support_claim_comments_support_claims_SupportClaimId", x => x.SupportClaimId, "support_claims", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_support_claims_CustomerId", "support_claims", "CustomerId");
        migrationBuilder.CreateIndex("IX_support_claims_TransferId", "support_claims", "TransferId");
        migrationBuilder.CreateIndex("IX_support_claim_comments_SupportClaimId", "support_claim_comments", "SupportClaimId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("support_claim_comments");
        migrationBuilder.DropTable("support_claims");
        migrationBuilder.DropColumn("DeviceFingerprint", "risk_checks");
        migrationBuilder.DropColumn("IpAddress", "risk_checks");
        migrationBuilder.DropColumn("RiskFactors", "risk_checks");
        migrationBuilder.DropColumn("BlockReason", "risk_checks");
        migrationBuilder.DropColumn("DocumentsRequested", "risk_checks");
        migrationBuilder.DropColumn("StepUpRequired", "risk_checks");
    }
}
