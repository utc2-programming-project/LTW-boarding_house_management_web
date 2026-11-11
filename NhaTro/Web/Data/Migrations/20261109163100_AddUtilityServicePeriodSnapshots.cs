using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityServicePeriodSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasServiceSnapshots",
                table: "UtilityReadingPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UtilityServicePeriodSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilityReadingPeriodId = table.Column<int>(type: "int", nullable: false),
                    SourceServiceCatalogId = table.Column<int>(type: "int", nullable: false),
                    SourceServicePriceHistoryId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityServicePeriodSnapshots", x => x.Id);
                    table.CheckConstraint("CK_UtilityServicePeriodSnapshots_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_UtilityServicePeriodSnapshots_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_UtilityServicePeriodSnapshots_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_UtilityServicePeriodSnapshots_UtilityReadingPeriods_UtilityReadingPeriodId",
                        column: x => x.UtilityReadingPeriodId,
                        principalTable: "UtilityReadingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UtilityReadingPeriods_ServiceSnapshotsRequireReadings",
                table: "UtilityReadingPeriods",
                sql: "[HasServiceSnapshots] = 0 OR [HasReadings] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityServicePeriodSnapshots_UtilityReadingPeriodId_SourceServicePriceHistoryId",
                table: "UtilityServicePeriodSnapshots",
                columns: new[] { "UtilityReadingPeriodId", "SourceServicePriceHistoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityServicePeriodSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UtilityReadingPeriods_ServiceSnapshotsRequireReadings",
                table: "UtilityReadingPeriods");

            migrationBuilder.DropColumn(
                name: "HasServiceSnapshots",
                table: "UtilityReadingPeriods");
        }
    }
}
