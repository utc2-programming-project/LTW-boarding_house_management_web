using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <summary>
    /// VHT-23 (1B/1C): mo rong bang Tenants theo de xuat muc 2/muc 8
    /// (docs/task/de_xuat_hop_dong_khach_thue.md).
    ///
    /// KHAC schema CoreSchema (ERD VHT-5) — vi sao:
    ///  - Them cot chua co trong ERD: IsActive, RowVersion, Gender, DateOfBirth, IdIssuedDate, IdIssuedPlace,
    ///    PermanentAddress, Note (xem muc 0.1: DTO 1B de xuat, P2 da tach IsActive/IsDeleted).
    ///  - Doi loc chi muc CitizenId: them [IsActive] = 1 de khop logic service (chi chan trung ho so dang dung).
    ///  - Doi chi muc UserId tu index thuong sang unique filtered: huong 1 tai khoan <-> 1 ho so (can Dao/Phat chot).
    ///  - IsActive mac dinh 1: ho so cu coi nhu dang su dung.
    /// Migration nay da chot (khong regenerate); doi schema thi them migration moi.
    /// </summary>
    /// <inheritdoc />
    public partial class ExtendTenantProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_CitizenId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_UserId",
                table: "Tenants");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Tenants",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IdIssuedDate",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdIssuedPlace",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress",
                table: "Tenants",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tenants",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CitizenId",
                table: "Tenants",
                column: "CitizenId",
                unique: true,
                filter: "[CitizenId] IS NOT NULL AND [IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_FullName",
                table: "Tenants",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Phone",
                table: "Tenants",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_UserId",
                table: "Tenants",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_CitizenId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_FullName",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Phone",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_UserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IdIssuedDate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IdIssuedPlace",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PermanentAddress",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tenants");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CitizenId",
                table: "Tenants",
                column: "CitizenId",
                unique: true,
                filter: "[CitizenId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_UserId",
                table: "Tenants",
                column: "UserId");
        }
    }
}
