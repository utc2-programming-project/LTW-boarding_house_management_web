using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Infrastructure.Auditing;

/// <summary>
/// Job dọn nhật ký thao tác cũ hơn hạn lưu (VHT-104). Chạy lúc khởi động và định kỳ theo
/// <see cref="AuditOptions.CleanupIntervalHours"/>; lỗi dọn chỉ ghi log, không làm sập app.
/// </summary>
public sealed class AuditLogCleanupService(
    AuditLogFileStore store,
    IOptions<AuditOptions> options,
    ILogger<AuditLogCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PruneSafelyAsync(stoppingToken);

        var interval = TimeSpan.FromHours(Math.Clamp(options.Value.CleanupIntervalHours, 1, 168));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PruneSafelyAsync(stoppingToken);
        }
    }

    private async Task PruneSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var removed = await store.PruneAsync(cancellationToken);
            if (removed > 0)
            {
                logger.LogInformation("Nhật ký thao tác: đã dọn {Count} dòng cũ hơn hạn lưu.", removed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Dừng app bình thường.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không dọn được nhật ký thao tác.");
        }
    }
}
