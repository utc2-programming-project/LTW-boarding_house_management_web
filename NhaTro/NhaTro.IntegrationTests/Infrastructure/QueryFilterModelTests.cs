using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Web.Data;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// Chặn hồi quy: navigation required trỏ tới entity có global query filter làm Include dùng
/// INNER JOIN và rơi dòng con im lặng. Quan hệ như vậy phải có query filter đồng bộ ở bảng con.
/// </summary>
public class QueryFilterModelTests
{
    [Fact]
    public void Model_khong_co_required_navigation_tro_toi_entity_bi_query_filter()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=ModelOnly;Trusted_Connection=True")
            .ConfigureWarnings(warnings => warnings.Throw(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;

        using var db = new ApplicationDbContext(options);

        // Chỉ build + validate model, không mở kết nối nên không cần SQL Server đang chạy.
        _ = db.Model;
    }
}
