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
            ScopeCode = "HCMC-SAWACO",
            Unit = "m³",
            IsPerResident = true,
            EffectiveDate = new DateOnly(2026, 1, 1),
            IsReadOnly = true,
            SourceName = "SAWACO customer-care portal; 2026 continuation notice",
            SourceReference = "https://cskh.sawaco.com.vn/News/Content?branchCode=KtSzxj0rfxc%3D&contentId=Qu20CKHdW0Q%3D ; https://cskh.sawaco.com.vn/ho_tro/hoi_dap?branchCodeIframe=TCT (residential tariff; VAT and wastewater fee excluded)",
        },
    ];

    public static readonly UtilityTariffTier[] Tiers =
    [
        new() { Id = 1, UtilityTariffId = 1, TierNumber = 1, From = 0m, To = 50m, UnitPrice = 1_984m },
        new() { Id = 2, UtilityTariffId = 1, TierNumber = 2, From = 50m, To = 100m, UnitPrice = 2_050m },
        new() { Id = 3, UtilityTariffId = 1, TierNumber = 3, From = 100m, To = 200m, UnitPrice = 2_380m },
        new() { Id = 4, UtilityTariffId = 1, TierNumber = 4, From = 200m, To = 300m, UnitPrice = 2_998m },
        new() { Id = 5, UtilityTariffId = 1, TierNumber = 5, From = 300m, To = 400m, UnitPrice = 3_350m },
        new() { Id = 6, UtilityTariffId = 1, TierNumber = 6, From = 400m, To = null, UnitPrice = 3_460m },
        new() { Id = 7, UtilityTariffId = 2, TierNumber = 1, From = 0m, To = 4m, UnitPrice = 6_700m },
        new() { Id = 8, UtilityTariffId = 2, TierNumber = 2, From = 4m, To = 6m, UnitPrice = 12_900m },
        new() { Id = 9, UtilityTariffId = 2, TierNumber = 3, From = 6m, To = null, UnitPrice = 14_400m },
    ];
}
