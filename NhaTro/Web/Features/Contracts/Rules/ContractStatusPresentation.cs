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
        if (status == ContractStatus.Pending) return ContractDisplayStatus.Pending;
        if (status == ContractStatus.Rejected) return ContractDisplayStatus.Rejected;

        var start = DateOnly.FromDateTime(startDate);
        var end = DateOnly.FromDateTime(endDate);
        if (start > today) return ContractDisplayStatus.Upcoming;
        if (end < today) return ContractDisplayStatus.Expired;
        if (end <= today.AddDays(ExpiringSoonDays)) return ContractDisplayStatus.ExpiringSoon;
        return ContractDisplayStatus.Active;
    }
}
