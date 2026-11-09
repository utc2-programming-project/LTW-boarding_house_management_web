using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRegistrationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceRegistrationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceCatalogId = table.Column<int>(type: "int", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRegistrationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrationRequests_ContractId",
                table: "ServiceRegistrationRequests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrationRequests_Status",
                table: "ServiceRegistrationRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RegistrationId",
                table: "Invoices");
        }
    }
}
