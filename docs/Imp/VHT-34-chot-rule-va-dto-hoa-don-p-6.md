# VHT-34 — Chốt rule và DTO hóa đơn P6

> **Nhánh:** `VHT-34-chot-rule-va-dto-hoa-don-p-6`, tạo từ `dev` tại `aa25bcf`.
> **Đợt/phân hệ:** Sprint 2A, P6 — Hóa đơn và thanh toán.
> **Phạm vi:** chỉ chốt kỳ/làm tròn, DTO hóa đơn/thanh toán, trạng thái và rule tính tổng.
> **Không làm:** entity, migration, service, endpoint, UI, phát hành hóa đơn, xác nhận tiền thật.
> **Nguồn đối chiếu:** VHT-9 (nhánh remote `Chốt-trạng-thái-kỳ-và-dữ-liệu-P4/P5`), VHT-14 (nhánh `VHT-14-chuan-bi-kiem-thu-va-case-p-6`), D13, D17, `docs/quy_uoc_ngay_tien_gio.md`.

## 1. Quyết định đã chốt

| Nội dung | Quyết định | Nguồn |
| --- | --- | --- |
| Kỳ hóa đơn | Kỳ là khoảng ngày `[PeriodStart, PeriodEnd]`, tính cả hai đầu, do P5 chốt; P6 không tự tính tỷ lệ ngày cho kỳ lẻ | VHT-9 `BillingPeriodDto.PeriodStart/PeriodEnd` |
| Kỳ tháng đủ | `PeriodStart` = ngày 1, `PeriodEnd` = ngày cuối tháng; helper ở `InvoicePeriodRules` | `docs/quy_uoc_ngay_tien_gio.md` |
| Làm tròn | Làm tròn **từng dòng** về số nguyên VND, đúng `.5` làm tròn lên, rồi mới cộng | Quy ước ngày/tiền; VHT-9 mục 7 |
| Nguồn thành tiền | Ưu tiên `Amount` đã snapshot từ P4/P5; nếu null mới tính `Quantity × UnitPrice` | VHT-9 `CostLineDto.Amount` |
| Điện theo bậc | P5 có thể gửi từng bậc thành dòng, hoặc gửi một `Amount` đã chốt; P6 không tính lại bậc | VHT-9 ví dụ điện 120 kWh = 250.000 |
| Giảm giá | Có dòng `Discount`; số tiền nguồn nhập dương, rule đổi dấu âm khi cộng | VHT-14 `P6-MONEY-OK` |
| Tổng hóa đơn | `TotalAmount = Σ Amount` sau khi làm tròn từng dòng | `InvoiceCalculationRules` |
| Thanh toán một phần | **Không** hỗ trợ; số tiền gửi lên phải bằng đúng tổng hóa đơn | D13 |
| Gửi yêu cầu | `PaymentStatus.Pending` chỉ là chờ đối chiếu, **không** phải tiền đã thu | P6; D13 |
| Tiền đã thu | Chỉ cộng các `PaymentStatus.Confirmed`; `Pending`/`Rejected` không cộng | `InvoicePaymentRules` |
| Từ chối minh chứng | Chỉ từ chối yêu cầu `Pending`; khách được gửi yêu cầu mới | D13 |
| Hủy hóa đơn | Không có trạng thái hủy trong v1.0 | D13 |
| Minh chứng | Tạm theo đề xuất D17: ảnh/PDF, bắt buộc số tiền và thời điểm chuyển, mã giao dịch tùy chọn; **D17 vẫn còn `❓`** | D17 (chưa chốt) |
| Admin | Không có quyền nghiệp vụ P6; không thêm quyền trong task này | D1; D9 |

### Trạng thái

```text
InvoiceStatus:  Draft ──phát hành──> Issued ──xác nhận đủ tiền──> Paid
PaymentStatus:  Pending ──xác nhận──> Confirmed
                     └──từ chối────> Rejected ──khách gửi lại──> Pending (bản ghi mới)
```

- `Draft`: được sửa dòng hóa đơn.
- `Issued`: đã phát hành, còn công nợ; gửi yêu cầu thanh toán không đổi trạng thái này.
- `Paid`: đã xác nhận thu đủ; trạng thái cuối.
- Không có `Cancelled`/`PartiallyPaid`.

## 2. DTO và rule đã thêm

| File | Nội dung |
| --- | --- |
| `Web/Features/Invoices/Enums/InvoiceStatus.cs` | `Draft`, `Issued`, `Paid`; không có hủy |
| `Web/Features/Invoices/Enums/InvoiceItemType.cs` | `Rent`, `Electric`, `Water`, `Service`, `Discount` |
| `Web/Features/Invoices/Enums/PaymentStatus.cs` | `Pending`, `Confirmed`, `Rejected` |
| `Web/Features/Invoices/DTOs/InvoiceItemInputDto.cs` | Dòng đầu vào: loại, mô tả, đơn vị, số lượng, đơn giá, `Amount` snapshot tùy chọn |
| `Web/Features/Invoices/DTOs/InvoiceItemDto.cs` | Dòng đã chốt, `Amount` đã làm tròn; discount lưu âm |
| `Web/Features/Invoices/DTOs/InvoiceDto.cs` | Hóa đơn: kỳ start/end, trạng thái, dòng, tổng, đã thu, còn nợ, yêu cầu mới nhất |
| `Web/Features/Invoices/DTOs/PaymentDto.cs` | Lần gửi thanh toán: số tiền, thời điểm chuyển, mã giao dịch, minh chứng, trạng thái |
| `Web/Features/Invoices/DTOs/SubmitPaymentRequest.cs` | Dữ liệu khách gửi: số tiền, thời điểm chuyển, mã giao dịch, ID minh chứng |
| `Web/Features/Invoices/Rules/InvoiceCalculationRules.cs` | `CalculateItemAmount`, `ToItem`, `CalculateTotal`, `TotalOf` |
| `Web/Features/Invoices/Rules/InvoicePeriodRules.cs` | Khoảng kỳ, kỳ tháng, contains, next/previous, số ngày |
| `Web/Features/Invoices/Rules/InvoicePaymentRules.cs` | Chuyển trạng thái, chặn trả một phần, cộng tiền đã xác nhận, công nợ |

### DTO hóa đơn

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `Id` | `int` | ID hóa đơn |
| `ContractId` | `int` | Hợp đồng nguồn |
| `PeriodStart` | `DateOnly` | Ngày đầu kỳ, tính cả |
| `PeriodEnd` | `DateOnly` | Ngày cuối kỳ, tính cả |
| `Status` | `InvoiceStatus` | `Draft`/`Issued`/`Paid` |
| `Items` | `IReadOnlyList<InvoiceItemDto>` | Các dòng đã chốt |
| `TotalAmount` | `decimal` | Tổng đã cộng theo dòng |
| `PaidAmount` | `decimal` | Chỉ Payment `Confirmed` |
| `OutstandingAmount` | `decimal` | `TotalAmount - PaidAmount`, không âm |
| `LatestPayment` | `PaymentDto?` | Hiển thị chờ/đã từ chối; không dùng để tính tiền |

### DTO dòng hóa đơn

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `ItemType` | `InvoiceItemType` | `Rent`/`Electric`/`Water`/`Service`/`Discount` |
| `Description` | `string` | Tối đa 250 ký tự |
| `Unit` | `string?` | kWh, m³, tháng, người… |
| `Quantity` | `decimal` | Dùng khi không có `Amount` |
| `UnitPrice` | `decimal` | Dùng khi không có `Amount` |
| `Amount` | `decimal?` | Số tiền nguồn P4/P5 đã snapshot, trước làm tròn; null thì tính `Quantity × UnitPrice` |
| `Amount` (output) | `decimal` | Đã làm tròn; dòng `Discount` là số âm |

### DTO thanh toán

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `Id` | `int` | ID lần gửi |
| `InvoiceId` | `int` | Hóa đơn được trả |
| `Amount` | `decimal` | Phải bằng đúng tổng hóa đơn |
| `TransferredAt` | `DateTimeOffset` | Thời điểm khách báo đã chuyển, lưu UTC |
| `TransactionCode` | `string?` | Tùy chọn (D17 còn `❓`) |
| `Status` | `PaymentStatus` | `Pending`/`Confirmed`/`Rejected` |
| `ProofFileIds` | `IReadOnlyList<Guid>` | Tối thiểu một tệp ảnh/PDF |
| `RequestedAt` | `DateTimeOffset` | Hệ thống nhận yêu cầu, UTC |
| `ReviewedAt` | `DateTimeOffset?` | Khi xác nhận/từ chối |
| `RejectionReason` | `string?` | Có khi `Rejected` |

## 3. Ví dụ tính tiền

### 3.1. Mẫu đối chiếu VHT-9

Kỳ tháng đủ, phòng P101:

| Dòng | Loại | Số lượng | Đơn giá | Thành tiền đã làm tròn |
| --- | --- | ---: | ---: | ---: |
| Tiền thuê phòng P101 | `Rent` | 1 tháng | 3.000.000 | 3.000.000 |
| Điện bậc 1 | `Electric` | 50 kWh | 1.800 | 90.000 |
| Điện bậc 2 | `Electric` | 50 kWh | 2.000 | 100.000 |
| Điện bậc 3 | `Electric` | 20 kWh | 3.000 | 60.000 |
| Nước kỳ 10/2026 | `Water` | 6 m³ | 15.000 | 90.000 |
| Internet | `Service` | 1 tháng | 100.000 | 100.000 |
| Rác | `Service` | 1 tháng | 30.000 | 30.000 |
| **Tổng** | | | | **3.470.000** |

Tính tay: `3.000.000 + 90.000 + 100.000 + 60.000 + 90.000 + 100.000 + 30.000 = 3.470.000`.
`InvoiceCalculationRules.CalculateTotal` trả đúng `3.470.000`.

Nếu P5 gửi điện đã chốt theo bậc thành một số tiền `Amount = 250.000` (không tách đơn giá),
rule dùng `Amount` đó và vẫn ra `3.470.000`.

### 3.2. Mẫu VHT-14 có giảm giá

| Dòng | Loại | Số tiền nguồn | Thành tiền |
| --- | --- | ---: | ---: |
| Tiền thuê | `Rent` | 3.000.000 | 3.000.000 |
| Điện | `Electric` | 700.000 | 700.000 |
| Nước | `Water` | 120.000 | 120.000 |
| Dịch vụ | `Service` | 100.000 | 100.000 |
| Giảm giá | `Discount` | 50.000 | −50.000 |
| **Tổng** | | | **3.870.000** |

Tính tay: `3.000.000 + 700.000 + 120.000 + 100.000 − 50.000 = 3.870.000`.

### 3.3. Làm tròn từng dòng

Hai dòng `10,5 × 1 = 10,5` phải thành `11 + 11 = 22`, không lấy tổng thô `21` rồi làm tròn.
Quy tắc: `MoneyFormatter.RoundVnd` (đúng `.5` làm tròn lên), gọi trên từng dòng trước khi cộng.

## 4. Ví dụ thanh toán

- Tổng hóa đơn: `3.870.000`.
- Khách gửi `Pending` đúng `3.870.000`: `PaidAmount = 0`, `OutstandingAmount = 3.870.000`.
- Quản lý/Chủ trọ xác nhận: Payment chuyển `Confirmed`; `PaidAmount = 3.870.000`, `OutstandingAmount = 0`.
- Khách gửi `3.869.999` hoặc `3.870.001`: bị chặn bởi `CanSubmitPayment`.
- Minh chứng bị từ chối: Payment `Rejected`, không tính tiền; khách gửi bản ghi `Pending` mới.

## 5. Kiểm thử

Đã thêm test thuần (không DB/HTTP):

| File | Nội dung |
| --- | --- |
| `NhaTro.UnitTests/Features/Invoices/InvoiceCalculationRulesTests.cs` | Mẫu VHT-9, mẫu VHT-14 có giảm giá, snapshot P5, làm tròn từng dòng, .5 lên, tổng rỗng |
| `NhaTro.UnitTests/Features/Invoices/InvoicePeriodRulesTests.cs` | Kỳ tháng, kỳ lẻ, nửa mở, tháng 2 nhuận, qua năm |
| `NhaTro.UnitTests/Features/Invoices/InvoicePaymentRulesTests.cs` | Pending không phải tiền thu, chỉ Confirmed được cộng, chặn một phần/trả thừa, từ chối, không hủy |
| `NhaTro.UnitTests/Features/Invoices/SubmitPaymentRequestTests.cs` | DTO yêu cầu thanh toán: số tiền > 0, bắt buộc minh chứng |

Kết quả:

```text
dotnet build NhaTro/NhaTro.slnx --nologo
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test NhaTro/NhaTro.slnx --nologo
Unit:        Passed! Failed: 0, Passed: 323, Skipped: 0
Integration: Passed! Failed: 0, Passed: 42,  Skipped: 0
E2E:         Passed! Failed: 0, Passed: 1,   Skipped: 0
```

## 6. Giới hạn và việc chuyển tiếp

- Chưa có `Invoice`/`InvoiceItem`/`Payment` entity, migration, service, endpoint hoặc UI. Các phần đó thuộc VHT-35 và VHT-54.
- Chưa phát hành hóa đơn từ dữ liệu mẫu; mẫu chỉ dùng trong unit test.
- Chưa chốt hạn thanh toán (`DueDate`) nên DTO chưa có trường này; không tự suy luận khi chưa có quyết định.
- Kỳ lẻ được truyền nguyên `PeriodStart/PeriodEnd` từ P5; P6 không tự chia tỷ lệ ngày. Nếu nhóm muốn prorate thì phải chốt ở VHT-9/VHT-32 trước.
- DTO chưa chứa quy tắc quyền. Việc chặn Admin, giới hạn Quản lý theo tòa nhà và D9 do VHT-24/VHT-6 xử lý ở service/endpoint.
- D17 vẫn còn `❓`; DTO tạm giữ `ProofFileIds` theo đề xuất ảnh/PDF, số tiền/thời điểm bắt buộc, mã giao dịch tùy chọn. Việc kiểm đuôi/dung lượng thật do storage (VHT-13) thực hiện; khi nhóm chốt D17 phải rà lại DTO này.
- Khi VHT-9/VHT-32 có DTO chi phí thật, service VHT-53/VHT-54 map sang `InvoiceItemInputDto` và dùng đúng rule trong task này; không tính lại bằng công thức khác.

## 7. Ràng buộc đề xuất cho VHT-35 (Tính review khi làm migration)

| Đối tượng | Ràng buộc | Mức |
| --- | --- | --- |
| `Invoice` | `PeriodStart <= PeriodEnd` | DB check |
| `Invoice` | Unique `(ContractId, PeriodStart)` — một hợp đồng một kỳ một hóa đơn | DB unique |
| `Invoice` | `TotalAmount >= 0`; service kiểm `TotalAmount = Σ Item.Amount` | DB check + service |
| `Invoice` | `Status` chỉ `Draft/Issued/Paid`; không có `Cancelled` | DB check |
| `InvoiceItem` | `Quantity > 0`, `UnitPrice >= 0` | DB check |
| `InvoiceItem` | `Amount` chỉ âm khi `ItemType = Discount`; còn lại `>= 0` | DB check |
| `Payment` | `Amount > 0`; service kiểm `Amount = Invoice.TotalAmount` và không trả một phần | DB check + service |
| `Payment` | Tối đa một `Confirmed` cho mỗi hóa đơn; `Pending/Rejected` không cộng tiền | DB filtered unique + service |
| `Payment` | `ReviewedAt` null khi `Pending`; có giá trị khi `Confirmed/Rejected` | Service |
| `PaymentProof` | Mỗi Payment có ít nhất một tệp ảnh/PDF; kiểm loại/dung lượng qua storage | Service + VHT-13 |
