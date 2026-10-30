using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceCatalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: true),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCatalogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceCatalogs_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePriceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceCatalogId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CalculationMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePriceHistory", x => x.Id);
                    table.CheckConstraint("CK_ServicePriceHistory_CalculationMethod_NotBlank", "LEN(LTRIM(RTRIM([CalculationMethod]))) > 0");
                    table.CheckConstraint("CK_ServicePriceHistory_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_ServicePriceHistory_Unit_NotBlank", "LEN(LTRIM(RTRIM([Unit]))) > 0");
                    table.CheckConstraint("CK_ServicePriceHistory_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_ServicePriceHistory_ServiceCatalogs_ServiceCatalogId",
                        column: x => x.ServiceCatalogId,
                        principalTable: "ServiceCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCatalogs_PropertyId",
                table: "ServiceCatalogs",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePriceHistory_EffectiveDate_ServiceCatalogId",
                table: "ServicePriceHistory",
                columns: new[] { "EffectiveDate", "ServiceCatalogId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePriceHistory_ServiceCatalogId_EffectiveDate",
                table: "ServicePriceHistory",
                columns: new[] { "ServiceCatalogId", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServicePriceHistory");

            migrationBuilder.DropTable(
                name: "ServiceCatalogs");
        }
    }
}
