using Web.Common.SoftDelete;

namespace Web.Features.Utilities.Entities;

/// <summary>
/// Đăng ký dịch vụ hằng tháng của một hợp đồng (phòng).
/// Khách thuê chọn/hủy các dịch vụ của nhà trọ áp cho phòng mình; P5 chỉ tính
/// các dịch vụ đã đăng ký. Chưa có dòng đăng ký nào → giữ hành vi cũ (áp tất cả).
/// </summary>
public sealed class ContractServiceRegistration : ISoftDeletable
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    public int ServiceCatalogId { get; set; }

    /// <summary>Tháng hiệu lực (ngày 1 của tháng) mà đăng ký này áp dụng.</summary>
    public DateOnly MonthStart { get; set; }

    public string RegisteredByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Xóa mềm khi khách hủy đăng ký: giữ lịch sử để có thể khôi phục từ thùng rác.</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
