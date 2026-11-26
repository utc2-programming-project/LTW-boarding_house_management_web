# Đặc tả — Ra vào cổng (Access Control: QR + vân tay + khuôn mặt)

> **Trạng thái:** Đặc tả v1, **chưa code**. Đây là **chức năng bổ sung** ngoài backlog Jira (Jira đã chốt xong, không tạo issue/label mới). Cập nhật 22/11/2026 theo góp ý của nhóm: **khách thuê được cấp QR cho khách của mình**, không bắt buộc chỉ Chủ trọ.
>
> **Phạm vi:** v1, thiết kế để mở rộng. Đối chiếu pháp lý ngày 20/11/2026 (mục 14) và đối chiếu code `dev` @ `0de1572` ngày 22/11/2026 (mục 13).
>
> **Nền:** NhaTro (Blazor .NET 10) — vai trò `Admin`, `ChuTro`, `QuanLy`, `KhachThue` + khách chưa có tài khoản.
>
> **Liên quan:** Quản lý xe (chưa có trong dev) · Trợ lý AI · Tìm kiếm toàn cục · Thông báo lưu trú.

---

## 0. Quy ước chung

| Mục | Quy ước |
|---|---|
| Nguyên tắc ghi dữ liệu | Mọi thao tác ghi qua service nghiệp vụ hiện có + audit; khách vãng lai không ghi trực tiếp DB |
| Phạm vi dữ liệu | Theo scope sẵn có (`PropertyScope`, `RoomScope`, `TenantScope`…): Chủ trọ = nhà mình, Quản lý = nhà được giao, Khách thuê = dữ liệu của mình |
| Thông báo | Dùng `INotificationService.NotifyAsync(userId, title, body, link)` có sẵn (bảng `AppNotifications`) để mở đúng màn xử lý |
| Mở rộng | Điểm cắm riêng (provider/config/adapter) — thêm không phá lõi |
| Thời gian | Nghiệp vụ dùng `TimeProvider`; lưu UTC, hiển thị `dd/MM/yyyy HH:mm` theo giờ Việt Nam |
| Ngôn ngữ | Folder/class/resource key tiếng Anh; UI Việt/Anh; enum lưu SQL không dịch |

---

## 1. Tổng quan chức năng

Gộp QR + vân tay + khuôn mặt thành **một chức năng duy nhất**: mỗi người có một hoặc nhiều **chứng thư (credential)**, mỗi cổng có **chế độ xác thực riêng** do chủ trọ chọn. QR không đứng một mình mà là một yếu tố trong hệ thống ra vào. Xe dùng chung cổng này — chi tiết ở Quản lý xe.

### 1.1 Mục tiêu

- Kiểm soát ai được vào/ra, có nhật ký và báo cáo cho chủ trọ.
- Người trong trọ vào nhanh bằng khuôn mặt, vân tay hoặc QR; người ngoài phải có phiếu do **chủ trọ hoặc khách thuê** cấp/duyệt (theo chính sách).
- Chủ trọ tự quyết: mức xác thực của từng cổng, có bắt khai báo thông tin khách hay không, và **khách thuê có được tự cấp QR hay không**.
- Tuân thủ nghĩa vụ thông báo lưu trú: khách ở qua đêm bắt buộc khai báo (2.5); chỉ được tắt khai báo với khách đến chơi trong ngày.

### 1.2 Phương thức xác thực

| Phương thức | Ai dùng | Dữ liệu lưu ở đâu | Thiết bị | Ghi chú |
|---|---|---|---|---|
| Khuôn mặt | Cư dân; khách có gắn ảnh vào phiếu (tuỳ chọn) | Vector khuôn mặt (embedding) mã hoá tại server | Camera tại cổng | Chỉ so khớp tại cổng, có kiểm tra ảnh sống (liveness); ngưỡng khớp cấu hình được |
| Vân tay | Cư dân; người phục vụ định kỳ | Mẫu vân tay nằm trên máy quét tại cổng (không lên server); với điện thoại cá nhân, sinh trắc dùng passkey thiết bị (server chỉ giữ public key) | Máy quét vân tay tại cổng (mức B); hoặc kiosk hiện mã thử thách, cư dân quét bằng điện thoại + xác nhận vân tay/Face ID thiết bị (mức A) | Server chỉ nhận "khớp/không khớp", không nhận mẫu vân tay |
| QR động | Cư dân | App khách thuê; server giữ hash token | Camera kiosk quét | TOTP theo thời gian (RFC 6238), làm mới mỗi 60 giây, kiosk chấp nhận ±1 bước; không chứa PII |
| QR một lần | Khách vãng lai | Server giữ hash token | Camera kiosk hoặc link mở trên điện thoại khách | Có hạn + số lần dùng, mặc định 1 lần / 60 phút |
| Mã 6 số | Tất cả (dự phòng) | App / phiếu khách | Nhập tại kiosk | Dùng khi camera/hệ sinh trắc lỗi |
| Bảo vệ mở tay | Bất kỳ | Log Console bảo vệ / app chủ trọ | — | Bắt buộc ghi lý do, có audit |

### 1.3 Loại người được vào

| Loại | Chứng thư mặc định | Thời hạn | Ghi chú |
|---|---|---|---|
| Cư dân (hợp đồng hiệu lực: `Contract.Status = Approved`, trong khoảng `StartDate`–`EndDate`) | QR động + khuôn mặt/vân tay đã đăng ký | Theo hợp đồng, tự khoá khi thanh lý/hết hạn | Dùng được 1–3 phương thức tuỳ đăng ký |
| Khách vãng lai | QR một lần (**chủ trọ hoặc khách thuê cấp**; qua đêm cần chủ trọ/quản lý duyệt) | Mặc định 1 lần / 60 phút, gia hạn được | Có thể gắn ảnh khách để vào bằng khuôn mặt trong thời hạn phiếu |
| Người phục vụ định kỳ (shipper, giúp việc…) | Thẻ định kỳ: QR / vân tay / khuôn mặt + khung giờ | Theo thẻ, thu hồi bất kỳ lúc nào | Giới hạn ngày/giờ, không vào khu vực ngoài phạm vi |
| Chủ trọ / Quản lý | QR quản trị hoặc mở tay | Không giới hạn theo hợp đồng | Mọi lần mở tay đều ghi log |

**Thay đổi so với bản PDF gốc:** dòng *Khách vãng lai* bổ sung quyền của **khách thuê** được cấp QR. Điều kiện chi tiết ở 2.3 và mục 8.

### 1.4 Hai mức triển khai

| Hạng mục | Mức A — không cần thiết bị ngoài (mặc định v1) | Mức B — có thiết bị tại cổng |
|---|---|---|
| Kiosk | Điện thoại/máy tính bảng đặt ở cổng, chế độ toàn màn hình | Kiosk chuyên dụng |
| Khuôn mặt | Camera kiosk chụp → server so khớp | Camera AI + chống ảnh giả |
| Vân tay | Kiosk hiện mã thử thách → cư dân quét bằng điện thoại + xác nhận vân tay/Face ID thiết bị | Máy quét vân tay tại cổng |
| QR | Camera kiosk quét | Đầu đọc QR |
| Mở cổng | Hiện "Hợp lệ" cho bảo vệ / bấm xác nhận trong app | Webhook/HTTP tới relay, khoá điện, barrier, turnstile |
| Offline | Cần mạng | Kiosk giữ chứng thư mã hoá + hàng đợi nhật ký, đồng bộ khi có mạng |

V1 chạy được ngay ở mức A; chuyển sang mức B chỉ thay lớp thiết bị, không đổi luồng nghiệp vụ.

**Hai giới hạn kỹ thuật cần biết trước:**

- Trình duyệt không đọc trực tiếp máy quét vân tay; mức B phải qua SDK/OSDP–Wiegand của hãng (ZKTeco, Hikvision, Suprema…). Mức A vì vậy dùng passkey (vân tay/Face ID của chính điện thoại).
- Camera thường ở mức A không có chống ảnh giả đủ mạnh; chỉ dùng demo/nội bộ. Mức B mới đạt an ninh thật (camera AI + hồng ngoại, kiểm thử theo ISO/IEC 30107-3).

### 1.5 Chế độ xác thực theo cổng (chủ trọ chọn)

| Chế độ | Cách vào | Dùng khi |
|---|---|---|
| Một yếu tố | Bất kỳ 1 trong: khuôn mặt / vân tay / QR | Cổng thường, cần nhanh |
| QR + sinh trắc | Quét QR rồi xác nhận khuôn mặt hoặc vân tay | Chống mượn điện thoại/thẻ |
| Bắt buộc khuôn mặt | Cổng tự nhận diện, không cần thao tác | Cổng chính, an ninh cao |
| Kèm ràng buộc | Khung giờ, hướng vào/ra, chống đi theo (anti-passback), số người mỗi lượt | Tuỳ nhu cầu |

Cổng còn xác thực xe (xe đạp, xe máy, ô tô — nếu bãi đủ chỗ) trên cùng hạ tầng: QR thẻ xe hoặc biển số là chủ thể của cổng; chi tiết ở Quản lý xe.

---

## 2. Luồng hoạt động

### 2.1 Đăng ký sinh trắc (enrollment)

1. Cư dân vào `/my/gate`: đăng ký khuôn mặt (chụp 1–3 ảnh theo hướng dẫn) hoặc vân tay thiết bị bằng passkey; vân tay mức B đăng ký tại máy quét của cổng.
2. Chủ trọ/Quản lý duyệt đăng ký đối chiếu hồ sơ + CCCD (`Tenant`), bật phương thức được dùng.
3. Hợp đồng thanh lý/hết hạn → khoá mọi chứng thư tự động; chủ trọ thu hồi tay bất kỳ lúc nào.

### 2.2 Cư dân vào cổng

Theo chế độ ở 1.5: kiosk nhận diện (khuôn mặt tại camera; vân tay tại máy quét, hoặc quét mã thử thách bằng điện thoại + vân tay/Face ID thiết bị; QR) → server kiểm tra **chứng thư + hợp đồng + cổng + khung giờ + anti-passback** → mở/không → ghi `GateLog` kèm phương thức và điểm khớp.

### 2.3 Khách vãng lai

1. **Chủ trọ cấp trước** (chọn phòng, thời hạn, thông tin bắt buộc, tuỳ chọn gắn ảnh khách) **hoặc khách quét QR cố định của nhà để tự xin**.
2. **Khách thuê cấp cho khách của mình** (nếu `GatePolicy.AllowTenantIssuePass = true`):
   - Chỉ trong phạm vi phòng mình đang thuê (hợp đồng `Approved`, còn hiệu lực).
   - **Đến chơi trong ngày:** được tự cấp và tự duyệt nếu `AutoApproveDayVisit = true`; nếu tắt thì phiếu `Pending` chờ chủ trọ/quản lý duyệt.
   - **Ở lại qua đêm:** luôn tạo phiếu `Pending`; **không được tự duyệt**. Chủ trọ/quản lý duyệt xong mới sinh QR + phiếu thông báo lưu trú.
   - Bị giới hạn bởi `TenantMaxPassesPerDay`, `TenantMaxTtlMinutes`, `TenantMaxUses`.
   - Mỗi phiếu do khách thuê cấp đều thông báo cho chủ trọ và ghi audit (`IssuerRole = Tenant`).
3. Khách tự khai báo (kèm câu hỏi đến chơi trong ngày hay ở lại qua đêm) → chủ trọ duyệt/từ chối (hoặc tự duyệt nếu bật và đã khai đủ).
4. Khách nhận QR một lần; nếu có ảnh đã gắn, có thể vào bằng khuôn mặt trong thời hạn phiếu.
5. Nếu qua đêm: hệ thống lập phiếu thông báo lưu trú và đếm hạn 23:00 hôm nay / 08:00 hôm sau — xem 2.5.
6. Hết hạn/hết lượt/thu hồi → từ chối, báo lý do.

### 2.4 Dự phòng

Quên điện thoại, mặt không khớp, camera lỗi → mã 6 số hoặc bảo vệ mở tay (bắt buộc lý do).

### 2.5 Thông báo lưu trú (bắt buộc pháp lý)

- **Căn cứ:** Luật số 118/2025/QH15 sửa đổi Luật Cư trú (Điều 30), hiệu lực 01/7/2026: khi có người lưu trú qua đêm, chủ nhà/cơ sở lưu trú phải thông báo cơ quan đăng ký cư trú. Hạn: trước 23:00 ngày bắt đầu; nếu khách đến sau 23:00 thì trước 08:00 sáng hôm sau. Chế tài: Nghị định 282/2025/NĐ-CP (sửa đổi bởi NĐ 347/2026/NĐ-CP), Điều 10.
- **Thông tin tối thiểu:** họ tên, ngày sinh, số định danh cá nhân hoặc số hộ chiếu, lý do lưu trú, thời gian lưu trú, địa chỉ lưu trú.
- **Kênh:** phần mềm ASM của Bộ Công an / web `tbltkbtt.bocongan.gov.vn` / VNeID → Thông báo lưu trú; dự phòng: sổ lưu trú, gọi cảnh sát khu vực.
- **Trong app (v1):** phiếu `StayReport` trạng thái `Pending → Reported` (đánh dấu đã gửi + kênh + giờ + ảnh xác nhận tuỳ chọn) `→ Overdue` nếu quá hạn; hủy nếu khách rời trước 23:00. Chưa có API công khai của ASM cho bên thứ ba (cần xác minh khi triển khai), nên app chuẩn bị dữ liệu đúng thứ tự trường + xuất CSV/sao chép để chủ trọ dán vào ASM/VNeID, **không tự gửi thay**.
- **Nhắc trách nhiệm:** hiển thị mức phạt tham khảo — cơ sở kinh doanh lưu trú 3 triệu (1–3 khách), 6 triệu (4–8), 10 triệu (≥9); hộ gia đình 750.000đ. App chỉ nhắc, nghĩa vụ pháp lý vẫn thuộc chủ trọ.
- **Tuỳ chọn:** quét QR CCCD/VNeID do khách chia sẻ để điền trước họ tên, ngày sinh, số định danh (khách chủ động xuất QR trên VNeID).
- **Không được tự động hoá thay nghĩa vụ:** phiếu do khách thuê cấp vẫn phải hiện nhãn "khách thuê cấp" và vẫn nhắc chủ trọ nếu có người ở qua đêm chưa khai báo.

---

## 3. Trạng thái

- **Phiếu khách:** `Pending → Approved → Used / Expired / Revoked / Rejected`. Chỉ `Approved` mới vào được; lần dùng cuối ⇒ `Used`.
- **Đăng ký sinh trắc:** `Pending → Active → Revoked` (kèm hạn lưu theo chính sách).
- **Thông báo lưu trú:** `Pending → Reported → Overdue`; `Cancelled` nếu khách rời trước hạn.

---

## 4. Chính sách khai báo khách (do chủ trọ cấu hình theo từng nhà)

| Trường khai báo | Mặc định | Ghi chú |
|---|---|---|
| Hình thức | Bắt buộc | Đến chơi trong ngày / Ở lại qua đêm — quyết định có phải thông báo lưu trú hay không |
| Họ tên | Bắt buộc | |
| Số điện thoại | Bắt buộc | |
| Ngày sinh | Bắt buộc khi qua đêm | Trường luật định |
| Số CCCD/định danh hoặc hộ chiếu | Bắt buộc khi qua đêm | Trường luật định; hiển thị che, tự ẩn/xoá sau N ngày (mặc định 30) |
| Ảnh CCCD | Tuỳ chọn | Lưu qua kho tệp hiện có (`StoredFile`); không bắt buộc nếu đã có số định danh |
| Lý do lưu trú | Bắt buộc khi qua đêm | Trường luật định |
| Thời gian lưu trú | Bắt buộc khi qua đêm | Trường luật định |
| Biển số xe | Tuỳ chọn | Xe gắn với phiếu — xem Quản lý xe |
| Phòng đến thăm / người liên hệ | Tuỳ chọn | Dùng để định tuyến thông báo |
| Mục đích vào | Tuỳ chọn | |

Chủ trọ tắt hết ⇒ khách đến chơi trong ngày vào nhanh, nhật ký ghi "không khai báo". Khách qua đêm thì bộ trường luật định (họ tên, ngày sinh, định danh/hộ chiếu, lý do, thời gian, địa chỉ) **luôn bắt buộc — không tắt được**.

---

## 5. Khách đến chơi vs lưu trú qua đêm

| Loại | Cách xác định | Hệ thống xử lý |
|---|---|---|
| Đến chơi trong ngày | Khách khai khi xin/cấp phiếu | Không cần thông báo lưu trú; phiếu ngắn hạn, tự hết |
| Ở lại qua đêm | Khách khai khi xin/cấp phiếu, hoặc hệ thống gợi ý khi phiếu còn hiệu lực qua 22:00 mà chưa có lượt ra (dựa `GateLog`) | Bắt buộc lập phiếu thông báo lưu trú (2.5) + nhắc hạn |

Hệ thống chỉ gợi ý dựa trên thời gian; chủ trọ xác nhận cuối cùng (có thể đánh dấu "không qua đêm").

---

## 6. Mở cổng vật lý & đồng bộ

- **Adapter `IGateController`:**
  - `SoftOpen`: báo bảo vệ / hiện "Hợp lệ" trên kiosk (mức A).
  - `Relay/webhook`: relay khô, OSDP/Wiegand tới bộ điều khiển, barrier/turnstile (mức B).
  - `Hãng khoá`: điểm cắm mở rộng.
- Mỗi lệnh mở chỉ có hiệu lực **1 lần**, tự đóng sau N giây; log ai mở, cổng nào, lý do.
- **Kiosk offline (mức B):** giữ chứng thư mã hoá tại thiết bị + hàng đợi `GateLog`, đồng bộ khi có mạng; quá hạn offline thì chặn.

---

## 7. Dữ liệu cần thêm

| Thực thể | Trường cốt lõi |
|---|---|
| `GatePass` | `Id` (int), `PropertyId` (int), `RoomId?` (int), `TenantId?` (int), `ContractId?` (**Guid** — trỏ `Contract.ContractId`), `PassType` (Resident/Guest/Service), `StayType` (Visit/Overnight), `TokenHash` (SHA-256, không lưu token thô), `VisitorInfoJson`, `VisitorPhotoRef?`, `Status`, `MaxUses`, `UsedCount`, `ExpiresAt`, `AllowedMethods`, **`IssuerRole`**, `IssuedByUserId`, **`ApprovedByUserId?`**, **`AutoApproved`**, `RevokedAt/Reason`, `StayReportId?`, `RowVersion` |
| `StayReport` | `Id`, `GatePassId?`, `PropertyId`, `RoomId?`, `ContractId?` (Guid), `GuestName`, `GuestDob`, `GuestIdNumber/Passport`, `StayReason`, `StayFrom`, `StayTo`, `AddressSnapshot`, `Status` (Pending/Reported/Overdue/Cancelled), `Channel` (Asm/WebBca/VNeId/Manual), `DueAt`, `ReportedAt`, `ReportedByUserId`, `EvidenceRef?`, `RowVersion` |
| `GatePolicy` | `PropertyId`, `GateId?`, chế độ xác thực (1.5), khung giờ, chống đi theo, các cờ khai báo (mục 4), `AutoApproveDayVisit`, `AutoApproveOvernight`, `PassTtlMinutes`, `RetentionDays`, cho phép Quản lý duyệt, **`AllowTenantIssuePass`**, **`TenantMaxPassesPerDay`**, **`TenantMaxTtlMinutes`**, **`TenantMaxUses`**, **`TenantMayIssueOvernight`**, kênh thông báo lưu trú ưu tiên, chính sách offline, `RowVersion` |
| `GateDevice` | `Id`, `PropertyId`, tên cổng, loại (kiosk/đầu đọc/máy quét/relay), mức A–B, trạng thái online, `LastSeenAt` |
| `BiometricEnrollment` | `Id`, chủ thể (`TenantId?` int / `GuestPassId?`), `Method` (Face/Fingerprint/Passkey/QrTotp), `TemplateRef` (server mã hoá hoặc id template trên thiết bị), `ConsentAt`, `EnrolledByUserId`, `Status`, `RevokedAt`, `RetentionUntil` |
| `GateLog` | `Id`, `PropertyId`, `GateDeviceId`, `SubjectType` (Person/Vehicle), `GatePassId?`, hướng (In/Out), phương thức (Face/Fingerprint/Qr/Code/Manual), kết quả + lý do, điểm khớp (nếu có), người vào (`UserId` hoặc tên khách), thời điểm |

**Tái dùng (đã có trên `dev`):** `Tenant` (`int Id`, `UserId?`, `CitizenId`, `DateOfBirth`), `Contract` (`Guid ContractId`, `Status = Approved`, `StartDate/EndDate`), `ContractMember` (`ContractId`, `TenantId`, `IsRepresentative`), `StoredFile`/`FileService` (ảnh CCCD, ảnh khách, ảnh đăng ký, ảnh xác nhận đã gửi lưu trú), `AuditLogEntry`, `AppNotification`.

**Ghi chú thiết kế:**

- `GatePolicy` có thể đặt **một bản ghi / nhà trọ** ở v1 (đơn giản), `GateId` để trống nghĩa là áp dụng chung; mở rộng theo từng cổng ở v2.
- `IssuerRole` lưu chuỗi enum (`ChuTro/QuanLy/KhachThue`) để truy vết và thống kê.
- Không lưu token QR thô; chỉ lưu `TokenHash`. Với TOTP, server giữ khoá ký và sinh mã phía server, không gửi khoá xuống client.
- Không có `Vehicle` trong dev: v1 bỏ phần xe/ANPR, chỉ để `SubjectType = Vehicle` và điểm cắm ở `GateLog` cho giai đoạn sau.

---

## 8. Phân quyền

| Hành động | Chủ trọ | Quản lý | Khách thuê | Khách vãng lai |
|---|---|---|---|---|
| Cấu hình cổng & chính sách khai báo | ✔ | – | – | – |
| Cấu hình thiết bị cổng | ✔ | – | – | – |
| Duyệt/từ chối khách, cấp/thu hồi thẻ | ✔ | Theo cấu hình | – | – |
| **Cấp/thu hồi QR khách của mình** | ✔ | Theo cấu hình | ✔ (giới hạn quota, chỉ phòng mình, không tự duyệt qua đêm) | – |
| Duyệt/thu hồi đăng ký sinh trắc | ✔ | Theo cấu hình | – | – |
| Đăng ký khuôn mặt/vân tay của mình | – | – | ✔ (chờ duyệt) | Theo phiếu nếu được bật |
| Lập/gửi thông báo lưu trú | ✔ | Theo cấu hình | – | – |
| Thẻ cư dân | Xem | Xem | Xem của mình | – |
| Xin vào, nhận QR một lần | – | – | – | ✔ (không cần tài khoản) |
| Mở tay | ✔ | Theo cấu hình | – | – |
| Xem nhật ký ra vào | ✔ | Nhà được giao | Lịch sử của mình | – |

**Ghi chú:** quyền của khách thuê chỉ có hiệu lực khi `GatePolicy.AllowTenantIssuePass = true` **và** hợp đồng còn hiệu lực. Qua đêm **không** nằm trong quyền tự duyệt của khách thuê.

---

## 9. Bảo mật & riêng tư

- Dữ liệu sinh trắc là **dữ liệu cá nhân nhạy cảm** theo Luật Bảo vệ dữ liệu cá nhân 2025 (số 91/2025/QH15, hiệu lực 01/01/2026; trước đó là NĐ 13/2023/NĐ-CP): có đồng ý rõ ràng, đúng mục đích, có thời hạn, không xuất, xoá khi thu hồi/hết hạn lưu; lập hồ sơ đánh giá tác động xử lý dữ liệu cá nhân.
- Nếu dùng dịch vụ nhận diện đặt ở nước ngoài: thêm hồ sơ đánh giá tác động chuyển dữ liệu ra nước ngoài; ưu tiên xử lý trong nước hoặc model nội bộ cho dữ liệu sinh trắc.
- Ảnh gốc đăng ký để trong kho riêng (không hiển thị công khai); chỉ lưu vector đã xử lý để so khớp; không ghi ảnh/vector vào log. Với passkey, server chỉ giữ public key — sinh trắc không rời thiết bị.
- Ảnh khách/CCCD chỉ lưu khi chủ trọ bật; tự xoá theo hạn lưu (mặc định 30 ngày với khách).
- Ngưỡng khớp + giới hạn số lần thử + khoá tạm sau N lần sai + cảnh báo chủ trọ.
- QR dùng TOTP/token ngẫu nhiên, server chỉ lưu hash, xoay vòng, dùng một lần.
- **Không xác thực khuôn mặt bằng ảnh gửi từ xa** — chỉ chụp tại cổng.
- Phiếu do khách thuê cấp: hiển thị nhãn nguồn, không che giấu khỏi chủ trọ; chủ trọ có quyền thu hồi và xem toàn bộ.

---

## 10. Liên hệ hệ thống

| Module | Liên hệ |
|---|---|
| Hợp đồng | Điều kiện tự cấp/khoá chứng thư (QR, sinh trắc) |
| Khách thuê + tài liệu | Đối chiếu khi duyệt đăng ký sinh trắc, ảnh CCCD khách |
| Thông báo | Yêu cầu vào cổng → báo chủ trọ; duyệt/từ chối → báo khách; nhắc hạn thông báo lưu trú; cảnh báo lạm dụng |
| Thông báo lưu trú | Phiếu qua đêm → nhắc hạn 23:00/08:00, lưu kênh/bằng chứng, xuất dữ liệu cho ASM / tbltkbtt / VNeID |
| Duyệt (Approvals) | Dùng cùng mô hình "quyết định một lần + lý do"; phiếu cổng ở module riêng, không trộn mã SW |
| Quản lý xe | Cùng hạ tầng cổng, `GateLog` chung — xem Quản lý xe |
| Audit | Mọi đăng ký/thu hồi/mở cổng/gửi lưu trú đều ghi log |
| Dashboard | Thẻ KPI "Đang trong nhà", "Khách qua đêm chưa khai báo" (tuỳ chọn) |
| Tìm kiếm | Tìm phiếu ra vào / khách đang trong nhà — xem Tìm kiếm |

---

## 11. Mở rộng sau

Tích hợp API ASM khi có kênh chính thức, đăng ký tạm trú cho khách thuê dài hạn, camera phát hiện người lạ, tích hợp thang máy/chấm công bảo vệ, báo cáo lưu trú xuất file, API mở cổng cho đối tác giao hàng. Nhận diện biển số xe thuộc Quản lý xe.

---

## 12. Quyết định đã chốt & điểm mở

### 12.1 Đã chốt trong bản này

| Mã | Quyết định | Ngày |
|---|---|---|
| D-AC-01 | **Khách thuê được cấp QR cho khách của mình**, không bắt buộc chỉ Chủ trọ | 22/11/2026 |
| D-AC-02 | Khách thuê **không được tự duyệt phiếu qua đêm**; luôn cần Chủ trọ/Quản lý duyệt | 22/11/2026 |
| D-AC-03 | Mặc định chỉ khách đến chơi trong ngày được auto-approve khi khách thuê cấp; qua đêm luôn `Pending` | 22/11/2026 |
| D-AC-04 | Mỗi phiếu khách thuê cấp đều thông báo chủ trọ + audit `IssuerRole = Tenant`, chủ trọ thu hồi được | 22/11/2026 |
| D-AC-05 | Dùng lại `INotificationService` + bảng `AppNotifications` có sẵn cho mọi thông báo; không tạo service thông báo mới | 22/11/2026 |
| D-AC-06 | Phiếu cổng có **hàng đợi duyệt riêng** (`GatePass.Status`); **không** nhét vào `ManagerApprovalRequest` vì bảng đó gắn `OperationCode`/`ApprovalTargetType` của công tác Quản lý | 22/11/2026 |
| D-AC-07 | v1 **chỉ mức A** (kiosk + bảo vệ mở tay); viết `IGateController` + adapter mức B để cắm sau, không tính vào phạm vi | 22/11/2026 |
| D-AC-08 | v1 **bỏ phần xe/ANPR** vì dev chưa có module Quản lý xe; chỉ giữ `SubjectType = Vehicle` trong `GateLog` làm điểm cắm | 22/11/2026 |

### 12.2 Điểm mở cần nhóm chốt trước khi code

| Mã | Điểm mở | Đề xuất | Người chốt |
|---|---|---|---|
| O-AC-01 | Quota mặc định cho khách thuê | 3 phiếu/ngày, TTL 60 phút, 1 lượt/phiếu | Chủ trọ cấu hình, nhóm chốt mặc định |
| O-AC-02 | `TenantMayIssueOvernight` có cho phép bật không? | Mặc định `false`; nếu bật vẫn phải chờ duyệt | Nhóm + tư vấn pháp lý |
| O-AC-03 | Quản lý có được duyệt phiếu cổng không? (Approvals hiện chỉ Chủ trọ duyệt) | Cho phép theo cờ `GatePolicy.AllowManagerApprove`, mặc định `false` | Nhóm |

> Đã bỏ khỏi danh sách treo: nhu cầu `INotificationService` (đã có trên `dev`) và các vấn đề nhãn/mã Jira (không tạo issue mới — đây là chức năng bổ sung).

---

## 13. Đối chiếu nền code hiện tại & phần phải làm

> Đối chiếu trên nhánh `dev` @ `0de1572` (22/11/2026). Kết luận: **dev chưa có module ra vào cổng**, nhưng hạ tầng dùng lại được gần như đủ. Phần phải viết mới tập trung ở `Features/AccessControl` + UI.

### 13.1 Có sẵn — dùng lại, không viết lại

| Hạng mục | Trong code | Dùng cho |
|---|---|---|
| Vai trò | `Features/Accounts/Roles.cs` — `Admin/ChuTro/QuanLy/KhachThue`, claim `nhatro.active_role` | Ma trận quyền mục 8 |
| Phạm vi | `PropertyScope`, `RoomScope`, `TenantScope`; `PropertyManager` còn hiệu lực | Lọc phiếu/nhật ký theo nhà, phòng |
| Audit | `AuditLogService.RecordAsync(category, action, actorUserId, propertyId, metadata, occurredAt)` | Ghi mọi thao tác cấp/thu hồi/duyệt/mở cổng/gửi lưu trú |
| Thông báo in-app | `Features/Notifications` — `INotificationService.NotifyAsync(userId, title, body, link)`, bảng `AppNotifications`, đã đăng ký DI | Báo chủ trọ khi khách thuê cấp phiếu; báo khách khi duyệt/từ chối; nhắc hạn lưu trú |
| Tệp | `Features/Storage` — `StoredFile`, `FileService`, `FileCategory`, `RelatedEntityType` | Ảnh CCCD, ảnh khách, ảnh đăng ký sinh trắc, ảnh bằng chứng đã gửi lưu trú |
| Thời gian | `TimeProvider` (singleton) | TOTP, hạn phiếu, mốc 23:00/08:00 |
| Duyệt | `Features/Approvals` — `ManagerApprovalRequest`, `IApprovalApplier`, `ManagerApprovalService`; chỉ Chủ trọ duyệt, đối tượng `Room/Tenant/Contract/UtilityPeriod/Incident/Invoice/Report` | Chỉ mượn mô hình trạng thái + lý do + audit; **không** dùng làm hàng đợi phiếu cổng |
| Hợp đồng | `Contract` — khoá `Guid ContractId`, `Status` (`Approved` = hiệu lực), `StartDate/EndDate`; `ContractMember` (`ContractId`, `TenantId`, `IsRepresentative`) | Điều kiện cư dân; xác định khách thuê thuộc phòng nào |
| Khách thuê | `Tenant` — khoá `int Id`, `UserId?` liên kết tài khoản, `CitizenId`, `DateOfBirth` | Đối chiếu danh tính, điền sẵn phiếu lưu trú |
| DB | `Data/ApplicationDbContext.cs`, `Data/Configurations/<Module>/`, migration ở `Data/Migrations/` | Schema mới |
| Song ngữ | `Resources/Features/<Module>/<Module>Resources.{vi-VN,en-US}.resx` | Nhãn UI mới |

### 13.2 Chưa có — phải làm mới

| Hạng mục | Đường dẫn dự kiến | Ghi chú |
|---|---|---|
| Thực thể + enum + DTO | `Features/AccessControl/Entities/`, `Enums/`, `DTOs/` | `GatePass`, `StayReport`, `GatePolicy`, `GateDevice`, `BiometricEnrollment`, `GateLog` |
| Cấu hình EF + migration | `Data/Configurations/AccessControl/`, `Data/Migrations/` | Thêm `DbSet` vào `ApplicationDbContext` |
| Service nghiệp vụ | `Features/AccessControl/Services/` | Cấu hình cổng, phiếu khách, token/TOTP, sinh trắc, kiểm soát lượt vào, lưu trú |
| Mở cổng | `Features/AccessControl/Gates/IGateController.cs` | `SoftOpenGateController` (mức A) + adapter mức B |
| UI chủ trọ/quản lý | `/gate/passes`, `/gate/logs`, `/gate/settings`, `/gate/kiosk` | Kiosk toàn màn hình |
| UI khách thuê | `/my/gate` | QR động, cấp khách, đăng ký sinh trắc |
| Tài nguyên | `Resources/Features/AccessControl/` | VI/EN đủ key |
| Kiểm thử | `NhaTro.UnitTests`, `NhaTro.IntegrationTests`, `NhaTro.E2ETests` | Quyền, quota, token, mốc 23:00/08:00 |

### 13.3 Thứ tự làm đề xuất

1. Enum + DTO + ma trận quyền/trạng thái (chốt với nhóm, gồm các điểm `O-AC-01…03`).
2. Entity + cấu hình EF + migration.
3. `GatePolicyService` + `GatePassService` (gồm quota và nhánh khách thuê cấp phiếu).
4. `GateTokenService` (QR động/QR một lần/mã 6 số) + `IGateController` (mức A).
5. `GateAccessService` (kiểm tra + `GateLog`) + `StayReportService` (hạn 23:00/08:00).
6. Đăng ký sinh trắc/passkey.
7. UI `/gate/*` và `/my/gate`.
8. Test quyền/luồng + tài liệu.

## 14. Tham chiếu (kiểm chứng 20/11/2026)

| Chủ đề | Nguồn |
|---|---|
| Thông báo lưu trú: hạn 23:00/08:00, thông tin, kênh, mức phạt | Tuổi Trẻ — Công an hướng dẫn khai báo lưu trú (07/7/2026) |
| Luật Cư trú sửa đổi (Luật 118/2025/QH15), hiệu lực 01/7/2026 | LuatVietnam |
| Mức phạt Điều 10 NĐ 282/2025/NĐ-CP (sửa đổi bởi NĐ 347/2026/NĐ-CP) | Thư Viện Pháp Luật |
| ASM — phần mềm thông báo lưu trú của Bộ Công an | Cục Cảnh sát QLHC về TTXH |
| VNeID chia sẻ thông tin/QR CCCD | Thị trường Tài chính Tiền tệ |
| Luật BVDLCN 2025 (91/2025/QH15), hiệu lực 01/01/2026 | Cổng TTĐT Chính phủ |
| WebAuthn/passkey | MDN, W3C |
| Chống ảnh giả khuôn mặt (PAD, ISO/IEC 30107-3) | NCheck, Innovatrics |
| Thiết bị cổng (OSDP, Wiegand, relay) | Hikvision MinMoe, ZKTeco |
| Máy quét vân tay + web (SDK/OSDP, WebAuthn) | Biometric Scanner Integration, Stack Overflow Blog |
| QR xoay vòng chống chụp màn hình | Invixium, AccessFlow |

> Số liệu phạt và hạn thông báo có thể thay đổi theo văn bản sửa đổi; khi triển khai thật cần đối chiếu lại văn bản hiện hành.

---
