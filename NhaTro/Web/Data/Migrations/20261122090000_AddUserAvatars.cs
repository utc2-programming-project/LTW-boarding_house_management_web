using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>
    /// Ảnh đại diện lưu ở server (PLAN-030 bổ sung): một dòng mỗi tài khoản,
    /// khoá chính đồng thời là khoá ngoại cascade tới AspNetUsers nên xoá tài khoản là ảnh mất theo.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261122090000_AddUserAvatars")]
    public partial class AddUserAvatars : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAvatars",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAvatars", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserAvatars_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserAvatars");
        }
    }
}
