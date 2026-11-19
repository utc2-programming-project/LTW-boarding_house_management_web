# SPEC — Bộ dữ liệu demo NhaTro (schemaVersion 3)

> Nguồn sự thật: generator `NhaTro/tools/demo-seed/generate-demo-data.mjs` → `NhaTro/Web/Data/Seeding/demo-data.json`
> (gitignored). Seeder C# chỉ đọc file, không chứa dữ liệu demo.
> Trạng thái: đã sinh 60,1 MB và `validate.mjs` đạt **665.718 điều kiện**; **chưa nạp DB**
> (người dùng tự chạy `--clear-demo` / `--seed-demo`).

## 0. Quyết định đã chốt

| # | Quyết định | Giá trị |
|---|---|---|
| D1 | Quy mô | 4 chủ trọ × 10 quản lý × 10 nhà = **400 nhà**; 8–10 phòng/nhà; 1–4 người/phòng |
| D2 | Đan xen | Người ở được nhiều trọ; chủ trọ/quản lý cũng đi thuê; mọi khách đều có tài khoản |
| D3 | Cách sinh | Script Node trong repo, output JSON gitignored |
| D4 | Ảnh chuyển khoản | 1 template PNG dùng chung; thay ảnh thật bằng `--proof-image <file>` |
| D5 | DB đích | `NhaTro_Demo_3A` (Development) |
| D6 | Kiểu seed | Ảnh chụp đầy đủ: DB còn dữ liệu (kể cả `AspNetUsers`) ⇒ từ chối, phải `--clear-demo` |
| D7 | Điểm vào | `dotnet run --project NhaTro/Web -- --seed-demo` / `--clear-demo` |
| D8 | Tệp | base64 nằm trong `fileTemplates`; seeder ghi **8 tệp vật lý** dùng chung cho 18.163 bản ghi |
| D9 | Mật khẩu | `Demo@123456` (cờ `DemoSeed:Password`) |
| D10 | Mốc thời gian | `seedNow = 2026-11-13T02:00:00`; chỉ số 08–10/2026, kỳ 11/2026 đang mở |
| D11 | Tái lập | PRNG `mulberry32` seed `20261113` ⇒ chạy lại cho đúng cùng dữ liệu |

## 1. Quy ước chung

| Mục | Quy ước |
|---|---|
| Email | `admin01..02`, `chutro01..04`, `quanly001..040`, `khachthue0001..6550` |
| UserId | GUID cố định `d0000000-0000-4000-8000-{index:D12}`; index nằm trong email |
| Hợp đồng | GUID `c0000000-…`; số `HD-` + GUID dạng `N` in hoa |
| Tệp | GUID `f0000000-…` |
| Thời gian | `DateTime` không offset; `DateTimeOffset` `+07:00`; `DateOnly` `YYYY-MM-DD` |
| Tiền | VND nguyên; cọc = 20% `standardPrice`; tổng hoá đơn = tổng dòng |
| RowVersion | Không có trong JSON; SQL Server tự sinh |
| Xoá mềm | 2 tài khoản, 2 nhà, 30 phòng, 6 hợp đồng, 6 hoá đơn, 5 hồ sơ khách, 6 sự cố, 2 dịch vụ, 10 tệp |

### 1.1 Cấu trúc JSON

```json
{
  "schemaVersion": 3,
  "seedName": "nhatro-demo-v3",
  "seedNow": "2026-11-13T02:00:00",
  "fileTemplates": { "paymentProof": "<base64 PNG>", "contractPdf": "<base64 PDF>" },
  "accounts": [], "tenants": [], "properties": [], "rooms": [],
  "files": [ { "file": { "id": "f0000000-…", "relativePath": "demo/templates/phong-a.png" }, "templateKey": "roomPhotoA" } ]
}
```

- `files[]` là cặp `{ file, templateKey }`: metadata `StoredFile` + khoá nội dung dùng chung.
- Thứ tự chèn: accounts → tenants/owner links → properties/rooms/managers → invitations →
  assignments → contracts/members/versions/acceptances/change requests/renewals →
  catalogs/prices/registrations → readings/audits/periods/snapshots → invoices/items/revisions →
  payments/proofs/audits → incidents/history/photos → approvals/otp → files → notifications/audits.
- Xoá `demo-data.json` = tắt seed; seeder log “not found; skip” và thoát 0.

## 2. Quy mô thực tế

| Nhóm | Bảng | SL |
|---|---|---|
| Danh tính | `AspNetUsers` / `Tenants` / `TenantOwnerLinks` | 6.582 / 6.550 / 7.489 |
| Nhà trọ | `Properties` / `Rooms` / `PropertyManagers` | 400 / 3.590 / 412 |
| Hợp đồng | `Contracts` / `ContractMembers` / `RenewalRequests` | 3.084 / 7.808 / 400 |
| Ký điện tử | `ContractVersions` / `ContractAcceptances` / `ContractChangeRequests` | 186 / 120 / 54 |
| Lời mời | `TenantInvitations` / `PendingRoomAssignments` | 397 / 60 |
| Dịch vụ | `ServiceCatalogs` / `PriceHistories` / `Registrations` / `Requests` | 10 / 14 / 6.107 / 240 |
| Điện nước | `UtilityReadings` / `Audits` / `ReadingPeriods` / `BillingPeriods` / `CostLines` / `ServiceSnapshots` | 8.346 / 200 / 11.128 / 16.692 / 39.597 / 16.692 |
| Hoá đơn | `Invoices` / `Items` / `Revisions` | 8.370 / 47.680 / 103 |
| Thanh toán | `Payments` / `Proofs` / `AuditLogs` | 7.602 / 7.602 / 13.947 |
| Sự cố | `Incidents` / `History` / `Photos` | 826 / 1.749 / 1.830 |
| Duyệt | `ManagerApprovalRequests` / `OtpChallenges` | 130 / 2 |
| Tệp & log | `StoredFiles` / `FileAccessLogs` / `Notifications` / `AuditLogs` | 18.163 / 400 / 500 / 240 |

Tổng tệp vật lý trên đĩa: **8** (`fileTemplates`), đường dẫn `demo/templates/*.png|pdf`.

## 3. Tài khoản

| Nhóm | SL | Vai trò | Ghi chú |
|---|---|---|---|
| Admin | 2 | Admin | `admin01` kiêm khách thuê |
| Chủ trọ | 4 | ChuTro + KhachThue | Mỗi người 100 nhà; đều đang thuê ở trọ khác |
| Quản lý | 40 | 30 QuanLy + 10 QuanLy+KhachThue | 10 người/chủ; mỗi người 10 nhà |
| Khách thuê | 6.536 | KhachThue | Dải email 0001–6550 (14 số thuộc chủ/quản lý) |
| **Tổng** | **6.582** | | |

| Tài khoản | Trạng thái | Mục đích demo |
|---|---|---|
| `quanly039@demo.vn` | Chờ duyệt (`approvedAt = null`) | Admin duyệt tài khoản |
| `quanly040@demo.vn` | Khoá đến 2027-11-13 | Đăng nhập bị chặn |
| `khachthue0001@demo.vn` | Chưa xác thực email | Gửi lại OTP/xác thực |
| `khachthue6546/6547` | Đã xoá mềm | Thùng rác tài khoản |

- Ngân hàng mặc định (QR VietQR): `chutro01..03` = mã `TECHCOMBANK` (BIN `970407`) / `2145181026` /
  `CAO TRUNG TINH`; `chutro04` **không có** STK (case “chưa nhập”). Cột `bankCode` lưu **mã ngân hàng**,
  không lưu BIN; BIN chỉ dùng khi dựng payload EMV.
- Đăng nhập demo: `chutro01@demo.vn`, `quanly001@demo.vn`, `khachthue0001@demo.vn` / `Demo@123456`.

## 4. Cây nhà trọ

| Chủ trọ | Property Id | Tên | Phòng |
|---|---|---|---|
| chutro01 | 1–100 | Bình Thạnh 001–100 | 8–10/nhà |
| chutro02 | 101–200 | Quận 7 001–100 | 8–10/nhà |
| chutro03 | 201–300 | Thủ Đức 001–100 | 8–10/nhà |
| chutro04 | 301–400 | Gò Vấp 001–100 | 8–10/nhà; 2 nhà đã xoá, 1 nhà ngưng |

- 400 nhà (398 sống): Id 385/386 trong thùng rác, Id 393 `isActive = false`.
- 3.590 phòng (3.560 sống): **2.782 Rented**, **611 Available** (597 trong nhà đang hoạt động),
  **167 Maintenance**, 30 xoá mềm.
- 2.782 phòng thuê ↔ 2.788 hợp đồng Approved: 2.782 hợp đồng hiện hành + **6 hợp đồng gia hạn
  kế nhiệm** bắt đầu ngay sau hợp đồng cũ (trạng thái hiển thị `Upcoming`). Không hai hợp đồng
  Approved nào chồng lấn ngày trên cùng phòng.
- 30 phòng trống mang hợp đồng Pending/Rejected, 30 phòng trống khác mang hợp đồng ở luồng ký:
  10 Draft, 12 AwaitingTenant, 8 ChangeRequested (phòng vẫn `Available` vì chưa nhận phòng).
- `PropertyManagers`: 412 dòng (400 active — 1 người/nhà; 12 đã thu hồi).
- Mỗi nhà còn ít nhất 1 phòng trống (kiểm tra C01 của validator).

## 5. Ca biên có mẫu (validator bắt buộc > 0)
| Mã | Ca | SL |
|---|---|---|
| C01 | Phòng trống trong nhà đang hoạt động | 597 |
| C02 | Phòng bảo trì | 167 |
| C03 | Hợp đồng chờ duyệt / bị từ chối | 20 / 16 |
| C03b | Hợp đồng Draft / AwaitingTenant / ChangeRequested | 10 / 12 / 8 |
| C04 | Hoá đơn Nháp / Đã phát hành / Đã thu | 1.261 / 2.802 / 4.301 |
| C05 | Thanh toán Chờ duyệt / Bị từ chối | 1.257 / 2.044 |
| C06 | Hoá đơn đã phát hành **chưa nhập chuyển khoản** | 840 |
| C07 | Sự cố Mới / Đang xử lý / Hoàn thành | 234 / 249 / 337 |
| C08 | Yêu cầu duyệt Chờ / Từ chối | 60 / 14 |
| C09 | Kỳ đọc chưa nhập số / đã nhập số (11/2026) | 973 / 1.809 |
| C10 | Hoá đơn đã chỉnh sửa (revision) | 103 |
| C11 | Khách thiếu CCCD (chưa cung cấp) | 1.309 |
| C12 | Chủ trọ chưa có tài khoản ngân hàng | 1 |
| C13 | Thùng rác: nhà / phòng / khách / HĐ / hoá đơn / sự cố / dịch vụ / tài khoản / tệp | 2 / 30 / 5 / 6 / 6 / 6 / 2 / 2 / 10 |
| C14 | Ở ghép 1–4 người/HĐ (HĐ Approved) | 260 / 836 / 1.012 / 680 |
| C15 | Người thuê ≥ 2 phòng đang hiệu lực | 1.155 |
| C16 | Hồ sơ khách thuộc 1 / 2 / 3 chủ trọ | 5.675 / 860 / 15 |
| C17 | Lời mời Accepted / Pending / Expired / Revoked | 100 / 99 / 99 / 99 |
| C18 | Nhận phòng Pending / Completed / Cancelled | 20 / 20 / 20 |
| C19 | Gia hạn HĐ Pending / Approved / Rejected | 150 / 125 / 125 |
| C20 | Yêu cầu duyệt đang chờ (60): SW-06/07/08/09/10/11/12/13/14/16 | 5–8 mỗi mã |
| C21 | Hợp đồng có phiên bản gửi khách / khách đã xác nhận | 186 / 120 |
| C22 | Hợp đồng khách xin sửa điều khoản (8 chờ / 40 xong / 6 bỏ qua) | 54 |
| C23 | Hợp đồng gia hạn kế nhiệm (Upcoming) / sắp hết hạn (≤30 ngày) | 6 / 296 |

Thùng rác đúng nghĩa: bản ghi còn nguyên trong DB với `isDeleted = true`; seeder không xoá vật lý.

## 6. Quy ước nghiệp vụ

| Chủ đề | Luật đã áp |
|---|---|
| Hợp đồng | 1 năm; 2.788 Approved (2.782 hiện hành + 6 kế nhiệm) / 20 Pending / 16 Rejected / 230 Liquidated / 10 Draft / 12 AwaitingTenant / 8 ChangeRequested; 6 xoá mềm |
| Phòng ↔ HĐ | Rented ⇔ có ≥1 HĐ Approved, và không hai HĐ Approved chồng lấn ngày (khớp trigger `TR_Contracts_PreventApprovedOverlap`); HĐ Pending/Rejected/Draft/Awaiting/ChangeRequested để phòng Available |
| Người tạo HĐ | `CreatedByRole` = `ChuTro` (~35%) hoặc `QuanLy`; `CreatedAt` luôn có, khớp `CreatedByUserId` |
| Ký điện tử | 186 `ContractVersions` (120 HĐ đã duyệt có v1, 40 trong đó có v2 sau khi sửa; 12 AwaitingTenant + 8 ChangeRequested gửi v1; 6 Draft từng gửi rồi thu hồi); `ContentHash` = SHA-256 Base64 của đúng `SnapshotJson` |
| Khách xác nhận | 120 `ContractAcceptances`, mỗi bản ghi là người đại diện, `SignatureMethod = "Otp"`, `ContentHash` = hash của phiên bản được chấp nhận |
| Yêu cầu sửa | 54 `ContractChangeRequests` trên phiên bản đã gửi; Pending ⇒ chưa có `ResolvedAt/ResolvedByUserId` |
| Hoá đơn | Kỳ 7–10/2026; 18 hoá đơn dịch vụ có `registrationId` (chỉ HĐ còn hạn tới 30/11/2026) để không đụng unique `(contractId, periodStart)` |
| Tiền | `Invoice.Total = Σ Items.Amount`; Discount lưu âm; `Payment.Amount = Invoice.Total` |
| Thanh toán | Paid ⇔ đúng 1 Payment Confirmed; mỗi Payment có đúng 1 proof `PaymentProof` |
| Proof | `FileId` duy nhất, `RelatedEntityType = Payment`, uploader là người thuê |
| Chỉ số | 2.782 phòng × 3 kỳ (08–10/2026) = 8.346 reading; snapshot điện 6 bậc, nước 3 bậc theo số người |
| Kỳ đọc | 11.128 kỳ: 8.346 Invoiced + 2.782 Open (tháng 11, 973 chưa nhập số) |
| Revision | 103 hoá đơn có điều chỉnh; chênh lệch nằm trên dòng điện, không làm dòng âm |
| Sự cố | 826 (6 xoá mềm); ảnh Report/Result gắn `relatedEntityId` |
| Duyệt | 130 yêu cầu: 60 chờ, 56 duyệt, 14 từ chối; SW-07 Pending payload `action = Renew` + `endDate` sau hạn hiện tại; SW-08 Pending `action = UpdateDeposit`, `expectedDepositAmount` = cọc hiện tại; không trùng `(operationCode, targetKey)` khi Pending |

## 7. Tệp, ảnh, thùng rác

- 18.163 `StoredFiles`: PaymentProof 7.602 · IdentityDocument 5.236 · Contract 2.788 ·
  IncidentPhoto 1.821 · RoomPhoto 407 · Invoice 300 · IncidentApproval 9.
- 10 tệp `isDeleted` (thùng rác tệp); `FileAccessAuditLogs` 400 dòng, 24 dòng `succeeded = false`.
- Ảnh chuyển khoản mặc định là PNG sinh sẵn có chữ “minh chứng chuyển khoản”.
  Muốn dùng ảnh thật (không được commit):

```text
node NhaTro/tools/demo-seed/generate-demo-data.mjs --proof-image "D:\anh-ck.png"
```

## 8. Sinh, nạp, xoá

```text
node NhaTro/tools/demo-seed/generate-demo-data.mjs        # sinh JSON
node NhaTro/tools/demo-seed/validate.mjs                  # kiểm tra, không cần DB
```

```text
dotnet run --project NhaTro/Web -- --clear-demo   # xoá DB demo
dotnet run --project NhaTro/Web -- --seed-demo    # nạp demo-data.json
```

- `--clear-demo` chỉ chạy khi tên DB chứa `Demo`; xoá cả tệp demo trong storage.
- Seeder kiểm tra toàn bộ FK/tổng tiền/unique **trước khi commit**; sai ⇒ rollback.
- Tài khoản chèn bulk (`AddRange` + hash song song), `IDENTITY_INSERT` bật riêng từng bảng.
- Ô `ExpectedToken` của yêu cầu chờ duyệt đọc rowversion thật sau khi chèn (JSON để `null`).
- Marker `AuditLogEntry(DemoSeed.Completed)` chặn seed lặp; nay có cả `AspNetUsers` trong
  danh sách bảng phải trống.

## 9. Kiểm chứng sau seed

```sql
SELECT COUNT(*) FROM AspNetUsers;                -- 6582
SELECT COUNT(*) FROM Tenants;                    -- 6550
SELECT COUNT(*) FROM Rooms WHERE Status = 1;     -- 2782
SELECT COUNT(*) FROM Contracts WHERE Status = 1; -- 2788 (6 hợp đồng gia hạn kế nhiệm)
SELECT COUNT(*) FROM ContractVersions;           -- 186
SELECT COUNT(*) FROM ContractAcceptances;        -- 120
SELECT COUNT(*) FROM ContractChangeRequests;     -- 54
SELECT COUNT(*) FROM Invoices i WHERE i.TotalAmount <>
  (SELECT SUM(Amount) FROM InvoiceItems x WHERE x.InvoiceId = i.Id);          -- 0
SELECT COUNT(*) FROM Payments p JOIN Invoices i ON i.Id = p.InvoiceId
  WHERE p.Amount <> i.TotalAmount;                                            -- 0
SELECT COUNT(*) FROM Invoices i WHERE i.Status = 2 AND
  (SELECT COUNT(*) FROM Payments p WHERE p.InvoiceId = i.Id AND p.Status = 1) <> 1; -- 0
SELECT COUNT(*) FROM UtilityBillingPeriodSnapshots s
  JOIN UtilityReadingPeriods p ON p.Id = s.BillingPeriodId;                   -- 0
```

`ContractVersions.ContentHash` là SHA-256 **Base64** của `SnapshotJson`; kiểm bằng
`node NhaTro/tools/demo-seed/validate.mjs` (không so bằng `HASHBYTES` vì trả hex).

Kiểm UI nhanh: `chutro01` mở Dashboard/Phòng/Hợp đồng/Hoá đơn/Duyệt/Sự cố/Báo cáo/Thùng rác;
`quanly001` mở trang quản lý 10 nhà; `khachthue0001` mở `/my/invoices` và gửi minh chứng; `admin01`
mở `/admin/users` (thấy Chờ duyệt/Khoá/Chưa xác thực). Riêng tab hợp đồng: lọc Draft/AwaitingTenant/
ChangeRequested/Upcoming, mở chi tiết một HĐ có phiên bản ký, và duyệt thử 1 yêu cầu SW-07 (gia hạn)
cùng 1 SW-08 (cập nhật cọc) trong hàng đợi.

## 10. Rủi ro & lưu ý

| Rủi ro | Xử lý |
|---|---|
| Seed nhầm DB thật | Chỉ Development + tên DB chứa `Demo` |
| JSON 62 MB lỡ bị commit | Đã gitignore `NhaTro/Web/Data/Seeding/demo-data.json`; không commit thư mục `.tmp` |
| Ảnh CK thật lộ thông tin | Chỉ dùng ảnh tự sinh trong repo; ảnh thật truyền qua `--proof-image` ở máy local |
| Chạy lại nhân đôi | DB không trống ⇒ từ chối; muốn nạp lại phải `--clear-demo` |
| ID snapshot đụng nhau | Reading dùng Id ≤ 99.999, kỳ đọc mới dùng 100.001+ |
| Bản ghi Pending thiếu ảnh/token | Validator kiểm count ảnh và payload; token đọc lại sau khi chèn |
| Khác biệt giữa máy | PRNG seed cố định; số liệu trong tài liệu này ứng với seed `20261113` |
