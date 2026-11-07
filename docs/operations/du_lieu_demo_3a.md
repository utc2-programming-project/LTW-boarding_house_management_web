# Dữ liệu demo 3A — VHT-49

> DB demo: `NhaTro_Demo_3A` (LocalDB mặc định). DB test của integration test vẫn là DB tạm riêng.
> Dữ liệu hoàn toàn giả; không dùng cho Production.

## 1. Chạy một lệnh

```powershell
powershell -File scripts/database/tao_db_demo_3a.ps1 -Database NhaTro_Demo_3A -Recreate
```

Kết quả đúng: `KET QUA: DAT`. Chạy lại **không** có `-Recreate` để kiểm idempotent; không nhân đôi dòng.

Script thực hiện: tạo DB (nếu cần) → `dotnet ef database update` từ 0 migration → `--seed-demo` → kiểm count bằng `sqlcmd`.

## 2. Tài khoản

Mật khẩu mặc định: `Demo@123456` (đổi bằng `-Password`). Chỉ dùng cho demo.

Khi chạy profile `http`/`https`, bảng **“Đăng nhập nhanh để test”** ở trang Login hiện đúng 6 tài khoản demo bên dưới (không hiện tài khoản `@nhatro.local`).

| Email | Vai trò | Phạm vi |
|---|---|---|
| `admin@demo.local` | Admin | Quản trị hệ thống; không vào nghiệp vụ |
| `chutro.a@demo.local` | Chủ trọ | Nhà A |
| `chutro.b@demo.local` | Chủ trọ | Nhà B |
| `quanly.a@demo.local` | Quản lý | Chỉ nhà A |
| `khachthue.a@demo.local` | Khách thuê | Chỉ phòng A101 |
| `khachthue.b@demo.local` | Khách thuê | Chỉ phòng B201 |

## 3. Dữ liệu nền

| Nhóm | Dữ liệu |
|---|---|
| Nhà | A: Nhà trọ Demo 3A - Bình Thạnh; B: Nhà trọ Demo 3A - Thủ Đức |
| Phòng A | A101 đang thuê; A102 trống; A103 trong thùng rác |
| Phòng B | B201 đang thuê; B202 trống |
| Hợp đồng | `DEMO-A101-2026` (A101, 3.000.000đ, cọc 6.000.000đ); `DEMO-B201-2026` (B201, 4.000.000đ, cọc 8.000.000đ) — Approved, 01/01/2026 → 31/12/2027 |
| Khách | Khách thuê Demo A (A101); Khách thuê Demo B (B201); Khách Demo đã xóa (thùng rác) |
| Dịch vụ nhà A | Internet 100.000đ; Giữ xe 50.000đ; Vệ sinh 30.000đ (mỗi tháng) |
| Dịch vụ nhà B | Internet 100.000đ; Giữ xe 60.000đ; Vệ sinh 30.000đ (mỗi tháng) |

## 4. Hai kỳ chỉ số

| Phòng | Kỳ | Điện | Nước |
|---|---|---|---|
| A101 | 01/09/2026 – 30/09/2026 | 1.000 → 1.105 (105 kWh) | 100 → 105 (5 m³) |
| A101 | 01/10/2026 – 31/10/2026 | 1.105 → 1.215 (110 kWh) | 105 → 111 (6 m³) |
| B201 | 01/09/2026 – 30/09/2026 | 2.000 → 2.080 (80 kWh) | 50 → 54 (4 m³) |
| B201 | 01/10/2026 – 31/10/2026 | 2.080 → 2.160 (80 kWh) | 54 → 59 (5 m³) |

Lịch sử sửa chỉ số: A101 kỳ 1, điện `1.100 → 1.105`, nước `104 → 105`, lý do “Điều chỉnh chỉ số demo 3A”.

## 5. Đáp án tiền

Công thức: `tiền thuê + dịch vụ cố định + tiền điện theo bậc + tiền nước theo bậc`.

| Khóa | Thuê | Dịch vụ | Điện | Nước | Tổng |
|---|---:|---:|---:|---:|---:|
| A101-2026-09 | 3.000.000 | 180.000 | 213.600 | 39.700 | **3.433.300** |
| A101-2026-10 | 3.000.000 | 180.000 | 225.500 | 52.600 | **3.458.100** |
| B201-2026-09 | 4.000.000 | 190.000 | 160.700 | 26.800 | **4.377.500** |
| B201-2026-10 | 4.000.000 | 190.000 | 160.700 | 39.700 | **4.390.400** |

Cách tính điện/nước (biểu giá seed sẵn, đã gồm VAT theo quy định):

- Điện 105 kWh = `50×1.984 + 50×2.050 + 5×2.380 = 213.600`.
- Điện 110 kWh = `50×1.984 + 50×2.050 + 10×2.380 = 225.500`.
- Điện 80 kWh = `50×1.984 + 30×2.050 = 160.700`.
- Nước 4 m³ = `4×6.700 = 26.800`; nước 5 m³ = `4×6.700 + 1×12.900 = 39.700`.
- Nước 6 m³ = `4×6.700 + 2×12.900 = 52.600`.

`DemoDataSeeder` tính lại bằng `UtilityTariffService`; nếu biểu giá đổi làm lệch bảng trên, seeder dừng và báo lỗi thay vì âm thầm seed sai.

## 6. D1–D17 bổ sung

| Hạng mục | Dữ liệu |
|---|---|
| Admin | `admin@demo.local` |
| Công tắc 3 chế độ | `SW-01 = Cho phép`, `SW-02 = Duyệt`, `SW-03 = Không thể`; 18 công tắc còn lại giữ mặc định |
| Yêu cầu chờ duyệt | `SW-02`, đối tượng phòng A102, do Quản lý A gửi, trạng thái Pending |
| Thùng rác | Phòng A103; khách “Khách Demo đã xóa”; dịch vụ “Truyền hình cáp” |
| Biểu giá nạp sẵn | 5 `UtilityTariffs` + 24 bậc giá từ migration; 6 dịch vụ theo nhà |
| Lịch sử sửa chỉ số | 1 dòng của A101 kỳ 1 |

## 7. Chạy web trên DB demo

Script chỉ seed rồi thoát. Profile `http`/`https` trong `launchSettings.json` đã trỏ sẵn `NhaTro_Demo_3A` và đặt `DemoSeed__SkipDevAccounts=true`:

```powershell
dotnet run --project NhaTro/Web --launch-profile http
```

Mở http://localhost:5177 (hoặc https://localhost:7260). Trong Visual Studio chọn profile `http` hoặc `https` rồi F5.

Profile `dev` (http://localhost:5178) giữ hành vi cũ: dùng `DefaultConnection` trong `appsettings.json` và seed 4 tài khoản dev. Nếu chạy tay bằng `Web.exe` mà không qua launch profile, phải tự đặt `ConnectionStrings__DefaultConnection` và `DemoSeed__SkipDevAccounts=true` như trên.

## 8. Reset và idempotent

- Tạo lại từ sạch: thêm `-Recreate` (script mới drop DB demo).
- Chạy lại không `-Recreate`: giữ dữ liệu, seeder tìm theo email/tên/mã/số hợp đồng/`RoomId + PeriodStart`.
- Đổi mật khẩu demo: `-Password "..."`; seeder cập nhật hash cho 6 tài khoản demo.

## 9. Ngoài phạm vi 3A

- Không seed hóa đơn, payment, snapshot tài chính; các phần này thuộc 3C/VHT-135.
- Không sửa schema/migration; DB demo dùng đúng 39 migration của `origin/dev@b5eac48`.
- Không dùng dữ liệu cá nhân thật; mọi email là `@demo.local`, điện thoại/CCCD bắt đầu `000000`.
