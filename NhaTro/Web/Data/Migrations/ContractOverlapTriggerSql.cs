namespace Web.Data.Migrations;

/// <summary>Canonical SQL definition shared by the migration and SQL integration test.</summary>
public static class ContractOverlapTriggerSql
{
    public const string CreateOrAlter = """
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
        """;
}
