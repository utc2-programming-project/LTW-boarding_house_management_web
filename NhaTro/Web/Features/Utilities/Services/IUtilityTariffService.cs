using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Services;

/// <summary>Read-only access to the locked, date-versioned electricity and water tariff tables.</summary>
public interface IUtilityTariffService
{
    Task<UtilityTariffSnapshotDto> CalculateForPeriodAsync(
        TariffType type,
        string scopeCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal quantity,
        int residentCount = 1,
        CancellationToken cancellationToken = default);
}
