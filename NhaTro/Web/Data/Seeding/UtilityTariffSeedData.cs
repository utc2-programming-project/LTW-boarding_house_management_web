using Web.Features.Utilities;
using Web.Features.Utilities.Entities;

namespace Web.Data.Seeding;

/// <summary>Reviewed, versioned utility tariffs. Update rates only through a reviewed migration.</summary>
public static class UtilityTariffSeedData
{
    public static readonly UtilityTariff[] Tariffs =
    [
        new()
        {
            Id = 3,
            Type = TariffType.Electric,
            ScopeCode = "VN-EVN-HOUSEHOLD",
            Unit = "kWh",
            IsPerResident = false,
            EffectiveDate = new DateOnly(2023, 11, 9),
            IsReadOnly = true,
            SourceName = "EVN / Ministry of Industry and Trade",
            SourceReference = "https://www.evn.com.vn/userfile/User/tcdl/files/2023-11-08%20QD%202941-QD-BCT%20quy%20dinh%20ve%20gia%20ban%20dien.PDF (Decision 2941/QĐ-BCT; household retail rates, VAT excluded)",
        },
        new()
        {
            Id = 4,
            Type = TariffType.Electric,
            ScopeCode = "VN-EVN-HOUSEHOLD",
            Unit = "kWh",
            IsPerResident = false,
            EffectiveDate = new DateOnly(2024, 10, 11),
            IsReadOnly = true,
            SourceName = "EVN / Ministry of Industry and Trade",
            SourceReference = "https://www.evn.com.vn/userfile/User/tcdl/files/2024_10_11_QD2699_QD_BCT_quydinhvegiadien%281%29.pdf (Decision 2699/QĐ-BCT; household retail rates, VAT excluded)",
        },
        new()
        {
            Id = 1,
            Type = TariffType.Electric,
            ScopeCode = "VN-EVN-HOUSEHOLD",
            Unit = "kWh",
            IsPerResident = false,
            EffectiveDate = new DateOnly(2025, 5, 10),
            IsReadOnly = true,
            SourceName = "EVN / Ministry of Industry and Trade",
            SourceReference = "https://evn.com.vn/userfile/User/tcdl/files/2025/5/BanTinEVNSo182025-20250513160725352.pdf (Decision 1279/QĐ-BCT; household retail rates, VAT excluded)",
        },
        new()
        {
            Id = 2,
            Type = TariffType.Water,
            ScopeCode = "HCMC-BENTHANH",
            Unit = "m³",
            IsPerResident = true,
            EffectiveDate = new DateOnly(2026, 1, 1),
            IsReadOnly = true,
            SourceName = "Công ty Cổ phần Cấp nước Bến Thành; Notice 3698/TB-CNBT-KDDVKH",
            SourceReference = "https://cdn.thuvienphapluat.vn//phap-luat/2022-2/NVK/2026/T4-2026/280426/thong-bao-3698-tb-cnbt-kddvkh-nam-2025.pdf (residential clean water rates; VAT and wastewater fee excluded)",
        },
        new()
        {
            Id = 5,
            Type = TariffType.Water,
            ScopeCode = "HCMC-BENTHANH",
            Unit = "m³",
            IsPerResident = true,
            EffectiveDate = new DateOnly(2025, 1, 1),
            IsReadOnly = true,
            SourceName = "Công ty Cổ phần Cấp nước Bến Thành; Notice 3698/TB-CNBT-KDDVKH",
            SourceReference = "https://cdn.thuvienphapluat.vn//phap-luat/2022-2/NVK/2026/T4-2026/280426/thong-bao-3698-tb-cnbt-kddvkh-nam-2025.pdf (states 2026 rates are unchanged from 2025; residential clean water rates, VAT and wastewater fee excluded)",
        },
    ];

    public static readonly UtilityTariffTier[] Tiers =
    [
        new() { Id = 10, UtilityTariffId = 3, TierNumber = 1, From = 0m, To = 50m, UnitPrice = 1_806m },
        new() { Id = 11, UtilityTariffId = 3, TierNumber = 2, From = 50m, To = 100m, UnitPrice = 1_866m },
        new() { Id = 12, UtilityTariffId = 3, TierNumber = 3, From = 100m, To = 200m, UnitPrice = 2_167m },
        new() { Id = 13, UtilityTariffId = 3, TierNumber = 4, From = 200m, To = 300m, UnitPrice = 2_729m },
        new() { Id = 14, UtilityTariffId = 3, TierNumber = 5, From = 300m, To = 400m, UnitPrice = 3_050m },
        new() { Id = 15, UtilityTariffId = 3, TierNumber = 6, From = 400m, To = null, UnitPrice = 3_151m },
        new() { Id = 16, UtilityTariffId = 4, TierNumber = 1, From = 0m, To = 50m, UnitPrice = 1_893m },
        new() { Id = 17, UtilityTariffId = 4, TierNumber = 2, From = 50m, To = 100m, UnitPrice = 1_956m },
        new() { Id = 18, UtilityTariffId = 4, TierNumber = 3, From = 100m, To = 200m, UnitPrice = 2_271m },
        new() { Id = 19, UtilityTariffId = 4, TierNumber = 4, From = 200m, To = 300m, UnitPrice = 2_860m },
        new() { Id = 20, UtilityTariffId = 4, TierNumber = 5, From = 300m, To = 400m, UnitPrice = 3_197m },
        new() { Id = 21, UtilityTariffId = 4, TierNumber = 6, From = 400m, To = null, UnitPrice = 3_302m },
        new() { Id = 1, UtilityTariffId = 1, TierNumber = 1, From = 0m, To = 50m, UnitPrice = 1_984m },
        new() { Id = 2, UtilityTariffId = 1, TierNumber = 2, From = 50m, To = 100m, UnitPrice = 2_050m },
        new() { Id = 3, UtilityTariffId = 1, TierNumber = 3, From = 100m, To = 200m, UnitPrice = 2_380m },
        new() { Id = 4, UtilityTariffId = 1, TierNumber = 4, From = 200m, To = 300m, UnitPrice = 2_998m },
        new() { Id = 5, UtilityTariffId = 1, TierNumber = 5, From = 300m, To = 400m, UnitPrice = 3_350m },
        new() { Id = 6, UtilityTariffId = 1, TierNumber = 6, From = 400m, To = null, UnitPrice = 3_460m },
        new() { Id = 7, UtilityTariffId = 2, TierNumber = 1, From = 0m, To = 4m, UnitPrice = 6_700m },
        new() { Id = 8, UtilityTariffId = 2, TierNumber = 2, From = 4m, To = 6m, UnitPrice = 12_900m },
        new() { Id = 9, UtilityTariffId = 2, TierNumber = 3, From = 6m, To = null, UnitPrice = 14_400m },
        new() { Id = 22, UtilityTariffId = 5, TierNumber = 1, From = 0m, To = 4m, UnitPrice = 6_700m },
        new() { Id = 23, UtilityTariffId = 5, TierNumber = 2, From = 4m, To = 6m, UnitPrice = 12_900m },
        new() { Id = 24, UtilityTariffId = 5, TierNumber = 3, From = 6m, To = null, UnitPrice = 14_400m },
    ];
}
