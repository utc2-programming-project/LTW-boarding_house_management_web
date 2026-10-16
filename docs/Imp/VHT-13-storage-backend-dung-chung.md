# VHT-13 — Tạo storage backend dùng chung

> **Nhánh:** `VHT-13-tao-storage-backend-dung-chung` (từ `dev`).
> **Đầu vào:** VHT-12 (`docs/task/de_xuat_hop_dong_storage.md`) — hợp đồng DTO/enum/giới hạn/quyền.
> **Phạm vi đợt:** 1C. Không làm nghiệp vụ P4/P6/P7 (chỉ cung cấp nền storage để VHT-26/33/54 dùng).

## 1. Đã làm

| Lớp | File | Việc |
|---|---|---|
| Cấu hình | `Web/Configuration/Options/StorageOptions.cs` | Bind section `Storage`; `RootPath` + `Limits` + `MaxFilesPerEntity`; mở rộng biến môi trường/đường dẫn tương đối |
| Cấu hình | `Web/Configuration/Validation/StorageOptionsValidator.cs` | Kiểm ngay khi khởi động: thiếu `RootPath`, `RootPath` trong `wwwroot`, giới hạn ≤ 0 |
| Rules | `Web/Features/Storage/Rules/FileTypeRules.cs` | Đuôi theo loại, cặp loại tệp–đối tượng, MIME, giới hạn mặc định, tên hiển thị |
| Rules | `Web/Features/Storage/Rules/FileContentSniffer.cs` | Magic bytes JPEG/PNG/WebP/PDF; đuôi phải khớp nội dung thật |
| Rules | `Web/Features/Storage/Rules/StoragePermissionRules.cs` | Ma trận quyền upload/tải/xóa; **Admin luôn bị chặn** |
| Rules | `Web/Features/Storage/Rules/StorageLimits.cs` | Cấu hình thắng, thiếu thì lấy mặc định |
| I/O | `Web/Infrastructure/Storage/LocalFileStorage.cs` | Ghi/đọc/xóa tệp ngoài `wwwroot`, tổ chức năm/tháng giờ VN, chặn path traversal |
| I/O | `Web/Infrastructure/Storage/StoredFileNaming.cs` | Tên uuid + đuôi do server sinh; đọc stream có giới hạn (`LimitedStreamReader`) |
| Service | `Web/Features/Storage/Services/FileService.cs` | Upload/tải/liệt kê/xóa mềm + kiểm quyền và phạm vi |
| Endpoint | `Web/Endpoints/Files/FileEndpoints.cs` | `POST /api/files`, `GET /api/files/{id}`, `GET /api/files?...`, `DELETE /api/files/{id}` |
| Lỗi | `Web/Infrastructure/ErrorHandling/FileUploadExceptionMiddleware.cs` | Kestrel/multipart chặn → cùng lỗi JSON `FileSizeExceeded` 413 |
| Dữ liệu | `Web/Features/Storage/Entities/StoredFile.cs`, `Web/Data/Configurations/Storage/StoredFileConfiguration.cs`, migration `AddStorageStoredFiles` | 13 trường + `RelativePath` (nội bộ), xóa mềm, index theo đối tượng và người upload |
| Test | `NhaTro.UnitTests/Features/Storage/*`, `NhaTro.UnitTests/TestSupport/FakeWebHostEnvironment.cs` | Loại/duôi, nội dung thật, quyền (Admin/khách/chủ trọ), giới hạn dung lượng, đường dẫn, naming, cấu hình |

## 2. Quyết định đã chốt trong code

- Tệp **không** nằm trong `wwwroot`; chỉ tải qua `GET /api/files/{id}` sau khi kiểm quyền. Validator chặn cấu hình sai ngay khi khởi động.
- Tên lưu = `uuid` + đuôi; đường dẫn `yyyy/MM` theo giờ Việt Nam; tên gốc chỉ để hiển thị `Content-Disposition`.
- Kiểm **cả đuôi và nội dung thật**; không tin `Content-Type`/tên client. WebP chỉ cho ảnh phòng/ảnh sự cố.
- Giới hạn đọc ngay trên stream (`LimitedStreamReader`); tệp sai loại/quá giới hạn **không** được ghi đĩa. Metadata lưu lỗi thì xóa tệp vừa ghi.
- `StoredFileName`/`RelativePath`/`IsDeleted` không rời khỏi server (không có trong `FileMetadataDto`).
- Admin bị chặn hai lớp: policy endpoint `Business` và `BusinessGuard` trong service.
- Endpoint `/api` trả **401/403** thay vì chuyển hướng trang đăng nhập.
- Xóa là xóa mềm (`IsDeleted = true`), giữ tệp vật lý.

## 3. Còn để ngỏ (cần nhóm chốt — không tự quyết)

1. ChuTro/QuanLy có được upload `PaymentProof` không (VHT-12 mục 4.1 ghi "Cần chốt"). Hiện tạm **cho phép**.
2. Trả `403` hay `404` khi tải/xóa tệp của người khác (VHT-12 mục 5). Hiện trả `403`.
3. Số tệp tối đa mỗi đối tượng (`Storage:MaxFilesPerEntity` đang `null` = không giới hạn).
4. Khách thuê xóa tệp của mình: điều kiện "nghiệp vụ chưa xử lý" chờ trạng thái P6/P7.
5. Phạm vi QuanLy theo tòa nhà (chờ VHT-24) — hiện theo VHT-12 "tất cả file".
6. Thời hạn lưu/xóa giấy tờ cá nhân; có thêm `DeletedAt`/`DeletedByUserId` không.

## 4. Phần chờ nghiệp vụ P4/P6/P7

- `RelatedEntityType` `Contract`, `Invoice`, `Payment`, `MaintenanceIncident` chưa có bảng nên **chưa kiểm được** tồn tại đối tượng liên quan; `Room`/`Tenant` đã kiểm.
- Phạm vi khách thuê với ảnh phòng/ảnh sự cố/hợp đồng/hóa đơn cần liên kết thuê (P4) nên hiện chỉ nhận diện qua **tệp do chính khách upload**; các trường hợp khác mặc định **từ chối** để không mở quyền sớm.
- Khi P4/P6/P7 có bảng, bổ sung vào `FileService.RelatedEntityExistsAsync` và `IsRelatedEntityInScopeAsync`.

## 5. Kiểm chứng

- `dotnet build NhaTro/NhaTro.slnx` — 0 warning, 0 error.
- `dotnet test NhaTro/NhaTro.slnx` — UnitTests 148 pass (gồm 48 test storage mới).
- Chạy thử `dotnet run` tại `Web` với 3 tài khoản tạm (chủ trọ/khách thuê/admin) trên DB local: `StorageOptionsValidator` không báo lỗi và các kết quả khớp hợp đồng VHT-12:
  - `GET /api/files` khi chưa đăng nhập → `401`.
  - Chủ trọ upload PNG hợp lệ (`RoomPhoto`) → `200`; tải lại đúng `Content-Type` và byte khớp tệp gốc.
  - Khách thuê tải tệp của người khác → `403`; admin mọi thao tác (upload/tải/liệt kê) → `403`.
  - Nội dung PNG giả đuôi `.pdf` (loại `Contract`), `.webp` cho `PaymentProof`, khách thuê upload `RoomPhoto` → `400`/`403`; cặp loại tệp–đối tượng lệch → `400 InvalidRequest`.
  - PNG 6 MB cho `RoomPhoto` (giới hạn 5 MB) → `413 FileSizeExceeded` và không ghi đĩa.
  - `POST` thiếu antiforgery token → `400`.
  - Xóa mềm → `204`; tải lại → `404 FileNotFound`; người khác xóa → `404`.
  - Kiểm DB/đĩa: `StoredFileName` = uuid + đuôi, `RelativePath` = `yyyy/MM/<uuid>.<ext>` theo giờ VN, tệp nằm ngoài `wwwroot`; xóa mềm giữ tệp vật lý.
- Chưa có: quét virus (`MaliciousFile` — VHT-12 để sau), integration test cần SQL Server, và phạm vi khách thuê theo hợp đồng (chờ P4).
