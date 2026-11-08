using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractRenewalRequests_Contracts_ContractId",
                table: "ContractRenewalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantOwnerLinks_Tenants_TenantId",
                table: "TenantOwnerLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId",
                table: "UtilityReadingAudits");

            // Các CHECK và unique index Payment đã có trong AddPayments.

            migrationBuilder.AddForeignKey(
                name: "FK_ContractRenewalRequests_Contracts_ContractId",
                table: "ContractRenewalRequests",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "ContractId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantOwnerLinks_Tenants_TenantId",
                table: "TenantOwnerLinks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId",
                table: "UtilityReadingAudits",
                column: "UtilityReadingId",
                principalTable: "UtilityReadings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractRenewalRequests_Contracts_ContractId",
                table: "ContractRenewalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantOwnerLinks_Tenants_TenantId",
                table: "TenantOwnerLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId",
                table: "UtilityReadingAudits");

            // Các CHECK và unique index Payment đã có trong AddPayments.

            migrationBuilder.AddForeignKey(
                name: "FK_ContractRenewalRequests_Contracts_ContractId",
                table: "ContractRenewalRequests",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "ContractId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantOwnerLinks_Tenants_TenantId",
                table: "TenantOwnerLinks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId",
                table: "UtilityReadingAudits",
                column: "UtilityReadingId",
                principalTable: "UtilityReadings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
