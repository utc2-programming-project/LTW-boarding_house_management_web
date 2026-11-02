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
