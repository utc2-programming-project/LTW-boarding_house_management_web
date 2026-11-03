using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Migrations;

public partial class AddIncidentPhotoAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IncidentPhotoAttachments",
            columns: table => new
            {
                FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                IncidentId = table.Column<int>(type: "int", nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncidentPhotoAttachments", x => x.FileId);
                table.CheckConstraint("CK_IncidentPhotoAttachments_Kind", "[Kind] IN (0, 1)");
                table.CheckConstraint("CK_IncidentPhotoAttachments_Target", "[IncidentId] IS NOT NULL OR [ApprovalRequestId] IS NOT NULL");
                table.ForeignKey("FK_IncidentPhotoAttachments_StoredFiles_FileId", x => x.FileId, "StoredFiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_IncidentPhotoAttachments_MaintenanceIncidents_IncidentId", x => x.IncidentId, "MaintenanceIncidents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_IncidentPhotoAttachments_ManagerApprovalRequests_ApprovalRequestId", x => x.ApprovalRequestId, "ManagerApprovalRequests", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_IncidentPhotoAttachments_ApprovalRequestId", "IncidentPhotoAttachments", "ApprovalRequestId");
        migrationBuilder.CreateIndex("IX_IncidentPhotoAttachments_IncidentId_Kind", "IncidentPhotoAttachments", new[] { "IncidentId", "Kind" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("IncidentPhotoAttachments");
}
