using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>Ghi nhận chủ trọ đã thực nhận tiền cọc giữ căn (không phải doanh thu thuê).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261115100000_AddContractDepositPayment")]
    public partial class AddContractDepositPayment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositPaidAmount",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositPaidAt",
                table: "Contracts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositPaidByUserId",
                table: "Contracts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DepositPaidAmount", table: "Contracts");
            migrationBuilder.DropColumn(name: "DepositPaidAt", table: "Contracts");
            migrationBuilder.DropColumn(name: "DepositPaidByUserId", table: "Contracts");
        }
    }
}
