# VHT-13 — Tạo storage backend dùng chung

> **Nhánh:** `VHT-13-tao-storage-backend-dung-chung` (từ `dev`).
> **Đầu vào:** VHT-12 — hợp đồng storage (`docs/task/de_xuat_hop_dong_storage.md`) về DTO/enum/giới hạn/quyền.
> **Phạm vi đợt:** 1C. Không làm nghiệp vụ P4/P6/P7 (chỉ cung cấp nền storage để VHT-26/33/54 dùng).
> **Lưu ý:** hợp đồng storage ghi rõ **chưa có giá trị nào được nhóm xác nhận**. Mọi giới hạn/định dạng/quyền
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
- Tải/xóa tệp **ngoài phạm vi** trả **404** (không phải 403) để không lộ tệp có tồn tại — theo gợi ý hợp đồng storage mục 5. Lưu ý: các ví dụ hợp đồng **8.7/8.8 minh họa `403`** và hợp đồng **chưa có ca `404`** cho tệp ngoài phạm vi, nên đây là lựa chọn chờ nhóm chốt (mục 3). Admin vẫn **403** vì bị chặn ở tầng policy, không phải "ngoài phạm vi".
- `PaymentProof` cho Chủ trọ/Quản lý: **mặc định chặn** (deny-by-default) vì hợp đồng storage mục 4.1 ghi "Cần chốt (nhập hộ khách?)"; khách thuê vẫn upload `PaymentProof` của mình được.
- Khách thuê **xóa tệp của chính mình**: **mặc định chặn** (deny-by-default) vì hợp đồng storage mục 4.3 ghi "Cần chốt"; mở lại kèm điều kiện "nghiệp vụ chưa xử lý" khi P6/P7 sẵn sàng.
- Phản hồi tải tệp gắn `X-Content-Type-Options: nosniff`.

## 3. Còn để ngỏ (cần nhóm chốt — không tự quyết)

### 3.1. Cần chốt ngay (đang ảnh hưởng hành vi đã code)

1. `PaymentProof` cho Chủ trọ/Quản lý: hiện **mặc định chặn** (mục 2). Cần chốt có cho phép "nhập hộ khách" không.
2. `403` hay `404` khi tải/xóa tệp của người khác (hợp đồng storage mục 5): code đã thống nhất **404**; cần nhóm xác nhận.
4. Khách thuê xóa tệp của mình: hiện **chặn mặc định** (deny-by-default) cho tới khi nhóm chốt; khi mở cần thêm điều kiện "nghiệp vụ chưa xử lý/xác nhận" (chờ trạng thái P6/P7). Khách xóa tệp của người khác → `404`.
8. **Tệp mồ côi khi siết kiểm tra tồn tại (mục 4):** `Contract`/`Invoice`/`Payment`/`MaintenanceIncident` hiện chưa kiểm tồn tại đối tượng nên có thể đã có tệp gắn `relatedEntityId` sai; khi P6/P7 có bảng, phải chốt **ai chịu trách nhiệm rà/dọn** các tệp này (script dọn + thời điểm chạy) — hoặc chọn phương án **tạm từ chối** upload các loại đó đến khi có bảng. Cả hai đều cần nhóm chốt.

### 3.2. Có thể chờ (không ảnh hưởng đợt 1C)

3. Số tệp tối đa mỗi đối tượng (`Storage:MaxFilesPerEntity` đang `null` = không giới hạn).
5. Phạm vi Quản lý theo tòa nhà (chờ VHT-24) — hiện theo hợp đồng storage "tất cả file".
6. **Thời hạn lưu giấy tờ cá nhân (CCCD):** xóa mềm đang giữ tệp vật lý **vô thời hạn** — là dữ liệu nhạy cảm nên cần chốt thời hạn/xóa thật khi khách trả phòng; có thêm `DeletedAt`/`DeletedByUserId` không (hợp đồng storage mục 3).
7. `Content-Disposition` cho ảnh sự cố: chốt `inline` hay `attachment` (với Phúc/Phú).

## 4. Phần chờ nghiệp vụ P4/P6/P7

- `RelatedEntityType` `Contract`, `Invoice`, `Payment`, `MaintenanceIncident` chưa có bảng nên **chưa kiểm được** tồn tại đối tượng liên quan; `Room`/`Tenant` đã kiểm.
- **Hệ quả của việc chưa có bảng:** `RelatedEntityExistsAsync` hiện **bỏ qua** kiểm tồn tại (trả `true`) cho các loại trên, nên Chủ trọ/Quản lý có thể upload và gắn tệp vào `relatedEntityId` **bất kỳ, kể cả không tồn tại**. Khách thuê vẫn bị chặn bởi kiểm phạm vi (mặc định từ chối). Cần siết khi P4/P6/P7 có bảng — **tệp đã gắn sai sẽ không tự được dọn, nên cách rà/dọn (hoặc tạm từ chối upload) được đưa vào mục 3 để nhóm chốt**.
- Phạm vi khách thuê với ảnh phòng/ảnh sự cố/hợp đồng/hóa đơn cần liên kết thuê (P4) nên hiện chỉ nhận diện qua **tệp do chính khách upload**; các trường hợp khác mặc định **từ chối** để không mở quyền sớm.
- **Hệ quả bàn giao 1D:** khách thuê hiện **không xem được** ảnh phòng/hợp đồng/hóa đơn của mình (vd: hóa đơn gắn `Invoice`) vì chờ P4. Đây là **hành vi có chủ đích, không phải lỗi** — đừng ghi thành bug 1D.
- Khi P4/P6/P7 có bảng, bổ sung vào `FileService.RelatedEntityExistsAsync` và `IsRelatedEntityInScopeAsync`.

## 5. Đối chiếu hợp đồng storage (schema/DTO)

- **`RelatedEntityId` kiểu `string` (nvarchar 450):** PK thật của `Room`/`Tenant` là `int`; service parse `int` khi kiểm tồn tại. Contract/Invoice/Payment/MaintenanceIncident chưa có bảng nên **chưa xác nhận** được "kiểu phải khớp PK thật" (hợp đồng storage mục 3). Giữ `string` cho đa hình (một cột cho nhiều đối tượng) — cần chốt khi có schema thật.
- **Enum `Payment` vs `PaymentRequest`:** hợp đồng storage (`docs/task/de_xuat_hop_dong_storage.md`) ghi ở **mục 1** rằng `MaintenanceIncident`/`Payment` là tên đề xuất, và nếu schema P6 gắn minh chứng vào yêu cầu thanh toán thì **đổi thành `PaymentRequest`**. Code đang dùng `Payment`. Chưa có bảng nào nên chưa chốt tên cuối.
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
- **Script idempotent:** sinh bằng `dotnet ef migrations script --idempotent --project NhaTro/Web/Web.csproj`; khối `StoredFiles` được bọc `IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261016142707_AddStorageStoredFiles')`. Trích đoạn sinh ra:
  ```sql
  BEGIN TRANSACTION;
  IF NOT EXISTS (
      SELECT * FROM [__EFMigrationsHistory]
      WHERE [MigrationId] = N'20261016142707_AddStorageStoredFiles'
  )
  BEGIN
      CREATE TABLE [StoredFiles] (
          [Id] uniqueidentifier NOT NULL,
          [FileName] nvarchar(260) NOT NULL,
          [StoredFileName] nvarchar(128) NOT NULL,
          [RelativePath] nvarchar(512) NOT NULL,
          [ContentType] nvarchar(128) NOT NULL,
          [FileSizeBytes] bigint NOT NULL,
          [FileCategory] int NOT NULL,
          [UploadedByUserId] nvarchar(450) NOT NULL,
          [UploadedAt] datetimeoffset NOT NULL,
          [RelatedEntityId] nvarchar(450) NOT NULL,
          [RelatedEntityType] int NOT NULL,
          [Description] nvarchar(500) NULL,
          [IsDeleted] bit NOT NULL,
          CONSTRAINT [PK_StoredFiles] PRIMARY KEY ([Id]),
          CONSTRAINT [FK_StoredFiles_AspNetUsers_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
      );
  END;

  IF NOT EXISTS (
      SELECT * FROM [__EFMigrationsHistory]
      WHERE [MigrationId] = N'20261016142707_AddStorageStoredFiles'
  )
  BEGIN
      CREATE INDEX [IX_StoredFiles_RelatedEntityType_RelatedEntityId] ON [StoredFiles] ([RelatedEntityType], [RelatedEntityId]);
  END;

  IF NOT EXISTS (
      SELECT * FROM [__EFMigrationsHistory]
      WHERE [MigrationId] = N'20261016142707_AddStorageStoredFiles'
  )
  BEGIN
      CREATE INDEX [IX_StoredFiles_UploadedByUserId] ON [StoredFiles] ([UploadedByUserId]);
  END;

  IF NOT EXISTS (
      SELECT * FROM [__EFMigrationsHistory]
      WHERE [MigrationId] = N'20261016142707_AddStorageStoredFiles'
  )
  BEGIN
      INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
      VALUES (N'20261016142707_AddStorageStoredFiles', N'10.0.12');
  END;

  COMMIT;
  GO
  ```
  Trích đoạn trên gồm đủ 1 `CREATE TABLE` (chứa PK + 1 FK), 2 `CREATE INDEX` và dòng `INSERT` vào `__EFMigrationsHistory`.
  (File đầy đủ sinh lại được bằng lệnh trên; có thể đính kèm theo PR nếu reviewer yêu cầu.)
- **Thử `database update` trên DB test riêng (không dùng DB LocalDB dùng chung):**
  - DB **sạch** (`VHT13_Test_Clean`): `database update` → `Done`; bảng `StoredFiles` tồn tại; `__EFMigrationsHistory` đủ 3 dòng.
  - DB **có dữ liệu mẫu không nhạy cảm** (`VHT13_Test_Sample`): update tới `CoreSchema`, chèn 2 user + 1 Property + 1 Room + 1 Tenant, rồi update lên bản mới → `Done`; dữ liệu mẫu còn nguyên (2/1/1/1), `StoredFiles` = 0, history đủ 3 dòng.
  - Đã xóa 2 DB test sau khi kiểm.
- **Snapshot đồng bộ:** `dotnet ef migrations has-pending-model-changes` → `No changes have been made to the model since the last migration.`
- **DB local lúc áp migration KHÔNG phải DB sạch:** `aspnet-Web-fe3a4465-...` đã có `CreateIdentitySchema` + `CoreSchema` + 4 tài khoản dev trước khi áp; migration chỉ thêm bảng mới.

## 7. Kiểm chứng (build / test / smoke / CI)

- `dotnet build NhaTro/NhaTro.slnx` — 0 warning, 0 error.
- `dotnet test NhaTro/NhaTro.slnx` — UnitTests **149 pass**, trong đó **87 ca** thuộc `Features/Storage` (đã gồm ca chặn mặc định `PaymentProof` và chặn xóa của khách thuê); IntegrationTests 1 pass; E2ETests 1 pass.
- Smoke test HTTP (`dotnet run` tại `Web`, 3 tài khoản tạm tạo trên DB local rồi dọn) khớp hợp đồng storage:
  - `GET /api/files` khi chưa đăng nhập → `401`.
  - Chủ trọ upload PNG hợp lệ (`RoomPhoto`) → `200`; tải lại đúng `Content-Type`, byte khớp tệp gốc, có `X-Content-Type-Options: nosniff`.
  - Khách thuê tải/xóa tệp của **người khác** → `404` (đã smoke). Riêng **khách thuê xóa tệp của chính mình** thì chưa smoke — xem "Chưa kiểm". Admin mọi thao tác (upload/tải/liệt kê) → `403`.
  - Chủ trọ upload `PaymentProof` → `403` (mặc định chặn).
  - Nội dung PNG giả đuôi `.pdf` (loại `Contract`) → `400 InvalidFileType`; `.webp` cho `PaymentProof` → `400 InvalidFileType`; cặp loại tệp–đối tượng lệch → `400 InvalidRequest`.
  - Khách thuê upload `RoomPhoto` (không được phép) → `403`.
  - PNG 6 MB cho `RoomPhoto` (giới hạn 5 MB) → `413 FileSizeExceeded` và không ghi đĩa.
  - `POST` thiếu antiforgery token → `400` (đã smoke). `DELETE /api/files/{id}` cũng **bắt buộc** antiforgery token (endpoint tự kiểm trước khi xóa): dự kiến thiếu token → `400 InvalidRequest`, **nhưng ca này chưa smoke (suy từ code)** — xem "Chưa kiểm".
  - Chủ trọ xóa mềm tệp **do chính Chủ trọ upload** → `204`; tải lại → `404 FileNotFound`; danh sách sau xóa → `[]`. (Ca Chủ trọ xóa tệp **do khách thuê upload** chưa kiểm — xem danh sách "chưa kiểm".)
  - Kiểm DB/đĩa: `StoredFileName` = uuid + đuôi, `RelativePath` = `yyyy/MM/<uuid>.<ext>` theo giờ VN, tệp nằm ngoài `wwwroot`; xóa mềm giữ tệp vật lý.
- **CI / SHA / reviewer: chưa có.** Toàn bộ kiểm chứng trên là chạy local. Cần mở PR về `dev` để GitHub Actions `build-test` (`.github/workflows/ci.yml`) chạy đạt trên đúng head, và gán **reviewer độc lập** (đề xuất **Phát**, dự phòng **Đào** — không phải tác giả) theo bảng VHT-117. Ghi SHA head và link run khi PR mở.
- **Chưa kiểm (bằng smoke test HTTP):** ca Chủ trọ xóa tệp **do khách thuê upload**; ca khách thuê xóa tệp **của chính mình** (mặc định chặn → `404`) — hiện chỉ có unit test; ca `DELETE` **thiếu antiforgery token** (suy từ code: endpoint tự kiểm trước khi xóa).
- **Chưa có:** quét virus (`MaliciousFile` — hợp đồng storage để sau); integration test cần SQL Server; phạm vi khách thuê theo hợp đồng (chờ P4).
