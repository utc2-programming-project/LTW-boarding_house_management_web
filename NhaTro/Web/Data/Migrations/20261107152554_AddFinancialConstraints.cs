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

            migrationBuilder.DropIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId_RequestedAt",
                table: "Payments",
                columns: new[] { "InvoiceId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Payments_InvoiceConfirmed",
                table: "Payments",
                column: "InvoiceId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount",
                table: "Payments",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_RejectionReason",
                table: "Payments",
                sql: "([Status] <> 2 AND [RejectionReason] IS NULL) OR ([Status] = 2 AND [RejectionReason] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_ReviewedAt",
                table: "Payments",
                sql: "([Status] = 0 AND [ReviewedAt] IS NULL) OR ([Status] <> 0 AND [ReviewedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Status",
                table: "Payments",
                sql: "[Status] IN (0, 1, 2)");

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

            migrationBuilder.DropIndex(
                name: "IX_Payments_InvoiceId_RequestedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "UX_Payments_InvoiceConfirmed",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_RejectionReason",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_ReviewedAt",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Status",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

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
