using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RelaxInvoicePeriodUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices",
                columns: new[] { "ContractId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_RegistrationId",
                table: "Invoices",
                column: "RegistrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_RegistrationId",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_PeriodStart",
                table: "Invoices",
                columns: new[] { "ContractId", "PeriodStart" },
                unique: true);
        }
    }
}
