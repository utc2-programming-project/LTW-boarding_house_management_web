using Web.Features.Utilities.Enums;

namespace Web.Features.Utilities.Entities;

/// <summary>
/// Kỳ ghi chỉ số thật của một hợp đồng (VHT-51). Một hợp đồng chỉ có một kỳ cho mỗi ngày
/// bắt đầu kỳ; chỉ số đầu kỳ lấy từ chỉ số cuối kỳ liền trước đã lưu trong bảng này,
/// không lấy từ dữ liệu mẫu.
/// </summary>
public sealed class UtilityReadingPeriod
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    /// <summary>Ảnh chụp phòng tại thời điểm mở kỳ, dùng để tra danh mục dịch vụ của tòa.</summary>
    public int RoomId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal ElectricStart { get; set; }

    public decimal ElectricEnd { get; set; }

    public decimal WaterStart { get; set; }

    public decimal WaterEnd { get; set; }

    /// <summary>Số người tính theo thành viên hợp đồng tại thời điểm mở kỳ; dùng cho biểu giá theo người.</summary>
    public int ResidentCount { get; set; } = 1;

    public UtilityReadingPeriodStatus Status { get; set; } = UtilityReadingPeriodStatus.Open;

    /// <summary>Hóa đơn đã phát hành cho kỳ; có giá trị thì D10 chặn mọi thay đổi chỉ số.</summary>
    public int? InvoiceId { get; set; }

    /// <summary>Đã nhập chỉ số cuối kỳ (phân biệt "chưa nhập" với "bằng 0").</summary>
    public bool HasReadings { get; set; }

    /// <summary>
    /// Đã chụp các dòng dịch vụ khi chốt chỉ số. Kỳ cũ trước migration giữ false để không
    /// suy diễn snapshot bằng giá hiện hành.
    /// </summary>
    public bool HasServiceSnapshots { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Chống ghi đè đồng thời khi hai người cùng nhập chỉ số kỳ.</summary>
    public byte[] RowVersion { get; set; } = [];
}
