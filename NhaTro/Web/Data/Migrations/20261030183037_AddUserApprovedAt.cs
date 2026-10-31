using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApprovedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            // VHT-95: tai khoan da co truoc day coi nhu da duyet; neu de null
            // toan bo nguoi dung hien huu se roi vao trang thai Cho duyet va bi chan dang nhap.
            migrationBuilder.Sql(
                "UPDATE [AspNetUsers] SET [ApprovedAt] = SYSUTCDATETIME() WHERE [ApprovedAt] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "AspNetUsers");
        }
    }
}
