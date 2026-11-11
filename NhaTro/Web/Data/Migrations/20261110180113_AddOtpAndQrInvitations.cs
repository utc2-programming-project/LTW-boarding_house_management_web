using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpAndQrInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "TenantInvitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "TenantInvitations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryPresetMinutes",
                table: "TenantInvitations",
                type: "int",
                nullable: false,
                defaultValue: 1440);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "TenantInvitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "TenantInvitations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "TenantInvitations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetUserId",
                table: "TenantInvitations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OtpChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingRoomAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    InvitationId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRoomAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_Channel_CreatedAt",
                table: "TenantInvitations",
                columns: new[] { "Channel", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_ExpiresAt",
                table: "TenantInvitations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_Email_CreatedAt",
                table: "OtpChallenges",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_ExpiresAt",
                table: "OtpChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_UserId_Purpose_ConsumedAt",
                table: "OtpChallenges",
                columns: new[] { "UserId", "Purpose", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingRoomAssignments_InvitationId",
                table: "PendingRoomAssignments",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRoomAssignments_PropertyId",
                table: "PendingRoomAssignments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRoomAssignments_TenantId_RoomId_Status",
                table: "PendingRoomAssignments",
                columns: new[] { "TenantId", "RoomId", "Status" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpChallenges");

            migrationBuilder.DropTable(
                name: "PendingRoomAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TenantInvitations_Channel_CreatedAt",
                table: "TenantInvitations");

            migrationBuilder.DropIndex(
                name: "IX_TenantInvitations_ExpiresAt",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "ExpiryPresetMinutes",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "TenantInvitations");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "TenantInvitations");
        }
    }
}
