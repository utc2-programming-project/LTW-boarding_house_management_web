using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Services;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceCatalogDto>> GetServiceCatalogsAsync(string userRole);

    Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        CreateServiceCatalogDto request,
        int userId,
        string userRole);

    Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        int serviceCatalogId,
        UpdateServicePriceDto request,
        int userId,
        string userRole);
}
