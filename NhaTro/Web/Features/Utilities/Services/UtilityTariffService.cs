using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Rules;

namespace Web.Features.Utilities.Services;

public sealed class UtilityTariffService(ApplicationDbContext dbContext) : IUtilityTariffService
{
    public async Task<UtilityTariffSnapshotDto> CalculateForPeriodAsync(
        TariffType type,
        string scopeCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal quantity,
        int residentCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scopeCode))
        {
            throw new ArgumentException("A tariff scope is required.", nameof(scopeCode));
        }

        UtilityTariffRules.EnsurePeriodUsesSingleTariff(periodStart, periodEnd, nextEffectiveDate: null);

        if (quantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Consumption cannot be negative.");
        }

        var tariff = await UtilityTariffRules.QueryEffective(
                dbContext.UtilityTariffs
                    .AsNoTracking()
                    .Include(item => item.Tiers.OrderBy(tier => tier.TierNumber)),
                type,
                scopeCode,
                periodStart)
            .FirstOrDefaultAsync(cancellationToken);

        if (tariff is null)
        {
            throw new InvalidOperationException("No tariff is configured for this scope and billing period.");
        }

        if (!tariff.IsReadOnly)
        {
            throw new InvalidOperationException("Configured utility tariffs must be read-only.");
        }

        if (tariff.IsPerResident && residentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(residentCount), "Resident count must be greater than zero.");
        }

        var nextEffectiveDate = await dbContext.UtilityTariffs
            .AsNoTracking()
            .Where(item => item.Type == type
                && item.ScopeCode == scopeCode
                && item.EffectiveDate > periodStart
                && item.EffectiveDate <= periodEnd)
            .Select(item => (DateOnly?)item.EffectiveDate)
            .MinAsync(cancellationToken);

        UtilityTariffRules.EnsurePeriodUsesSingleTariff(periodStart, periodEnd, nextEffectiveDate);

        var applied = UtilityTariffRules.Calculate(
            quantity,
            tariff.Tiers.ToArray(),
            tariff.IsPerResident ? residentCount : 1);
        return new UtilityTariffSnapshotDto(
            tariff.Id,
            tariff.Type,
            tariff.ScopeCode,
            tariff.Unit,
            tariff.EffectiveDate,
            periodStart,
            periodEnd,
            tariff.SourceName,
            tariff.SourceReference,
            quantity,
            tariff.IsPerResident ? residentCount : 1,
            applied.Sum(line => line.Amount),
            applied);
    }
}
