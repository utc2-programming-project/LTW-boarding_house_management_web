using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityPeriodTariffSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityBillingPeriodSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ScopeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillingPeriodId = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ResidentCount = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityBillingPeriodSnapshots", x => x.Id);
                    table.CheckConstraint("CK_UtilityBillingPeriodSnapshots_BillingPeriodId", "[BillingPeriodId] > 0");
                    table.CheckConstraint("CK_UtilityBillingPeriodSnapshots_Period", "[PeriodEnd] >= [PeriodStart]");
                    table.CheckConstraint("CK_UtilityBillingPeriodSnapshots_Quantity", "[Quantity] >= 0");
                    table.CheckConstraint("CK_UtilityBillingPeriodSnapshots_ResidentCount", "[ResidentCount] > 0");
                    table.CheckConstraint("CK_UtilityBillingPeriodSnapshots_Total", "[Total] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "UtilityBillingCostLineSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilityBillingPeriodSnapshotId = table.Column<int>(type: "int", nullable: false),
                    TierNumber = table.Column<int>(type: "int", nullable: false),
                    TariffVersionId = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    From = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    To = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityBillingCostLineSnapshots", x => x.Id);
                    table.CheckConstraint("CK_UtilityBillingCostLineSnapshots_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_UtilityBillingCostLineSnapshots_Quantity", "[Quantity] >= 0");
                    table.CheckConstraint("CK_UtilityBillingCostLineSnapshots_Range", "[To] IS NULL OR [To] > [From]");
                    table.CheckConstraint("CK_UtilityBillingCostLineSnapshots_TierNumber", "[TierNumber] > 0");
                    table.CheckConstraint("CK_UtilityBillingCostLineSnapshots_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_UtilityBillingCostLineSnapshots_UtilityBillingPeriodSnapshots_UtilityBillingPeriodSnapshotId",
                        column: x => x.UtilityBillingPeriodSnapshotId,
                        principalTable: "UtilityBillingPeriodSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityBillingCostLineSnapshots_UtilityTariffs_TariffVersionId",
                        column: x => x.TariffVersionId,
                        principalTable: "UtilityTariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "UtilityTariffs",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ScopeCode", "SourceName", "SourceReference" },
                values: new object[] { "HCMC-BENTHANH", "Công ty Cổ phần Cấp nước Bến Thành; Notice 3698/TB-CNBT-KDDVKH", "https://cdn.thuvienphapluat.vn//phap-luat/2022-2/NVK/2026/T4-2026/280426/thong-bao-3698-tb-cnbt-kddvkh-nam-2025.pdf (residential clean water rates; VAT and wastewater fee excluded)" });

            migrationBuilder.InsertData(
                table: "UtilityTariffs",
                columns: new[] { "Id", "EffectiveDate", "IsPerResident", "IsReadOnly", "ScopeCode", "SourceName", "SourceReference", "Type", "Unit" },
                values: new object[,]
                {
                    { 3, new DateOnly(2023, 11, 9), false, true, "VN-EVN-HOUSEHOLD", "EVN / Ministry of Industry and Trade", "https://www.evn.com.vn/userfile/User/tcdl/files/2023-11-08%20QD%202941-QD-BCT%20quy%20dinh%20ve%20gia%20ban%20dien.PDF (Decision 2941/QĐ-BCT; household retail rates, VAT excluded)", 0, "kWh" },
                    { 4, new DateOnly(2024, 10, 11), false, true, "VN-EVN-HOUSEHOLD", "EVN / Ministry of Industry and Trade", "https://www.evn.com.vn/userfile/User/tcdl/files/2024_10_11_QD2699_QD_BCT_quydinhvegiadien%281%29.pdf (Decision 2699/QĐ-BCT; household retail rates, VAT excluded)", 0, "kWh" },
                    { 5, new DateOnly(2025, 1, 1), true, true, "HCMC-BENTHANH", "Công ty Cổ phần Cấp nước Bến Thành; Notice 3698/TB-CNBT-KDDVKH", "https://cdn.thuvienphapluat.vn//phap-luat/2022-2/NVK/2026/T4-2026/280426/thong-bao-3698-tb-cnbt-kddvkh-nam-2025.pdf (states 2026 rates are unchanged from 2025; residential clean water rates, VAT and wastewater fee excluded)", 1, "m³" }
                });

            migrationBuilder.InsertData(
                table: "UtilityTariffTiers",
                columns: new[] { "Id", "From", "TierNumber", "To", "UnitPrice", "UtilityTariffId" },
                values: new object[,]
                {
                    { 10, 0m, 1, 50m, 1806m, 3 },
                    { 11, 50m, 2, 100m, 1866m, 3 },
                    { 12, 100m, 3, 200m, 2167m, 3 },
                    { 13, 200m, 4, 300m, 2729m, 3 },
                    { 14, 300m, 5, 400m, 3050m, 3 },
                    { 15, 400m, 6, null, 3151m, 3 },
                    { 16, 0m, 1, 50m, 1893m, 4 },
                    { 17, 50m, 2, 100m, 1956m, 4 },
                    { 18, 100m, 3, 200m, 2271m, 4 },
                    { 19, 200m, 4, 300m, 2860m, 4 },
                    { 20, 300m, 5, 400m, 3197m, 4 },
                    { 21, 400m, 6, null, 3302m, 4 },
                    { 22, 0m, 1, 4m, 6700m, 5 },
                    { 23, 4m, 2, 6m, 12900m, 5 },
                    { 24, 6m, 3, null, 14400m, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBillingCostLineSnapshots_TariffVersionId",
                table: "UtilityBillingCostLineSnapshots",
                column: "TariffVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBillingCostLineSnapshots_UtilityBillingPeriodSnapshotId_TariffVersionId_TierNumber",
                table: "UtilityBillingCostLineSnapshots",
                columns: new[] { "UtilityBillingPeriodSnapshotId", "TariffVersionId", "TierNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBillingPeriodSnapshots_BillingPeriodId_Type",
                table: "UtilityBillingPeriodSnapshots",
                columns: new[] { "BillingPeriodId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityBillingCostLineSnapshots");

            migrationBuilder.DropTable(
                name: "UtilityBillingPeriodSnapshots");

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "UtilityTariffTiers",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "UtilityTariffs",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "UtilityTariffs",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "UtilityTariffs",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.UpdateData(
                table: "UtilityTariffs",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ScopeCode", "SourceName", "SourceReference" },
                values: new object[] { "HCMC-SAWACO", "SAWACO customer-care portal; 2026 continuation notice", "https://cskh.sawaco.com.vn/News/Content?branchCode=KtSzxj0rfxc%3D&contentId=Qu20CKHdW0Q%3D ; https://cskh.sawaco.com.vn/ho_tro/hoi_dap?branchCodeIframe=TCT (residential tariff; VAT and wastewater fee excluded)" });
        }
    }
}
