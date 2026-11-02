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
}
