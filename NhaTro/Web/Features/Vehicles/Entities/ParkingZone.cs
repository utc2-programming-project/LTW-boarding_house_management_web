using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Khu vực đỗ xe của nhà trọ (khi bật EnableParkingZones = true).
/// Hỗ trợ ràng buộc loại xe được phép đỗ (ví dụ: khu trong nhà không cho phép xe máy điện).
/// </summary>
public sealed class ParkingZone
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsIndoor { get; set; } = true;
    public int Capacity { get; set; } = 20;
    public AllowedVehicleTypeFlags AllowedVehicleTypes { get; set; } = AllowedVehicleTypeFlags.All;
    public bool HasSlots { get; set; }
    /// <summary>Tiền tố mã ô đỗ đã dùng gần nhất (ví dụ "A", "B"); giữ nguyên khi sửa khu không còn ô nào.</summary>
    public string? SlotPrefix { get; set; }
    public bool IsFree { get; set; }
    public decimal? OverrideMonthlyFee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
