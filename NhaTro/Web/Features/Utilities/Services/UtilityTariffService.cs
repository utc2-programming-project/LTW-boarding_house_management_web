using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Rules;
using Web.Data.Tariffs;

namespace Web.Features.Utilities.Services;

public sealed class UtilityTariffService(ApplicationDbContext dbContext) : IUtilityTariffService
{
    public async Task<TariffDto> CalculateForPeriodAsync(
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

        UtilityTariffRules.EnsureValidPeriod(periodStart, periodEnd);

        if (quantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Consumption cannot be negative.");
        }

        var effectiveDates = await dbContext.UtilityTariffs
            .AsNoTracking()
            .Where(item => item.Type == type
                && item.ScopeCode == scopeCode
                && item.EffectiveDate > periodStart
                && item.EffectiveDate <= periodEnd)
            .Select(item => item.EffectiveDate)
            .OrderBy(date => date)
            .ToListAsync(cancellationToken);

        var segments = UtilityTariffRules.SplitByEffectiveDates(periodStart, periodEnd, effectiveDates);
        var totalDays = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var tiers = new List<TariffTierDto>();
        var appliedVersions = new List<UtilityTariff>();
        decimal total = 0m;
        foreach (var segment in segments)
        {
            var tariff = await UtilityTariffRules.QueryEffective(
                    dbContext.UtilityTariffs
                        .AsNoTracking()
                        .Include(item => item.Tiers.OrderBy(tier => tier.TierNumber)),
                    type,
                    scopeCode,
                    segment.Start)
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

            var segmentDays = segment.End.DayNumber - segment.Start.DayNumber + 1;
            var segmentQuantity = UtilityTariffRules.AllocateQuantityByDays(quantity, segmentDays, totalDays);
            var segmentTierLimitMultiplier = (tariff.IsPerResident ? residentCount : 1)
                * segmentDays / (decimal)totalDays;
            var applied = UtilityTariffRules.Calculate(
                segmentQuantity,
                tariff.Tiers.ToArray(),
                segmentTierLimitMultiplier)
                .ToDictionary(line => line.TierNumber);

            appliedVersions.Add(tariff);
            foreach (var tariffTier in tariff.Tiers.OrderBy(item => item.TierNumber))
            {
                applied.TryGetValue(tariffTier.TierNumber, out var appliedLine);
                tiers.Add(new TariffTierDto(
                    tariffTier.From,
                    tariffTier.To,
                    tariffTier.UnitPrice,
                    tariffTier.TierNumber,
                    appliedLine?.Quantity ?? 0m,
                    appliedLine?.Amount ?? 0m,
                    tariff.Id,
                    tariff.EffectiveDate));
                total += appliedLine?.Amount ?? 0m;
            }
        }

        var primaryTariff = appliedVersions[0];
        return new TariffDto(
            primaryTariff.Type,
            primaryTariff.EffectiveDate,
            tiers,
            primaryTariff.Id,
            primaryTariff.ScopeCode,
            primaryTariff.Unit,
            periodStart,
            periodEnd,
            primaryTariff.SourceName,
            primaryTariff.SourceReference,
            quantity,
            primaryTariff.IsPerResident ? residentCount : 1,
            total,
            TariffVersionIds: appliedVersions.Select(item => item.Id).Distinct().ToArray());
    }

    public async Task<TariffDto> SnapshotForPeriodAsync(
        int billingPeriodId,
        TariffType type,
        string scopeCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal quantity,
        int residentCount = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(billingPeriodId);

        var existing = await dbContext.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.CostLines.OrderBy(line => line.TierNumber))
            .SingleOrDefaultAsync(snapshot => snapshot.Type == type
                && snapshot.BillingPeriodId == billingPeriodId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ScopeCode != scopeCode
                || existing.PeriodStart != periodStart
                || existing.PeriodEnd != periodEnd)
            {
                throw new InvalidOperationException("The billing period already has a snapshot with different tariff inputs.");
            }

            return ToDto(existing);
        }

        var calculated = await CalculateForPeriodAsync(
            type, scopeCode, periodStart, periodEnd, quantity, residentCount, cancellationToken);

        var snapshot = new UtilityBillingPeriodSnapshot
        {
            Type = calculated.Type,
            ScopeCode = calculated.ScopeCode,
            BillingPeriodId = billingPeriodId,
            Unit = calculated.Unit,
            EffectiveDate = calculated.EffectiveFrom,
            PeriodStart = calculated.PeriodStart,
            PeriodEnd = calculated.PeriodEnd,
            Quantity = calculated.Quantity,
            ResidentCount = calculated.ResidentCount,
            Total = calculated.Total,
            SourceName = calculated.SourceName,
            SourceReference = calculated.SourceReference,
            CreatedAt = DateTime.UtcNow,
            CostLines = calculated.Tiers.Select(tier => new UtilityBillingCostLineSnapshot
            {
                TierNumber = tier.TierNumber,
                TariffVersionId = tier.TariffVersionId,
                EffectiveDate = tier.EffectiveFrom ?? calculated.EffectiveFrom,
                From = tier.From,
                To = tier.To,
                Quantity = tier.Quantity,
                UnitPrice = tier.Price,
                Amount = tier.Amount,
            }).ToList(),
        };

        dbContext.UtilityBillingPeriodSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(snapshot);
    }

    public async Task<TariffDto?> GetPeriodSnapshotAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .Include(item => item.CostLines.OrderBy(line => line.TierNumber))
            .SingleOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);
        return snapshot is null ? null : ToDto(snapshot);
    }

    private static TariffDto ToDto(UtilityBillingPeriodSnapshot snapshot) => new(
        snapshot.Type,
        snapshot.EffectiveDate,
        snapshot.CostLines.Select(line => new TariffTierDto(
            line.From, line.To, line.UnitPrice, line.TierNumber, line.Quantity, line.Amount,
            line.TariffVersionId, line.EffectiveDate)).ToArray(),
        snapshot.CostLines.FirstOrDefault()?.TariffVersionId ?? 0,
        snapshot.ScopeCode,
        snapshot.Unit,
        snapshot.PeriodStart,
        snapshot.PeriodEnd,
        snapshot.SourceName,
        snapshot.SourceReference,
        snapshot.Quantity,
        snapshot.ResidentCount,
        snapshot.Total,
        snapshot.Id,
        snapshot.CostLines.Select(line => line.TariffVersionId).Distinct().ToArray());
}
