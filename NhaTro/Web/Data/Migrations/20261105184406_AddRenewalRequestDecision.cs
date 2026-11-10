using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalRequestDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                table: "ContractRenewalRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedByUserId",
                table: "ContractRenewalRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "ContractRenewalRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRenewalRequests_DecidedByUserId",
                table: "ContractRenewalRequests",
                column: "DecidedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractRenewalRequests_AspNetUsers_DecidedByUserId",
                table: "ContractRenewalRequests",
                column: "DecidedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractRenewalRequests_AspNetUsers_DecidedByUserId",
                table: "ContractRenewalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ContractRenewalRequests_DecidedByUserId",
                table: "ContractRenewalRequests");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "ContractRenewalRequests");

            migrationBuilder.DropColumn(
                name: "DecidedByUserId",
                table: "ContractRenewalRequests");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "ContractRenewalRequests");
        }
    }
}
