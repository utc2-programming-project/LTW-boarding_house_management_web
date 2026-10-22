namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// [Fact] HTTP/UI chỉ chạy khi có SQL Server LocalDB; nếu không thì Skip để CI không đỏ oan.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WebAppFactAttribute : FactAttribute
{
    public WebAppFactAttribute()
    {
        if (!SqlServerFixture.Available)
        {
            Skip = "Không có SQL Server LocalDB trên máy này (bỏ qua HTTP/auth test).";
        }
    }
}
