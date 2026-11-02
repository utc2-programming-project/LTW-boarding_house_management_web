using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.Rules;

public static class IncidentRules
{
    public const int MinTitleLength = 5;
    public const int MinDescriptionLength = 10;

    public static string? ValidateCreate(IncidentCreateRequest? request)
    {
        if (request is null
            || request.RoomId <= 0
            || string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Trim().Length < MinTitleLength
            || request.Title.Trim().Length > 200
            || string.IsNullOrWhiteSpace(request.Description)
            || request.Description.Trim().Length < MinDescriptionLength
            || request.Description.Trim().Length > 2000
            || !Enum.IsDefined(request.Category)
            || !Enum.IsDefined(request.Urgency))
        {
            return IncidentErrorCodes.InvalidRequest;
        }

        return null;
    }

    public static string? ValidateUpdate(IncidentUpdateRequest? request)
    {
        if (request is null || !Enum.IsDefined(request.Status))
        {
            return IncidentErrorCodes.InvalidRequest;
        }

        if (request.AssigneeName?.Trim().Length > 100
            || request.TechnicalNotes?.Trim().Length > 2000)
        {
            return IncidentErrorCodes.InvalidRequest;
        }

        if (request.RepairCost is < 0
            || (request.RepairCost is { } amount && decimal.Truncate(amount) != amount))
        {
            return IncidentErrorCodes.RepairCostInvalid;
        }

        return null;
    }

    public static bool CanTransition(IncidentStatus current, IncidentStatus next) =>
        Enum.IsDefined(current)
        && Enum.IsDefined(next)
        && current != IncidentStatus.Completed
        && (int)next == (int)current + 1;
}
