using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Features.Assistant.Enums;
using Web.Features.Assistant.Rules;
using Web.Features.Assistant.Services;

namespace NhaTro.UnitTests.Features.Assistant;

public class DemoAiChatClientTests
{
    private static readonly DemoAiChatClient Client = new(Options.Create(new AiOptions { Model = "demo-test" }));

    public static TheoryData<AiUseCase, string, string?> ValidCases => new()
    {
        {
            AiUseCase.ReportSummary,
            """
            {
              "common": { "periodLabel": "01/11/2026 – 21/11/2026", "propertyId": null },
              "revenue": { "totalRevenue": 12000000, "outstanding": 3000000, "overdueOutstanding": 1500000, "overdueInvoiceCount": 2 },
              "rooms": { "total": 10, "rented": 8, "available": 2 },
              "incidents": { "new": 1, "inProgress": 1, "urgent": 1 },
              "contracts": { "expiring30": 1 },
              "costs": { "missingSnapshots": 0 }
            }
            """,
            null
        },
        {
            AiUseCase.ReminderMessage,
            """
            {
              "common": { "propertyId": null },
              "debtors": [
                { "roomCode": "P101", "tenantNames": ["Nguyễn Văn A"], "amount": 1500000, "dueDate": "2026-11-25" }
              ]
            }
            """,
            null
        },
        {
            AiUseCase.RoomDraft,
            """
            { "common": { "propertyId": null }, "existingRoomCodes": ["P101"] }
            """,
            "Tạo nhà trọ Hoa Sen ở 12 Lê Lợi với 6 phòng, 2 tầng, giá 2,5 triệu, diện tích 20 m2"
        },
        {
            AiUseCase.IncidentTriage,
            """
            { "incidentText": "Chập điện ở phòng P101, có khói" }
            """,
            "Chập điện ở phòng P101, có khói"
        },
        {
            AiUseCase.DailySummary,
            """
            {
              "invoiceDue": { "overdueCount": 2, "overdueAmount": 3000000, "dueSoonCount": 1 },
              "incidents": { "open": 1, "urgent": 0 },
              "approvals": { "pending": 1 },
              "aiPending": 1,
              "expiringContracts": { "count": 1 }
            }
            """,
            null
        },
        {
            AiUseCase.TenantDraft,
            """{ "common": { "propertyId": null } }""",
            "Thêm khách thuê Nguyễn Văn A, SĐT 0901234567, email a@example.com, sinh ngày 01/02/1998, giới tính nam"
        },
        {
            AiUseCase.ServiceDraft,
            """
            {
              "common": { "propertyId": 1, "today": "2026-11-21" },
              "property": { "id": 1, "name": "Nhà A" },
              "today": "2026-11-21"
            }
            """,
            "Tạo dịch vụ giữ xe giá 100.000đ mỗi xe hàng tháng"
        },
        {
            AiUseCase.ContractDraft,
            """
            {
              "common": { "propertyId": 1 },
              "today": "2026-11-21",
              "rooms": [
                { "id": 5, "propertyId": 1, "propertyName": "Nhà A", "code": "P101", "floor": 1, "areaM2": 20, "standardPrice": 3000000, "status": "Available", "isAvailable": true }
              ],
              "tenants": [
                { "id": 9, "fullName": "Nguyễn Văn A", "phone": "0901234567", "email": null }
              ],
              "paymentCycles": [1, 2, 3, 6, 12]
            }
            """,
            "Hợp đồng cho khách Nguyễn Văn A thuê phòng P101 từ 01/11/2026 đến 31/10/2027, giá 3 triệu, cọc 1 tháng"
        },
    };

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void Demo_sinh_json_qua_duoc_bo_kiem_duyet(AiUseCase useCase, string contextJson, string? instruction)
    {
        var response = Client.Complete(new AiChatRequest(
            useCase,
            "system",
            "user",
            contextJson,
            instruction));

        Assert.True(response.Succeeded, response.ErrorMessage);

        var validation = AiOutputValidator.Validate(useCase, response.Json);
        Assert.True(validation.Succeeded, validation.ErrorMessage);
    }

    [Fact]
    public void Demo_hop_dong_suy_ra_tien_coc_theo_so_thang()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.ContractDraft,
            "system",
            "user",
            """
            {
              "common": { "propertyId": 1 },
              "today": "2026-11-21",
              "rooms": [
                { "id": 5, "propertyId": 1, "propertyName": "Nhà A", "code": "P101", "floor": 1, "areaM2": 20, "standardPrice": 3000000, "status": "Available", "isAvailable": true }
              ],
              "tenants": [
                { "id": 9, "fullName": "Nguyễn Văn A", "phone": "0901234567", "email": null }
              ]
            }
            """,
            "Hợp đồng phòng P101 giá 3 triệu, cọc 1 tháng"));

        Assert.True(response.Succeeded);
        Assert.Contains("\"depositAmount\":3000000", response.Json);
    }

    [Fact]
    public void Demo_dich_vu_dung_ngay_hieu_luc_trong_cau()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.ServiceDraft,
            "system",
            "user",
            """{ "common": { "propertyId": 1 }, "today": "2026-11-21" }""",
            "Tạo dịch vụ giữ xe giá 100.000đ mỗi xe hàng tháng, áp dụng từ 01/12/2026"));

        Assert.True(response.Succeeded);
        Assert.Contains("\"effectiveDate\":\"2026-12-01\"", response.Json);
    }

    private const string ContractContext = """
        {
          "common": { "propertyId": 1 },
          "today": "2026-11-21",
          "rooms": [
            { "id": 5, "propertyId": 1, "propertyName": "Nhà A", "code": "P101", "floor": 1, "areaM2": 20, "standardPrice": 3000000, "status": "Available", "isAvailable": true }
          ],
          "tenants": [
            { "id": 9, "fullName": "Nguyễn Văn A", "phone": "0901234567", "email": null },
            { "id": 12, "fullName": "Trần Thị Lan", "phone": "0912345678", "email": null }
          ]
        }
        """;

    [Fact]
    public void Demo_hop_dong_khop_ten_goi_chon_dung_khach()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.ContractDraft,
            "system",
            "user",
            ContractContext,
            "Hợp đồng cho Lan thuê phòng P101 từ 01/11/2026, giá 3 triệu"));

        Assert.True(response.Succeeded);
        Assert.Contains("\"tenantIds\":[12]", response.Json);
        Assert.True(AiOutputValidator.Validate(AiUseCase.ContractDraft, response.Json).Succeeded);
    }

    [Fact]
    public void Demo_hop_dong_khong_doan_bua_khi_ten_goi_mo_ho()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.ContractDraft,
            "system",
            "user",
            """
            {
              "common": { "propertyId": 1 },
              "today": "2026-11-21",
              "rooms": [
                { "id": 5, "propertyId": 1, "propertyName": "Nhà A", "code": "P101", "floor": 1, "areaM2": 20, "standardPrice": 3000000, "status": "Available", "isAvailable": true }
              ],
              "tenants": [
                { "id": 9, "fullName": "Nguyễn Thị Lan", "phone": "0901234567", "email": null },
                { "id": 12, "fullName": "Trần Thị Lan", "phone": "0912345678", "email": null }
              ]
            }
            """,
            "Hợp đồng cho Lan thuê phòng P101 từ 01/11/2026"));

        Assert.True(response.Succeeded);
        Assert.Contains("\"tenantIds\":[]", response.Json);
        Assert.False(AiOutputValidator.Validate(AiUseCase.ContractDraft, response.Json).Succeeded);
    }

    [Fact]
    public void Demo_hop_dong_khong_doan_bua_khi_khong_khop_khach()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.ContractDraft,
            "system",
            "user",
            ContractContext,
            "Hợp đồng cho khách tên Bình thuê phòng P101 từ 01/11/2026"));

        Assert.True(response.Succeeded);
        Assert.Contains("\"tenantIds\":[]", response.Json);

        var validation = AiOutputValidator.Validate(AiUseCase.ContractDraft, response.Json);
        Assert.False(validation.Succeeded);
        Assert.Contains("khách thuê", validation.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Demo_hop_dong_khong_doan_bua_khi_ma_phong_khong_co()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.ContractDraft,
            "system",
            "user",
            ContractContext,
            "Hợp đồng cho Nguyễn Văn A thuê phòng P999 từ 01/11/2026"));

        Assert.True(response.Succeeded);
        Assert.Contains("\"roomId\":null", response.Json);

        var validation = AiOutputValidator.Validate(AiUseCase.ContractDraft, response.Json);
        Assert.False(validation.Succeeded);
        Assert.Contains("phòng", validation.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Demo_ban_nhap_phong_tranh_ma_da_ton_tai()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.RoomDraft,
            "system",
            "user",
            """{ "common": { "propertyId": null }, "existingRoomCodes": ["P101"] }""",
            "Tạo nhà trọ 2 phòng"));

        Assert.True(response.Succeeded);
        Assert.DoesNotContain("\"P101\"", response.Json);
    }

    [Fact]
    public void Demo_nhan_dien_su_co_khan_cap()
    {
        var response = Client.Complete(new AiChatRequest(
            AiUseCase.IncidentTriage,
            "system",
            "user",
            "{}",
            "Chập điện, có khói và nguy hiểm"));

        Assert.Contains("\"urgency\":\"Emergency\"", response.Json);
        Assert.Contains("\"category\":\"Electrical\"", response.Json);
    }
}
