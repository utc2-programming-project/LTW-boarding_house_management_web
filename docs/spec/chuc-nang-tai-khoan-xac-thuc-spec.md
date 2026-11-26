# SPEC — Tài khoản & xác thực (hiện trạng)

> Ghi lại hành vi **đang có trong code** (đối chiếu 20/11/2026). Không gồm đề xuất/kế hoạch.
> Nền: NhaTro Blazor .NET 10, ASP.NET Identity, EF Core SQL Server; vai trò `Admin`, `ChuTro`, `QuanLy`, `KhachThue`.

## 0. Quy tắc chung

| Mục | Hiện trạng |
|---|---|
| Tài khoản | `ApplicationUser : IdentityUser`; `UserName` = email, email duy nhất |
| Một vai trò hoạt động | Claim chỉ phát role đang chọn; danh sách ngữ cảnh nằm ở claim riêng (mục 1.2) |
| Kiểm quyền | Policy/attribute ở trang hoặc endpoint + service nhận `callerUserId`/`callerRole` và tự kiểm quyền, phạm vi |
| Ghi vết | `ILogger` cho mọi thao tác; sổ tài khoản (`AuditLogCategory.Account`) nhận create/approve/reject/lock/unlock/role_change/delete/restore/reset_password/force_logout, `OwnerLocked`/`OwnerUnlocked` và `account.purge` |
| Thông báo | Thao tác nghiệp vụ **không** tạo `AppNotification`; riêng cảnh báo bảo mật (mục 2.4) có cả `AppNotification` lẫn email |

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
- Chủ trọ bị khoá: chỉ khi **mọi** chủ trọ liên quan đều bị khoá thì ngữ cảnh Quản lý/Khách thuê mới bị loại; dữ liệu của chủ trọ bị khoá luôn bị lọc khỏi truy vấn. Quy tắc đầy đủ ở 5.2.

### 1.3 Trạng thái tài khoản

| Trạng thái | Điều kiện | Đăng nhập |
|---|---|---|
| `Unverified` | `EmailConfirmed = false` | Được vào nhưng chỉ tới trang OTP |
| `Pending` | Đã xác thực email, `ApprovedAt = null` | Bị chặn (`NotAllowed`) |
| `Active` | Đã xác thực email, `ApprovedAt ≠ null`, không khoá | Bình thường |
| `Locked` | `LockoutEnd = DateTimeOffset.MaxValue` | Bị chặn |
| Thùng rác | `IsDeleted = true` | Bị chặn |

Ưu tiên suy trạng thái: **Locked > Unverified > Pending > Active**. Khoá tạm do sai mật khẩu không phải `Locked`.

`Pending` bị chặn đăng nhập (`NotAllowed`). Tài khoản `Pending` vừa hoàn tất OTP bị huỷ phiên và chuyển `/login?pendingApproval=1` — cùng cơ chế, chỉ khác là phiên OTP không được giữ.

### 1.4 Ma trận thao tác

| Thao tác | Admin | Chủ trọ | Quản lý |
|---|---|---|---|
| Tạo tài khoản | ChuTro, QuanLy, KhachThue | QuanLy, KhachThue (hiệu lực ngay) | KhachThue (Pending) |
| Duyệt | Mọi vai trò | QuanLy, KhachThue trong phạm vi | Không |
| Khoá / mở khoá | Mọi vai trò, trừ tự khoá và Admin cuối | Không | Không |
| Đổi vai trò | Có; không nâng lên Admin, không tự đổi, không gỡ Admin cuối, không đổi tài khoản đang gắn hồ sơ khách | Không | Không |
| Xoá mềm | Có; trừ tự xoá, Admin khác, tài khoản còn hồ sơ khách | Không | Không |
| Khôi phục | Có | Không (chỉ Admin) | Không |
| Xoá vĩnh viễn / dọn thùng rác | Có; chặn khi còn dữ liệu tham chiếu | Không | Không |

Quản lý không có quyền duyệt; danh sách tài khoản của Quản lý gồm chính mình + khách có hợp đồng (kể cả lịch sử) tại tòa được giao.

## 2. Đăng nhập

### 2.1 Luồng

1. `/login` hoặc `/Account/Login`: email + mật khẩu + "ghi nhớ đăng nhập"; có hiện/ẩn mật khẩu.
2. Sai email/mật khẩu → thông báo chung, không phân biệt lỗi.
3. Đúng (mật khẩu, hoặc **passkey** không mật khẩu) → xét theo thứ tự:
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
| Rate limit | Xem bảng 8.1 |
| Lockout Identity | 10 lần sai/5 phút, chỉ bật sau khi đã giải đúng CAPTCHA (`lockoutOnFailure: true`); hết hạn tự mở |
| Anti-forgery | POST `/login`/`/Account/Login` trả 400 → chuyển `/login?stale=1` để tải lại form |

### 2.3 Phiên

- Cookie xác thực `Identity.Application`: HttpOnly, SameSite=Lax; Secure `Always` (production), `SameAsRequest` (Development/Testing); hết hạn 8 giờ, sliding; security stamp kiểm lại mỗi 2 phút.
- Mỗi thiết bị đăng nhập một phiên riêng (cookie phiên + bản ghi phía server, chi tiết Phụ lục A); phiên bị thu hồi → request kế tiếp bị đăng xuất ngay, không đợi security stamp.
- Trang `Account/Manage/Sessions`: liệt kê thiết bị/IP/lần hoạt động cuối; thu hồi một thiết bị hoặc mọi thiết bị khác (giữ thiết bị hiện tại); "hoạt động gần nhất" cập nhật tối đa 5 phút một lần.
- Đổi mật khẩu → thu hồi mọi phiên khác. Khoá tài khoản/force logout/xoá mềm → thu hồi mọi phiên.
- Đăng xuất: POST `/Account/Logout`, kiểm antiforgery (thiếu token vẫn kết thúc phiên); xoá phiên server + cookie, trả `Clear-Site-Data`.
- `/api/*`: thiếu đăng nhập 401, thiếu quyền 403 (không redirect).

### 2.4 Thông báo bảo mật

- Gửi email + thông báo trong ứng dụng (best-effort, lỗi gửi không rollback hành động) khi: đổi mật khẩu, yêu cầu đổi email (gửi tới hộp thư **cũ**), khoá/mở khoá tài khoản, force logout.
- Đăng nhập từ IP/UA chưa từng thấy: tạo thông báo trong ứng dụng ngay và gửi email nền.

## 3. Đăng ký & tạo tài khoản

### 3.1 Chủ trọ tự đăng ký

1. `/Account/RegisterOwner`: họ tên, email, SĐT (tuỳ chọn, 10 số bắt đầu bằng 0), mật khẩu + xác nhận.
2. Service tạo user `EmailConfirmed = false`, `ApprovedAt = null`, gán role `ChuTro`; trùng email báo lỗi.
3. Service phát OTP `OwnerRegistration`; trang chuyển `/Account/VerifyOtp`.
4. OTP đúng → `EmailConfirmed = true`; **chỉ luồng `OwnerRegistration`** mới đặt `ApprovedAt ??= now` → tài khoản `Active`.
5. Rate limit đăng ký: xem 8.1.

### 3.2 Khách thuê qua lời mời

| Tiêu chí | Khách mới | Đã có tài khoản |
|---|---|---|
| Kênh | Email (1/6/24 giờ, mặc định 24 giờ — `Invitations:EmailLifetimeMinutes`) hoặc QR (15 phút/1 giờ/6 giờ/24 giờ) | Email hoặc QR, cùng bộ hạn |
| Điều kiện | Email chưa có tài khoản; chọn "khách mới" mà email đã tồn tại → từ chối | Phải đăng nhập đúng tài khoản được mời |
| Khi nhận | `/invite/{Token}` tạo tài khoản (`EmailConfirmed = false`, `ApprovedAt = now`), role `KhachThue`; gửi OTP `TenantRegistration` | Thêm liên kết chủ trọ, tạo hồ sơ khách nếu chưa có, `RefreshSignInAsync` để nhận ngữ cảnh mới |
| Hồ sơ & phòng | Tạo hồ sơ khách + xếp phòng chờ (nếu có phòng) | Xếp phòng chờ (nếu có phòng) |

- Token 32 byte, dùng một lần; hết hạn/đã thu hồi → trang lỗi. Mời lại cùng email + chủ trọ sẽ thu hồi lời mời cũ.
- Khoá chủ trọ → thu hồi lời mời đang mở (5.2).

### 3.3 Tạo tài khoản bởi người quản lý

- Admin/Chủ trọ tạo `QuanLy`/`KhachThue`: `ApprovedAt = now`, `EmailConfirmed = false` → lần đầu đăng nhập vẫn qua OTP.
- Quản lý tạo `KhachThue`: `ApprovedAt = null` (Pending), chỉ trong phạm vi tòa được giao.
- Mọi thao tác tạo `KhachThue` phải chọn hồ sơ khách **chưa gắn tài khoản** (`Tenant.UserId = null`) trong phạm vi; Chủ trọ/Quản lý chỉ chọn được hồ sơ đã có hợp đồng tại tòa trong phạm vi. Gắn hồ sơ bằng cập nhật có điều kiện, thua đua thì xoá tài khoản vừa tạo.
- OTP xác thực email không tự duyệt: tài khoản Quản lý tạo vẫn `Pending` sau OTP (xem 1.3) cho tới khi Chủ trọ bấm duyệt.
- Chủ trọ/Quản lý có nút **Từ chối** cho tài khoản đang chờ duyệt (bắt buộc lý do): gỡ liên kết hồ sơ khách, thu hồi lời mời đang mở, xoá mềm tài khoản và giữ `OriginalEmail` để mời lại cùng email.

### 3.4 Xác nhận email

- Trang `Account/ConfirmEmail`, `Account/ResendEmailConfirmation`; token sống 15 phút.
- Có cấu hình SMTP Gmail → gửi thư thật; chưa cấu hình → `IdentityNoOpEmailSender` ghi log, trang `RegisterConfirmation` hiện link xác nhận.
- Gửi thư: thử 3 lần mỗi máy chủ, lỗi máy chính chuyển sang `Email:FallbackSmtp`; thư OTP/lời mời ném lỗi khi cả hai đều hỏng để trả trạng thái thất bại, thư Identity vẫn nuốt lỗi. Production thiếu SMTP và không đặt `Email:AllowLogOnly=true` → ứng dụng không khởi động.
- Đổi email tại `Account/Manage/Email`, xác nhận email mới tại `ConfirmEmailChange`.

## 4. Mật khẩu

### 4.1 Quên mật khẩu (OTP, không phải link)

1. `/Account/ForgotPassword` — nhập email.
2. Chỉ phát OTP `PasswordReset` khi tài khoản tồn tại và đã xác thực email; điều hướng/phản hồi luôn giống nhau.
3. `/Account/VerifyOtp?purpose=PasswordReset`: OTP đúng → sinh reset token (15 phút) → `/Account/ResetPassword`.
4. Token sai/hết hạn → `/Account/InvalidPasswordReset`.
5. Quy tắc OTP: 6 số (cấu hình được 8), sống 10 phút, tối đa 5 lần thử, sai quá khoá 15 phút, gửi lại chờ 60 giây, tối đa 5 lần/giờ.
6. Rate limit form quên mật khẩu: xem 8.1.
7. Nút gửi lại mã trên trang OTP báo "không tìm thấy" nếu email không tồn tại hoặc chưa xác thực.

### 4.2 Đổi mật khẩu / bảo vệ tài khoản

- `/Account/Manage/ChangePassword`: cần mật khẩu hiện tại; đổi xong thu hồi mọi phiên khác và gửi cảnh báo bảo mật.
- `/Account/Manage/SetPassword`: dành cho tài khoản chưa có mật khẩu (chỉ xảy ra khi đăng nhập bằng provider đã đăng ký — xem mục 6).
- `/Account/Manage/ResetAuthenticator` hoặc mã dự phòng khi mất thiết bị 2FA.
- Production: Admin/Chủ trọ chưa bật 2FA bị chuyển sang `EnableAuthenticator` trước khi vào nghiệp vụ (`Security:RequireTwoFactorForPrivileged`, tắt ở Development/Testing).
- `/Account/Manage/DeletePersonalData`: người dùng tự xoá dữ liệu cá nhân; chặn nếu là Admin cuối, còn nhà đứng tên, phân công quản lý hiệu lực hoặc hợp đồng đang hiệu lực.

## 5. Quản lý tài khoản

### 5.1 `/admin/users`

| Mục | Hiện trạng |
|---|---|
| Truy cập | `[Authorize]`; dữ liệu chỉ trả cho Admin/Chủ trọ/Quản lý (`CanManageAccounts`) |
| Danh sách | Từ khóa (email/tên/SĐT), lọc vai trò, lọc trạng thái (locked/unverified/pending/active), phân trang; phạm vi theo `VisibleRoles` + scope hợp đồng |
| Số liệu | `GetSummaryAsync` chỉ Admin: đếm Active/Pending/Locked + 5 tài khoản Pending (theo tên); dùng ở dashboard Admin |
| Tạo | Theo ma trận 1.4; chọn hồ sơ khách khi tạo Khách thuê |
| Duyệt | `ApproveAsync`: chỉ tài khoản `ApprovedAt = null`; không đặt `EmailConfirmed` |
| Từ chối | Chủ trọ/Quản lý trong phạm vi, tài khoản Pending, bắt buộc lý do: gỡ hồ sơ khách, thu hồi lời mời, xoá mềm, giữ `OriginalEmail` |
| Khoá | `LockoutEnabled = true`, `LockoutEnd = MaxValue`, đổi security stamp, thu hồi mọi phiên; khoá Chủ trọ → khoá lan (5.2) |
| Mở khoá | `LockoutEnd = null`, `AccessFailedCount = 0`; không tự duyệt (`ApprovedAt` giữ nguyên) |
| Đặt lại mật khẩu / đăng xuất mọi thiết bị | Quyền như Duyệt; reset gửi OTP `PasswordReset` tới email đã xác thực (không đặt mật khẩu hộ); logout đổi security stamp + thu hồi mọi phiên + cảnh báo bảo mật |
| Đổi vai trò | Gỡ role cũ, gán role mới, đổi security stamp; phiên cũ bị đá trong ≤ 2 phút |
| Xoá mềm | Email đổi sang `deleted-{guid}@deleted.local`, lưu `OriginalEmail`, thu hồi mọi phiên; chặn khi còn hồ sơ khách gắn tài khoản |
| Thùng rác | Chỉ Admin thấy; tối đa 200 tài khoản mới xoá nhất; khôi phục báo lỗi nếu email gốc đã bị dùng lại |
| Xoá vĩnh viễn | Chặn khi còn tham chiếu (hồ sơ khách, tòa sở hữu, phân công quản lý, yêu cầu duyệt, sự cố, lịch sử giá dịch vụ, công tắc quản lý); ghi audit `account.purge` |
| Dọn thùng rác | Duyệt từng tài khoản đã xoá, trả về số xoá được / số bỏ qua |

### 5.2 Khoá chủ trọ lan tỏa

- Khoá Chủ trọ → thu hồi mọi phiên của Quản lý/Khách thuê có ngữ cảnh thuộc chủ trọ đó (đăng xuất ở request kế tiếp).
- Thu hồi toàn bộ lời mời chưa chấp nhận/chưa thu hồi của chủ trọ.
- Ngữ cảnh Quản lý/Khách thuê chỉ bị loại khi **mọi** chủ trọ liên quan đều bị khoá; hết ngữ cảnh thì không đăng nhập được.
- Dữ liệu của chủ trọ bị khoá (nhà/phòng/hợp đồng/hóa đơn/khách/chi phí/báo cáo/tệp) bị lọc khỏi mọi truy vấn theo phạm vi.

### 5.3 `/admin/owners`

- Chỉ `Admin`; tìm theo tên/email/SĐT, phân trang.
- Mỗi dòng: trạng thái khoá, số tòa nhà, số quản lý, số khách thuê (liên kết chủ trọ + hợp đồng).
- Khoá/mở chủ trọ dùng cùng logic khoá lan; ghi sổ tài khoản `OwnerLocked`/`OwnerUnlocked`.

### 5.4 `/profile` và hồ sơ khách

- `/profile` cho Admin/Chủ trọ/Quản lý: họ tên, SĐT.
- Mục tài khoản nhận tiền (ngân hàng/số TK/tên) chỉ hiện với Chủ trọ; nhập đủ 3 trường hoặc xoá cả 3; số TK 6–20 ký tự, tên 5–50 ký tự, mã ngân hàng phải có trong catalogue VietQR.
- Khách thuê dùng `/my/tenant-profile` cho hồ sơ khách.

### 5.5 Trang tự phục vụ `/account`

- Năm tab, chọn bằng query `?tab=`: `profile` (hồ sơ), `security` (bảo mật), `devices` (thiết bị), `activity` (hoạt động gần đây), `data` (dữ liệu). Trang render tĩnh; riêng form hồ sơ là island `InteractiveServer`.
- Ảnh đại diện lưu ở server, một dòng mỗi tài khoản trong `UserAvatars`; phục vụ qua `GET /api/avatar/{userId}`, nạp qua `POST`, gỡ qua `DELETE`. Chỉ chính chủ đọc/ghi (đọc tài khoản khác trả 404 như "chưa có ảnh" để không dò được id; ghi trả 403).
- Ảnh thật được nhận diện bằng magic byte (jpeg/png/webp), trần 256 KB; trình duyệt nén về tối đa 256px trước khi nộp. Nội dung trả về kèm `X-Content-Type-Options: nosniff` và `Cache-Control: private, no-store`; payload quá cỡ bị chặn trước khi giải mã base64 (413), sai định dạng trả 400.
- Tab Bảo mật liệt kê mỗi thiết lập một dòng; dòng liên kết đăng nhập ngoài chỉ hiện khi có lý do thấy nó (mục 6).
- Tab Hoạt động liệt 10 sự kiện gần nhất của chính tài khoản, lấy từ hộp thư `AppNotification` (không thêm bảng mới); đánh dấu tất cả đã đọc trong một request.

## 6. Xác thực đang bật

| Phương thức | Hiện trạng |
|---|---|
| Mật khẩu | Tối thiểu 8 ký tự; bắt chữ hoa, chữ thường, số; không bắt ký tự đặc biệt |
| OTP email | Mục đích: `OwnerRegistration`, `ManagerFirstLogin`, `TenantFirstLogin`, `TenantRegistration`, `FirstLogin`, `PasswordReset`, `ContractAcceptance`; thực tế đang phát: OwnerRegistration, TenantRegistration, FirstLogin, PasswordReset, ContractAcceptance |
| TOTP + mã dự phòng | Khung Identity có sẵn; người dùng bật ở `/Account/Manage/TwoFactorAuthentication`, đăng nhập tại `/Account/LoginWith2fa` |
| Passkey/WebAuthn | Có trang `/Account/Manage/Passkeys` (thêm/đổi tên/xoá) và endpoint lấy creation/request options |
| Đăng nhập ngoài | Provider Google đăng ký khi `Authentication:Google:ClientId` + `ClientSecret` có đủ; thiếu cấu hình thì scheme không tồn tại và mọi nút Google tự ẩn. Trang `/Account/Manage/ExternalLogins` liên kết/gỡ; chính sách liên kết chỉ nhận liên kết/tự liên kết khi email provider đã xác thực trùng tài khoản, không tạo tài khoản mới (xem 5.5) |

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
- Log: service không ghi mật khẩu; sender ghi log mặc định chỉ ghi người nhận/tiêu đề. Nội dung thư (OTP/link) chỉ ra log khi Development **và** đặt `Email:LogBodyInDevelopment=true`.
- Bảng/trường liên quan: `ApplicationUser` (`FullName`, `PhoneNumber`, `ApprovedAt`, `IsDeleted`/`DeletedAt`/`DeletedByUserId`, `OriginalEmail`, `BankCode`/`BankAccountNumber`/`BankAccountName`), `UserAvatars` (`UserId` vừa là khoá chính vừa là khoá ngoại), `AspNet*`, `OtpChallenge`, `TenantInvitation`, `TenantOwnerLink`, `PendingRoomAssignment`.

### 8.1 Rate limit (theo IP)

| Luồng | Giới hạn | Khi vượt |
|---|---|---|
| Đăng nhập, 2FA | 10/phút | 303 `/login?tooManyRequests=1` |
| Đăng ký chủ trọ | 5/giờ | 429 |
| Xác thực OTP (POST `/Account/VerifyOtp`) | 20/15 phút | 429 |
| Quên mật khẩu | 5/15 phút | 303 về form kèm cờ lỗi |
| Gửi lại email xác nhận | 5/15 phút | 303 về form kèm cờ lỗi |
| Tải tệp | 20/phút | 429 |
| Đổi ngôn ngữ | 20/phút | 429 |

Cấu hình ở `Security:RateLimit`; `/api/*` khi vượt luôn trả 429 JSON.

## 9. Liên kết module

- Hợp đồng & khách thuê: `Tenant.UserId` nối tài khoản với hồ sơ; OTP `ContractAcceptance` khi khách xác nhận hợp đồng; khách xem hồ sơ ở `/my/tenant-profile`.
- Duyệt nghiệp vụ: `ManagerApprovalRequest` (thao tác Quản lý cần Chủ trọ duyệt) tách khỏi duyệt tài khoản.
- Dashboard/Nhật ký/Thông báo: `GetSummaryAsync` cấp số liệu tài khoản cho Admin; `/admin/audit-log`; cảnh báo bảo mật ở 2.4.

## Phụ lục A. Chi tiết triển khai (không thuộc hành vi)

| Chủ đề | Chi tiết |
|---|---|
| Phiên | Cookie `nhatro.sid` (HttpOnly) + `UserSessionInfo` trong `Security:SessionsPath` (mặc định `%LOCALAPPDATA%\NhaTro\sessions`), mỗi tài khoản một file JSON, tối đa 20 phiên, tự xoá sau 90 ngày; `UserSessionMiddleware` |
| OTP / lời mời | OTP lưu HMAC-SHA256(salt:mã) trong `OtpChallenge`; token lời mời 32 byte, server chỉ lưu SHA-256 trong `TenantInvitation` |
| Đăng xuất | `Clear-Site-Data: "cache", "cookies", "storage"` |
| Ảnh đại diện | `UserAvatars` (PK = FK → `AspNetUsers`, xoá cascade); `AvatarService` sniff nội dung, trần 256 KB; `AvatarEndpoints` kiểm tra antiforgery tay cho body JSON, rate limit `file-upload` |
| Provider ngoài | `AddGoogle` chỉ chạy khi `Authentication:Google:ClientId`/`ClientSecret` có đủ; UI đọc `GetExternalAuthenticationSchemesAsync` nên không cần flag riêng |
| Trạng thái & điều hướng | `AccountStatusRules.Derive`; `PostLoginRouter`, `RefreshSignInAsync` |
| Cấu hình | `Security:RateLimit`, `Security:SessionsPath`, `Security:RequireTwoFactorForPrivileged`, `Email:*`, `Invitations:EmailLifetimeMinutes` |
