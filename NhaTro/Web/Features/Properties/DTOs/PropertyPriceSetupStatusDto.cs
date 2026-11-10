using Web.Features.Utilities.DTOs;

namespace Web.Features.Properties.DTOs;

/// <summary>
/// PLAN-012: tình trạng giá của một nhà trọ sau khi tạo, dùng cho bước thiết lập giá.
/// </summary>
public sealed record PropertyPriceSetupStatusDto(
    int PropertyId,
    bool HasElectricTariff,
    decimal? ElectricRate,
    ServiceUnitCategory? ElectricUnit,
    DateOnly? ElectricEffectiveFrom,
    bool HasWaterTariff,
    decimal? WaterRate,
    ServiceUnitCategory? WaterUnit,
    DateOnly? WaterEffectiveFrom,
    IReadOnlyList<ServiceItemDto> PropertyServices,
    IReadOnlyList<BuildingOptionDto> CopySources)
{
    public bool HasPropertyServices => PropertyServices.Count > 0;
    public bool IsComplete => HasElectricTariff && HasWaterTariff && HasPropertyServices;
}
