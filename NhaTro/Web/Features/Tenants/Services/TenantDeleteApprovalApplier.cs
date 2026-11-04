using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;

namespace Web.Features.Tenants.Services;

/// <summary>
/// VHT-99 — áp dụng yêu cầu SW-06 khi Chủ trọ duyệt: đưa hồ sơ vào thùng rác trong cùng
/// transaction với quyết định duyệt. Không xóa cứng hồ sơ/tệp giấy tờ và giữ nguyên UserId.
/// </summary>
public sealed class TenantDeleteApprovalApplier(TimeProvider clock) : IApprovalApplier
{
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

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
        if (tenant is null)
        {
            throw new ApprovalApplyException(
                TenantErrorCodes.TenantNotFound, TenantErrorMessages.TenantNotFound);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (await TenantService.HasActiveContractAsync(db, tenantId, now.Date, ct))
        {
            throw new ApprovalApplyException(TenantErrorCodes.TenantInUse, TenantErrorMessages.TenantInUse);
        }

        // Lệch token nghĩa là hồ sơ đã đổi sau khi gửi yêu cầu ⇒ SaveChanges báo Conflict.
        db.Entry(tenant).Property(t => t.RowVersion).OriginalValue = request.ExpectedToken;
        tenant.IsDeleted = true;
        tenant.DeletedAt = now;
        tenant.DeletedByUserId = request.RequestedByUserId;
        tenant.UpdatedAt = now;
    }

    private static ApprovalApplyException Invalid() =>
        new(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
}
