using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceStrictServicePriceHistoryConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [ServicePriceHistory] WHERE [UnitPrice] <= 0) BEGIN " +
                ";THROW 51000, N'Cannot enforce positive service prices: ServicePriceHistory contains a rate less than or equal to zero.', 1; END; " +
                "IF EXISTS (SELECT 1 FROM [ServicePriceHistory] WHERE [EffectiveTo] IS NOT NULL AND [EffectiveTo] <= [EffectiveDate]) BEGIN " +
                ";THROW 51001, N'Cannot enforce strict service price dates: ServicePriceHistory contains an end date on or before its start date.', 1; END;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServicePriceHistory_EffectiveTo_OnOrAfter_EffectiveDate",
                table: "ServicePriceHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServicePriceHistory_UnitPrice_NonNegative",
                table: "ServicePriceHistory");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServicePriceHistory_EffectiveTo_After_EffectiveDate",
                table: "ServicePriceHistory",
                sql: "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServicePriceHistory_UnitPrice_Positive",
                table: "ServicePriceHistory",
                sql: "[UnitPrice] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ServicePriceHistory_EffectiveTo_After_EffectiveDate",
                table: "ServicePriceHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServicePriceHistory_UnitPrice_Positive",
                table: "ServicePriceHistory");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServicePriceHistory_EffectiveTo_OnOrAfter_EffectiveDate",
                table: "ServicePriceHistory",
                sql: "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServicePriceHistory_UnitPrice_NonNegative",
                table: "ServicePriceHistory",
                sql: "[UnitPrice] >= 0");
        }
    }
}
