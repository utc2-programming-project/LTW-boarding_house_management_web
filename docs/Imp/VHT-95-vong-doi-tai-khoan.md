---
plan:
  id: VHT-95
  name: "Vòng đời tài khoản: Chờ duyệt / Hoạt động / Khóa"
  status: in_progress
  approval_status: approved
  created_at: 2026-10-31
  updated_at: 2026-10-31
  version: 1
---

# VHT-95 — Vòng đời tài khoản: Chờ duyệt / Hoạt động / Khóa (đợt 2B, P1)

- **Nhánh:** `VHT-95-vong-doi-tai-khoan-cho-duyet-hoat-dong-khoa` (tạo từ `dev` @ `143e8a7`)
- **Nguồn:** `docs/task/cong_viec_theo_tuan.md` (VHT-95), `docs/task/timeline_song_song.md` (2B),
  `docs/task/thao_tac_va_quyen.xlsx` (ô #3, #5, #6, #7), `docs/thiet_ke_he_thong.md` (P1/D1, dòng 27 + 80)
- **Phụ thuộc:** VHT-4, VHT-6, VHT-24 (nhánh riêng, chưa vào `dev`), VHT-25 (đã merge)
- **Bàn giao:** service + UI quản lý tài khoản + test trái quyền

---

## 1. Mục tiêu & vấn đề

### Mục tiêu

1. Tài khoản có đúng 3 trạng thái: **Chờ duyệt / Hoạt động / Khóa**; chuyển trạng thái đúng ma trận.
2. Tạo tài khoản đúng ô #5: Admin tạo mọi vai trò; Chủ trọ tạo Quản lý + Khách thuê (hiệu lực ngay);
   Quản lý tạo Khách thuê trong tòa nhà được giao (Chờ duyệt); Khách thuê không tạo được.
3. Duyệt / khóa / mở khóa đúng ô #6: Admin mọi tài khoản; Chủ trọ Quản lý + Khách thuê trong phạm vi của mình.
4. Chặn đăng nhập khi Chờ duyệt hoặc Khóa; chặn khóa Admin cuối cùng.
5. Không có đăng ký công khai (đã chặn từ VHT-8 — lần này chỉ kiểm chứng).
6. UI danh sách tài khoản theo phạm vi từng vai trò, nút hành động theo quyền, VI/EN.

### Hiện trạng (đã xác minh trong code)

- `AccountService` (VHT-8) chỉ có list (Admin/Chủ trọ), đổi vai trò (Admin), khóa/mở khóa; **chưa có tạo tài khoản**.
- Trạng thái chỉ suy từ `LockoutEnd` (`PermanentLockout` = `DateTimeOffset.MaxValue`, `AccountService.cs:22-23,294-296`).
  Không phân biệt được Chờ duyệt với Khóa.
- `AccountPermissionRules.VisibleRoles(ChuTro)` = `[QuanLy, KhachThue]`; `CanManageAccounts` chỉ Admin/Chủ trọ;
  `AssignableRoles(ChuTro)` = `[QuanLy]` (thiếu Khách thuê so với ô #5).
- `/admin/users` gắn cứng `[Authorize(Roles = Roles.Admin)]` (`AccountManagement.razor:2`), menu cũng chỉ Admin
  (`AppNavigation.cs:11`).
- Chưa có phạm vi Quản lý–tòa nhà trong `dev`: `PropertyScope` còn ghi "Quản lý tạm xem toàn hệ thống".
  Bảng `PropertyManagers` chỉ có trên nhánh `VHT-24-hoan-thien-pham-vi-quan-ly-toa-nha-p-1-p-3`.
- `Tenant` chưa có cột tòa nhà; quan hệ khách → tòa chỉ suy được qua
  `Tenant → ContractMember → Contract → Room → Property` (`TenantService.cs:670-688`).
  `Tenant.UserId` unique có lọc (`TenantConfiguration.cs:36-40`).
- Identity đăng nhập qua `SignInManager.PasswordSignInAsync` (`Login.razor:188`); khóa vĩnh viễn đã bị chặn
  bởi nhánh `IsLockedOut` (`Login.razor:209-214`).
- Key dịch trạng thái đã có sẵn: `AccountStatusPendingApproval/Active/Locked` (VI/EN, `Shared.*.resx`).
- Không còn trang đăng ký công khai (VHT-8).

### Kết quả mong đợi

Tài khoản tạo ra luôn có trạng thái rõ ràng; người không đủ quyền không tạo/duyệt/khóa được; tài khoản
Chờ duyệt/Khóa không đăng nhập được; Chủ trọ/Quản lý chỉ thấy tài khoản trong phạm vi của mình.

### Tiêu chí hoàn thành (kiểm chứng được)

- Ô #3, #5, #6, #7 của `thao_tac_va_quyen.xlsx` đúng cho 4 vai trò (test trái quyền cho từng ô ❌/⚠️).
- Quản lý tạo khách ngoài tòa nhà được giao → bị từ chối ở service (không chỉ ẩn nút).
- Tài khoản Chờ duyệt và Khóa đăng nhập đều thất bại; sau khi duyệt/mở khóa đăng nhập được.
- Không khóa được Admin cuối cùng; không tự khóa mình.
- `dotnet build` 0 error; unit test + integration test liên quan pass.

---

## 2. Phạm vi

### Trong phạm vi (được sửa)

- `Features/Accounts/` (service, rules, DTO, mã lỗi, trạng thái).
- `ApplicationUser` + EF configuration + migration mới.
- Cấu hình Identity / SignInManager trong `Program.cs`.
- Trang `/admin/users` + dialog tạo tài khoản + menu + resource VI/EN.
- Seeder (Admin/Dev/Test) cập nhật `ApprovedAt`.
- Unit test + integration test tài khoản.

### Ngoài phạm vi (không sửa)

- VHT-96/97 (hàng đợi duyệt thao tác Quản lý, UI công tắc/badge mới); VHT-99 (thùng rác tài khoản).
- Nghiệp vụ P2–P8; gán Quản lý–tòa nhà UI (thuộc VHT-24, chỉ dùng service/entity có sẵn sau khi merge).
- Đăng ký công khai, gửi email đặt mật khẩu (chưa có SMTP), đổi mật khẩu lần đầu.
- Không đổi schema ngoài cột `ApprovedAt`.

### Bị ảnh hưởng nhưng không sửa

- `TenantService.LinkAccountAsync` (VHT-25) — giữ nguyên quy tắc "chỉ Chủ trọ gắn/gỡ" cho luồng gắn tay;
  luồng **tạo tài khoản kèm hồ sơ** là ngoại lệ có kiểm soát (ghi rõ ở mục 6).
- `PropertyScope` / `RoomScope` (VHT-24) — chỉ đọc/dùng, không sửa.

### Phụ thuộc

- **VHT-24 phải có trong nhánh này** (bảng `PropertyManagers`) để chặn phạm vi Quản lý.
- VHT-25 (`Tenant.UserId`) để gắn tài khoản với hồ sơ khách.

### Rủi ro / tương thích ngược

- **Migration `ApprovedAt`**: nếu không backfill, toàn bộ tài khoản cũ (kể cả Admin seed) thành Chờ duyệt
  và bị khóa đăng nhập. Bắt buộc backfill trong `Up()`.
- Lộ dữ liệu nếu lọc phạm vi sau khi phân trang/đếm: phải áp phạm vi **trước** keyword/count.
- Merge VHT-24 vào nhánh này khiến PR VHT-95 chứa commit VHT-24 (đã chốt phương án); nếu VHT-24 vào `dev`
  trước thì giải conflict nhỏ ở `Program.cs`, `ApplicationDbContext.cs`, snapshot.
- Khóa tài khoản đang đăng nhập: cookie còn hiệu lực tới lần revalidate (30 phút,
  `IdentityRevalidatingAuthenticationStateProvider.cs:18`); hiện có đổi SecurityStamp khi khóa — giữ nguyên.
- `DevAccountSeeder` reset lockout mỗi lần chạy dev; phải set thêm `ApprovedAt` để tài khoản mẫu luôn dùng được.

---

## 3. Code tham khảo

| File | Vai trò | Dự kiến |
|---|---|---|
| `NhaTro/Web/Features/Accounts/AccountService.cs` | list/lock/unlock/đổi vai trò (VHT-8) | SỬA |
| `NhaTro/Web/Features/Accounts/AccountPermissionRules.cs` | luật D1 thuần | SỬA |
| `NhaTro/Web/Features/Accounts/IAccountService.cs` | hợp đồng service | SỬA |
| `NhaTro/Web/Features/Accounts/AccountErrorCodes.cs` | mã lỗi | SỬA |
| `NhaTro/Web/Features/Accounts/DTOs/AccountListItemDto.cs` | dòng danh sách | SỬA |
| `NhaTro/Web/Features/Accounts/DTOs/AccountResult.cs` | kết quả | GIỮ |
| `NhaTro/Web/Features/Accounts/Roles.cs` | tên vai trò | GIỮ |
| `NhaTro/Web/Data/ApplicationUser.cs` | entity user | SỬA (`ApprovedAt`) |
| `NhaTro/Web/Data/Configurations/Accounts/ApplicationUserConfiguration.cs` | EF config | SỬA |
| `NhaTro/Web/Data/Migrations/` | migration mới | TẠO |
| `NhaTro/Web/Program.cs` | Identity, service, seeder | SỬA |
| `NhaTro/Web/Components/Pages/Accounts/AccountManagement.razor` | trang `/admin/users` | SỬA |
| `NhaTro/Web/Components/Account/Pages/Login.razor` | kết quả đăng nhập | SỬA |
| `NhaTro/Web/Components/Layout/AppNavigation.cs` | menu | SỬA |
| `NhaTro/Web/Resources/Shared.vi-VN.resx`, `Shared.en-US.resx` | nhãn VI/EN | SỬA |
| `NhaTro/Web/Data/Seeding/AdminSeeder.cs`, `DevAccountSeeder.cs` | tài khoản seed | SỬA |
| `NhaTro/Web/Features/Properties/{PropertyScope,Entities/PropertyManager,Services/PropertyManagerService}.cs` | phạm vi tòa nhà (VHT-24) | CHỈ ĐỌC (sau merge) |
| `NhaTro/Web/Features/Tenants/TenantService.cs`, `Entities/Tenant.cs` | hồ sơ khách + liên kết | CHỈ ĐỌC |
| `NhaTro/Web/Components/Shared/Feedback/StatusBadge.razor` | badge (Phúc) | CHỈ ĐỌC |
| `NhaTro/NhaTro.UnitTests/Features/Accounts/AccountPermissionRulesTests.cs` | test luật | SỬA |
| `NhaTro/NhaTro.IntegrationTests/Infrastructure/TestDataSeeder.cs` | dữ liệu test | SỬA |

Nguồn đã dùng: code hiện tại → `thao_tac_va_quyen.xlsx` → `thiet_ke_he_thong.md` → `phong_cach_giao_dien.md`
(dòng 215: trạng thái tài khoản dùng màu thông tin/thành công/trung tính) → `docs/architecture/localization.md`.

---

## 4. File thay đổi (dự kiến)

| File | Trạng thái | Chức năng | Lý do |
|---|---|---|---|
| `NhaTro/Web/Features/Accounts/AccountStatus.cs` | TẠO | enum 3 trạng thái + hàm suy trạng thái thuần | một nguồn logic trạng thái, unit test được |
| `NhaTro/Web/Data/ApplicationUser.cs` | SỬA | thêm `DateTime? ApprovedAt` | null = Chờ duyệt |
| `NhaTro/Web/Data/Configurations/Accounts/ApplicationUserConfiguration.cs` | SỬA | map cột `ApprovedAt` | schema |
| `NhaTro/Web/Data/Migrations/*_AddUserApprovedAt.cs` | TẠO | thêm cột + backfill tài khoản cũ | tránh khóa toàn bộ tài khoản cũ |
| `NhaTro/Web/Features/Accounts/AccountPermissionRules.cs` | SỬA | tạo/dyệt/khóa/xem theo phạm vi, chặn Admin cuối | ô #5/#6/#7 |
| `NhaTro/Web/Features/Accounts/AccountService.cs` | SỬA | list theo phạm vi, create, approve, lock/unlock, chọn hồ sơ khách | ô #3/#5/#6 |
| `NhaTro/Web/Features/Accounts/IAccountService.cs` | SỬA | thêm API create/approve/tenant picker | hợp đồng service |
| `NhaTro/Web/Features/Accounts/DTOs/CreateAccountRequest.cs` | TẠO | dữ liệu tạo tài khoản | input UI |
| `NhaTro/Web/Features/Accounts/DTOs/AccountListItemDto.cs` | SỬA | thêm `Status`, `ApprovedAt` | hiển thị 3 trạng thái |
| `NhaTro/Web/Features/Accounts/AccountErrorCodes.cs` | SỬA | mã lỗi mới (LastAdmin, OutOfScope, ...) | thông báo UI |
| `NhaTro/Web/Components/Account/AccountSignInManager.cs` | TẠO | override `CanSignInAsync` chặn Chờ duyệt | chặn đăng nhập |
| `NhaTro/Web/Program.cs` | SỬA | đăng ký SignInManager riêng | dùng lớp trên |
| `NhaTro/Web/Components/Account/Pages/Login.razor` | SỬA | xử lý `NotAllowed` → "Chờ duyệt" | UX |
| `NhaTro/Web/Components/Pages/Accounts/AccountManagement.razor` | SỬA | mở cho Admin/Chủ trọ/Quản lý, nút tạo/duyệt/khóa, cột trạng thái | UI quản trị |
| `NhaTro/Web/Components/Pages/Accounts/CreateAccountDialog.razor` | TẠO | form tạo tài khoản + chọn hồ sơ khách | UI |
| `NhaTro/Web/Components/Layout/AppNavigation.cs` | SỬA | menu Accounts cho Chủ trọ/Quản lý | điều hướng |
| `NhaTro/Web/Resources/Shared.{vi-VN,en-US}.resx` | SỬA | nhãn mới VI/EN | song ngữ |
| `NhaTro/Web/Data/Seeding/AdminSeeder.cs`, `DevAccountSeeder.cs` | SỬA | set `ApprovedAt` | tài khoản seed hoạt động |
| `NhaTro/NhaTro.UnitTests/Features/Accounts/AccountPermissionRulesTests.cs` | SỬA | kỳ vọng mới (Chủ trọ 2 vai trò, phạm vi) | ô #5 |
| `NhaTro/NhaTro.UnitTests/Features/Accounts/AccountStatusTests.cs` | TẠO | suy trạng thái thuần | hồi quy |
| `NhaTro/NhaTro.IntegrationTests/Features/Accounts/AccountLifecycleTests.cs` | TẠO | vòng đời + trái quyền + đăng nhập | tiêu chí |
| `NhaTro/NhaTro.IntegrationTests/Infrastructure/TestDataSeeder.cs` | SỬA | `ApprovedAt`, phân công Quản lý | dữ liệu test |

---

## 5. Câu hỏi đã hỏi & trả lời

- Q: VHT-24 chưa merge `dev`, xử lý cách nào? → **A: merge VHT-24 vào nhánh VHT-95** (chấp nhận PR chứa commit VHT-24).
- Q: Lưu trạng thái thế nào? → **A: `ApprovedAt` + `LockoutEnd`** (không thêm cột Status riêng).
- Q: Ô #3 "Chủ trọ xem toàn hệ thống" hay giữ code hiện tại? → **A: siết phạm vi** — Chủ trọ chỉ thấy tài khoản thuộc
  của mình; Admin cao hơn nhưng chỉ quản lý việc tài khoản, không xem nghiệp vụ. Giới hạn rõ ràng để tránh lộ
  thông tin khách hàng. (Ghi nhận lệch chữ "Toàn hệ thống" của ô #3 — ưu tiên bảo mật theo chỉ đạo.)
- Q: "Khách trong tòa nhà được giao" xác định bằng gì? → **A: chọn hồ sơ khách khi tạo** (hồ sơ đã có hợp đồng
  tại tòa thuộc phạm vi); gắn luôn `Tenant.UserId`; không thêm bảng.

### Hệ quả đã chốt (định nghĩa phạm vi)

| Người gọi | Thấy tài khoản nào | Tạo được vai trò | Trạng thái khi tạo | Duyệt / Khóa |
|---|---|---|---|---|
| Admin | Tất cả (chỉ trường tài khoản) | Admin, Chủ trọ, Quản lý, Khách thuê | Hoạt động | Mọi tài khoản, trừ tự khóa mình / Admin cuối |
| Chủ trọ | Chính mình; Quản lý đang được phân công vào tòa mình; Khách thuê gắn hồ sơ có hợp đồng tại tòa mình | Quản lý, Khách thuê | Hoạt động (thao tác Chủ trọ hiệu lực ngay) | Quản lý/Khách thuê trong phạm vi; không khóa Admin |
| Quản lý | Chính mình; Khách thuê gắn hồ sơ có hợp đồng tại tòa được phân công | Khách thuê | **Chờ duyệt** | Không |
| Khách thuê | Không | — | — | Không |

- "Phân công" = dòng `PropertyManagers` có `RevokedAt IS NULL`; "tòa mình" = `Properties.OwnerId = caller`.
- "Hồ sơ có hợp đồng tại tòa" = tồn tại `ContractMember → Contract → Room → Property` (giữ cả hợp đồng lịch sử;
  đã ghi rõ trong mục 6).
- Trạng thái hiển thị ưu tiên: **Khóa** (LockoutEnd vĩnh viễn) > **Chờ duyệt** (`ApprovedAt == null`) > **Hoạt động**.
- Mở khóa tài khoản chưa duyệt → quay về Chờ duyệt (không tự thành Hoạt động).

---

## 6. Quy tắc & không được làm

### Quy tắc nghiệp vụ

1. Không có đăng ký công khai; mọi tài khoản do Admin/Chủ trọ/Quản lý tạo.
2. Mật khẩu ban đầu do người tạo nhập (chưa có SMTP); áp chính sách mật khẩu hiện tại
   (`Program.cs:139-144`). Không hiển thị/ghi log mật khẩu.
3. Tạo tài khoản Khách thuê **bắt buộc** chọn hồ sơ khách chưa có tài khoản, thuộc phạm vi người gọi;
   gắn `Tenant.UserId` ngay khi tạo. Hồ sơ đã có tài khoản → từ chối.
4. Ngoại lệ VHT-25: quy tắc "chỉ Chủ trọ gắn/gỡ hồ sơ" áp cho luồng gắn tay; luồng tạo tài khoản kèm hồ sơ
   cho phép Quản lý **chỉ trong tòa được giao** theo ô #5. Gỡ liên kết vẫn chỉ Chủ trọ.
5. Không cho đổi vai trò một tài khoản đang gắn hồ sơ khách (phải gỡ liên kết trước) — tránh hồ sơ trỏ vào
   tài khoản không còn vai trò Khách thuê.
6. Khóa: `LockoutEnd = MaxValue`, `LockoutEnabled = true`, đổi SecurityStamp (giữ hành vi VHT-8).
   Chặn khóa Admin cuối cùng (đếm Admin đang hoạt động, chưa khóa) và chặn tự khóa.
7. Duyệt: chỉ tài khoản Chờ duyệt; set `ApprovedAt = now`, xóa `LockoutEnd` nếu không phải khóa quản trị.
8. Chặn đăng nhập Chờ duyệt ở tầng `SignInManager.CanSignInAsync` (áp cho mọi luồng đăng nhập, kể cả 2FA).
9. Mọi kiểm quyền ở service, không tin `callerRole`/`callerUserId` từ client; UI chỉ ẩn/hiện nút.

### Không được làm

- Không commit/push (kể cả merge VHT-24) khi chưa được duyệt.
- Không thêm bảng/cột ngoài `ApprovedAt`; không đổi schema Tenant/Property.
- Không sửa nghiệp vụ P2–P8, VHT-24 UI, VHT-96/97.
- Không tạo abstraction/file rỗng trước; không migration rỗng.

---

## 7. Công việc

### TASK-01: Merge VHT-24 vào nhánh VHT-95
- Các bước: `git merge --no-ff origin/VHT-24-hoan-thien-pham-vi-quan-ly-toa-nha-p-1-p-3`; giải conflict nếu có
  (`Program.cs`, `ApplicationDbContext.cs`, snapshot); build lại.
- File: theo commit VHT-24 (22 file) + merge commit.
- Kết quả dự kiến: có `PropertyManagers`, `IPropertyManagerService`, migration `AddPropertyManagers`.
- Điều kiện hoàn thành: `dotnet build` pass; bảng có trong model.
- Kết quả: merge `c1e8498`, conflict `Program.cs` + `SqlServerFixture.cs` giữ cả hai bên, build 0 error.
- Phụ thuộc: duyệt kế hoạch. Trạng thái: **done**

### TASK-02: Mô hình trạng thái `ApprovedAt`
- Các bước: thêm `DateTime? ApprovedAt` vào `ApplicationUser`; config cột `datetime2 NULL`;
  migration có backfill `UPDATE AspNetUsers SET ApprovedAt = SYSUTCDATETIME() WHERE ApprovedAt IS NULL`;
  tạo `AccountStatus` + hàm thuần `Derive(ApprovedAt, LockoutEnd, now)`.
- Kết quả: suy đúng 3 trạng thái, tài khoản cũ không bị khóa.
- Điều kiện hoàn thành: `dotnet ef migrations has-pending-model-changes` sạch; unit test suy trạng thái pass.
- Kết quả: `ApplicationUser.ApprovedAt`, migration `20261030183037_AddUserApprovedAt` (backfill `SYSUTCDATETIME()`),
  `AccountStatus` + `AccountStatusRules`, seeder set `ApprovedAt`; 7 test trạng thái pass; model sạch.
- Phụ thuộc: TASK-01. Trạng thái: **done**

### TASK-03: Luật quyền tài khoản
- Các bước: `AssignableRoles(ChuTro)` = `[QuanLy, KhachThue]`; thêm `CanCreateRole`, `CanApprove`,
  `CheckLock(..., targetIsLastAdmin)`; `CanManageAccounts` thêm Quản lý; giữ `VisibleRoles` theo vai trò,
  phạm vi dữ liệu xử lý ở service; thêm mã lỗi mới.
- Điều kiện hoàn thành: unit test cho ma trận tạo/duyệt/khóa + Admin cuối pass.
- Kết quả: `AssignableRoles(ChuTro)` = QuanLy + KhachThue; thêm `CanCreateRole`, `CanApprove`,
  `CheckLock(targetIsLastAdmin)`, `CheckChangeRole(targetIsLinkedTenant)`; `CanManageAccounts` gồm Quản lý;
  mã lỗi mới; 47 test Accounts pass.
- Phụ thuộc: TASK-02. Trạng thái: **done**

### TASK-04: `AccountService` — list theo phạm vi, tạo, duyệt
- Các bước:
  - Tiêm `IDbContextFactory<ApplicationDbContext>`; query user + role server-side (không load cả role rồi lọc).
  - Áp phạm vi **trước** keyword/count:
    - Chủ trọ: self ∪ Quản lý có phân công active vào tòa mình ∪ Khách thuê gắn hồ sơ có hợp đồng tại tòa mình.
    - Quản lý: self ∪ Khách thuê gắn hồ sơ có hợp đồng tại tòa được phân công.
  - `CreateAsync`: kiểm `CanCreateRole`, dựng `ApplicationUser` + role + `ApprovedAt` theo bảng mục 5;
    vai trò Khách thuê: bắt buộc `TenantId`, kiểm hồ sơ chưa gắn + thuộc phạm vi; tạo user rồi gắn
    `Tenant.UserId`; nếu gắn lỗi thì xóa user vừa tạo (bù trừ), bắt unique vi phạm.
  - `ApproveAsync`; chỉnh `LockAsync` dùng `targetIsLastAdmin`; `UnlockAsync` giữ nguyên `ApprovedAt`.
  - `SearchAssignableTenantsAsync`: tìm hồ sơ chưa có tài khoản trong phạm vi (dùng cho dialog).
- Điều kiện hoàn thành: integration test ô #3/#5/#6 + trái quyền pass; không lộ tài khoản ngoài phạm vi.
- Kết quả: `ListAsync` áp phạm vi trước keyword/count; `CreateAsync` kiểm `CanCreateRole` + hồ sơ trong phạm vi,
  gắn `Tenant.UserId` có bù trừ khi lỗi; `ApproveAsync`; `LockAsync` chặn Admin cuối; `UnlockAsync` giữ `ApprovedAt`;
  `SearchAssignableTenantsAsync` chỉ trả hồ sơ chưa gắn trong phạm vi. 7 integration test Accounts pass.
- Phụ thuộc: TASK-03. Trạng thái: **done**

### TASK-05: Chặn đăng nhập Chờ duyệt
- Các bước: `AccountSignInManager : SignInManager<ApplicationUser>` override `CanSignInAsync`
  (base && `ApprovedAt != null`); đăng ký `AddSignInManager<AccountSignInManager>()`; `Login.razor`
  xử lý `result.NotAllowed` → thông báo "Tài khoản đang chờ duyệt".
- Điều kiện hoàn thành: integration test đăng nhập pending thất bại; sau duyệt thành công.
- Kết quả: `AccountSignInManager` override `CanSignInAsync`; `Login.razor` xử lý `NotAllowed` → câu "Tài khoản đang
  chờ duyệt…"; test pending (200 + ở lại `/login`) → duyệt → 302. Trang `/Account/Lockout` được Việt hoá lại.
- Phụ thuộc: TASK-02. Trạng thái: **done**

### TASK-06: UI quản trị tài khoản
- Các bước:
  - `/admin/users`: bỏ `[Authorize(Roles=Admin)]`, dùng policy yêu cầu đăng nhập + service tự chặn;
    danh sách theo phạm vi; cột trạng thái 3 màu Info/Success/Neutral (theo `phong_cach_giao_dien.md:215`).
  - Nút: Tạo tài khoản (theo `AssignableRoles`), Duyệt (pending), Khóa/Mở khóa (theo quyền), Gán vai trò (Admin).
  - `CreateAccountDialog`: email, họ tên, SĐT, mật khẩu, vai trò; nếu Khách thuê → ô tìm/chọn hồ sơ khách.
  - Menu `Accounts` cho `Admin,ChuTro,QuanLy`; thêm nhãn VI/EN.
- Điều kiện hoàn thành: 3 vai trò vào được đúng phạm vi; thao tác trái quyền bị từ chối kèm thông báo;
  keyboard/mobile không tràn (kiểm 320–414px).
- Kết quả: `/admin/users` mở cho Admin/Chủ trọ/Quản lý; nút Duyệt/Khóa/Gán vai trò theo luật; `CreateAccountDialog`
  chọn hồ sơ khách thuê trong phạm vi; sửa lỗi Razor truyền thiếu `@` cho `CallerUserId`/`CallerRole`.
- Phụ thuộc: TASK-04. Trạng thái: **done**

### TASK-07: Test & cập nhật test cũ
- Các bước: sửa `AccountPermissionRulesTests` (Chủ trọ 2 vai trò, mã lỗi mới); thêm unit test trạng thái;
  thêm integration `AccountLifecycleTests`; cập nhật `TestDataSeeder` (`ApprovedAt`, phân công Quản lý);
  rà `P1P3RoleAccessTests` nếu có kỳ vọng `/admin/users`.
- Điều kiện hoàn thành: toàn bộ test Accounts + Tenants pass (LocalDB có thì chạy, không có thì ghi rõ skip).
- Kết quả: `AccountPermissionRulesTests` cập nhật; thêm `AccountStatusTests`, `AccountLifecycleTests` (7 test) và test
  đăng nhập pending; `TestDataSeeder` thêm `ApprovedAt` + phân công Quản lý. Accounts 7/7, toàn bộ integration 99/99.
- Phụ thuộc: TASK-04, TASK-05, TASK-06. Trạng thái: **done**

### TASK-08: Kiểm chứng tổng + báo cáo
- Các bước: build, test, chạy app dev, smoke bằng 4 tài khoản mẫu; ghi kết quả vào mục 9.
- Kết quả: build 0 lỗi (1 warning `xUnit2031` có sẵn); integration Accounts 7/7, toàn bộ integration 99/99;
  unit 501 pass + 3 fail `SubmitPaymentRequestTests` (lỗi culture vi-VN có sẵn, ngoài phạm vi);
  smoke UI headed bằng `scripts/ui-smoke` (local-only) đạt 8/8 kịch bản vòng đời và 3/3 kịch bản phân quyền;
  trang `/Account/Lockout` đã Việt hoá lại (trước đó là scaffold Identity tiếng Anh).
- Phụ thuộc: TASK-07. Trạng thái: **done**

---

## 8. Kiểm thử & xác minh

| Loại | Lệnh / kịch bản |
|---|---|
| Build | `dotnet build NhaTro/NhaTro.slnx` |
| Migration | `dotnet ef migrations has-pending-model-changes --project NhaTro/Web` |
| Unit | `dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj` |
| Integration | `dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter "FullyQualifiedName~Accounts"` |
| E2E (không bắt buộc) | `NhaTro/NhaTro.E2ETests` nếu môi trường có Playwright |
| Smoke UI (local-only, không commit) | `cd scripts/ui-smoke && npm install` rồi `npm run vht95:headed`, `npm run vht95-roles:headed` (app đang chạy ở `http://localhost:5177`) |

Kịch bản tay (dev, 4 tài khoản mẫu):

1. Quản lý tạo tài khoản Khách thuê cho hồ sơ trong tòa được giao → trạng thái Chờ duyệt; đăng nhập bị chặn.
2. Chọn hồ sơ ngoài tòa được giao → bị từ chối (kể cả gọi service trực tiếp).
3. Chủ trọ duyệt → Khách thuê đăng nhập được; Chủ trọ khóa → đăng nhập bị chặn; mở khóa → vào lại.
4. Chủ trọ tạo Quản lý → Hoạt động ngay, đăng nhập được.
5. Chủ trọ/Quản lý không thấy tài khoản ngoài phạm vi (kể cả khi sửa `roleFilter`, `keyword`).
6. Admin tạo từng vai trò; khóa/mở khóa; không khóa được Admin cuối cùng/chính mình.
7. Khách thuê không vào được `/admin/users`; Quản lý không thấy nút Duyệt/Khóa.
8. `GET /Account/Register` → 404 (không đăng ký công khai).

---

## 9. Tiến độ

- Đang làm: — (TASK-01 → TASK-08 xong)
- Tiếp theo: chờ duyệt để push nhánh `VHT-95-...` và mở PR về `dev`
- Bị chặn: chưa
- Quyết định còn chờ: —
- Thay đổi ngoài kế hoạch + lý do: viết lại `/Account/Lockout` + thêm khoá dịch `LockoutTitle`/`LockoutMessage`
  (trang mặc định của Identity hiện tiếng Anh, lệch chuẩn UI); thêm bộ smoke UI local `scripts/ui-smoke/`
  (đã nằm trong `.gitignore` qua `scripts/*`) để kiểm chứng luồng thật thay vì gọi tool rời rạc.
- File đã sửa thực tế:
  - Web: `Data/ApplicationUser.cs`, `Data/Configurations/Accounts/ApplicationUserConfiguration.cs`,
    `Data/Migrations/20261030183037_AddUserApprovedAt.{cs,Designer.cs}` + `ApplicationDbContextModelSnapshot.cs`,
    `Data/Seeding/AdminSeeder.cs`, `Data/Seeding/DevAccountSeeder.cs`, `Program.cs`,
    `Features/Accounts/{AccountService,IAccountService,AccountPermissionRules,AccountErrorCodes,AccountStatus}.cs`,
    `Features/Accounts/DTOs/{AccountListItemDto,CreateAccountRequest,AssignableTenantDto}.cs`,
    `Components/Account/AccountSignInManager.cs`, `Components/Account/Pages/{Login,Lockout}.razor`,
    `Components/Layout/AppNavigation.cs`, `Components/Pages/Accounts/{AccountManagement.razor,CreateAccountDialog.razor,.razor.css}`,
    `Resources/Shared.vi-VN.resx`, `Resources/Shared.en-US.resx`.
  - Test: `NhaTro.UnitTests/Features/Accounts/{AccountPermissionRulesTests,AccountStatusTests}.cs`,
    `NhaTro.IntegrationTests/Infrastructure/{SqlServerFixture,TestDataSeeder}.cs`,
    `NhaTro.IntegrationTests/Features/Accounts/`.
  - Local-only (không commit): `scripts/ui-smoke/` (package.json, run.mjs, lib/, scenarios/, README.md, artifacts/).
- Kiểm tra đã chạy / chưa chạy:
  - Build `dotnet build NhaTro/NhaTro.slnx`: chạy, 0 lỗi.
  - Integration Accounts: chạy 7/7; toàn bộ integration 99/99.
  - Unit: 501 pass, 3 fail `Features.Invoices.SubmitPaymentRequestTests` do culture vi-VN (có sẵn, không liên quan VHT-95).
  - Migration `AddUserApprovedAt`: đã áp vào LocalDB `NhaTroDb`.
  - Smoke UI headed: `vht95` 8/8, `vht95-roles` 3/3.
  - Kịch bản tay mục 8: phủ qua smoke, trừ mục #2 (chọn hồ sơ ngoài tòa) đã có integration test thay thế.
- Vấn đề tồn đọng: 3 unit test invoice fail do culture (ngoài phạm vi VHT-95); DB dev tích luỹ tài khoản
  `ui.smoke.<timestamp>@test.local` sau mỗi lần chạy smoke; `khach.smoke` còn bị khoá trong DB dev;
  Khách thuê vào `/admin/users` thấy danh sách rỗng (service trả rỗng) chứ không redirect AccessDenied — không lộ dữ liệu.
