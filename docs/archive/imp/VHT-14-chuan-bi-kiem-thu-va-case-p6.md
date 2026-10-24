# VHT-14 — Chuẩn bị kiểm thử và case P6

## 1. Mục tiêu

Dựng nền kiểm thử thật cho Sprint 1D:

- DB test riêng, không dùng chung DB demo.
- Fixture SQL Server + `WebApplicationFactory` cho HTTP/auth/UI.
- Đủ 4 vai trò, 2 nhà trọ của 2 chủ trọ khác nhau để test chéo phạm vi.
- Công tắc quản lý chạy đủ 3 chế độ: Cho phép / Duyệt / Không thể.
- Bộ case nền P6: thành công, sai dữ liệu, sai quyền.
- Fixture Playwright cho smoke UI đăng nhập.

Phạm vi đợt này là testing scaffold. Khi VHT-34 có DTO/rule hóa đơn thật, thay dữ liệu mẫu P6 bằng service thật.

## 2. Tách DB test với DB demo

`SqlServerFixture` tạo database `NhaTro_It_<guid>` trên LocalDB, `EnsureCreated`, rồi drop khi test xong.
`NhaTroWebApplicationFactory` thay toàn bộ đăng ký `ApplicationDbContext`/`IDbContextFactory` bằng connection string test.
Không có test nào ghi vào `aspnet-Web-...` (DB demo).

Nếu máy/CI không có LocalDB, test integration/HTTP được đánh dấu **Skip**, không fail oan.

## 3. Tài khoản và phạm vi

| Email | Vai trò | Phạm vi |
| --- | --- | --- |
| `admin@test.local` | Admin | Quản trị tài khoản/vai trò |
| `chutro.a@test.local` | Chủ trọ | Nhà trọ A |
| `chutro.b@test.local` | Chủ trọ | Nhà trọ B |
| `quanly@test.local` | Quản lý | Nghiệp vụ P2 (hiện thấy toàn bộ) |
| `khachthue@test.local` | Khách thuê | Hồ sơ khách thuê A |

Mật khẩu test: `TestPass123!` (chỉ dùng cho DB test, không phải secret thật).

Dữ liệu:

- Nhà trọ A — chủ trọ A — phòng `A101`, giá chuẩn `3.000.000`.
- Nhà trọ B — chủ trọ B — phòng `B201`, giá chuẩn `4.000.000`.
- Khách thuê A liên kết tài khoản `khachthue@test.local`.

## 4. Công tắc 3 chế độ

`TestReadinessIntegrationTests` dùng policy giả lập để kiểm đúng điểm gọi `IRoomChangePolicy`:

| Chế độ | Kết quả mong đợi |
| --- | --- |
| Cho phép (`Allowed`) | Cập nhật thành công, dữ liệu được ghi |
| Duyệt (`RequiresApproval`) | Trả `RoomErrorCodes.ApprovalRequired`, không ghi |
| Không thể (`Denied`) | Ném `UnauthorizedAccessException`, không ghi |

Khi VHT-94 thay policy thật, giữ nguyên bộ test này và đổi policy stub sang policy thật.

## 5. Case nền P6

Bộ case nằm ở `NhaTro.UnitTests/TestSupport/P6MoneyCase.cs` và được kiểm bằng `P6MoneyCaseTests`.

| Mã case | Loại | Nội dung | Kỳ vọng |
| --- | --- | --- | --- |
| `P6-MONEY-OK` | Thành công | Thuê 3.000.000 + điện 700.000 + nước 120.000 + dịch vụ 100.000 − giảm 50.000 | Tổng `3.870.000` |
| `P6-MONEY-BAD` | Sai dữ liệu | Số điện âm | Bị đánh dấu không hợp lệ |
| `P6-MONEY-DENY` | Sai quyền | Khách thuê lập hóa đơn | Không được phép |

Tiêu chí VHT-14 yêu cầu ít nhất một case mỗi loại; ba case trên là mức tối thiểu.

## 6. Lệnh chạy

```powershell
# Toàn bộ unit + integration + E2E (E2E tự skip nếu app/browser chưa sẵn sàng)
dotnet test NhaTro/NhaTro.slnx

# Chỉ integration test mới
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj

# Chỉ unit test P6
dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj --filter "FullyQualifiedName~P6MoneyCase"
```

### Chạy E2E thật

1. Chạy app ở Development:

```powershell
dotnet run --project NhaTro/Web --launch-profile http
```

2. Cài browser Playwright một lần:

```powershell
pwsh NhaTro/NhaTro.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

3. Chạy E2E, truyền tài khoản có thật trong DB demo:

```powershell
$env:NHATRO_E2E_BASE_URL = "http://localhost:5177"
$env:NHATRO_E2E_EMAIL = "admin@nhatro.local"
$env:NHATRO_E2E_PASSWORD = "<mật-khẩu-trong-User-Secrets>"
dotnet test NhaTro/NhaTro.E2ETests/NhaTro.E2ETests.csproj --filter "FullyQualifiedName~LoginSmokeTests"
```

Nếu không đặt `NHATRO_E2E_EMAIL`/`NHATRO_E2E_PASSWORD`, test đăng nhập thật tự bỏ qua; test render form vẫn chạy khi app đang mở.

## 7. Tiêu chí hoàn thành đối chiếu

- [x] Test chạy lại từ DB riêng, không sửa DB demo.
- [x] Có ít nhất case thành công (`ChuTroA_chi_thay_du_lieu_trong_pham_vi_cua_minh`, `CongTac_ChoPhep_...`).
- [x] Có ít nhất case sai dữ liệu (`Du_lieu_sai_tra_ma_loi_nghiep_vu`, `P6-MONEY-BAD`).
- [x] Có ít nhất case sai quyền (`KhachThue_khong_duoc_goi_service_quan_ly`, `CongTac_KhongThe_...`, `P6-MONEY-DENY`).
- [x] Fixture Playwright sẵn sàng; test UI đăng nhập chạy khi app/browser có mặt.
- [ ] Chạy auth/UI trên bản ghép P1–P3 đầy đủ (đợt 1D, chờ bản ghép tiếp theo).
