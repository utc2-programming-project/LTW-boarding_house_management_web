using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssigneeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TechnicalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RepairCost = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceIncidents", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceIncidents_Category", "[Category] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_MaintenanceIncidents_RepairCost", "[RepairCost] IS NULL OR [RepairCost] >= 0");
                    table.CheckConstraint("CK_MaintenanceIncidents_Status", "[Status] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_MaintenanceIncidents_Urgency", "[Urgency] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_MaintenanceIncidents_AspNetUsers_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIncidents_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceIncidentHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceIncidentHistories", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceIncidentHistories_Action", "[Action] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_MaintenanceIncidentHistories_FromStatus", "[FromStatus] IS NULL OR [FromStatus] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_MaintenanceIncidentHistories_ToStatus", "[ToStatus] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_MaintenanceIncidentHistories_MaintenanceIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "MaintenanceIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIncidentHistories_IncidentId",
                table: "MaintenanceIncidentHistories",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIncidents_ReportedByUserId",
                table: "MaintenanceIncidents",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIncidents_RoomId",
                table: "MaintenanceIncidents",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceIncidentHistories");

            migrationBuilder.DropTable(
                name: "MaintenanceIncidents");
        }
    }
}
