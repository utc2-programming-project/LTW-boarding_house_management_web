# Nhật ký rà soát migration

Ghi kết quả từng lượt rà soát migration của VHT-28 (2C/2D/2E). Chỉ ghi việc đã chạy thật; checklist review đầy đủ ở [VHT-117](../../archive/imp/VHT-117-review-va-thu-tu-merge-tuan-1.md).

## Cách chạy

```powershell
powershell -File scripts/database/kiem_tra_migration.ps1 -Database <ten_db_kiem_tra> [-KeepDatabase]
```

- Script tạo DB sạch, áp đủ migration, kiểm PK/FK/unique/check, rồi dựng lại theo đường **nâng cấp**: áp tới migration trước lượt cần rà, seed dữ liệu cũ, áp phần còn lại, so số dòng và kiểm ràng buộc mới.
- Không dùng `--no-build`; pending phải so theo `--connection` của DB kiểm tra, không so với DB trong `appsettings`.
- Từ 02/11/2026 script có pha nâng cấp thật và assert schema hóa đơn. Trước đó pha seed chạy sau khi đã áp đủ migration nên không chứng minh được đường nâng cấp.

## Lượt 2C — hóa đơn (VHT-121)

**Nguồn rà:** `dev@049c22d`, PR #55 (`85a1b12`), migration `20261031120915_AddInvoices` (17/18), đã merge ở `c96c84c`. Đối chiếu VHT-28 §5 và VHT-34 §7.

**Kết luận: không có blocker**, không cần migration sửa lỗi.

### Bằng chứng

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build NhaTro/NhaTro.slnx` | 0 warning, 0 error |
| `dotnet ef migrations has-pending-model-changes` | Không có thay đổi model |
| `kiem_tra_migration.ps1 -Database NhaTro_VHT121_invoices -KeepDatabase` | `KET QUA: DAT` |
| `dotnet test NhaTro.UnitTests --filter FullyQualifiedName~Invoices` | 24 đạt; 3 lỗi culture vi-VN có sẵn ở `SubmitPaymentRequestTests` |

- DB sạch: đủ 18 migration, 0 pending; 2 bảng hóa đơn có PK, FK `NO_ACTION`, unique `(ContractId, PeriodStart)`, 6 CHECK.
- DB nâng cấp: seed `Users|Properties|Rooms|Tenants|Contracts = 1|1|1|1|1` trước lượt hóa đơn, áp phần còn lại vẫn giữ `1|1|1|1|1`.
- Chặn đúng: trùng kỳ, FK hợp đồng không tồn tại, `PeriodEnd < PeriodStart`, tổng âm, dòng thường có tiền âm.

### Ràng buộc hóa đơn đã xác nhận

| Ràng buộc | Cài đặt |
| --- | --- |
| `PeriodStart <= PeriodEnd` | `CK_Invoices_Period` |
| Unique `(ContractId, PeriodStart)` | `IX_Invoices_ContractId_PeriodStart` |
| `TotalAmount >= 0` | `CK_Invoices_TotalAmount` |
| `Status` ∈ Draft(0)/Issued(1)/Paid(2) | `CK_Invoices_Status` |
| `Quantity > 0`, `UnitPrice >= 0` | `CK_InvoiceItems_Quantity`, `CK_InvoiceItems_UnitPrice` |
| `Amount` âm chỉ khi `Discount` | `CK_InvoiceItems_AmountSign` |
| Tiền `decimal(18,2)`, ngày `date`, enum `int`, FK `Restrict` | script assert 4/4 cột tiền, 2/2 cột ngày |

### Delta ERD cho 2D (VHT-122)

| Bảng | Cột/quan hệ cần vẽ |
| --- | --- |
| `Invoices` | `Id` PK; `ContractId` GUID FK → `Contracts.ContractId`; `PeriodStart/PeriodEnd` date; `Status` int; `TotalAmount` decimal(18,2); `CreatedAt/UpdatedAt/IssuedAt`; `RowVersion`; unique `(ContractId, PeriodStart)`; index `(PeriodStart, Status)` |
| `InvoiceItems` | `Id` PK; `InvoiceId` int FK → `Invoices.Id`; `ItemType` int; `Description` nvarchar(250); `Unit` nvarchar(20); `Quantity/UnitPrice/Amount` decimal(18,2); `DisplayOrder`; index `(InvoiceId, DisplayOrder)` |

Quan hệ: `Contracts 1—N Invoices 1—N InvoiceItems`, xóa `Restrict` cả hai nhánh.

### Ghi nhận chưa chặn

1. Snapshot PR #55 đổi 501+/367− chủ yếu do sắp xếp block; dùng `has-pending-model-changes` làm bằng chứng thay vì đọc diff.
2. VHT-34 ghi `InvoiceDto.ContractId` là `int`, thực tế `Guid` — sửa khi cập nhật ERD/Word.
3. `Quantity/UnitPrice` hóa đơn `decimal(18,2)`, P5 dùng `decimal(18,4)` — chốt lại ở VHT-53/54; `Amount` snapshot đã làm tròn từng dòng.
4. `CK_InvoiceItems_AmountSign` hardcode `ItemType = 4`; thêm enum phải tạo migration sửa CHECK.
5. `DisplayOrder` không unique; service đọc phải sắp kèm `Id`.
6. Hóa đơn chưa có `CreatedBy`/audit — chờ yêu cầu nghiệp vụ, không tự thêm.
7. Hóa đơn không có `IsDeleted` (D13 không cho hủy), nên chưa cần filter xóa mềm.

### Còn lại

- Chạy tay trên DB `NhaTro_VHT121_invoices`: kiểm 2 bảng, FK, unique; mở `/invoices`, `/my-invoices` sau khi seed hóa đơn.
- VHT-28 2A yêu cầu người thứ hai chạy script — chưa có.

## Lượt 2D — ERD theo migration đã merge (VHT-122)

**Nguồn rà:** `dev@12a7156`, 18 migration tới `20261101110944_AddManagerApprovalQueue`. Đối chiếu VHT-28 §3.2 và delta ERD của lượt 2C.

**Kết luận: ERD khớp schema EF**, 0 khác biệt ở 4 nhóm `sys.*` trong phạm vi 19 bảng ERD + `AspNetUsers`.

### Bằng chứng

| Lệnh | Kết quả |
| --- | --- |
| `git diff --check` | Sạch (không khoảng trắng thừa) |
| `sqlcmd -i <bản sao ERD SQL, DB NhaTro_ERD_sql>` chạy 2 lần | Lần 1 và 2 đều không lỗi; số bảng/khóa không đổi, seed không nhân đôi dòng |
| `dotnet ef database update --project NhaTro/Web --connection "...Database=NhaTro_ERD_ef..."` | `Done.` |
| `dotnet ef migrations has-pending-model-changes` | Không có thay đổi model |
| Diff `sys.columns` | 184 / 184 dòng trùng (tên, kiểu, độ dài, precision/scale, nullable, identity, computed) |
| Diff `sys.indexes` | 57 / 57 index trùng cả tên lẫn cột/unique/filter |
| Diff `sys.foreign_keys` | 22 / 22 FK trùng tên, cột, bảng đích, `NO_ACTION` |
| Diff `sys.check_constraints` | 29 / 29 CHECK trùng định nghĩa |
| Diff `sys.triggers` | 1 / 1: `TR_Contracts_PreventApprovedOverlap` |

### Khác biệt được giải thích (không phải lỗi)

1. **Thứ tự cột** lệch ở `AspNetUsers` (`FullName` sau `AccessFailedCount`) và `Tenants` (`CreatedAt`… về cuối bảng) — do migration thêm cột bằng `ADD`, EF luôn nối cuối bảng; không ảnh hưởng schema.
2. **Giá trị mặc định bit**: ERD ghi `((0))`/`((1))`, EF ghi `(CONVERT([bit],(0)))` — cùng một mặc định, chỉ khác cách SQL Server chuẩn hóa.
3. **Bảng ngoài phạm vi**: `AspNetRoles/UserRoles/Claims/Logins/Tokens/Passkeys`, `__EFMigrationsHistory` không nằm trong ERD (đã ghi chú cuối hình SVG).

### ERD đã cập nhật

- `so_do_quan_he_vht5_schema.sql`: 19 `CREATE TABLE` (7 bảng VHT-5 + 12 bảng P4/P5/P6/P7), trigger chống chồng hợp đồng, seed công tắc + dữ liệu mẫu chạy lại được.
- `so_do_quan_he_vht5.svg`: 19 bảng vẽ theo 5 cụm, ghi đủ `PK/FK/UQ/nullable` và nhãn `1—N` + `Restrict`.

### Còn lại

- 2D.4: chưa gửi Phúc bản ERD + ghi chú để ghép Word P1–P4.
- VHT-28 2A yêu cầu người thứ hai chạy script — chưa có.

## Lượt 2E — kiểm trên bản chung (VHT-123)

**Nguồn rà:** `dev@951ab06`, 21 migration tới `20261103080000_AddIncidentPhotoAttachments`. Nhánh làm việc `VHT-123-kiểm-migration-va-erd-tren-bản-chung` (base `951ab06`).

**Kết luận: ERD khớp schema EF và script kiểm đạt trên cả DB sạch lẫn DB nâng cấp.** Không phát hiện blocker migration; không sửa migration/entity.

### Bằng chứng

| Lệnh | Kết quả |
| --- | --- |
| `dotnet ef migrations has-pending-model-changes --project NhaTro/Web` | `No changes have been made to the model since the last migration.` |
| `powershell -File scripts/database/kiem_tra_migration.ps1 -Database NhaTro_VHT123_2E -KeepDatabase` | `KET QUA: DAT` — 21 migration, 0 pending |
| `sqlcmd -i <bản sao ERD SQL, DB NhaTro_VHT123_erd>` chạy 2 lần | Lần 1 tạo 21 bảng nghiệp vụ; lần 2 `(0 rows affected)`, không lỗi, seed không nhân đôi (21 `ManagerToggles`) |
| `dotnet ef database update --project NhaTro/Web --connection "...Database=NhaTro_VHT123_ef..."` | `Done.` |
| `git diff --check` | Sạch sau khi chuẩn hóa xuống dòng SVG (`CRCRLF` → `LF`) |

### Diff ERD SQL vs EF migrations (5 nhóm `sys.*`, bỏ bảng `AspNet*` phụ)

| Nhóm | ERD | EF | Khác |
| --- | --- | --- | --- |
| `sys.columns` (tên, kiểu, độ dài, precision/scale, nullable, identity, computed) | 195 | 195 | 0 |
| `sys.indexes` (tên, loại, unique, PK, filter, cột theo thứ tự) | 64 | 64 | 0 |
| `sys.foreign_keys` (tên, cha→con, hành vi xóa, disabled) | 30 | 30 | 0 |
| `sys.check_constraints` (tên, định nghĩa) | 40 | 40 | 0 |
| `sys.triggers` | 3 | 3 | 0 |

### Delta ERD bổ sung ở 2E

| Bảng | Nội dung |
| --- | --- |
| `MaintenanceIncidents` | 14 cột; PK `Id`; FK `RoomId`, `ReportedByUserId` (Restrict); 4 CHECK (Category 0..4, Urgency 0..2, Status 0..2, `RepairCost >= 0`); 2 index; `RepairCost decimal(18,0)` |
| `MaintenanceIncidentHistories` | 8 cột; PK `Id bigint`; FK `IncidentId` (Restrict); 3 CHECK; `Note nvarchar(2000)` sau `20261102125721`; `ActorUserId` là liên kết mềm, không FK |
| `IncidentPhotoAttachments` | PK `FileId` (1 tệp = 1 ảnh); FK `FileId`, `IncidentId`, `ApprovalRequestId` (Restrict); 2 CHECK (`Kind IN (0,1)`, phải có ít nhất một đích); index `(IncidentId, Kind)`, `(ApprovalRequestId)` |

- SVG thêm 3 khung P7 nối `Rooms`/`AspNetUsers`/`StoredFiles`/`ManagerApprovalRequests`; footer viết lại đúng UTF-8, thêm dòng enum + check P7.

### Kiểm ràng buộc P7 bằng dữ liệu thật (pha nâng cấp `NhaTro_VHT123_2E`)

- 3 bảng P7 rỗng sau khi nâng cấp DB cũ → không làm méo dữ liệu có sẵn.
- Thêm được sự cố, lịch sử, ảnh trỏ tệp + sự cố.
- Bị chặn đúng: ảnh không có đích; `Kind` ngoài 0..1; `IncidentId` không tồn tại; `FileId` không tồn tại; `ApprovalRequestId` không tồn tại.

### Còn lại

- 2E.5: gửi Phúc bản ERD cuối + ghi chú để ghép Word/slide.
- VHT-28 2A yêu cầu người thứ hai chạy script — vẫn chưa có.
- SHA sẽ đổi khi VHT-124/126 merge → chạy lại TASK-02/04/06 (script + diff) trước khi publish.

## VHT-51 — snapshot dịch vụ và gia cố kỳ ghi chỉ số (09/11/2026)

| Migration | Nội dung rà soát |
| --- | --- |
| `20261109163100_AddUtilityServicePeriodSnapshots` | Thêm `HasServiceSnapshots` vào `UtilityReadingPeriods`; tạo `UtilityServicePeriodSnapshots`, FK cascade về kỳ, unique `(UtilityReadingPeriodId, SourceServicePriceHistoryId)` và các CHECK số tiền/số lượng. |
| `20261109195353_HardenUtilityReadingPeriods` | Nới `Unit` của `InvoiceItems` và `UtilityBillingPeriodSnapshots` lên 30 ký tự; thay index `InvoiceId` bằng unique filtered index để một invoice chỉ thuộc một kỳ. |

SVG ERD chưa vẽ thêm bảng snapshot dịch vụ trong lượt này; schema SQL là nguồn đối chiếu cập nhật cho hai migration trên.
