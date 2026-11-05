# VHT-101 — Biên bản bàn giao Sprint 2E
## KIỂM THỬ MA TRẬN QUYỀN TỰ ĐỘNG TỪ EXCEL (45 THAO TÁC × 4 VAI TRÒ = 180 Ô)

- **Mã task:** VHT-101
- **Tên task:** Kiểm thử ma trận quyền tự động
- **Sprint:** Sprint 2E – Đợt 2E
- **Người thực hiện:** Phúc (UI & Automation)
- **Phối hợp & Giám sát:**
  - **Phát:** Điều phối kiểm thử (Test Coordinator)
  - **Tính:** Cung cấp tài khoản mẫu, User và Policy
- **Dependencies:** NEW-01, NEW-03, VHT-4, VHT-14
- **Trạng thái bàn giao:** **READY FOR REVIEW — 173/173 ô khả dụng PASS, 0 FAIL, 0 BLOCKED.**
  Toàn bộ ô từng thiếu API/màn hình đã được đóng bằng service + database thật (mục 6.2), không đổi
  kỳ vọng của workbook và không chuyển ô nào thành BLOCKED.
- **Nguồn sự thật:** `docs/task/thao_tac_va_quyen.xlsx` (Sheet: *Ma trận quyền*, *Công tác Quản lý*, *Quyết định*)

---

## 1. Kết luận tổng quan (Executive Summary)

> Số liệu trong mục này lấy từ lần chạy thật gần nhất trên SQL Server LocalDB và từ
> `docs/test-results/permission_matrix_results.json` do chính bộ test sinh ra
> (SHA-256 workbook `48fdd0c2…99a3` được kiểm khi nạp manifest — workbook đổi là test fail ngay).

- **Độ phủ ma trận:** **45 thao tác nghiệp vụ × 4 vai trò = 180 ô**, trong đó **173 ô bắt buộc chứng minh**
  và **7 ô `—`** loại trừ hợp lệ theo đặc tả mục VI.5.
- **Kết quả theo ký hiệu (180 ô):**

  | Ký hiệu | Tổng ô | PASS | BLOCKED | FAIL |
  |---|---:|---:|---:|---:|
  | `✅` | 67 | 67 | 0 | 0 |
  | `❌` | 76 | 76 | 0 | 0 |
  | `⚠️` | 14 | 14 | 0 | 0 |
  | `⚙️` | 16 | 16 | 0 | 0 |
  | `—` | 7 | — | — | — (không áp dụng) |
  | **Tổng** | **180** | **173** | **0** | **0** |

- **Ô thực thi được:** **173/173**. Tỷ lệ PASS trên ô đã thực thi: **100 %**.
- **Không còn ô BLOCKED:** danh sách `PermissionMatrixProbeExecutor.UnsupportedCells` rỗng. Hai guard
  `No_Cell_Is_Declared_Unsupported` và `No_Forbidden_Cell_Is_Declared_Unsupported` giữ CI không cho
  ai thêm lại ô BLOCKED mà không kèm bằng chứng.
- **Mọi ô `❌` (76/76) đều ghi mã lỗi quyền:** 75 ô `Forbidden`, 1 ô `RoleNotAllowed`. Không có ô ❌ nào
  PASS nhờ lỗi nghiệp vụ tình cờ (validation, trùng dữ liệu, lỗi DB).
- **Tầng kiểm tra:** 180/180 ô ở tầng **Service** — gọi service nghiệp vụ thật trên database test riêng
  và đọc lại DB để xác nhận hiệu lực ghi, không dùng Policy/SampleState để "tính là đã chứng minh".
- **Số test case:** **364** test của bộ PermissionMatrix, gồm:
  - **191** test harness: 173 ô thực thi + 7 ô `—` + 11 guard/collector;
  - **173** test per-ô của 10 file phân hệ (mỗi ô đúng một method).
  Kết quả chạy: **364 PASS / 0 FAIL / 0 SKIPPED**.
- **Hồi quy toàn solution:** Build 0 error; Unit **795 PASS**; Integration **661 PASS**;
  E2E **3 PASS / 13 SKIPPED** (skip vì chưa đặt `E2E_BASE_URL`/`E2E_PASSWORD` và app chưa chạy — không phải fail).
- **Bảo toàn dữ liệu:** chạy hoàn toàn trên **SQL Server LocalDB** với DB riêng cho từng class test
  (`NhaTro_It_<guid>`), `ResetAsync()` dọn theo thứ tự khóa ngoại; không đụng production DB,
  không dùng tài khoản thật, không có credential thật trong log/báo cáo.

---

## 2. Bằng chứng kiểm thử thực tế (Test Execution Evidence)

### 2.1. Lệnh kiểm thử ma trận quyền VHT-101
```powershell
# 364 test case: 191 harness (173 ô thực thi + 7 ô "—" + 11 guard/collector) + 173 test per-ô phân hệ
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter "FullyQualifiedName~PermissionMatrix"
```
**Kết quả đo được:**
- **Passed: 364**
- **Failed: 0**
- **Skipped: 0** (`SqlServerFact`/`SqlServerTheory` chỉ skip khi LocalDB vắng; ở đây LocalDB có thật nên mọi test đều chạy)
- **Thời gian chạy:** ~40 giây cho bộ lọc PermissionMatrix trong lần chạy gần nhất (máy phát triển).

Trong đó (đếm từ `dotnet test --list-tests`, không suy đoán):
- `173 test` `Cell_Satisfies_Matrix_Requirement` — mỗi test một ô **có backend thật để gọi**, gọi Service thật.
- `7 test` `Cell_Not_Applicable_Is_Excluded_From_Execution` — chứng minh 7 ô `—` bị loại khỏi execution đúng đặc tả.
- `11 test` guard/collector:
  - `No_Cell_Is_Declared_Unsupported`, `No_Forbidden_Cell_Is_Declared_Unsupported` — CI gate chống khai báo BLOCKED;
  - `Manifest_Integrity_Counts_Match_Loaded_Cells`, `Workbook_Hash_In_Manifest_Matches_Current_File`;
  - `Every_Cell_Has_Unique_Traceable_Test_Id`, `Legend_Symbols_Map_To_Expected_Permission`,
    `Special_Mode_Cells_Map_One_To_One_To_Manager_Switches`;
  - `Declared_Unsupported_Entries_Are_Traceable_And_Partition_Applicable_Cells`,
    `Theory_Data_Sources_Cover_Every_Cell_Exactly_Once`,
    `Module_Tests_Are_One_Per_Cell_And_Named_By_Cell_Address` (guard này còn chặn ô chưa có test phân hệ);
  - `Generate_Full_Permission_Matrix_Report` — sinh 3 báo cáo Markdown/JSON/CSV.
- `173 test` per-ô theo phân hệ (10 file):
  Accounts 32 · Rooms 24 · Contracts 24 · Invoices 21 · Tenants 20 · Utilities 20 · Maintenance 16 ·
  Reports 8 · Approvals 4 · Settings 4.
  **Mỗi ô đúng một method** đặt tên `Cell_<ô>_OP<nn>_<Role>_<IsDenied|IsAllowed|IsDeniedNoMutation|IsAllowedInScope|IsDeniedOutOfScope>`;
  ô `⚙️`/`⚠️` được ghi rõ trong header là do harness chứng minh.
  Mọi ô `❌` đều **đọc lại DB** để chứng minh bị từ chối mà không có side effect.
  **Không còn stub/policy tự chế** trong test phân hệ: dùng `ManagerTogglePolicy` đọc công tắc từ SQL
  và `ManagerApprovalService` thật (`PermissionMatrixWorld.NewRealTogglePolicy` / `NewRealApprovals` / `NewRealIncidentPhotos`).

### 2.2. Kiểm thử toàn bộ Solution (Regression Testing)
```powershell
# 1. Build toàn bộ Solution
dotnet build NhaTro/NhaTro.slnx --nologo

# 2. Chạy toàn bộ test
dotnet test NhaTro/NhaTro.slnx --no-build --nologo
```
**Kết quả đo được trong đợt này:**
- **Build:** `0 errors, 1 warning` — warning còn lại là `xUnit2031` tại
  `NhaTro/NhaTro.UnitTests/Features/Maintenance/IncidentDemoSessionTests.cs:99`, có từ trước và
  **không thuộc** các tệp VHT-101.
- **Unit Tests:** **795 passed, 0 failed, 0 skipped**
- **Integration Tests:** **661 passed, 0 failed, 0 skipped**
- **E2E Tests (`NhaTro.E2ETests`):** **3 passed, 13 skipped, 0 failed**. Các test skip vì thiếu
  `E2E_BASE_URL`/`E2E_PASSWORD` hoặc app chưa chạy ở môi trường hiện tại.
- **Migration:** `dotnet ef migrations has-pending-model-changes` trả
  *"No changes have been made to the model since the last migration."*
- **Kết luận hồi quy:** không phát sinh hồi quy — toàn bộ Unit + Integration đều xanh sau khi bổ sung
  service/database cho các ô từng thiếu API.

---

## 3. Kiến trúc bộ kiểm thử ma trận quyền tự động

Bộ kiểm thử được thiết kế theo mô hình **Data-Driven Testing từ Excel** với khả năng truy vết (Traceability) 100%:

```
docs/task/thao_tac_va_quyen.xlsx (Nguồn sự thật duy nhất)
   │
   ▼
scripts/permission_matrix/build_manifest.py (Sinh manifest OOXML thuần, không phụ thuộc)
   │
   ▼
docs/test-results/permission_matrix_manifest.json (180 cell definitions + SHA-256 hash)
   │
   ▼
PermissionMatrixManifest.cs (Typed C# parser + runtime hash check chống lệch Excel)
   │
   ▼
PermissionMatrixWorld.cs (Deterministic fixture: 2 nhà, 6 tài khoản, hợp đồng, chỉ số, sự cố, hóa đơn, 16 switches)
   │
   ▼
PermissionMatrixProbeExecutor.cs (Gọi trực tiếp Service backend thật cho OP01..OP45)
   │
   ▼
PermissionMatrixAutomatedTests.cs (180 Theory cases: 173 Applicable + 7 Not Applicable + 11 guard/collector)
   │
   ├─► docs/test-results/VHT-101-permission-matrix-report.md (Báo cáo Markdown chi tiết mục A-H)
   ├─► docs/test-results/permission_matrix_results.json (Báo cáo JSON cho CI pipeline)
   └─► docs/test-results/permission_matrix_results.csv (Báo cáo CSV cho CI thống kê)
```

Các test phân hệ dùng chung `PermissionMatrixModuleProbe.AssertCellAsync` để chạy đúng probe hành vi
của engine với một `PermissionMatrixWorld` mới cho mỗi test. Nhờ vậy mỗi ô có đúng một method test
mang tên ô Excel, nhưng không có bản sao logic probe và không nới lỏng kỳ vọng.

---

## 4. Danh mục tệp bàn giao (Deliverables Checklist)

| STT | Đường dẫn tệp | Mô tả nội dung |
|:---:|---|---|
| 1 | `NhaTro/NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixManifest.cs` | Trình đọc manifest JSON, kiểm tra tính toàn vẹn và ánh xạ tọa độ ô Excel (D2..G46) |
| 2 | `NhaTro/NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixOutcome.cs` | Chuẩn hóa kết quả probe (`Allowed`, `Denied`, `PendingApproval`, `Blocked`, `Error`), mã lỗi và model `CellResult` |
| 3 | `NhaTro/NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixWorld.cs` | Fixture chuẩn hóa dữ liệu: 2 nhà trọ (nhà A, nhà B), 6 tài khoản mẫu, phòng, hợp đồng, chỉ số, hóa đơn, công tắc |
| 4 | `NhaTro/NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixProbeExecutor.cs` | Thực thi probe nghiệp vụ thực tế cho 45 thao tác qua Service backend; phân loại Denied/Error nghiêm ngặt |
| 5 | `NhaTro/NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixAutomatedTests.cs` | 180 Theory cases + 11 guard/collector + sinh báo cáo Markdown/JSON/CSV |
| 6 | `NhaTro/NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixModuleProbe.cs` | Cầu nối dùng chung cho test per-ô: chạy đúng engine probe, không tự bịa kết quả |
| 7 | `.../PermissionMatrixAccountsTests.cs` | Test per-ô phân hệ Tài khoản & Phân quyền (32 ô) |
| 8 | `.../PermissionMatrixApprovalsTests.cs` | Test per-ô phân hệ Hàng đợi phê duyệt (4 ô) |
| 9 | `.../PermissionMatrixRoomsTests.cs` | Test per-ô phân hệ Phòng & Thiết bị (24 ô) |
| 10 | `.../PermissionMatrixTenantsTests.cs` | Test per-ô phân hệ Khách thuê & Giấy tờ (20 ô) |
| 11 | `.../PermissionMatrixContractsTests.cs` | Test per-ô phân hệ Hợp đồng & Cọc (24 ô) |
| 12 | `.../PermissionMatrixUtilitiesTests.cs` | Test per-ô phân hệ Dịch vụ & Chỉ số điện nước (20 ô) |
| 13 | `.../PermissionMatrixInvoicesTests.cs` | Test per-ô phân hệ Hóa đơn & Thanh toán (21 ô) |
| 14 | `.../PermissionMatrixMaintenanceTests.cs` | Test per-ô phân hệ Báo hỏng & Sự cố (16 ô) |
| 15 | `.../PermissionMatrixReportsTests.cs` | Test per-ô phân hệ Báo cáo & Xuất Excel (8 ô) |
| 16 | `.../PermissionMatrixSettingsTests.cs` | Test per-ô phân hệ Cấu hình công tắc Quản lý (4 ô) |
| 17 | `scripts/permission_matrix/build_manifest.py` | Script chuyển Excel OOXML sang manifest JSON — **bị `.gitignore` dòng `scripts/*` che**, cần `git add -f` nếu muốn bàn giao kèm |
| 18 | `docs/test-results/permission_matrix_manifest.json` | Manifest 180 ô ma trận kèm hash, sheet, cell address |
| 19 | `docs/test-results/permission_matrix_results.json` | Báo cáo máy đọc được định dạng JSON (UTF-8, không BOM) |
| 20 | `docs/test-results/permission_matrix_results.csv` | Báo cáo máy đọc được định dạng CSV (UTF-8, không BOM) |
| 21 | `docs/test-results/VHT-101-permission-matrix-report.md` | Báo cáo Markdown tự sinh: A. Workbook, B. Coverage, C. Bảng 180 ô, D. FAIL, E. BLOCKED, F–H. Legend/Quyết định/Hạn chế |
| 22 | `docs/Imp/VHT-101-ban-giao-kiem-thu-ma-tran-quyen-tu-dong.md` | Tài liệu biên bản bàn giao chính thức Task VHT-101 (file này) |

### 4.1. Service/entity production được bổ sung để đóng các ô từng thiếu API

| Thao tác | Tệp chính | Nội dung |
|---|---|---|
| OP10 — Nhật ký thao tác (D16) | `Web/Features/Audit/{Entities,DTOs,Services}` | `AuditLogEntry`, `AuditLogService`: Admin chỉ đọc sổ tài khoản/hệ thống; Chủ trọ chỉ đọc sổ nghiệp vụ trong nhà mình; Quản lý/Khách bị `Forbidden`; truy vấn ngoài phạm vi bị `OutsideScope`. Entity chỉ có actor/action/property/thời điểm/metadata; không có trường mật khẩu hay token. |
| OP27 — Cập nhật tiền cọc | `Web/Features/Contracts/Services/ContractService.cs`, `ContractApprovalApplier.cs` | `UpdateDepositAsync` + cổng SW-08; chế độ Duyệt ghi yêu cầu chờ, applier cập nhật cọc khi Chủ trọ duyệt. |
| OP28–OP32 — Chỉ số điện nước (P5) | `Web/Features/Utilities/{Entities,DTOs,Services}` | `UtilityReading`/`UtilityReadingAudit` + `UtilityReadingService`: xem, nhập, sửa kỳ đã chốt (D10, bắt buộc lý do), xem lịch sử; SW-09; `UtilityReadingApprovalApplier` cho chế độ Duyệt. |
| OP37–OP38 — Thanh toán (P6) | `Web/Features/Payments/{Entities,Services}` | `Payment` + `PaymentService`: khách gửi minh chứng, Chủ trọ/Quản lý trong phạm vi xác nhận hoặc từ chối; kiểm tra hóa đơn/hợp đồng/phòng/nhà. |
| OP39 — Doanh thu/công nợ (P8) | `Web/Features/Reports/Services/RevenueReportService.cs` | Báo cáo tổng hợp theo phạm vi Chủ trọ/Quản lý; Admin/Khách bị chặn. |
| OP45 — Xuất Excel (D8) | `Web/Features/Reports/Services/ReportExportService.cs`, `ReportExportApprovalApplier.cs` | Xuất `.xlsx` thật bằng ClosedXML + cổng SW-16; tệp có chữ ký ZIP `PK\x03\x04`. |

Các thay đổi hạ tầng đi kèm: `ApplicationDbContext` (DbSet + cấu hình precision/quan hệ), migration
`20261105044209_AddAuditUtilityPayment`, đăng ký DI trong `Program.cs`, và thứ tự `ResetAsync` trong
`SqlServerFixture`.

---

## 5. Bảng tổng hợp kết quả 180 ô ma trận

Số liệu lấy trực tiếp từ `docs/test-results/permission_matrix_results.json` (do collector trong
`PermissionMatrixAutomatedTests` sinh ra ở lần chạy gần nhất). Đẳng thức:
`PASS 173 + FAIL 0 + BLOCKED 0 + không áp dụng 7 = 180`.

| Ký hiệu | Ý nghĩa | Tổng ô | PASS | BLOCKED | FAIL | Ghi chú kiểm thử |
|:---:|---|:---:|:---:|:---:|:---:|---|
| `✅` | Được phép | 67 | 67 | 0 | 0 | Chạy thật và đọc lại DB xác nhận hiệu lực ghi trong phạm vi |
| `❌` | Cấm tuyệt đối | 76 | 76 | 0 | 0 | Bị từ chối ở tầng Service, DB không đổi, **có mã lỗi** (75 `Forbidden`, 1 `RoleNotAllowed`) |
| `⚠️` | Được phép theo phạm vi | 14 | 14 | 0 | 0 | Case `A-*` trong phạm vi phải được phép; case `B-*` chéo nhà B phải bị chặn (`IN_SCOPE_BLOCKED`/`AUTHZ_LEAK`/`SCOPE_CASES_MISSING` nếu sai) |
| `⚙️` | Công tắc Quản lý đặc biệt | 16 | 16 | 0 | 0 | Dò đủ số chế độ workbook quy định (3 chế độ, riêng nhóm D12 và "Xem báo cáo" 2 chế độ); chế độ Duyệt chỉ tính khi có dòng chờ duyệt thật trong DB |
| `—` | Không áp dụng | 7 | — | — | — | 7 ô xác nhận loại trừ hợp lệ theo đặc tả mục VI.5 (`Cell_Not_Applicable_Excluded_From_Execution`): D31, E31, F31, G31, D38, E38, F38 |
| **Tổng** | **Toàn bộ ma trận** | **180** | **173** | **0** | **0** | **Rà soát đủ 180/180 ô; 173 ô gọi backend thật ở tầng Service (100 % ô đã thực thi)** |

---

## 6. Ghi nhận phối hợp & Chuyển giao tiếp theo

### 6.1. Bàn giao CI và phối hợp

- **Bàn giao cho Phát (Điều phối kiểm thử):**
  - Tích hợp lệnh `dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter "FullyQualifiedName~PermissionMatrix"` vào CI test pipeline.
    **Trạng thái hiện tại: 364/364 xanh, không cần allow-list hay continue-on-error.**
  - Khi nghiệp vụ hoặc workbook `thao_tac_va_quyen.xlsx` có cập nhật, chạy lại
    `python scripts/permission_matrix/build_manifest.py` rồi chạy test: guard hash sẽ fail ngay nếu
    manifest chưa được sinh lại.
  - CI hiện chạy `dotnet test NhaTro/NhaTro.slnx`; các guard `No_Cell_Is_Declared_Unsupported` và
    `No_Forbidden_Cell_Is_Declared_Unsupported` là cổng chặn nếu ai đó khai báo BLOCKED thay vì làm test.
- **Bàn giao cho Tính (Tài khoản & Policy):**
  - Bộ fixture tài khoản mẫu và các scoped identity (`Admin`, `ChuTro`, `QuanLy` nhà A, `ChuTro` nhà B,
    `KhachThue` phòng có hợp đồng đã duyệt) trong `PermissionMatrixWorld` dùng Identity/Role thật
    (`SqlServerFixture.SeedAccountAsync`), không mock phân quyền.
- **Lưu ý cho người dùng UI hóa đơn (P6):** từ đợt này, Quản lý lập/phát hành hóa đơn không còn
  "ghi ngay" một cách mặc định. Ở chế độ **Duyệt**, thao tác throw `InvoiceOperationException` với
  `ErrorCode = ApprovalRequired` ("Thao tác đang chờ Chủ trọ duyệt; hóa đơn chưa được ghi.") và bản
  hóa đơn **chưa** vào sổ; màn hình P6 cần hiển thị thông báo này thay vì coi là lỗi hệ thống.

### 6.2. Các ô từng FAIL/BLOCKED đã được đóng bằng service thật

Trước đợt sửa cuối, bộ test ghi nhận 37 ô BLOCKED và 2 ô FAIL. Tất cả đã được xử lý bằng cách bổ sung
nghiệp vụ thật, **không** đổi kỳ vọng workbook, **không** chuyển ô sang BLOCKED:

- **`F36`/`F37` (OP35/OP36 — Quản lý lập/phát hành hóa đơn):** `InvoiceService` trước đây không tham vấn
  `ManagerTogglePolicy` nên cả 3 chế độ SW-10/SW-11 cho cùng một hành vi (`MODE_SEMANTICS_MISMATCH`).
  Bản sửa:
  - `InvoiceService` nhận thêm 2 dependency **optional** (`IManagerTogglePolicy`, `IManagerApprovalService`)
    theo đúng thông lệ của `ContractService`/`IncidentService`; constructor cũ vẫn gọi được.
  - Cổng SW-10 trong `CreateDraftAsync`; cổng SW-11 trong `IssueAsync`. Chủ trọ luôn ghi ngay theo D4;
    Quản lý thiếu policy thì coi là "cần duyệt" — **fail closed**.
  - Chế độ Duyệt tạo `ManagerApprovalRequest` (SW-10 snapshot toàn bộ dòng hóa đơn; SW-11 target
    `ApprovalTargetType.Invoice` + `RowVersion` làm `ExpectedToken`) rồi throw `ApprovalRequired`;
    `InvoiceCreateApprovalApplier`/`InvoiceIssueApprovalApplier` ghi dữ liệu thật khi Chủ trọ duyệt.
  - Chế độ Không thể throw `ApprovalDenied`, không ghi, không tạo yêu cầu.
  - `CreateAndIssueAsync` kiểm công tắc **trước** transaction để yêu cầu chờ duyệt không bị rollback.
- **OP10, OP27, OP28–OP32, OP37–OP39, OP45:** đã bổ sung service/entity/migration thật như bảng 4.1.
  Không còn ô nào dựa vào `UtilitySampleStateService` hay policy tĩnh.
- **Kết quả chứng minh bằng DB, không bằng lời service nói** (`DecideModeVerdict` đọc bảng thật trước-sau
  từng chế độ):
  - `F36` (SW-10): Cho phép → `invoices 0→1`; Duyệt → `0→0` + yêu cầu chờ `0→1`; Không thể → `0→0` + yêu cầu chờ không tăng.
  - `F37` (SW-11): Cho phép → `Draft→Issued`; Duyệt → `Draft→Draft` + yêu cầu chờ `0→1`; Không thể → không đổi.

### 6.3. Hardening của chính bộ test

- **`ProbeVerdict.Error`:** lỗi validation/trùng dữ liệu/lỗi DB **không** còn được tính là "bị từ chối".
  Ô `❌` chỉ PASS khi backend trả tín hiệu phân quyền thật; nếu gặp lỗi nghiệp vụ, ô FAIL ngay.
- **Chỉ mã phân quyền mới là Denied:** `Forbidden`, `Unauthorized`, `AccessDenied`, `NotAllowed`,
  `OutOfScope`, `NotFound` (che dấu dữ liệu ngoài phạm vi), `ApprovalDenied`, các mã khóa/role...;
  mã `Duplicate`/`Invalid`/`Concurrency` bị coi là lỗi phép thử.
- **Mọi phán quyết Denied đều có mã lỗi:** kể cả probe cũ chỉ nêu message; báo cáo không còn ô ❌ PASS
  nào có `errorCode` rỗng.
- **`MatchConditional` kiểm tra hướng** theo tên case: thiếu case → `SCOPE_CASES_MISSING`;
  case A bị chặn → `IN_SCOPE_BLOCKED`; case B lọt → `AUTHZ_LEAK`.
- **`DecideModeVerdict`** trả Denied cho ô ⚙️ không chứng minh được gì; chế độ Duyệt chỉ tính khi có
  dòng chờ duyệt thật trong DB.
- **`Module_Tests_Are_One_Per_Cell_And_Named_By_Cell_Address`** kiểm cả đủ coverage: ô đã thực thi mà
  thiếu method test phân hệ sẽ FAIL, không im lặng bỏ qua.
- **Báo cáo UTF-8 không BOM**, để JSON/CSV đọc được bởi CI và script Python.
- **OP08** ghi thật bằng `ManagerToggleService.SetAsync`, đọc lại bằng `GetSwitchAsync`, khôi phục giá trị
  cũ trong `finally`; không còn "PASS vì policy trả về đúng".

### 6.4. Migration và triển khai

Đợt này thêm entity/bảng mới, **bắt buộc chạy migration trước khi deploy**:

```powershell
dotnet ef database update --project NhaTro/Web --startup-project NhaTro/Web
```

Migration: `NhaTro/Web/Data/Migrations/20261105044209_AddAuditUtilityPayment.cs`
(`AuditLogEntries`, `UtilityReadings`, `UtilityReadingAudits`, `Payments` và các cột/index liên quan).
Integration test dùng `EnsureCreated` trên DB tạm nên không phụ thuộc migration; production dùng
`Migrate()`/pipeline migration như hiện tại.

### 6.5. Còn tồn đọng ngoài phạm vi đợt này

(ghi rõ để không hiểu nhầm là đã xong):
- **UI cho các nghiệp vụ mới chưa được nối:** audit log, nhập/sửa chỉ số bền vững, thanh toán, báo cáo
  doanh thu và xuất Excel mới được chứng minh ở **tầng Service + database**; chưa có trang Razor/UI
  tương ứng. VHT-101 là task kiểm thử tầng nghiệp vụ, không phải task hoàn thiện UI.
- **UI P6 chưa hiển thị thông báo "chờ Chủ trọ duyệt"** cho hai cổng SW-10/SW-11 (mục 6.2).
- **E2E chưa chạy đủ:** 13 test skip vì thiếu `E2E_BASE_URL`/`E2E_PASSWORD` và app chưa chạy; chưa có
  số liệu E2E mới cho các màn hình mới (vì các màn hình đó chưa tồn tại).
- **Chưa có `WebApplicationFactory` cho toàn bộ phân hệ**, nên các ô được chứng minh qua Service thật
  chứ chưa qua HTTP/form thật. Đây là giới hạn đã ghi ở mục H của báo cáo tự sinh.
- **Tệp chưa commit:** toàn bộ thay đổi của đợt này vẫn nằm trong working tree (16 tệp sửa + các tệp
  mới + `docs/test-results/`), chưa được commit theo luật Git của nhánh.
- `scripts/permission_matrix/build_manifest.py` bị `.gitignore` (`scripts/*`) che; muốn bàn giao script
  thì owner phải `git add -f` hoặc chuyển ra khỏi `scripts/`.

### 6.6. Cách tái lập kết quả

```powershell
# 1. Sinh lại manifest nếu workbook thay đổi
python scripts/permission_matrix/build_manifest.py

# 2. Build và chạy toàn bộ test
dotnet build NhaTro/NhaTro.slnx --nologo
dotnet test NhaTro/NhaTro.slnx --no-build --nologo

# 3. Chỉ chạy bộ ma trận quyền (nhanh)
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj `
  --filter "FullyQualifiedName~PermissionMatrix"
```

Báo cáo tự sinh nằm tại `docs/test-results/` và được ghi đè mỗi lần chạy
`Generate_Full_Permission_Matrix_Report`; không sửa tay các tệp này.
