namespace Web.Features.Contracts.DTOs;

/// <summary>Form model for the in-memory contract preview. It is not persisted or sent to an API.</summary>
public sealed class RentalContractDraft
{
    public int? PropertyId { get; set; }
    public int? RoomId { get; set; }
    public int? RepresentativeTenantId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? MonthlyRentVnd { get; set; }
    public decimal? DepositVnd { get; set; }
    public int? PaymentCycleMonths { get; set; } = 1;
    public int? MeterReadingDay { get; set; } = 25;
    public string? Notes { get; set; }
    public List<ContractMemberDraft> Members { get; set; } = [];

    public RentalContractDraft Copy() => new()
    {
        PropertyId = PropertyId,
        RoomId = RoomId,
        RepresentativeTenantId = RepresentativeTenantId,
        StartDate = StartDate,
        EndDate = EndDate,
        MonthlyRentVnd = MonthlyRentVnd,
        DepositVnd = DepositVnd,
        PaymentCycleMonths = PaymentCycleMonths,
        MeterReadingDay = MeterReadingDay,
        Notes = Notes,
        Members = Members.Select(member => new ContractMemberDraft
        {
            FullName = member.FullName,
            PhoneNumber = member.PhoneNumber,
            Relationship = member.Relationship,
        }).ToList(),
    };
}

public sealed class ContractMemberDraft
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Relationship { get; set; } = "Other";
}
