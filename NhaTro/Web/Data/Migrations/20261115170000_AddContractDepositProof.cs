using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>Danh sách file minh chứng chuyển cọc.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261115170000_AddContractDepositProof")]
    public partial class AddContractDepositProof : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProofFileIdsJson",
                table: "ContractDepositPayments",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProofFileIdsJson", table: "ContractDepositPayments");
        }
    }
}
