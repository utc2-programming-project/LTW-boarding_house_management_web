using Microsoft.AspNetCore.Components.Forms;
using Web.Features.Storage.Rules;

namespace Web.Features.Maintenance.Models;

// Model chỉ phục vụ UI mẫu 2A, không phải hợp đồng DTO hoặc entity của VHT-33.
public enum IncidentDemoStatus { New, InProgress, Completed }
public enum IncidentDemoCategory { Electrical, Water, Furniture, Equipment, Other }
public enum IncidentDemoUrgency { Normal, Urgent, Emergency }
public enum IncidentDemoMode { Allow, Approval, Deny }
public enum IncidentDemoAction { Create, Update, ResultPhoto }

public sealed record IncidentDemoRoom(int Id, int PropertyId, string PropertyName, string Name);
public sealed record IncidentDemoHistory(DateTime AtUtc, string ActorKey, string EventKey);
public sealed record IncidentDemoPending(IncidentDemoAction Action, IncidentDemoStatus? ProposedStatus,
    string Notes, IReadOnlyList<IncidentDemoPhoto> Photos, DateTime AtUtc);

public sealed record IncidentDemoPhoto(string Name, byte[] Content, bool IsResult = false, string UploadedBy = "")
{
    // Giới hạn riêng cho mỗi nhóm: ảnh báo hoặc ảnh kết quả, gồm ảnh chờ duyệt của nhóm đó.
    public const int MaxCount = 3;
    public const long MaxBytes = 5L * 1024 * 1024;
    public string? ContentType => FileContentSniffer.DetectContentType(Content);
    public string DataUrl => $"data:{ContentType};base64,{Convert.ToBase64String(Content)}";

    public bool IsValid => Content.LongLength is > 0 and <= MaxBytes
        && (Path.GetExtension(Name).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png")
        && FileContentSniffer.IsConsistentWithExtension(Path.GetExtension(Name).ToLowerInvariant(), ContentType);

    // Đọc stream có chặn dung lượng, không tin extension hoặc Content-Type từ trình duyệt.
    public static async Task<IncidentDemoPhoto> ReadAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        if (file.Size is <= 0 or > MaxBytes
            || Path.GetExtension(file.Name).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
            throw new InvalidDataException("PhotoInvalid");

        await using var stream = file.OpenReadStream(MaxBytes, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var photo = new IncidentDemoPhoto(Path.GetFileName(file.Name), buffer.ToArray());
        if (!photo.IsValid) throw new InvalidDataException("PhotoInvalid");
        return photo;
    }
}

public sealed record IncidentDemoItem(int Id, int RoomId, string Title, string Description,
    IncidentDemoCategory Category, IncidentDemoUrgency Urgency, IncidentDemoStatus Status,
    DateTime CreatedAtUtc, string TechnicalNotes, IReadOnlyList<IncidentDemoPhoto> Photos,
    IReadOnlyList<IncidentDemoHistory> History, IReadOnlyList<IncidentDemoPending> Pending,
    bool AwaitingCreation = false, bool IsSeeded = false);

public sealed class IncidentDemoForm
{
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IncidentDemoCategory Category { get; set; }
    public IncidentDemoUrgency Urgency { get; set; }
    public IReadOnlyList<IncidentDemoPhoto> Photos { get; set; } = [];
}

public sealed class IncidentDemoException(string resourceKey) : Exception(resourceKey) { }
