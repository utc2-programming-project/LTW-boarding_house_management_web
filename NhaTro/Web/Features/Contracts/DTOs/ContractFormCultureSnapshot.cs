namespace Web.Features.Contracts.DTOs;

/// <summary>Serializable, browser-session-only form state used when the culture selector reloads the page.</summary>
public sealed class ContractFormCultureSnapshot
{
    public int? PropertyId { get; set; }
    public int? RoomId { get; set; }
    public int? RepresentativeTenantId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? MonthlyRentVnd { get; set; }
    public decimal? DepositVnd { get; set; }
    public int? PaymentCycleMonths { get; set; }
    public int? MeterReadingDay { get; set; }
    public string? Notes { get; set; }
    public List<ContractMemberCultureSnapshot>? Members { get; set; }
}

public sealed class ContractMemberCultureSnapshot
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Relationship { get; set; }
}
