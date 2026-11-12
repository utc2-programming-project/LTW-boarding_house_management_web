namespace Web.Features.Maintenance;

public static class IncidentErrorCodes
{
    public const string Forbidden = "Forbidden";
    public const string NotFound = "IncidentNotFound";
    public const string RoomNotFound = "RoomNotFound";
    public const string InvalidRequest = "InvalidRequest";
    public const string InvalidTransition = "TransitionInvalid";
    public const string RepairCostInvalid = "RepairCostInvalid";
    public const string Conflict = "Conflict";
    public const string ApprovalFailed = "ApprovalFailed";
    public const string NotInTrash = "IncidentNotInTrash";
    public const string HasPendingApproval = "IncidentHasPendingApproval";
    public const string HasDependencies = "IncidentHasDependencies";
    public const string RoomDeleted = "IncidentRoomDeleted";
}
