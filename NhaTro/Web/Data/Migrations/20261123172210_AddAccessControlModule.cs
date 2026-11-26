using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessControlModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GateDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateDevices", x => x.Id);
                    table.CheckConstraint("CK_GateDevices_Kind", "[Kind] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_GateDevices_Level", "[Level] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_GateDevices_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatePasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PassType = table.Column<int>(type: "int", nullable: false),
                    StayType = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VisitorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VisitorPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VisitorIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VisitorDob = table.Column<DateOnly>(type: "date", nullable: true),
                    StayReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VisitorPhotoRef = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AllowedMethods = table.Column<int>(type: "int", nullable: false),
                    MaxUses = table.Column<int>(type: "int", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuerRole = table.Column<int>(type: "int", nullable: false),
                    IssuedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AutoApproved = table.Column<bool>(type: "bit", nullable: false),
                    RejectedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    StayReportId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatePasses", x => x.Id);
                    table.CheckConstraint("CK_GatePasses_Issuer", "[IssuerRole] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_GatePasses_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_GatePasses_Uses", "[MaxUses] BETWEEN 1 AND 50 AND [UsedCount] >= 0");
                    table.ForeignKey(
                        name: "FK_GatePasses_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatePasses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    AuthMode = table.Column<int>(type: "int", nullable: false),
                    AntiPassbackEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowManualOpen = table.Column<bool>(type: "bit", nullable: false),
                    AllowManagerApprove = table.Column<bool>(type: "bit", nullable: false),
                    AutoApproveDayVisit = table.Column<bool>(type: "bit", nullable: false),
                    AutoApproveOvernight = table.Column<bool>(type: "bit", nullable: false),
                    AllowTenantIssuePass = table.Column<bool>(type: "bit", nullable: false),
                    TenantMayIssueOvernight = table.Column<bool>(type: "bit", nullable: false),
                    TenantMaxPassesPerDay = table.Column<int>(type: "int", nullable: false),
                    TenantMaxTtlMinutes = table.Column<int>(type: "int", nullable: false),
                    TenantMaxUses = table.Column<int>(type: "int", nullable: false),
                    PassTtlMinutes = table.Column<int>(type: "int", nullable: false),
                    DayVisitStartHour = table.Column<int>(type: "int", nullable: false),
                    DayVisitEndHour = table.Column<int>(type: "int", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    OfflineGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    RequiredGuestFields = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PreferredStayChannel = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatePolicies", x => x.Id);
                    table.CheckConstraint("CK_GatePolicies_AuthMode", "[AuthMode] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_GatePolicies_Hours", "[DayVisitStartHour] BETWEEN 0 AND 23 AND [DayVisitEndHour] BETWEEN 1 AND 24 AND [DayVisitStartHour] < [DayVisitEndHour]");
                    table.CheckConstraint("CK_GatePolicies_TenantQuota", "[TenantMaxPassesPerDay] BETWEEN 0 AND 50 AND [TenantMaxUses] BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_GatePolicies_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BiometricEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    GatePassId = table.Column<int>(type: "int", nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    TemplateRef = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PublicKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConsentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EnrolledByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetentionUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricEnrollments", x => x.Id);
                    table.CheckConstraint("CK_BiometricEnrollments_Method", "[Method] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_BiometricEnrollments_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_BiometricEnrollments_GatePasses_GatePassId",
                        column: x => x.GatePassId,
                        principalTable: "GatePasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiometricEnrollments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiometricEnrollments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GateLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    GateDeviceId = table.Column<int>(type: "int", nullable: true),
                    GatePassId = table.Column<int>(type: "int", nullable: true),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MatchScore = table.Column<int>(type: "int", nullable: true),
                    SubjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateLogs", x => x.Id);
                    table.CheckConstraint("CK_GateLogs_Direction", "[Direction] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_GateLogs_Subject", "[SubjectType] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_GateLogs_GateDevices_GateDeviceId",
                        column: x => x.GateDeviceId,
                        principalTable: "GateDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GateLogs_GatePasses_GatePassId",
                        column: x => x.GatePassId,
                        principalTable: "GatePasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GateLogs_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StayReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GatePassId = table.Column<int>(type: "int", nullable: true),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GuestDob = table.Column<DateOnly>(type: "date", nullable: true),
                    GuestIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StayReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StayFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StayTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AddressSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EvidenceRef = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StayReports", x => x.Id);
                    table.CheckConstraint("CK_StayReports_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_StayReports_GatePasses_GatePassId",
                        column: x => x.GatePassId,
                        principalTable: "GatePasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StayReports_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StayReports_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricEnrollments_GatePassId",
                table: "BiometricEnrollments",
                column: "GatePassId");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricEnrollments_PropertyId_Status",
                table: "BiometricEnrollments",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricEnrollments_TenantId",
                table: "BiometricEnrollments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_PropertyId_Name",
                table: "GateDevices",
                columns: new[] { "PropertyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GateLogs_GateDeviceId",
                table: "GateLogs",
                column: "GateDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GateLogs_GatePassId_OccurredAt",
                table: "GateLogs",
                columns: new[] { "GatePassId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GateLogs_PropertyId_Direction_IsSuccess",
                table: "GateLogs",
                columns: new[] { "PropertyId", "Direction", "IsSuccess" });

            migrationBuilder.CreateIndex(
                name: "IX_GateLogs_PropertyId_OccurredAt",
                table: "GateLogs",
                columns: new[] { "PropertyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_ContractId",
                table: "GatePasses",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_ExpiresAt",
                table: "GatePasses",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_PropertyId_Status",
                table: "GatePasses",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_RoomId_CreatedAt",
                table: "GatePasses",
                columns: new[] { "RoomId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_TenantId",
                table: "GatePasses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_TokenHash",
                table: "GatePasses",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatePolicies_PropertyId",
                table: "GatePolicies",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StayReports_DueAt",
                table: "StayReports",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_StayReports_GatePassId",
                table: "StayReports",
                column: "GatePassId");

            migrationBuilder.CreateIndex(
                name: "IX_StayReports_PropertyId_Status",
                table: "StayReports",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StayReports_RoomId",
                table: "StayReports",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiometricEnrollments");

            migrationBuilder.DropTable(
                name: "GateLogs");

            migrationBuilder.DropTable(
                name: "GatePolicies");

            migrationBuilder.DropTable(
                name: "StayReports");

            migrationBuilder.DropTable(
                name: "GateDevices");

            migrationBuilder.DropTable(
                name: "GatePasses");
        }
    }
}
