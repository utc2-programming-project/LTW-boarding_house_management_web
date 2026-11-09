using System.Globalization;
using System.Resources;
using Web.Features.Invoices;

namespace NhaTro.UnitTests.Features.Invoices;

// VHT-55: trang chi tiết hóa đơn dùng resource VI/EN; mọi khóa phải có ở cả hai ngôn ngữ.
public sealed class InvoiceResourceTests
{
    private static readonly ResourceManager Manager =
        new("Web.Resources.Features.Invoices.InvoiceResources", typeof(InvoiceResources).Assembly);

    [Fact]
    public void Both_languages_have_matching_nonempty_keys()
    {
        var vi = Read("vi-VN");
        var en = Read("en-US");

        Assert.Equal(vi.Keys.Order(), en.Keys.Order());
        Assert.All(vi.Values.Concat(en.Values), value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void Payment_flow_keys_exist_in_both_languages()
    {
        var vi = Read("vi-VN");
        var en = Read("en-US");

        string[] keys =
        [
            "PaymentHeading",
            "PaymentStatusPending",
            "PaymentStatusConfirmed",
            "PaymentStatusRejected",
            "PaymentStatusNone",
            "PaymentAmount",
            "PaymentAmountHint",
            "PaymentAmountMismatch",
            "PaymentTransferredAt",
            "PaymentProof",
            "PaymentProofHint",
            "PaymentProofRequired",
            "PaymentSubmit",
            "PaymentSubmitting",
            "PaymentSubmittedSuccess",
            "PaymentCheckResult",
            "PaymentResultUnknown",
            "PaymentPendingNotice",
            "PaymentRejectedNotice",
            "PaymentPaidNotice",
            "PaymentDraftNotice",
            "PaymentHistoryHeading",
            "PayErrorForbidden",
            "PayErrorAmountMismatch",
            "PayErrorPendingPaymentExists",
            "PayErrorConcurrencyConflict",
            "PayErrorFileSizeExceeded",
            "PayErrorUploadUnknown",
        ];

        foreach (var key in keys)
        {
            Assert.True(vi.ContainsKey(key), $"Thiếu khóa VI: {key}");
            Assert.True(en.ContainsKey(key), $"Thiếu khóa EN: {key}");
        }
    }

    [Fact]
    public void Payment_review_keys_exist_in_both_languages()
    {
        var vi = Read("vi-VN");
        var en = Read("en-US");

        string[] keys =
        [
            "ReviewPageTitle",
            "ReviewHeading",
            "ReviewSubtitle",
            "ReviewStatPending",
            "ReviewStatConfirmed",
            "ReviewStatRejected",
            "ReviewStatPendingAmount",
            "ReviewFilterStatus",
            "ReviewAllStatuses",
            "ReviewThRequest",
            "ReviewThInvoice",
            "ReviewThRoomProperty",
            "ReviewThTenants",
            "ReviewThPeriod",
            "ReviewThAmount",
            "ReviewThStatus",
            "ReviewViewDetail",
            "ReviewDetailPageTitle",
            "ReviewDetailHeading",
            "ReviewDetailSubtitle",
            "ReviewBackToList",
            "ReviewInvoiceHeading",
            "ReviewPaymentHeading",
            "ReviewConfirmAction",
            "ReviewRejectAction",
            "ReviewConfirmTitle",
            "ReviewConfirmMessage",
            "ReviewConfirmText",
            "ReviewConfirmSuccess",
            "ReviewRejectTitle",
            "ReviewRejectReasonLabel",
            "ReviewRejectReasonPlaceholder",
            "ReviewRejectSubmit",
            "ReviewRejectSuccess",
            "ReviewResultUnknown",
            "ReviewCheckResult",
            "ReviewChecking",
            "ReviewProcessing",
            "ReviewResultChecked",
            "ReviewConfirmedNotice",
            "ReviewRejectedNotice",
            "ReviewNotPendingNotice",
            "ReviewDetailNotFoundTitle",
            "ReviewDetailNotFoundDesc",
            "ReviewErrorForbidden",
            "ReviewErrorInvoiceAlreadyPaid",
            "ReviewErrorInvalidStatus",
            "ReviewErrorPaymentNotFound",
            "ReviewErrorRejectionReasonRequired",
            "ReviewErrorRejectionReasonTooLong",
            "ReviewErrorConcurrency",
            "ReviewErrorUnknown",
        ];

        foreach (var key in keys)
        {
            Assert.True(vi.ContainsKey(key), $"Thiếu khóa VI: {key}");
            Assert.True(en.ContainsKey(key), $"Thiếu khóa EN: {key}");
        }
    }

    private static Dictionary<string, string> Read(string culture)
    {
        // Không dispose: ResourceManager có thể trả về resource set dùng chung cho lần đọc sau.
        var set = Manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, false);
        Assert.NotNull(set);
        return set.Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
