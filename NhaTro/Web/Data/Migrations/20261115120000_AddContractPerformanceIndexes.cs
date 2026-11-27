using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>Index cho các truy vấn hợp đồng/thành viên/thanh toán trên dataset lớn.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261115120000_AddContractPerformanceIndexes")]
    public partial class AddContractPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_RoomId_Status_StartDate_EndDate",
                table: "Contracts",
                columns: new[] { "RoomId", "Status", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status_DepositDueAt",
                table: "Contracts",
                columns: new[] { "Status", "DepositDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractMembers_TenantId_ContractId",
                table: "ContractMembers",
                columns: new[] { "TenantId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_ReviewedAt",
                table: "Payments",
                columns: new[] { "Status", "ReviewedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Payments_Status_ReviewedAt", table: "Payments");
            migrationBuilder.DropIndex(name: "IX_ContractMembers_TenantId_ContractId", table: "ContractMembers");
            migrationBuilder.DropIndex(name: "IX_Contracts_Status_DepositDueAt", table: "Contracts");
            migrationBuilder.DropIndex(name: "IX_Contracts_RoomId_Status_StartDate_EndDate", table: "Contracts");
        }
    }
}
