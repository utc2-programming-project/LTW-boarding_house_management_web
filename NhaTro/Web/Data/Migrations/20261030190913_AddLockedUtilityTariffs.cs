using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLockedUtilityTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityTariffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ScopeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPerResident = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityTariffs", x => x.Id);
                    table.CheckConstraint("CK_UtilityTariffs_ReadOnly", "[IsReadOnly] = 1");
                });

            migrationBuilder.CreateTable(
                name: "UtilityTariffTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilityTariffId = table.Column<int>(type: "int", nullable: false),
                    TierNumber = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    To = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityTariffTiers", x => x.Id);
                    table.CheckConstraint("CK_UtilityTariffTiers_From_NonNegative", "[From] >= 0");
                    table.CheckConstraint("CK_UtilityTariffTiers_TierNumber_Positive", "[TierNumber] > 0");
                    table.CheckConstraint("CK_UtilityTariffTiers_To_After_From", "[To] IS NULL OR [To] > [From]");
                    table.CheckConstraint("CK_UtilityTariffTiers_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_UtilityTariffTiers_UtilityTariffs_UtilityTariffId",
                        column: x => x.UtilityTariffId,
                        principalTable: "UtilityTariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "UtilityTariffs",
                columns: new[] { "Id", "EffectiveDate", "IsPerResident", "IsReadOnly", "ScopeCode", "SourceName", "SourceReference", "Type", "Unit" },
                values: new object[,]
                {
                    { 1, new DateOnly(2025, 5, 10), false, true, "VN-EVN-HOUSEHOLD", "EVN / Ministry of Industry and Trade", "https://evn.com.vn/userfile/User/tcdl/files/2025/5/BanTinEVNSo182025-20250513160725352.pdf (Decision 1279/QĐ-BCT; household retail rates, VAT excluded)", 0, "kWh" },
                    { 2, new DateOnly(2026, 1, 1), true, true, "HCMC-SAWACO", "SAWACO customer-care portal; 2026 continuation notice", "https://cskh.sawaco.com.vn/News/Content?branchCode=KtSzxj0rfxc%3D&contentId=Qu20CKHdW0Q%3D ; https://cskh.sawaco.com.vn/ho_tro/hoi_dap?branchCodeIframe=TCT (residential tariff; VAT and wastewater fee excluded)", 1, "m³" }
                });

            migrationBuilder.InsertData(
                table: "UtilityTariffTiers",
                columns: new[] { "Id", "From", "TierNumber", "To", "UnitPrice", "UtilityTariffId" },
                values: new object[,]
                {
                    { 1, 0m, 1, 50m, 1984m, 1 },
                    { 2, 50m, 2, 100m, 2050m, 1 },
                    { 3, 100m, 3, 200m, 2380m, 1 },
                    { 4, 200m, 4, 300m, 2998m, 1 },
                    { 5, 300m, 5, 400m, 3350m, 1 },
                    { 6, 400m, 6, null, 3460m, 1 },
                    { 7, 0m, 1, 4m, 6700m, 2 },
                    { 8, 4m, 2, 6m, 12900m, 2 },
                    { 9, 6m, 3, null, 14400m, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityTariffs_Type_ScopeCode_EffectiveDate",
                table: "UtilityTariffs",
                columns: new[] { "Type", "ScopeCode", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityTariffTiers_UtilityTariffId_TierNumber",
                table: "UtilityTariffTiers",
                columns: new[] { "UtilityTariffId", "TierNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityTariffTiers");

            migrationBuilder.DropTable(
                name: "UtilityTariffs");
        }
    }
}
