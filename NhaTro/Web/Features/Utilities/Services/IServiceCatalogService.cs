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
}
