using Web.Features.Contracts.DTOs;

namespace Web.Features.Contracts.MockData;

/// <summary>Explicitly fictitious picker and tariff values for the contract form preview.</summary>
public static class ContractFormSampleData
{
    public static IReadOnlyList<ContractPropertySample> Properties { get; } =
    [
        new(1, "Tòa nhà An Gia 01", "12 Nguyễn Văn Bảo, Gò Vấp"),
        new(2, "Nhà trọ Bình Minh", "48 Phan Văn Trị, Gò Vấp"),
    ];

    public static IReadOnlyList<ContractRoomSample> Rooms { get; } =
    [
        new(101, 1, "P101", 1, 24m, 4_500_000m, true),
        new(102, 1, "P102", 1, 20m, 3_800_000m, true),
        new(103, 1, "P203", 2, 25m, 4_800_000m, false, new DateOnly(2026, 11, 1)),
        new(201, 2, "P201", 2, 27m, 5_000_000m, true),
        new(202, 2, "P202", 2, 22m, 4_200_000m, false),
    ];

    public static IReadOnlyList<ContractTenantSample> Tenants { get; } =
    [
        new(1, "Nguyễn Văn An", "0908889901", "********1234"),
        new(2, "Trần Thị Bình", "0908889902", "********2345"),
        new(3, "Lê Hoàng Minh", "0908889903", "********3456"),
        new(4, "Phạm Ngọc Lan", "0908889904", "********4567"),
        new(5, "Vũ Đức Long", "0908889905", "********5678"),
    ];

    public static IReadOnlyList<ContractUtilitySample> Utilities { get; } =
    [
        new("ContractElectricity", 3_500m, "ContractPerKwh"),
        new("ContractWater", 20_000m, "ContractPerCubicMeter"),
        new("ContractInternet", 100_000m, "ContractPerMonth"),
        new("ContractWaste", 50_000m, "ContractPerMonth"),
    ];
}
