using Web.Features.Assistant;
using Web.Features.Assistant.Enums;
using Web.Features.Assistant.Rules;

namespace NhaTro.UnitTests.Features.Assistant;

public class AiOutputValidatorTests
{
    [Fact]
    public void Chap_nhan_ban_tom_tat_bao_cao_hop_le()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ReportSummary, """
            {
              "headline": "Tóm tắt kỳ 11/2026",
              "summary": "Doanh thu ổn định, còn 2 hóa đơn quá hạn.",
              "insights": [{ "title": "Công nợ", "detail": "2 hóa đơn quá hạn.", "severity": "warning" }],
              "actions": [{ "title": "Thu nợ", "detail": "Gửi nhắc thanh toán.", "link": "/invoices" }],
              "metrics": [{ "label": "Doanh thu", "value": "12.000.000 đ" }]
            }
            """);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.NormalizedJson);
    }

    [Fact]
    public void Tu_choi_lien_ket_ngoai_he_thong()
    {
        var result = AiOutputValidator.Validate(AiUseCase.DailySummary, """
            {
              "headline": "Việc hôm nay",
              "summary": "1 việc cần làm.",
              "items": [{ "label": "Thu nợ", "count": 1, "link": "//evil.example", "severity": "info" }]
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Equal(AiErrorCodes.InvalidOutput, result.ErrorCode);
    }

    [Fact]
    public void Tu_choi_ma_phong_trung_trong_ban_nhap()
    {
        var result = AiOutputValidator.Validate(AiUseCase.RoomDraft, """
            {
              "property": { "name": "Nhà trọ A", "address": "12 Lê Lợi" },
              "rooms": [
                { "code": "P101", "floor": 1, "areaM2": 20, "standardPrice": 2500000 },
                { "code": "p101", "floor": 1, "areaM2": 20, "standardPrice": 2500000 }
              ],
              "notes": "Bản nháp"
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains("P101", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tu_choi_ban_nhap_phong_rong()
    {
        var result = AiOutputValidator.Validate(AiUseCase.RoomDraft, """
            { "rooms": [], "notes": "Không có phòng" }
            """);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Tu_choi_noi_dung_khong_phai_json()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ReminderMessage, "Xin chào");

        Assert.False(result.Succeeded);
        Assert.Equal(AiErrorCodes.InvalidOutput, result.ErrorCode);
    }

    [Fact]
    public void Tu_choi_noi_dung_rong()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ReportSummary, "   ");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Chap_nhan_ban_nhap_khach_thue_hop_le()
    {
        var result = AiOutputValidator.Validate(AiUseCase.TenantDraft, """
            {
              "fullName": "Nguyễn Văn A",
              "phoneNumber": "0901234567",
              "email": "a@example.com",
              "dateOfBirth": "1998-02-01",
              "gender": "Male",
              "permanentAddress": "12 Lê Lợi",
              "note": null
            }
            """);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public void Tu_choi_ban_nhap_khach_thue_thieu_so_dien_thoai()
    {
        var result = AiOutputValidator.Validate(AiUseCase.TenantDraft, """
            { "fullName": "Nguyễn Văn A", "phoneNumber": "" }
            """);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Chap_nhan_ban_nhap_dich_vu_hop_le()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ServiceDraft, """
            {
              "name": "Giữ xe",
              "unit": "xe",
              "calculationMethod": "fixed-monthly",
              "unitPrice": 100000,
              "effectiveDate": "2026-11-01",
              "effectiveTo": null,
              "note": null,
              "description": null
            }
            """);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public void Tu_choi_dich_vu_sai_cach_tinh()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ServiceDraft, """
            {
              "name": "Giữ xe",
              "unit": "xe",
              "calculationMethod": "đoán mò",
              "unitPrice": 100000,
              "effectiveDate": "2026-11-01",
              "effectiveTo": null,
              "note": null,
              "description": null
            }
            """);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Chap_nhan_ban_nhap_hop_dong_hop_le()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ContractDraft, """
            {
              "roomId": 5,
              "roomCode": "P101",
              "tenantIds": [9],
              "startDate": "2026-11-01",
              "endDate": "2027-10-31",
              "depositAmount": 3000000,
              "monthlyRent": 3000000,
              "paymentCycleMonths": 1,
              "meterReadingDay": null,
              "terms": null
            }
            """);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public void Tu_choi_hop_dong_khong_co_khach()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ContractDraft, """
            {
              "roomId": 5,
              "roomCode": "P101",
              "tenantIds": [],
              "startDate": "2026-11-01",
              "endDate": "2027-10-31",
              "depositAmount": null,
              "monthlyRent": null,
              "paymentCycleMonths": 1,
              "meterReadingDay": null,
              "terms": null
            }
            """);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Tu_choi_hop_dong_ngay_ket_thuc_truoc_ngay_bat_dau()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ContractDraft, """
            {
              "roomId": 5,
              "tenantIds": [9],
              "startDate": "2026-11-01",
              "endDate": "2026-11-01",
              "depositAmount": null,
              "monthlyRent": null,
              "paymentCycleMonths": 1,
              "meterReadingDay": null,
              "terms": null
            }
            """);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Tu_choi_urgency_khong_hop_le()
    {
        var result = AiOutputValidator.Validate(AiUseCase.IncidentTriage, """
            {
              "category": "Electrical",
              "urgency": "Sometime",
              "reason": "Chập điện"
            }
            """);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Bao_thieu_so_dien_thoai_bang_tieng_Viet()
    {
        var result = AiOutputValidator.Validate(AiUseCase.TenantDraft, """
            {
              "fullName": "Nguyễn Văn A",
              "phoneNumber": "",
              "email": null,
              "dateOfBirth": null,
              "gender": null,
              "permanentAddress": null,
              "note": null
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Equal("Thiếu số điện thoại.", result.ErrorMessage);
    }

    [Fact]
    public void Bao_khong_xac_dinh_duoc_khach_khi_hop_dong_rong()
    {
        var result = AiOutputValidator.Validate(AiUseCase.ContractDraft, """
            {
              "roomId": 5,
              "roomCode": "P101",
              "tenantIds": [],
              "startDate": "2026-11-21",
              "endDate": "2027-11-20",
              "depositAmount": null,
              "monthlyRent": 3000000,
              "paymentCycleMonths": 1,
              "meterReadingDay": null,
              "terms": null
            }
            """);

        Assert.False(result.Succeeded);
        Assert.Contains("khách thuê", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
