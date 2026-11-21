using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Assistant.DTOs;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Maintenance.Enums;
using Web.Features.Properties;

namespace Web.Features.Assistant.Services;

/// <summary>
/// Ghép dữ liệu cho trang Trợ lý AI: lựa chọn tòa/hóa đơn/sự cố trong phạm vi người gọi,
/// cài đặt hiệu lực, và chuyển tiếp các thao tác sang <see cref="AiSuggestionService"/>.
/// </summary>
public sealed class AiAssistantPageService(
    ApplicationDbContext db,
    AiSettingsService settings,
    IPropertyService properties,
    IInvoiceQueryService invoices,
    IOptions<AiOptions> options)
{
    public async Task<AiSnapshotResult> GetSnapshotAsync(
        PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager)
        {
            return AiSnapshotResult.Failure(
                AiErrorCodes.Forbidden, "Vai trò hiện tại không dùng được trợ lý AI.");
        }

        var settingsResult = await GetSettingsAsync(caller, ct);
        if (!settingsResult.Succeeded || settingsResult.Settings is null)
        {
            return AiSnapshotResult.Failure(
                settingsResult.ErrorCode ?? AiErrorCodes.Forbidden,
                settingsResult.ErrorMessage ?? "Không đọc được cài đặt trợ lý AI.");
        }

        var propertyRows = await properties.ListOptionsAsync(caller, ct);
        var propertyOptions = propertyRows
            .Select(row => new AiPropertyOption(row.Id, row.Name))
            .ToList();

        var invoiceList = await invoices.GetInvoicesAsync(new InvoiceFilter
        {
            Status = InvoiceStatus.Issued,
            Page = 1,
            PageSize = 50,
        }, caller, ct);
        var invoiceOptions = invoiceList.Items
            .Where(item => item.OutstandingAmount > 0)
            .Select(item => new AiInvoiceOption(
                item.Id,
                $"{item.RoomCode} · {item.ContractNumber} · còn {item.OutstandingAmount:N0}đ",
                item.OutstandingAmount,
                item.DueDate))
            .ToList();

        var scopedPropertyIds = PropertyScope.Apply(
                db.Properties.AsNoTracking(),
                db.PropertyManagers.AsNoTracking(),
                caller)
            .Where(property => !property.IsDeleted)
            .Select(property => property.Id);

        var incidentRows = await (
                from row in db.MaintenanceIncidents.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on row.RoomId equals room.Id
                where !row.IsDeleted
                    && !room.IsDeleted
                    && row.Status != IncidentStatus.Completed
                    && scopedPropertyIds.Contains(room.PropertyId)
                orderby row.CreatedAt descending, row.Id descending
                select new { row.Id, room.Code, row.Title })
            .Take(50)
            .ToListAsync(ct);
        var incidentOptions = incidentRows
            .Select(row => new AiIncidentOption(row.Id, $"{row.Code} · {row.Title}"))
            .ToList();

        return AiSnapshotResult.Success(new AiAssistantSnapshot
        {
            Properties = propertyOptions,
            Invoices = invoiceOptions,
            Incidents = incidentOptions,
            Settings = settingsResult.Settings,
        });
    }

    public async Task<AiSettingsResult> GetSettingsAsync(
        PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return await BuildManagerSettingsAsync(caller, ct);
        }

        return AiSettingsResult.Success(await settings.GetAsync(caller.UserId, ct));
    }

    /// <summary>Chỉ Chủ trọ đổi cài đặt; Quản lý kế thừa mức chặt nhất của các tòa được giao.</summary>
    public async Task<AiSettingsResult> SaveSettingsAsync(
        PropertyCaller caller, bool enabled, int dailyLimit, CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return AiSettingsResult.Failure(
                AiErrorCodes.Forbidden, "Chỉ Chủ trọ đổi được cài đặt trợ lý AI.");
        }

        return await settings.SaveAsync(caller.UserId, enabled, dailyLimit, ct);
    }

    private async Task<AiSettingsResult> BuildManagerSettingsAsync(
        PropertyCaller caller, CancellationToken ct)
    {
        var access = await settings.ResolveAccessAsync(caller, null, ct);
        if (!access.Allowed)
        {
            return AiSettingsResult.Failure(
                access.ErrorCode ?? AiErrorCodes.Forbidden,
                access.ErrorMessage ?? "Bạn chưa được phân công tòa nhà nào.");
        }

        var usedToday = await settings.CountTodayAsync(caller.UserId, ct);
        var configured = options.Value;
        var isDemo = string.Equals(configured.Provider, "Demo", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(configured.BaseUrl);

        return AiSettingsResult.Success(new AiSettingsDto(
            access.Enabled,
            access.DailyLimit,
            usedToday,
            configured.DefaultDailyLimit,
            configured.Provider,
            configured.Model,
            isDemo));
    }
}
