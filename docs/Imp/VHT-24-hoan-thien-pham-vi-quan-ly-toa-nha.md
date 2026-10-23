# VHT-24 — Hoàn thiện phạm vi quản lý/tòa nhà P1–P3

**Nhánh:** `VHT-24-hoan-thien-pham-vi-quan-ly-toa-nha-p-1-p-3` (tạo từ `dev`)
**Nguồn:** `docs/task/cong_viec_theo_tuan.md` (VHT-24), `docs/task/timeline_song_song.md` (nhịp 2A)
**Phạm vi 2A:** backend/phạm vi dữ liệu (phân công + lọc query/update). UI/menu ghép sau với Phúc.

---

## 0. Mục tiêu

Quản lý chỉ thấy/thao tác trên **tòa nhà được Chủ trọ phân công**; chặn ở tầng dữ liệu
(không chỉ ẩn nút, gõ URL trực tiếp cũng không xem/sửa được). Chủ trọ vẫn thấy toàn bộ
nhà của mình. Admin/Khách thuê vẫn bị chặn nghiệp vụ.

---

## 1. Quyết định đã chốt

| # | Vấn đề | Quyết định |
|---|---|---|
| 1 | Quan hệ phân công | Nhiều-nhiều (1 tòa ↔ nhiều Quản lý; 1 Quản lý ↔ nhiều tòa) |
| 2 | Ai được gán/bỏ gán | Chỉ Chủ trọ sở hữu tòa nhà |
| 3 | Quản lý tạo nhà/phòng | Không (theo ma trận `thao_tac_va_quyen.xlsx`) |
| 4 | Bỏ gán/khóa | Giữ lịch sử, chặn truy cập ngay |

---

## 2. Thiết kế dữ liệu (SQL)

**Bảng mới: `PropertyManagers`** — lịch sử phân công Quản lý theo tòa nhà.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | int identity | PK |
| `PropertyId` | int | FK → `Properties(Id)`, Restrict |
| `ManagerUserId` | nvarchar(450) | FK → `AspNetUsers(Id)`, Restrict |
| `AssignedAt` | datetime2 | thời điểm gán |
| `AssignedByUserId` | nvarchar(450) | FK → `AspNetUsers(Id)`, Restrict (người gán) |
| `RevokedAt` | datetime2 NULL | `NULL` = đang hiệu lực |
| `RevokedByUserId` | nvarchar(450) NULL | FK → `AspNetUsers(Id)`, Restrict |

**Index:**

- Unique filtered `(PropertyId, ManagerUserId) WHERE RevokedAt IS NULL` → chặn trùng phân công đang hiệu lực.
- Index thường `ManagerUserId` (tra theo Quản lý).
- Index thường `PropertyId`.

> Truy vấn phạm vi luôn lọc `RevokedAt IS NULL` → bỏ gán mất quyền ngay; dòng cũ vẫn còn để tra lịch sử.

---

## 3. File mới

| File | Nội dung |
|---|---|
| `NhaTro/Web/Features/Properties/Entities/PropertyManager.cs` | Entity phân công |
| `NhaTro/Web/Data/Configurations/Properties/PropertyManagerConfiguration.cs` | EF config (index, FK, độ dài) |
| `NhaTro/Web/Features/Properties/PropertyManagerErrorCodes.cs` | Mã + thông báo lỗi tiếng Việt |
| `NhaTro/Web/Features/Properties/DTOs/PropertyManagerItem.cs` | DTO + `PropertyManagerResult` / `PropertyManagerListResult` |
| `NhaTro/Web/Features/Properties/IPropertyManagerService.cs` | Interface `ListAssignmentsAsync` / `AssignAsync` / `RevokeAsync` |
| `NhaTro/Web/Features/Properties/Services/PropertyManagerService.cs` | Cài đặt, kiểm quyền Chủ trọ sở hữu + target phải là Quản lý |

---

## 4. File thay đổi

| File | Thay đổi |
|---|---|
| `NhaTro/Web/Data/ApplicationDbContext.cs` | Thêm `DbSet<PropertyManager> PropertyManagers` |
| `NhaTro/Web/Features/Properties/PropertyScope.cs` | `Apply(properties, assignments, caller)` — Quản lý lọc theo phân công đang hiệu lực |
| `NhaTro/Web/Features/Rooms/RoomScope.cs` | Thêm tham số `assignments`; Quản lý chỉ thấy phòng thuộc tòa được gán |
| `NhaTro/Web/Features/Properties/PropertyService.cs` | `ScopeQuery` truyền `db.PropertyManagers` |
| `NhaTro/Web/Features/Rooms/RoomService.cs` | `ScopeRooms` + `PropertyInScopeAsync` truyền `db.PropertyManagers` |
| `NhaTro/Web/Program.cs` | Đăng ký `IPropertyManagerService` |
| `NhaTro/Web/Features/Properties/PropertyCaller.cs` | Cập nhật comment (bỏ "tạm xem toàn hệ thống") |
| `NhaTro/NhaTro.IntegrationTests/Infrastructure/SqlServerFixture.cs` | `ResetAsync` xóa `PropertyManagers` trước Properties/Users |
| `NhaTro/NhaTro.IntegrationTests/Infrastructure/TestDataSeeder.cs` | Gán Quản lý mặc định → tòa A |

**Không đụng tới** (WIP việc khác): `AppNavigation.cs`, `AppSidebar.razor`, `Home.razor`, `*.resx`, `PageHeader.razor`, `theme.css`, `docs/Imp/VHT-14-...`.

---

## 5. Migration & Database

- [x] Migration `20261023072615_AddPropertyManagers.cs` + Designer/Snapshot.
- [x] `SqlServerFixture.ResetAsync` xóa `PropertyManagers` trước `Properties`/`Users`.
- Ghi chú: integration test dùng `EnsureCreatedAsync()` nên tự nhận schema mới; migration thật dùng cho DB demo/prod.

---

## 6. Test

### Test mới (VHT-24) — `Quan_ly_chi_thay_va_sua_duoc_toa_nha_duoc_phan_cong`

- [x] Quản lý chưa được gán → danh sách nhà/phòng = 0.
- [x] Gán A → Quản lý chỉ thấy nhà A/phòng A, không thấy B.
- [x] Quản lý không `Get/Update` được nhà/phòng ngoài phạm vi (NotFound, không lộ tồn tại).
- [x] Chủ trọ gán/bỏ gán được cho nhà mình; không gán được nhà người khác.
- [x] Quản lý/Admin không gán được; gán sai vai trò bị `InvalidManagerRole`.
- [x] Bỏ gán → mất quyền ngay; dòng lịch sử vẫn còn.
- [x] Gán trùng khi đang hiệu lực → `AlreadyAssigned`.

### Tác động test cũ (phải sửa)

| File | Vấn đề |
|---|---|
| `P1P3RoleAccessTests.cs` | ~117–119 kỳ vọng Quản lý thấy 2 nhà/2 phòng → đổi theo phân công |
| `TestDataSeeder.cs` | Thêm phân công để test công tắc cũ còn đúng |
| `TestReadinessIntegrationTests.cs` | 3 test `CongTac_*` Quản lý sửa RoomA (dựa seeder) |
| `RoomPropertyServiceIntegrationTests.cs` | 2 test `Quan_ly_...` tạo property trong test → thêm phân công |
| `SqlServerFixture.ResetAsync` | Thêm xóa `PropertyManagers` |
| `GuardTests.cs` | Chỉ dùng `EnsureOwnerCanAccess` → không ảnh hưởng |

---

## 7. Phần CHƯA làm trong 2A

- **Menu/UI theo phạm vi** (màn gán Quản lý, menu ẩn khi chưa có tòa) → ghép sau với Phúc.
- **Phạm vi Khách thuê (P3)** → chờ liên kết hợp đồng ở **VHT-25**; VHT-24 không tự thêm cột `PropertyId` vào Tenant.
- **Hàng đợi duyệt** cho thao tác Quản lý → VHT-96.

---

## 8. Checklist tiến độ

- [x] Tạo nhánh từ `dev`
- [x] Chốt 4 quyết định thiết kế
- [x] Rà code hiện trạng + migration/config/test
- [x] Tạo entity + config + DbSet
- [x] Viết service phân công + error codes + DTO
- [x] Sửa `PropertyScope` + `RoomScope` + 2 service + đăng ký DI
- [x] Tạo migration `AddPropertyManagers`
- [x] Cập nhật `ResetAsync` + `TestDataSeeder`
- [x] Viết test mới + sửa test cũ bị ảnh hưởng
- [x] Build 0 warning/0 error
- [x] Chạy integration + unit test
- [x] Cập nhật tài liệu VHT-22 (gỡ blocker phạm vi Quản lý)

---

## 9. Kết quả

**Build:** thành công, 0 warning / 0 error.

| Bộ test | Kết quả |
|---|---|
| `NhaTro.UnitTests` | 341 pass / 0 fail / 0 skip |
| `NhaTro.IntegrationTests` | 64 pass / 0 fail / 0 skip |
| Lọc ảnh hưởng (`P1P3RoleAccessTests`, `TestReadinessIntegrationTests`, `RoomPropertyServiceIntegrationTests`) | 33 pass / 0 fail |
| `NhaTro.E2ETests` | 3 pass / 0 fail / 9 skip (skip do thiếu môi trường chạy app, không phải lỗi) |

**Điểm kiểm chứng chính** (test `Quan_ly_chi_thay_va_sua_duoc_toa_nha_duoc_phan_cong`):

- Quản lý chưa gán → danh sách nhà/phòng `= 0`.
- Chủ trọ B không gán được Quản lý vào nhà của Chủ trọ A → `PropertyNotFound`.
- Quản lý tự gán / Admin gán → `Forbidden`.
- Gán sai vai trò (không phải Quản lý) → `InvalidManagerRole`.
- Gán A → Quản lý chỉ thấy nhà A/phòng A; `Get`/`Update` nhà B/phòng B → `NotFound`.
- Gán trùng khi đang hiệu lực → `AlreadyAssigned`.
- Bỏ gán → mất quyền ngay (danh sách `= 0`), dòng cũ vẫn còn khi `includeHistory: true`.

**Tài liệu liên quan đã cập nhật:** `docs/Imp/VHT-22-kiem-thu-p-1-p-3-va-ghi-tai-lieu.md` — case **P2-03 PASS**, **B-22-03** đánh dấu phần P2 đã sửa; phần P3 (khách thuê) chờ **VHT-25**.

**Không đụng WIP việc khác:** không sửa các file đang dở ở working tree. Chưa commit/push (chờ xác nhận).
