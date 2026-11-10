using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Áp dụng yêu cầu SW-09 khi Chủ trọ duyệt: kiểm lại D10 trên dữ liệu tại thời điểm duyệt
/// rồi mới ghi chỉ số và lịch sử. Chạy trong transaction của hàng đợi nên không tự SaveChanges.
/// </summary>
public sealed class UtilityReadingApprovalApplier : IApprovalApplier
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-09";

    public async Task ApplyAsync(
        ManagerApprovalRequest request,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.UtilityPeriod
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var readingId)
            || readingId <= 0
            || request.PropertyId is null)
        {
            throw Invalid(UtilityReadingErrorCodes.ReadingNotFound);
        }

        UtilityAdjustmentApprovalPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<UtilityAdjustmentApprovalPayload>(
                request.PayloadJson, PayloadJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ApprovalApplyException(
                UtilityReadingErrorCodes.ReadingNotFound,
                "Payload sửa chỉ số không hợp lệ.",
                ex);
        }

        if (payload is null || payload.ReadingId != readingId)
        {
            throw Invalid(UtilityReadingErrorCodes.ReadingNotFound);
        }

        var reading = await db.Set<UtilityReading>()
            .Include(item => item.Audits)
            .SingleOrDefaultAsync(item => item.Id == readingId, ct);

        if (reading is null)
        {
            throw Invalid(UtilityReadingErrorCodes.ReadingNotFound);
        }

        var roomPropertyId = await db.Rooms.IgnoreQueryFilters().AsNoTracking()
            .Where(room => room.Id == reading.RoomId)
            .Select(room => (int?)room.PropertyId)
            .SingleOrDefaultAsync(ct);

        if (roomPropertyId != request.PropertyId.Value)
        {
            throw Invalid(UtilityReadingErrorCodes.OutsideScope);
        }

        await LegacyUtilityReadingService.EnsureAdjustableAsync(db, reading, ct);

        var reason = payload.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Invalid(UtilityReadingErrorCodes.AdjustmentReasonRequired);
        }

        if (payload.ElectricEnd < reading.ElectricStart || payload.WaterEnd < reading.WaterStart)
        {
            throw Invalid(UtilityReadingErrorCodes.InvalidReading);
        }

        LegacyUtilityReadingService.Apply(
            reading,
            new AdjustUtilityReadingRequest(payload.ElectricEnd, payload.WaterEnd, reason),
            reason,
            request.RequestedByUserId ?? string.Empty);
    }

    private static ApprovalApplyException Invalid(string errorCode) =>
        new(errorCode, "Không thể áp dụng yêu cầu sửa chỉ số.");
}
