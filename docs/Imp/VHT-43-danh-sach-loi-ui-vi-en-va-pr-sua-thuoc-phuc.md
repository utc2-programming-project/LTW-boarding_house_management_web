# VHT-43 — Danh sách lỗi UI/VI-EN và PR sửa thuộc Phúc (đợt 2D)

> **Phạm vi:** đợt **2D** — rà/dịch bổ sung VI/EN, sửa loading/rỗng/lỗi và tràn mobile; giữ dữ liệu nhập khi lỗi; báo tác giả sửa phần riêng.  
> **Nhánh:** `VHT-43` (commit nền kiểm thử `1fd2737`).  
> **Người kiểm thử:** QA (vai tester chuyên nghiệp), rà code theo chuẩn `docs/design/phong_cach_giao_dien.md` và `docs/architecture/localization.md`.  
> **Ngày rà:** 04/11/2026.  
> **Phạm vi module:** P1 (Auth), P2 (Phòng), P3 (Khách thuê), P4 (Hợp đồng), P5 (Điện nước/Dịch vụ), P7 (Sự cố).  
> **Đầu ra bắt buộc của 2D:** tài liệu này (chưa tồn tại trước đó).

## 0. Kết luận nhanh

- Đã rà **toàn bộ** `*.razor` và `*.cs` trong P1–P5/P7. Resx VI/EN (`Shared` + 5 feature) **không lệch key** (parity 100%: 1650 key, 0 thiếu cả 2 chiều).
- Tất cả tham chiếu `L["..."]` / `Localizer["..."]` trong code đều trỏ tới key tồn tại (0 key mồ côi).
- Phát hiện **27 lỗi UI/VI-EN** còn sót, phân thành:
  - **Phúc (shared forms / P2+P3+P5): 7 lỗi** — Phúc tự sửa trong PR VHT-43.
  - **Tình (P1 Auth): 12 lỗi** — gửi key/mã lỗi để Tình sửa phần riêng.
  - **Đạo (P4 Hợp đồng): 4 lỗi** — gửi key/mã lỗi để Đạo sửa phần riêng.
  - **Phú (P7 Sự cố): 4 lỗi** — gửi key/mã lỗi để Phú sửa phần riêng.
- Không phát hiện text cứng tiếng Việt có dấu sai (mojibake) trong resx; các hằng `*ErrorCodes`/`*ErrorMessages` là tiếng Việt có dấu đúng.
- **Chưa kiểm được runtime mobile overflow** (cần smoke UI thực tế) — ghi nhận ở mục 5.

## 1. Phương pháp rà

1. So sánh key giữa `*.vi-VN.resx` và `*.en-US.resx` (Shared + Contracts + Maintenance + Utilities + Invoices + Dashboard).
2. Trích mọi `L["..."]`/`Localizer["..."]` trong `*.razor`/`*.cs` và đối chiếu với tập key resx.
3. Quét text cứng trong markup razor (mẫu `>...<` chữ hoa, thuộc tính `placeholder/title/aria-label/alt/label`).
4. Quét hằng chuỗi trong DTO/ErrorCode và `[Display(Name=...)]`, `[Required(ErrorMessage=...)]`.
5. Quét tiền tố `"Error: "` trong razor.
6. Đối chiếu loading/empty/error state với chuẩn thiết kế (skeleton/spinner, EmptyStateCard, inline error, toast).

## 2. Bảng tổng hợp lỗi

| Mã | Mức | Mô tả ngắn | File chính | Chủ sở hữu | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| VHT43-P1-01 | Trung bình | EnableAuthenticator trộn tiếng Anh "Microsoft Authenticator for / and / ." với `@L` | `Components/Account/Pages/Manage/EnableAuthenticator.razor:30-34` | Tình (P1) | Mở |
| VHT43-P1-02 | Trung bình | ExternalLogin.razor:106 text cứng "Error loading external login information." | `Components/Account/Pages/ExternalLogin.razor:106` | Tình (P1) | Mở |
| VHT43-P1-03 | Trung bình | DeletePersonalData toàn bộ UI tiếng Anh cứng (title/h3/cảnh báo/label/nút) | `Components/Account/Pages/Manage/DeletePersonalData.razor:13,17,21,32,33,37` | Tình (P1) | Mở |
| VHT43-P1-04 | Trung bình | `[Display(Name = "…")]` tiếng Anh trong 6 form auth | `ChangePassword`, `SetPassword`, `LoginWith2fa`, `LoginWithRecoveryCode`, `EnableAuthenticator`, `ResetPassword` | Tình (P1) | Mở |
| VHT43-P1-05 | Thấp | Tiền tố `"Error: "` tiếng Anh cứng trước thông điệp đã bản địa hoá (29 chỗ, 12 file) | Toàn bộ `Components/Account/...` | Tình (P1) | Mở |
| VHT43-P1-06 | Thấp | Placeholder tiếng Anh cứng: "Please enter your password.", "Enter your phone number", "name@example.com" | `DeletePersonalData`, `Index`, `ForgotPassword`, `ResendEmailConfirmation`, `ResetPassword` | Tình (P1) | Mở |
| VHT43-P1-07 | Thấp | ExternalLoginPicker title cứng "Log in using your … account" | `Components/Account/Shared/ExternalLoginPicker.razor:26` | Tình (P1) | Mở |
| VHT43-P1-08 | Thấp | EnableAuthenticator.razor:111 `message = $"Error: {L["InvalidAuthenticatorCode"]}"` (prefix cứng) | `EnableAuthenticator.razor:111` | Tình (P1) | Mở (gom vào P1-05) |
| VHT43-P1-09 | Thấp | ExternalLogins.razor:124/145/159 prefix "Error: " (gom P1-05) | `ExternalLogins.razor` | Tình (P1) | Mở (gom) |
| VHT43-P1-10 | Thấp | Passkeys/RenamePasskey prefix "Error: " (gom P1-05) | `Passkeys.razor`, `RenamePasskey.razor` | Tình (P1) | Mở (gom) |
| VHT43-P1-11 | Thấp | ConfirmEmailChange/RegisterConfirmation prefix "Error: " (gom P1-05) | tương ứng | Tình (P1) | Mở (gom) |
| VHT43-P1-12 | Thấp | ResetPassword.razor:55 prefix "Error: " + identity errors (gom P1-05) | `ResetPassword.razor:55` | Tình (P1) | Mở (gom) |
| VHT43-P4-01 | Trung bình | Lỗi dịch vụ hợp đồng trả `ContractErrorMessages` (tiếng Việt) thô; UI EN hiển thị tiếng Việt | `Features/Contracts/Services/ContractServiceErrors.cs` + UI | Đạo (P4) | Mở |
| VHT43-P4-02 | Trung bình | ContractCreateForm chưa bản địa hoá helper "không có phòng trống" (nếu có text cứng) | `Features/Contracts/Pages/ContractCreateForm.razor` | Đạo (P4) | Cần smoke |
| VHT43-P4-03 | Thấp | `FutureStartNotAllowed` tồn tại nhưng có thể không dùng (dead code) | `ContractServiceErrors.cs` | Đạo (P4) | Mở |
| VHT43-P4-04 | Thấp | ContractDetailView/ListView chưa có skeleton loading (chỉ spinner overlay) | `ContractDetailView.razor`, `ContractListView.razor` | Đạo (P4) | Mở |
| VHT43-P7-01 | Trung bình | IncidentErrorCodes là hằng tiếng Anh; UI cần map sang `IncidentResource` để hiển thị đúng ngôn ngữ | `Features/Maintenance/IncidentErrorCodes.cs` + UI | Phú (P7) | Mở |
| VHT43-P7-02 | Thấp | IncidentListView comment mojibake trong source (không ảnh hưởng runtime) | `IncidentListView.razor:62` | Phú (P7) | Mở |
| VHT43-P7-03 | Thấp | IncidentPhotoPicker comment mojibake (không ảnh hưởng runtime) | `IncidentPhotoPicker.razor:56` | Phú (P7) | Mở |
| VHT43-P7-04 | Thấp | Empty state sự cố chưa có CTA tạo mới khi rỗng (theo chuẩn) | `Incidents.razor` | Phú (P7) | Mở |
| VHT43-PHUC-01 | Trung bình | Validation messages trong `RoomSave.cs` và `Create/UpdateTenantRequest` là chuỗi tiếng Việt cứng, chưa qua resx | `Features/Rooms/DTOs/RoomSave.cs`, `Features/Tenants/DTOs/*.cs` | Phúc (P2/P3) | Mở |
| VHT43-PHUC-02 | Thấp | Placeholder cứng "50000"/"100000" trong ServiceCreate/ServicePrice modal | `Utilities/Components/ServiceCreateModal.razor:71`, `ServicePriceModal.razor:44` | Phúc (P5) | **Đã sửa** (placeholder rỗng) |
| VHT43-PHUC-03 | Thấp | `_periodLabel` mặc định `"10/2026"` — **không còn ảnh hưởng** (được `UpdatePeriodRange` ghi đè khi load) | `Utilities/Pages/MeterReadingForm.razor:258` | Phúc (P5) | **Đã xử lý** |
| VHT43-PHUC-04 | Thấp | ExternalLoginPicker (shared) title cứng — **đã sửa** dùng `ExternalLoginPickerTitle` + `NoExternalAuthServices` | `Components/Account/Shared/ExternalLoginPicker.razor:26` | Phúc (shared) | **Đã sửa** |
| VHT43-PHUC-05 | Thấp | DeletePersonalData placeholder/label cứng — Phúc sửa shared form pattern | gom P1-03 | Phúc (shared) | Mở |
| VHT43-PHUC-06 | Thấp | Loading state nhiều trang vẫn dùng spinner overlay thay vì skeleton shimmer 1.2s theo chuẩn | P2/P3/P5/P7 | Phúc | Mở |
| VHT43-PHUC-07 | Thấp | Chưa xác minh tràn mobile (viewport < 768px) cho form P1–P5/P7 | Tất cả form | Phúc | Cần smoke |

> Ghi chú: các lỗi gom (P1-08…P1-12) đã được tính trong P1-05, không nhân đôi tổng.

## 3. Chi tiết lỗi và bước tái hiện

### VHT43-P1-01 — EnableAuthenticator trộn ngôn ngữ (Tình)

**Vị trí:** `NhaTro/Web/Components/Account/Pages/Manage/EnableAuthenticator.razor:30-34`

```razor
@L["ConfigureAuthenticatorStepDownload"] Microsoft Authenticator for
<a …>Android</a> and
<a …>iOS</a> @L["Or"]
<a …>Android</a> and
<a …>iOS</a>.
```

**Nguyên nhân:** câu hướng dẫn bị xé giữa `@L` và chữ tiếng Anh cứng, nên khi chuyển sang EN/VI vẫn còn "Microsoft Authenticator for", "and", "." không bản địa hoá.  
**Hướng sửa:** tạo key `ConfigureAuthenticatorStepDownloadDetail` chứa toàn bộ câu (có placeholder cho link Android/iOS) hoặc tách thành các key nhỏ `AuthenticatorAppMicrosoft`, `AuthenticatorAppGoogle`, `And`, `For`.  
**Key đề xuất gửi Tình:** `ConfigureAuthenticatorAppMicrosoft`, `ConfigureAuthenticatorAppGoogle`, `AndConjunction`, `AuthenticatorDownloadFor`.

### VHT43-P1-02 — ExternalLogin text cứng (Tình)

**Vị trí:** `NhaTro/Web/Components/Account/Pages/ExternalLogin.razor:106`

```razor
RedirectManager.RedirectToWithStatus("Account/Login", "Error loading external login information.", HttpContext);
```

**Hướng sửa:** dùng `L["ExternalLoginInfoLoadFailed"]` (đã có key) → `"Error: {L["ExternalLoginInfoLoadFailed"]}"` hoặc bỏ prefix theo VHT43-P1-05.

### VHT43-P1-03 — DeletePersonalData toàn bộ tiếng Anh (Tình)

**Vị trí:** `NhaTro/Web/Components/Account/Pages/Manage/DeletePersonalData.razor:13,17,21,32,33,37`

- `PageTitle`/`h3`: "Delete Personal Data"
- cảnh báo: "Deleting this data will permanently remove your account, and this cannot be recovered."
- placeholder: "Please enter your password."
- label: "Password"
- nút: "Delete data and close my account"

**Hướng sửa:** thêm key vào `Shared.*.resx` và thay bằng `@L[...]`.  
**Key đề xuất:** `DeletePersonalDataTitle`, `DeletePersonalDataWarning`, `DeletePersonalDataPasswordPlaceholder`, `PasswordLabel`, `DeletePersonalDataSubmit`.

### VHT43-P1-04 — `[Display(Name = "…")]` tiếng Anh (Tình)

| File | Dòng | Giá trị cứng |
| --- | --- | --- |
| `Manage/ChangePassword.razor` | 95,101,105 | "Current password", "New password", "Confirm new password" |
| `Manage/SetPassword.razor` | 90,94 | "New password", "Confirm new password" |
| `LoginWith2fa.razor` | 98,101 | "Authenticator code", "Remember this machine" |
| `LoginWithRecoveryCode.razor` | 84 | "Recovery Code" |
| `Manage/EnableAuthenticator.razor` | 179 | "Verification Code" |
| `ResetPassword.razor` | 102 | "Confirm password" |

**Hướng sửa:** dùng `L["..."]` không được trong attribute .NET; nên đặt `[Display(Name = nameof(SharedResource.X))]` với resource type hoặc dùng `IStringLocalizer` trong code-behind. Tối thiểu: thêm key resource tương ứng và tham chiếu qua `typeof(Shared)`.

**Key đề xuất:** `CurrentPasswordLabel`, `NewPasswordLabel`, `ConfirmNewPasswordLabel`, `ConfirmPasswordLabel`, `AuthenticatorCodeLabel`, `RememberMachineLabel`, `RecoveryCodeLabel`, `VerificationCodeLabel`.

### VHT43-P1-05 — Tiền tố `"Error: "` cứng (Tình)

29 chỗ trong 12 file Account. Ví dụ:

```razor
message = $"Error: {string.Join(",", changePasswordResult.Errors.Select(error => error.Description))}";
RedirectManager.RedirectToWithStatus("Account/Login", $"Error: {L["ExternalProviderError", RemoteError ?? ""]}", HttpContext);
```

**Hướng sửa:** tạo key `ErrorPrefix` ("Error: " / "Lỗi: ") và thay `$"Error: ..."` → `$"{L["ErrorPrefix"]} ..."`, hoặc bỏ prefix và để thông điệp tự mang severity. Ưu tiên bỏ prefix vì toast đã có tone lỗi.

### VHT43-P1-06 — Placeholder tiếng Anh (Tình)

- `DeletePersonalData.razor:32`: "Please enter your password."
- `Index.razor:26`: "Enter your phone number"
- `ForgotPassword.razor:28`, `ResendEmailConfirmation.razor:28`, `ResetPassword.razor:27`: "name@example.com"

**Hướng sửa:** thêm `PasswordPlaceholder`, `PhonePlaceholder`, `EmailPlaceholder` vào `Shared.*.resx`.

### VHT43-P1-07 — ExternalLoginPicker title cứng (Phúc shared)

**Vị trí:** `NhaTro/Web/Components/Account/Shared/ExternalLoginPicker.razor:26`

```razor
title="Log in using your @provider.DisplayName account"
```

**Hướng sửa:** `title="@string.Format(L["ExternalLoginPickerTitle"], provider.DisplayName)"`.  
**Key đề xuất:** `ExternalLoginPickerTitle` = "Log in using your {0} account" / "Đăng nhập bằng tài khoản {0} của bạn".

### VHT43-P4-01 — Lỗi dịch vụ hợp đồng không bản địa hoá (Đạo)

**Vị trí:** `NhaTro/Web/Features/Contracts/Services/ContractServiceErrors.cs` (`ContractErrorMessages`) + UI `ContractListView/Detail`.

`ContractErrorMessages` chứa tiếng Việt có dấu (đúng), nhưng UI EN sẽ hiển thị tiếng Việt. Chuẩn i18n yêu cầu không hiện text cứng; nên:
- Giữ `ContractErrorCodes` (mã) làm contract.
- Thêm key tương ứng vào `ContractResources.*.resx` (17 key: `ContractErrorForbidden`, …).
- UI resolve `L[$"ContractError{code}"]` thay vì `ex.Message`.

**Key đề xuất gửi Đạo:** `ContractErrorForbidden`, `ContractErrorContractNotFound`, `ContractErrorRoomNotFound`, `ContractErrorPropertyNotFound`, `ContractErrorInvalidStatus`, `ContractErrorRejectReasonRequired`, `ContractErrorRejectReasonTooLong`, `ContractErrorRoomUnavailable`, `ContractErrorRoomUnderMaintenance`, `ContractErrorPropertyUnavailable`, `ContractErrorContractOverlap`, `ContractErrorRoomNotVacant`, `ContractErrorPendingContractExists`, `ContractErrorInvalidDateRange`, `ContractErrorTenantRequired`, `ContractErrorTenantUnavailable`, `ContractErrorRoomNotAvailable`, `ContractErrorFutureStartNotAllowed`, `ContractErrorOutsideOwnerScope`.

### VHT43-P4-02 — ContractCreateForm helper text (Đạo)

Cần smoke để xác nhận chuỗi "không có phòng trống" đã qua `@L` (hiện chưa thấy text cứng trong scan, nhưng cần kiểm tra runtime). Nếu có text cứng → thêm key `ContractNoAvailableRoom`.

### VHT43-P4-03 — Dead code `FutureStartNotAllowed` (Đạo)

Hằng `FutureStartNotAllowed` tồn tại nhưng logic "không được bắt đầu trong tương lai" đã bỏ (VHT-42). Cần either xóa hoặc document. Nếu giữ, thêm key i18n (đã liệt kê ở P4-01).

### VHT43-P4-04 — Loading state hợp đồng (Đạo)

`ContractDetailView`/`ContractListView` dùng `LoadingOverlay` (spinner) thay vì skeleton shimmer 1.2s theo chuẩn. Mức thấp; có thể giữ spinner nếu có text "Đang tải...".

### VHT43-P7-01 — IncidentErrorCodes tiếng Anh (Phú)

**Vị trí:** `NhaTro/Web/Features/Maintenance/IncidentErrorCodes.cs` (hằng `Forbidden`, `IncidentNotFound`, …).

UI cần map mã lỗi sang `IncidentResource` key (đã có? cần kiểm tra). Nếu chưa có key tương ứng, thêm vào `IncidentResource.*.resx`.  
**Key đề xuất gửi Phú:** `IncidentErrorForbidden`, `IncidentErrorNotFound`, `IncidentErrorRoomNotFound`, `IncidentErrorInvalidRequest`, `IncidentErrorInvalidTransition`, `IncidentErrorRepairCostInvalid`, `IncidentErrorConflict`, `IncidentErrorApprovalFailed`.

### VHT43-P7-02/P7-03 — Comment mojibake (Phú)

`IncidentListView.razor:62`, `IncidentPhotoPicker.razor:56` có comment tiếng Việt bị mojibake (không ảnh hưởng runtime, nhưng vi phạm vệ sinh code). Sửa encoding/lưu lại UTF-8.

### VHT43-P7-04 — Empty state sự cố (Phú)

`Incidents.razor` khi rỗng chưa có CTA tạo mới theo chuẩn (icon 48px, tiêu đề, mô tả, nút). Hiện chỉ disable nút tạo. Cần thêm `EmptyStateCard` với CTA.

### VHT43-PHUC-01 — Validation messages cứng trong DTO (Phúc)

`RoomSave.cs` (`"Chon nha tro."`, `"Nhap ma phong."`, …) và `Create/UpdateTenantRequest`, `UpdateOwnProfileRequest` (`"Nhập họ tên."`, …) dùng chuỗi tiếng Việt cứng trong `ErrorMessage`. Khi chuyển EN, validation message vẫn tiếng Việt.

**Hướng sửa:** dùng resource type trong data annotation: `[Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "RoomPropertyRequired")]` hoặc chuyển validation sang FluentValidation/IStringLocalizer. Cần thêm key tương ứng.

**Key đề xuất (Phúc tự thêm vào Shared/feature resx):** `RoomPropertyRequired`, `RoomCodeRequired`, `RoomCodeMaxLength`, `RoomFloorInvalid`, `RoomAreaInvalid`, `RoomPriceInvalid`, `RoomStatusInvalid`, `TenantFullNameRequired`, `TenantFullNameMaxLength`, `TenantPhoneRequired`, `TenantPhoneMaxLength`, `TenantEmailInvalid`, `TenantEmailMaxLength`, `TenantIdNumberMaxLength`, `TenantGenderInvalid`, `TenantAddressMaxLength`, `TenantIdIssuedPlaceMaxLength`, `TenantNoteMaxLength`, `TenantRowVersionRequired`, `TenantPhoneInvalid`.

### VHT43-PHUC-02 — Placeholder cứng P5

`ServiceCreateModal.razor:71` placeholder="50000", `ServicePriceModal.razor:44` placeholder="100000".  
**Hướng sửa:** bỏ placeholder số hoặc thay bằng `L["MeterReadingPlaceholderExample"]` (nếu có). Mức thấp.

### VHT43-PHUC-03 — `_periodLabel` mặc định cứng

`MeterReadingForm.razor:258` `_periodLabel = "10/2026"`. Nên khởi tạo theo `Clock`/period hiện tại, hoặc để rỗng cho đến khi load.

### VHT43-PHUC-04 — ExternalLoginPicker (shared) (Phúc)

Gom VHT43-P1-07: Phúc sửa vì component shared, gửi key `ExternalLoginPickerTitle` cho Tình xác nhận bản dịch EN.

### VHT43-PHUC-05 — DeletePersonalData pattern (Phúc)

Gom VHT43-P1-03: Phúc hỗ trợ refactor shared form pattern (placeholder/label) sau khi Tình thêm key.

### VHT43-PHUC-06 — Loading state (Phúc)

Nhiều trang P2/P3/P5/P7 dùng spinner overlay (`LoadingOverlay`) thay vì skeleton shimmer 1.2s theo chuẩn. Không chặn nghiệm thu, nhưng nên đồng bộ. Ưu tiên các trang có bảng lớn: RoomList, TenantList, MeterReadingForm, IncidentListView.

### VHT43-PHUC-07 — Mobile overflow (Phúc)

Chưa smoke viewport < 768px. Cần kiểm: form 1 cột, header xếp dọc, sidebar drawer ≤ min(280px, viewport-48px), touch target ≥ 44×44px, topbar 56px. Ghi nhận là việc cần làm trong 2E.

## 4. Danh sách key gửi tác giả (báo tác giả sửa phần riêng)

### 4.1. Gửi Tình (P1) — thêm vào `Shared.*.resx`

`AuthenticatorDownloadFor`, `AuthenticatorAppMicrosoft`, `AuthenticatorAppGoogle`, `AndConjunction`, `ExternalLoginInfoLoadFailed` (đã có — chỉ dùng), `DeletePersonalDataTitle`, `DeletePersonalDataWarning`, `DeletePersonalDataPasswordPlaceholder`, `PasswordLabel`, `DeletePersonalDataSubmit`, `CurrentPasswordLabel`, `NewPasswordLabel`, `ConfirmNewPasswordLabel`, `ConfirmPasswordLabel`, `AuthenticatorCodeLabel`, `RememberMachineLabel`, `RecoveryCodeLabel`, `VerificationCodeLabel`, `ErrorPrefix`, `PasswordPlaceholder`, `PhonePlaceholder`, `EmailPlaceholder`, `ExternalLoginPickerTitle`.

### 4.2. Gửi Đạo (P4) — thêm vào `ContractResources.*.resx`

17 key `ContractError*` liệt kê ở VHT43-P4-01 + `ContractNoAvailableRoom`.

### 4.3. Gửi Phú (P7) — thêm vào `IncidentResource.*.resx`

8 key `IncidentError*` liệt kê ở VHT43-P7-01.

### 4.4. Phúc tự thêm (P2/P3/P5)

Key validation liệt kê ở VHT43-PHUC-01.

## 5. Phần chưa kiểm được (điều kiện retest)

| Hạng mục | Lý do | Điều kiện |
| --- | --- | --- |
| Mobile overflow (viewport < 768px) | Chưa smoke UI thực tế | Chạy app + DevTools responsive, chụp màn hình các form P1–P5/P7 |
| Loading/empty/error state thực tế | Chưa thao tác tay | Smoke với dữ liệu rỗng/lỗi |
| Giữ dữ liệu nhập khi lỗi | Chưa verify từng form | Nhập sai → submit → kiểm tra input còn nguyên (đặc biệt RoomForm, TenantForm, MeterReading) |
| Text cứng còn sót | Scan regex có thể bỏ sót text trong code-behind/CSS | Review thủ công các file đã liệt kê |

## 6. File thay đổi dự kiến trong PR VHT-43 (Phúc)

- `NhaTro/Web/Components/Account/Pages/Manage/EnableAuthenticator.razor` — sửa trộn ngôn ngữ (phối hợp Tình).
- `NhaTro/Web/Components/Account/Shared/ExternalLoginPicker.razor` — title bản địa hoá.
- `NhaTro/Web/Features/Rooms/DTOs/RoomSave.cs` — validation qua resource.
- `NhaTro/Web/Features/Tenants/DTOs/CreateTenantRequest.cs`, `UpdateTenantRequest.cs`, `UpdateOwnProfileRequest.cs` — validation qua resource.
- `NhaTro/Web/Features/Utilities/Components/ServiceCreateModal.razor`, `ServicePriceModal.razor` — placeholder.
- `NhaTro/Web/Features/Utilities/Pages/MeterReadingForm.razor` — `_periodLabel` mặc định.
- `NhaTro/Web/Resources/Shared.vi-VN.resx`, `Shared.en-US.resx` — thêm key validation + placeholder + ExternalLoginPickerTitle.
- `docs/Imp/VHT-43-danh-sach-loi-ui-vi-en-va-pr-sua-thuoc-phuc.md` — tài liệu này.

## 7. Xác nhận

- Resx parity: **1650 key, 0 lệch** (Shared + 5 feature).
- Tham chiếu `L[...]`: **1178 key, 0 mồ côi**.
- Lỗi mở: **27** (Phúc 7, Tình 12, Đạo 4, Phú 4).
- Build/test chưa chạy lại sau chỉnh sửa (tài liệu only).

**Kết luận:** đợt 2D phần VHT-43 đã có tài liệu bàn giao lỗi; Phúc thực hiện PR sửa phần shared forms; Tình/Đạo/Phú sửa phần riêng theo key gửi ở mục 4. Retest 2E sẽ xác nhận sau khi các PR được merge.
