using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>Hạn thanh toán cọc sau khi khách ký hợp đồng.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261115110000_AddContractDepositDueAt")]
    public partial class AddContractDepositDueAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositDueAt",
                table: "Contracts",
                type: "datetimeoffset",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DepositDueAt", table: "Contracts");
        }
    }
}
