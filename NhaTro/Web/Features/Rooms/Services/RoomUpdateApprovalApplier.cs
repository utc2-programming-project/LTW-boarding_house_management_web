using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Mapping;
using Web.Infrastructure.Time;

namespace Web.Features.Rooms.Services;

/// <summary>
/// Áp dụng yêu cầu SW-01/SW-02/SW-03 khi Chủ trọ duyệt: ghi toàn bộ đề xuất sửa phòng
/// trong cùng transaction của hàng đợi. Mỗi mã công tắc có một instance riêng vì
/// <see cref="IApprovalApplier.OperationCode"/> chỉ trả một giá trị.
/// </summary>
public sealed class RoomUpdateApprovalApplier(
    TimeProvider clock, string operationCode) : IApprovalApplier, IApprovalSaveExceptionMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => operationCode;

    public async Task ApplyAsync(
        ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Room
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var roomId)
            || roomId <= 0
            || request.PropertyId is null)
        {
            throw Invalid(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidRequest);
        }

        RoomUpdateApprovalPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RoomUpdateApprovalPayload>(request.PayloadJson, PayloadJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ApprovalApplyException(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidRequest, ex);
        }

        if (payload?.Values is null || RoomService.Validate(payload.Values) is not null)
        {
            throw Invalid(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidRequest);
        }

        var room = await db.Rooms.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == roomId && !item.IsDeleted, ct);
        if (room is null)
        {
            throw Invalid(RoomErrorCodes.RoomNotFound, RoomErrorMessages.RoomNotFound);
        }

        if (room.PropertyId != request.PropertyId.Value)
        {
            throw Invalid(RoomErrorCodes.PropertyNotFound, RoomErrorMessages.PropertyNotFound);
        }

        // Phòng không có rowversion: UpdatedAt đóng vai trò token để không ghi đè thay đổi
        // phát sinh sau khi Quản lý gửi yêu cầu.
        if ((room.UpdatedAt?.Ticks ?? 0) != payload.OriginalUpdatedAtTicks)
        {
            throw new ApprovalApplyException(ApprovalErrorCodes.Conflict, ApprovalErrorMessages.Conflict);
        }

        if (await RoomService.RoomCodeExistsAsync(
                db, payload.Values.PropertyId, payload.Values.Code, roomId, ct))
        {
            throw Invalid(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        RoomMapping.ApplySave(payload.Values, room);
        room.UpdatedAt = clock.UtcNow();
    }

    public ApprovalApplyException? MapSaveException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return Invalid(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
            }
        }

        return null;
    }

    private static ApprovalApplyException Invalid(string errorCode, string message) =>
        new(errorCode, message);
}

/// <summary>Dữ liệu đề xuất sửa phòng kèm mốc cập nhật lúc gửi duyệt.</summary>
public sealed record RoomUpdateApprovalPayload(RoomSave Values, long OriginalUpdatedAtTicks);
