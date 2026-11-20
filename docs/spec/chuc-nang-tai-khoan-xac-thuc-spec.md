# SPEC — Tài khoản & xác thực (hiện trạng)

> Ghi lại hành vi **đang có trong code** (đối chiếu 20/11/2026). Không gồm đề xuất/kế hoạch.
> Nền: NhaTro Blazor .NET 10, ASP.NET Identity, EF Core SQL Server; vai trò `Admin`, `ChuTro`, `QuanLy`, `KhachThue`.

## 0. Quy tắc chung

| Mục | Hiện trạng |
|---|---|
| Tài khoản | `ApplicationUser : IdentityUser`; `UserName` = email, email duy nhất |
| Một vai trò hoạt động | Claim chỉ phát role đang chọn; danh sách ngữ cảnh nằm ở claim riêng (mục 1.2) |
| Kiểm quyền | Policy/attribute ở trang hoặc endpoint + service nhận `callerUserId`/`callerRole` và tự kiểm quyền, phạm vi |
| Ghi vết | Service ghi `ILogger`; sổ tài khoản (file JSONL) chỉ nhận `account.purge`, `OwnerLocked`, `OwnerUnlocked` |
| Thông báo | Thao tác tài khoản **không** tạo `AppNotification`; chỉ email/OTP |

## 1. Vai trò, ngữ cảnh, trạng thái

### 1.1 Bốn vai trò

| Vai trò | Nguồn tài khoản | Phạm vi |
|---|---|---|
| `Admin` | Chỉ có từ seed; Admin không tạo/nâng vai trò thành Admin | Toàn hệ thống |
| `ChuTro` | Tự đăng ký (`/Account/RegisterOwner`) hoặc Admin tạo | Nhà mình sở hữu |
| `QuanLy` | Admin/Chủ trọ tạo | Tòa được phân công |
| `KhachThue` | Tạo kèm hồ sơ khách, hoặc nhận lời mời (email/QR) | Hồ sơ khách của mình |

### 1.2 Ngữ cảnh hoạt động

- Ngữ cảnh khả dụng = role thật trong `AspNetUserRoles`, cộng thêm `KhachThue` nếu có hồ sơ `Tenant.UserId` (không cần cấp role).
- Claim chỉ giữ **một** role đang chọn (`ActiveRoleCookie`), kèm `nhatro.available_roles`.
- Một ngữ cảnh → vào thẳng sau đăng nhập; nhiều ngữ cảnh → `/select-role` (chọn xong `RefreshSignInAsync`).
- Nếu tài khoản thuộc **bất kỳ** chủ trọ đang bị khoá, ngữ cảnh Quản lý/Khách thuê bị loại khỏi danh sách (kể cả khi còn chủ trọ khác); hết ngữ cảnh dùng được thì `CanSignInAsync` trả `false`.

### 1.3 Trạng thái tài khoản

| Trạng thái | Điều kiện | Đăng nhập |
|---|---|---|
| `Unverified` | `EmailConfirmed = false` | Được vào nhưng chỉ tới trang OTP |
| `Pending` | Đã xác thực email, `ApprovedAt = null` | Bị chặn (`NotAllowed`) |
| `Active` | Đã xác thực email, `ApprovedAt ≠ null`, không khoá | Bình thường |
| `Locked` | `LockoutEnd = DateTimeOffset.MaxValue` | Bị chặn |
| Thùng rác | `IsDeleted = true` | Bị chặn |

Ưu tiên suy trạng thái: **Locked > Unverified > Pending > Active** (`AccountStatusRules.Derive`). Khoá tạm do sai mật khẩu không phải `Locked`.

### 1.4 Ma trận thao tác

| Thao tác | Admin | Chủ trọ | Quản lý |
|---|---|---|---|
| Tạo tài khoản | ChuTro, QuanLy, KhachThue | QuanLy, KhachThue (hiệu lực ngay) | KhachThue (Pending) |
| Duyệt | Mọi vai trò | QuanLy, KhachThue trong phạm vi | Không |
| Khoá / mở khoá | Mọi vai trò, trừ tự khoá và Admin cuối | Không | Không |
| Đổi vai trò | Có; không nâng lên Admin, không tự đổi, không gỡ Admin cuối, không đổi tài khoản đang gắn hồ sơ khách | Không | Không |
| Xoá mềm | Có; trừ tự xoá, Admin khác, tài khoản còn hồ sơ khách | Không | Không |
| Khôi phục | Có | Service cho phép trong phạm vi, nhưng danh sách thùng rác chỉ trả cho Admin | Không |
| Xoá vĩnh viễn / dọn thùng rác | Có; chặn khi còn dữ liệu tham chiếu | Không | Không |

Quản lý không có quyền duyệt; danh sách tài khoản của Quản lý gồm chính mình + khách có hợp đồng (kể cả lịch sử) tại tòa được giao.

## 2. Đăng nhập

### 2.1 Luồng

1. `/login` hoặc `/Account/Login`: email + mật khẩu + "ghi nhớ đăng nhập"; có hiện/ẩn mật khẩu.
2. Sai email/mật khẩu → thông báo chung, không phân biệt lỗi.
3. Đúng → xét theo thứ tự:
   - `EmailConfirmed = false` → `/Account/VerifyOtp?purpose=FirstLogin` (kèm `returnUrl`).
   - Cần 2FA → `/Account/LoginWith2fa`.
   - Bị khoá → `/Account/Lockout`.
   - `NotAllowed` (Pending) → báo "đang chờ duyệt".
   - Còn lại → `PostLoginRouter`: một ngữ cảnh vào `ReturnUrl`/`/home`, nhiều ngữ cảnh về `/select-role`.
4. `ReturnUrl` chỉ nhận đường dẫn nội bộ; chặn `//` và URL tuyệt đối.
5. `/login` vẫn mở khi đang đăng nhập (để đổi tài khoản/ngữ cảnh).

### 2.2 Chống dò

| Cơ chế | Hiện trạng |
|---|---|
| Đếm sai theo IP | Sai 10 lần/15 phút từ một IP → chặn IP 15 phút; không khoá tài khoản |
| CAPTCHA | Sau 3 lần sai của tài khoản phải giải thử thách; sai CAPTCHA cũng tính một lần sai |
| Rate limit | POST đăng nhập/2FA 10 request/phút/IP; vượt → 303 về `/login?tooManyRequests=1` để form báo lỗi |
| Lockout Identity | Cấu hình 5 lần/15 phút nhưng luồng đăng nhập dùng `lockoutOnFailure: false`; khoá chỉ do Admin |
| Anti-forgery | POST `/login`/`/Account/Login` trả 400 → chuyển `/login?stale=1` để tải lại form |

### 2.3 Phiên

- Cookie `Identity.Application`: HttpOnly, SameSite=Lax; Secure `Always` (production), `SameAsRequest` (Development/Testing).
- Hết hạn 8 giờ, sliding; security stamp kiểm lại mỗi 2 phút.
- Đăng xuất: POST `/Account/Logout`, kiểm antiforgery thủ công (thiếu token vẫn kết thúc phiên), trả `Clear-Site-Data: "cache", "cookies", "storage"`.
- `/api/*`: thiếu đăng nhập 401, thiếu quyền 403 (không redirect).

## 3. Đăng ký & tạo tài khoản

### 3.1 Chủ trọ tự đăng ký

1. `/Account/RegisterOwner`: họ tên, email, SĐT (tuỳ chọn, 10 số bắt đầu bằng 0), mật khẩu + xác nhận.
2. Service tạo user `EmailConfirmed = false`, `ApprovedAt = null`, gán role `ChuTro`; trùng email báo lỗi.
3. Service phát OTP `OwnerRegistration`; trang chuyển `/Account/VerifyOtp`.
4. OTP đúng → `EmailConfirmed = true`, `ApprovedAt ??= now`, cập nhật security stamp → tài khoản `Active`.
5. Rate limit đăng ký: 5 request/giờ/IP.

### 3.2 Khách thuê qua lời mời

| Kênh | Hiện trạng |
|---|---|
| Email | Chỉ mời khách **mới**; hạn 15 phút; email đã có tài khoản bị từ chối; lời mời cũ cùng email/chủ trọ bị thu hồi |
| QR | Chọn hạn 15 phút / 1 giờ / 6 giờ / 24 giờ; mời khách mới hoặc khách đã có tài khoản; gắn chủ trọ + tòa/phòng |

- Token 32 byte ngẫu nhiên; server chỉ lưu **SHA-256**; dùng một lần; hết hạn hoặc đã thu hồi → trang lỗi.
- **Khách mới**: `/invite/{Token}` tạo tài khoản (`EmailConfirmed = false`, `ApprovedAt = now`), role `KhachThue`, hồ sơ khách, liên kết chủ trọ và xếp phòng ở trạng thái chờ (nếu có phòng); sau đó OTP `TenantRegistration` để xác thực email.
- **Khách đã có tài khoản**: phải đăng nhập đúng người được mời; hệ thống tạo hồ sơ khách nếu chưa có, thêm liên kết chủ trọ và xếp phòng chờ; `RefreshSignInAsync` để nhận ngữ cảnh mới.
- Khoá chủ trọ → thu hồi mọi lời mời đang mở của chủ trọ đó.

### 3.3 Tạo tài khoản bởi người quản lý

- Admin/Chủ trọ tạo `QuanLy`/`KhachThue`: `ApprovedAt = now`, `EmailConfirmed = false` → lần đầu đăng nhập vẫn qua OTP.
- Quản lý tạo `KhachThue`: `ApprovedAt = null` (Pending), chỉ trong phạm vi tòa được giao.
- Mọi thao tác tạo `KhachThue` phải chọn hồ sơ khách **chưa gắn tài khoản** (`Tenant.UserId = null`) trong phạm vi; Chủ trọ/Quản lý chỉ chọn được hồ sơ đã có hợp đồng tại tòa trong phạm vi. Gắn hồ sơ bằng cập nhật có điều kiện, thua đua thì xoá tài khoản vừa tạo.
- Hiện trạng cần biết: tài khoản Quản lý tạo có `EmailConfirmed = false`; luồng đăng nhập kiểm email trước khi kiểm `ApprovedAt`, và OTP thành công đặt `ApprovedAt ??= now` — nên sau khi xác thực email, tài khoản chuyển `Active` dù chưa bấm duyệt.

### 3.4 Xác nhận email

- Trang `Account/ConfirmEmail`, `Account/ResendEmailConfirmation`; token sống 15 phút.
- Có cấu hình SMTP Gmail → gửi thư thật; chưa cấu hình → `IdentityNoOpEmailSender` ghi log, trang `RegisterConfirmation` hiện link xác nhận.
- Đổi email tại `Account/Manage/Email`, xác nhận email mới tại `ConfirmEmailChange`.

## 4. Mật khẩu

### 4.1 Quên mật khẩu (OTP, không phải link)

1. `/Account/ForgotPassword` — nhập email.
2. Chỉ phát OTP `PasswordReset` khi tài khoản tồn tại và đã xác thực email; điều hướng/phản hồi luôn giống nhau.
3. `/Account/VerifyOtp?purpose=PasswordReset`: OTP đúng → sinh reset token (15 phút) → `/Account/ResetPassword`.
4. Token sai/hết hạn → `/Account/InvalidPasswordReset`.
5. Quy tắc OTP: 6 số (cấu hình được 8), sống 10 phút, tối đa 5 lần thử, sai quá khoá 15 phút, gửi lại chờ 60 giây, tối đa 5 lần/giờ; mã lưu dạng HMAC-SHA256(salt:mã).
6. Rate limit form quên mật khẩu: 5 request/15 phút/IP.
7. Nút gửi lại mã trên trang OTP báo "không tìm thấy" nếu email không tồn tại hoặc chưa xác thực.

### 4.2 Đổi mật khẩu / bảo vệ tài khoản

- `/Account/Manage/ChangePassword`: cần mật khẩu hiện tại.
- `/Account/Manage/SetPassword`: dành cho tài khoản chưa có mật khẩu (dùng đăng nhập ngoài; hiện chưa bật provider).
- `/Account/Manage/ResetAuthenticator` hoặc mã dự phòng khi mất thiết bị 2FA.
- `/Account/Manage/DeletePersonalData`: người dùng tự xoá dữ liệu cá nhân; chặn nếu là Admin cuối.

## 5. Quản lý tài khoản

### 5.1 `/admin/users`

| Mục | Hiện trạng |
|---|---|
| Truy cập | `[Authorize]`; dữ liệu chỉ trả cho Admin/Chủ trọ/Quản lý (`CanManageAccounts`) |
| Danh sách | Lọc theo từ khóa (email/tên/SĐT), lọc vai trò, phân trang; phạm vi theo `VisibleRoles` + scope hợp đồng |
| Bộ lọc trạng thái | Service hỗ trợ `statusFilter` (locked/unverified/pending/active) nhưng UI chưa có ô lọc này |
| Số liệu | `GetSummaryAsync` chỉ Admin: đếm Active/Pending/Locked + 5 tài khoản Pending (theo tên); dùng ở dashboard Admin |
| Tạo | Theo ma trận 1.4; chọn hồ sơ khách khi tạo Khách thuê |
| Duyệt | `ApproveAsync`: chỉ tài khoản `ApprovedAt = null`; không đặt `EmailConfirmed` |
| Khoá | `LockoutEnabled = true`, `LockoutEnd = MaxValue`, đổi security stamp; khoá Chủ trọ → khoá lan + thu hồi lời mời |
| Mở khoá | `LockoutEnd = null`, `AccessFailedCount = 0`; không tự duyệt (`ApprovedAt` giữ nguyên) |
| Đổi vai trò | Gỡ role cũ, gán role mới; không đổi security stamp (claim đổi ở lần làm mới principal, tối đa 2 phút) |
| Xoá mềm | Email đổi sang `deleted-{guid}@deleted.local`, lưu `OriginalEmail`; chặn khi còn hồ sơ khách gắn tài khoản |
| Thùng rác | Chỉ Admin thấy; tối đa 200 tài khoản mới xoá nhất; khôi phục báo lỗi nếu email gốc đã bị dùng lại |
| Xoá vĩnh viễn | Chặn khi còn tham chiếu (hồ sơ khách, tòa sở hữu, phân công quản lý, yêu cầu duyệt, sự cố, lịch sử giá dịch vụ, công tắc quản lý); ghi audit `account.purge` |
| Dọn thùng rác | Duyệt từng tài khoản đã xoá, trả về số xoá được / số bỏ qua |

### 5.2 Khoá chủ trọ lan tỏa

- Khoá Chủ trọ → cập nhật security stamp cho Quản lý/Khách thuê có ngữ cảnh thuộc chủ trọ; phiên cũ bị loại ở lần kiểm kế tiếp (tối đa 2 phút).
- Thu hồi toàn bộ lời mời chưa chấp nhận/chưa thu hồi của chủ trọ.
- Đăng nhập sau đó: vai trò Quản lý/Khách thuê bị loại khỏi `/select-role` nếu thuộc bất kỳ chủ trọ bị khoá; hết ngữ cảnh thì không đăng nhập được.

### 5.3 `/admin/owners`

- Chỉ `Admin`; tìm theo tên/email/SĐT, phân trang.
- Mỗi dòng: trạng thái khoá, số tòa nhà, số quản lý, số khách thuê (liên kết chủ trọ + hợp đồng).
- Khoá/mở chủ trọ dùng cùng logic khoá lan; ghi sổ tài khoản `OwnerLocked`/`OwnerUnlocked`.

### 5.4 `/profile` và hồ sơ khách

- `/profile` cho Admin/Chủ trọ/Quản lý: họ tên, SĐT.
- Mục tài khoản nhận tiền (ngân hàng/số TK/tên) chỉ hiện với Chủ trọ; nhập đủ 3 trường hoặc xoá cả 3; số TK 6–20 ký tự, tên 5–50 ký tự, mã ngân hàng phải có trong catalogue VietQR.
- Khách thuê dùng `/my/tenant-profile` cho hồ sơ khách.

## 6. Xác thực đang bật

| Phương thức | Hiện trạng |
|---|---|
| Mật khẩu | Tối thiểu 8 ký tự; bắt chữ hoa, chữ thường, số; không bắt ký tự đặc biệt |
| OTP email | Mục đích: `OwnerRegistration`, `ManagerFirstLogin`, `TenantFirstLogin`, `TenantRegistration`, `FirstLogin`, `PasswordReset`, `ContractAcceptance`; thực tế đang phát: OwnerRegistration, TenantRegistration, FirstLogin, PasswordReset, ContractAcceptance |
| TOTP + mã dự phòng | Khung Identity có sẵn; người dùng bật ở `/Account/Manage/TwoFactorAuthentication`, đăng nhập tại `/Account/LoginWith2fa` |
| Passkey/WebAuthn | Có trang `/Account/Manage/Passkeys` (thêm/đổi tên/xoá) và endpoint lấy creation/request options |
| Đăng nhập ngoài | Trang/endpoint có nhưng **chưa đăng ký provider** nào |

## 7. Phân quyền

### 7.1 Ba lớp

1. **Endpoint/trang**: `[Authorize]` + policy (`AdminOnly`, `Business`, `ContractBusiness`, `ContractManage`, `InvoiceView`, `PaymentReview`, `UtilityManage`, `UtilityRecord`, `IncidentAccess`).
2. **Service**: kiểm `callerRole`, quy tắc thuần trong `AccountPermissionRules`, `BusinessGuard` ở các module nghiệp vụ.
3. **Phạm vi dữ liệu**: `PropertyScope`, `RoomScope`, `TenantScope`, `ContractManagerTenantScope`, `OwnerAccessGuard`; riêng tài khoản dùng `ScopedUserIds` (quản lý được phân công, khách có hợp đồng tại tòa của chủ trọ/quản lý).

### 7.2 Claim & middleware

- Claim: role đang chọn, `nhatro.active_role`, `nhatro.available_roles`, `nhatro.pending_verification`, `nhatro.pending_role`, `nhatro.pending_approval`.
- `PendingVerificationMiddleware`: tài khoản chưa xác thực email — request GET ngoài danh sách cho phép bị chuyển về `/Account/VerifyOtp`.
- `PendingRoleMiddleware`: chưa chọn ngữ cảnh — request GET ngoài danh sách cho phép bị chuyển về `/select-role`.
- Admin có role khác vẫn có ngữ cảnh nghiệp vụ, nhưng policy nghiệp vụ loại Admin ngay từ endpoint (admin thấy 0 dòng ở trang nghiệp vụ là hành vi có chủ đích).

### 7.3 Liên kết nghiệp vụ

- Công tắc Quản lý quyết định "được ngay / phải gửi duyệt / bị từ chối"; yêu cầu duyệt lưu ở `ManagerApprovalRequest` và có thể yêu cầu OTP.
- Quyền vai trò là lớp gác; phạm vi dữ liệu là lớp lọc; không thay thế nhau.

## 8. Bảo mật & dữ liệu

- Anti-forgery toàn cục; security headers; HSTS ở production; `/api` lỗi trả JSON an toàn.
- DataProtection dùng chung keys; tuỳ chọn `Security:ProtectKeysWithDpapi` chỉ chạy trên Windows.
- Rate limit theo IP: đăng nhập 10/phút, đăng ký 5/giờ, OTP 20/15 phút, quên mật khẩu 5/15 phút, gửi lại email xác nhận 5/15 phút.
- Log: service không ghi mật khẩu; Development chưa cấu hình SMTP dùng sender NoOp nên nội dung thư (OTP/link) có thể xuất hiện trong log.
- Bảng/trường liên quan: `ApplicationUser` (`FullName`, `PhoneNumber`, `ApprovedAt`, `IsDeleted`/`DeletedAt`/`DeletedByUserId`, `OriginalEmail`, `BankCode`/`BankAccountNumber`/`BankAccountName`), `AspNet*`, `OtpChallenge` (hash + salt + mục đích + số lần thử + hạn/khoá), `TenantInvitation`, `TenantOwnerLink`, `PendingRoomAssignment`.

## 9. Liên kết module đang có

| Module | Liên kết |
|---|---|
| Hợp đồng | Tài khoản khách gắn `Tenant.UserId`; OTP `ContractAcceptance` khi khách xác nhận hợp đồng |
| Khách thuê | Tạo hồ sơ + liên kết chủ trọ + xếp phòng chờ từ lời mời; `/my/tenant-profile` |
| Duyệt | `ManagerApprovalRequest` cho thao tác Quản lý cần Chủ trọ duyệt (khác luồng duyệt tài khoản) |
| Dashboard | `GetSummaryAsync` cấp số liệu tài khoản cho trang chủ Admin |
| Nhật ký | `/admin/audit-log` (Admin đọc sổ tài khoản; Chủ trọ đọc sổ nghiệp vụ) |
| Thông báo | Thao tác tài khoản không tạo `AppNotification` |
