# Đề xuất hợp đồng storage và giới hạn tệp

> **Trạng thái:** Bản đề xuất, **chưa có giá trị nào được nhóm xác nhận**. Mọi con số, định dạng và quyền dưới đây là *đề xuất* để nhóm chốt ở mục 10. Không đưa vào code cho đến khi mục tương ứng ở mục 10 chuyển sang "Đã chốt".
>
> **Phối hợp:** Tính (kiểm quyền / cấu hình giới hạn), Phúc/Phú (nhu cầu tệp, dữ liệu gửi lên).
> **Bàn giao cho:** VHT-13 (Tạo storage backend dùng chung).
> **Nguồn đối chiếu:** `docs/task/cong_viec_theo_tuan.md` (VHT-33/39/60 ảnh sự cố; VHT-54/55 minh chứng thanh toán), `docs/architecture/cau_truc_thu_muc.md` dòng 238, `docs/task/thao_tac_va_quyen.xlsx`, `docs/design/phong_cach_giao_dien.md` §9 (Admin không có nghiệp vụ).

---

## 0. Phạm vi và vị trí code

**Phạm vi:** hợp đồng storage này bao **toàn bộ tệp riêng tư** của hệ thống, gồm 3 nhóm theo `cau_truc_thu_muc.md` dòng 238 ("Giấy tờ / Minh chứng / Ảnh sự cố nằm ngoài wwwroot"), cộng thêm ảnh phòng, hợp đồng và hóa đơn:

| Nhóm | `FileCategory` | Luồng nghiệp vụ |
|---|---|---|
| Giấy tờ | `IdentityDocument` | Khách thuê |
| Minh chứng | `PaymentProof` | P6 – Thu tiền (VHT-54, VHT-55) |
| Ảnh sự cố | `IncidentPhoto` | P7 – Sự cố (VHT-33, VHT-39, VHT-60) |
| Ảnh phòng | `RoomPhoto` | Quản lý phòng |
| Hợp đồng | `Contract` | Hợp đồng thuê |
| Hóa đơn | `Invoice` | Hóa đơn do chủ trọ/quản lý phát hành |

**Vị trí code:**

| Thành phần | Vị trí |
|---|---|
| DTO | `NhaTro/Web/Features/Storage/DTOs/` (namespace `Web.Features.Storage.DTOs`) |
| Service / Controller storage | `NhaTro/Web/Features/Storage/` |
| Thư mục lưu file thật | Đường dẫn cấu hình được, **nằm ngoài `wwwroot`** (mục 6) |

**Nguồn chuẩn:** tài liệu này là hợp đồng; code phải khớp với mục 7 (xem bảng đối chiếu ở mục 7.5).

---

## 1. Loại file được phép upload

Mỗi file phải thuộc một **loại tệp (`FileCategory`)** và gắn với một **đối tượng liên quan (`RelatedEntityType`)**. Cặp này được kiểm tra chặt (không cho trộn).

| `FileCategory` | Nghiệp vụ | Định dạng cho phép | `RelatedEntityType` hợp lệ |
|---|---|---|---|
| `RoomPhoto` | Ảnh phòng trọ | `.jpg`, `.jpeg`, `.png`, `.webp` | `Room` |
| `Contract` | Hợp đồng thuê phòng | `.pdf` | `Contract` |
| `Invoice` | Hóa đơn do chủ trọ/quản lý phát hành | `.pdf`, `.jpg`, `.jpeg`, `.png` | `Invoice` |
| `IdentityDocument` | Giấy tờ tùy thân khách thuê | `.pdf`, `.jpg`, `.jpeg`, `.png` | `Tenant` |
| `IncidentPhoto` | Ảnh sự cố (P7) | `.jpg`, `.jpeg`, `.png`, `.webp` | `MaintenanceIncident` |
| `PaymentProof` | Minh chứng thanh toán do khách gửi (P6) | `.pdf`, `.jpg`, `.jpeg`, `.png` | `Payment` |
| Loại khác | — | **Không cho phép** | — |

> **Tên entity cần đối chiếu schema:** `MaintenanceIncident` và `Payment` là tên đề xuất. Nếu schema P6 gắn minh chứng vào yêu cầu thanh toán thì đổi thành `PaymentRequest`. Phúc/Phú/Tính xác nhận theo bảng thật.

**Quy ước kiểu dữ liệu:** `FileCategory` và `RelatedEntityType` là **enum** (serialize thành chuỗi, ví dụ `"IncidentPhoto"`), không nhận chuỗi tự do. Giá trị sai hoặc cặp không khớp bảng trên trả `InvalidRequest`.

**Đề xuất kiểm tra:**
- Kiểm tra theo **từng loại** ở bảng trên (`.webp` chỉ dành cho ảnh phòng và ảnh sự cố).
- Kiểm tra cả **đuôi file** và **nội dung thật** (magic bytes). Không tin `Content-Type` do client gửi.
- Từ chối: `.exe`, `.zip`, `.docm` (có macro), `.svg`, `.html` và mọi đuôi không có trong bảng.
- Từ chối file rỗng (0 byte).

**Lý do:** Đơn giản, đáp ứng nhu cầu thực tế, dễ kiểm soát bảo mật.

---

## 2. Kích thước tối đa

| `FileCategory` | Giới hạn đề xuất |
|---|---|
| `RoomPhoto` | **5 MB** |
| `Contract` | **10 MB** |
| `Invoice` | **5 MB** |
| `IdentityDocument` | **10 MB** |
| `IncidentPhoto` | **5 MB** |
| `PaymentProof` | **5 MB** |

**Số file tối đa cho mỗi đối tượng liên quan:** *Cần hỏi Phúc/Phú* (ví dụ: ảnh phòng/phòng, ảnh/sự cố, minh chứng/lần thanh toán, giấy tờ/khách). Chưa đặt số trong bản này.

**Cấu hình giới hạn (đề xuất cho Tính xác nhận):** đặt trong `appsettings.json`, không hard-code:

```json
{
  "Storage": {
    "RootPath": "<đường dẫn ngoài wwwroot, nhóm chốt>",
    "Limits": {
      "RoomPhotoMaxBytes": 5242880,
      "ContractMaxBytes": 10485760,
      "InvoiceMaxBytes": 5242880,
      "IdentityDocumentMaxBytes": 10485760,
      "IncidentPhotoMaxBytes": 5242880,
      "PaymentProofMaxBytes": 5242880
    },
    "MaxFilesPerEntity": {
      "RoomPhoto": null,
      "Contract": null,
      "Invoice": null,
      "IdentityDocument": null,
      "IncidentPhoto": null,
      "PaymentProof": null
    }
  }
}
```

> Các giá trị `null` là chỗ trống chờ nhóm chốt.

**Giới hạn tầng server và lỗi 413:** ngoài kiểm tra trong service, cần đặt giới hạn request ở tầng server (`MaxRequestBodySize` của Kestrel, `FormOptions.MultipartBodyLengthLimit`) **lớn hơn mức tối đa cao nhất một chút** (10 MB + overhead multipart). Khi vượt giới hạn này, request bị chặn **trước khi vào service**, nên phải map về cùng một lỗi:

- `FileSizeExceeded` luôn trả **HTTP 413** (cả khi service phát hiện lẫn khi server chặn), cùng cấu trúc JSON ở mục 8.2.
- Bắt các trường hợp server chặn bằng một exception-handling middleware: `BadHttpRequestException` có `StatusCode = 413` (Kestrel) và `InvalidDataException` do `MultipartBodyLengthLimit` (đọc form), rồi trả `FileSizeExceeded`.
- Nếu chạy sau IIS: `maxAllowedContentLength` (web.config) chặn bằng lỗi 404.13 **trước khi vào ứng dụng**, không map được. Đặt giá trị này ≥ giới hạn Kestrel để Kestrel/service là nơi chặn.
- Phần thông báo "tối đa {X} MB" lấy theo loại tệp; nếu server chặn khi chưa đọc được `fileCategory` thì dùng giới hạn lớn nhất hoặc thông báo chung.

**Lý do:** Đủ cho ảnh chất lượng cao và PDF nhiều trang, đồng thời không lãng phí băng thông.

---

## 3. Metadata cần lưu

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `Id` | Guid | Có | Khóa chính file, dùng trong `/api/files/{id}` |
| `FileName` | string | Có | Tên file gốc (đã làm sạch ký tự đường dẫn) |
| `StoredFileName` | string | Có | Tên lưu trên server (uuid + extension). **Chỉ dùng nội bộ** |
| `ContentType` | string | Có | MIME type do server xác định (ví dụ: `image/jpeg`) |
| `FileSizeBytes` | long | Có | Kích thước tính bằng byte |
| `FileCategory` | enum | Có | 6 giá trị ở mục 1 |
| `UploadedByUserId` | string | Có | ID người upload |
| `UploadedAt` | datetime | Có | Thời điểm upload |
| `RelatedEntityId` | string | Có | ID đối tượng liên quan. **Kiểu phải khớp PK thật** của Room/Contract/Invoice/Tenant/MaintenanceIncident/Payment (Tính xác nhận) |
| `RelatedEntityType` | enum | Có | `Room`, `Contract`, `Invoice`, `Tenant`, `MaintenanceIncident`, `Payment` |
| `Description` | string | Không | Ghi chú tùy chọn |
| `IsDeleted` | bool | Có | Xóa mềm |

**Đề xuất:** 12 trường là đủ cho phiên bản đầu. Không thêm trường phức tạp (số lần tải, trạng thái duyệt) vì chưa có nhu cầu. Trạng thái xác nhận minh chứng thanh toán nằm ở bảng `Payment`, không nằm trong metadata file.

**Cần hỏi nhóm:** giấy tờ cá nhân (CCCD/hộ khẩu) lưu bao lâu; có xóa khi khách trả phòng không; có cần `DeletedAt` / `DeletedByUserId` không.

---

## 4. Kiểm quyền — Ai được làm gì

> **Tính cần đối chiếu bảng này với `docs/task/thao_tac_va_quyen.xlsx`** trước khi chốt. Chỗ ghi *"Cần chốt"* là câu hỏi mở. "Khách thuê" luôn bị giới hạn trong phạm vi **phòng / hợp đồng của mình**. **Admin không có quyền nghiệp vụ** nên bị chặn toàn bộ tệp (xem ghi chú cuối mục 4).

### 4.1 Upload

| Vai trò | `RoomPhoto` | `Contract` | `Invoice` | `IdentityDocument` | `IncidentPhoto` | `PaymentProof` |
|---|---|---|---|---|---|---|
| Chủ trọ | Có | Có | Có | Có | Có | Cần chốt (nhập hộ khách?) |
| Quản lý | Có | Có | Có | Có | Có | Cần chốt (nhập hộ khách?) |
| Khách thuê | **Không** | **Không** | **Không** | Có — chỉ của mình | Có — chỉ sự cố thuộc phòng mình | Có — chỉ khoản thanh toán của mình |
| Admin | **Không** | **Không** | **Không** | **Không** | **Không** | **Không** |

`Invoice` là hóa đơn do chủ trọ/quản lý phát hành nên khách không upload. Việc khách gửi chứng từ thanh toán đi qua `PaymentProof`, nên không còn mâu thuẫn giữa quyền upload hóa đơn và nhu cầu P6.

### 4.2 Tải file

| Vai trò | Phạm vi được tải |
|---|---|
| Chủ trọ | Tất cả file |
| Quản lý | Tất cả file *(cần chốt: toàn bộ nhà trọ hay chỉ nhà được phân công)* |
| Khách thuê | Hợp đồng, hóa đơn, minh chứng thanh toán, giấy tờ của mình; ảnh phòng của phòng mình đang thuê; ảnh sự cố của phòng mình |
| Admin | **Không** — chỉ quản trị tài khoản/vai trò/nhật ký (VHT-104), không truy cập tệp nghiệp vụ |

Khách thuê **không** tải được file của khách thuê khác hoặc file không liên quan.

### 4.3 Xóa / thay thế

| Vai trò | Xóa (xóa mềm) | Ghi chú |
|---|---|---|
| Chủ trọ | Có — tất cả file | |
| Quản lý | Có | Cần chốt phạm vi như mục 4.2 |
| Khách thuê | Cần chốt | Đề xuất: chỉ xóa file **do mình upload** (`IdentityDocument`, `IncidentPhoto`, `PaymentProof`) và chỉ khi nghiệp vụ liên quan chưa xử lý/xác nhận. Điều kiện "chưa xử lý" cần Phúc/Phú xác nhận theo trạng thái P6/P7 |
| Admin | **Không** | Không có nghiệp vụ |

> **Admin luôn bị chặn:** Admin không truy cập bất kỳ API `/api/files` nào (kể cả file do chính mình tạo), do policy Admin chỉ quản trị tài khoản/vai trò/nhật ký, **không có quyền nghiệp vụ** — xem `docs/design/phong_cach_giao_dien.md` §9 và checklist nghiệm thu ("Admin bị chặn mọi thao tác nghiệp vụ"). Các API trả `401/403` như mọi thao tác nghiệp vụ bị chặn; cần **ghi tường minh** để lúc viết policy/viết test (VHT-101) không vô tình cấp quyền mặc định cho Admin.

**API xóa và luồng thay thế:** xem mục 8.8 và 8.9.

**Lý do:**
- Khách thuê chỉ upload giấy tờ, ảnh sự cố và minh chứng của chính mình; không upload ảnh phòng/hợp đồng/hóa đơn để tránh spam và nội dung sai.
- Ảnh sự cố và minh chứng là dữ liệu riêng tư, chỉ truy cập qua API có kiểm quyền (mục 6).

---

## 5. Lỗi trả về

| Tình huống | HTTP | Mã lỗi | Thông báo hiển thị | Phiên bản |
|---|---|---|---|---|
| File quá lớn (service hoặc server chặn) | **413** | `FileSizeExceeded` | "File vượt quá dung lượng cho phép (tối đa {X} MB)" | v1 |
| Sai định dạng | 400 | `InvalidFileType` | "Loại này chỉ chấp nhận file {danh sách đuôi theo loại}" | v1 |
| Thiếu trường / enum sai / cặp loại–đối tượng không khớp | 400 | `InvalidRequest` | "Dữ liệu gửi lên không hợp lệ" | v1 |
| Mất kết nối / lỗi lưu | 500 | `UploadFailed` | "Không thể tải lên, vui lòng thử lại" | v1 |
| File không tìm thấy | 404 | `FileNotFound` | "File không tồn tại hoặc đã bị xóa" | v1 |
| Không đủ quyền | 403 | `Forbidden` | "Bạn không có quyền thực hiện thao tác này" | v1 |
| Chưa đăng nhập | 401 | `Unauthorized` | "Vui lòng đăng nhập để tiếp tục" | v1 |
| File rỗng | 400 | `EmptyFile` | "File không có nội dung" | v1 |
| Đối tượng liên quan không tồn tại | 400 | `RelatedEntityNotFound` | "Không tìm thấy đối tượng liên quan" | v1 |
| Vượt số file tối đa | 400 | `FileCountExceeded` | "Đã đạt số file tối đa cho mục này" | Chờ nhóm chốt số file |
| File bị nhiễm virus (nếu có scan) | 400 | `MaliciousFile` | "File không an toàn, vui lòng thử file khác" | Để sau |

**Đề xuất:** 9 lỗi đánh dấu v1 là đủ cho phiên bản 1. Virus scan cần tool bổ sung, để sau nếu nhóm muốn. Thông báo `InvalidFileType` tạo theo loại (ví dụ ảnh sự cố: `.jpg, .jpeg, .png, .webp`; minh chứng thanh toán: `.pdf, .jpg, .jpeg, .png`). Với tải/xóa file của người khác nên trả `FileNotFound` thay vì `Forbidden` nếu nhóm muốn tránh lộ việc file có tồn tại (cần chốt).

---

## 6. Lưu trữ

**Đề xuất:** Lưu trên server local, trong thư mục **nằm ngoài `wwwroot`** (đường dẫn lấy từ `Storage:RootPath`). Tổ chức theo năm/tháng:

```text
<Storage:RootPath>/
├── 2026/
│   ├── 10/
│   │   ├── a1b2c3d4-....jpg
│   │   ├── e5f6g7h8-....pdf
│   │   └── ...
│   └── 11/
│       └── ...
```

**Quy tắc:**
- **Không** dùng `wwwroot/uploads/`: file trong `wwwroot` được phục vụ công khai qua static files, bỏ qua kiểm quyền. Điều này đặc biệt quan trọng với giấy tờ, minh chứng thanh toán và ảnh sự cố (ảnh "private").
- File chỉ được tải qua `GET /api/files/{id}` sau khi kiểm quyền. Nơi nào cần hiển thị ảnh (ghép ảnh private ở P7) cũng gọi qua API này, không dùng đường dẫn tĩnh.
- Tên lưu là uuid + extension, không dùng tên gốc để tránh trùng tên và path traversal.
- Không trả `StoredFileName` hay đường dẫn nội bộ cho client.

**Không dùng cloud storage (Azure Blob/S3)** ở phiên bản đầu vì thêm chi phí và độ phức tạp. Chuyển sang cloud khi cần mở rộng.

---

## 7. DTO mẫu

> **Vị trí code:** `NhaTro/Web/Features/Storage/DTOs/` — namespace `Web.Features.Storage.DTOs`

### 7.1 `UploadFileRequest` — dữ liệu gửi lên (Phúc/Phú cần gửi)

Gửi bằng **`multipart/form-data`** (không phải JSON).

| Trường form | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `file` | `IFormFile` | Có | Nội dung file |
| `fileCategory` | enum `FileCategory` | Có | `RoomPhoto` / `Contract` / `Invoice` / `IdentityDocument` / `IncidentPhoto` / `PaymentProof` |
| `relatedEntityId` | string | Có | ID đối tượng liên quan |
| `relatedEntityType` | enum `RelatedEntityType` | Có | `Room` / `Contract` / `Invoice` / `Tenant` / `MaintenanceIncident` / `Payment` |
| `description` | string | Không | Ghi chú |

`UploadedByUserId` **không** gửi từ client; server lấy từ phiên đăng nhập.

### 7.2 `FileMetadataDto` — metadata trả về client

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Guid | |
| `FileName` | string | Tên gốc |
| `ContentType` | string | |
| `FileSizeBytes` | long | |
| `FileCategory` | enum | |
| `UploadedByUserId` | string | Cần chốt có trả về client hay không |
| `UploadedAt` | DateTimeOffset | |
| `RelatedEntityId` | string | |
| `RelatedEntityType` | enum | |
| `Description` | string? | |

**Không** có `StoredFileName` và `IsDeleted` (file đã xóa không được liệt kê; đường dẫn nội bộ không lộ ra ngoài). Nếu tầng service/repository cần các trường này thì dùng model/entity nội bộ riêng, không dùng chung `FileMetadataDto`.

### 7.3 `UploadResult` — kết quả upload và lỗi dùng chung

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Succeeded` | bool | |
| `File` | `FileMetadataDto?` | `null` khi thất bại |
| `ErrorCode` | string? | Mã ở mục 5, `null` khi thành công |
| `ErrorMessage` | string? | `null` khi thành công |

### 7.4 `FileDownloadResponse` — kết quả tải file (nội bộ service → controller)

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `FileStream` | `Stream` | Luồng nội dung file |
| `ContentType` | string | |
| `FileName` | string | Tên gốc dùng cho `Content-Disposition` |
| `FileSizeBytes` | long | Kích thước file, để đặt `Content-Length` |

Tên `FileStream` giữ theo code hiện có (DTO này chỉ dùng nội bộ nên không cần đổi).

### 7.5 Đối chiếu code hiện tại (4 file đã có trong `Features/Storage/DTOs/`)

Code đã được cập nhật cho khớp mục 7 (2026-10-14). Các giá trị enum vẫn là **đề xuất**, chờ nhóm chốt ở mục 10. Bảng đối chiếu:

| DTO | Việc cần sửa ở code |
|---|---|
| `UploadFileRequest` | Thêm `FileCategory` (enum); đổi `RelatedEntityType` sang enum `RelatedEntityType` |
| `FileMetadataDto` | **Bỏ** `StoredFileName` và `IsDeleted` khỏi DTO trả client; **thêm** `FileCategory`; đổi `RelatedEntityType` sang enum. Nếu cần `StoredFileName`/`IsDeleted` thì chuyển sang entity/model nội bộ |
| `FileDownloadResponse` | Giữ tên `FileStream` (doc đã theo code) |
| `UploadResult` | Khớp mục 7.3; kiểm tra `ErrorCode` dùng đúng các mã ở mục 5 |
| Mới | Tạo enum `FileCategory` (6 giá trị) và `RelatedEntityType` (6 giá trị), cấu hình serialize chuỗi (`JsonStringEnumConverter`) |

---

## 8. API và ví dụ — Thành công / Thất bại

| Thao tác | Endpoint | Thành công |
|---|---|---|
| Upload | `POST /api/files` | 200 + `UploadResult` |
| Tải file | `GET /api/files/{id}` | 200 + binary stream |
| Liệt kê theo đối tượng | `GET /api/files?relatedEntityType=&relatedEntityId=` | 200 + danh sách `FileMetadataDto` |
| Xóa (xóa mềm) | `DELETE /api/files/{id}` | 204 No Content |

Endpoint liệt kê là đề xuất để màn hình P6/P7 lấy danh sách ảnh/minh chứng của một sự cố hoặc khoản thanh toán; kết quả đã lọc theo quyền ở mục 4.2. Mọi endpoint đều **chặn Admin** (mục 4).

### 8.1 Upload thành công — `POST /api/files`

**Request** (`multipart/form-data`), ví dụ ảnh sự cố:

```text
file              = <binary vong-nuoc-ro.jpg>
fileCategory      = IncidentPhoto
relatedEntityId   = incident-007
relatedEntityType = MaintenanceIncident
description       = Vòi nước phòng 101 bị rò
```

**Response 200:**

```json
{
  "succeeded": true,
  "file": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "fileName": "vong-nuoc-ro.jpg",
    "contentType": "image/jpeg",
    "fileSizeBytes": 2457600,
    "fileCategory": "IncidentPhoto",
    "uploadedByUserId": "user-001",
    "uploadedAt": "2026-10-14T10:30:00+07:00",
    "relatedEntityId": "incident-007",
    "relatedEntityType": "MaintenanceIncident",
    "description": "Vòi nước phòng 101 bị rò"
  },
  "errorCode": null,
  "errorMessage": null
}
```

### 8.2 Upload thất bại — File quá lớn

**Response 413:**

```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "FileSizeExceeded",
  "errorMessage": "File vượt quá dung lượng cho phép (tối đa 5 MB)"
}
```

### 8.3 Upload thất bại — Sai định dạng

**Response 400** (ví dụ gửi `.webp` cho `PaymentProof`):

```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "InvalidFileType",
  "errorMessage": "Minh chứng thanh toán chỉ chấp nhận file .pdf, .jpg, .jpeg, .png"
}
```

### 8.4 Upload thất bại — Không đủ quyền

**Response 403:**

```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "Forbidden",
  "errorMessage": "Bạn không có quyền thực hiện thao tác này"
}
```

### 8.5 Tải file thành công — `GET /api/files/{id}`

**Response 200:**
- Headers: `Content-Type: image/jpeg`, `Content-Disposition: attachment; filename="vong-nuoc-ro.jpg"`
- Body: binary stream

*(Cần hỏi Phúc/Phú: ảnh sự cố có cần hiển thị inline để ghép vào màn hình không; nếu có thì thêm tham số `?inline=true` trả `Content-Disposition: inline`.)*

### 8.6 Tải file thất bại — Không tìm thấy

**Response 404:**

```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "FileNotFound",
  "errorMessage": "File không tồn tại hoặc đã bị xóa"
}
```

### 8.7 Tải file thất bại — Không đủ quyền

**Response 403:**

```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "Forbidden",
  "errorMessage": "Bạn không có quyền thực hiện thao tác này"
}
```

### 8.8 Xóa file — `DELETE /api/files/{id}`

- Xóa mềm: đặt `IsDeleted = true`, giữ nguyên file vật lý (xóa vật lý theo chính sách lưu trữ ở mục 9, câu hỏi 8).
- Kiểm quyền theo mục 4.3.
- **Thành công:** `204 No Content`, không có body.
- **Thất bại:** cùng cấu trúc lỗi như 8.6 / 8.7, thêm `401 Unauthorized`.
- Xóa file đã xóa trước đó: `404 FileNotFound`.

### 8.9 Luồng thay thế file

Không có API sửa nội dung file. Thay thế gồm 2 bước từ phía client:

1. `POST /api/files` với file mới (cùng `relatedEntityId`, `relatedEntityType`, `fileCategory`). Nếu đối tượng có giới hạn số file thì xử lý theo `FileCountExceeded`.
2. Khi bước 1 thành công, `DELETE /api/files/{id}` file cũ.

Làm theo thứ tự này để lỗi upload không làm mất file cũ. Nếu nhóm muốn thay thế nguyên tử (một lệnh), thêm `PUT /api/files/{id}` ở phiên bản sau.

---

## Tóm tắt đề xuất

| Mục | Đề xuất |
|---|---|
| Phạm vi | Giấy tờ, minh chứng thanh toán, ảnh sự cố, ảnh phòng, hợp đồng, hóa đơn — tất cả nằm ngoài `wwwroot` |
| `FileCategory` | `RoomPhoto`, `Contract`, `Invoice`, `IdentityDocument`, `IncidentPhoto`, `PaymentProof` (enum) |
| `RelatedEntityType` | `Room`, `Contract`, `Invoice`, `Tenant`, `MaintenanceIncident`, `Payment` (enum; tên chờ đối chiếu schema) |
| Định dạng | Ảnh phòng và ảnh sự cố `.jpg/.jpeg/.png/.webp`; hợp đồng `.pdf`; hóa đơn, giấy tờ, minh chứng `.pdf/.jpg/.jpeg/.png` |
| Dung lượng tối đa | 5 MB (ảnh phòng, hóa đơn, ảnh sự cố, minh chứng), 10 MB (hợp đồng, giấy tờ) |
| Metadata | 12 trường (có `Id` và `FileCategory`) |
| Quyền upload | Chủ trọ + Quản lý: tất cả; Khách thuê: giấy tờ, ảnh sự cố, minh chứng thanh toán của mình; Admin: không |
| Quyền tải | Chủ trọ + Quản lý: tất cả; Khách thuê: chỉ file liên quan đến mình; Admin: không |
| Quyền xóa | Chủ trọ + Quản lý; Khách thuê: cần chốt; Admin: không |
| API | `POST /api/files`, `GET /api/files/{id}`, `GET /api/files?...`, `DELETE /api/files/{id}` |
| Lưu trữ | Server local, ngoài `wwwroot`, thư mục theo năm/tháng, tải qua API có kiểm quyền |
| Lỗi | 9 lỗi v1; `FileSizeExceeded` luôn 413 (cả khi server chặn); `FileCountExceeded` chờ chốt số file; `MaliciousFile` để sau |
| Request upload | `multipart/form-data` |
| DTO | `UploadFileRequest`, `FileMetadataDto`, `FileDownloadResponse` (`FileStream`), `UploadResult`; code cần sửa theo mục 7.5 |
| Vị trí code | `NhaTro/Web/Features/Storage/` |
