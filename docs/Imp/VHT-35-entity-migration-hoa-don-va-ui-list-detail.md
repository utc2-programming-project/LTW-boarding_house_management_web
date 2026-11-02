# VHT-35 — Entity/migration hóa đơn và UI list/detail

> **Nhánh:** `VHT-35-entity-migration-hoa-don-va-ui-list-detail`, tạo từ `dev` tại `d9c421b`.
> **Đợt/phân hệ:** Sprint 2C, P6 — Hóa đơn và thanh toán.
> **Phạm vi:** entity + EF configuration + migration; query hóa đơn theo quyền; DTO list/detail; UI list/detail VI/EN.
> **Không làm:** tạo/phát hành hóa đơn, xác nhận thanh toán, bảng `Payments` (để VHT-54).
> **Nguồn đối chiếu:** VHT-34 (rule/DTO), VHT-28 (checklist migration), `thao_tac_va_quyen.xlsx` mục #34.

## 1. Schema

| Bảng | Cột chính | Ràng buộc |
| --- | --- | --- |
| `Invoices` | `Id` int identity, `ContractId` Guid FK, `PeriodStart`/`PeriodEnd` date, `Status` int, `TotalAmount` decimal(18,2), `CreatedAt`, `UpdatedAt`, `IssuedAt`, `RowVersion` | FK `Contracts` **Restrict**; unique `(ContractId, PeriodStart)`; index `(PeriodStart, Status)`; CHECK `PeriodEnd >= PeriodStart`, `Status IN (0,1,2)`, `TotalAmount >= 0` |
| `InvoiceItems` | `Id` int identity, `InvoiceId` int FK, `ItemType` int, `Description` nvarchar(250), `Unit` nvarchar(20), `Quantity`/`UnitPrice`/`Amount` decimal(18,2), `DisplayOrder` int | FK `Invoices` **Restrict**; index `(InvoiceId, DisplayOrder)`; CHECK `Quantity > 0`, `UnitPrice >= 0`, dấu `Amount` theo `ItemType` (Discount = 4 được âm) |

- Kỳ là khoảng ngày `[PeriodStart, PeriodEnd]`, tính cả hai đầu (VHT-34).
- Một hợp đồng chỉ có một hóa đơn cho mỗi `PeriodStart`; migration `20261031120915_AddInvoices`.
- `InvoiceStatus`: `Draft = 0`, `Issued = 1`, `Paid = 2`. Không có trạng thái hủy.
- VHT-35 chưa có bảng `Payments`: `PaidAmount` suy ra bằng `TotalAmount` khi `Status = Paid`, ngược lại `0`; `LatestPayment` luôn `null` cho tới VHT-54.

## 2. Query theo quyền

`IInvoiceQueryService` (`GetInvoicesAsync`, `GetInvoiceAsync`, `GetContractOptionsAsync`) nhận `PropertyCaller` và tự giới hạn phạm vi:

| Vai trò | Phạm vi |
| --- | --- |
| Chủ trọ | Hợp đồng thuộc phòng của nhà có `Property.OwnerId = caller.UserId` |
| Quản lý | Hợp đồng thuộc nhà có `PropertyManager` đang hiệu lực (`RevokedAt IS NULL`) |
| Khách thuê | Hợp đồng có `ContractMember` trỏ tới `Tenant.UserId = caller.UserId` |
| Admin | Từ chối (`UnauthorizedAccessException`) |

- Trang dùng policy `InvoiceView`: đã đăng nhập, **không** phải Admin, thuộc Chủ trọ/Quản lý/Khách thuê.
- Chi tiết ngoài phạm vi và không tồn tại trả cùng một lỗi (`KeyNotFoundException`) để không lộ ID.
- Join phòng/nhà/khách thuê dùng `IgnoreQueryFilters()` để hóa đơn lịch sử vẫn đọc được sau khi xóa mềm.
- Lọc danh sách: `Year`/`Month` (giao với kỳ hóa đơn), `ContractId`, `Status`, phân trang. KPI tổng hợp trên toàn bộ kết quả lọc, không chỉ trang hiện tại.

### Mẫu DTO list

```json
{
  "items": [
    {
      "id": 12,
      "contractId": "8f14e45f-ceea-467f-a8d7-8a3b0d4c1f21",
      "contractNumber": "HD-2026-001",
      "roomCode": "A101",
      "propertyName": "Nhà trọ A",
      "tenantNames": ["Nguyễn Văn A"],
      "periodStart": "2026-10-01",
      "periodEnd": "2026-10-31",
      "status": 1,
      "totalAmount": 2900000,
      "paidAmount": 0,
      "outstandingAmount": 2900000
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "summary": { "draftCount": 0, "issuedCount": 1, "paidCount": 0, "outstandingAmount": 2900000 }
}
```

`InvoiceDto` chi tiết bổ sung `contractNumber`, `roomCode`, `propertyName`, `tenantNames`, `createdAt`, `issuedAt` và danh sách `items` đã sắp theo `DisplayOrder`.

## 3. UI

| Route | Thành phần | Ghi chú |
| --- | --- | --- |
| `/invoices` | `InvoiceListView` | Chủ trọ/Quản lý: KPI, lọc kỳ/hợp đồng/trạng thái, bảng, phân trang |
| `/my-invoices` | `InvoiceListView` | Khách thuê: cùng thành phần, chỉ thấy hợp đồng có tên mình |
| `/invoices/{Id:int}` | `InvoiceDetailView` | Thông tin hóa đơn, các dòng, tổng/đã trả/còn nợ |
| `/my-invoices/{Id:int}` | `InvoiceDetailView` | Dùng chung detail, breadcrumb và nút quay lại theo route |

- Resource VI/EN: `Web/Resources/Features/Invoices/InvoiceResources.{vi-VN,en-US}.resx`, marker `InvoiceResources`.
- Điều hướng sidebar `Invoices` → `/invoices`, `MyInvoices` → `/my-invoices`.
- Tiền dùng `MoneyFormatter`, ngày thuần dùng `DateFormatter`, thời điểm dùng `DateFormatter.FormatDateTime`.

## 4. Kiểm thử

`NhaTro.IntegrationTests/Features/Invoices/InvoiceQueryServiceIntegrationTests.cs` (SQL Server LocalDB thật):

- Chủ trọ A/B chỉ thấy hóa đơn nhà mình.
- Quản lý chỉ thấy nhà được phân công.
- Khách thuê chỉ thấy hợp đồng có tên mình.
- Admin bị từ chối.
- Lọc tháng/năm (giao kỳ), hợp đồng ngoài phạm vi trả rỗng, lọc trạng thái.
- Chi tiết đúng dữ liệu, dòng sắp theo `DisplayOrder`, discount âm.
- Chi tiết ngoài phạm vi/không tồn tại đều ném `KeyNotFoundException`.
- Contract options giới hạn theo phạm vi.
- Unique index `(ContractId, PeriodStart)` chặn bản ghi trùng.

Đã chạy: `dotnet build NhaTro/NhaTro.slnx` (0 lỗi), unit tests 568/568, integration tests 151/151, `dotnet ef migrations has-pending-model-changes` → không còn thay đổi model.

## 5. Việc chuyển tiếp

- VHT-54: thêm bảng `Payments`, thay `PaidAmount` suy diễn bằng tổng payment `Confirmed`; điền `LatestPayment`.
- Các task tạo/phát hành hóa đơn (#35/#36) và xác nhận thanh toán (#38) dùng lại entity/query ở đây.
