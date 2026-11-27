using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTermsAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeterReadingDay",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentCycleMonths",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Terms",
                table: "Contracts",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepresentative",
                table: "ContractMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Relationship",
                table: "ContractMembers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeterReadingDay",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentCycleMonths",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsRepresentative",
                table: "ContractMembers");

            migrationBuilder.DropColumn(
                name: "Relationship",
                table: "ContractMembers");
        }
    }
}
