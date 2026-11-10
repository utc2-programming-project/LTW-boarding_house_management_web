using Web.Features.Rooms.DTOs;

namespace Web.Features.Properties.DTOs;

/// <summary>Bước của luồng thiết lập nhà trọ liền mạch.</summary>
public enum PropertySetupStep
{
    Basics = 1,
    Rooms = 2,
    Pricing = 3,
    Finished = 4
}

/// <summary>
/// Bản nháp lưu ở trình duyệt của người dùng (không ghi DB).
/// Chỉ giữ phần chưa được lưu thật: thông tin bước 1 và phòng đang nhập dở.
/// </summary>
public sealed class PropertySetupDraft
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Nhà trọ đã tạo ở bước 1; null khi chưa bấm "Lưu và thêm phòng".</summary>
    public int? PropertyId { get; set; }

    public PropertySetupStep Step { get; set; } = PropertySetupStep.Basics;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Phòng đang nhập dở trong dialog; null khi không có.</summary>
    public RoomSave? PendingRoom { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
