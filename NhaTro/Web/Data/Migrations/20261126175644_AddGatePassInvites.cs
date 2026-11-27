using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGatePassInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowGuestSelfFill",
                table: "GatePolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GuestSelfMaxPassesPerDay",
                table: "GatePolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GatePassInviteId",
                table: "GatePasses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GatePassInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IssuerRole = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UsedGatePassId = table.Column<int>(type: "int", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatePassInvites", x => x.Id);
                    table.CheckConstraint("CK_GatePassInvites_Issuer", "[IssuerRole] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_GatePassInvites_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePassInvites_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_GatePassInviteId",
                table: "GatePasses",
                column: "GatePassInviteId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassInvites_ExpiresAt",
                table: "GatePassInvites",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassInvites_PropertyId_CreatedAt",
                table: "GatePassInvites",
                columns: new[] { "PropertyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePassInvites_RoomId",
                table: "GatePassInvites",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassInvites_TokenHash",
                table: "GatePassInvites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatePassInvites");

            migrationBuilder.DropIndex(
                name: "IX_GatePasses_GatePassInviteId",
                table: "GatePasses");

            migrationBuilder.DropColumn(
                name: "AllowGuestSelfFill",
                table: "GatePolicies");

            migrationBuilder.DropColumn(
                name: "GuestSelfMaxPassesPerDay",
                table: "GatePolicies");

            migrationBuilder.DropColumn(
                name: "GatePassInviteId",
                table: "GatePasses");
        }
    }
}
