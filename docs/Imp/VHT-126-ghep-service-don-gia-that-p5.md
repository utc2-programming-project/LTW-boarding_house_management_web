# Báo Cáo Nghiệm Thu & Đánh Giá — VHT-126: Ghép Service Đơn Giá Thật P5

- **Task cha:** VHT-37 — Dựng UI đơn giá và dịch vụ P5
- **Task thực hiện:** VHT-126 — Ghép service đơn giá thật P5
- **Sprint:** Sprint 2C
- **Phân hệ:** P5 — Utilities
- **Branch:** `VHT-126-ghep-service-don-gia-that-p-5` (base `dev`)
- **Ngày hoàn thành:** 02/11/2026
- **Trạng thái tổng thể:** **DONE** (Hoàn thành toàn diện cả luồng đọc và ghi dữ liệu SQL Server thật, chuyển đổi schema Identity `CreatedByUserId` sang `nvarchar(450)`, áp dụng Property Scope qua `PropertyCaller` và `PropertyScope.Apply`, mở khóa ghi cho Chủ trọ kèm xác minh đọc lại từ SQL Server, bảo toàn 100% giao diện 2A).

---

## 1. Phân loại trạng thái các hạng mục triển khai

### 1.1. DONE (Đã hoàn thành 100%)

1. **Chuyển đổi Schema `ServicePriceHistory.CreatedByUserId` sang ASP.NET Core Identity:**
   - Cập nhật entity [`ServicePriceHistory.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Entities/ServicePriceHistory.cs): Đổi `CreatedByUserId` từ `int` sang `string?` (`nvarchar(450)`).
   - Cấu hình EF Core tại [`ServicePriceHistoryConfiguration.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Data/Configurations/Utilities/ServicePriceHistoryConfiguration.cs): Cấu hình độ dài tối đa 450, quan hệ foreign key với `ApplicationUser` (`DeleteBehavior.Restrict`) và index `IX_ServicePriceHistory_CreatedByUserId`.
   - Tạo migration mới [`20261102081030_ChangeCreatedByUserIdToString.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Data/Migrations/20261102081030_ChangeCreatedByUserIdToString.cs):
     - Alter column `CreatedByUserId` sang `nvarchar(450)` nullable.
     - Lệnh SQL chuyển đổi dữ liệu cũ trong `Up()`: cập nhật các ID không tồn tại trong `AspNetUsers` thành `NULL`.
     - Tạo Index và Foreign Key ràng buộc toàn vẹn dữ liệu.
     - **Rollback an toàn trong `Down()`:** Tuyệt đối không thay thế dữ liệu thành `0`. Thay vào đó, kiểm tra bằng SQL `IF EXISTS`: nếu còn bất kỳ dòng nào có giá trị `NULL` hoặc chuỗi Identity không thể chuyển chính xác sang `int`, chủ động phát sinh lỗi (`THROW 50000`) và hủy bỏ transaction rollback để bảo vệ dữ liệu. Chỉ chuyển về `int NOT NULL` khi toàn bộ dữ liệu có thể chuyển đổi toàn vẹn.
     - Tuyệt đối không chỉnh sửa bất kỳ migration lịch sử nào.

2. **Chuyển đổi Contract Service sang `PropertyCaller` & Bổ sung Property Scope:**
   - Cập nhật interface [`IServiceCatalogService.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Services/IServiceCatalogService.cs) và implementation [`ServiceCatalogService.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Services/ServiceCatalogService.cs):
     - `GetServiceCatalogsAsync(PropertyCaller caller)`
     - `CreateOrUpdateServicePriceAsync(CreateServiceCatalogDto request, PropertyCaller caller)`
     - `CreateOrUpdateServicePriceAsync(int serviceCatalogId, UpdateServicePriceDto request, PropertyCaller caller)`
   - **Thực thi Property Scope:**
     - Sử dụng `PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)` để giới hạn phạm vi truy vấn danh mục dịch vụ.
     - Danh mục hệ thống (`PropertyId == null`) hiển thị cho tất cả vai trò được xem.
     - Bắt buộc có `PropertyId` khi tạo danh mục dịch vụ; chặn tuyệt đối việc tạo danh mục global (`PropertyId == null`).
     - Kiểm tra nghiêm ngặt quyền sở hữu nhà trọ qua Property Scope trước khi thực hiện ghi (tạo mới hoặc cập nhật giá). Không cho phép truy cập chéo property giữa các chủ trọ.

3. **Mở khóa thao tác Ghi (Create/Update) trên Adapter & UI:**
   - [`ServiceCatalogPageService.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Services/ServiceCatalogPageService.cs):
     - Khởi tạo `PropertyCaller(userId, role)` từ thông tin xác thực hiện tại.
     - `CheckWriteSupport()` trả về `(true, null)` cho phép ghi.
     - Triển khai `AddServiceAsync(CreateServiceRequest request)` và `UpdateServicePriceAsync(UpdatePriceRequest request)`:
       - Kiểm tra vai trò Chủ trọ (`caller.IsChuTro`).
       - Gọi backend service với `PropertyCaller` thật.
       - **Đọc lại dữ liệu từ SQL Server để xác minh dữ liệu đã thực sự được lưu trước khi trả về thành công cho UI.**
   - UI Modals ([`ServiceCreateModal.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Components/ServiceCreateModal.razor), [`ServicePriceModal.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Components/ServicePriceModal.razor)):
     - Mở khóa nút Submit cho Chủ trọ khi thao tác ghi đã sẵn sàng.
     - Ẩn thông báo tạm dừng chức năng ghi khi `CanWrite == true`.
   - UI Views ([`ServiceCatalogView.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Pages/ServiceCatalogView.razor), [`ServicePricesView.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Pages/ServicePricesView.razor)):
     - Ẩn banner "Dữ liệu mẫu" khi kết nối SQL Server thành công.
     - Phân quyền `_canWrite` chỉ kích hoạt cho `Roles.ChuTro`.

4. **Bảo toàn Giao diện 2A & CSS:**
   - 0 CSS diffs (`git diff -- "*.css"` không có thay đổi nào).
   - Bảo toàn 100% cấu trúc HTML markup và giao diện 2A.
   - Không ảnh hưởng đến module ghi chỉ số (VHT-38 / VHT-51).

---

## 2. Bằng chứng kiểm thử tự động (Test Evidence)

### 2.1. Kiểm thử tích hợp SQL Server thật ([`ServiceCatalogServiceIntegrationTests.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/NhaTro.IntegrationTests/Features/Utilities/ServiceCatalogServiceIntegrationTests.cs))
- `CreateOrUpdateServicePriceAsync_AppendsVersionWithoutChangingHistoricalSnapshot`: **PASS** (Tạo catalog và phiên bản mới, xác minh lưu `CreatedByUserId` là string Identity User ID).
- `GetServiceCatalogsAsync_AllowsLandlordManagerAndTenantForGlobalCatalogs`: **PASS** (Danh mục dùng chung `PropertyId == null` hiển thị cho cả Chủ trọ, Quản lý, Khách thuê).
- `GetServiceCatalogsAsync_SelectsMostRecentPriceEffectiveToday`: **PASS**.
- `GetServiceCatalogsAsync_ReturnsNextScheduledPriceWhenAllVersionsAreFuture`: **PASS**.
- `CreateOrUpdateServicePriceAsync_RejectsDuplicateEffectiveDate`: **PASS**.
- `CreateOrUpdateServicePriceAsync_CannotChangeReadOnlyTariff`: **PASS**.
- `CreateOrUpdateServicePriceAsync_RejectsGlobalCatalogCreation`: **PASS** (Chặn tạo catalog có `PropertyId == null`).
- `CreateOrUpdateServicePriceAsync_RejectsCrossPropertyAccess`: **PASS** (Chặn tạo/sửa catalog thuộc nhà trọ của chủ khác).

### 2.2. Kiểm thử tích hợp Adapter & Property Scope ([`ServiceCatalogPageServiceIntegrationTests.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/NhaTro.IntegrationTests/Features/Utilities/ServiceCatalogPageServiceIntegrationTests.cs))
- `PropertyScope_ChuTro_OnlySeesOwnedProperties`: **PASS**.
- `PropertyScope_QuanLy_OnlySeesAssignedActiveProperties`: **PASS**.
- `PropertyScope_KhachThue_OnlySeesPropertyWithApprovedContract`: **PASS**.
- `PropertyScope_KhachThue_ExpiredOrFutureContract_DoesNotGrantAccess`: **PASS**.
- `PropertyScope_KhachThue_PendingOrRejectedContract_DoesNotGrantAccess`: **PASS**.
- `GetServicesForCallerAsync_SystemCatalogsVisibleToAll_PropertyCatalogsStrictlyScoped`: **PASS**.
- `AddServiceAsync_Owner_SuccessfullyPersistsAndVerifiesFromSql`: **PASS** (Chủ trọ tạo dịch vụ thành công, dữ liệu được ghi và kiểm tra trực tiếp từ DB).
- `AddServiceAsync_CrossPropertyOrNonOwner_Rejected`: **PASS** (Quản lý hoặc truy cập chéo nhà trọ bị từ chối).
- `UpdateServicePriceAsync_Owner_SuccessfullyPersistsAndVerifiesFromSql`: **PASS** (Cập nhật đơn giá thành công, kiểm tra trực tiếp từ DB).

### 2.3. Tổng kết Suite Tests
- **Unit Tests (`NhaTro.UnitTests.csproj`):** **626/626 PASSED (100%)**.
- **Integration Tests (`NhaTro.IntegrationTests.csproj`):** **187/187 PASSED (100%)** (bao gồm 3 test kiểm chứng rollback Down migration an toàn: thành công khi ID là số, từ chối và giữ nguyên dữ liệu khi có NULL hoặc GUID).

---

## 3. Danh sách các file thay đổi

| STT | File | Thay đổi chính |
|---|---|---|
| 1 | [`ServicePriceHistory.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Entities/ServicePriceHistory.cs) | Đổi `CreatedByUserId` sang `string?` |
| 2 | [`ServicePriceHistoryConfiguration.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Data/Configurations/Utilities/ServicePriceHistoryConfiguration.cs) | Cấu hình quan hệ với `ApplicationUser`, FK `Restrict`, index |
| 3 | [`20261102081030_ChangeCreatedByUserIdToString.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Data/Migrations/20261102081030_ChangeCreatedByUserIdToString.cs) | Migration mới xử lý đổi kiểu và làm sạch dữ liệu cũ |
| 4 | [`IServiceCatalogService.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Services/IServiceCatalogService.cs) | Đổi contract sang nhận `PropertyCaller` |
| 5 | [`ServiceCatalogService.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Services/ServiceCatalogService.cs) | Bổ sung `PropertyScope.Apply`, bắt buộc `PropertyId`, kiểm tra quyền sở hữu |
| 6 | [`ServiceCatalogPageService.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Services/ServiceCatalogPageService.cs) | Mở khóa thao tác ghi, truyền `PropertyCaller`, kiểm tra xác minh từ SQL |
| 7 | [`ServiceCatalogView.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Pages/ServiceCatalogView.razor) | Mở khóa ghi cho Chủ trọ, ẩn banner dữ liệu mẫu khi có SQL |
| 8 | [`ServicePricesView.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Pages/ServicePricesView.razor) | Mở khóa ghi cho Chủ trọ, ẩn banner dữ liệu mẫu khi có SQL |
| 9 | [`ServiceCreateModal.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Components/ServiceCreateModal.razor) | Bỏ hoàn toàn mock, nhận Buildings & CanWrite từ parent |
| 10 | [`ServicePriceModal.razor`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/Web/Features/Utilities/Components/ServicePriceModal.razor) | Bỏ hoàn toàn mock, nhận Buildings & CanWrite từ parent |
| 11 | [`ServiceCatalogServiceTests.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/NhaTro.UnitTests/Features/Utilities/ServiceCatalogServiceTests.cs) | Cập nhật unit tests theo `PropertyCaller` và ràng buộc `PropertyId` |
| 12 | [`ServiceCatalogPageServiceTests.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/NhaTro.UnitTests/Features/Utilities/ServiceCatalogPageServiceTests.cs) | Cập nhật unit tests cho thao tác ghi |
| 13 | [`ServiceCatalogServiceIntegrationTests.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/NhaTro.IntegrationTests/Features/Utilities/ServiceCatalogServiceIntegrationTests.cs) | Bổ sung integration tests cho `PropertyCaller`, scope, global catalog rejection |
| 14 | [`ServiceCatalogPageServiceIntegrationTests.cs`](file:///d:/LAP%20TRINH%20WEB/BAITAPLON/LTW-boarding_house_management_web/NhaTro/NhaTro.IntegrationTests/Features/Utilities/ServiceCatalogPageServiceIntegrationTests.cs) | Bổ sung integration tests cho thao tác ghi và xác minh SQL |


---

## 4. Báo Cáo Xử Lý 8 Lỗi Kiểm Thử Thực Tế (Tester Findings)

| Mã lỗi | Mô tả lỗi | Nguyên nhân gốc | File đã sửa | Có Migration? | Test chứng minh | Phân loại | Kết quả |
|---|---|---|---|:---:|---|:---:|:---:|
| **TF-01** | Bật/tắt trạng thái sử dụng: Switch luôn disabled | Entity `ServiceCatalog` chưa có `IsActive` và backend thiếu API toggle | `ServiceCatalog.cs`, `ServiceCatalogConfiguration.cs`, `IServiceCatalogService.cs`, `ServiceCatalogService.cs`, `ServiceCatalogPageService.cs`, `ServiceCatalogView.razor` | Có (`20261102152923_AddServiceCatalogIsActiveAndEffectiveTo`) | `ToggleServiceCatalogStatusAsync_Owner_SuccessfullyTogglesAndPersists`, `ToggleServiceActiveAsync_Owner_SuccessfullyTogglesAndReReadsFromSql` | **FIXED** | **PASS** |
| **TF-02** | Ngày hết hiệu lực: Form có nhập nhưng DB không lưu, bảng hiển thị "—" | Schema định giá thiếu cột EffectiveTo trong ServicePriceHistory và DTO chưa nhận | `ServicePriceHistory.cs`, `ServicePriceHistoryConfiguration.cs`, `CreateServiceCatalogDto.cs`, `UpdateServicePriceDto.cs`, `ServiceCatalogDto.cs`, `ServiceCatalogRules.cs`, `ServiceCatalogService.cs`, `ServiceCatalogPageService.cs`, `ServicePricesView.razor` | Có (`20261102152923_AddServiceCatalogIsActiveAndEffectiveTo`) | `EffectiveTo_WhenNewVersionAdded_ClosesPreviousVersionAndSetsNewToNull`, `EffectiveTo_ExplicitValue_IsPersistedAndNotExtendedByLaterVersion`, `AddServiceAsync_WithEffectiveTo_PersistsEffectiveToInSql` | **FIXED** | **PASS** |
| **TF-03** | Dữ liệu SQL thật bị gắn nhãn "Bản ghi mẫu"; Toast báo chưa lưu | `ServiceCatalogView.razor` thiếu `@if (_isSampleData)` bọc quanh `record-origin-badge`. Toast hiển thị resource key cũ của mock data. | `ServiceCatalogView.razor`, `UtilityResources.*.resx` | Không | `ServiceCatalogView.razor` L209, `ToastCreatedSuccessSql` | **FIXED** | **PASS** |
| **TF-04** | Lịch sử giá: Người tạo "—", CreatedAt `01/01/0001`, Ghi chú trống | `ServicePriceVersionDto` thiếu `CreatedAt` & `CreatedByUserId`, mapping gán `DateTime.MinValue`. Ghi chú (Note) là legal reference cho tariff và để trống cho catalog thường. | `ServiceCatalogDto.cs`, `ServiceCatalogService.cs`, `ServiceCatalogPageService.cs`, `ServicePricesView.razor` | Không | `GetPriceVersionsForCallerAsync_ResolvesRealUserCreatorNameAndValidCreatedAt` | **FIXED** | **PASS** |
| **TF-05** | Đơn giá phải lớn hơn 0: Backend cho phép 0m, UI chặn <= 0m | `ServiceCatalogRules.cs` kiểm tra `unitPrice < 0m` thay vì `<= 0m`. | `ServiceCatalogRules.cs`, `ServiceCatalogPageService.cs`, `ServiceCatalogRulesTests.cs`, `ServiceCatalogServiceTests.cs` | Không | `CreateValidation_RejectsZeroPrice`, `UpdateValidation_RejectsNonPositiveUnitPrice` | **FIXED** | **PASS** |
| **TF-06** | Điện/nước read-only: Chưa có seed catalog trong ServiceCatalog | Chọn Phương án A: tổng hợp `UtilityTariff` làm read-only services mà không duplicate data | `ServiceCatalogPageService.cs` | Không | `GetServicesForCallerAsync_IncludesUtilityTariffsAsReadOnly`, `UpdateServicePriceAsync_Tariff_Blocked`, `ToggleServiceActiveAsync_TariffOrNonOwner_Rejected` | **FIXED** | **PASS** |
| **TF-07** | Hardcode tiếng Việt trong view, toast, message | Còn các chuỗi tiếng Việt hardcoded (nhãn lỗi SQL, modal/service error, tariff name). Đã chuyển 100% sang resource key qua `ServiceCatalogErrorKeys`. | `ServiceCatalogPageService.cs`, `ServiceCatalogErrorKeys.cs`, `ServiceCatalogView.razor`, `ServicePricesView.razor`, `UtilityResources.*.resx` | Không | 20 key song ngữ VI/EN mới, toàn bộ test assert qua `UtilityKeyLocalizer` | **FIXED** | **PASS** |
| **TF-08** | Cho phép chọn "Tất cả" khi tạo dịch vụ trong modal | `ServiceCreateModal.razor` không lọc bỏ tùy chọn `all` và không chặn submit trước khi gọi backend. | `ServiceCreateModal.razor` | Không | `ServiceCreateModal.razor` L86, `AddServiceAsync_RejectsZeroOrNegativeRateAndAllScope` | **FIXED** | **PASS** |
