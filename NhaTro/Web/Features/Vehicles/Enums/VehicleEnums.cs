namespace Web.Features.Vehicles.Enums;

/// <summary>
/// D-VH-01: Chỉ quản lý 3 loại xe 2 bánh bắt buộc biển số.
/// </summary>
public enum VehicleType
{
    ElectricMotorbike = 1,
    ManualMotorbike = 2,
    Scooter = 3,
}

/// <summary>
/// Cờ tổ hợp loại xe được phép đỗ trong một khu đỗ xe (ParkingZone).
/// </summary>
[Flags]
public enum AllowedVehicleTypeFlags
{
    None = 0,
    ElectricMotorbike = 1 << 0,
    ManualMotorbike = 1 << 1,
    Scooter = 1 << 2,
    All = ElectricMotorbike | ManualMotorbike | Scooter,
}

/// <summary>
/// D-VH-06: Trạng thái vòng đời của xe (giữ lịch sử, không xóa).
/// </summary>
public enum VehicleStatus
{
    Active = 1,
    Inactive = 2,
}

/// <summary>
/// Trạng thái ô đỗ xe cố định trong khu đỗ có chia ô.
/// </summary>
public enum ParkingSlotStatus
{
    Available = 1,
    Occupied = 2,
    Maintenance = 3,
}

/// <summary>
/// Trạng thái phiếu báo xe bạn gửi nhờ / qua đêm.
/// </summary>
public enum GuestVehicleNoticeStatus
{
    Active = 1,
    Completed = 2,
    Expired = 3,
    Cancelled = 4,
}

/// <summary>
/// Trạng thái phiên kiểm kê bãi xe định kỳ.
/// </summary>
public enum VehicleAuditSessionStatus
{
    InProgress = 1,
    Completed = 2,
}

/// <summary>
/// Kết quả kiểm kê từng xe trong phiên kiểm kê.
/// </summary>
public enum VehicleAuditItemResult
{
    Pending = 0,
    Present = 1,
    Missing = 2,
}
