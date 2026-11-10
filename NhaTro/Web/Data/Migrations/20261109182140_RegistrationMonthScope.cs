using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationMonthScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId",
                table: "ContractServiceRegistrations");

            migrationBuilder.AddColumn<DateOnly>(
                name: "MonthStart",
                table: "ContractServiceRegistrations",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId_MonthStart",
                table: "ContractServiceRegistrations",
                columns: new[] { "ContractId", "ServiceCatalogId", "MonthStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId_MonthStart",
                table: "ContractServiceRegistrations");

            migrationBuilder.DropColumn(
                name: "MonthStart",
                table: "ContractServiceRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId",
                table: "ContractServiceRegistrations",
                columns: new[] { "ContractId", "ServiceCatalogId" },
                unique: true);
        }
    }
}
