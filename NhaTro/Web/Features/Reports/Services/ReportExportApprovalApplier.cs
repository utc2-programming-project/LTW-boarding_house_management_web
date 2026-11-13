using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Reports.DTOs;

namespace Web.Features.Reports.Services;

/// <summary>
/// SW-16 — duyệt yêu cầu xuất báo cáo của Quản lý. Tệp Excel được sinh lại khi Quản lý
/// thực hiện lệnh xuất sau khi yêu cầu đã được duyệt; applier chỉ xác thực phạm vi yêu cầu.
/// </summary>
public sealed class ReportExportApprovalApplier : IApprovalApplier
{
    public string OperationCode => "SW-16";

    public Task ApplyAsync(
        ManagerApprovalRequest request,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Report
            || string.IsNullOrWhiteSpace(request.TargetKey)
            || !IsValidTargetKey(request.TargetKey))
        {
            throw new ApprovalApplyException(
                ReportExportErrorCodes.Forbidden,
                "Yêu cầu xuất báo cáo không hợp lệ.");
        }

        return Task.CompletedTask;
    }

    private static bool IsValidTargetKey(string targetKey)
    {
        var parts = targetKey.Split(':', StringSplitOptions.None);
        return parts.Length == 6
            && parts[0] == "export"
            && Enum.TryParse<ReportExportSection>(parts[1], true, out var section)
            && Enum.IsDefined(section);
    }
}
