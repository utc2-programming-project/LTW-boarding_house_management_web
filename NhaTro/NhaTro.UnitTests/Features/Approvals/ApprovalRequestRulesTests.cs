using Web.Features.Approvals;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Rules;
using Xunit;

namespace NhaTro.UnitTests.Features.Approvals;

/// <summary>
/// VHT-96 — luật thuần của hàng đợi duyệt: kiểm payload/khóa đối tượng, lý do từ chối
/// và điều kiện chuyển trạng thái. Không cần DB.
/// </summary>
public sealed class ApprovalRequestRulesTests
{
    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("{\"tenantId\":7,\"fullName\":\"An\"}", true)]
    [InlineData("khong-phai-json", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Payload_phai_la_json_hop_le(string payload, bool valid)
    {
        var error = ApprovalRequestRules.ValidateCreate("1", payload);

        Assert.Equal(valid ? null : ApprovalErrorCodes.InvalidRequest, error);
    }

    [Fact]
    public void Khoa_doi_tuong_rong_hoac_qua_dai_bi_chan()
    {
        Assert.Equal(
            ApprovalErrorCodes.InvalidRequest,
            ApprovalRequestRules.ValidateCreate("", "{}"));

        Assert.Equal(
            ApprovalErrorCodes.InvalidRequest,
            ApprovalRequestRules.ValidateCreate(
                new string('x', ApprovalRequestRules.MaxTargetKeyLength + 1), "{}"));

        Assert.Null(ApprovalRequestRules.ValidateCreate("10", "{}"));
    }

    [Fact]
    public void Ly_do_tu_choi_bat_buoc_va_gioi_han_500_ky_tu()
    {
        Assert.Equal(
            ApprovalErrorCodes.RejectReasonRequired,
            ApprovalRequestRules.ValidateRejectReason("  ", out _));

        Assert.Equal(
            ApprovalErrorCodes.RejectReasonTooLong,
            ApprovalRequestRules.ValidateRejectReason(new string('x', 501), out _));

        Assert.Null(ApprovalRequestRules.ValidateRejectReason("  Sai giá  ", out var normalized));
        Assert.Equal("Sai giá", normalized);
    }

    [Theory]
    [InlineData(ApprovalRequestStatus.Pending, true)]
    [InlineData(ApprovalRequestStatus.Approved, false)]
    [InlineData(ApprovalRequestStatus.Rejected, false)]
    [InlineData(ApprovalRequestStatus.Cancelled, false)]
    public void Chi_cho_duyet_moi_duoc_quyet_dinh(ApprovalRequestStatus status, bool canDecide)
    {
        Assert.Equal(canDecide, ApprovalRequestRules.CanDecide(status));
    }
}
