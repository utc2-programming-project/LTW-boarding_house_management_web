namespace Web.Data.Migrations;

/// <summary>
/// SQL trigger bảo vệ lịch sử điều chỉnh chỉ số khỏi UPDATE/DELETE. Điều kiện <c>deleted</c>
/// giúp câu lệnh không tác động dòng nào vẫn chạy bình thường.
/// </summary>
public static class UtilityReadingAuditImmutabilitySql
{
    public const string CreateOrAlter = """
        EXEC(N'
        CREATE OR ALTER TRIGGER [dbo].[TR_UtilityReadingAudits_Immutable]
        ON [dbo].[UtilityReadingAudits]
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
            BEGIN
                THROW 51000, N''Lịch sử điều chỉnh chỉ số là bất biến: không sửa, không xóa.'', 1;
            END
        END');
        """;

    public const string Drop = "DROP TRIGGER IF EXISTS [dbo].[TR_UtilityReadingAudits_Immutable];";
}
