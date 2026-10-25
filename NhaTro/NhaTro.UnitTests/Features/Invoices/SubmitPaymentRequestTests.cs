using System.ComponentModel.DataAnnotations;
using Web.Features.Invoices.DTOs;

namespace NhaTro.UnitTests.Features.Invoices;

public class SubmitPaymentRequestTests
{
    [Fact]
    public void Yeu_cau_hop_le_qua_kiem_tra_du_lieu()
    {
        Assert.True(IsValid(ValidRequest()));
    }

    [Fact]
    public void Thieu_minh_chung_bi_loi()
    {
        var request = ValidRequest();
        request.ProofFileIds.Clear();

        Assert.False(IsValid(request));
    }

    [Fact]
    public void So_tien_bang_0_bi_loi()
    {
        var request = new SubmitPaymentRequest
        {
            Amount = 0m,
            TransferredAt = new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero),
            ProofFileIds = [Guid.NewGuid()],
        };

        Assert.False(IsValid(request));
    }

    private static SubmitPaymentRequest ValidRequest() =>
        new()
        {
            Amount = 3_870_000m,
            TransferredAt = new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero),
            TransactionCode = "FT261020123456",
            ProofFileIds = [Guid.NewGuid()],
        };

    private static bool IsValid(SubmitPaymentRequest request) =>
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            validateAllProperties: true);
}
