using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityReadingPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityReadingPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    ElectricStart = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ElectricEnd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WaterStart = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WaterEnd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ResidentCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    HasReadings = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityReadingPeriods", x => x.Id);
                    table.CheckConstraint("CK_UtilityReadingPeriods_Electric", "[ElectricStart] >= 0 AND [ElectricEnd] >= [ElectricStart]");
                    table.CheckConstraint("CK_UtilityReadingPeriods_InvoicedHasInvoice", "[Status] = 0 OR [InvoiceId] IS NOT NULL");
                    table.CheckConstraint("CK_UtilityReadingPeriods_InvoicedHasReadings", "[Status] = 0 OR [HasReadings] = 1");
                    table.CheckConstraint("CK_UtilityReadingPeriods_Period", "[PeriodEnd] >= [PeriodStart]");
                    table.CheckConstraint("CK_UtilityReadingPeriods_ResidentCount", "[ResidentCount] > 0");
                    table.CheckConstraint("CK_UtilityReadingPeriods_Status", "[Status] IN (0, 1)");
                    table.CheckConstraint("CK_UtilityReadingPeriods_Water", "[WaterStart] >= 0 AND [WaterEnd] >= [WaterStart]");
                    table.ForeignKey(
                        name: "FK_UtilityReadingPeriods_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityReadingPeriods_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityReadingPeriods_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityReadingPeriods_ContractId_PeriodEnd",
                table: "UtilityReadingPeriods",
                columns: new[] { "ContractId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityReadingPeriods_ContractId_PeriodStart",
                table: "UtilityReadingPeriods",
                columns: new[] { "ContractId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityReadingPeriods_InvoiceId",
                table: "UtilityReadingPeriods",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityReadingPeriods_RoomId",
                table: "UtilityReadingPeriods",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityReadingPeriods");
        }
    }
}
