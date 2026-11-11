using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractServiceRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractServiceRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceCatalogId = table.Column<int>(type: "int", nullable: false),
                    RegisteredByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractServiceRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractServiceRegistrations_ContractId",
                table: "ContractServiceRegistrations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractServiceRegistrations_ContractId_ServiceCatalogId",
                table: "ContractServiceRegistrations",
                columns: new[] { "ContractId", "ServiceCatalogId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractServiceRegistrations");
        }
    }
}
