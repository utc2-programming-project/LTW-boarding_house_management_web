using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Tenants.DTOs;
using Web.Infrastructure.Time;

namespace Web.Features.Tenants.Services;

/// <summary>
/// VHT-99 — áp dụng yêu cầu SW-06 khi Chủ trọ duyệt. SW-06 dùng chung cho hai thao tác
/// trên hồ sơ: xóa mềm (đưa vào thùng rác) và bật/tắt trạng thái hoạt động; payload quyết định.
/// Không xóa cứng hồ sơ/tệp giấy tờ và giữ nguyên UserId.
/// </summary>
public sealed class TenantDeleteApprovalApplier(TimeProvider clock) : IApprovalApplier
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-06";

    public async Task ApplyAsync(
        ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Tenant
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId)
            || tenantId <= 0
            || request.ExpectedToken is not { Length: > 0 })
        {
            throw Invalid();
        }

        var action = ReadAction(request);

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
        if (tenant is null)
        {
            throw new ApprovalApplyException(
                TenantErrorCodes.TenantNotFound, TenantErrorMessages.TenantNotFound);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        db.Entry(tenant).Property(t => t.RowVersion).OriginalValue = request.ExpectedToken;

        if (action == TenantApprovalActions.SetActive)
        {
            var isActive = ReadIsActive(request);
            if (isActive && !string.IsNullOrEmpty(tenant.CitizenId)
                && await db.Tenants.AsNoTracking().AnyAsync(
                    t => t.IsActive && !t.IsDeleted && t.Id != tenantId && t.CitizenId == tenant.CitizenId, ct))
            {
                throw new ApprovalApplyException(
                    TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
            }

            if (!isActive && await TenantService.HasActiveContractAsync(
                    db, tenantId, clock.TodayVietnam().ToDateTime(TimeOnly.MinValue), ct))
            {
                throw new ApprovalApplyException(TenantErrorCodes.TenantInUse, TenantErrorMessages.TenantInUse);
            }

            tenant.IsActive = isActive;
            tenant.UpdatedAt = now;
            return;
        }

        if (await TenantService.HasActiveContractAsync(
                db, tenantId, clock.TodayVietnam().ToDateTime(TimeOnly.MinValue), ct))
        {
            throw new ApprovalApplyException(TenantErrorCodes.TenantInUse, TenantErrorMessages.TenantInUse);
        }

        // Lệch token nghĩa là hồ sơ đã đổi sau khi gửi yêu cầu ⇒ SaveChanges báo Conflict.
        tenant.IsDeleted = true;
        tenant.DeletedAt = now;
        tenant.DeletedByUserId = request.RequestedByUserId;
        tenant.UpdatedAt = now;
    }

    private static string ReadAction(ManagerApprovalRequest request)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<TenantApprovalPayload>(request.PayloadJson, PayloadJsonOptions);
            // Payload cũ (chỉ TenantId/FullName) không có Action: hiểu là xóa như trước.
            return string.IsNullOrWhiteSpace(payload?.Action) ? TenantApprovalActions.Delete : payload.Action;
        }
        catch (JsonException)
        {
            return TenantApprovalActions.Delete;
        }
    }

    private static bool ReadIsActive(ManagerApprovalRequest request)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<TenantApprovalPayload>(request.PayloadJson, PayloadJsonOptions);
            return payload?.IsActive ?? false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ApprovalApplyException Invalid() =>
        new(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
}
