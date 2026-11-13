using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>Nghĩa vụ hoàn tiền cọc khi hủy/từ chối/thanh lý hợp đồng.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261115150000_AddContractDepositRefund")]
    public partial class AddContractDepositRefund : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositRefundAmount",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositRefundedAt",
                table: "Contracts",
                type: "datetimeoffset",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DepositRefundAmount", table: "Contracts");
            migrationBuilder.DropColumn(name: "DepositRefundedAt", table: "Contracts");
        }
    }
}
