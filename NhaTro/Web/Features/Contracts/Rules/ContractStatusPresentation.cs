using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.Rules;

public enum ContractDisplayStatus
{
    Pending,
    Upcoming,
    Active,
    ExpiringSoon,
    Expired,
    Rejected,
    Liquidated,
    Draft,
    AwaitingTenant,
    ChangeRequested,
    TenantAccepted,
    Cancelled,
}

/// <summary>Derives date-based labels without adding persisted contract states.</summary>
public static class ContractStatusPresentation
{
    public const int ExpiringSoonDays = 30;

    public static ContractDisplayStatus GetDisplayStatus(
        ContractStatus status,
        DateTime startDate,
        DateTime endDate,
        DateOnly today)
    {
        if (status == ContractStatus.Cancelled) return ContractDisplayStatus.Cancelled;
        if (status == ContractStatus.Draft) return ContractDisplayStatus.Draft;
        if (status == ContractStatus.AwaitingTenant) return ContractDisplayStatus.AwaitingTenant;
        if (status == ContractStatus.ChangeRequested) return ContractDisplayStatus.ChangeRequested;
        if (status == ContractStatus.TenantAccepted) return ContractDisplayStatus.TenantAccepted;
        if (status == ContractStatus.Pending) return ContractDisplayStatus.Pending;
        if (status == ContractStatus.Rejected) return ContractDisplayStatus.Rejected;
        if (status == ContractStatus.Liquidated) return ContractDisplayStatus.Liquidated;

        var start = DateOnly.FromDateTime(startDate);
        var end = DateOnly.FromDateTime(endDate);
        if (start > today) return ContractDisplayStatus.Upcoming;
        if (end < today) return ContractDisplayStatus.Expired;
        if (end <= today.AddDays(ExpiringSoonDays)) return ContractDisplayStatus.ExpiringSoon;
        return ContractDisplayStatus.Active;
    }
}
