# Đề xuất hợp đồng storage và giới hạn tệp

## 1. Loại file được phép upload

| Loại nghiệp vụ | Định dạng cho phép |
|---|---|
| Ảnh phòng trọ | `.jpg`, `.jpeg`, `.png`, `.webp` |
| Hợp đồng thuê phòng | `.pdf` |
| Hóa đơn / biên lai | `.pdf`, `.jpg`, `.png` |
| Giấy tờ tùy thân khách thuê | `.pdf`, `.jpg`, `.png` |
| Tệp khác | **Không cho phép** |

**Đề xuất:** Chỉ chấp nhận `.pdf`, `.jpg`, `.jpeg`, `.png`, `.webp`. Từ chối `.exe`, `.zip`, `.docm` (có macro).

**Lý do:** Đơn giản, đáp ứng nhu cầu thực tế, dễ kiểm soát bảo mật.

---

## 2. Kích thước tối đa

| Loại file | Giới hạn đề xuất |
|---|---|
| Ảnh phòng | **5 MB** |
| Hợp đồng | **10 MB** |
| Hóa đơn / biên lai | **5 MB** |
| Giấy tờ | **10 MB** |

**Lý do:** Đủ cho ảnh chất lượng cao và PDF nhiều trang, đồng thời không lãng phí băng thông.

---

## 3. Metadata cần lưu

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `FileName` | string | Có | Tên file gốc |
| `StoredFileName` | string | Có | Tên file lưu trên server (uuid + extension) |
| `ContentType` | string | Có | MIME type (ví dụ: `image/jpeg`) |
| `FileSizeBytes` | long | Có | Kích thước tính bằng byte |
| `UploadedByUserId` | string | Có | ID người upload |
| `UploadedAt` | datetime | Có | Thời điểm upload |
| `RelatedEntityId` | string | Có | ID của phòng/hợp đồng/hóa đoin liên quan |
| `RelatedEntityType` | string | Có | `"Room"`, `"Contract"`, `"Invoice"`, `"Tenant"` |
| `Description` | string | Không | Ghi chú tùy chọn |
| `IsDeleted` | bool | Có | Xóa mềm |

**Đề xuất:** Bảng trên đủ dùng cho phiên bản đầu. Không cần thêm trường phức tạp (số lần tải, trạng thái duyệt) vì chưa có nhu cầu.

---

## 4. Kiểm quyền — Ai được làm gì

| Vai trò | Upload ảnh phòng | Upload hợp đồng | Upload giấy tờ | Tải file |
|---|---|---|---|---|
| Chủ trọ | Có | Có | Có | Có — tất cả file |
| Quản lý | Có | Có | Có | Có — tất cả file |
| Khách thuê | **Không** | **Không** | Có — chỉ của mình | Có — chỉ file liên quan đến mình |

**Đề xuất:**
- **Khách thuê chỉ upload giấy tờ** (CMND/CCCD/hộ khẩu) — vì quản trị viên cần xác minh danh tính.
- **Khách thuê không upload ảnh phòng, hợp đồng** — để tránh spam/nội dung sai.
- **Khách thuê tải được:** hợp đồng của mình, hóa đơn của mình, ảnh phòng của phòng mình đang thuê.
- **Khách thuê không tải được:** file của khách thuê khác, file không liên quan.

---

## 5. Lỗi trả về

| Tình huống | Mã lỗi | Thông báo hiển thị |
|---|---|---|
| File quá lớn | `FileSizeExceeded` | "File vượt quá dung lượng cho phép (tối đa X MB)" |
| Sai định dạng | `InvalidFileType` | "Chỉ chấp nhận file .pdf, .jpg, .png, .webp" |
| Mất kết nối | `UploadFailed` | "Không thể tải lên, vui lòng thử lại" |
| File không tìm thấy | `FileNotFound` | "File không tồn tại hoặc đã bị xóa" |
| Không đủ quyền | `Forbidden` | "Bạn không có quyền thực hiện thao tác này" |
| File bị nhiễm virus (nếu có scan) | `MaliciousFile` | "File không an toàn, vui lòng thử file khác" |

**Đề xuất:** 5 lỗi đầu là đủ cho phiên bản 1. Virus scan cần tool bổ sung — để sau nếu nhóm muốn.

---

## 6. Lưu trữ

**Đề xuất:** Lưu trên server local trong thư mục `NhaTro/Web/Uploads/` hoặc `wwwroot/uploads/`. Tổ chức theo năm/tháng để dễ quản lý:

```text
Uploads/
├── 2026/
│   ├── 10/
│   │   ├── a1b2c3d4.jpg
│   │   ├── e5f6g7h8.pdf
│   │   └── ...
│   └── 11/
│       └── ...
```

**Không dùng cloud storage (Azure Blob/S3)** ở phiên bản đầu vì thêm chi phí và độ phức tạp. Chuyển sang cloud khi cần mở rộng.

---

## 7. DTO mẫu

> **Vị trí code:** `NhaTro/Web/Features/Storage/DTOs/` (theo đúng cấu trúc feature-oriented)

| File | Vai trò |
|---|---|
| [`UploadFileRequest.cs`](../../NhaTro/Web/Features/Storage/DTOs/UploadFileRequest.cs) | Dữ liệu gửi lên khi upload |
| [`FileMetadataDto.cs`](../../NhaTro/Web/Features/Storage/DTOs/FileMetadataDto.cs) | Metadata file trả về |
| [`FileDownloadResponse.cs`](../../NhaTro/Web/Features/Storage/DTOs/FileDownloadResponse.cs) | Kết quả tải file (stream + metadata) |
| [`UploadResult.cs`](../../NhaTro/Web/Features/Storage/DTOs/UploadResult.cs) | Kết quả upload (thành công/thất bại) |

**Namespace:** `Web.Features.Storage.DTOs`

---

## 8. Ví dụ JSON — Thành công / Thất bại

### 8.1 Upload thành công — `POST /api/files`

**Request:**
```json
{
  "file": "<binary>",
  "relatedEntityId": "room-001",
  "relatedEntityType": "Room",
  "description": "Ảnh phòng 101"
}
```

**Response 200:**
```json
{
  "succeeded": true,
  "file": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "fileName": "phong-101.jpg",
    "storedFileName": "a1b2c3d4-e5f6-7890-abcd-ef1234567890.jpg",
    "contentType": "image/jpeg",
    "fileSizeBytes": 2457600,
    "uploadedByUserId": "user-001",
    "uploadedAt": "2026-10-14T10:30:00+07:00",
    "relatedEntityId": "room-001",
    "relatedEntityType": "Room",
    "description": "Ảnh phòng 101",
    "isDeleted": false
  },
  "errorCode": null,
  "errorMessage": null
}
```

### 8.2 Upload thất bại — File quá lớn

**Response 400:**
```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "FileSizeExceeded",
  "errorMessage": "File vượt quá dung lượng cho phép (tối đa 5 MB)"
}
```

### 8.3 Upload thất bại — Sai định dạng

**Response 400:**
```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "InvalidFileType",
  "errorMessage": "Chỉ chấp nhận file .pdf, .jpg, .png, .webp"
}
```

### 8.4 Tải file thành công — `GET /api/files/{id}`

**Response 200:**
- Headers: `Content-Type: image/jpeg`, `Content-Disposition: attachment; filename="phong-101.jpg"`
- Body: binary stream

### 8.5 Tải file thất bại — Không tìm thấy

**Response 404:**
```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "FileNotFound",
  "errorMessage": "File không tồn tại hoặc đã bị xóa"
}
```

### 8.6 Tải file thất bại — Không đủ quyền

**Response 403:**
```json
{
  "succeeded": false,
  "file": null,
  "errorCode": "Forbidden",
  "errorMessage": "Bạn không có quyền thực hiện thao tác này"
}
```

---

## 9. Theo dõi việc nhóm chốt

| Mục | Trạng thái | Ngày chốt | Người chốt |
|---|---|---|---|
| Loại file | Chờ chốt | — | — |
| Dung lượng tối đa | Chờ chốt | — | — |
| Metadata | Chờ chốt | — | — |
| Kiểm quyền | Chờ chốt | — | — |
| Lỗi trả về | Chờ chốt | — | — |
| Lưu trữ | Chờ chốt | — | — |
| DTO mẫu | Chờ chốt | — | — |

**Ghi chú:** Sau khi nhóm chốt xong, cập nhật bảng này với ngày và người chốt. File này sẽ trở thành hợp đồng chính thức cho VHT-13 (Tạo storage backend dùng chung).

---

## Tóm tắt đề xuất

| Mục | Đề xuất |
|---|---|
| Định dạng | `.pdf`, `.jpg`, `.jpeg`, `.png`, `.webp` |
| Dung lượng tối đa | 5 MB (ảnh, hóa đơn), 10 MB (hợp đồng, giấy tờ) |
| Metadata | 10 trường (tên, kích thước, người upload, ngày, liên quan đến đối tượng nào) |
| Quyền upload | Chủ trọ + Quản lý: tất cả; Khách thuê: chỉ giấy tờ |
| Quyền tải | Chủ trọ + Quản lý: tất cả; Khách thuê: chỉ file của mình |
| Lưu trữ | Server local, thư mục theo năm/tháng |
| Lỗi | 5 loại chính (quá lớn, sai định dạng, mất kết nối, không tìm thấy, không đủ quyền) |
| DTO | `UploadFileRequest`, `FileMetadataDto`, `FileDownloadResponse`, `UploadResult` |
| Vị trí code | `Infrastructure/Storage/` (dùng chung) |

