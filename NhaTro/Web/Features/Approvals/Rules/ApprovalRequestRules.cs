using System.Text.Json;
using Web.Features.Approvals.Enums;

namespace Web.Features.Approvals.Rules;

/// <summary>
/// Luật thuần của hàng đợi duyệt (VHT-96): kiểm dữ liệu đầu vào và điều kiện chuyển trạng thái.
/// Tách khỏi service để unit test không cần DB.
/// </summary>
public static class ApprovalRequestRules
{
    /// <summary>Mã SW tối đa 10 ký tự (SW-15a…e), khóa đối tượng tối đa 200.</summary>
    public const int MaxTargetKeyLength = 200;

    /// <summary>Dữ liệu đề xuất tối đa 100.000 ký tự để không nhét file vào cột.</summary>
    public const int MaxPayloadLength = 100_000;

    /// <summary>Trả mã lỗi khi dữ liệu tạo yêu cầu không hợp lệ, null khi hợp lệ.</summary>
    public static string? ValidateCreate(string? targetKey, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(targetKey) || targetKey.Trim().Length > MaxTargetKeyLength)
        {
            return ApprovalErrorCodes.InvalidRequest;
        }

        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson.Length > MaxPayloadLength)
        {
            return ApprovalErrorCodes.InvalidRequest;
        }

        try
        {
            using var _ = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return ApprovalErrorCodes.InvalidRequest;
        }

        return null;
    }

    /// <summary>Chuẩn hóa lý do từ chối; trả mã lỗi khi thiếu hoặc quá dài.</summary>
    public static string? ValidateRejectReason(string? reason, out string normalized)
    {
        normalized = (reason ?? string.Empty).Trim();

        if (normalized.Length == 0)
        {
            return ApprovalErrorCodes.RejectReasonRequired;
        }

        if (normalized.Length > DTOs.RejectApprovalRequest.MaxReasonLength)
        {
            return ApprovalErrorCodes.RejectReasonTooLong;
        }

        return null;
    }

    /// <summary>Chỉ yêu cầu đang Chờ duyệt mới được quyết định/hủy.</summary>
    public static bool CanDecide(ApprovalRequestStatus status) =>
        status == ApprovalRequestStatus.Pending;
}
