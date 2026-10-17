namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Kết quả thao tác tạo / sửa / ngừng sử dụng khách thuê.
/// Cùng khuôn Succeeded / ErrorCode / ErrorMessage như UploadResult (hợp đồng storage).
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.9.
/// </summary>
public class TenantResult
{
    // Chi tao qua cac factory de ket qua luon nhat quan.
    private TenantResult()
    {
    }

    public required bool Succeeded { get; init; }

    /// <summary>null khi thất bại.</summary>
    public TenantDetailDto? Tenant { get; init; }

    /// <summary>Mã lỗi ở mục 9, null khi thành công.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>null khi thành công.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Có khi nghi trùng hoặc trùng cứng; còn lại null.</summary>
    public List<TenantListItemDto>? PossibleDuplicates { get; init; }

    public static TenantResult Success(TenantDetailDto tenant) =>
        new() { Succeeded = true, Tenant = tenant };

    public static TenantResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static TenantResult Failure(
        string errorCode,
        string errorMessage,
        List<TenantListItemDto> possibleDuplicates) =>
        new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            PossibleDuplicates = possibleDuplicates
        };
}
