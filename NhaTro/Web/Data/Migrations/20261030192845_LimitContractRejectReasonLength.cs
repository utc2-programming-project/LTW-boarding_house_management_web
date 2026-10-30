using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimitContractRejectReasonLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS
                (
                    SELECT 1
                    FROM [dbo].[Contracts]
                    WHERE [RejectReason] IS NOT NULL
                        AND DATALENGTH([RejectReason]) > 1000
                )
                BEGIN
                    THROW 51031, 'ExistingRejectReasonExceeds500', 1;
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "RejectReason",
                table: "Contracts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.Sql("""
                EXEC(N'
                CREATE OR ALTER TRIGGER [dbo].[TR_Contracts_PreventApprovedOverlap]
                ON [dbo].[Contracts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS candidate
                        INNER JOIN [dbo].[Contracts] AS existing WITH (UPDLOCK, HOLDLOCK)
                            ON existing.[RoomId] = candidate.[RoomId]
                            AND existing.[Status] = 1
                            AND existing.[ContractId] <> candidate.[ContractId]
                            AND existing.[StartDate] <= candidate.[EndDate]
                            AND candidate.[StartDate] <= existing.[EndDate]
                        WHERE candidate.[Status] = 1
                    )
                    BEGIN
                        THROW 51030, ''ContractOverlap'', 1;
                    END
                END;
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_Contracts_PreventApprovedOverlap];");

            migrationBuilder.AlterColumn<string>(
                name: "RejectReason",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
