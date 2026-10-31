namespace Web.Features.Tenants.DTOs;

public sealed class TenantRoommateDto
{
    public required int TenantId { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }
}
