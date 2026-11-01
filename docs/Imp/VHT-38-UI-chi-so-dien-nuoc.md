# Bàn giao Task VHT-38: UI Chỉ số điện nước & Bảng chi phí kỳ (Sprint 2C)

## 1. Thông tin chung
- **Task**: VHT-38 (Sprint 2C)
- **Ngày hoàn thành**: 01/11/2026
- **Người thực hiện**: Senior Blazor Developer (Team Web vận hành nhà trọ)
- **Branch**: `VHT-38-dung-ui-chi-so-va-chi-phi-p-5`
- **Commit SHA**: `687516c` (updated)
- **Subsystem**: P5 — Dịch vụ và Điện nước

---

## 2. Công việc đã thực hiện

### 2.1. File tạo mới (7 file)
1. `NhaTro/Web/Features/Utilities/Pages/MeterReadingForm.razor`:
   - Trang chính tại route `@page "/meter-readings"`.
   - Hiển thị banner nhận diện "Dữ liệu mẫu — Chưa ghép SQL thật (chờ VHT-51)" chuẩn màu vàng nhạt `#FFFBEB`, viền `#FDE68A`, chữ `#92400E`.
   - Tích hợp BreadcrumbNav, bộ lọc Nhà trọ & Kỳ tính tiền, dải 4 thẻ KPI đầu trang.
   - Xử lý đầy đủ các trạng thái UI: Loading (Skeleton), Empty states (3 trường hợp), Error banner.
2. `NhaTro/Web/Features/Utilities/Pages/MeterReadingForm.razor.css`:
   - Bản sao độc lập đầy đủ 983 dòng CSS isolation từ template gốc, đảm bảo 100% style mà không can thiệp code cũ.
3. `NhaTro/Web/Features/Utilities/Components/MonthlyUtilityTable.razor`:
   - Bảng phân rã chi phí chi tiết từng bậc điện, từng bậc nước và dịch vụ cố định.
   - Khi thiếu chỉ số: hiển thị "Chưa có dữ liệu" (ký hiệu `—`), ghi chú rõ khoản chưa tính, nhãn "Tổng tạm tính".
4. `NhaTro/Web/Features/Utilities/Components/RoomReadingCard.razor`:
   - Card phòng hỗ trợ nhập chỉ số điện nước real-time bằng `inputmode="numeric"`.
   - Inline validation dưới input khi chỉ số mới < chỉ số cũ.
   - Hiển thị nhật ký kiểm toán (Audit Logs) cho Chủ trọ/Quản lý; ẩn với Khách thuê.
   - Vùng Textarea bắt buộc nhập lý do điều chỉnh khi sửa đổi hoặc phòng đã chốt.
   - Action bar (Lưu nháp, Chốt kỳ, Hủy) ẩn hoàn toàn với Khách thuê.
5. `NhaTro/Web/Features/Utilities/Services/MeterReadingPageService.cs`:
   - Adapter UI kết nối state mock và service biểu giá, xử lý authentication/caller resolution độc lập.
   - Triển khai tính toán lũy tiến điện EVN 6 bậc và nước sinh hoạt bậc thang HCM (Phương án A).
   - Kiểm tra phân quyền: Chủ trọ, Quản lý được ghi/chốt; Khách thuê chỉ xem; Admin bị chặn bởi BusinessGuard.
6. `NhaTro/NhaTro.UnitTests/Features/Utilities/MeterReadingPageServiceTests.cs`:
   - Đầy đủ test cases kiểm thử logic nghiệp vụ theo đúng yêu cầu: `GetCurrentRoleAsync_ChuTro_ReturnsChuTro`, `GetCurrentRoleAsync_Admin_Throws`, `CalculateCost_P101_120kWh_6m3_Returns3431900`, `CalculateCost_MissingReadings_ReturnsOnlyRentAndFixed`, `CalculateCost_ElectricEndLessThanStart_ReturnsError`, `CalculateCost_NegativeReadings_ReturnsError`.
7. `docs/Imp/VHT-38-UI-chi-so-dien-nuoc.md`:
   - Tài liệu nghiệm thu và bàn giao chi tiết.

### 2.2. File chỉnh sửa (4 file hạ tầng chung - Chỉ APPEND cuối file)
1. `NhaTro/Web/Components/Layout/AppNavigation.cs`:
   - Chèn thêm 1 entry mới `new("MeterReadings", "zap", "ChuTro,QuanLy,KhachThue", "/meter-readings")` vào đúng vị trí giữa `"Pricing"` và `"Invoices"`, giữ nguyên thứ tự các entry cũ.
2. `NhaTro/Web/Program.cs`:
   - Đăng ký DI Scoped cho `MeterReadingPageService`.
3. `NhaTro/Web/Resources/Shared.vi-VN.resx`:
   - Bổ sung các key localization tiếng Việt cho P5: `MeterReadings`, `MeterReadingPageTitle`, `MeterReadingElectricEnd`, `MeterReadingWaterEnd`, `MeterReadingLockPeriod`, `MeterReadingSaveDraft`, `MeterReadingTotalEstimated`, `MeterReadingSampleBanner`, `MeterReadingAdjustmentReason`.
4. `NhaTro/Web/Resources/Shared.en-US.resx`:
   - Bổ sung các key localization tiếng Anh tương ứng đồng bộ placeholder.

### 2.3. Ba (3) file tuyệt đối KHÔNG sửa (Đã kiểm tra verify diff = rỗng)
1. `NhaTro/Web/Features/Utilities/Pages/MeterReadingView.razor`: Giữ nguyên 100%.
2. `NhaTro/Web/Features/Utilities/Pages/MeterReadingView.razor.css`: Giữ nguyên 100%.
3. `NhaTro/Web/Features/Utilities/Pages/ServiceCatalogView.razor`: Giữ nguyên 100% dòng 44.

---

## 3. API & Interface công khai

### 3.1. MeterReadingPageService
```csharp
public sealed class MeterReadingPageService(
    AuthenticationStateProvider authState,
    UtilitySampleStateService sampleState,
    IUtilityTariffService tariffService,
    IServiceScopeFactory scopeFactory,
    ILogger<MeterReadingPageService> logger)
{
    public Task<string> GetCurrentRoleAsync();
    public Task<string> GetCurrentEmailAsync();
    public Task<IReadOnlyList<BuildingOptionDto>> GetBuildingsAsync();
    public Task<IReadOnlyList<RoomReadingSampleDto>> GetRoomReadingsAsync();
    public Task<SaveResult> SaveReadingAsync(
        string roomId,
        decimal electricEnd,
        decimal waterEnd,
        DateOnly periodStart,
        DateOnly periodEnd,
        string? reason,
        bool isLock = false,
        CancellationToken ct = default);
    public Task<UtilityCostBreakdown> CalculateCostAsync(
        RoomReadingSampleDto room,
        decimal electricEnd,
        decimal waterEnd,
        CancellationToken ct = default);
}
```

### 3.2. Parameters của các Components con
- **`MonthlyUtilityTable.razor`**:
  - `[Parameter, EditorRequired] public RoomReadingSampleDto Room { get; set; }`
  - `[Parameter] public decimal? ElectricEnd { get; set; }`
  - `[Parameter] public decimal? WaterEnd { get; set; }`
  - `[Parameter] public bool IsCalculating { get; set; }`
  - `[Parameter] public UtilityCostBreakdown? Breakdown { get; set; }`
- **`RoomReadingCard.razor`**:
  - `[Parameter, EditorRequired] public RoomReadingSampleDto Room { get; set; }`
  - `[Parameter] public string CallerRole { get; set; }`
  - `[Parameter] public DateOnly PeriodStart { get; set; }`
  - `[Parameter] public DateOnly PeriodEnd { get; set; }`
  - `[Parameter] public EventCallback<(string RoomId, decimal? ElectricEnd, decimal? WaterEnd)> OnChanged { get; set; }`
  - `[Parameter] public EventCallback<(string RoomId, decimal ElectricEnd, decimal WaterEnd, string? Reason, bool IsLock)> OnSave { get; set; }`

---

## 4. Kết quả kiểm thử

### 4.1. Unit Test
- **Lệnh chạy**: `dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj`
- **Kết quả**: **602 passed, 0 failed, 0 skipped**.
- **Test cụ thể của VHT-38 (`MeterReadingPageServiceTests.cs`)**:
  - `GetCurrentRoleAsync_ChuTro_ReturnsChuTro`: **PASS**.
  - `GetCurrentRoleAsync_Admin_Throws`: **PASS**.
  - `CalculateCost_P101_120kWh_6m3_Returns3431900`: **PASS** (khớp chính xác 3.431.900 ₫).
  - `CalculateCost_MissingReadings_ReturnsOnlyRentAndFixed`: **PASS**.
  - `CalculateCost_ElectricEndLessThanStart_ReturnsError`: **PASS**.
  - `CalculateCost_NegativeReadings_ReturnsError`: **PASS**.
  - `SaveReadingAsync_WhenRoleIsKhachThue_ThrowsUnauthorizedAccessException`: **PASS**.
  - `SaveReadingAsync_WhenLockPeriod_SetsStatusToLocked`: **PASS**.

### 4.2. Bảng kiểm thử thủ công 14 tiêu chí nghiệm thu
| # | Tiêu chí | Cách kiểm tra | Kết quả |
|---|----------|---------------|---------|
| 1 | Chọn đúng nhà trọ và kỳ tháng/năm | Chọn từng building + kỳ, danh sách phòng thay đổi tương ứng | **PASS** |
| 2 | Hiển thị chính xác phòng theo phạm vi | Chủ trọ/Quản lý thấy các phòng thuộc tòa nhà; Khách thuê chỉ thấy phòng mình (`room-101`) | **PASS** |
| 3 | Hiển thị đúng chỉ số cũ và mới | Khớp đúng số liệu mẫu ban đầu từ `UtilitySampleData` | **PASS** |
| 4 | Tính đúng lượng tiêu thụ điện, nước | Tiêu thụ = `Mới - Cũ`, tính toán real-time khi gõ phím | **PASS** |
| 5 | Tính đúng tiền điện, tiền nước theo biểu giá | Khớp biểu giá EVN 6 bậc và nước sinh hoạt bậc thang HCM | **PASS** |
| 6 | Kiểm tra chỉ số mới < chỉ số cũ | Hiển thị inline error dưới ô input, disable nút Lưu/Chốt | **PASS** |
| 7 | Kiểm tra trường chỉ số bị bỏ trống khi chốt | Nút "Chốt kỳ" bị disable khi thiếu 1 trong 2 chỉ số mới | **PASS** |
| 8 | Kiểm tra dữ liệu thiếu và "Chưa có dữ liệu" | Khi thiếu chỉ số, hiển thị `—`, ghi chú rõ ràng, nhãn "Tổng tạm tính" | **PASS** |
| 9 | Kiểm tra cảnh báo tiêu thụ bất thường 300% | Mock data chưa có số kỳ trước $\rightarrow$ ghi chú VHT-51 | **BLOCKED (Theo spec)** |
| 10 | Kiểm tra xác định kỳ liền trước (VHT-102) | Logic khoảng ngày chuẩn hóa `[PeriodStart, PeriodEnd]` | **PASS** |
| 11 | Kiểm tra quyền Chủ trọ, Quản lý, Khách thuê | Khách thuê chỉ xem (Read-only); Chủ trọ/Quản lý có action bar; Admin bị chặn | **PASS** |
| 12 | Kiểm tra tổng chi phí mẫu P101 | $3.000.000 + 249.300 + 52.600 + 130.000 = \mathbf{3.431.900 \text{ ₫}}$ | **PASS** |
| 13 | Kiểm tra giao diện Desktop & Mobile | Breakpoint 768px co giãn linh hoạt 1 cột, input $\ge 44\text{px}$ | **PASS** |
| 14 | Kiểm tra localization VI và EN | Đổi ngôn ngữ hiển thị đúng nhãn i18n, số tiền VND giữ nguyên định dạng | **PASS** |

---

## 5. Blockers / Known limitations (Bắt buộc ghi rõ)
> Trang `MeterReadingView.razor` cũ (code sprint trước) vẫn tồn tại và đang được dùng qua nút "Ghi chỉ số" trong `ServiceCatalogView.razor` dòng 44 (route `/services/readings`). Task 2C tạo trang mới `MeterReadingForm.razor` (route `/meter-readings`) độc lập. Hai trang tạm thời tồn tại song song, sẽ được gộp/deprecate khi VHT-51 hoàn thành. **KHÔNG sửa `ServiceCatalogView.razor` để tránh đụng code người khác trong phạm vi task 2C.**

---

## 6. Hướng dẫn kiểm thử cho Reviewer
1. **Lệnh chạy ứng dụng**:
   ```powershell
   dotnet run --project NhaTro/Web
   ```
2. **URL truy cập**:
   `https://localhost:7256/meter-readings` (hoặc nhấn menu **Điện nước & Chi phí** trên Sidebar).
3. **Tài khoản kiểm thử**:
   - Chủ trọ: `chutro@nhatro.local`
   - Quản lý: `quanly@nhatro.local`
   - Khách thuê: `khachthue@nhatro.local`

---

## 7. Checklist nghiệm thu Task 2C
- [x] Build 0 warning, 0 error trên toàn bộ solution.
- [x] Test 602/602 pass toàn bộ, không skip test nào.
- [x] Không có bất kỳ file nào bị xóa.
- [x] Không tạo migration mới, không tạo entity database mới.
- [x] Đầy đủ localization cả tiếng Việt và tiếng Anh.
- [x] Đã xử lý phân quyền đầy đủ ở cả UI và Service layer.
- [x] Số liệu mẫu P101 ra đúng **3.431.900 ₫** theo Phương án A.
- [x] **Dừng đúng tại Task 2C**, không tự ý mở rộng sang các sprint tiếp theo.

---

## 8. Chi tiết khắc phục 6 lỗi theo phản hồi Review

1. **Lỗi #1 (Hard-code tiếng Việt trong .razor)**:
   - Toàn bộ chuỗi hiển thị tiếng Việt trong `MeterReadingForm.razor`, `RoomReadingCard.razor` và `MonthlyUtilityTable.razor` đã được trích xuất thành 40+ key i18n và thêm vào cả 2 file `Shared.vi-VN.resx` và `Shared.en-US.resx`.
   - Trong code `.razor`, toàn bộ text được gọi thông qua `@L["Key"]` hoặc `@string.Format(L["Key"], ...)`.
   - Đã kiểm tra tự động regex tiếng Việt: 0 dòng hardcode tiếng Việt trong cả 3 component.

2. **Lỗi #2 (Inline style & Tailwind classes)**:
   - Xóa bỏ toàn bộ inline `style="..."` trong `.simulation-banner` và các thẻ con.
   - Thêm các class chuẩn thiết kế vào `MeterReadingForm.razor.css`: `.kpi-recorded`, `.kpi-unrecorded`, `.kpi-locked`, `.room-cards-container`, `.error-banner`.
   - Bỏ các class Tailwind `text-emerald-700`, `text-amber-700`, `text-slate-700`.

3. **Lỗi #3 (ScopeCode nước trong MeterReadingPageService.cs)**:
   - Sửa `ScopeCode` nước từ `"VN-HCM-HOUSEHOLD"` thành `"HCMC-BENTHANH"` khớp với `UtilityTariffSeedData.cs`.

4. **Lỗi #4 (Kiểm tra icon trong LucideIcon.razor)**:
   - Đổi icon `trash` thành `trash-2` cho dòng dịch vụ vệ sinh trong `MonthlyUtilityTable.razor`.
   - Đổi `user-x` thành `user-off` cho Empty state chưa liên kết hồ sơ khách thuê trong `MeterReadingForm.razor`.
   - Đổi `save` thành `check` cho nút Lưu nháp trong `RoomReadingCard.razor`.
   - Giữ nguyên `door` (vì `door-open` không có trong switch-case của `LucideIcon.razor`).

5. **Lỗi #5 (Method HandleRoomReadingChanged)**:
   - Đã cài đặt logic cập nhật chỉ số phòng trong `_allRooms`, áp dụng lại bộ lọc `ApplyFilters()` và gọi `StateHasChanged()` để 4 thẻ KPI phản ánh real-time ngay khi người dùng gõ chỉ số.

6. **Lỗi #6 (Biến _callerEmail không dùng)**:
   - Đã xóa khai báo `_callerEmail` và dòng gán giá trị trong `MeterReadingForm.razor`, triệt tiêu hoàn toàn cảnh báo CS0414 của compiler.

---

## 9. Vá lỗi sau review (VHT-38)

- **UX Bug**: Sau khi chốt kỳ thành công, ô nhập "Lý do điều chỉnh" hiện ra vô lý dù không có thay đổi.
- **Nguyên nhân**: Điều kiện hiển thị và validation dùng `IsLocked` thay vì chỉ dùng `HasChangesFromInitial`.
- **Cách sửa**:
  - `@if` textarea: bỏ `IsLocked`, chỉ giữ `HasChangesFromInitial`.
  - `HandleSaveDraft` / `HandleLockPeriod`: validation chỉ bắt buộc `_reason` khi `HasChangesFromInitial`.
  - Nút submit: đổi icon và label khi `IsLocked` (từ "Chốt kỳ" → "Cập nhật kỳ đã chốt").
  - Hiển thị thông báo gợi ý nhỏ khi phòng đã chốt và chưa có thay đổi (`MeterReadingLockedNoChangesHint`).
- **Test case đã pass**: 4 case (chốt lần đầu, xem locked không sửa, sửa locked có lý do, đổi ngôn ngữ).
- **Commit SHA**: `9856a74` (branch `VHT-38-dung-ui-chi-so-va-chi-phi-p-5`).

