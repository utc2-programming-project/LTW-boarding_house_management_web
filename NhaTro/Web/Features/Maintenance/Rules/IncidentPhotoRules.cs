using Web.Features.Approvals.Enums;

namespace Web.Features.Maintenance.Rules;

public static class IncidentPhotoRules
{
    public const int MaxCount = 3;
    public const long MaxBytes = 5L * 1024 * 1024;
    public static bool IsEffective(ApprovalRequestStatus? status) => status is null or ApprovalRequestStatus.Pending or ApprovalRequestStatus.Approved;
    public static bool IsPublished(ApprovalRequestStatus? status) => status is null or ApprovalRequestStatus.Approved;
    public static bool AllowedExtension(string extension) => extension is ".jpg" or ".jpeg" or ".png";
}
