using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileAccessAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileAccessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileCategory = table.Column<int>(type: "int", nullable: true),
                    RelatedEntityType = table.Column<int>(type: "int", nullable: true),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAccessAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileAccessAuditLogs_ActorUserId_OccurredAt",
                table: "FileAccessAuditLogs",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileAccessAuditLogs_FileId",
                table: "FileAccessAuditLogs",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileAccessAuditLogs_RelatedEntityType_RelatedEntityId_OccurredAt",
                table: "FileAccessAuditLogs",
                columns: new[] { "RelatedEntityType", "RelatedEntityId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileAccessAuditLogs");
        }
    }
}
