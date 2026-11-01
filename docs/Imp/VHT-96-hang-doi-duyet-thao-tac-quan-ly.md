---
plan:
  id: VHT-96
  name: "Hàng đợi duyệt thao tác Quản lý — service + migration + test"
  level: L
  size: { files: 16, lines_est: 780 }
  complexity: [schema-migration, concurrency]
  parent: null
  status: completed
  approval_status: approved
  created_at: 2026-11-01
  updated_at: 2026-11-01
  last_checkpoint_at: 2026-11-01 11:30
  version: 4
---

> ## ▶ TIẾP THEO
> - **Task**: tất cả `done` — plan đóng
> - **Đã xong**: service + migration + 9 test tích hợp + 11 unit test
> - **Bước kế tiếp**: ngoài plan — nối call site P2/P3 (RoomService/TenantService), applier P4/P5 (Đào), UI (VHT-97)
> - **Lệnh cần chạy**: không có
> - **Cập nhật lúc**: 2026-11-01 11:30

## 1. Mục tiêu & vấn đề
- Mục tiêu: thao tác ở chế độ Duyệt tạo yêu cầu Chờ duyệt; chỉ Chủ trọ duyệt mới có hiệu lực; không duyệt trùng.
- Vấn đề: chưa có bảng/hàng đợi. `ManagerToggleMode.cs:12-13` ghi rõ "hàng đợi NEW-03 (VHT-96)"; `RoomService.cs:177-181` + `TenantService.cs:897-909` hiện chỉ trả lỗi — call site do người khác nối.
- Xong khi: 5 tiêu chí mục 7 đạt trên SQL Server thật; `dotnet build` 0 error. **Đạt.**

## 2. Phạm vi
- Sửa: `Features/Approvals/**` (mới), `Data/Configurations/Approvals/**` (mới), 1 migration, `ApplicationDbContext`, `Program.cs`, 2 file test + fixture test.
- Không sửa: `RoomService`, `TenantService`, `RoomResult`, `TenantResult`, `Room`/`Tenant` entity, UI (VHT-97), công tắc VHT-94, applier nghiệp vụ thật (P4/P5 do Đào nối).
- Ảnh hưởng gián tiếp: bảng mới `ManagerApprovalRequests` (FK AspNetUsers, Properties); không đổi schema bảng khác.
- Phụ thuộc: VHT-94 (`ManagerToggleCatalog.IsKnown`), VHT-30 (mẫu transaction/UPDLOCK).
- Rủi ro còn lại: production chưa đăng ký applier nào → duyệt mã SW nào cũng trả `ApplierNotRegistered` cho tới khi Đào nối.

## 3. File liên quan & thay đổi
| File | Hành động | Ghi chú |
|---|---|---|
| `Features/Approvals/ApprovalCaller.cs` | TẠO | record caller: `CanApprove` = Chủ trọ, `CanRequestApproval` = Quản lý |
| `Features/Approvals/Enums/ApprovalEnums.cs` | TẠO | `ApprovalRequestStatus` (0–3), `ApprovalTargetType` |
| `Features/Approvals/Entities/ManagerApprovalRequest.cs` | TẠO | payload JSON, `ExpectedToken`, `Status`, người gửi/duyệt, lý do, `RowVersion` |
| `Features/Approvals/DTOs/ApprovalDtos.cs` | TẠO | `CreateApprovalRequest`, `ApprovalRequestDto`, `RejectApprovalRequest`, `ApprovalResult`, `ApprovalListResult` |
| `Features/Approvals/ApprovalErrorCodes.cs` | TẠO | `ApprovalErrorCodes` + `ApprovalErrorMessages` + `ApprovalApplyException` |
| `Features/Approvals/Rules/ApprovalRequestRules.cs` | TẠO | validate payload JSON/khóa, lý do từ chối, điều kiện chuyển trạng thái |
| `Features/Approvals/Services/IApprovalApplier.cs` | TẠO | điểm cắm theo mã SW, chạy trong transaction của service |
| `Features/Approvals/Services/IManagerApprovalService.cs` | TẠO | create/list/inbox/cancel/approve/reject |
| `Features/Approvals/Services/ManagerApprovalService.cs` | TẠO | transaction + `UPDLOCK,HOLDLOCK` + map `DbUpdateConcurrencyException` → `Conflict` |
| `Data/Configurations/Approvals/ManagerApprovalRequestConfiguration.cs` | TẠO | unique filtered index `UX_ManagerApprovalRequests_Pending` + check constraint |
| `Data/Migrations/20261101110944_AddManagerApprovalQueue.cs` (+ Designer) | TẠO | chỉ bảng mới |
| `Data/Migrations/ApplicationDbContextModelSnapshot.cs` | SỬA | +97 dòng (chèn thủ công, không regenerate để tránh đổi toàn file) |
| `Data/ApplicationDbContext.cs` | SỬA | `DbSet<ManagerApprovalRequest>` |
| `Web/Program.cs` | SỬA | DI service + chú thích chưa có applier (fail closed) |
| `NhaTro.IntegrationTests/Infrastructure/SqlServerFixture.cs` | SỬA | xóa `ManagerApprovalRequests` trước Properties/Users trong `ResetAsync` |
| `NhaTro.IntegrationTests/Features/Approvals/ManagerApprovalServiceIntegrationTests.cs` | TẠO | 9 test `[SqlServerFact]` |
| `NhaTro.UnitTests/Features/Approvals/ApprovalRequestRulesTests.cs` | TẠO | 11 test luật thuần |

## 4. Quyết định & câu hỏi
| Câu hỏi | Trả lời | Ảnh hưởng |
|---|---|---|
| Báo lỗi kiểu gì? | result wrapper `ApprovalResult`/`ApprovalListResult` (không throw) | call site P4/P5 không cần try/catch |
| Áp dụng thao tác? | `IApprovalApplier` theo mã SW; chưa đăng ký → `ApplierNotRegistered` | Đào nối chỉ cần thêm 1 class |
| Concurrency token? | caller truyền `ExpectedToken`; applier gán `OriginalValues["RowVersion"]` | lệch ⇒ `Conflict`, không ghi |
| Chống duyệt trùng? | unique filtered index `WHERE Status = 0` + `UPDLOCK,HOLDLOCK` khi quyết định | 2 lớp |
| Yêu cầu chưa gắn tòa hiện cho ai? | Chủ trọ có phân công hiệu lực với Quản lý gửi (`PropertyManagers.RevokedAt == null`) | không cần cột mới |
| Nơi lưu plan? | `docs/Imp/` theo quy ước repo | khác mặc định skill |

## 5. Luật riêng
- Không đụng `RoomService`/`TenantService`/`RoomResult`/`TenantResult`.
- Không dùng InMemory; test tích hợp `[SqlServerFact]`, tự skip khi thiếu LocalDB.
- Duyệt/từ chối: chỉ Chủ trọ. Hủy: chỉ người tạo + `Status=Pending`. Quản lý chỉ thấy yêu cầu của mình.
- Từ chối bắt buộc lý do, tối đa 500 ký tự.
- Bảng là lịch sử thao tác: dòng đã quyết định không sửa lại.

## 6. Công việc
| Task | Nội dung | Trạng thái |
|---|---|---|
| TASK-01 | Entity + enum + caller + DTO + result + mã lỗi | done |
| TASK-02 | Cấu hình EF + DbSet + migration | done |
| TASK-03 | Service lõi (create/list/cancel/approve/reject + registry) | done |
| TASK-04 | DI trong `Program.cs` | done |
| TASK-05 | Test duyệt/từ chối/hủy/trùng/đồng thời/phạm vi | done |

## 7. Kiểm thử & xác minh
- Máy đã tự chạy (kết quả thật):
  - `dotnet build NhaTro/NhaTro.slnx` → 0 error; 1 warning **có sẵn** ở `IncidentDemoSessionTests.cs:99`.
  - `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model".
  - `dotnet test NhaTro.UnitTests --filter ~Approvals` → 11/11 pass.
  - `dotnet test NhaTro.IntegrationTests --filter ~ManagerApprovalService` → 9/9 pass trên LocalDB thật.
  - Hồi quy: `~ManagerToggleServiceIntegrationTests|~TenantServiceIntegrationTests` → 26/26 pass (fixture đổi `ResetAsync`).
- Tiêu chí → test: duyệt 2 lần (`Duyet_hai_lan_chi_ap_dung_mot_lan`) · dữ liệu đã đổi (`Duyet_khi_du_lieu_da_doi_bi_chan`) · từ chối (`Tu_choi_khong_doi_du_lieu_va_bat_buoc_ly_do`) · Quản lý bị chặn (`Quan_ly_khong_duyet_duoc_yeu_cau_cua_minh`) · song song (`Hai_luong_duyet_song_song_chi_ap_dung_mot_lan`).
- Người dùng test tay: không bắt buộc — đã chạy trên SQL Server thật. Muốn tự kiểm: `dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter FullyQualifiedName~ManagerApprovalService`.
- Không chạy: E2E/UI (VHT-97 chưa có), publish IIS.

## 8. Tiến độ
- Đang làm: –
- Đã xong: TASK-01…05
- Tiếp theo: ngoài plan — call site + applier + UI
- Bị chặn: –
- Lệch kế hoạch + lý do: thêm `Rules/ApprovalRequestRules.cs` và sửa `SqlServerFixture.ResetAsync` (FK mới) → 16 file thay vì 15, vẫn trong ngưỡng L
- File đã sửa: 4 file (DbContext, Program, snapshot, fixture) + 13 file mới
- Kiểm tra đã chạy: build, ef has-pending-model-changes, 11 unit + 9 tích hợp + 26 hồi quy
- Tồn đọng: applier SW-01…SW-16 thật; nối `RequiresApproval` ở P2/P3; inbox UI VHT-97

## 9. Nhật ký (append-only)
| lúc | do | task | đã làm | kết quả | bước kế tiếp |
|---|---|---|---|---|---|
| 2026-11-01 00:00 | agent | – | plan v1 (XL, quá phạm vi) → v2 → v3 (đối chiếu code) | duyệt | TASK-01 |
| 2026-11-01 11:00 | agent | 01–02 | entity/enum/caller/DTO/lỗi/rules + config + migration | đạt | TASK-03 |
| 2026-11-01 11:20 | agent | 03–04 | service + registry + DI | đạt | TASK-05 |
| 2026-11-01 11:30 | agent | 05 | 9 tích hợp + 11 unit; snapshot chèn +97 dòng thay vì regenerate (tránh đổi toàn file) | đạt | đóng plan |
