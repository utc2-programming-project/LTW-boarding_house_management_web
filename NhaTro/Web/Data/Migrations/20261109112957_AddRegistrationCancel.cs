using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationCancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "ServiceRegistrationRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelRejectReason",
                table: "ServiceRegistrationRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelReviewedAt",
                table: "ServiceRegistrationRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReviewedByUserId",
                table: "ServiceRegistrationRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelStatus",
                table: "ServiceRegistrationRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrationRequests_ContractId_ServiceCatalogId_StartDate",
                table: "ServiceRegistrationRequests",
                columns: new[] { "ContractId", "ServiceCatalogId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRegistrationRequests_ContractId_ServiceCatalogId_StartDate",
                table: "ServiceRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "ServiceRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "CancelRejectReason",
                table: "ServiceRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "CancelReviewedAt",
                table: "ServiceRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "CancelReviewedByUserId",
                table: "ServiceRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "CancelStatus",
                table: "ServiceRegistrationRequests");
        }
    }
}
