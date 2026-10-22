using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.MockData;

namespace Web.Features.Contracts.Rules;

public enum ContractDraftError
{
    PropertyRequired,
    RoomRequired,
    RoomUnavailable,
    RepresentativeRequired,
    StartDateRequired,
    EndDateRequired,
    EndDateMustBeAtLeastOneMonthAfterStart,
    RentRequired,
    RentMustBePositive,
    DepositRequired,
    DepositCannotBeNegative,
    PaymentCycleRequired,
    PaymentCycleInvalid,
}

public sealed record ContractDraftValidationIssue(string FieldName, ContractDraftError Error);

/// <summary>Pure validation rules shared by the form and unit tests; no storage or identity dependency.</summary>
public static class ContractDraftRules
{
    public static IReadOnlyList<ContractDraftValidationIssue> Validate(RentalContractDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var issues = new List<ContractDraftValidationIssue>();
        var propertyExists = draft.PropertyId is int propertyId &&
            ContractFormSampleData.Properties.Any(property => property.Id == propertyId);

        if (!propertyExists)
        {
            issues.Add(new(nameof(draft.PropertyId), ContractDraftError.PropertyRequired));
        }

        if (draft.RoomId is not int roomId)
        {
            issues.Add(new(nameof(draft.RoomId), ContractDraftError.RoomRequired));
        }
        else if (!propertyExists || !ContractFormSampleData.Rooms.Any(room =>
                     room.Id == roomId && room.PropertyId == draft.PropertyId && room.IsAvailableForLeaseStart(draft.StartDate)))
        {
            issues.Add(new(nameof(draft.RoomId), ContractDraftError.RoomUnavailable));
        }

        if (draft.RepresentativeTenantId is not int tenantId ||
            !ContractFormSampleData.Tenants.Any(tenant => tenant.Id == tenantId))
        {
            issues.Add(new(nameof(draft.RepresentativeTenantId), ContractDraftError.RepresentativeRequired));
        }

        if (draft.StartDate is null)
        {
            issues.Add(new(nameof(draft.StartDate), ContractDraftError.StartDateRequired));
        }

        if (draft.EndDate is null)
        {
            issues.Add(new(nameof(draft.EndDate), ContractDraftError.EndDateRequired));
        }
        else if (draft.StartDate is DateOnly startDate)
        {
            try
            {
                if (draft.EndDate.Value < startDate.AddMonths(1))
                {
                    issues.Add(new(nameof(draft.EndDate), ContractDraftError.EndDateMustBeAtLeastOneMonthAfterStart));
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                issues.Add(new(nameof(draft.EndDate), ContractDraftError.EndDateMustBeAtLeastOneMonthAfterStart));
            }
        }

        if (draft.MonthlyRentVnd is null)
        {
            issues.Add(new(nameof(draft.MonthlyRentVnd), ContractDraftError.RentRequired));
        }
        else if (draft.MonthlyRentVnd <= 0)
        {
            issues.Add(new(nameof(draft.MonthlyRentVnd), ContractDraftError.RentMustBePositive));
        }

        if (draft.DepositVnd is null)
        {
            issues.Add(new(nameof(draft.DepositVnd), ContractDraftError.DepositRequired));
        }
        else if (draft.DepositVnd < 0)
        {
            issues.Add(new(nameof(draft.DepositVnd), ContractDraftError.DepositCannotBeNegative));
        }

        if (draft.PaymentCycleMonths is null)
        {
            issues.Add(new(nameof(draft.PaymentCycleMonths), ContractDraftError.PaymentCycleRequired));
        }
        else if (draft.PaymentCycleMonths is not (1 or 3 or 6))
        {
            issues.Add(new(nameof(draft.PaymentCycleMonths), ContractDraftError.PaymentCycleInvalid));
        }

        return issues;
    }

    /// <summary>Initial payment is the deposit plus one complete selected billing cycle, always in VND.</summary>
    public static decimal CalculateInitialPayment(decimal? monthlyRentVnd, decimal? depositVnd, int? paymentCycleMonths)
    {
        if (monthlyRentVnd is null || depositVnd is null || paymentCycleMonths is null)
        {
            return 0m;
        }

        return checked(depositVnd.Value + (monthlyRentVnd.Value * paymentCycleMonths.Value));
    }
}
