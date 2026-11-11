namespace Web.Features.Accounts.Owners;

public sealed record OwnerListItemDto(
    string Id,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    bool IsLocked,
    int PropertyCount,
    int ManagerCount,
    int TenantCount,
    DateTime? ApprovedAt);

public sealed record OwnerImpactDto(
    string OwnerId,
    int PropertyCount,
    int ManagerCount,
    int TenantCount);

public sealed record OwnerAdminResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static OwnerAdminResult Success() => new(true);

    public static OwnerAdminResult Failure(string code, string message) =>
        new(false, code, message);
}
