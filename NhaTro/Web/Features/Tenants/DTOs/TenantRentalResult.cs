// 2C (VHT-25) — kết quả truy vấn hồ sơ/phòng/hợp đồng đang thuê; chờ VHT-30 mới bật.
// Mở khối comment dưới và bật lại cùng khối 2C trong TenantService/ITenantService.
/*
namespace Web.Features.Tenants.DTOs;

/// <summary>Kết quả truy vấn hồ sơ/phòng/hợp đồng đang thuê theo liên kết thuê.</summary>
public sealed class TenantRentalResult
{
    private TenantRentalResult()
    {
    }

    public required bool Succeeded { get; init; }

    public TenantRentalOverviewDto? Rental { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static TenantRentalResult Success(TenantRentalOverviewDto rental) =>
        new() { Succeeded = true, Rental = rental };

    public static TenantRentalResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
*/
