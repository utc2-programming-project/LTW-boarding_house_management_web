using Web.Common.Utils;
using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.MockData;

public static class UtilitySampleData
{
    public static readonly IReadOnlyList<BuildingOptionDto> Buildings =
    [
        new("all", "Tất cả nhà trọ", "Toàn bộ hệ thống", 20, 19),
        new("b1", "Tòa nhà An Gia 01", "128 Ung Văn Khiêm, P. 25, Q. Bình Thạnh, TP. Hồ Chí Minh", 12, 11),
        new("b2", "Nhà trọ Bình Minh", "45/12 Đường số 8, P. Linh Trung, TP. Thủ Đức, TP. Hồ Chí Minh", 8, 8),
    ];

    public static List<ServiceItemDto> CreateDefaultServices() =>
    [
        new()
        {
            Id = "srv-1",
            Name = "Điện sinh hoạt",
            Code = "ELEC",
            Type = ServiceTypeCategory.Meter,
            TypeLabel = "Theo chỉ số",
            Unit = "kWh",
            RateType = ServiceRateType.Tiered,
            CurrentRate = 2_000m,
            RateDisplay = "Bậc thang · 3 bậc",
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Active = true,
            IsReadOnly = true,
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            Description = "Tính theo bậc thang quy định cố định hệ thống (Bậc 1: 1.800 ₫, Bậc 2: 2.000 ₫, Bậc 3: 3.000 ₫)",
            HistoryCount = 3,
        },
        new()
        {
            Id = "srv-2",
            Name = "Nước sinh hoạt",
            Code = "WATER",
            Type = ServiceTypeCategory.Meter,
            TypeLabel = "Theo chỉ số",
            Unit = "m³",
            RateType = ServiceRateType.Flat,
            CurrentRate = 15_000m,
            RateDisplay = MoneyFormatter.Format(15_000m),
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Active = true,
            IsReadOnly = true,
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            Description = "Chỉ số nước sinh hoạt qua đồng hồ phòng riêng hoặc chia đều theo thỏa thuận",
            HistoryCount = 2,
        },
        new()
        {
            Id = "srv-3",
            Name = "Internet cáp quang",
            Code = "NET",
            Type = ServiceTypeCategory.FixedMonthly,
            TypeLabel = "Cố định hàng tháng",
            Unit = "Phòng",
            RateType = ServiceRateType.Flat,
            CurrentRate = 100_000m,
            RateDisplay = MoneyFormatter.Format(100_000m),
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Active = true,
            IsReadOnly = false,
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            Description = "Gói cước băng thông cao 300Mbps phân bổ theo từng phòng",
            HistoryCount = 4,
        },
        new()
        {
            Id = "srv-4",
            Name = "Vệ sinh & Môi trường",
            Code = "CLEAN",
            Type = ServiceTypeCategory.FixedMonthly,
            TypeLabel = "Cố định hàng tháng",
            Unit = "Người",
            RateType = ServiceRateType.Flat,
            CurrentRate = 30_000m,
            RateDisplay = MoneyFormatter.Format(30_000m),
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Active = true,
            IsReadOnly = false,
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            Description = "Dọn dẹp hành lang, khử khuẩn định kỳ, thu gom rác thải chung",
            HistoryCount = 2,
        },
        new()
        {
            Id = "srv-5",
            Name = "Gửi xe máy",
            Code = "PARK",
            Type = ServiceTypeCategory.FixedMonthly,
            TypeLabel = "Cố định hàng tháng",
            Unit = "Xe",
            RateType = ServiceRateType.Flat,
            CurrentRate = 80_000m,
            RateDisplay = MoneyFormatter.Format(80_000m),
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Active = false,
            IsReadOnly = false,
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            Description = "Bãi đỗ xe máy tầng hầm có camera giám sát và bảo vệ 24/7",
            HistoryCount = 2,
        },
    ];

    public static List<PriceVersionDto> CreateDefaultPriceVersions() =>
    [
        new()
        {
            Id = "pv-3-1",
            ServiceId = "srv-3",
            ServiceName = "Internet cáp quang",
            Unit = "Phòng",
            Rate = 100_000m,
            RateType = ServiceRateType.Flat,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Status = "active",
            BuildingScope = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            AutoActivate = true,
            CreatedAt = new DateTime(2025, 12, 15, 8, 30, 0, DateTimeKind.Utc),
            CreatedBy = "Chủ trọ An",
            Note = "Nâng cấp băng thông lên 300Mbps cho toàn tòa",
        },
        new()
        {
            Id = "pv-3-2",
            ServiceId = "srv-3",
            ServiceName = "Internet cáp quang",
            Unit = "Phòng",
            Rate = 90_000m,
            RateType = ServiceRateType.Flat,
            EffectiveFrom = new DateOnly(2025, 6, 1),
            EffectiveTo = new DateOnly(2025, 12, 31),
            Status = "expired",
            BuildingScope = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            AutoActivate = true,
            CreatedAt = new DateTime(2025, 5, 20, 10, 0, 0, DateTimeKind.Utc),
            CreatedBy = "Chủ trọ An",
            Note = "Gói cước 150Mbps cũ",
        },
        new()
        {
            Id = "pv-3-3",
            ServiceId = "srv-3",
            ServiceName = "Internet cáp quang",
            Unit = "Phòng",
            Rate = 80_000m,
            RateType = ServiceRateType.Flat,
            EffectiveFrom = new DateOnly(2025, 1, 1),
            EffectiveTo = new DateOnly(2025, 5, 31),
            Status = "expired",
            BuildingScope = "all",
            BuildingName = "Tất cả nhà trọ",
            AutoActivate = true,
            CreatedAt = new DateTime(2024, 12, 10, 9, 0, 0, DateTimeKind.Utc),
            CreatedBy = "Chủ trọ An",
            Note = "Gói khởi điểm ban đầu",
        },
        new()
        {
            Id = "pv-4-1",
            ServiceId = "srv-4",
            ServiceName = "Vệ sinh & Môi trường",
            Unit = "Người",
            Rate = 30_000m,
            RateType = ServiceRateType.Flat,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Status = "active",
            BuildingScope = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            AutoActivate = true,
            CreatedAt = new DateTime(2025, 12, 20, 14, 0, 0, DateTimeKind.Utc),
            CreatedBy = "Chủ trọ An",
            Note = "Áp dụng định kỳ dọn vệ sinh 3 lần/tuần",
        },
        new()
        {
            Id = "pv-5-1",
            ServiceId = "srv-5",
            ServiceName = "Gửi xe máy",
            Unit = "Xe",
            Rate = 80_000m,
            RateType = ServiceRateType.Flat,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            Status = "active",
            BuildingScope = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            AutoActivate = true,
            CreatedAt = new DateTime(2025, 12, 20, 14, 30, 0, DateTimeKind.Utc),
            CreatedBy = "Chủ trọ An",
            Note = "Giữ xe máy cố định",
        },
    ];

    public static List<RoomReadingSampleDto> CreateDefaultRoomReadings() =>
    [
        new()
        {
            Id = "room-101",
            RoomCode = "P101",
            RoomName = "Phòng 101",
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            ContractCode = "HD-2026-001",
            TenantName = "Nguyễn Văn An",
            TenantPhone = "0908889901",
            RoomRent = 3_000_000m,
            PeriodMonth = "Tháng 10/2026",
            PeriodStart = new DateOnly(2026, 10, 1),
            PeriodEnd = new DateOnly(2026, 10, 31),
            Status = "open",
            ElectricMeterCode = "EM-101",
            ElectricStart = 1_450m,
            ElectricEnd = 1_570m, // Usage: 120 kWh
            WaterMeterCode = "WM-101",
            WaterStart = 124m,
            WaterEnd = 130m, // Usage: 6 m3
            WaterRate = 15_000m,
            InternetCost = 100_000m,
            CleaningCost = 30_000m,
            ParkingCost = 0m,
            RefCode = "REF-P101-202610",
            AuditLogs =
            [
                new()
                {
                    Id = "log-1",
                    Modifier = "Chủ trọ An",
                    Role = "Chủ trọ",
                    Timestamp = new DateTime(2026, 10, 20, 14, 30, 0, DateTimeKind.Utc),
                    FieldChanged = "Chỉ số điện cuối",
                    OldValue = "1560",
                    NewValue = "1570",
                    DetailDiff = "1.560 → 1.570 kWh",
                    Reason = "Ghi nhầm số đồng hồ thực tế kiểm tra lại",
                }
            ]
        },
        new()
        {
            Id = "room-102",
            RoomCode = "P102",
            RoomName = "Phòng 102",
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            ContractCode = "HD-2026-002",
            TenantName = "Trần Thị Bình",
            TenantPhone = "0908889902",
            RoomRent = 3_200_000m,
            PeriodMonth = "Tháng 10/2026",
            PeriodStart = new DateOnly(2026, 10, 1),
            PeriodEnd = new DateOnly(2026, 10, 31),
            Status = "open",
            ElectricMeterCode = "EM-102",
            ElectricStart = 980m,
            ElectricEnd = 1_065m, // Usage: 85 kWh
            WaterMeterCode = "WM-102",
            WaterStart = 85m,
            WaterEnd = 91m, // Usage: 6 m3
            WaterRate = 15_000m,
            InternetCost = 100_000m,
            CleaningCost = 30_000m,
            ParkingCost = 80_000m,
            RefCode = "REF-P102-202610",
            AuditLogs = []
        },
        new()
        {
            Id = "room-201",
            RoomCode = "P201",
            RoomName = "Phòng 201",
            BuildingId = "b1",
            BuildingName = "Tòa nhà An Gia 01",
            ContractCode = "HD-2026-003",
            TenantName = "Lê Hoàng Minh",
            TenantPhone = "0908889903",
            RoomRent = 3_500_000m,
            PeriodMonth = "Tháng 10/2026",
            PeriodStart = new DateOnly(2026, 10, 1),
            PeriodEnd = new DateOnly(2026, 10, 31),
            Status = "open",
            ElectricMeterCode = "EM-201",
            ElectricStart = 2_110m,
            ElectricEnd = 2_200m, // Usage: 90 kWh
            WaterMeterCode = "WM-201",
            WaterStart = 160m,
            WaterEnd = 168m, // Usage: 8 m3
            WaterRate = 15_000m,
            InternetCost = 100_000m,
            CleaningCost = 30_000m,
            ParkingCost = 160_000m,
            RefCode = "REF-P201-202610",
            AuditLogs = []
        },
    ];
}

