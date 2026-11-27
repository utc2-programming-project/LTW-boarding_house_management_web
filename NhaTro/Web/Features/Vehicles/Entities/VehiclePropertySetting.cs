using Web.Features.Vehicles.Rules;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Cấu hình quản lý bãi xe theo từng nhà trọ (1 bản ghi / PropertyId).
/// Tự khởi tạo với giá mặc định 50.000đ / 80.000đ / 100.000đ khi truy cập lần đầu.
/// </summary>
public sealed class VehiclePropertySetting
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public decimal ElectricMotorbikeMonthlyFee { get; set; } = VehicleRules.DefaultElectricMotorbikeMonthlyFee;
    public decimal ManualMotorbikeMonthlyFee { get; set; } = VehicleRules.DefaultManualMotorbikeMonthlyFee;
    public decimal ScooterMonthlyFee { get; set; } = VehicleRules.DefaultScooterMonthlyFee;
    public int MaxVehiclesPerRoom { get; set; } = VehicleRules.DefaultMaxVehiclesPerRoom;
    public bool EnableParkingZones { get; set; }
    public int ZoneNearFullThresholdPercent { get; set; } = VehicleRules.DefaultZoneNearFullThresholdPercent;
    public bool AllowTenantGuestNotice { get; set; } = true;
    public bool AllowBlockingAlert { get; set; } = true;
    public bool AllowManagerManageVehicles { get; set; } = true;
    public string? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
