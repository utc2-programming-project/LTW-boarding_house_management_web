using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantOwnerLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantOwnerLinks",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantOwnerLinks", x => new { x.TenantId, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_TenantOwnerLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantOwnerLinks_OwnerId",
                table: "TenantOwnerLinks",
                column: "OwnerId");

            // Chuyển dữ liệu cũ sang liên kết nhiều-nhiều trước khi bỏ cột Tenants.OwnerId.
            migrationBuilder.Sql("""
                INSERT INTO TenantOwnerLinks (TenantId, OwnerId, CreatedAt)
                SELECT t.Id, t.OwnerId, SYSUTCDATETIME()
                FROM Tenants t
                WHERE t.OwnerId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM TenantOwnerLinks l
                      WHERE l.TenantId = t.Id AND l.OwnerId = t.OwnerId);
                """);

            // Bổ sung liên kết từ hợp đồng đã có (khách thuê của chủ trọ khác vẫn thấy được).
            migrationBuilder.Sql("""
                INSERT INTO TenantOwnerLinks (TenantId, OwnerId, CreatedAt)
                SELECT DISTINCT cm.TenantId, p.OwnerId, SYSUTCDATETIME()
                FROM ContractMembers cm
                JOIN Contracts c ON c.ContractId = cm.ContractId
                JOIN Rooms r ON r.Id = c.RoomId
                JOIN Properties p ON p.Id = r.PropertyId
                WHERE NOT EXISTS (
                    SELECT 1 FROM TenantOwnerLinks l
                    WHERE l.TenantId = cm.TenantId AND l.OwnerId = p.OwnerId);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Tenants_OwnerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantOwnerLinks");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Tenants",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OwnerId",
                table: "Tenants",
                column: "OwnerId");
        }
    }
}
