namespace Web.Features.Accounts.Owners;

public enum OwnerAccessDecision
{
    Allowed,
    PartiallyBlocked,
    AllBlocked,
}

public static class OwnerAccessRules
{
    public static OwnerAccessDecision Decide(
        IReadOnlyCollection<string> ownerIds,
        IReadOnlySet<string> blockedOwnerIds)
    {
        if (ownerIds.Count == 0 || blockedOwnerIds.Count == 0)
        {
            return OwnerAccessDecision.Allowed;
        }

        var blocked = ownerIds.Count(blockedOwnerIds.Contains);
        if (blocked == 0)
        {
            return OwnerAccessDecision.Allowed;
        }

        return blocked >= ownerIds.Count
            ? OwnerAccessDecision.AllBlocked
            : OwnerAccessDecision.PartiallyBlocked;
    }
}
