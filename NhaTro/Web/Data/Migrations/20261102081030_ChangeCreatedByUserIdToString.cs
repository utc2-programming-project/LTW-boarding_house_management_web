using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCreatedByUserIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "ServicePriceHistory",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"
                UPDATE [ServicePriceHistory]
                SET [CreatedByUserId] = NULL
                WHERE [CreatedByUserId] IS NOT NULL
                  AND [CreatedByUserId] NOT IN (SELECT [Id] FROM [AspNetUsers]);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePriceHistory_CreatedByUserId",
                table: "ServicePriceHistory",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServicePriceHistory_AspNetUsers_CreatedByUserId",
                table: "ServicePriceHistory",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServicePriceHistory_AspNetUsers_CreatedByUserId",
                table: "ServicePriceHistory");

            migrationBuilder.DropIndex(
                name: "IX_ServicePriceHistory_CreatedByUserId",
                table: "ServicePriceHistory");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM [ServicePriceHistory]
                    WHERE [CreatedByUserId] IS NULL
                       OR TRY_CAST([CreatedByUserId] AS INT) IS NULL
                )
                BEGIN
                    THROW 50000, N'Cannot rollback migration ChangeCreatedByUserIdToString: ServicePriceHistory contains NULL or non-integer CreatedByUserId values. Data loss would occur.', 1;
                END;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                table: "ServicePriceHistory",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
