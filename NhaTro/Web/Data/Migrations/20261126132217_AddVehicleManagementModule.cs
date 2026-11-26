using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestVehicleNotices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExpectedFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpectedTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestVehicleNotices", x => x.Id);
                    table.CheckConstraint("CK_GuestVehicleNotices_Status", "[Status] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_GuestVehicleNotices_VehicleType", "[VehicleType] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_GuestVehicleNotices_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestVehicleNotices_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestVehicleNotices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParkingZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsIndoor = table.Column<bool>(type: "bit", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    AllowedVehicleTypes = table.Column<int>(type: "int", nullable: false),
                    HasSlots = table.Column<bool>(type: "bit", nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    OverrideMonthlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingZones", x => x.Id);
                    table.CheckConstraint("CK_ParkingZones_Capacity", "[Capacity] BETWEEN 1 AND 5000");
                    table.CheckConstraint("CK_ParkingZones_OverrideFee", "[OverrideMonthlyFee] IS NULL OR [OverrideMonthlyFee] >= 0");
                    table.ForeignKey(
                        name: "FK_ParkingZones_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAuditSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    AuditedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalExpected = table.Column<int>(type: "int", nullable: false),
                    TotalPresent = table.Column<int>(type: "int", nullable: false),
                    TotalMissing = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAuditSessions", x => x.Id);
                    table.CheckConstraint("CK_VehicleAuditSessions_Status", "[Status] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_VehicleAuditSessions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehiclePropertySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    ElectricMotorbikeMonthlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ManualMotorbikeMonthlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScooterMonthlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxVehiclesPerRoom = table.Column<int>(type: "int", nullable: false),
                    EnableParkingZones = table.Column<bool>(type: "bit", nullable: false),
                    ZoneNearFullThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    AllowTenantGuestNotice = table.Column<bool>(type: "bit", nullable: false),
                    AllowBlockingAlert = table.Column<bool>(type: "bit", nullable: false),
                    AllowManagerManageVehicles = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePropertySettings", x => x.Id);
                    table.CheckConstraint("CK_VehiclePropertySettings_Fees", "[ElectricMotorbikeMonthlyFee] >= 0 AND [ManualMotorbikeMonthlyFee] >= 0 AND [ScooterMonthlyFee] >= 0");
                    table.CheckConstraint("CK_VehiclePropertySettings_MaxVehicles", "[MaxVehiclesPerRoom] BETWEEN 0 AND 50");
                    table.CheckConstraint("CK_VehiclePropertySettings_Threshold", "[ZoneNearFullThresholdPercent] BETWEEN 50 AND 100");
                    table.ForeignKey(
                        name: "FK_VehiclePropertySettings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParkingZoneId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentVehicleId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSlots", x => x.Id);
                    table.CheckConstraint("CK_ParkingSlots_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ParkingSlots_ParkingZones_ParkingZoneId",
                        column: x => x.ParkingZoneId,
                        principalTable: "ParkingZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PhotoRef = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QrCodeToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParkingZoneId = table.Column<int>(type: "int", nullable: true),
                    ParkingSlotId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_Status", "[Status] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_Vehicles_VehicleType", "[VehicleType] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Vehicles_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_ParkingSlots_ParkingSlotId",
                        column: x => x.ParkingSlotId,
                        principalTable: "ParkingSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_ParkingZones_ParkingZoneId",
                        column: x => x.ParkingZoneId,
                        principalTable: "ParkingZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAuditItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditSessionId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    PlateNumberSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RoomIdSnapshot = table.Column<int>(type: "int", nullable: false),
                    RoomCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VehicleTypeSnapshot = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAuditItems", x => x.Id);
                    table.CheckConstraint("CK_VehicleAuditItems_Result", "[Result] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_VehicleAuditItems_VehicleAuditSessions_AuditSessionId",
                        column: x => x.AuditSessionId,
                        principalTable: "VehicleAuditSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleAuditItems_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestVehicleNotices_ExpectedTo",
                table: "GuestVehicleNotices",
                column: "ExpectedTo");

            migrationBuilder.CreateIndex(
                name: "IX_GuestVehicleNotices_PropertyId_Status",
                table: "GuestVehicleNotices",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestVehicleNotices_RoomId_Status",
                table: "GuestVehicleNotices",
                columns: new[] { "RoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestVehicleNotices_TenantId",
                table: "GuestVehicleNotices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSlots_ParkingZoneId_Code",
                table: "ParkingSlots",
                columns: new[] { "ParkingZoneId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingZones_PropertyId_Name",
                table: "ParkingZones",
                columns: new[] { "PropertyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditItems_AuditSessionId_VehicleId",
                table: "VehicleAuditItems",
                columns: new[] { "AuditSessionId", "VehicleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditItems_VehicleId",
                table: "VehicleAuditItems",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditSessions_PropertyId_StartedAt",
                table: "VehicleAuditSessions",
                columns: new[] { "PropertyId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAuditSessions_PropertyId_Status",
                table: "VehicleAuditSessions",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePropertySettings_PropertyId",
                table: "VehiclePropertySettings",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ContractId_Status",
                table: "Vehicles",
                columns: new[] { "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ParkingSlotId",
                table: "Vehicles",
                column: "ParkingSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ParkingZoneId",
                table: "Vehicles",
                column: "ParkingZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_PropertyId_PlateNumber",
                table: "Vehicles",
                columns: new[] { "PropertyId", "PlateNumber" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_QrCodeToken",
                table: "Vehicles",
                column: "QrCodeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RoomId_Status",
                table: "Vehicles",
                columns: new[] { "RoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId",
                table: "Vehicles",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestVehicleNotices");

            migrationBuilder.DropTable(
                name: "VehicleAuditItems");

            migrationBuilder.DropTable(
                name: "VehiclePropertySettings");

            migrationBuilder.DropTable(
                name: "VehicleAuditSessions");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "ParkingSlots");

            migrationBuilder.DropTable(
                name: "ParkingZones");
        }
    }
}
