namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Hồ sơ khách + toàn bộ hợp đồng thuê theo liên kết thật.
/// Hồ sơ chưa thuê trả <see cref="Rentals"/> rỗng và <see cref="CurrentRental"/> null (kết quả rỗng rõ ràng).
/// </summary>
public sealed class TenantRentalOverviewDto
{
    public required int TenantId { get; init; }

    public required string FullName { get; init; }

    /// <summary>Toàn bộ hợp đồng của khách, đang thuê trước rồi tới lịch sử (mới nhất trước).</summary>
    public required IReadOnlyList<TenantRentalContractDto> Rentals { get; init; }

    /// <summary>Hợp đồng đang có hiệu lực; null khi khách chưa thuê.</summary>
    public TenantRentalContractDto? CurrentRental { get; init; }

    public bool HasActiveContract => CurrentRental is not null;
}
