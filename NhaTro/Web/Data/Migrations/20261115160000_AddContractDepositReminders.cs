using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>Mốc đã gửi nhắc hạn thanh toán cọc 24h/6h.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261115160000_AddContractDepositReminders")]
    public partial class AddContractDepositReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositReminder24hAt",
                table: "Contracts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositReminder6hAt",
                table: "Contracts",
                type: "datetimeoffset",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DepositReminder24hAt", table: "Contracts");
            migrationBuilder.DropColumn(name: "DepositReminder6hAt", table: "Contracts");
        }
    }
}
