using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.DTOs;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Nạp các dịch vụ áp cho một hợp đồng kèm giá hiệu lực tại một mốc ngày.
/// Chỉ đọc: không tự đăng ký dịch vụ khi tạo hợp đồng.
/// </summary>
internal static class ContractServiceLineLoader
{
    public static async Task<IReadOnlyList<ContractServiceLineDto>> LoadAsync(
        ApplicationDbContext db,
        int propertyId,
        Guid contractId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var catalogIds = await db.ServiceCatalogs.AsNoTracking()
            .Where(catalog => catalog.IsActive
                && (catalog.PropertyId == null || catalog.PropertyId == propertyId))
            .Select(catalog => catalog.Id)
            .ToListAsync(cancellationToken);

        if (catalogIds.Count == 0)
        {
            return [];
        }

        var prices = await db.ServicePriceHistories.AsNoTracking()
            .Where(price => catalogIds.Contains(price.ServiceCatalogId) && price.EffectiveDate <= asOf)
            .OrderByDescending(price => price.EffectiveDate)
            .ThenByDescending(price => price.Id)
            .Select(price => new
            {
                price.ServiceCatalogId,
                price.Name,
                price.Unit,
                price.CalculationMethod,
                price.UnitPrice,
                price.EffectiveTo,
            })
            .ToListAsync(cancellationToken);

        var registeredIds = await db.ContractServiceRegistrations.AsNoTracking()
            .Where(registration => registration.ContractId == contractId)
            .Select(registration => registration.ServiceCatalogId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var registered = registeredIds.ToHashSet();
        // Chưa có dòng đăng ký nào nghĩa là áp toàn bộ dịch vụ của nhà (hành vi P5 hiện tại).
        var appliesAll = registered.Count == 0;

        return prices
            .GroupBy(price => price.ServiceCatalogId)
            .Select(group => group.First())
            .Where(price => price.EffectiveTo is null || price.EffectiveTo >= asOf)
            .OrderBy(price => price.Name)
            .Select(price => new ContractServiceLineDto(
                price.ServiceCatalogId,
                price.Name,
                price.Unit,
                price.CalculationMethod,
                price.UnitPrice,
                appliesAll || registered.Contains(price.ServiceCatalogId)))
            .ToList();
    }
}
