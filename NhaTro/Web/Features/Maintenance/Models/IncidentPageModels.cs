using Microsoft.AspNetCore.Components.Forms;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Rules;
using Web.Features.Storage.Rules;

namespace Web.Features.Maintenance.Models;

public enum IncidentMode { Allow, Approval, Deny }
public enum IncidentAction { Create, Update, ResultPhoto }
public sealed record IncidentHistory(DateTime AtUtc, string ActorKey, string EventKey);
public sealed record IncidentPending(IncidentAction Action, IncidentStatus? ProposedStatus, string Notes,
    IReadOnlyList<IncidentStoredPhoto> Photos, DateTime AtUtc, string Assignee = "", decimal? RepairCost = null, int ExpectedPhotoCount = 0)
{
    public Guid Id { get; init; }
}
public sealed record IncidentStoredPhoto(Guid Id, string Name, bool IsResult, bool Pending)
{
    public string Url => $"/api/files/{Id:D}";
}
public sealed record IncidentItem(int Id, int RoomId, string Title, string Description,
    IncidentCategory Category, IncidentUrgency Urgency, IncidentStatus Status, DateTime CreatedAtUtc,
    string TechnicalNotes, IReadOnlyList<IncidentStoredPhoto> Photos, IReadOnlyList<IncidentHistory> History,
    IReadOnlyList<IncidentPending> Pending, string Assignee = "", decimal? RepairCost = null, string RoomLabel = "");

public sealed record IncidentSelectedPhoto(string Name, byte[] Content)
{
    public const int MaxCount = IncidentPhotoRules.MaxCount;
    public const long MaxBytes = IncidentPhotoRules.MaxBytes;
    public string DataUrl => $"data:{FileContentSniffer.DetectContentType(Content)};base64,{Convert.ToBase64String(Content)}";
    public static async Task<IncidentSelectedPhoto> ReadAsync(IBrowserFile file, CancellationToken ct)
    {
        if (file.Size is <= 0 or > MaxBytes || !IncidentPhotoRules.AllowedExtension(Path.GetExtension(file.Name).ToLowerInvariant()))
            throw new InvalidDataException("PhotoInvalid");
        await using var stream = file.OpenReadStream(MaxBytes, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var content = buffer.ToArray();
        if (!FileContentSniffer.IsConsistentWithExtension(Path.GetExtension(file.Name).ToLowerInvariant(), FileContentSniffer.DetectContentType(content)))
            throw new InvalidDataException("PhotoInvalid");
        return new(Path.GetFileName(file.Name), content);
    }
}
public sealed class IncidentForm
{
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IncidentCategory Category { get; set; }
    public IncidentUrgency Urgency { get; set; }
    public IReadOnlyList<IncidentSelectedPhoto> Photos { get; set; } = [];
    public Dictionary<string, string> PhotoErrors { get; } = [];
    public int? SavedIncidentId { get; set; }
    public Guid? SavedApprovalId { get; set; }
    public bool Saved => SavedIncidentId is not null || SavedApprovalId is not null;
}
public sealed class IncidentPageException(string key) : Exception(key) { }
