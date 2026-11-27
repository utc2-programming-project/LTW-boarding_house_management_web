using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixVehicleModuleConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_VehicleId",
                table: "VehicleAuditItems");

            migrationBuilder.AlterColumn<int>(
                name: "VehicleId",
                table: "VehicleAuditItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "GuestNoticeId",
                table: "VehicleAuditItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehicleId",
                table: "MaintenanceIncidents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlateNumber",
                table: "MaintenanceIncidents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Phiếu cũ thiếu TenantId không còn hợp lệ theo SPEC; xoá trước khi siết NOT NULL để không vi phạm FK.
            migrationBuilder.Sql("DELETE FROM [GuestVehicleNotices] WHERE [TenantId] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "GuestVehicleNotices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Đảm bảo mỗi nhà trọ chỉ còn một phiên InProgress trước khi tạo unique filtered index.
            migrationBuilder.Sql(@"
                ;WITH ranked AS (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [PropertyId] ORDER BY [StartedAt] DESC, [Id] DESC) AS rn
                    FROM [VehicleAuditSessions]
                    WHERE [Status] = 1
                )
                UPDATE s SET s.[Status] = 2, s.[CompletedAt] = SYSUTCDATETIME()
                FROM [VehicleAuditSessions] s
                INNER JOIN ranked r ON r.[Id] = s.[Id]
                WHERE r.rn > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditSessions_PropertyId_InProgress",
                table: "VehicleAuditSessions",
                column: "PropertyId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_GuestNoticeId",
                table: "VehicleAuditItems",
                columns: new[] { "AuditSessionId", "GuestNoticeId" },
                unique: true,
                filter: "[GuestNoticeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_VehicleId",
                table: "VehicleAuditItems",
                columns: new[] { "AuditSessionId", "VehicleId" },
                unique: true,
                filter: "[VehicleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditItems_GuestNoticeId",
                table: "VehicleAuditItems",
                column: "GuestNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSlots_CurrentVehicleId",
                table: "ParkingSlots",
                column: "CurrentVehicleId",
                unique: true,
                filter: "[CurrentVehicleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIncidents_VehicleId",
                table: "MaintenanceIncidents",
                column: "VehicleId");

            // Sửa các bản ghi cũ có khoảng thời gian không hợp lệ để có thể thêm check constraint.
            migrationBuilder.Sql(@"
                UPDATE [GuestVehicleNotices]
                SET [ExpectedTo] = DATEADD(HOUR, 1, [ExpectedFrom])
                WHERE [ExpectedTo] <= [ExpectedFrom];");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GuestVehicleNotices_ExpectedRange",
                table: "GuestVehicleNotices",
                sql: "[ExpectedTo] > [ExpectedFrom]");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceIncidents_Vehicles_VehicleId",
                table: "MaintenanceIncidents",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSlots_Vehicles_CurrentVehicleId",
                table: "ParkingSlots",
                column: "CurrentVehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleAuditItems_GuestVehicleNotices_GuestNoticeId",
                table: "VehicleAuditItems",
                column: "GuestNoticeId",
                principalTable: "GuestVehicleNotices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceIncidents_Vehicles_VehicleId",
                table: "MaintenanceIncidents");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSlots_Vehicles_CurrentVehicleId",
                table: "ParkingSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_VehicleAuditItems_GuestVehicleNotices_GuestNoticeId",
                table: "VehicleAuditItems");

            migrationBuilder.DropIndex(
                name: "IX_VehicleAuditSessions_PropertyId_InProgress",
                table: "VehicleAuditSessions");

            migrationBuilder.DropIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_GuestNoticeId",
                table: "VehicleAuditItems");

            migrationBuilder.DropIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_VehicleId",
                table: "VehicleAuditItems");

            migrationBuilder.DropIndex(
                name: "IX_VehicleAuditItems_GuestNoticeId",
                table: "VehicleAuditItems");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSlots_CurrentVehicleId",
                table: "ParkingSlots");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceIncidents_VehicleId",
                table: "MaintenanceIncidents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GuestVehicleNotices_ExpectedRange",
                table: "GuestVehicleNotices");

            migrationBuilder.DropColumn(
                name: "GuestNoticeId",
                table: "VehicleAuditItems");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "MaintenanceIncidents");

            migrationBuilder.DropColumn(
                name: "VehiclePlateNumber",
                table: "MaintenanceIncidents");

            migrationBuilder.AlterColumn<int>(
                name: "VehicleId",
                table: "VehicleAuditItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "GuestVehicleNotices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_VehicleId",
                table: "VehicleAuditItems",
                columns: new[] { "AuditSessionId", "VehicleId" },
                unique: true);
        }
    }
}
