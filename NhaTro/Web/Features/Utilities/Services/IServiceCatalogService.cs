using Web.Features.Properties;
using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Services;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceCatalogDto>> GetServiceCatalogsAsync(PropertyCaller caller);

    Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        CreateServiceCatalogDto request,
        PropertyCaller caller);

    Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        int serviceCatalogId,
        UpdateServicePriceDto request,
        PropertyCaller caller);

    Task<ServiceCatalogDto> ToggleServiceCatalogStatusAsync(
        int serviceCatalogId,
        PropertyCaller caller);

    Task DeleteServiceCatalogAsync(
        int serviceCatalogId,
        PropertyCaller caller);

    /// <summary>
    /// PLAN-012: sao chép danh mục dịch vụ và giá đang hiệu lực từ nhà trọ nguồn sang nhà trọ đích.
    /// Dịch vụ trùng tên ở đích bị bỏ qua; không mang theo lịch sử giá.
    /// </summary>
    Task<ServicePriceCopyResult> CopyPricesFromPropertyAsync(
        int sourcePropertyId,
        int targetPropertyId,
        PropertyCaller caller,
        CancellationToken ct = default);
}
