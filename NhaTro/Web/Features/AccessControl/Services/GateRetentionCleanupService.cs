using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;

namespace Web.Features.AccessControl.Services;

/// <summary>
/// Job dọn dữ liệu cổng quá hạn lưu (nhật ký, phiếu, liên kết tự điền). Chạy lúc khởi động
/// và định kỳ theo <see cref="GateRetentionOptions.CleanupIntervalHours"/>; lỗi chỉ ghi log.
/// Thông báo lưu trú không bị xoá (hồ sơ pháp lý).
/// </summary>
public sealed class GateRetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<GateRetentionOptions> options,
    TimeProvider clock,
    ILogger<GateRetentionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PruneSafelyAsync(stoppingToken);

        var hours = Math.Clamp(options.Value.CleanupIntervalHours, 1, 168);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PruneSafelyAsync(stoppingToken);
        }
    }

    private async Task PruneSafelyAsync(CancellationToken cancellationToken)
    {
        var days = options.Value.RetentionDays;
        if (days <= 0)
        {
            return;
        }

        try
        {
            var cutoff = clock.GetUtcNow().AddDays(-days);
            // Hosted service là singleton; DbContextFactory đăng ký scoped nên phải tạo scope riêng.
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var logs = await db.GateLogs.Where(log => log.OccurredAt < cutoff).ExecuteDeleteAsync(cancellationToken);
            var invites = await db.GatePassInvites
                .Where(invite => invite.ExpiresAt < cutoff || (invite.RevokedAt != null && invite.RevokedAt < cutoff))
                .ExecuteDeleteAsync(cancellationToken);
            var passes = await db.GatePasses.Where(pass => pass.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);

            if (logs + invites + passes > 0)
            {
                logger.LogInformation(
                    "Dữ liệu cổng quá {Days} ngày: đã xoá {Logs} nhật ký, {Invites} liên kết, {Passes} phiếu.",
                    days, logs, invites, passes);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Dừng app bình thường.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không dọn được dữ liệu cổng quá hạn.");
        }
    }
}
