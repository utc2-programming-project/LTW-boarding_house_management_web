namespace Web.Features.Utilities.Entities;

/// <summary>A versioned, system-managed tariff schedule. Application services expose read operations only.</summary>
public sealed class UtilityTariff
{
    public int Id { get; set; }

    public TariffType Type { get; set; }

    /// <summary>National scope for electricity or an explicitly identified water provider/area.</summary>
    public string ScopeCode { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    /// <summary>Whether consumption tier limits are multiplied by the number of residents.</summary>
    public bool IsPerResident { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public bool IsReadOnly { get; set; } = true;

    public string SourceName { get; set; } = string.Empty;

    public string SourceReference { get; set; } = string.Empty;

    public ICollection<UtilityTariffTier> Tiers { get; set; } = new List<UtilityTariffTier>();
}
