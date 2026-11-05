using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRenewalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractRenewalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByTenantId = table.Column<int>(type: "int", nullable: false),
                    RequestedMonths = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRenewalRequests", x => x.Id);
                    table.CheckConstraint("CK_ContractRenewalRequests_Months", "[RequestedMonths] BETWEEN 1 AND 120");
                    table.CheckConstraint("CK_ContractRenewalRequests_Status", "[Status] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_ContractRenewalRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractRenewalRequests_Tenants_RequestedByTenantId",
                        column: x => x.RequestedByTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRenewalRequests_RequestedByTenantId_Status",
                table: "ContractRenewalRequests",
                columns: new[] { "RequestedByTenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRenewalRequests_Status_RequestedAt",
                table: "ContractRenewalRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ContractRenewalRequests_Pending",
                table: "ContractRenewalRequests",
                column: "ContractId",
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractRenewalRequests");
        }
    }
}
