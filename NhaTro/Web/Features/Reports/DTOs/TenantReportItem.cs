namespace Web.Features.Reports.DTOs;

/// <summary>
/// Một dòng nguồn khách thuê P8 (VHT-46). Cố ý KHÔNG có trường CCCD/CMND
/// (kể cả bản che) để báo cáo không trả dữ liệu giấy tờ.
/// </summary>
public sealed class TenantReportItem
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>Null khi vai trò không được xem trạng thái tài khoản app (D9).</summary>
    public required bool? HasAccount { get; init; }

    /// <summary>Đã duyệt và hôm nay nằm trong [StartDate, EndDate].</summary>
    public required bool HasActiveContract { get; init; }

    /// <summary>Mã phòng của hợp đồng đang hiệu lực; null khi chưa thuê hoặc có nhiều hợp đồng.</summary>
    public string? RoomCode { get; init; }

    /// <summary>Tên nhà trọ của hợp đồng đang hiệu lực; null khi chưa thuê.</summary>
    public string? PropertyName { get; init; }
}
