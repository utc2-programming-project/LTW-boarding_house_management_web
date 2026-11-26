namespace Web.Features.Accounts.Avatar;

/// <summary>
/// PLAN-030 bổ sung: ảnh đại diện lưu ở server để đồng bộ giữa các thiết bị.
/// Một dòng mỗi tài khoản, khoá chính cũng là khoá ngoại tới AspNetUsers nên xoá tài khoản là ảnh mất theo.
/// </summary>
public class UserAvatar
{
    public string UserId { get; set; } = string.Empty;

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public string ContentType { get; set; } = "image/jpeg";

    public DateTimeOffset UpdatedAt { get; set; }
}
