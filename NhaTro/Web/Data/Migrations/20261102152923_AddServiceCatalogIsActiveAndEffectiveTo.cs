using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogIsActiveAndEffectiveTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveTo",
                table: "ServicePriceHistory",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ServiceCatalogs",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServicePriceHistory_EffectiveTo_OnOrAfter_EffectiveDate",
                table: "ServicePriceHistory",
                sql: "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ServicePriceHistory_EffectiveTo_OnOrAfter_EffectiveDate",
                table: "ServicePriceHistory");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "ServicePriceHistory");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ServiceCatalogs");
        }
    }
}
