using Web.Features.Approvals.DTOs;

namespace Web.Features.Maintenance.DTOs;

public sealed class IncidentResult
{
    private IncidentResult() { }

    public required bool Succeeded { get; init; }
    public IncidentDetailDto? Incident { get; init; }
    public ApprovalRequestDto? ApprovalRequest { get; init; }
    public string? ErrorCode { get; init; }

    public static IncidentResult Success(IncidentDetailDto incident) =>
        new() { Succeeded = true, Incident = incident };

    public static IncidentResult Success() =>
        new() { Succeeded = true };

    public static IncidentResult ApprovalCreated(ApprovalRequestDto request) =>
        new() { Succeeded = true, ApprovalRequest = request };

    public static IncidentResult Failure(string errorCode) =>
        new() { Succeeded = false, ErrorCode = errorCode };
}
