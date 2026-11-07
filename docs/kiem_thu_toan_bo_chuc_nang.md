# Kiểm thử toàn bộ chức năng — Web quản lý nhà trọ

> Cập nhật: 06/11/2026 · Phạm vi: toàn bộ chức năng đang có trong `NhaTro/Web` (UI Blazor Server → service → EF Core → SQL Server).
> Tài liệu này dùng để **test tay** và **đối chiếu code**. Mỗi mục đều chỉ rõ: đi từ đâu trên UI, component nào, service nào, bảng SQL nào, và cách kiểm chứng.
>
> Quy ước: số dòng có thể lệch nếu file bị sửa. Khi lệch, mở file và tìm theo **tên hàm** đã ghi.

---

## 0. TL;DR — chạy nhanh trong 10 phút

1. Mở `NhaTro/NhaTro.slnx` bằng Visual Studio → chọn project `Web` → **F5**. URL: `http://localhost:5177`.
2. Đăng nhập `chutro@nhatro.local` / mật khẩu trong User Secrets (`DevAccounts:Password`).
3. Đi theo kịch bản E2E ở [mục 8](#8-kịch-bản-e2e-tuần-tự-happy-path) (25 bước).
4. Kiểm DB bằng các query ở [mục 10](#10-kiểm-chứng-sql).
5. Chạy test tự động ở [mục 11](#11-test-tự-động).

---

## 1. Dữ liệu đã được dọn (06/11/2026)

### 1.1. Đã xóa

| Loại | Trước | Sau | Ghi chú |
|---|---:|---:|---|
| Nhà trọ | 35 | **1** | Xóa 34 nhà trọ tên `UI Smoke VHT98 ...` |
| Phòng | 83 | **1** | Xóa 82 phòng mã `UI98...` |
| Khách thuê | 25 | **1** | Xóa 24 hồ sơ `Khách VHT98 ...` |
| Hợp đồng | 21 | **0** | Toàn bộ là hợp đồng smoke, đã xóa kèm thành viên |
| Tài khoản | 13 | **5** | Xóa `*.smoke@test.local`, `likavn1717@gmail.com`, `likavn1919@gmail.com` |
| Hóa đơn / thanh toán / sự cố / dịch vụ / chỉ số | 0 | 0 | Vốn đã trống |

Script dọn: `scripts/database/don_du_lieu_smoke.sql` (idempotent — chạy lại không làm gì khi không còn dữ liệu smoke).
Backup trước khi dọn: `C:\Users\ADMIN\AppData\Local\Temp\NhaTroDb_before_cleanup.bak` (SQL Server LocalDB).

### 1.2. Dữ liệu chính còn lại

| Bảng | Dữ liệu |
|---|---|
| `Properties` | `Id=1` — **Nhà trọ Hòa Bình**, `12 Nguyễn Trãi, Phường Bến Thành, Quận 1, TP.HCM`, chủ sở hữu `chutro@nhatro.local` |
| `Rooms` | `Id=1` — **P101**, tầng 1, 25 m², giá 3.500.000, trạng thái `Available (0)` |
| `Tenants` | `Id=1` — Toan Alo Toan, SĐT `0384942049`, gắn tài khoản `khachthue@nhatro.local` |
| `PropertyManagers` | `quanly@nhatro.local` → nhà `Id=1`, `RevokedAt = NULL` |
| `ManagerToggles` | Đủ **21** công tắc SW-01…SW-16 + SW-15a…e |
| `UtilityTariffs` / `UtilityTariffTiers` | 5 phiên bản giá điện/nước (read-only) + 24 bậc |
| `AspNetUsers` | `admin@admin.com` (Admin), `chutro@nhatro.local` (ChuTro), `quanly@nhatro.local` (QuanLy), `khachthue@nhatro.local` (KhachThue), `name123@gmail.com` (QuanLy, chưa có chủ) |
| Còn lại | `Contracts`, `Invoices`, `Payments`, `MaintenanceIncidents`, `ServiceCatalogs`, `UtilityReadings`, `StoredFiles` = **0 dòng** |

Script chuẩn hóa 1 nhà + 1 phòng: `scripts/database/chuan_hoa_du_lieu_chinh.sql`.

> Vì `Contracts = 0`, muốn test hợp đồng/hóa đơn/chỉ số phải tạo dữ liệu theo kịch bản E2E. Đây là lý do tài liệu có mục "chuẩn bị dữ liệu" và thứ tự test bắt buộc.

---

## 2. Chuẩn bị môi trường

### 2.1. Chạy app

| Việc | Cách làm |
|---|---|
| Mở solution | `NhaTro/NhaTro.slnx` |
| Chạy | Chọn project `Web` → `F5` (Development) |
| URL | `http://localhost:5177` |
| Dừng app | `Shift+F5` — **đừng để server nền chiếm port** khi build CLI |
| Build CLI | `dotnet build NhaTro/Web/Web.csproj` |
| DB | SQL Server LocalDB `(localdb)\mssqllocaldb`, database `NhaTroDb` |

### 2.2. Cấu hình bắt buộc

- Connection string thật nằm trong **User Secrets**:
  `%APPDATA%\Microsoft\UserSecrets\aspnet-Web-fe3a4465-6d62-438e-9626-5895e0d57ca0\secrets.json`
- `appsettings.json` trỏ database `aspnet-Web-fe3a4465-...`, nên khi chạy `dotnet ef` phải đặt:
  `$env:ASPNETCORE_ENVIRONMENT="Development"`.
- `Program.cs` (Development) tự chạy migration trước khi seed: `Program.cs:527` (`MigrateAsync`), seed role/admin/dev account ở `Program.cs:535-539`.
- Nếu thiếu `DevAccounts:Password`, seeder **bỏ qua** tạo tài khoản dev (chỉ tạo role).
- Test tự động dùng database riêng `NhaTro_It_*`, không đụng `NhaTroDb`.

### 2.3. Tài khoản test

| Email | Vai trò | Trạng thái | Dùng để test |
|---|---|---|---|
| `admin@admin.com` | Admin | Active | Quản trị tài khoản, xem toàn bộ, **không** vào nghiệp vụ |
| `chutro@nhatro.local` | ChuTro | Active | Chủ sở hữu nhà Id=1, toàn quyền nghiệp vụ thuộc nhà mình |
| `quanly@nhatro.local` | QuanLy | Active | Đã được gán nhà Id=1 |
| `khachthue@nhatro.local` | KhachThue | Active | Gắn hồ sơ khách Id=1 (chưa có hợp đồng) |
| `name123@gmail.com` | QuanLy | Active | Quản lý **chưa có chủ** (dùng test banner cảnh báo) |

Mật khẩu dev: lấy từ `DevAccounts:Password` trong User Secrets. Không hardcode trong code.
`name123@gmail.com` do người dùng tạo tay, **không** nằm trong seeder nên mật khẩu có thể khác; nếu không đăng nhập được thì dùng cách thu hồi gán nhà của `quanly@nhatro.local` để test banner "Quản lý chưa có chủ".

### 2.4. Đăng nhập và trang chủ

- Login: `/login` hoặc `/Account/Login` — component `NhaTro/Web/Components/Account/Pages/Login.razor`.
- Không có trang đăng ký công khai; tài khoản do Admin/Chủ trọ/Quản lý tạo.
- Sau khi đăng nhập, `/` là `HomeView.razor`, nội dung KPI thay đổi theo vai trò.
- Nút đăng nhập nhanh chỉ hiện ở Development (`Components/Account/Shared/DevQuickLogin.razor`).

---

## 3. Cách đọc mỗi ca kiểm thử

Mỗi module có 4 phần:

1. **Đường đi UI**: route + file `.razor` + handler.
2. **Luồng code**: `Page/Component → PageService (nếu có) → Domain Service → DbContext → bảng SQL`.
3. **Bảng ca test**: `Mã | Vai trò | Thao tác | Kết quả mong đợi | Cách kiểm`.
4. **Query kiểm chứng**: câu SQL đối chiếu.

Ký hiệu vai trò: `AD` = Admin, `CT` = Chủ trọ, `QL` = Quản lý, `KT` = Khách thuê.

---

## 4. Bản đồ route và phân quyền

### 4.1. Route có UI

| URL | File | Quyền |
|---|---|---|
| `/` | `Features/Home/Pages/HomeView.razor` | Đã đăng nhập |
| `/dashboard` | `Features/Dashboard/Pages/DashboardView.razor` | CT, QL |
| `/admin/users` | `Components/Pages/Accounts/AccountManagement.razor` | Đã đăng nhập; service lọc theo vai trò |
| `/profile` | `Components/Pages/Accounts/UserProfile.razor` | AD, CT, QL |
| `/properties` | `Features/Properties/Pages/PropertyList.razor` | CT, QL |
| `/rooms` | `Features/Rooms/Pages/RoomList.razor` | CT, QL |
| `/rooms/trash` | `Features/Rooms/Pages/RoomTrash.razor` | CT |
| `/tenants` | `Features/Tenants/Pages/TenantList.razor` | CT, QL |
| `/trash` | `Features/Tenants/Pages/TenantTrash.razor` | CT |
| `/contracts` | `Features/Contracts/Pages/ContractListView.razor` | Policy `ContractBusiness` (CT, QL, KT; chặn AD) |
| `/contracts/create` | `Features/Contracts/Pages/ContractCreateForm.razor` | Policy `ContractManage` (CT, QL) |
| `/contracts/{id}` | `Features/Contracts/Pages/ContractDetailView.razor` | Policy `ContractBusiness` |
| `/services` | `Features/Utilities/Pages/ServiceCatalogView.razor` | Policy `UtilityBusiness` |
| `/services/prices` | `Features/Utilities/Pages/ServicePricesView.razor` | Policy `UtilityBusiness` |
| `/meter-readings` | `Features/Utilities/Pages/MeterReadingForm.razor` | Policy `UtilityBusiness` |
| `/services/readings` | `Features/Utilities/Pages/MeterReadingRedirect.razor` | Redirect |
| `/invoices`, `/my-invoices` | `Features/Invoices/Pages/InvoiceListView.razor` | Policy `InvoiceView` |
| `/invoices/{id}`, `/my-invoices/{id}` | `Features/Invoices/Pages/InvoiceDetailView.razor` | Policy `InvoiceView` |
| `/incidents` | `Features/Maintenance/Pages/Incidents.razor` | Policy `IncidentAccess` |
| `/reports` | `Features/Reports/Pages/BusinessReportsView.razor` | CT, QL |
| `/manager-switches` | `Components/Pages/ManagerSwitches.razor` | CT |
| `/approvals` | `Components/Pages/ApprovalInbox.razor` | CT |
| `/my-requests` | `Components/Pages/MyApprovalRequests.razor` | QL |
| `/documents` | `Features/Tenants/Pages/TenantDocumentsView.razor` | KT |
| `/tenant/my-room` | `Features/Tenants/Pages/MyLeasedRoomView.razor` | KT |
| `/tenant/profile` | `Features/Tenants/Pages/MyTenantProfile.razor` | KT |
| `/dev/components` | `Components/Pages/ComponentsDemo.razor` | Trang demo component, không thuộc nghiệp vụ |

### 4.2. Policy trong `Program.cs:161-203`

| Policy | Cho phép | Dùng ở |
|---|---|---|
| `AdminOnly` | AD | Mở rộng sau này |
| `Business` | CT, QL, KT | Endpoint file |
| `ContractBusiness` | CT, QL, KT (chặn AD) | `/contracts`, `/contracts/{id}` |
| `ContractManage` | CT, QL | `/contracts/create` |
| `IncidentAccess` | Theo `IncidentAccess.IsAllowed` | `/incidents` |
| `UtilityBusiness` | CT, QL, KT | `/services*`, `/meter-readings` |
| `UtilityManage` | CT | Quản lý dịch vụ |
| `UtilityRecord` | CT, QL | Ghi chỉ số |
| `InvoiceView` | CT, QL, KT (chặn AD) | `/invoices*` |

### 4.3. Nguyên tắc 2 lớp

1. **Route/policy** chặn ở cửa (403 hoặc redirect login).
2. **Service** kiểm tra lại quyền + phạm vi dữ liệu (owner/property scope).
   Không bao giờ chỉ tin UI.

---

## 5. Kiểm thử xuyên suốt (cross-cutting)

### 5.1. Đăng nhập, chặn IP, captcha

**Code**: `Login.razor` → `AccountSignInManager.CanSignInAsync` (`Components/Account/AccountSignInManager.cs`) → Identity.
**Cấu hình**: `Program.cs:142-155` (mật khẩu ≥ 8, có số/hoa/thường; email duy nhất; **không auto-lock tài khoản**).
**Chống dò mật khẩu**: `Infrastructure/Security/LoginAttemptGuard.cs` + `Security:LoginGuard` trong `appsettings.json`
(mặc định: sai 10 lần/15 phút từ cùng IP → chặn IP 15 phút; sai 3 lần → bắt CAPTCHA).

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| X-01 | — | Sai mật khẩu 1 lần | Báo lỗi, không vào được |
| X-02 | — | Sai 5 lần cùng tài khoản | **Tài khoản KHÔNG bị khóa** (chỉ tăng bộ đếm để bắt CAPTCHA). Đăng nhập đúng vẫn vào được ngay |
| X-03 | — | Sai 3 lần liên tiếp | Hiện captcha số học; nhập đúng mới thử tiếp |
| X-04 | — | Đăng nhập tài khoản `ApprovedAt = NULL` | Bị chặn bởi `AccountSignInManager` |
| X-05 | — | Đăng nhập tài khoản bị Admin khóa vĩnh viễn | Bị chặn (`LockoutEnd` do quản trị đặt) |
| X-06 | — | Đăng xuất rồi mở `/admin/users` | Redirect về `/login?ReturnUrl=...` |
| X-07 | — | Mở `/login` khi đã đăng nhập | Chuyển về trang chủ |
| X-08 | — | Sai mật khẩu quá ngưỡng tần suất (mặc định 10 lần/phút) | **Chuyển về `/login?tooManyRequests=1`** và hiện banner "Bạn thao tác quá nhanh. Vui lòng thử lại sau." trên form — không còn trang trắng chỉ có text thô |
| X-08a | — | Sau 10 lần thử thất bại từ cùng IP (sai mật khẩu hoặc trả lời CAPTCHA sai, kể cả đổi email) | IP bị chặn 15 phút; form hiện "Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau {n} phút." Tài khoản mục tiêu **vẫn đăng nhập được từ IP khác** |
| X-08b | — | Đăng nhập đúng sau vài lần sai | Bộ đếm CAPTCHA của tài khoản được xóa; IP không bị ảnh hưởng nếu chưa tới ngưỡng |

**SQL**:
```sql
SELECT Email, ApprovedAt, LockoutEnd, AccessFailedCount
FROM AspNetUsers ORDER BY Email;
```

### 5.2. Phân quyền route

| Mã | Vai trò | Mở | Kết quả |
|---|---|---|---|
| X-10 | AD | `/properties`, `/rooms`, `/tenants`, `/dashboard`, `/reports` | 403 / `AccessDenied` |
| X-11 | AD | `/invoices`, `/contracts` | 403 (policy chặn AD) |
| X-12 | KT | `/properties`, `/rooms`, `/tenants`, `/reports` | 403 |
| X-13 | KT | `/invoices` | Chỉ thấy hóa đơn của mình |
| X-14 | QL | `/manager-switches`, `/approvals`, `/trash`, `/rooms/trash` | 403 |
| X-15 | CT | `/documents`, `/tenant/my-room` | 403 |
| X-16 | Chưa đăng nhập | Bất kỳ route `[Authorize]` | Redirect `/login` |

### 5.3. Song ngữ VI/EN

**Code**: `Program.cs:357-369` (resource `Resources/`, mặc định `vi-VN`), `docs/architecture/localization.md`.
**Kiểm**: đổi ngôn ngữ trên topbar → refresh → mọi nhãn đổi, **không mất dữ liệu form** (hợp đồng có snapshot culture).

| Mã | Thao tác | Kết quả |
|---|---|---|
| X-20 | Đổi VI → EN ở trang danh sách | Nhãn, cột, nút đổi; dữ liệu giữ nguyên |
| X-21 | Đổi ngôn ngữ khi đang điền form hợp đồng | Giữ dữ liệu đã nhập |
| X-22 | Kiểm tra key thiếu | Không hiện raw key `Some.Key` trên UI |

### 5.4. Layout, nav, banner Quản lý

**Code**: `Components/Layout/MainLayout.razor`, `AppSidebar.razor`, `AppNavigation.cs`, `ManagerOwnerBanner.razor`.

| Mã | Vai trò | Kết quả |
|---|---|---|
| X-30 | CT | Banner không hiện (chỉ dành cho QL) |
| X-31 | QL đã gán nhà | Banner xanh: "Bạn thuộc Chủ trọ …" |
| X-32 | QL chưa gán nhà | Banner vàng cảnh báo, **vẫn dùng được app** |
| X-33 | AD | Menu không có mục nghiệp vụ; mục `Permissions` đang `unavailable` (chưa có trang) |
| X-34 | Mobile | Sidebar mở/đóng được, không tràn ngang |

### 5.5. Health, lỗi, log

| Mã | Thao tác | Kết quả |
|---|---|---|
| X-40 | `GET /health/live` | `200` + JSON tối giản |
| X-41 | `GET /health/ready` | `200` khi DB kết nối được |
| X-42 | Gây lỗi service (ví dụ sửa DB ngắt kết nối) | Trang hiện `Error.razor`, không lộ stack trace |
| X-43 | Kiểm log | Không log mật khẩu/token/CCCD đầy đủ (`SensitiveDataScrubber`) |

---

## 6. Kiểm thử theo module

### 6.1. Tài khoản & hồ sơ cá nhân

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh sách tài khoản | `/admin/users` | `Components/Pages/Accounts/AccountManagement.razor` | `OnInitializedAsync` (164), `LoadAccountsAsync` (285) |
| Tạo tài khoản | dialog | `Components/Pages/Accounts/CreateAccountDialog.razor` | `SubmitAsync` (175) |
| Duyệt tài khoản | dialog xác nhận | `AccountManagement.razor` | `ConfirmApproveAsync` (329) |
| Đổi vai trò | dialog | `AccountManagement.razor` | `ConfirmRoleAsync` (358) |
| Khóa/mở khóa | dialog | `AccountManagement.razor` | `ConfirmLockAsync` (405) |
| Phân công tòa nhà | dialog | `Features/Properties/Components/ManagerAssignmentDialog.razor` | `AssignAsync` (145), `RevokeAsync` (177) |
| Hồ sơ của tôi | `/profile` | `Components/Pages/Accounts/UserProfile.razor` | — |

#### Luồng code

```
AccountManagement.razor
  → IAccountService.ListAsync / ApproveAsync / ChangeRoleAsync / LockAsync / UnlockAsync
    (Features/Accounts/AccountService.cs: 24 / 375 / 415 / 473 / 524)
    → AccountPermissionRules (Features/Accounts/AccountPermissionRules.cs)
    → ApplicationDbContext.Users / UserRoles / Roles / PropertyManagers / Properties
    → bảng AspNetUsers, AspNetUserRoles, AspNetRoles, PropertyManagers, Properties
```

Quy tắc chính (`AccountPermissionRules.cs`):

- `AssignableRoles` (16): AD tạo mọi vai trò; CT tạo QL + KT; QL chỉ tạo KT.
- `CanChangeRole` (35): **chỉ Admin**.
- `LockableRoles` (38): AD mọi vai trò; CT chỉ QL + KT.
- `VisibleRoles` (51): CT thấy QL + KT; QL thấy KT.
- `CheckChangeRole` (63): chặn tự đổi vai trò mình, chặn bỏ Admin cuối, chặn đổi vai trò tài khoản đang gắn hồ sơ khách.
- `CheckLock` (106): chặn tự khóa mình, chặn khóa Admin cuối.
- Phạm vi danh sách: `AccountService.ScopedUserIds` (646)
  - CT: chính mình + QL đang được gán vào nhà mình + KT có hợp đồng tại nhà mình.
  - QL: chính mình + KT có hợp đồng tại nhà được gán.
  - **QL chưa được gán nhà** không thấy ai ngoài chính mình.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P1-01 | CT | `/admin/users` | Chỉ thấy CT (mình), QL `quanly@nhatro.local`, KT `khachthue@nhatro.local`; **không** thấy admin, không thấy `name123@gmail.com` (QL chưa gán) |
| P1-02 | CT | Tạo tài khoản QL mới | Tạo thành công, `ApprovedAt` có ngay, đăng nhập được, **0 tòa** |
| P1-03 | CT | Tạo tài khoản KT | Bắt buộc chọn hồ sơ khách chưa có tài khoản; nếu bỏ trống → lỗi `TenantRequired` |
| P1-04 | CT | Tạo tài khoản với email trùng | Lỗi `DuplicateEmail` |
| P1-05 | CT | Tạo với mật khẩu yếu | Lỗi `WeakPassword` |
| P1-06 | QL | Tạo tài khoản KT (sau khi đã có hợp đồng Approved tại nhà được gán) | Thành công nhưng `ApprovedAt = NULL` (Chờ duyệt) |
| P1-07 | CT | Duyệt tài khoản Chờ duyệt | `ApprovedAt` được set; đăng nhập được |
| P1-08 | CT | Đổi vai trò QL → KT | **Không có nút** (chỉ Admin) |
| P1-09 | AD | Đổi vai trò | Được; nhưng không thể tự đổi mình / bỏ Admin cuối / đổi tài khoản đang gắn hồ sơ khách |
| P1-10 | AD | Khóa Admin cuối | Lỗi `CannotLockLastAdmin` |
| P1-11 | CT | (Không thấy Admin trong danh sách) gọi service khóa Admin | Lỗi `CannotLockAdmin` |
| P1-12 | CT | Khóa chính mình | Không có nút (self) |
| P1-13 | CT | Khóa/mở QL | Thành công; QL bị khóa không đăng nhập được |
| P1-14 | AD | Xem cột "Chủ trọ" của QL | Thấy tên/email chủ trọ; **không có nút phân công** |
| P1-15 | QL | `/admin/users` | Chỉ thấy mình + KT thuộc nhà được gán (hiện chưa có KT) |
| P1-16 | QL | Sửa hồ sơ `/profile` | Lưu được họ tên/SĐT; không đổi được email/vai trò |
| P1-17 | KT | Mở trực tiếp `/admin/users` | Trang mở được nhưng danh sách **rỗng** (route chỉ `[Authorize]`, service lọc theo vai trò) |

#### SQL kiểm chứng

```sql
-- Vai trò & trạng thái
SELECT u.Email, r.Name AS Role, u.ApprovedAt, u.LockoutEnd, u.FullName
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN AspNetRoles r ON r.Id = ur.RoleId
ORDER BY u.Email;

-- Quản lý thuộc chủ nào (qua tòa nhà đang gán)
SELECT pm.Id, p.Name AS Property, m.Email AS Manager, o.Email AS Owner, pm.AssignedAt, pm.RevokedAt
FROM PropertyManagers pm
JOIN Properties p ON p.Id = pm.PropertyId
JOIN AspNetUsers m ON m.Id = pm.ManagerUserId
LEFT JOIN AspNetUsers o ON o.Id = p.OwnerId
ORDER BY pm.Id;

-- Hồ sơ khách gắn tài khoản
SELECT t.Id, t.FullName, t.Phone, u.Email
FROM Tenants t LEFT JOIN AspNetUsers u ON u.Id = t.UserId;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Accounts/AccountPermissionRulesTests.cs`
- `NhaTro.UnitTests/Features/Accounts/AccountStatusTests.cs`
- `NhaTro.UnitTests/Features/Accounts/RoleResolutionTests.cs`
- `NhaTro.IntegrationTests/Features/Accounts/AccountLifecycleTests.cs`
- `NhaTro.IntegrationTests/Features/TestReadiness/P1P3RoleAccessTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixAccountsTests.cs`

---

### 6.2. Nhà trọ & phân công Quản lý

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh sách nhà | `/properties` | `Features/Properties/Pages/PropertyList.razor` | `OnInitializedAsync` (107), `OpenCreate` (205) |
| Tạo/sửa nhà | dialog | `Features/Properties/Components/PropertyFormDialog.razor` | `HandleSubmitAsync` (119) |
| Xem chi tiết | dialog | `Features/Properties/Components/PropertyDetailsDialog.razor` | `LoadAsync` (139), `LoadManagersAsync` (167) |
| Gán/thu hồi QL trong chi tiết | dialog | `PropertyDetailsDialog.razor` | `AssignManagerAsync` (205), `RevokeManagerAsync` (235) |
| Gán/thu hồi từ trang tài khoản | dialog | `ManagerAssignmentDialog.razor` | `AssignAsync` (145), `RevokeAsync` (177) |
| Xem phòng của nhà | nút | `PropertyList.razor` | `ViewRooms` (211) → `/rooms?propertyId=` |

#### Luồng code

```
PropertyList.razor
  → PropertyPageService
    → PropertyService.ListAsync/GetAsync/CreateAsync/UpdateAsync
      (Features/Properties/PropertyService.cs: 18 / 59 / 71 / 97)
      → PropertyScope.Apply (lọc theo OwnerId hoặc PropertyManagers)
      → bảng Properties (+ đếm Rooms)

ManagerAssignmentDialog.razor
  → PropertyManagerService.ListAssignmentsAsync / ListCandidatesAsync / AssignAsync / RevokeAsync
    (Features/Properties/Services/PropertyManagerService.cs: 17 / 60 / 104 / 184)
    → bảng PropertyManagers, Properties, AspNetUsers, AspNetUserRoles
```

Quy tắc nghiệp vụ:

- Mỗi QL thuộc **đúng 1 chủ**: `AssignAsync` chặn nếu QL đang được gán ở nhà của chủ khác (`ManagerOwnedByAnotherOwner`, dòng 142).
- Ứng viên (`ListCandidatesAsync`, 60) loại QL đã gán nhà này và QL đang thuộc chủ khác.
- Gán trùng → `AlreadyAssigned`. Gán user không phải QL → `InvalidManagerRole`.
- Thu hồi = set `RevokedAt`, **không xóa lịch sử**.
- Chỉ CT được gán/thu hồi (`Forbidden` nếu QL/AD gọi service).

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P2-01 | CT | Tạo nhà mới | Nhà thuộc `OwnerId` của CT; hiện trong danh sách |
| P2-02 | CT | Sửa tên/địa chỉ | Lưu, danh sách cập nhật |
| P2-03 | CT | Để trống tên | Lỗi validation, không lưu |
| P2-04 | QL | `/properties` | Chỉ thấy nhà được gán (hiện nhà Id=1) |
| P2-05 | QL | Tạo nhà | UI không có nút; gọi service → `Forbidden` |
| P2-06 | AD | `/properties` | 403 |
| P2-07 | CT | Mở chi tiết → "Quản lý được phân công" | Thấy QL hiện tại + nút thu hồi |
| P2-08 | CT | Gán QL mới | Thành công; QL đó thấy nhà ngay sau khi refresh |
| P2-09 | CT | Gán QL đã thuộc chủ khác | Lỗi `ManagerOwnedByAnotherOwner` |
| P2-10 | CT | Thu hồi QL | `RevokedAt` có giá trị; QL mất quyền thấy nhà/phòng/khách |
| P2-11 | CT | Gán lại QL vừa thu hồi | Thành công (bản ghi mới hoặc bản ghi cũ được mở lại — kiểm DB) |
| P2-12 | QL | Tự gán mình vào nhà khác | Không có UI; service `Forbidden` |
| P2-13 | AD | Xem cột chủ trọ ở `/admin/users` | Chỉ đọc, không có nút gán |

#### SQL kiểm chứng

```sql
SELECT p.Id, p.Name, p.Address, p.IsActive, p.IsDeleted, u.Email AS Owner
FROM Properties p LEFT JOIN AspNetUsers u ON u.Id = p.OwnerId;

SELECT pm.Id, pm.PropertyId, p.Name, m.Email AS Manager,
       a.Email AS AssignedBy, pm.AssignedAt, pm.RevokedAt, r.Email AS RevokedBy
FROM PropertyManagers pm
JOIN Properties p ON p.Id = pm.PropertyId
JOIN AspNetUsers m ON m.Id = pm.ManagerUserId
LEFT JOIN AspNetUsers a ON a.Id = pm.AssignedByUserId
LEFT JOIN AspNetUsers r ON r.Id = pm.RevokedByUserId
ORDER BY pm.PropertyId, pm.AssignedAt;
```

#### Test tự động

- `NhaTro.IntegrationTests/Features/Properties/RoomPropertyServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/TestReadiness/P1P3RoleAccessTests.cs` (gán/thu hồi, cross-owner)
- `NhaTro.UnitTests/Features/Properties/RoomPropertyDtoTests.cs`

---

### 6.3. Phòng & thùng rác phòng

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh sách phòng | `/rooms` | `Features/Rooms/Pages/RoomList.razor` | `OnInitializedAsync` (234), `OpenCreate` (470) |
| Tạo/sửa phòng | dialog | `Features/Rooms/Components/RoomFormDialog.razor` | `HandleSubmitAsync` (176) |
| Xem chi tiết | dialog | `Features/Rooms/Components/RoomDetailsDialog.razor` | — |
| Xóa mềm | nút thùng rác | `RoomList.razor` | `RequestDelete` (489), `DeleteRoomAsync` (500) |
| Thùng rác | `/rooms/trash` | `Features/Rooms/Pages/RoomTrash.razor` | — |

#### Luồng code

```
RoomList.razor
  → RoomPageService
    → RoomService: GetSummary (23), List (46), Get (74), Create (90),
                   Update (132), Delete (201), ListDeleted (229), Restore (259)
      → PropertyScope / ScopeRooms
      → IRoomChangePolicy (ManagerTogglePolicy) khi QL sửa
      → bảng Rooms, Properties, PropertyManagers, ManagerToggles
```

Quy tắc:

- **Tạo/xóa phòng: chỉ CT** (`EnsureCanManage`).
- QL được sửa phòng nhưng phải qua công tắc:
  - `SW-01` sửa thông tin, `SW-02` đổi giá, `SW-03` đổi trạng thái.
  - `Allow` → ghi ngay; `Approve` → trả `ApprovalRequired` (chưa ghi); `Deny` → `Forbidden`.
- Mã phòng duy nhất trong cùng nhà (`DuplicateRoomCode`).
- Xóa là **xóa mềm** (`IsDeleted`, `DeletedAt`, `DeletedBy`), phòng đang có hợp đồng hiệu lực bị chặn bởi `IRoomUsageChecker`.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P2-20 | CT | Tạo phòng P102 | Thành công, trạng thái Available |
| P2-21 | CT | Tạo trùng mã trong cùng nhà | Lỗi `DuplicateRoomCode` |
| P2-22 | CT | Tạo cùng mã ở nhà khác | Thành công |
| P2-23 | QL | Tạo phòng | Không có nút / `Forbidden` |
| P2-24 | QL | Sửa giá khi `SW-02 = Allow` | Lưu ngay |
| P2-25 | CT | Đổi `SW-02` sang `Approve` → QL sửa giá | QL nhận `ApprovalRequired`, giá **không đổi** |
| P2-26 | CT | Đổi `SW-02` sang `Deny` → QL sửa giá | `Forbidden` |
| P2-27 | CT | Xóa phòng trống | Vào thùng rác, biến mất khỏi danh sách |
| P2-28 | CT | Xóa phòng đang có hợp đồng Approved | Bị chặn |
| P2-29 | CT | Khôi phục từ `/rooms/trash` | Phòng trở lại danh sách |
| P2-30 | QL | `/rooms/trash` | 403 |
| P2-31 | QL | Xem phòng nhà khác | Không thấy / `RoomNotFound` |

#### SQL kiểm chứng

```sql
SELECT Id, PropertyId, Code, Floor, AreaM2, StandardPrice, Status,
       IsActive, IsDeleted, DeletedAt, DeletedBy
FROM Rooms ORDER BY PropertyId, Code;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Rooms/RoomServiceRulesTests.cs`
- `NhaTro.UnitTests/Features/Rooms/P2ResourceTests.cs`
- `NhaTro.UnitTests/Components/RoomComponentTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixRoomsTests.cs`

---

### 6.4. Khách thuê & thùng rác

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh sách | `/tenants` | `Features/Tenants/Pages/TenantList.razor` | `OnInitializedAsync` (213) |
| Xuất CSV | nút Export | `TenantList.razor` | `ExportAsync` (30) |
| Tạo/sửa | dialog | `Features/Tenants/Components/TenantFormDialog.razor` | `SubmitAsync` (233) |
| Chi tiết + giấy tờ | dialog | `Features/Tenants/Components/TenantDetailsDialog.razor` | `OnParametersSetAsync` (180) |
| Gắn tài khoản | dialog | `Features/Tenants/Components/TenantAccountLinkDialog.razor` | `LinkAsync` (118) |
| Bật/tắt hoạt động | dialog xác nhận | `TenantList.razor` | `ConfirmStatusChangeAsync` (479) |
| Xóa mềm | dialog | `TenantList.razor` | `OpenDeleteFromListAsync` (554) |
| Thùng rác | `/trash` | `Features/Tenants/Pages/TenantTrash.razor` | — |
| Khách tự xem phòng | `/tenant/my-room` | `Features/Tenants/Pages/MyLeasedRoomView.razor` | — |
| Khách tự xem hồ sơ | `/tenant/profile` | `Features/Tenants/Pages/MyTenantProfile.razor` | `SaveAsync` |
| Khách xem giấy tờ | `/documents` | `Features/Tenants/Pages/TenantDocumentsView.razor` | — |

#### Luồng code

```
TenantList.razor
  → TenantPageService
    → TenantService: List (51), Get (157), Create (244), Update (320),
                     SetActive (475), Delete (541), Restore (595),
                     ListTrash (652), LinkAccount (702), UnlinkAccount (763)
      → TenantScope (lọc theo hợp đồng/nhà)
      → CheckToggleAsync (SW-04 tạo, SW-05 sửa, SW-06 xóa)
      → bảng Tenants, Contracts, ContractMembers, Rooms, Properties, PropertyManagers
```

Quy tắc:

- CT/QL tạo khách; QL đi qua `SW-04`.
- Sửa hồ sơ QL đi qua `SW-05`.
- Xóa hồ sơ QL đi qua `SW-06` → nếu `Approve` thì tạo yêu cầu chờ CT duyệt.
- KT tự sửa hồ sơ: **không** kiểm trùng SĐT, không trả danh sách nghi trùng (`UpdateOwnAsync`, 422).
- Xóa là xóa mềm; khôi phục ở `/trash` (chỉ CT).
- CCCD chỉ hiện đầy đủ cho CT/QL trong phạm vi; KT xem của mình.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P3-01 | CT | Tạo khách mới | Lưu, hiện cuối danh sách |
| P3-02 | CT | Tạo trùng SĐT | Cảnh báo nghi trùng, có thể xác nhận tiếp |
| P3-03 | CT | Nhập CCCD sai định dạng | Lỗi, không lưu |
| P3-04 | QL | Tạo khách khi `SW-04 = Approve` | Trả `ApprovalRequired`, chưa tạo; yêu cầu xuất hiện ở `/approvals` của CT |
| P3-05 | CT | Duyệt yêu cầu tạo khách | Bản ghi khách được tạo |
| P3-06 | CT | Sửa khách | Lưu; có kiểm tra RowVersion (2 tab cùng sửa → báo xung đột) |
| P3-07 | CT | Gắn tài khoản cho khách | Chỉ chọn tài khoản KT chưa gắn hồ sơ nào |
| P3-08 | CT | Gỡ tài khoản | `Tenants.UserId = NULL` |
| P3-09 | CT | Tắt hoạt động khách | `IsActive = false`; khách không tạo hợp đồng mới được |
| P3-10 | CT | Xóa khách | Vào thùng rác; khách biến mất khỏi danh sách |
| P3-11 | CT | Khôi phục từ `/trash` | Khách trở lại |
| P3-12 | QL | Xóa khách khi `SW-06 = Approve` | Tạo yêu cầu, chưa xóa |
| P3-13 | CT | Export CSV | File CSV đúng cột, đúng phạm vi, không lỗi font |
| P3-14 | KT | `/tenant/profile` | Chỉ sửa hồ sơ mình; không thấy khách khác |
| P3-15 | KT | `/tenant/my-room` khi chưa có hợp đồng | Trạng thái "chưa thuê phòng", không lỗi |
| P3-16 | QL | Xem khách nhà khác | Không thấy |

#### SQL kiểm chứng

```sql
SELECT t.Id, t.FullName, t.Phone, t.Email, t.CitizenId, t.UserId,
       t.IsActive, t.IsDeleted, t.DeletedAt
FROM Tenants t ORDER BY t.Id;

SELECT c.ContractId, c.ContractNumber, r.Code AS Room, p.Name AS Property,
       c.Status, c.StartDate, c.EndDate
FROM Contracts c
JOIN Rooms r ON r.Id = c.RoomId
JOIN Properties p ON p.Id = r.PropertyId
ORDER BY c.StartDate DESC;

SELECT cm.ContractId, cm.TenantId, t.FullName
FROM ContractMembers cm JOIN Tenants t ON t.Id = cm.TenantId;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Tenants/TenantServiceTests.cs`
- `NhaTro.UnitTests/Features/Tenants/TenantCsvExporterTests.cs`
- `NhaTro.IntegrationTests/Features/Tenants/TenantScopeIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Tenants/TenantServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Tenants/TenantTrashIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Tenants/TenantRentalLinkIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixTenantsTests.cs`

---

### 6.5. Hợp đồng

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh sách | `/contracts` | `Features/Contracts/Pages/ContractListView.razor` | `LoadAsync` |
| Tạo | `/contracts/create` | `Features/Contracts/Pages/ContractCreateForm.razor` | `SubmitAsync` (159) |
| Chi tiết | `/contracts/{id}` | `Features/Contracts/Pages/ContractDetailView.razor` | `OnParametersSetAsync` |
| Duyệt/từ chối | dialog | `Features/Contracts/Components/ContractApproveModal.razor` | `DecideAsync` |
| Gia hạn/thanh lý | dialog | `Features/Contracts/Components/ContractLifecycleModal.razor` | `SubmitAsync` (79) |

#### Luồng code

```
ContractCreateForm.razor
  → ContractPageService.CreateAsync
    → ContractService.CreateContractAsync (Features/Contracts/Services/ContractService.cs: 561)
      → PropertyScope.Apply
      → kiểm phòng Available, không trùng lịch Pending/Approved
      → bảng Contracts, ContractMembers, Rooms
      → trigger TR_Contracts_PreventApprovedOverlap (migration 20261031014422)

ContractDetailView.razor
  → ContractService.GetAsync (153)
  → ApproveContractAsync (240) — chỉ CT, kiểm owner
  → RenewContractAsync (440) / LiquidateContractAsync (478) — qua SW-07
  → UpdateDepositAsync (513) — qua SW-08
```

Trạng thái (`ContractStatus`): `Pending (0) → Approved (1)` hoặc `Rejected (2)`; `Liquidated (3)`.

Quy tắc:

- `StartDate` không được ở tương lai; `StartDate < EndDate`; tối thiểu 1 tháng (`LeaseDurationRules`).
- Phòng phải `Available`, không có hợp đồng Pending/Approved trùng khoảng ngày.
- Tiền cọc mặc định = 20% giá phòng, làm tròn VND.
- Chỉ CT được duyệt/từ chối hợp đồng; lý do từ chối bắt buộc, tối đa 500 ký tự.
- QL tạo được hợp đồng nhưng không duyệt được.
- KT gửi yêu cầu gia hạn ở `/tenant/my-room` → `ContractRenewalService.SubmitAsync` (19) → `ContractRenewalRequests`.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P4-01 | CT | Tạo hợp đồng cho P101 + khách Id=1 | Tạo `Pending`, mã `HD-...`, cọc = 20% giá phòng |
| P4-02 | CT | Tạo hợp đồng thứ 2 cùng phòng, trùng ngày | Lỗi `PendingContractExists` / `RoomUnavailable` |
| P4-03 | CT | Tạo với ngày bắt đầu tương lai | Lỗi `FutureStartNotAllowed` |
| P4-04 | CT | Tạo với ngày kết thúc ≤ bắt đầu | Lỗi `InvalidDateRange` |
| P4-05 | CT | Tạo với phòng đang `Maintenance` | Lỗi `RoomNotAvailable` |
| P4-06 | CT | Duyệt hợp đồng | `Status = Approved`; phòng chuyển `Rented` |
| P4-07 | CT | Từ chối không nhập lý do | Lỗi `RejectReasonRequired` |
| P4-08 | CT | Từ chối có lý do | `Status = Rejected`, lý do lưu |
| P4-09 | QL | Duyệt hợp đồng | Không có nút / `Forbidden` |
| P4-10 | QL | Tạo hợp đồng nhà được gán | Thành công (Pending) |
| P4-11 | AD | `/contracts` | 403 |
| P4-12 | KT | Xem hợp đồng của mình | Chỉ thấy hợp đồng mình là thành viên; SĐT thành viên khác bị ẩn |
| P4-13 | KT | Gửi yêu cầu gia hạn | Tạo `ContractRenewalRequests.Status = Pending` |
| P4-14 | CT | Gia hạn khi `SW-07 = Deny` (áp cho QL) | CT vẫn làm được (CT bỏ qua công tắc) |
| P4-15 | CT | Thanh lý hợp đồng Approved | `Status = Liquidated`, phòng về `Available` |
| P4-16 | CT | Cập nhật tiền cọc | Lưu; QL đi qua SW-08 |
| P4-17 | CT | Tạo 2 hợp đồng Approved trùng phòng bằng SQL | Trigger `TR_Contracts_PreventApprovedOverlap` chặn |

#### SQL kiểm chứng

```sql
SELECT c.ContractId, c.ContractNumber, r.Code AS Room, p.Name AS Property,
       c.StartDate, c.EndDate, c.SnapshotPrice, c.DepositAmount, c.Status, c.RejectReason
FROM Contracts c
JOIN Rooms r ON r.Id = c.RoomId
JOIN Properties p ON p.Id = r.PropertyId
ORDER BY c.StartDate DESC;

SELECT * FROM ContractRenewalRequests ORDER BY RequestedAt DESC;

-- Kiểm trigger: không được có 2 Approved trùng phòng + trùng khoảng ngày
SELECT RoomId, COUNT(*) AS ApprovedCount
FROM Contracts WHERE Status = 1
GROUP BY RoomId HAVING COUNT(*) > 1;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Contracts/ContractDraftRulesTests.cs`
- `NhaTro.UnitTests/Features/Contracts/ContractLifecycleOperationsTests.cs`
- `NhaTro.UnitTests/Features/Contracts/LeaseDurationRulesTests.cs`
- `NhaTro.UnitTests/Features/Contracts/ContractServiceTests.cs`
- `NhaTro.IntegrationTests/Features/Contracts/ContractServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Contracts/ContractRenewalServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Contracts/ContractUiAccessTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixContractsTests.cs`

---

### 6.6. Dịch vụ & bảng giá

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh mục dịch vụ | `/services` | `Features/Utilities/Pages/ServiceCatalogView.razor` | `LoadDataAsync` (416) |
| Tạo dịch vụ | modal | `Features/Utilities/Components/ServiceCreateModal.razor` | `HandleCreateService` (555) |
| Cập nhật giá | modal | `Features/Utilities/Components/ServicePriceModal.razor` | `HandleSavePrice` (540) |
| Xem bậc giá | modal | `Features/Utilities/Components/ServiceTierModal.razor` | `OpenTierModal` (516) |
| Bảng giá | `/services/prices` | `Features/Utilities/Pages/ServicePricesView.razor` | `HandleSavePrice` (305) |

#### Luồng code

```
ServiceCatalogView.razor
  → ServiceCatalogPageService
    → ServiceCatalogService.GetServiceCatalogsAsync (26)
    → CreateOrUpdateServicePriceAsync (76/127)
    → ToggleServiceCatalogStatusAsync (217)
      → ServiceCatalogRules / UtilityDraftRules
      → bảng ServiceCatalogs, ServicePriceHistory, Properties, PropertyManagers
```

Quy tắc:

- Giá là **bất biến theo phiên bản**: thêm dòng mới trong `ServicePriceHistory`, không sửa dòng cũ.
- `EffectiveDate` bắt buộc; `EffectiveTo` phải sau `EffectiveFrom`; không chồng lấn phiên bản.
- CT quản lý mọi dịch vụ thuộc nhà mình; QL chỉ đọc/đề xuất theo công tắc.
- Điện nước dùng bảng `UtilityTariffs` (read-only, seed sẵn) — không sửa qua UI này.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P5-01 | CT | Tạo dịch vụ "Internet" | Tạo `ServiceCatalogs` + phiên bản giá đầu tiên |
| P5-02 | CT | Tạo dịch vụ thiếu tên / đơn giá ≤ 0 | Lỗi validation, không lưu |
| P5-03 | CT | Thêm giá mới cho dịch vụ | Tạo dòng `ServicePriceHistory` mới |
| P5-04 | CT | Đặt `EffectiveTo` trước `EffectiveFrom` | Lỗi validation |
| P5-05 | CT | Đặt ngày hiệu lực mới ≤ ngày hiệu lực bản mới nhất | Lỗi `ErrorEffectiveDateNotAfterLatest` |
| P5-06 | CT | Tắt dịch vụ | `IsActive = false`, không chọn được khi lập hóa đơn |
| P5-07 | QL | Mở `/services` | Chỉ xem (service `EnsureCanManage` chỉ cho CT), không có nút sửa |
| P5-08 | KT | `/services` | Chỉ xem, không có nút sửa |
| P5-09 | AD | `/services` | 403 |
| P5-10 | CT | Xem bậc giá điện | 3 bậc mặc định, tính đúng lũy tiến |

#### SQL kiểm chứng

```sql
SELECT sc.Id, sc.PropertyId, sc.IsActive, sc.IsReadOnly, sc.Description
FROM ServiceCatalogs sc ORDER BY sc.Id;

SELECT h.Id, h.ServiceCatalogId, h.Name, h.Unit, h.CalculationMethod,
       h.UnitPrice, h.EffectiveDate, h.EffectiveTo, h.Note
FROM ServicePriceHistory h ORDER BY h.ServiceCatalogId, h.EffectiveDate;

SELECT Id, Type, ScopeCode, Unit, IsPerResident, EffectiveDate, IsReadOnly
FROM UtilityTariffs ORDER BY Type, EffectiveDate;

SELECT UtilityTariffId, TierNumber, [From], [To], UnitPrice
FROM UtilityTariffTiers ORDER BY UtilityTariffId, TierNumber;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Utilities/ServiceCatalogRulesTests.cs`
- `NhaTro.UnitTests/Features/Utilities/ServiceCatalogServiceTests.cs`
- `NhaTro.UnitTests/Features/Utilities/UtilityCalculationRulesTests.cs`
- `NhaTro.UnitTests/Features/Utilities/UtilityTariffRulesTests.cs`
- `NhaTro.IntegrationTests/Features/Utilities/ServiceCatalogServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Utilities/UtilityTariffServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixUtilitiesTests.cs`

---

### 6.7. Chỉ số điện nước

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Nhập chỉ số | `/meter-readings` | `Features/Utilities/Pages/MeterReadingForm.razor` | `ReloadDataAsync`, chọn kỳ |
| Bảng nhập theo phòng | component | `Features/Utilities/Pages/MeterReadingView.razor` | `HandleSaveClick` (56) |
| Sửa kỳ gần nhất | modal lý do | `MeterReadingView.razor` | `ConfirmSaveWithReason` (460) |
| Redirect cũ | `/services/readings` | `MeterReadingRedirect.razor` | — |

#### Luồng code

```
MeterReadingForm.razor
  → MeterReadingPageService
    → UtilityReadingService.ViewAsync (66) / RecordAsync (143) / AdjustAsync (202) / HistoryAsync (261)
      → UtilityTariffService.CalculateForPeriodAsync (12)
      → UtilityTariffRules (chia kỳ theo ngày hiệu lực, bậc lũy tiến)
      → bảng UtilityReadings, UtilityReadingAudits, UtilityTariffs, UtilityTariffTiers, Contracts
```

Quy tắc:

- Chỉ CT/QL ghi chỉ số (`UtilityRecord`); KT chỉ xem phòng mình.
- Chỉ số đầu kỳ lấy từ chỉ số cuối kỳ trước; cuối kỳ phải ≥ đầu kỳ.
- Sửa chỉ số (D10): chỉ kỳ gần nhất, chưa có hóa đơn `Issued` phủ kỳ, **bắt buộc lý do**, ghi audit.
- QL sửa kỳ trước đi qua `SW-09`.
- Tính tiền: làm tròn từng bậc về VND, không làm tròn tổng.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P5-20 | CT | Ghi chỉ số P101 kỳ này | Lưu; điện/tiêu thụ = cuối − đầu |
| P5-21 | CT | Nhập chỉ số cuối < đầu | Lỗi `InvalidReading` |
| P5-22 | CT | Kỳ kết thúc < bắt đầu | Lỗi `InvalidPeriod` |
| P5-23 | CT | Ghi kỳ thứ 2 | Đầu kỳ = cuối kỳ trước |
| P5-24 | CT | Sửa kỳ gần nhất có lý do | Lưu + 1 dòng `UtilityReadingAudits` |
| P5-25 | CT | Sửa kỳ không phải gần nhất | Lỗi `NotAdjustable` |
| P5-26 | CT | Sửa kỳ đã có hóa đơn Issued | Lỗi (D10) |
| P5-27 | QL | Sửa chỉ số khi `SW-09 = Approve` | Tạo yêu cầu chờ duyệt, chưa ghi |
| P5-28 | KT | Xem chỉ số phòng mình | Chỉ phòng mình |
| P5-29 | KT | Ghi chỉ số | `Forbidden` |
| P5-30 | AD | `/meter-readings` | 403 |

#### SQL kiểm chứng

```sql
SELECT r.Id, r.RoomId, r.PeriodStart, r.PeriodEnd,
       r.ElectricStart, r.ElectricEnd, (r.ElectricEnd - r.ElectricStart) AS ElectricUsage,
       r.WaterStart, r.WaterEnd, (r.WaterEnd - r.WaterStart) AS WaterUsage,
       r.RecordedByUserId, r.CreatedAt, r.RowVersion
FROM UtilityReadings r ORDER BY r.PeriodStart DESC, r.RoomId;

SELECT a.* FROM UtilityReadingAudits a ORDER BY a.Id DESC;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Utilities/UtilityValidationRulesTests.cs`
- `NhaTro.UnitTests/Features/Utilities/UtilityCalculationRulesTests.cs`
- `NhaTro.UnitTests/Features/Utilities/MeterReadingPageServiceTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixUtilitiesTests.cs`

---

### 6.8. Hóa đơn & thanh toán

> ⚠️ **Hiện trạng**: tầng nghiệp vụ đã đủ nhưng **UI chưa nối** nút tạo/phát hành hóa đơn và nút gửi/xác nhận thanh toán. Vì vậy các ca dưới đây test qua **integration test** hoặc gọi service trong test; UI chỉ kiểm danh sách/chi tiết.

#### Đường đi UI

| Việc | Route | File |
|---|---|---|
| Danh sách (CT/QL) | `/invoices` | `Features/Invoices/Pages/InvoiceListView.razor` |
| Danh sách (KT) | `/my-invoices` | cùng file, nhánh `IsMyInvoices` |
| Chi tiết | `/invoices/{id}`, `/my-invoices/{id}` | `Features/Invoices/Pages/InvoiceDetailView.razor` |

#### Luồng code

```
InvoiceListView.razor
  → IInvoiceQueryService.GetInvoicesAsync (17) / GetInvoiceAsync (112)
    → lọc theo phạm vi CT/QL/KT
    → bảng Invoices, InvoiceItems, Contracts, ContractMembers, Rooms, Properties

(Invoices chưa có UI tạo)
  → IInvoiceService.CreateDraftAsync (54) / IssueAsync (177) / CreateAndIssueAsync (274)
    → InvoiceDraftRules, InvoiceCalculationRules, InvoicePeriodRules, InvoicePaymentRules
    → InvoiceCreateApprovalApplier / InvoiceIssueApprovalApplier (SW-10 / SW-11)
    → bảng Invoices, InvoiceItems, Payments

PaymentService.SubmitAsync (50) / ConfirmAsync (135) / RejectAsync (161)
  → bảng Payments; Confirm → cập nhật Invoices.Status = Paid
```

Quy tắc tiền (`InvoiceCalculationRules`):

- Làm tròn **từng dòng** về VND (`.5` làm tròn lên), rồi mới cộng.
- `TotalAmount = Σ item.Amount`; `PaidAmount` hiện được suy từ `InvoiceStatus` (Paid → bằng tổng, còn lại → 0), **không** cộng từ `Payments`; `Outstanding = Total − Paid`.
- Hóa đơn `Draft` chưa phát hành; `Issued` không hủy (D13). Theo thiết kế chỉ chuyển `Paid` khi xác nhận thanh toán, nhưng **hiện chưa có code chuyển trạng thái này** (xem khoảng trống #9).
- Kỳ hóa đơn theo tháng (`InvoicePeriodRules`), không chồng kỳ đã phát hành.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P6-01 | CT | Mở `/invoices` | Danh sách rỗng (chưa có hóa đơn), không lỗi |
| P6-02 | KT | Mở `/my-invoices` | Chỉ hóa đơn của mình |
| P6-03 | AD | Mở `/invoices` | 403 |
| P6-04 | Service | `CreateDraftAsync` cho hợp đồng Approved | Tạo `Draft`, items đúng nguồn |
| P6-05 | Service | Tạo 2 draft cùng kỳ/contract | Bị chặn (trùng kỳ) |
| P6-06 | Service | `IssueAsync` draft | `Status = Issued`, `IssuedAt` có giá trị |
| P6-07 | Service | Sửa hóa đơn Issued | Bị chặn |
| P6-08 | Service | KT gửi thanh toán | `Payments.Status = Pending` |
| P6-09 | Service | CT/QL xác nhận thanh toán | `Payments.Status = Confirmed`; hóa đơn **vẫn `Issued`** (xem khoảng trống #9) |
| P6-10 | Service | Từ chối thanh toán | `Rejected`, có lý do; hóa đơn vẫn nợ |
| P6-11 | Service | Gửi số tiền khác tổng hóa đơn (kể cả trả một phần) | Bị chặn `AmountMismatch` |
| P6-12 | UI | Mở chi tiết hóa đơn | Đúng items, tổng, đã trả, còn nợ; format VND |
| P6-13 | UI | Phân trang/lọc theo năm/tháng/trạng thái | Đúng số lượng, giữ filter khi đổi trang |

#### SQL kiểm chứng

```sql
SELECT i.Id, i.ContractId, i.PeriodStart, i.PeriodEnd, i.Status,
       i.TotalAmount, i.CreatedAt, i.IssuedAt,
       ISNULL(SUM(CASE WHEN p.Status = 1 THEN p.Amount ELSE 0 END), 0) AS Paid
FROM Invoices i LEFT JOIN Payments p ON p.InvoiceId = i.Id
GROUP BY i.Id, i.ContractId, i.PeriodStart, i.PeriodEnd, i.Status,
         i.TotalAmount, i.CreatedAt, i.IssuedAt
ORDER BY i.PeriodStart DESC;

SELECT * FROM InvoiceItems ORDER BY InvoiceId, DisplayOrder;
SELECT * FROM Payments ORDER BY RequestedAt DESC;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Invoices/InvoiceCalculationRulesTests.cs`
- `NhaTro.UnitTests/Features/Invoices/InvoiceDraftRulesTests.cs`
- `NhaTro.UnitTests/Features/Invoices/InvoicePaymentRulesTests.cs`
- `NhaTro.UnitTests/Features/Invoices/InvoicePeriodRulesTests.cs`
- `NhaTro.UnitTests/Features/Invoices/P6MoneyCaseTests.cs`
- `NhaTro.UnitTests/Features/Invoices/SubmitPaymentRequestTests.cs`
- `NhaTro.IntegrationTests/Features/Invoices/InvoiceServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Invoices/InvoiceQueryServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Invoices/P6MoneyTransactionIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixInvoicesTests.cs`

---

### 6.9. Sự cố bảo trì

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Danh sách + tạo | `/incidents` | `Features/Maintenance/Pages/Incidents.razor` | `InitializeAsync` (48) |
| Form báo sự cố | component | `Features/Maintenance/Components/IncidentReportForm.razor` | `SubmitAsync` (122) |
| Chi tiết + xử lý | modal | `Features/Maintenance/Components/IncidentDetailModal.razor` | — |
| Duyệt kết quả/ảnh | panel | `IncidentApprovalPanel.razor`, `IncidentApprovalPhotoRetry.razor` | — |
| Xem ảnh | modal | `ImageViewerModal.razor` | — |

#### Luồng code

```
Incidents.razor
  → IncidentPageService
    → IncidentService.RoomsAsync (31) / CreateAsync (56) / ListAsync (142) / GetAsync (174) / UpdateAsync (193)
    → IncidentPhotoService (upload ảnh, SW-14)
    → ManagerApprovalService (SW-12 tạo, SW-13 đổi trạng thái, SW-14 ảnh)
      → bảng MaintenanceIncidents, MaintenanceIncidentHistories, IncidentPhotoAttachments, StoredFiles
```

Quy tắc:

- KT báo sự cố phòng mình; CT/QL báo cho phòng trong phạm vi.
- Tiêu đề ≥ 5 ký tự, mô tả ≥ 10 ký tự, tối đa 3 ảnh (`IncidentRules`).
- QL tạo/đổi trạng thái/đính ảnh đi qua công tắc SW-12/13/14.
- Lịch sử thay đổi ghi vào `MaintenanceIncidentHistories` (note tối đa 2000 ký tự).

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P7-01 | KT | Báo sự cố phòng mình | Tạo `New`; CT/QL thấy |
| P7-02 | KT | Báo sự cố phòng người khác | Không thấy phòng / `Forbidden` |
| P7-03 | KT | Tiêu đề 3 ký tự | Lỗi validation |
| P7-04 | CT | Tạo sự cố | Tạo ngay |
| P7-05 | QL | Tạo sự cố khi `SW-12 = Approve` | Tạo yêu cầu chờ duyệt |
| P7-06 | CT | Duyệt yêu cầu tạo | Sự cố xuất hiện |
| P7-07 | CT | Chuyển `New → InProgress → Completed` | Trạng thái đúng, có lịch sử |
| P7-08 | QL | Đổi trạng thái khi `SW-13 = Allow` (hiện tại) | Ghi ngay |
| P7-09 | CT | Đính 3 ảnh | Lưu 3 `IncidentPhotoAttachments` |
| P7-10 | CT | Đính ảnh thứ 4 | Bị chặn |
| P7-11 | CT | Upload file > giới hạn / sai loại | Bị chặn, thông báo rõ |
| P7-12 | KT | Xem sự cố phòng mình | Chỉ sự cố của mình |
| P7-13 | AD | `/incidents` | 403 |

#### SQL kiểm chứng

```sql
SELECT Id, RoomId, ReportedByUserId, Title, Category, Urgency, Status,
       AssigneeName, RepairCost, CreatedAt, UpdatedAt
FROM MaintenanceIncidents ORDER BY CreatedAt DESC;

SELECT Id, IncidentId, Action, Note, CreatedAt FROM MaintenanceIncidentHistories ORDER BY Id;
SELECT Id, IncidentId, StoredFileId, Kind FROM IncidentPhotoAttachments;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Maintenance/IncidentRulesTests.cs`
- `NhaTro.UnitTests/Features/Maintenance/IncidentAccessTests.cs`
- `NhaTro.IntegrationTests/Features/Maintenance/IncidentServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixMaintenanceTests.cs`

---

### 6.10. Hàng đợi duyệt & 21 công tắc Quản lý

#### Đường đi UI

| Việc | Route | File | Handler |
|---|---|---|---|
| Hộp thư duyệt của CT | `/approvals` | `Components/Pages/ApprovalInbox.razor` → `ApprovalRequests.razor` | `LoadAsync` (80), `DecideAsync` (93) |
| Yêu cầu của QL | `/my-requests` | `Components/Pages/MyApprovalRequests.razor` | — |
| Công tắc | `/manager-switches` | `Components/Pages/ManagerSwitches.razor` | `LoadAsync` (84), `SaveAsync` (91) |

#### Luồng code

```
ManagerSwitches.razor
  → ManagerToggleService.GetAllAsync (18) / SetAsync (41) / GetModeAsync (104)
    → ManagerToggleCatalog.All (21 định nghĩa)
    → bảng ManagerToggles

ApprovalRequests.razor
  → ManagerApprovalService.ListInboxAsync (96) / ApproveAsync (169) / RejectAsync (230)
    → IApprovalApplier (theo OperationCode) → nghiệp vụ thật
    → bảng ManagerApprovalRequests + bảng nghiệp vụ liên quan
```

#### 21 công tắc (nguồn: `ManagerToggleCatalog.cs:35-58`)

| Mã | Tên | Nhóm | Mặc định | Chế độ cho phép | Hiện tại trong DB |
|---|---|---|---|---|---|
| SW-01 | Sửa thông tin phòng | P2 | Duyệt | Allow/Approve/Deny | Approve |
| SW-02 | Thiết lập / đổi giá phòng | P2 | Duyệt | 3 | Approve |
| SW-03 | Đổi trạng thái phòng | P2 | Duyệt | 3 | Approve |
| SW-04 | Tạo hồ sơ khách thuê | P3 | Duyệt | 3 | Approve |
| SW-05 | Sửa hồ sơ khách thuê | P3 | Duyệt | 3 | Approve |
| SW-06 | Xóa hồ sơ khách thuê | P3 | Duyệt | 3 | Approve |
| SW-07 | Gia hạn / thanh lý hợp đồng | P4 | Duyệt | 3 | Approve |
| SW-08 | Cập nhật tiền cọc | P4 | Duyệt | 3 | Approve |
| SW-09 | Sửa chỉ số kỳ liền trước | P5 | Duyệt | 3 | Approve |
| SW-10 | Tạo / lập hóa đơn | P6 | Duyệt | 3 | **Allow** |
| SW-11 | Phát hành hóa đơn | P6 | Duyệt | 3 | **Allow** |
| SW-12 | Tạo phiếu bảo trì | P7 | Duyệt | 3 | **Allow** |
| SW-13 | Cập nhật trạng thái xử lý sự cố | P7 | Duyệt | 3 | **Allow** |
| SW-14 | Đính kèm ảnh kết quả | P7 | Duyệt | 3 | Approve |
| SW-15 | Xem báo cáo | P8 | Cho phép | Allow/Deny | **Allow** |
| SW-16 | Xuất báo cáo (Excel) | P8 | Duyệt | 3 | Approve |
| SW-15a | Xem báo cáo — mục Phòng | P8 | Cho phép | Allow/Deny | **Allow** |
| SW-15b | Xem báo cáo — mục Khách thuê | P8 | Cho phép | Allow/Deny | **Allow** |
| SW-15c | Xem báo cáo — mục Hợp đồng | P8 | Cho phép | Allow/Deny | **Allow** |
| SW-15d | Xem báo cáo — mục Hóa đơn | P8 | Cho phép | Allow/Deny | **Allow** |
| SW-15e | Xem báo cáo — mục Doanh thu / công nợ | P8 | Cho phép | Allow/Deny | **Allow** |

> Mode: `0 = Allow`, `1 = Approve`, `2 = Deny` (`Features/Settings/Enums/ManagerToggleMode.cs`).
> CT **luôn** thao tác tức thì, không đọc công tắc (`ManagerTogglePolicy.cs:22-26`).

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P8-01 | CT | Mở `/manager-switches` | Thấy đủ 21 dòng, đúng chế độ hiện tại |
| P8-02 | CT | Đổi SW-01 sang Deny | Lưu ngay; QL sửa phòng bị chặn |
| P8-03 | CT | Đổi SW-01 sang Approve | QL sửa phòng → tạo yêu cầu |
| P8-04 | CT | Đổi SW-15a sang Deny | QL mất mục báo cáo Phòng |
| P8-05 | QL | Mở `/manager-switches` | 403 |
| P8-06 | QL | Thao tác bị duyệt | `/my-requests` hiện yêu cầu Pending |
| P8-07 | QL | Hủy yêu cầu của mình | Status `Cancelled`, không áp dụng nghiệp vụ |
| P8-08 | CT | `/approvals` duyệt | Applier chạy, nghiệp vụ được áp dụng, request `Approved` |
| P8-09 | CT | Từ chối kèm lý do | `Rejected`, lý do lưu, nghiệp vụ **không** đổi |
| P8-10 | CT | Duyệt yêu cầu SW chưa có applier | Bị chặn (fail closed), không im lặng bỏ qua |
| P8-11 | CT | Duyệt 2 lần cùng yêu cầu | Lần 2 báo đã xử lý / xung đột RowVersion |
| P8-12 | QL | Xem `/approvals` | 403 |

#### SQL kiểm chứng

```sql
SELECT Code, Mode, UpdatedAt, UpdatedByUserId FROM ManagerToggles ORDER BY Code;

SELECT Id, OperationCode, TargetType, TargetKey, PropertyId, Status,
       RequestedByUserId, RequestedAt, DecidedByUserId, DecidedAt, DecisionReason
FROM ManagerApprovalRequests ORDER BY RequestedAt DESC;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Settings/ManagerTogglePolicyTests.cs`
- `NhaTro.UnitTests/Features/Approvals/ApprovalRequestRulesTests.cs`
- `NhaTro.IntegrationTests/Features/Settings/ManagerToggleServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Approvals/ManagerApprovalServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixSettingsTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixApprovalsTests.cs`

---

### 6.11. Báo cáo, Dashboard, Trang chủ

#### Đường đi UI

| Việc | Route | File |
|---|---|---|
| Báo cáo phòng/khách | `/reports` | `Features/Reports/Pages/BusinessReportsView.razor` |
| Dashboard | `/dashboard` | `Features/Dashboard/Pages/DashboardView.razor` |
| Trang chủ | `/` | `Features/Home/Pages/HomeView.razor` |

#### Luồng code

```
BusinessReportsView.razor
  → DashboardPageService
    → IRoomReportService / ITenantReportService
      → lọc theo PropertyScope, filter phòng/khách, phân trang
      → bảng Rooms, Tenants, Contracts, ContractMembers, Properties

HomeView.razor
  → HomePageService.LoadAsync (48)
    → chọn nhánh theo vai trò:
       CT  : Room KPI + Tenant KPI + RevenueReportService + invoices + approvals + incidents + contracts
       QL  : phạm vi nhà được gán
       AD  : AccountService.GetSummaryAsync
       KT  : hợp đồng/phòng/hóa đơn của mình
    → RevenueReportService.GetAsync (27) đọc Payments.Confirmed (dòng 66-74)

DashboardView.razor
  → DashboardPageService.GetRoomKpisAsync / GetTenantKpisAsync
```

Quy tắc:

- Doanh thu chỉ tính `Payments.Status = Confirmed`; công nợ = tổng hóa đơn − đã thu.
- Báo cáo của QL bị giới hạn theo nhà được gán và công tắc SW-15/SW-15a…e.
- CT xem tất cả nhà mình.
- AD không vào dashboard/báo cáo nghiệp vụ.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P9-01 | CT | `/dashboard` | KPI phòng/khách khớp DB |
| P9-02 | CT | Lọc theo nhà | Số liệu chỉ của nhà đó |
| P9-03 | CT | `/reports` | Bảng phòng + khách, phân trang đúng |
| P9-04 | CT | Lọc trạng thái phòng | Đúng tập kết quả |
| P9-05 | QL | `/reports` | Chỉ nhà được gán |
| P9-06 | CT | Tắt SW-15a | QL không thấy mục báo cáo Phòng |
| P9-07 | KT | `/` | Chỉ KPI/hóa đơn của mình, không lộ dữ liệu khác |
| P9-08 | AD | `/` | Thấy tổng quan tài khoản, không có KPI doanh thu |
| P9-09 | CT | Trang chủ khi bảng `Payments` trống | Không lỗi; doanh thu = 0 |
| P9-10 | CT | Doanh thu sau khi có payment Confirmed | Tổng tiền tăng đúng |
| P9-11 | CT | Refresh | Số liệu cập nhật, không cache cũ |

#### SQL kiểm chứng

```sql
-- KPI phòng
SELECT PropertyId, Status, COUNT(*) AS SoLuong
FROM Rooms WHERE IsDeleted = 0 GROUP BY PropertyId, Status;

-- Doanh thu theo tháng
SELECT SUM(p.Amount) AS DoanhThu
FROM Payments p
WHERE p.Status = 1
  AND p.ReviewedAt >= '2026-11-01' AND p.ReviewedAt < '2026-12-01';

-- Công nợ
SELECT SUM(i.TotalAmount) - ISNULL(SUM(CASE WHEN p.Status = 1 THEN p.Amount END), 0) AS CongNo
FROM Invoices i LEFT JOIN Payments p ON p.InvoiceId = i.Id
WHERE i.Status = 1;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Reports/RoomReportFilterTests.cs`
- `NhaTro.UnitTests/Features/Reports/TenantReportFilterTests.cs`
- `NhaTro.UnitTests/Components/BusinessReportsPageTests.cs`
- `NhaTro.UnitTests/Components/DashboardPageTests.cs`
- `NhaTro.UnitTests/Components/DashboardComponentTests.cs`
- `NhaTro.IntegrationTests/Features/Reports/RoomReportServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Reports/TenantReportServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Home/HomePageTests.cs`
- `NhaTro.IntegrationTests/Features/PermissionMatrix/PermissionMatrixReportsTests.cs`

---

### 6.12. Giấy tờ khách thuê & API file

#### Đường đi UI

| Việc | Route | File |
|---|---|---|
| Khách xem/upload giấy tờ | `/documents` | `Features/Tenants/Pages/TenantDocumentsView.razor` |
| Upload trong hồ sơ khách | dialog | `Features/Tenants/Components/TenantDocumentUploadDialog.razor` |
| Danh sách giấy tờ trong chi tiết khách | dialog | `TenantDetailsDialog.razor` (198-232) |

#### Luồng code

```
TenantDocumentsView.razor
  → TenantDocumentPageService
    → Storage/FileService, LocalFileStorage
    → TenantDocumentCategoryRules, TenantDocumentPermissionRules, FileTypeRules, StorageLimits
    → bảng StoredFiles, FileAccessAuditLogs, Tenants

HTTP API:
  /api/files                     (FileEndpoints.cs: 20-28)
  /api/tenants/{tenantId}/documents (TenantDocumentEndpoints.cs: 22-31)
```

Quy tắc:

- KT chỉ upload giấy tờ, ảnh sự cố, minh chứng thanh toán của mình.
- Admin **luôn bị chặn** khỏi storage nghiệp vụ.
- Giới hạn dung lượng/loại file; kiểm magic bytes (`FileContentSniffer`).
- Tên lưu trữ sinh ngẫu nhiên, không dùng tên gốc trực tiếp (chống path traversal).
- Rate limit cho upload (`RateLimitPolicies.FileUpload`).
- Mỗi lần tải/ghi/xóa ghi `FileAccessAuditLogs`.

#### Ca test

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| P10-01 | KT | Upload CCCD của mình | Thành công, hiện trong `/documents` |
| P10-02 | KT | Tải giấy tờ của mình | Tải được |
| P10-03 | KT | Tải giấy tờ khách khác (đổi URL) | 403/404 |
| P10-04 | KT | Upload file `.exe` đổi đuôi `.jpg` | Bị chặn bởi sniff nội dung |
| P10-05 | KT | Upload quá dung lượng | Bị chặn, thông báo rõ |
| P10-06 | KT | Xóa giấy tờ | Xóa mềm/thành công theo quyền |
| P10-07 | CT | Xem giấy tờ khách nhà mình | Được |
| P10-08 | CT | Xem giấy tờ khách nhà khác | Không được |
| P10-09 | AD | Gọi `/api/files` | 403 |
| P10-10 | — | Upload 20 lần/phút | Bị rate limit |
| P10-11 | — | Tên file chứa `../` | Không thể ghi ra ngoài thư mục |

#### SQL kiểm chứng

```sql
SELECT Id, FileName, StoredFileName, RelativePath, ContentType, FileSizeBytes,
       FileCategory, UploadedByUserId, UploadedAt, RelatedEntityId, RelatedEntityType, IsDeleted
FROM StoredFiles ORDER BY UploadedAt DESC;

SELECT Id, StoredFileId, UserId, Action, OccurredAt FROM FileAccessAuditLogs ORDER BY Id DESC;
```

#### Test tự động

- `NhaTro.UnitTests/Features/Storage/FileTypeRulesTests.cs`
- `NhaTro.UnitTests/Features/Storage/FileContentSnifferTests.cs`
- `NhaTro.UnitTests/Features/Storage/StoragePermissionRulesTests.cs`
- `NhaTro.UnitTests/Features/Storage/LocalFileStorageTests.cs`
- `NhaTro.UnitTests/Features/Tenants/TenantDocumentPermissionRulesTests.cs`
- `NhaTro.IntegrationTests/Features/Storage/FileServiceIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Tenants/TenantDocumentScopeIntegrationTests.cs`
- `NhaTro.IntegrationTests/Features/Security/TenantDocumentRateLimitTests.cs`

---

### 6.13. Bảo mật nền tảng & vận hành

| Mã | Việc | Cách kiểm | Kết quả |
|---|---|---|---|
| S-01 | Rate limit đăng nhập | Sai mật khẩu liên tục | 429 sau ngưỡng cấu hình |
| S-02 | Captcha sau nhiều lần sai | Nhập sai 3-5 lần | Hiện captcha |
| S-03 | Cookie an toàn | DevTools → cookie auth | `HttpOnly`, `SameSite=Lax` |
| S-04 | Data Protection | Cấu hình `Security:ProtectKeysWithDpapi=true` thiếu path | App **không khởi động**, báo lỗi rõ |
| S-05 | Health live | `GET /health/live` | 200, không lộ chi tiết |
| S-06 | Health ready | `GET /health/ready` | 200/503 đúng DB |
| S-07 | Log nhạy cảm | Đăng nhập sai, upload CCCD | Log không chứa mật khẩu/CCCD đầy đủ |
| S-08 | JSON chặt | POST field lạ vào endpoint | 400 do `UnmappedMemberHandling.Disallow` |
| S-09 | Xóa dữ liệu cá nhân | Admin cuối tự xóa | Bị chặn `CannotRemoveLastAdmin` |

**Code**: `Program.cs:264-341` (security options, rate limit), `Infrastructure/Logging/*`, `Infrastructure/ErrorHandling/*`, `Infrastructure/Security/*`.

**Test tự động**: `Features/Security/*.cs` trong cả UnitTests và IntegrationTests; `DeploymentConfigurationTests.cs`.

---

## 7. Ma trận quyền tổng hợp

Ký hiệu: ✅ được phép · ❌ bị chặn · 👁 chỉ xem · ⚠ qua công tắc duyệt.

| Chức năng | AD | CT | QL | KT |
|---|---|---|---|---|
| Xem tổng quan tài khoản ở `/` | ✅ | ✅ | ✅ | ✅ (dữ liệu mình) |
| `/admin/users` | ✅ toàn bộ | ✅ QL+KT thuộc mình | ✅ mình + KT nhà gán | ❌ |
| Tạo tài khoản | ✅ mọi vai trò | ✅ QL, KT | ✅ KT (Chờ duyệt) | ❌ |
| Duyệt tài khoản | ✅ | ✅ QL, KT | ❌ | ❌ |
| Đổi vai trò | ✅ | ❌ | ❌ | ❌ |
| Khóa/mở tài khoản | ✅ (trừ Admin cuối) | ✅ QL, KT | ❌ | ❌ |
| Gán tòa nhà cho QL | 👁 | ✅ | ❌ | ❌ |
| Nhà trọ: xem | ❌ | ✅ nhà mình | ✅ nhà gán | ❌ |
| Nhà trọ: tạo/sửa | ❌ | ✅ | ❌ (service chặn tạo) | ❌ |
| Phòng: xem | ❌ | ✅ nhà mình | ✅ nhà gán | 👁 phòng đang thuê |
| Phòng: tạo/xóa | ❌ | ✅ | ❌ | ❌ |
| Phòng: sửa | ❌ | ✅ ngay | ⚠ SW-01/02/03 | ❌ |
| Khách thuê: xem | ✅ (qua tài khoản) | ✅ nhà mình | ✅ nhà gán | 👁 mình |
| Khách thuê: tạo/sửa/xóa | ❌ | ✅ | ⚠ SW-04/05/06 | 👁 tự sửa hồ sơ |
| Hợp đồng: xem | ❌ | ✅ nhà mình | ✅ nhà gán | ✅ của mình |
| Hợp đồng: tạo | ❌ | ✅ | ✅ | ❌ |
| Hợp đồng: duyệt/từ chối | ❌ | ✅ | ❌ | ❌ |
| Hợp đồng: gia hạn/thanh lý | ❌ | ✅ | ⚠ SW-07 | ❌ |
| Dịch vụ/giá: xem | ❌ | ✅ | ✅ | ✅ |
| Dịch vụ/giá: sửa | ❌ | ✅ | ⚠ theo công tắc | ❌ |
| Chỉ số: xem | ❌ | ✅ nhà mình | ✅ nhà gán | 👁 phòng mình |
| Chỉ số: ghi | ❌ | ✅ | ✅ | ❌ |
| Chỉ số: sửa kỳ trước | ❌ | ✅ | ⚠ SW-09 | ❌ |
| Hóa đơn: xem | ❌ | ✅ | ✅ | ✅ của mình |
| Hóa đơn: tạo/phát hành | ❌ | ✅ (service) | ⚠ SW-10/11 (service) | ❌ |
| Thanh toán: gửi | ❌ | ❌ | ❌ | ✅ (service) |
| Thanh toán: xác nhận | ❌ | ✅ (service) | ✅ (service) | ❌ |
| Sự cố: xem | ❌ | ✅ nhà mình | ✅ nhà gán | 👁 mình |
| Sự cố: tạo | ❌ | ✅ | ⚠ SW-12 | ✅ |
| Sự cố: đổi trạng thái | ❌ | ✅ | ⚠ SW-13 | ❌ |
| Ảnh kết quả | ❌ | ✅ | ⚠ SW-14 | 👁 |
| Công tắc Quản lý | ❌ | ✅ | ❌ | ❌ |
| Hàng đợi duyệt | ❌ | ✅ | 👁 `/my-requests` | ❌ |
| Báo cáo | ❌ | ✅ | ⚠ SW-15 + phạm vi | ❌ |
| Cấu hình hệ thống | ❌ | ❌ | ❌ | ❌ |

---

## 8. Kịch bản E2E tuần tự (happy path)

> Làm **đúng thứ tự** vì dữ liệu sau phụ thuộc dữ liệu trước. Mỗi bước ghi lại mã ca tương ứng.
> Nếu muốn reset: chạy lại `scripts/database/don_du_lieu_smoke.sql` (không xóa dữ liệu chính) rồi `chuan_hoa_du_lieu_chinh.sql`.

| # | Vai trò | Việc | Kỳ vọng | Ca |
|---|---|---|---|---|
| 1 | CT | Đăng nhập, mở `/properties` | Thấy "Nhà trọ Hòa Bình", 1 phòng | X-01, P2-01 |
| 2 | CT | Sửa địa chỉ nhà | Lưu thành công | P2-02 |
| 3 | CT | `/admin/users` → tạo QL `ql2@test.local` | QL mới active, 0 tòa | P1-02 |
| 4 | CT | Gán QL mới vào nhà Id=1 (chi tiết nhà) | QL thấy nhà | P2-08 |
| 5 | CT | `/admin/users` kiểm cột Chủ trọ | QL mới hiện owner CT | P1-14 |
| 6 | CT | Tạo phòng P102 | Có 2 phòng | P2-20 |
| 7 | CT | Tạo khách "Nguyễn Văn A" | Có 2 khách | P3-01 |
| 8 | CT | `/contracts/create` cho P102 + khách A | Hợp đồng Pending | P4-01 |
| 9 | CT | Mở hợp đồng → Duyệt | Approved, P102 Rented | P4-06 |
| 10 | CT | `/services` tạo "Internet" 100.000đ | Có dịch vụ + giá | P5-01 |
| 11 | CT | `/services/prices` thêm giá 120.000đ hiệu lực tháng sau | Có phiên bản mới | P5-03 |
| 12 | CT | `/meter-readings` ghi chỉ số P102 | Có reading | P5-20 |
| 13 | QL2 | Đăng nhập | Banner xanh, thấy nhà + phòng + khách | X-31, P2-04 |
| 14 | QL2 | Thử tạo khách (SW-04 = Approve) | Tạo yêu cầu chờ duyệt | P3-04 |
| 15 | CT | `/approvals` duyệt yêu cầu tạo khách | Khách mới xuất hiện | P8-08 |
| 16 | QL2 | Sửa giá phòng (SW-02 = Approve) | Yêu cầu chờ duyệt, giá chưa đổi | P2-25 |
| 17 | CT | `/manager-switches` đổi SW-02 = Deny | Lưu | P8-02 |
| 18 | QL2 | Sửa giá phòng lại | Bị chặn | P2-26 |
| 19 | CT | Đổi SW-02 = Allow | Lưu | P8-03 |
| 20 | QL2 | Sửa giá phòng | Lưu ngay | P2-24 |
| 21 | KT (khachthue) | Đăng nhập, `/tenant/profile` | Xem/sửa hồ sơ mình | P3-14 |
| 22 | KT | `/tenant/my-room` | Chưa có hợp đồng → trạng thái trống | P3-15 |
| 23 | KT | `/incidents` báo sự cố | Sự cố mới | P7-01 |
| 24 | CT | `/incidents` xử lý → Completed | Trạng thái + lịch sử | P7-07 |
| 25 | CT | `/` và `/reports` | KPI/ báo cáo khớp DB | P9-01, P9-03 |

**Sau E2E, kiểm DB**: `Contracts` có 1 Approved, `Rooms` P102 = Rented, `MaintenanceIncidents` có 1 Completed, `ManagerApprovalRequests` có ≥ 1 Approved, `ManagerToggles.SW-02 = 0`.

---

## 9. Test âm tổng hợp

| Mã | Tình huống | Kỳ vọng |
|---|---|---|
| N-01 | QL/AD gọi service nghiệp vụ của CT | `Forbidden` |
| N-02 | Truy cập nhà/phòng/khách ngoài phạm vi | `NotFound` / `Forbidden` (không lộ dữ liệu) |
| N-03 | Gán QL thuộc chủ khác | `ManagerOwnedByAnotherOwner` |
| N-04 | Tạo hợp đồng trùng phòng/ngày | `PendingContractExists` / `RoomUnavailable` |
| N-05 | Duyệt hợp đồng không phải chủ sở hữu | `Forbidden` |
| N-06 | Từ chối hợp đồng không lý do | `RejectReasonRequired` |
| N-07 | Sửa chỉ số đã có hóa đơn | `NotAdjustable` |
| N-08 | Xóa phòng đang có hợp đồng | Bị chặn |
| N-09 | Upload sai loại/kích thước | Bị chặn, thông báo rõ |
| N-10 | Duyệt yêu cầu SW chưa có applier | Fail closed |
| N-11 | Khóa Admin cuối | `CannotLockLastAdmin` |
| N-12 | Đổi vai trò tài khoản đang gắn hồ sơ khách | `RoleLinkedToTenant` |
| N-13 | Tạo tài khoản trùng email | `DuplicateEmail` |
| N-14 | Mật khẩu yếu | `WeakPassword` |
| N-15 | Tạo 2 bản ghi cùng lúc (đua tranh) | Unique index/trigger chặn, trả lỗi thân thiện |
| N-16 | Sửa form ở 2 tab | Báo xung đột RowVersion, không ghi đè |
| N-17 | Mở URL không tồn tại | Trang `NotFound` |
| N-18 | DB mất kết nối | Trang lỗi thân thiện, log đầy đủ |

---

## 10. Kiểm chứng SQL

Chạy bằng:
```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -d NhaTroDb -W -s "|"
```

### 10.1. Toàn cảnh dữ liệu

```sql
SELECT 'Properties' AS Bang, COUNT(*) AS SoDong FROM Properties
UNION ALL SELECT 'Rooms', COUNT(*) FROM Rooms
UNION ALL SELECT 'Tenants', COUNT(*) FROM Tenants
UNION ALL SELECT 'Contracts', COUNT(*) FROM Contracts
UNION ALL SELECT 'ContractMembers', COUNT(*) FROM ContractMembers
UNION ALL SELECT 'Invoices', COUNT(*) FROM Invoices
UNION ALL SELECT 'InvoiceItems', COUNT(*) FROM InvoiceItems
UNION ALL SELECT 'Payments', COUNT(*) FROM Payments
UNION ALL SELECT 'MaintenanceIncidents', COUNT(*) FROM MaintenanceIncidents
UNION ALL SELECT 'ServiceCatalogs', COUNT(*) FROM ServiceCatalogs
UNION ALL SELECT 'ServicePriceHistory', COUNT(*) FROM ServicePriceHistory
UNION ALL SELECT 'UtilityReadings', COUNT(*) FROM UtilityReadings
UNION ALL SELECT 'ManagerApprovalRequests', COUNT(*) FROM ManagerApprovalRequests
UNION ALL SELECT 'ManagerToggles', COUNT(*) FROM ManagerToggles
UNION ALL SELECT 'StoredFiles', COUNT(*) FROM StoredFiles
UNION ALL SELECT 'AuditLogEntries', COUNT(*) FROM AuditLogEntries;
```

### 10.2. Bất biến dữ liệu (data invariants)

```sql
-- 1. Không có 2 assignment hiệu lực cho cùng (PropertyId, ManagerUserId)
SELECT PropertyId, ManagerUserId, COUNT(*) AS SoDong
FROM PropertyManagers WHERE RevokedAt IS NULL
GROUP BY PropertyId, ManagerUserId HAVING COUNT(*) > 1;

-- 2. Mỗi QL chỉ thuộc 1 chủ (qua các nhà đang gán)
SELECT pm.ManagerUserId, COUNT(DISTINCT p.OwnerId) AS SoChu
FROM PropertyManagers pm JOIN Properties p ON p.Id = pm.PropertyId
WHERE pm.RevokedAt IS NULL
GROUP BY pm.ManagerUserId HAVING COUNT(DISTINCT p.OwnerId) > 1;

-- 3. Không có 2 hợp đồng Approved trùng phòng + trùng khoảng ngày
SELECT a.ContractId, b.ContractId AS TrungVoi
FROM Contracts a JOIN Contracts b
  ON a.RoomId = b.RoomId AND a.ContractId < b.ContractId
 AND a.Status = 1 AND b.Status = 1
 AND a.StartDate <= b.EndDate AND b.StartDate <= a.EndDate;

-- 4. Hóa đơn Paid phải có tổng thanh toán Confirmed >= TotalAmount
SELECT i.Id, i.TotalAmount, ISNULL(SUM(p.Amount), 0) AS DaThu
FROM Invoices i LEFT JOIN Payments p ON p.InvoiceId = i.Id AND p.Status = 1
WHERE i.Status = 2
GROUP BY i.Id, i.TotalAmount
HAVING ISNULL(SUM(p.Amount), 0) < i.TotalAmount;

-- 5. Không có user mồ côi khỏi role
SELECT u.Email FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
WHERE ur.UserId IS NULL;
```

### 10.3. Truy vết một thao tác

Ví dụ gán QL:
```sql
SELECT TOP 5 * FROM PropertyManagers ORDER BY AssignedAt DESC;
```
Ví dụ duyệt yêu cầu:
```sql
SELECT TOP 10 * FROM ManagerApprovalRequests ORDER BY RequestedAt DESC;
```
Ví dụ upload file:
```sql
SELECT TOP 10 * FROM StoredFiles ORDER BY UploadedAt DESC;
SELECT TOP 10 * FROM FileAccessAuditLogs ORDER BY Id DESC;
```

---

## 11. Test tự động

### 11.1. Chạy

```powershell
# Toàn bộ
dotnet test NhaTro/NhaTro.slnx

# Unit test (nhanh nhất)
dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj

# Integration test (cần LocalDB)
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj

# E2E (Playwright, nếu đã cài browser)
dotnet test NhaTro/NhaTro.E2ETests/NhaTro.E2ETests.csproj
```

### 11.2. Bản đồ test theo module

| Module | Unit | Integration |
|---|---|---|
| Tài khoản | `Features/Accounts/*` | `Features/Accounts/AccountLifecycleTests`, `PermissionMatrixAccountsTests` |
| Nhà trọ/phòng | `Features/Rooms/*`, `Features/Properties/*` | `RoomPropertyServiceIntegrationTests`, `PermissionMatrixRoomsTests` |
| Khách thuê | `Features/Tenants/*` | `TenantScope`, `TenantService`, `TenantTrash`, `TenantRentalLink`, `PermissionMatrixTenantsTests` |
| Hợp đồng | `Features/Contracts/*` | `ContractService`, `ContractRenewalService`, `ContractUiAccess`, `PermissionMatrixContractsTests` |
| Dịch vụ | `Features/Utilities/*` | `ServiceCatalogService`, `ServiceCatalogPageService`, `UtilityTariffService`, `PermissionMatrixUtilitiesTests` |
| Hóa đơn | `Features/Invoices/*` | `InvoiceService`, `InvoiceQueryService`, `P6MoneyTransaction`, `PermissionMatrixInvoicesTests` |
| Sự cố | `Features/Maintenance/*` | `IncidentService`, `PermissionMatrixMaintenanceTests` |
| Duyệt/công tắc | `Features/Approvals/*`, `Features/Settings/*` | `ManagerApprovalService`, `ManagerToggleService`, `PermissionMatrixApprovals/SettingsTests` |
| Báo cáo | `Features/Reports/*`, `Components/BusinessReportsPageTests` | `RoomReportService`, `TenantReportService`, `HomePageTests`, `PermissionMatrixReportsTests` |
| Storage/giấy tờ | `Features/Storage/*`, `Features/Tenants/TenantDocument*` | `FileService`, `TenantDocumentScope`, `IncidentFileScope` |
| Bảo mật | `Infrastructure/Security/*`, `Features/Security/*` | `Security/*`, `LoginCaptcha`, `LoginRateLimit`, `DeploymentConfiguration` |

### 11.3. Lưu ý chạy test

- Integration test tạo DB riêng mỗi lần chạy (`NhaTro_It_<guid>`), tự xóa sau khi xong.
- Nếu không có LocalDB → test tự `Skip` (xem `SqlServerFixture.Available`).
- `P2ResourceTests` cần output path **trong repo**; build ra thư mục tạm ngoài repo sẽ fail vì không tìm thấy `Web/Resources/Shared.*.resx`.
- E2E cần Playwright browser đã cài (`npx playwright install`).

---

## 12. Khoảng trống đã biết (đừng báo là bug khi test)

| # | Khoảng trống | Ảnh hưởng | Cách xử lý khi test |
|---|---|---|---|
| 1 | Chưa có UI tạo/phát hành hóa đơn | `/invoices` chỉ đọc | Test qua integration test/service |
| 2 | Chưa có UI gửi/xác nhận thanh toán | Không thao tác được trên web | Test qua integration test/service |
| 3 | `ReportExportService` đã đăng ký DI nhưng chưa nối nút export | Không có nút xuất Excel trên `/reports` | Ghi nhận, không test UI |
| 4 | Menu `Permissions` (Admin) không có route | Mục menu ở trạng thái `unavailable` | Bỏ qua |
| 5 | Không có trang đăng ký công khai | Chỉ tạo tài khoản qua quản trị | Dùng `/admin/users` |
| 6 | Admin không vào nghiệp vụ (theo thiết kế D1) | 403 ở nhà/phòng/hợp đồng/hóa đơn | Đúng thiết kế |
| 7 | `name123@gmail.com` là QL chưa có chủ | Chỉ test banner cảnh báo | Không gán, hoặc thu hồi `quanly` để test |
| 8 | Hợp đồng/hóa đơn/chỉ số = 0 sau khi dọn | Không thể test ngay | Chạy kịch bản E2E mục 8 |
| 9 | Chưa có code chuyển `Invoices.Status → Paid` sau khi xác nhận thanh toán | Hóa đơn vẫn `Issued`; `PaidAmount` trên UI = 0 dù đã có payment Confirmed | Ghi nhận; doanh thu vẫn đúng vì báo cáo cộng `Payments.Confirmed` |
| 10 | Chưa chặn trùng tên dịch vụ trong cùng nhà | Có thể tạo 2 `ServiceCatalogs` cùng tên | Ghi nhận, không báo bug |
| 11 | Bộ đếm chặn IP không áp cho bước nhập mã 2FA | Chỉ đếm sai ở bước mật khẩu; rate limiter 10 POST/phút vẫn áp cho `/Account/LoginWith2fa` | Ghi nhận; muốn siết thì bổ sung đếm ở trang 2FA |
| 12 | Bộ đếm chặn IP lưu trong bộ nhớ tiến trình | Restart app hoặc chạy nhiều instance thì bộ đếm reset/không chia sẻ | Với 1 server (hiện tại) là đủ; scale-out cần cache phân tán |

---

## 13. Checklist tổng

### 13.1. Trước khi test

- [ ] `NhaTroDb` đã migrate đủ (30 migration, có `Payments`, `UtilityReadings`, `AuditLogEntries`)
- [ ] App chạy Development, `http://localhost:5177`
- [ ] 4 tài khoản dev đăng nhập được
- [ ] `quanly@nhatro.local` đang gán nhà Id=1
- [ ] 21 công tắc + 5 bảng giá điện nước còn nguyên

### 13.2. Theo module

- [ ] P1 Tài khoản: tạo/duyệt/đổi vai trò/khóa/phạm vi
- [ ] P2 Nhà trọ: CRUD + gán/thu hồi QL + cross-owner
- [ ] P2 Phòng: CRUD + thùng rác + công tắc SW-01/02/03
- [ ] P3 Khách thuê: CRUD + gắn tài khoản + thùng rác + CSV + công tắc SW-04/05/06
- [ ] P4 Hợp đồng: tạo/duyệt/từ chối/gia hạn/thanh lý/cọc + trigger trùng
- [ ] P5 Dịch vụ: tạo/giá phiên bản/bậc giá/tắt bật
- [ ] P5 Chỉ số: ghi/sửa có lý do/chặn hóa đơn/audit
- [ ] P6 Hóa đơn: tạo draft/phát hành/thanh toán/xác nhận (service)
- [ ] P7 Sự cố: tạo/duyệt/trạng thái/ảnh/lịch sử
- [ ] P8 Duyệt: inbox/my-requests/approve/reject/fail-closed + 21 công tắc
- [ ] P9 Báo cáo/Dashboard/Home: KPI/lọc/phân trang/doanh thu
- [ ] P10 Giấy tờ: upload/tải/xóa/quyền/rate limit/audit
- [ ] Bảo mật: captcha/chặn IP/rate limit/health/log

### 13.3. Sau khi test

- [ ] Không còn exception trong log
- [ ] Dữ liệu test không rác vào `NhaTroDb` (hoặc đã dọn)
- [ ] Các invariant ở 10.2 trả về 0 dòng
- [ ] `dotnet test NhaTro/NhaTro.slnx` xanh

---

## 14. Mẫu ghi kết quả

```
Mã ca: P4-06
Thời điểm: 2026-11-06 14:30
Vai trò: chutro@nhatro.local
Dữ liệu vào: ContractId=..., phòng P102
Bước làm: /contracts → mở HD-... → Duyệt
Kết quả thực tế: Status=Approved, phòng P102=Rented
SQL đối chiếu: SELECT Status FROM Contracts WHERE ContractId='...' → 1
Kết luận: PASS
Ghi chú: ...
```
