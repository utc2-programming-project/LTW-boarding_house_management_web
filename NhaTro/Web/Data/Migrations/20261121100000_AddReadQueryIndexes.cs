using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>
    /// Index cho các truy vấn đọc nóng (danh sách thanh toán/hợp đồng/hóa đơn/khách thuê)
    /// và thu hẹp cột + index hộp thư thông báo.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261121100000_AddReadQueryIndexes")]
    public partial class AddReadQueryIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_RequestedAt",
                table: "Payments",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedAt",
                table: "Contracts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status_StartDate_EndDate",
                table: "Contracts",
                columns: new[] { "Status", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAt",
                table: "Tenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIncidents_Status_CreatedAt",
                table: "MaintenanceIncidents",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AlterColumn<string>(
                name: "RecipientUserId",
                table: "AppNotifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AppNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Link",
                table: "AppNotifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_RecipientUserId_IsRead_CreatedAt",
                table: "AppNotifications",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppNotifications_RecipientUserId_IsRead_CreatedAt",
                table: "AppNotifications");

            migrationBuilder.AlterColumn<string>(
                name: "Link",
                table: "AppNotifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AppNotifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientUserId",
                table: "AppNotifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceIncidents_Status_CreatedAt",
                table: "MaintenanceIncidents");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CreatedAt",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_Status_StartDate_EndDate",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_CreatedAt",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Status_RequestedAt",
                table: "Payments");
        }
    }
}
