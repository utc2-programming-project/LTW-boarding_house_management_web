namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Một hồ sơ đang nằm trong thùng rác (VHT-99). Không trả UserId thô; chỉ cho biết
/// hồ sơ còn liên kết tài khoản hay không và còn bao nhiêu tệp giấy tờ được giữ lại.
/// </summary>
public sealed class TenantTrashItemDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    /// <summary>Chỉ 4 ký tự cuối, giống danh sách hồ sơ đang hoạt động.</summary>
    public string? IdNumberMasked { get; init; }

    /// <summary>Trạng thái hoạt động trước khi bị đưa vào thùng rác.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Còn liên kết tài khoản đăng nhập hay không (được giữ khi xóa mềm).</summary>
    public required bool HasAccount { get; init; }

    /// <summary>Số tệp giấy tờ chưa xóa gắn với hồ sơ (tệp vẫn được giữ theo hạn lưu).</summary>
    public required int DocumentCount { get; init; }

    /// <summary>UTC — thời điểm đưa vào thùng rác.</summary>
    public DateTimeOffset? DeletedAt { get; init; }

    /// <summary>Base64 rowversion, gửi lại khi khôi phục để tránh ghi đè.</summary>
    public required string RowVersion { get; init; }
}
