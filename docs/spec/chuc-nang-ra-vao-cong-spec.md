# SPEC — Ra vào cổng (Access Control: QR + vân tay + khuôn mặt)

> Trạng thái: **đặc tả, chưa code**. Phạm vi v1, thiết kế để mở rộng.
> Đã đối chiếu pháp lý & kỹ thuật ngày 20/11/2026 — nguồn ở mục 2.
> Nền: NhaTro (Blazor .NET 10) — vai trò `Admin`, `ChuTro`, `QuanLy`, `KhachThue` + khách chưa có tài khoản.
> Liên quan: [Quản lý xe](chuc-nang-quan-ly-xe-spec.md) · [Trợ lý AI](chuc-nang-tro-ly-ai-spec.md) · [Tìm kiếm toàn cục](chuc-nang-tim-kiem-toan-cuc-spec.md)

## 0. Quy ước chung

| Mục | Quy ước |
|---|---|
| Nguyên tắc ghi dữ liệu | Mọi thao tác ghi qua service nghiệp vụ hiện có + audit; khách vãng lai không ghi trực tiếp |
| Phạm vi dữ liệu | Theo scope sẵn có (`PropertyScope`, `RoomScope`, `TenantScope`…): Chủ trọ = nhà mình, Quản lý = nhà được giao, Khách thuê = dữ liệu của mình |
| Thông báo | Dùng `INotificationService.NotifyAsync(userId, title, body, link)` để mở đúng màn xử lý |
| Mở rộng | Điểm cắm riêng (provider/config/adapter) — thêm không phá lõi |

## 1. Chức năng ra vào cổng (Access Control)

> Gộp **QR + vân tay + khuôn mặt** thành **một** chức năng duy nhất: mỗi người có một hoặc nhiều "chứng thư" (credential), mỗi cổng có chế độ xác thực riêng do chủ trọ chọn. QR không đứng một mình mà là một yếu tố trong hệ thống ra vào. Xe dùng chung cổng này — xem [Quản lý xe](chuc-nang-quan-ly-xe-spec.md).

### 1.1 Mục tiêu

- Kiểm soát ai được vào/ra, có nhật ký và báo cáo cho chủ trọ.
- Người trong trọ vào nhanh bằng **khuôn mặt, vân tay hoặc QR**; người ngoài phải có phiếu do chủ trọ cấp/duyệt.
- Chủ trọ tự quyết: mức xác thực của từng cổng và có bắt khai báo thông tin khách hay không.
- Lưu ý pháp lý: khách **ở lại qua đêm** bắt buộc phải thông báo lưu trú (1.9–1.10); chủ trọ chỉ được tắt khai báo với khách đến chơi trong ngày.

### 1.2 Phương thức xác thực

| Phương thức | Ai dùng | Dữ liệu lưu ở đâu | Thiết bị | Ghi chú |
|---|---|---|---|---|
| **Khuôn mặt** | Cư dân; khách có gắn ảnh vào phiếu (tuỳ chọn) | Vector khuôn mặt (embedding) mã hoá tại server | Camera tại cổng | Chỉ so khớp **tại cổng**, có kiểm tra ảnh sống (liveness); ngưỡng khớp cấu hình được |
| **Vân tay** | Cư dân; người phục vụ định kỳ | Mẫu vân tay nằm **trên máy quét tại cổng** (không lên server); với điện thoại cá nhân, sinh trắc dùng passkey thiết bị (server chỉ giữ public key) | Máy quét vân tay tại cổng (mức B); hoặc kiosk hiện mã thử thách, cư dân quét bằng điện thoại + xác nhận vân tay/Face ID thiết bị (mức A) | Server chỉ nhận "khớp/không khớp", không nhận mẫu vân tay |
| **QR động** | Cư dân | App khách thuê; server giữ hash token | Camera kiosk quét | TOTP theo thời gian (RFC 6238), làm mới mỗi 60 giây, kiosk chấp nhận ±1 bước; không chứa PII |
| **QR một lần** | Khách vãng lai | Server giữ hash token | Camera kiosk hoặc link mở trên điện thoại khách | Có hạn + số lần dùng, mặc định **1 lần / 60 phút** |
| **Mã 6 số** | Tất cả (dự phòng) | App / phiếu khách | Nhập tại kiosk | Dùng khi camera/hệ sinh trắc lỗi |
| **Bảo vệ mở tay** | Bất kỳ | Log | Console bảo vệ / app chủ trọ | Bắt buộc ghi lý do, có audit |

### 1.3 Loại người được vào

| Loại | Chứng thư mặc định | Thời hạn | Ghi chú |
|---|---|---|---|
| **Cư dân** (hợp đồng hiệu lực: `Contract.Status = Approved`, trong khoảng Start–End) | QR động + khuôn mặt/vân tay đã đăng ký | Theo hợp đồng, tự khoá khi thanh lý/hết hạn | Dùng được 1–3 phương thức tuỳ đăng ký |
| **Khách vãng lai** | QR một lần (chủ trọ cấp hoặc duyệt) | Mặc định 1 lần / 60 phút, gia hạn được | Có thể gắn ảnh khách để vào bằng khuôn mặt trong thời hạn phiếu |
| **Người phục vụ định kỳ** (shipper, giúp việc…) | Thẻ định kỳ: QR / vân tay / khuôn mặt + khung giờ | Theo thẻ, thu hồi bất kỳ lúc nào | Giới hạn ngày/giờ, không vào khu vực ngoài phạm vi |
| **Chủ trọ / Quản lý** | QR quản trị hoặc mở tay | Không giới hạn theo hợp đồng | Mọi lần mở tay đều ghi log |

### 1.4 Hai mức triển khai

| | **Mức A** — không cần thiết bị ngoài (mặc định v1) | **Mức B** — có thiết bị tại cổng |
|---|---|---|
| Kiosk | Điện thoại/máy tính bảng đặt ở cổng, chế độ toàn màn hình | Kiosk chuyên dụng |
| Khuôn mặt | Camera kiosk chụp → server so khớp | Camera AI + chống ảnh giả |
| Vân tay | Kiosk hiện mã thử thách → cư dân quét bằng điện thoại + xác nhận vân tay/Face ID thiết bị | Máy quét vân tay tại cổng |
| QR | Camera kiosk quét | Đầu đọc QR |
| Mở cổng | Hiện "Hợp lệ" cho bảo vệ / bấm xác nhận trong app | Webhook/HTTP tới relay, khoá điện, barrier, turnstile |
| Offline | Cần mạng | Kiosk giữ chứng thư mã hoá + hàng đợi nhật ký, đồng bộ khi có mạng |

V1 chạy được ngay ở **mức A**; chuyển sang mức B chỉ thay lớp thiết bị, không đổi luồng nghiệp vụ.

Hai giới hạn kỹ thuật cần biết trước:
- Trình duyệt **không đọc trực tiếp** máy quét vân tay; mức B phải qua SDK/OSDP–Wiegand của hãng (ZKTeco, Hikvision, Suprema…). Mức A vì vậy dùng passkey (vân tay/Face ID của chính điện thoại).
- Camera thường ở mức A **không có chống ảnh giả** đủ mạnh; chỉ dùng demo/nội bộ. Mức B mới đạt an ninh thật (camera AI + hồng ngoại, kiểm thử theo ISO/IEC 30107-3).

### 1.5 Chế độ xác thực theo cổng (chủ trọ chọn)

| Chế độ | Cách vào | Dùng khi |
|---|---|---|
| **Một yếu tố** | Bất kỳ 1 trong: khuôn mặt / vân tay / QR | Cổng thường, cần nhanh |
| **QR + sinh trắc** | Quét QR rồi xác nhận khuôn mặt hoặc vân tay | Chống mượn điện thoại/thẻ |
| **Bắt buộc khuôn mặt** | Cổng tự nhận diện, không cần thao tác | Cổng chính, an ninh cao |
| **Kèm ràng buộc** | Khung giờ, hướng vào/ra, chống đi theo (anti-passback), số người mỗi lượt | Tuỳ nhu cầu |

Cổng còn xác thực **xe** (xe đạp, xe máy, ô tô — nếu bãi đủ chỗ) trên cùng hạ tầng: QR thẻ xe hoặc biển số là chủ thể của cổng; chi tiết ở [Quản lý xe](chuc-nang-quan-ly-xe-spec.md).

### 1.6 Luồng hoạt động

**A. Đăng ký sinh trắc (enrollment):**
1. Cư dân vào `/my/gate`: đăng ký khuôn mặt (chụp 1–3 ảnh theo hướng dẫn) hoặc vân tay thiết bị bằng passkey; vân tay mức B đăng ký tại máy quét của cổng.
2. Chủ trọ/Quản lý duyệt đăng ký đối chiếu hồ sơ + CCCD (`Tenant`), bật phương thức được dùng.
3. Hợp đồng thanh lý/hết hạn → khoá mọi chứng thư tự động; chủ trọ thu hồi tay bất kỳ lúc nào.

**B. Cư dân vào cổng** (theo chế độ ở 1.5): kiosk nhận diện (khuôn mặt tại camera; vân tay tại máy quét, hoặc quét mã thử thách bằng điện thoại + vân tay/Face ID thiết bị; QR) → server kiểm tra chứng thư + hợp đồng + cổng + khung giờ → mở/không → ghi `GateLog` kèm phương thức và điểm khớp.

**C. Khách vãng lai:**
1. Chủ trọ cấp trước (chọn phòng, thời hạn, thông tin bắt buộc, tuỳ chọn gắn ảnh khách) **hoặc** khách quét QR cố định của nhà để tự xin.
2. Khách tự khai báo (kèm câu hỏi **đến chơi trong ngày hay ở lại qua đêm**) → chủ trọ duyệt/từ chối (hoặc tự duyệt nếu bật và đã khai đủ).
3. Khách nhận QR một lần; nếu có ảnh đã gắn, có thể vào bằng khuôn mặt trong thời hạn phiếu.
4. Nếu **qua đêm**: hệ thống lập phiếu thông báo lưu trú và đếm hạn 23:00 hôm nay / 08:00 hôm sau — xem 1.10.
5. Hết hạn/hết lượt/thu hồi → từ chối, báo lý do.

**D. Dự phòng:** quên điện thoại, mặt không khớp, camera lỗi → mã 6 số hoặc bảo vệ mở tay (bắt buộc lý do).

### 1.7 Trạng thái

- Phiếu khách: `Pending` → `Approved` → `Used` / `Expired` / `Revoked` / `Rejected`. Chỉ `Approved` mới vào được; lần dùng cuối ⇒ `Used`.
- Đăng ký sinh trắc: `Pending` → `Active` → `Revoked` (kèm hạn lưu theo chính sách).

### 1.8 Chính sách khai báo khách (do chủ trọ cấu hình theo từng nhà)

| Trường khai báo | Mặc định | Ghi chú |
|---|---|---|
| **Hình thức** | **Bắt buộc** | Đến chơi trong ngày / **Ở lại qua đêm** — quyết định có phải thông báo lưu trú hay không |
| Họ tên | **Bắt buộc** | |
| Số điện thoại | Bắt buộc | |
| Ngày sinh | Bắt buộc khi qua đêm | Trường luật định |
| Số CCCD/định danh hoặc hộ chiếu | Bắt buộc khi qua đêm | Trường luật định; hiển thị che, tự ẩn/xoá sau N ngày (mặc định 30) |
| Ảnh CCCD | Tuỳ chọn | Lưu qua kho tệp hiện có (`StoredFile`); không bắt buộc nếu đã có số định danh |
| Lý do lưu trú | Bắt buộc khi qua đêm | Trường luật định |
| Thời gian lưu trú | Bắt buộc khi qua đêm | Trường luật định |
| Biển số xe | Tuỳ chọn | Xe gắn với phiếu — xem [Quản lý xe](chuc-nang-quan-ly-xe-spec.md) |
| Phòng đến thăm / người liên hệ | Tuỳ chọn | Dùng để định tuyến thông báo |
| Mục đích vào | Tuỳ chọn | |

Chủ trọ tắt hết ⇒ khách **đến chơi trong ngày** vào nhanh, nhật ký ghi "không khai báo". Khách **qua đêm** thì bộ trường luật định (họ tên, ngày sinh, định danh/hộ chiếu, lý do, thời gian, địa chỉ) luôn bắt buộc — không tắt được.

### 1.9 Khách đến chơi vs lưu trú qua đêm

| Loại | Cách xác định | Hệ thống xử lý |
|---|---|---|
| **Đến chơi trong ngày** | Khách khai khi xin/cấp phiếu | Không cần thông báo lưu trú; phiếu ngắn hạn, tự hết |
| **Ở lại qua đêm** | Khách khai khi xin/cấp phiếu, **hoặc** hệ thống gợi ý khi phiếu còn hiệu lực qua 22:00 mà chưa có lượt ra (dựa `GateLog`) | Bắt buộc lập phiếu thông báo lưu trú (1.10) + nhắc hạn |

Hệ thống chỉ **gợi ý** dựa trên thời gian; chủ trọ xác nhận cuối cùng (có thể đánh dấu "không qua đêm").

### 1.10 Thông báo lưu trú (bắt buộc pháp lý)

- **Căn cứ**: Luật số 118/2025/QH15 sửa đổi Luật Cư trú (Điều 30), hiệu lực **01/7/2026**: khi có người lưu trú qua đêm, chủ nhà/cơ sở lưu trú phải thông báo cơ quan đăng ký cư trú. **Hạn**: trước **23:00 ngày bắt đầu**; nếu khách đến sau 23:00 thì trước **08:00 sáng hôm sau**. Chế tài: Nghị định 282/2025/NĐ-CP (sửa đổi bởi NĐ 347/2026/NĐ-CP), Điều 10.
- **Thông tin tối thiểu**: họ tên, ngày sinh, số định danh cá nhân hoặc số hộ chiếu, lý do lưu trú, thời gian lưu trú, địa chỉ lưu trú.
- **Kênh**: phần mềm ASM của Bộ Công an / web `tbltkbtt.bocongan.gov.vn` / VNeID → Thông báo lưu trú; dự phòng: sổ lưu trú, gọi cảnh sát khu vực.
- **Trong app (v1)**: phiếu `StayReport` trạng thái `Pending` → `Reported` (đánh dấu đã gửi + kênh + giờ + ảnh xác nhận tuỳ chọn) → `Overdue` nếu quá hạn; hủy nếu khách rời trước 23:00. Hiện chưa thấy API công khai của ASM cho bên thứ ba (cần xác minh khi triển khai), nên app **chuẩn bị dữ liệu đúng thứ tự trường + xuất CSV/sao chép** để chủ trọ dán vào ASM/VNeID, không tự gửi thay.
- **Nhắc trách nhiệm**: hiển thị mức phạt tham khảo — cơ sở kinh doanh lưu trú 3 triệu (1–3 khách), 6 triệu (4–8), 10 triệu (≥9); hộ gia đình 750.000đ. App chỉ nhắc, nghĩa vụ pháp lý vẫn thuộc chủ trọ.
- **Tuỳ chọn**: quét QR CCCD/VNeID do khách chia sẻ để điền trước họ tên, ngày sinh, số định danh (khách chủ động xuất QR trên VNeID).

### 1.11 Mở cổng vật lý & đồng bộ

- Adapter `IGateController`: `SoftOpen` (báo bảo vệ), `Relay`/webhook (mức B — relay khô, OSDP/Wiegand tới bộ điều khiển, barrier/turnstile), hãng khoá (mở rộng).
- Mỗi lệnh mở chỉ có hiệu lực 1 lần, tự đóng sau N giây; log ai mở, cổng nào, lý do.
- Kiosk offline (mức B): giữ chứng thư mã hoá tại thiết bị + hàng đợi `GateLog`, đồng bộ khi có mạng; quá hạn offline thì chặn.

### 1.12 Dữ liệu cần thêm

| Thực thể | Trường cốt lõi |
|---|---|
| `GatePass` | Id, PropertyId, RoomId?, TenantId?, PassType (`Resident`/`Guest`/`Service`), StayType (`Visit`/`Overnight`), TokenHash (SHA-256, không lưu token thô), VisitorInfoJson, VisitorPhotoRef?, Status, MaxUses, UsedCount, ExpiresAt, AllowedMethods, IssuedByUserId, RevokedAt/Reason, StayReportId?, RowVersion |
| `StayReport` | Id, GatePassId?, PropertyId, RoomId?, GuestName, GuestDob, GuestIdNumber/Passport, StayReason, StayFrom, StayTo, AddressSnapshot, Status (`Pending`/`Reported`/`Overdue`), Channel (`Asm`/`WebBca`/`VNeId`/`Manual`), DueAt, ReportedAt, ReportedByUserId, EvidenceRef?, RowVersion |
| `GatePolicy` | PropertyId, GateId?, Chế độ xác thực (1.5), khung giờ, chống đi theo, các cờ khai báo (1.8), AutoApprove, PassTtlMinutes, RetentionDays, cho phép Quản lý duyệt, kênh thông báo lưu trú ưu tiên, chính sách offline |
| `GateDevice` | Id, PropertyId, Tên cổng, Loại (kiosk/đầu đọc/máy quét/relay), Mức A–B, Trạng thái online, LastSeenAt |
| `BiometricEnrollment` | Id, Chủ thể (TenantId/khách), Method (`Face`/`Fingerprint`/`Passkey`), TemplateRef (server mã hoá hoặc id template trên thiết bị), ConsentAt, EnrolledByUserId, Status, RevokedAt, RetentionUntil |
| `GateLog` | Id, PropertyId, GateDeviceId, SubjectType (`Person`/`Vehicle`), GatePassId?, VehicleId?, Hướng (`In`/`Out`), Phương thức (`Face`/`Fingerprint`/`Qr`/`Anpr`/`Code`/`Manual`), Kết quả + lý do, Điểm khớp (nếu có), Người vào (UserId hoặc tên khách), Thời điểm |

Tái dùng: `Tenant` (đối chiếu người), `Contract` (điều kiện cư dân), `StoredFile` (ảnh CCCD, ảnh khách, ảnh đăng ký, ảnh xác nhận đã gửi lưu trú), `AppNotification`, `AuditLog`.

### 1.13 Phân quyền

| Hành động | Chủ trọ | Quản lý | Khách thuê | Khách vãng lai |
|---|---|---|---|---|
| Cấu hình cổng & chính sách khai báo | ✔ | – | – | – |
| Duyệt/từ chối khách, cấp/thu hồi thẻ | ✔ | Theo cấu hình | – | – |
| Duyệt/thu hồi đăng ký sinh trắc | ✔ | Theo cấu hình | – | – |
| Lập/gửi thông báo lưu trú | ✔ | Theo cấu hình | – | – |
| Đăng ký khuôn mặt/vân tay của mình | – | – | ✔ (chờ duyệt) | Theo phiếu nếu được bật |
| Thẻ cư dân | Xem | Xem | Xem của mình | – |
| Xin vào, nhận QR một lần | – | – | – | ✔ (không cần tài khoản) |
| Mở tay | ✔ | Theo cấu hình | – | – |
| Xem nhật ký ra vào | ✔ | Nhà được giao | Lịch sử của mình | – |

### 1.14 Bảo mật & riêng tư

- Dữ liệu sinh trắc là **dữ liệu cá nhân nhạy cảm** theo Luật Bảo vệ dữ liệu cá nhân 2025 (số 91/2025/QH15, hiệu lực 01/01/2026; trước đó là NĐ 13/2023/NĐ-CP): có **đồng ý rõ ràng**, đúng mục đích, có thời hạn, không xuất, xoá khi thu hồi/hết hạn lưu; lập **hồ sơ đánh giá tác động xử lý dữ liệu cá nhân**.
- Nếu dùng dịch vụ nhận diện đặt ở nước ngoài: thêm **hồ sơ đánh giá tác động chuyển dữ liệu ra nước ngoài**; ưu tiên xử lý trong nước hoặc model nội bộ cho dữ liệu sinh trắc.
- Ảnh gốc đăng ký để trong kho riêng (không hiển thị công khai); chỉ lưu vector đã xử lý để so khớp; không ghi ảnh/vector vào log. Với passkey, server chỉ giữ public key — sinh trắc không rời thiết bị.
- Ảnh khách/CCCD chỉ lưu khi chủ trọ bật; tự xoá theo hạn lưu (mặc định 30 ngày với khách).
- Ngưỡng khớp + giới hạn số lần thử + khoá tạm sau N lần sai + cảnh báo chủ trọ.
- QR dùng TOTP/token ngẫu nhiên, server chỉ lưu hash, xoay vòng, dùng một lần.
- Không xác thực khuôn mặt bằng ảnh gửi từ xa — chỉ chụp **tại cổng**.

### 1.15 Liên hệ hệ thống

| Module | Liên hệ |
|---|---|
| Hợp đồng | Điều kiện tự cấp/khoá chứng thư (QR, sinh trắc) |
| Khách thuê + tài liệu | Đối chiếu khi duyệt đăng ký sinh trắc, ảnh CCCD khách |
| Thông báo | Yêu cầu vào cổng → báo chủ trọ; duyệt/từ chối → báo khách; nhắc hạn thông báo lưu trú; cảnh báo lạm dụng |
| Thông báo lưu trú | Phiếu qua đêm → nhắc hạn 23:00/08:00, lưu kênh/bằng chứng, xuất dữ liệu cho ASM / tbltkbtt / VNeID |
| Duyệt (Approvals) | Dùng cùng mô hình "quyết định một lần + lý do"; phiếu cổng ở module riêng, không trộn mã SW |
| Quản lý xe | Cùng hạ tầng cổng, `GateLog` chung — xem [Quản lý xe](chuc-nang-quan-ly-xe-spec.md) |
| Audit | Mọi đăng ký/thu hồi/mở cổng/gửi lưu trú đều ghi log |
| Dashboard | Thẻ KPI "Đang trong nhà", "Khách qua đêm chưa khai báo" (tuỳ chọn) |
| Tìm kiếm | Tìm phiếu ra vào / khách đang trong nhà — xem [Tìm kiếm](chuc-nang-tim-kiem-toan-cuc-spec.md) |

### 1.16 Mở rộng sau

Tích hợp API ASM khi có kênh chính thức, đăng ký tạm trú cho khách thuê dài hạn, camera phát hiện người lạ, tích hợp thang máy/chấm công bảo vệ, báo cáo lưu trú xuất file, API mở cổng cho đối tác giao hàng. Nhận diện biển số xe thuộc [Quản lý xe](chuc-nang-quan-ly-xe-spec.md).

## 2. Tham chiếu (kiểm chứng 20/11/2026)

| Chủ đề | Nguồn |
|---|---|
| Thông báo lưu trú: hạn 23:00/08:00, thông tin, kênh, mức phạt | [Tuổi Trẻ — Công an hướng dẫn khai báo lưu trú (07/7/2026)](https://tuoitre.vn/cong-an-dia-phuong-huong-dan-cu-the-cach-khai-bao-luu-tru-theo-quy-dinh-moi-100260707102644935.htm) |
| Luật Cư trú sửa đổi (Luật 118/2025/QH15), hiệu lực 01/7/2026 | [LuatVietnam](https://luatvietnam.vn/tin-van-ban-moi/tu-01-7-2026-nguoi-yeu-den-nha-ngu-qua-dem-phai-khai-bao-luu-tru-186-110046-article.html) |
| Mức phạt Điều 10 NĐ 282/2025/NĐ-CP (sửa đổi bởi NĐ 347/2026/NĐ-CP) | [Thư Viện Pháp Luật](https://thuvienphapluat.vn/van-ban/Vi-pham-hanh-chinh/Nghi-dinh-282-2025-ND-CP-xu-phat-vi-pham-hanh-chinh-linh-vuc-an-ninh-trat-tu-an-toan-xa-hoi-679658.aspx) |
| ASM — phần mềm thông báo lưu trú của Bộ Công an | [Cục Cảnh sát QLHC về TTXH](https://canhsatquanlyhanhchinh.gov.vn/gioi-thieu/day-manh-trien-khai-phan-mem-thong-bao-luu-tru-asm-gop-phan-chuyen-doi-so-trong-cong-tac-quan-ly-nganh-nghe-kinh-doanh-3719) |
| VNeID chia sẻ thông tin/QR CCCD | [Thị trường Tài chính Tiền tệ](https://thitruongtaichinhtiente.vn/huong-dan-su-dung-thong-tin-giay-to-tich-hop-tren-vneid-46544.html) |
| Luật BVDLCN 2025 (91/2025/QH15), hiệu lực 01/01/2026 | [Cổng TTĐT Chính phủ](https://chinhphu.vn/?pageid=27160&docid=214590&classid=1&typegroupid=3) |
| WebAuthn/passkey | [MDN](https://developer.mozilla.org/en-US/docs/Web/API/Web_Authentication_API), [W3C](https://www.w3.org/TR/webauthn-2/) |
| Chống ảnh giả khuôn mặt (PAD, ISO/IEC 30107-3) | [NCheck](https://www.ncheck.net/anti-spoofing-iso-30107-attendance/), [Innovatrics](https://www.innovatrics.com/news/innovatrics-face-liveness-renews-level-2-pad-certification-advances-toward-level-3/) |
| Thiết bị cổng (OSDP, Wiegand, relay) | [Hikvision MinMoe](https://www.hikvision.com/en/products/Access-Control-Products/terminals/face-recognition-terminal/), [ZKTeco](https://www.zkteco.com/en/BiometricControlPanel) |
| Máy quét vân tay + web (SDK/OSDP, WebAuthn) | [Biometric Scanner Integration](https://www.biometricscannerinfo.com/biometric-scanner-integration.php), [Stack Overflow Blog](https://stackoverflow.blog/2022/11/16/biometric-authentication-for-web-devs/) |
| QR xoay vòng chống chụp màn hình | [Invixium](https://www.invixium.com/qr-code-access-control-solutions/), [AccessFlow](https://www.accessflowglobal.com/post/why-accessflow-now-features-rotating-qr-codes-and-what-it-means-for-your-site) |

> Số liệu phạt và hạn thông báo có thể thay đổi theo văn bản sửa đổi; khi triển khai thật cần đối chiếu lại văn bản hiện hành.
