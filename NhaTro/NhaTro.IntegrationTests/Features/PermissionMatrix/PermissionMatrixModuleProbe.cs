using NhaTro.IntegrationTests.Infrastructure;
using Xunit;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// Cầu nối cho các test phân hệ bổ sung: mỗi method vẫn chạy ĐÚNG probe hành vi của engine
/// (service thật + database test), không tự bịa kết quả và không nới kỳ vọng của workbook.
/// Dùng một <see cref="PermissionMatrixWorld"/> mới cho mỗi test để không phụ thuộc thứ tự chạy
/// của các test phân hệ cũ (mỗi test cũ tự reset database của fixture).
/// </summary>
internal static class PermissionMatrixModuleProbe
{
    public static async Task AssertCellAsync(SqlServerFixture fixture, string cellAddress)
    {
        var cell = PermissionMatrixManifest.Cell(cellAddress);
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var result = await PermissionMatrixProbeExecutor.ExecuteProbeAsync(cell, world);

        Assert.True(
            result.Status == "PASS",
            $"Ô {cell.Sheet}!{cell.Cell} ({cell.OperationCode} {cell.OperationName} - {cell.Role}) " +
            $"ký hiệu '{cell.Symbol}' phải PASS nhưng thực tế {result.Actual}/{result.Status}: " +
            $"{result.Evidence}");
    }
}
