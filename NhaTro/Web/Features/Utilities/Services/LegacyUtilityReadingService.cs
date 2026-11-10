using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Infrastructure.Time;

namespace Web.Features.Utilities.Services;

public static class UtilityReadingErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string OutsideScope = nameof(OutsideScope);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string ReadingNotFound = nameof(ReadingNotFound);
    public const string InvalidPeriod = nameof(InvalidPeriod);
    public const string InvalidReading = nameof(InvalidReading);
    public const string AdjustmentReasonRequired = nameof(AdjustmentReasonRequired);
    public const string NotAdjustable = nameof(NotAdjustable);
    public const string ApprovalRequired = nameof(ApprovalRequired);
    public const string ApprovalDenied = nameof(ApprovalDenied);
    public const string ApprovalFailed = nameof(ApprovalFailed);
}

/// <summary>
/// Legacy P5 reading flow on <see cref="UtilityReading"/> (pre-VHT-51): Chủ trọ toàn bộ tòa mình,
/// Quản lý chỉ tòa được giao, Khách thuê chỉ đọc phòng mình. Giữ lại cho applier SW-09 và probe
/// ma trận quyền đến khi VHT-102 chuyển sang <see cref="UtilityReadingPeriod"/>.
/// </summary>
public sealed class LegacyUtilityReadingService(
    ApplicationDbContext db,
    TimeProvider clock,
    IManagerTogglePolicy? togglePolicy = null,
    IManagerApprovalService? approvals = null)
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<UtilityReadingDto>> ViewAsync(
        PropertyCaller caller,
        int? propertyId = null,
        int? roomId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.IsAdmin)
        {
            throw Forbidden();
        }

        IQueryable<int> visibleRoomIds;
        if (caller.IsKhachThue)
        {
            visibleRoomIds = from contract in db.Contracts.AsNoTracking()
                             join member in db.ContractMembers.AsNoTracking()
                                 on contract.ContractId equals member.ContractId
                             join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking()
                                 on member.TenantId equals tenant.Id
                             where tenant.UserId == caller.UserId
                                 && contract.Status == Contracts.Enums.ContractStatus.Approved
                             select contract.RoomId;
        }
        else if (caller.IsChuTro || caller.IsQuanLy)
        {
            visibleRoomIds = VisibleRoomIds(caller);
        }
        else
        {
            throw Forbidden();
        }

        if (roomId is { } requestedRoomId
            && !await visibleRoomIds.AnyAsync(id => id == requestedRoomId, cancellationToken))
        {
            throw new UtilityReadingAuthorizationException(
                "Phòng không thuộc phạm vi được phép xem chỉ số.",
                UtilityReadingErrorCodes.OutsideScope);
        }

        var query = db.Set<UtilityReading>().AsNoTracking()
            .Where(reading => visibleRoomIds.Contains(reading.RoomId));

        if (roomId is { } filterRoomId)
        {
            query = query.Where(reading => reading.RoomId == filterRoomId);
        }

        if (propertyId is { } filterPropertyId)
        {
            var roomIdsInProperty = db.Rooms.IgnoreQueryFilters().AsNoTracking()
                .Where(room => room.PropertyId == filterPropertyId)
                .Select(room => room.Id);
            query = query.Where(reading => roomIdsInProperty.Contains(reading.RoomId));
        }

        return await query
            .OrderByDescending(reading => reading.PeriodStart)
            .ThenBy(reading => reading.RoomId)
            .Select(reading => new UtilityReadingDto(
                reading.Id,
                reading.RoomId,
                reading.PeriodStart,
                reading.PeriodEnd,
                reading.ElectricStart,
                reading.ElectricEnd,
                reading.WaterStart,
                reading.WaterEnd,
                reading.ElectricEnd - reading.ElectricStart,
                reading.WaterEnd - reading.WaterStart,
                reading.RecordedByUserId,
                reading.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<UtilityReadingDto> RecordAsync(
        RecordUtilityReadingRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw Forbidden();
        }

        await EnsureRoomInScopeAsync(request.RoomId, caller, cancellationToken);

        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new UtilityReadingException(
                "Ngày kết thúc kỳ phải sau hoặc bằng ngày bắt đầu.",
                UtilityReadingErrorCodes.InvalidPeriod);
        }

        var previous = await db.Set<UtilityReading>().AsNoTracking()
            .Where(reading => reading.RoomId == request.RoomId && reading.PeriodStart < request.PeriodStart)
            .OrderByDescending(reading => reading.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

        var electricStart = previous?.ElectricEnd ?? 0m;
        var waterStart = previous?.WaterEnd ?? 0m;

        if (request.ElectricEnd < electricStart || request.WaterEnd < waterStart)
        {
            throw new UtilityReadingException(
                "Chỉ số cuối kỳ phải lớn hơn hoặc bằng chỉ số đầu kỳ.",
                UtilityReadingErrorCodes.InvalidReading);
        }

        var reading = new UtilityReading
        {
            RoomId = request.RoomId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            ElectricStart = electricStart,
            ElectricEnd = request.ElectricEnd,
            WaterStart = waterStart,
            WaterEnd = request.WaterEnd,
            RecordedByUserId = caller.UserId,
            CreatedAt = clock.UtcNow(),
        };

        db.Set<UtilityReading>().Add(reading);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(reading);
    }

    /// <summary>
    /// D10 — chỉ sửa kỳ gần nhất của phòng, chưa có hóa đơn đã phát hành phủ kỳ đó,
    /// bắt buộc lý do và ghi lịch sử. Quản lý đi qua SW-09.
    /// </summary>
    public async Task<UtilityReadingDto> AdjustAsync(
        int readingId,
        AdjustUtilityReadingRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw Forbidden();
        }

        var reading = await LoadInScopeAsync(readingId, caller, cancellationToken);
        await EnsureAdjustableAsync(db, reading, cancellationToken);

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new UtilityReadingException(
                "Phải nhập lý do điều chỉnh chỉ số.",
                UtilityReadingErrorCodes.AdjustmentReasonRequired);
        }

        if (request.ElectricEnd < reading.ElectricStart || request.WaterEnd < reading.WaterStart)
        {
            throw new UtilityReadingException(
                "Chỉ số cuối kỳ phải lớn hơn hoặc bằng chỉ số đầu kỳ.",
                UtilityReadingErrorCodes.InvalidReading);
        }

        if (caller.IsQuanLy)
        {
            var decision = togglePolicy is null
                ? RoomChangeDecision.RequiresApproval
                : await togglePolicy.EvaluateAsync(ManagerAction.SW09, caller.Role, cancellationToken);

            if (decision == RoomChangeDecision.Denied)
            {
                throw new UtilityReadingException(
                    "Công tắc SW-09 đang chặn sửa chỉ số kỳ trước.",
                    UtilityReadingErrorCodes.ApprovalDenied);
            }

            if (decision == RoomChangeDecision.RequiresApproval)
            {
                await CreateApprovalRequestAsync(reading, request, reason, caller, cancellationToken);
                throw new UtilityReadingException(
                    "Sửa chỉ số đang chờ Chủ trọ duyệt.",
                    UtilityReadingErrorCodes.ApprovalRequired);
            }
        }

        Apply(reading, request, reason, caller.UserId);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(reading);
    }

    public async Task<IReadOnlyList<UtilityReadingHistoryDto>> HistoryAsync(
        int readingId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw Forbidden();
        }

        var reading = await LoadInScopeAsync(readingId, caller, cancellationToken);

        return await db.Set<UtilityReadingAudit>().AsNoTracking()
            .Where(audit => audit.UtilityReadingId == reading.Id)
            .OrderByDescending(audit => audit.ChangedAt)
            .Select(audit => new UtilityReadingHistoryDto(
                audit.Id,
                audit.UtilityReadingId,
                audit.OldElectricEnd,
                audit.NewElectricEnd,
                audit.OldWaterEnd,
                audit.NewWaterEnd,
                audit.Reason,
                audit.ChangedByUserId,
                audit.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Dùng lại bởi applier SW-09 và probe để cùng một chốt kiểm D10.</summary>
    public static async Task EnsureAdjustableAsync(
        ApplicationDbContext db,
        UtilityReading reading,
        CancellationToken cancellationToken = default)
    {
        var hasNewer = await db.Set<UtilityReading>().AsNoTracking()
            .AnyAsync(other => other.RoomId == reading.RoomId
                && other.PeriodStart > reading.PeriodStart, cancellationToken);

        if (hasNewer)
        {
            throw new UtilityReadingException(
                "Chỉ được sửa chỉ số của kỳ gần nhất.",
                UtilityReadingErrorCodes.NotAdjustable);
        }

        var hasIssuedInvoice = await (
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking()
                on invoice.ContractId equals contract.ContractId
            where contract.RoomId == reading.RoomId
                && invoice.Status == InvoiceStatus.Issued
                && invoice.PeriodStart <= reading.PeriodEnd
                && reading.PeriodStart <= invoice.PeriodEnd
            select invoice.Id)
            .AnyAsync(cancellationToken);

        if (hasIssuedInvoice)
        {
            throw new UtilityReadingException(
                "Kỳ đã phát hành hóa đơn thì không được sửa chỉ số.",
                UtilityReadingErrorCodes.NotAdjustable);
        }
    }

    public static void Apply(
        UtilityReading reading,
        AdjustUtilityReadingRequest request,
        string reason,
        string changedByUserId)
    {
        reading.UpdatedAt = DateTimeOffset.UtcNow;
        var audit = new UtilityReadingAudit
        {
            UtilityReadingId = reading.Id,
            OldElectricEnd = reading.ElectricEnd,
            NewElectricEnd = request.ElectricEnd,
            OldWaterEnd = reading.WaterEnd,
            NewWaterEnd = request.WaterEnd,
            Reason = reason,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
        };

        // DbContext được lấy từ navigation của reading (service luôn nạp tracked entity).
        reading.ElectricEnd = request.ElectricEnd;
        reading.WaterEnd = request.WaterEnd;
        reading.UpdatedAt = audit.ChangedAt;
        reading.Audits.Add(audit);
    }

    private async Task<UtilityReading> LoadInScopeAsync(
        int readingId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var reading = await db.Set<UtilityReading>()
            .Include(item => item.Audits)
            .SingleOrDefaultAsync(item => item.Id == readingId, cancellationToken);

        if (reading is null)
        {
            throw new UtilityReadingNotFoundException(
                "Không tìm thấy chỉ số.",
                UtilityReadingErrorCodes.ReadingNotFound);
        }

        var visibleRoomIds = VisibleRoomIds(caller);
        if (!await visibleRoomIds.AnyAsync(id => id == reading.RoomId, cancellationToken))
        {
            throw new UtilityReadingAuthorizationException(
                "Chỉ số nằm ngoài phạm vi được phép.",
                UtilityReadingErrorCodes.OutsideScope);
        }

        return reading;
    }

    private IQueryable<int> VisibleRoomIds(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                   join property in db.Properties.IgnoreQueryFilters().AsNoTracking()
                       on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select room.Id;
        }

        if (caller.IsQuanLy)
        {
            return from room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                   join assignment in db.PropertyManagers.AsNoTracking()
                       on room.PropertyId equals assignment.PropertyId
                   where assignment.ManagerUserId == caller.UserId
                       && assignment.RevokedAt == null
                   select room.Id;
        }

        return db.Rooms.IgnoreQueryFilters().AsNoTracking()
            .Where(_ => false)
            .Select(room => room.Id);
    }

    private async Task EnsureRoomInScopeAsync(
        int roomId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var visibleRoomIds = VisibleRoomIds(caller);
        if (!await visibleRoomIds.AnyAsync(id => id == roomId, cancellationToken))
        {
            throw new UtilityReadingNotFoundException(
                "Không tìm thấy phòng trong phạm vi được phép.",
                UtilityReadingErrorCodes.RoomNotFound);
        }
    }

    private async Task CreateApprovalRequestAsync(
        UtilityReading reading,
        AdjustUtilityReadingRequest request,
        string reason,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (approvals is null)
        {
            throw new UtilityReadingException(
                "Không thể gửi yêu cầu duyệt.",
                UtilityReadingErrorCodes.ApprovalFailed);
        }

        var propertyId = await db.Rooms.IgnoreQueryFilters().AsNoTracking()
            .Where(room => room.Id == reading.RoomId)
            .Select(room => (int?)room.PropertyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UtilityReadingException(
                "Không xác định được tòa nhà của chỉ số.",
                UtilityReadingErrorCodes.RoomNotFound);

        var payload = new UtilityAdjustmentApprovalPayload(
            reading.Id, request.ElectricEnd, request.WaterEnd, reason);

        var approval = await approvals.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = ManagerToggleCatalog.ToCode(ManagerAction.SW09),
                TargetType = ApprovalTargetType.UtilityPeriod,
                TargetKey = reading.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PropertyId = propertyId,
                PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions),
                ExpectedToken = reading.RowVersion.Length == 0 ? null : reading.RowVersion,
            },
            new ApprovalCaller(caller.UserId, caller.Role),
            cancellationToken);

        if (!approval.Succeeded)
        {
            throw new UtilityReadingException(
                approval.ErrorMessage ?? "Không thể gửi yêu cầu duyệt.",
                UtilityReadingErrorCodes.ApprovalFailed);
        }
    }

    private static UtilityReadingAuthorizationException Forbidden() =>
        new("Không có quyền thực hiện thao tác chỉ số điện/nước.",
            UtilityReadingErrorCodes.Forbidden);

    private static UtilityReadingDto ToDto(UtilityReading reading) => new(
        reading.Id,
        reading.RoomId,
        reading.PeriodStart,
        reading.PeriodEnd,
        reading.ElectricStart,
        reading.ElectricEnd,
        reading.WaterStart,
        reading.WaterEnd,
        reading.ElectricEnd - reading.ElectricStart,
        reading.WaterEnd - reading.WaterStart,
        reading.RecordedByUserId,
        reading.CreatedAt);
}
