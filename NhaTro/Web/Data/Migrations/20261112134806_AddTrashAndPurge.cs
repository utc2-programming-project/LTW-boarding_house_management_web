using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrashAndPurge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId_MonthStart",
                table: "ContractServiceRegistrations");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StoredFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "StoredFiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "MaintenanceIncidents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "MaintenanceIncidents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MaintenanceIncidents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Invoices",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ContractServiceRegistrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ContractServiceRegistrations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ContractServiceRegistrations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Contracts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_IsDeleted_DeletedAt",
                table: "StoredFiles",
                columns: new[] { "IsDeleted", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIncidents_IsDeleted",
                table: "MaintenanceIncidents",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices",
                columns: new[] { "ContractId", "PeriodStart" },
                unique: true,
                filter: "[RegistrationId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IsDeleted",
                table: "Invoices",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId_MonthStart",
                table: "ContractServiceRegistrations",
                columns: new[] { "ContractId", "ServiceCatalogId", "MonthStart" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_IsDeleted",
                table: "Contracts",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_IsDeleted_DeletedAt",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceIncidents_IsDeleted",
                table: "MaintenanceIncidents");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_IsDeleted",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId_MonthStart",
                table: "ContractServiceRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_IsDeleted",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "MaintenanceIncidents");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "MaintenanceIncidents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MaintenanceIncidents");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ContractServiceRegistrations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ContractServiceRegistrations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ContractServiceRegistrations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices",
                columns: new[] { "ContractId", "PeriodStart" },
                unique: true,
                filter: "[RegistrationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId_MonthStart",
                table: "ContractServiceRegistrations",
                columns: new[] { "ContractId", "ServiceCatalogId", "MonthStart" },
                unique: true);
        }
    }
}
