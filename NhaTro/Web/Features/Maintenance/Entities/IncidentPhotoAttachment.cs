using Web.Features.Approvals.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Storage.Entities;

namespace Web.Features.Maintenance.Entities;

// Metadata riêng cho P7; nội dung ảnh vẫn nằm trong private storage.
public sealed class IncidentPhotoAttachment
{
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = default!;
    public IncidentPhotoKind Kind { get; set; }
    public int? IncidentId { get; set; }
    public MaintenanceIncident? Incident { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public ManagerApprovalRequest? ApprovalRequest { get; set; }
}
