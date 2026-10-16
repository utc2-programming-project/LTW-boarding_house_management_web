# VHT-13 — Tạo storage backend dùng chung

> **Nhánh:** `VHT-13-tao-storage-backend-dung-chung` (từ `dev`).
> **Đầu vào:** VHT-12 (`docs/task/de_xuat_hop_dong_storage.md`) — hợp đồng DTO/enum/giới hạn/quyền.
> **Phạm vi đợt:** 1C. Không làm nghiệp vụ P4/P6/P7 (chỉ cung cấp nền storage để VHT-26/33/54 dùng).
> **Lưu ý:** VHT-12 ghi rõ **chưa có giá trị nào được nhóm xác nhận**. Mọi giới hạn/định dạng/quyền
> trong code là **giá trị mặc định tạm** — xem mục 2 và danh sách chờ chốt ở mục 3.

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
| Dữ liệu | `Web/Features/Storage/Entities/StoredFile.cs`, `Web/Data/Configurations/Storage/StoredFileConfiguration.cs`, migration `AddStorageStoredFiles` | 12 trường theo hợp đồng + `RelativePath` nội bộ, xóa mềm, index theo đối tượng và người upload |
| Test | `NhaTro.UnitTests/Features/Storage/*`, `NhaTro.UnitTests/TestSupport/FakeWebHostEnvironment.cs` | loại/đuôi, nội dung thật, quyền (Admin/khách/chủ trọ), giới hạn dung lượng, đường dẫn, naming, cấu hình |

## 2. Giá trị mặc định đang dùng trong code (chờ nhóm xác nhận)

> Đây **không phải** quyết định đã được nhóm chốt. Code dùng các mặc định dưới đây và sẽ đổi khi nhóm chốt.

- Tệp **không** nằm trong `wwwroot`; chỉ tải qua `GET /api/files/{id}` sau khi kiểm quyền. Validator chặn cấu hình sai ngay khi khởi động.
- Tên lưu = `uuid` + đuôi; đường dẫn `yyyy/MM` theo giờ Việt Nam; tên gốc chỉ để hiển thị `Content-Disposition`.
- Kiểm **cả đuôi và nội dung thật**; không tin `Content-Type`/tên client. WebP chỉ cho ảnh phòng/ảnh sự cố.
- Giới hạn đọc ngay trên stream (`LimitedStreamReader`); tệp sai loại/quá giới hạn **không** được ghi đĩa. Metadata lưu lỗi thì xóa tệp vừa ghi.
- `StoredFileName`/`RelativePath`/`IsDeleted` không rời khỏi server (không có trong `FileMetadataDto`).
- Admin bị chặn hai lớp: policy endpoint `Business` và `BusinessGuard` trong service.
- Endpoint `/api` trả **401/403** thay vì chuyển hướng trang đăng nhập.
- Xóa là xóa mềm (`IsDeleted = true`), giữ tệp vật lý.
- Tải/xóa tệp **ngoài phạm vi** trả **404** (không phải 403) để không lộ tệp có tồn tại (VHT-12 mục 5 nghiêng về 404). Admin vẫn **403** vì bị chặn ở tầng policy, không phải "ngoài phạm vi".
- `PaymentProof` cho Chủ trọ/Quản lý: **mặc định chặn** (deny-by-default) vì VHT-12 mục 4.1 ghi "Cần chốt (nhập hộ khách?)"; khách thuê vẫn upload `PaymentProof` của mình được.
- Phản hồi tải tệp gắn `X-Content-Type-Options: nosniff`.

## 3. Còn để ngỏ (cần nhóm chốt — không tự quyết)

1. `PaymentProof` cho Chủ trọ/Quản lý: hiện **mặc định chặn** (mục 2). Cần chốt có cho phép "nhập hộ khách" không.
2. `403` hay `404` khi tải/xóa tệp của người khác (VHT-12 mục 5): code đã thống nhất **404**; cần nhóm xác nhận.
3. Số tệp tối đa mỗi đối tượng (`Storage:MaxFilesPerEntity` đang `null` = không giới hạn).
4. Khách thuê xóa tệp của mình: code **cho phép nếu là người upload** (`CanDelete` = `isUploader`), **chưa** kiểm điều kiện "nghiệp vụ chưa xử lý/xác nhận" (chờ trạng thái P6/P7). Khách xóa tệp của người khác → `404`.
5. Phạm vi Quản lý theo tòa nhà (chờ VHT-24) — hiện theo VHT-12 "tất cả file".
6. **Thời hạn lưu giấy tờ cá nhân (CCCD):** xóa mềm đang giữ tệp vật lý **vô thời hạn** — là dữ liệu nhạy cảm nên cần chốt thời hạn/xóa thật khi khách trả phòng; có thêm `DeletedAt`/`DeletedByUserId` không (VHT-12 mục 3).
7. `Content-Disposition` cho ảnh sự cố: chốt `inline` hay `attachment` (với Phúc/Phú).

## 4. Phần chờ nghiệp vụ P4/P6/P7

- `RelatedEntityType` `Contract`, `Invoice`, `Payment`, `MaintenanceIncident` chưa có bảng nên **chưa kiểm được** tồn tại đối tượng liên quan; `Room`/`Tenant` đã kiểm.
- Phạm vi khách thuê với ảnh phòng/ảnh sự cố/hợp đồng/hóa đơn cần liên kết thuê (P4) nên hiện chỉ nhận diện qua **tệp do chính khách upload**; các trường hợp khác mặc định **từ chối** để không mở quyền sớm.
- **Hệ quả bàn giao 1D:** khách thuê hiện **không xem được** ảnh phòng/hợp đồng/hóa đơn của mình (vd: hóa đơn gắn `Invoice`) vì chờ P4. Đây là **hành vi có chủ đích, không phải lỗi** — đừng ghi thành bug 1D.
- Khi P4/P6/P7 có bảng, bổ sung vào `FileService.RelatedEntityExistsAsync` và `IsRelatedEntityInScopeAsync`.

## 5. Đối chiếu hợp đồng VHT-12 (schema/DTO)

- **`RelatedEntityId` kiểu `string` (nvarchar 450):** PK thật của `Room`/`Tenant` là `int`; service parse `int` khi kiểm tồn tại. Contract/Invoice/Payment/MaintenanceIncident chưa có bảng nên **chưa xác nhận** được "kiểu phải khớp PK thật" (VHT-12 mục 3). Giữ `string` cho đa hình (một cột cho nhiều đối tượng) — cần chốt khi có schema thật.
- **Enum `Payment` vs `PaymentRequest`:** VHT-12 còn chỗ ghi `PaymentRequest` (mục 7). Code dùng `Payment` theo VHT-12 mục 1 & mục 3. Chưa có bảng nào nên chưa chốt tên cuối.
- 12 trường metadata khớp hợp đồng; `RelativePath` là trường **nội bộ bổ sung** (không có trong hợp đồng) để tải tệp ngoài `wwwroot`.

## 6. Bằng chứng migration (theo quy trình VHT-117 mục 4)

- **Tác động dữ liệu:** chỉ **tạo bảng mới** `StoredFiles` (+ 2 index, 1 FK tới `AspNetUsers`). Không đổi/xóa cột bảng cũ → không mất dữ liệu. `Down` drop bảng.
- **`dotnet ef migrations list --project NhaTro/Web`:**
  ```text
  00000000000000_CreateIdentitySchema
  20261008142750_CoreSchema
  20261016142707_AddStorageStoredFiles
  ```
  Không có dấu `(Pending)` → cả 3 đã áp trên LocalDB hiện tại.
- **Script idempotent:** sinh bằng `dotnet ef migrations script --idempotent --project NhaTro/Web`; khối `StoredFiles` được bọc `IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261016142707_AddStorageStoredFiles')`.
- **Thử `database update` trên DB test riêng (không dùng DB LocalDB dùng chung):**
  - DB **sạch** (`VHT13_Test_Clean`): `database update` → `Done`; bảng `StoredFiles` tồn tại; `__EFMigrationsHistory` đủ 3 dòng.
  - DB **có dữ liệu mẫu không nhạy cảm** (`VHT13_Test_Sample`): update tới `CoreSchema`, chèn 2 user + 1 Property + 1 Room + 1 Tenant, rồi update lên bản mới → `Done`; dữ liệu mẫu còn nguyên (2/1/1/1), `StoredFiles` = 0, history đủ 3 dòng.
  - Đã xóa 2 DB test sau khi kiểm.
- **Snapshot đồng bộ:** `dotnet ef migrations has-pending-model-changes` → `No changes have been made to the model since the last migration.`
- **DB local lúc áp migration KHÔNG phải DB sạch:** `aspnet-Web-fe3a4465-...` đã có `CreateIdentitySchema` + `CoreSchema` + 4 tài khoản dev trước khi áp; migration chỉ thêm bảng mới.

## 7. Kiểm chứng (build / test / smoke / CI)

- `dotnet build NhaTro/NhaTro.slnx` — 0 warning, 0 error.
- `dotnet test NhaTro/NhaTro.slnx` — UnitTests **149 pass** (gồm 49 test storage mới); IntegrationTests 1 pass; E2ETests 1 pass.
- Smoke test HTTP (`dotnet run` tại `Web`, 3 tài khoản tạm tạo trên DB local rồi dọn) khớp hợp đồng VHT-12:
  - `GET /api/files` khi chưa đăng nhập → `401`.
  - Chủ trọ upload PNG hợp lệ (`RoomPhoto`) → `200`; tải lại đúng `Content-Type`, byte khớp tệp gốc, có `X-Content-Type-Options: nosniff`.
  - Khách thuê tải/xóa tệp của người khác → `404`; admin mọi thao tác (upload/tải/liệt kê) → `403`.
  - Chủ trọ upload `PaymentProof` → `403` (mặc định chặn).
  - Nội dung PNG giả đuôi `.pdf` (loại `Contract`), `.webp` cho `PaymentProof`, khách thuê upload `RoomPhoto` → `400`/`403`; cặp loại tệp–đối tượng lệch → `400 InvalidRequest`.
  - PNG 6 MB cho `RoomPhoto` (giới hạn 5 MB) → `413 FileSizeExceeded` và không ghi đĩa.
  - `POST` thiếu antiforgery token → `400`.
  - Xóa mềm của chính mình → `204`; tải lại → `404 FileNotFound`; danh sách sau xóa → `[]`.
  - Kiểm DB/đĩa: `StoredFileName` = uuid + đuôi, `RelativePath` = `yyyy/MM/<uuid>.<ext>` theo giờ VN, tệp nằm ngoài `wwwroot`; xóa mềm giữ tệp vật lý.
- **CI / SHA / reviewer: chưa có.** Toàn bộ kiểm chứng trên là chạy local. Cần mở PR về `dev` để GitHub Actions `build-test` (`.github/workflows/ci.yml`) chạy đạt trên đúng head, và gán **reviewer độc lập** (đề xuất **Phát**, dự phòng **Đào** — không phải tác giả) theo bảng VHT-117. Ghi SHA head và link run khi PR mở.
- **Chưa có:** quét virus (`MaliciousFile` — VHT-12 để sau); integration test cần SQL Server; phạm vi khách thuê theo hợp đồng (chờ P4).
