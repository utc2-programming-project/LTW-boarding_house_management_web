# VHT-26 — Hoàn thiện tệp giấy tờ P3

- **Nhánh:** `VHT-26-hoan-thien-tep-giay-to-p3` (tạo từ `dev` tại `aa25bcf`)
- **Phạm vi:** Sprint 2A, subsystem P3 (hồ sơ khách thuê) + tầng storage dùng chung VHT-13.
- **Nguồn quyết định:** `docs/task/cong_viec_theo_tuan.md` (VHT-26), `docs/task/thao_tac_va_quyen.xlsx` (D9), `docs/thiet_ke_he_thong.md` (P3), `docs/task/de_xuat_hop_dong_storage.md`, `docs/Imp/VHT-13-storage-backend-dung-chung.md`.
- **Không làm:** UI P3 (VHT-36), bảng phân công Quản lý theo tòa nhà (VHT-24), liên kết thuê/hợp đồng (VHT-25/VHT-29), màn hình nhật ký (VHT-104).

## 1. Kết quả bàn giao

| Hạng mục | Trạng thái | Vị trí |
|---|---|---|
| Service giấy tờ P3 | Xong | `NhaTro/Web/Features/Tenants/Documents/` |
| DTO metadata giấy tờ | Xong | `TenantDocumentMetadataDto`, `TenantDocumentListDto` |
| Endpoint P3 (list/upload/tải/thay/xóa mềm) | Xong | `NhaTro/Web/Endpoints/Tenants/TenantDocumentEndpoints.cs` |
| Kiểm quyền upload + download, chặn ngoài phạm vi | Xong | `FileService`, `TenantDocumentScopeService`, `TenantDocumentPermissionRules` |
| Audit truy cập tệp trong SQL, không chứa nội dung | Xong | `FileAccessAuditLog`, `FileAccessAuditWriter`, migration `AddFileAccessAuditLog` |
| Chốt cách thay/xóa tệp | Xong | `TenantDocumentService.ReplaceAsync`, xóa mềm VHT-13 |
| Tự kiểm tra | Xong | mục 5 |

## 2. Phạm vi quyền (D9 + VHT-12 §4)

`TenantDocumentAccessLevel` gồm `None`, `Own`, `Owner`, `Manager`.

| Vai trò | Xem/tải | Upload | Thay | Xóa mềm | Xuất |
|---|---:|---:|---:|---:|---:|
| Admin | Không | Không | Không | Không | Không |
| Chủ trọ (`Owner`) | Có | Có | Có | Có | Có |
| Quản lý trong tòa nhà được giao (`Manager`) | Có | Có | Có | Có | **Không (D9)** |
| Quản lý ngoài phạm vi (`None`) | Không | Không | Không | Không | Không |
| Khách thuê đúng hồ sơ (`Own`) | Có | Có | Không | Không | Có |
| Khách thuê hồ sơ khác (`None`) | Không | Không | Không | Không | Không |

Điểm quan trọng:

- **Admin bị chặn 2 lớp:** policy `Business` ở endpoint + `FileService`/`TenantDocumentService` kiểm lại.
- **Ngoài phạm vi tải bằng id vẫn bị chặn:** `DownloadAsync` trả `null` → endpoint trả `404` (không phân biệt “không có tệp” với “không có quyền”), không mở stream.
- **Đường tải chung `/api/files/{id}` cũng bị chặn:** `FileService` dùng chung `ITenantDocumentScopeService`, không chỉ endpoint P3.
- **D9 “không xuất”:** `TenantDocumentPermissionRules.CanExport` trả `false` cho `Manager`; DTO danh sách trả cờ `CanExport` để UI VHT-36 ẩn nút xuất. Chưa có endpoint export trong VHT-26.
- **Phạm vi Quản lý:** VHT-24 chưa bàn giao bảng phân công. VHT-26 dùng interface `IManagerTenantScope` + implementation mặc định `NoManagerTenantScope` (deny-by-default). Khi VHT-24 có dữ liệu, chỉ thay implementation, không sửa service giấy tờ. Integration test đã kiểm cả trường hợp được giao (fake scope) và chưa được giao (mặc định).
- **Phạm vi Chủ trọ:** hiện giữ như P3 hiện tại (mọi hồ sơ còn tồn tại), chờ VHT-25 khóa theo chủ sở hữu/nhà.

## 3. Metadata và audit

### 3.1 Metadata giấy tờ

Không tạo bảng metadata mới: dùng `StoredFiles` của VHT-13 với cặp bắt buộc:

- `FileCategory = IdentityDocument`
- `RelatedEntityType = Tenant`
- `RelatedEntityId = tenantId` (chuỗi số)

`TenantDocumentService.NormalizeForTenant` ép lại 3 trường trên, nên client không thể đổi `relatedEntityId` sang hồ sơ khác để vượt kiểm phạm vi.

DTO trả client chỉ có: `Id`, `FileName`, `ContentType`, `FileSizeBytes`, `FileCategory`, `UploadedAt`, `UploadedByUserId`, `Description`. Không trả `StoredFileName`, `RelativePath`, `IsDeleted`.

### 3.2 Audit

Bảng `FileAccessAuditLogs` (migration `20261021014020_AddFileAccessAuditLog`):

| Cột | Ý nghĩa |
|---|---|
| `Id` | Khóa chính |
| `OccurredAt` | Thời điểm UTC |
| `ActorUserId`, `ActorRole` | Người gọi |
| `Action` | Upload / Download / List / Delete / Replace |
| `FileId`, `FileCategory`, `RelatedEntityType`, `RelatedEntityId` | Tệp/đối tượng liên quan |
| `Succeeded`, `ErrorCode` | Kết quả, mã lỗi nghiệp vụ |

**Không có** `FileName`, `StoredFileName`, `RelativePath`, `ContentType`, `Content` hay bất kỳ nội dung tệp nào. Unit test `Audit_khong_luu_ten_hay_noi_dung_giay_to` kiểm bằng reflection.

Audit ghi cả lượt thành công và lượt bị chặn (ví dụ khách khác, Quản lý ngoài tòa nhà). Lỗi ghi audit được log kỹ thuật (chỉ id/action) và không làm hỏng thao tác nghiệp vụ.

## 4. Cách thay/xóa tệp

- **Xóa:** xóa mềm `StoredFile.IsDeleted = true`, giữ tệp vật lý theo hợp đồng VHT-13. Metadata vẫn còn để truy vết; không tạo tệp mồ côi vì tệp vật lý luôn có metadata (kể cả đã xóa mềm).
- **Thay:** upload tệp mới trước; chỉ khi tệp mới lưu metadata thành công mới xóa mềm tệp cũ. Nếu bước xóa tệp cũ lỗi, service bù trừ bằng cách xóa mềm tệp mới để hồ sơ không còn hai tệp hiện hành; tệp vật lý vẫn giữ theo chính sách xóa mềm.
- **Không mất liên kết:** tệp mới luôn mang đúng `RelatedEntityType = Tenant`, `RelatedEntityId = tenantId`; tệp cũ chỉ bị đánh dấu xóa mềm, không xóa cứng.

## 5. Tự kiểm tra

### 5.1 Lệnh đã chạy

```powershell
dotnet build NhaTro/NhaTro.slnx --nologo
dotnet test NhaTro/NhaTro.slnx --no-build --nologo
```

Kết quả:

- Build: **0 warning, 0 error**.
- Unit tests: **219 passed**.
- Integration tests: **33 passed** (gồm test mới của VHT-26, chạy trên SQL Server LocalDB thật).
- E2E tests: **1 passed**.

### 5.2 Tiêu chí nghiệm thu

| Tiêu chí | Cách kiểm | Kết quả |
|---|---|---|
| Tải đúng hồ sơ | `FileServiceIntegrationTests` upload rồi Chủ trọ + khách đúng hồ sơ tải được | Đạt |
| Người ngoài phạm vi bị chặn dù biết id | Khách khác, Quản lý chưa được giao, Admin đều không tải được | Đạt |
| Quản lý trong tòa nhà được giao | Fake `IManagerTenantScope` trả `true`; Quản lý tải được nhưng `CanExport = false` | Đạt (chờ dữ liệu VHT-24) |
| Audit không chứa nội dung giấy tờ | Reflection trên `FileAccessAuditLog` + kiểm dòng audit thực tế | Đạt |
| Không mất liên kết khi thay/xóa | Upload mới trước, xóa mềm cũ sau; bù trừ nếu lỗi; xóa mềm giữ metadata | Đạt |
| Migration hợp lệ | Integration test áp migration mới lên database sạch | Đạt |

## 6. Hợp đồng endpoint P3

Tất cả dưới `/api/tenants/{tenantId}/documents`, policy `Business`:

| Method | Đường dẫn | Việc |
|---|---|---|
| `GET` | `/` | Danh sách metadata + cờ `CanUpload/CanDelete/CanReplace/CanExport`, giới hạn dung lượng/đuôi |
| `POST` | `/` | Upload giấy tờ (multipart `file`, `description`) |
| `GET` | `/{fileId}` | Tải tệp sau khi kiểm phạm vi |
| `PUT` | `/{fileId}` | Thay tệp (multipart `file`, `description`) |
| `DELETE` | `/{fileId}` | Xóa mềm |

Endpoint chuyên P3 tự đặt `FileCategory = IdentityDocument`, `RelatedEntityType = Tenant`, `RelatedEntityId = tenantId`; client không gửi các trường này.

## 7. Điểm còn chờ / rủi ro

1. **VHT-24 — phân công Quản lý theo tòa nhà:** hiện `NoManagerTenantScope` từ chối tất cả Quản lý để không mở quyền sớm. Cần thay implementation khi VHT-24 bàn giao. Đây là gap có chủ đích, đã có interface + test.
2. **VHT-25 — phạm vi Chủ trọ theo chủ sở hữu/nhà:** hiện Chủ trọ xem mọi hồ sơ còn tồn tại như P3 hiện tại.
3. **D9 “không xuất”:** VHT-26 chỉ trả cờ `CanExport = false` cho Quản lý; chưa có endpoint/report export để chặn. Khi làm P8/VHT-36 phải kiểm lại cờ này ở server, không chỉ ẩn nút.
4. **Audit retention/D16:** bảng audit đã ghi; màn hình xem nhật ký là VHT-104 (điều kiện D16). Chưa có job dọn audit cũ.
5. **Xóa cứng tệp vật lý:** giữ nguyên chính sách VHT-13 (xóa mềm, giữ tệp). Thời hạn lưu/xóa thật giấy tờ CCCD vẫn là quyết định mở của VHT-13 §3.2.
6. **UI P3:** VHT-36 dùng DTO/endpoint này; VHT-26 không sửa UI.
