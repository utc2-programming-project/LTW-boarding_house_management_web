# VHT-63 — Kiểm thử tiền và transaction P4–P6 (đợt 3A)

> **Phạm vi:** đợt **3A** — dựng và chạy test tự động cho tính tiền, snapshot, trùng kỳ,
> xác nhận/lập–phát hành đồng thời, rollback và sai quyền trên phần service P4–P6 đã có.
> **Không làm:** 3D/3E, migration, nghiệp vụ ngoài D1–D17.
> **Nhánh:** `VHT-63-kiểm-thử-tiền-va-transaction-p-4-p-6`
> **Commit nền:** `cf201b6` (VHT-53 — lập/phát hành hóa đơn từ nguồn thật, được đưa vào nhánh test để chạy).
> **Commit test + sửa lỗi 3A:** `8c99126`.
> **Ngày chạy:** 04/11/2026, database test SQL Server LocalDB riêng cho từng fixture.

## 1. Kết luận nhanh

- Đã bổ sung **6 integration test** cho VHT-63 trên SQL Server thật, phủ: bộ số tính tay,
  snapshot nguồn, trùng kỳ, hai luồng lập cùng kỳ, hai luồng phát hành đồng thời, rollback và sai quyền.
- **Phát hiện 1 lỗi thật** khi hai luồng lập hóa đơn cùng kỳ chạy đồng thời: service ném
  `InvalidOperationException` do detach `Invoice` trước `InvoiceItem`, che mất xử lý trùng kỳ.
  Đã sửa tối thiểu trong `InvoiceService` và retest **PASS**.
- Toàn bộ test hiện có vẫn xanh: **705 unit + 274 integration**, build **0 warning/0 error**.
- **Giới hạn 3A:** backend xác nhận thanh toán VHT-54 chưa có trên `dev`/remote tại thời điểm chạy,
  nên phần “xác nhận thanh toán đồng thời” mới kiểm ở mức rule (`InvoicePaymentRulesTests`);
  test transaction thật cho xác nhận thanh toán sẽ chạy ở 3D khi VHT-54 được ghép.

## 2. Kết quả lệnh chạy

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build NhaTro/NhaTro.slnx --nologo -v minimal` | **0 warning, 0 error** |
| `dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj` | **705 pass, 0 fail** |
| `dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj` | **274 pass, 0 fail** |
| Lọc `FullyQualifiedName~Invoices` (integration) | **28 pass, 0 fail** |
| Lọc `FullyQualifiedName~Invoices` (unit) | **35 pass, 0 fail** |
| `P6MoneyTransactionIntegrationTests` | **6 pass, 0 fail** sau khi sửa BUG-3A-01 |

## 3. Bảng case đợt 3A

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| M-01 | Tính tiền theo bộ số tay: thuê 3.000.000 + điện 250.000 + nước 90.000 + internet 100.000 + rác 30.000 − giảm 50.000 | Tổng 3.420.000; từng dòng đúng | **PASS** | `Tinh_tien_va_snapshot_dung_bo_so_tinh_tay` |
| M-02 | Dòng điện có `Amount` nguồn 250.000 nhưng `Quantity × UnitPrice` = 120.000 | Lưu đúng 250.000, không tính lại | **PASS** | cùng test |
| M-03 | Đổi giá hợp đồng/phòng sau khi lập hóa đơn | Snapshot hóa đơn không đổi | **PASS** | cùng test |
| M-04 | Gửi lặp cùng hợp đồng/kỳ/cùng dữ liệu | Trả lại hóa đơn cũ, không ghi trùng | **PASS** | `Trung_ky_cung_du_lieu_khong_ghi_trung_va_khac_du_lieu_bi_chan` |
| M-05 | Cùng hợp đồng/kỳ nhưng dữ liệu khác | `DuplicateInvoice`, DB vẫn 1 hóa đơn | **PASS** | cùng test |
| M-06 | Hai luồng lập cùng hợp đồng/kỳ đồng thời | Chỉ 1 hóa đơn + 1 bộ dòng | **PASS sau sửa BUG-3A-01** | `Hai_luong_lap_cung_ky_dong_thoi_chi_ghi_mot_hoa_don` |
| M-07 | Hai luồng phát hành cùng hóa đơn đồng thời | Cả hai trả `Issued`, chỉ đổi trạng thái một lần, không tạo bản ghi mới | **PASS** | `Hai_luong_phat_hanh_dong_thoi_chi_doi_trang_thai_mot_lan` |
| M-08 | Phát hành lỗi sau khi đã tạo nháp trong `CreateAndIssueAsync` | Rollback cả hóa đơn và dòng | **PASS** | `Rollback_khi_phat_hanh_loi_khong_de_lai_hoa_don_nhap` |
| M-09 | Chủ trọ khác, Khách thuê, Admin lập/phát hành | Bị chặn, hóa đơn vẫn `Draft`, không ghi thêm | **PASS** | `Sai_quyen_khong_lap_va_khong_phat_hanh_duoc` |
| M-10 | Rule gửi/xác nhận thanh toán: không trả một phần, chỉ cộng `Confirmed`, từ chối minh chứng | Đúng D13 | **PASS (unit)** | `InvoicePaymentRulesTests` (35 test lọc `Invoices`) |
| D1–D17 | Duyệt trùng, duyệt khi dữ liệu đã đổi, Chủ trọ bỏ qua công tắc | Bị chặn/đi đúng quyết định | **PASS (đã có)** | `ManagerApprovalServiceIntegrationTests`, `ManagerToggleServiceIntegrationTests` |

## 4. Lỗi phát hiện và đã sửa

### BUG-3A-01 — [Cao] Lập trùng kỳ đồng thời làm lộ lỗi EF, không trả kết quả idempotent

**Vị trí:** `NhaTro/Web/Features/Invoices/Services/InvoiceService.cs`, nhánh `catch (DbUpdateException)`.

**Tái hiện:** hai `DbContext` cùng gọi `CreateDraftAsync` cho cùng hợp đồng/kỳ/dữ liệu.
Một luồng ghi trước; luồng còn lại vi phạm unique index `(ContractId, PeriodStart)`.

**Hành vi sai:** trước khi kiểm hóa đơn đã có, service đặt `Invoice` sang `Detached` khi các
`InvoiceItem` còn tracked. EF cố xóa FK bắt buộc và ném
`InvalidOperationException: The association between entity types 'Invoice' and 'InvoiceItem' has been severed...`
thay vì trả lại hóa đơn đã có.

**Đã sửa:** detach từng `InvoiceItem` (qua `ToList()` để tránh sửa collection khi duyệt) rồi mới detach `Invoice`.
Sau đó service kiểm lại bản ghi trùng và trả hóa đơn cũ như thiết kế.

**Retest:** `Hai_luong_lap_cung_ky_dong_thoi_chi_ghi_mot_hoa_don` — **PASS**;
DB còn đúng 1 hóa đơn, 6 dòng, tổng 3.420.000.

## 5. File thay đổi trong nhánh

| File | Việc |
| --- | --- |
| `NhaTro/NhaTro.IntegrationTests/Features/Invoices/P6MoneyTransactionIntegrationTests.cs` | 6 integration test VHT-63 đợt 3A trên SQL Server thật |
| `NhaTro/Web/Features/Invoices/Services/InvoiceService.cs` | Sửa BUG-3A-01: detach dòng trước hóa đơn khi gặp `DbUpdateException` |
| `docs/Imp/VHT-63-kiem-thu-tien-va-transaction-p4-p6-3A.md` | Tài liệu case, kết quả và lỗi của đợt 3A |

Không tạo migration; không thay đổi nghiệp vụ D1–D17.

## 6. Điều kiện sang 3D

- Ghép VHT-54 (yêu cầu/xác nhận thanh toán) rồi bổ sung test transaction thật cho:
  xác nhận đồng thời, chống cộng tiền hai lần, rollback khi xác nhận lỗi.
- Chạy lại `P6MoneyTransactionIntegrationTests` trên commit tích hợp 3D và ghi kết quả theo commit.
