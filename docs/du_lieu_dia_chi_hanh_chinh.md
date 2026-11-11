# Dữ liệu địa chỉ hành chính

## Nguồn dữ liệu

- Nguồn: [provinces.open-api.vn](https://provinces.open-api.vn) – API v2, dữ liệu 2 cấp
  (tỉnh/thành phố + phường/xã/đặc khu) sau sáp nhập năm 2025.
- Script tải và chuẩn hóa: `scripts/data/cap_nhat_dia_chi_hanh_chinh.ps1`.
- Dữ liệu ứng dụng đọc lúc chạy: `NhaTro/Web/wwwroot/data/vn-address.json`.
- Thông tin ảnh chụp dữ liệu: `NhaTro/Web/wwwroot/data/vn-address.meta.json`
  (nguồn, thời điểm tải, số tỉnh/thành phố, số phường/xã, nhắc lịch cập nhật).

Dữ liệu được tải **một lần** rồi lưu trong project. Ứng dụng không gọi API bên ngoài
khi người dùng đang nhập địa chỉ, nên form vẫn hoạt động khi mất mạng hoặc API bị lỗi.

## Cập nhật định kỳ

Đơn vị hành chính vẫn có thể thay đổi, vì vậy cần chạy lại script:

- định kỳ khoảng **3 tháng/lần**, và
- ngay sau khi có nghị quyết/văn bản thay đổi đơn vị hành chính.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/data/cap_nhat_dia_chi_hanh_chinh.ps1
```

Sau khi chạy:

1. Kiểm tra diff của `vn-address.json` và `vn-address.meta.json`.
2. Chạy các bài kiểm thử địa chỉ:
   `dotnet test NhaTro/NhaTro.UnitTests --filter "FullyQualifiedName~Address"`.
3. Commit cả hai tệp dữ liệu cùng thay đổi mã nguồn (nếu có).

### Lưu ý đang chờ xác minh

Thông tin **Đồng Nai trở thành thành phố trực thuộc trung ương từ 30/4/2026**
(và các thay đổi hành chính tương tự) cần đối chiếu văn bản chính thức trước khi
coi là chuẩn. Ảnh chụp hiện tại ghi nhận Đồng Nai là thành phố trực thuộc trung ương
theo API v2; script sẽ ghi lại đúng dữ liệu API trả về ở lần cập nhật kế tiếp.

## Địa chỉ cũ 3 cấp

Các nhà trọ/hồ sơ cũ có thể đang lưu địa chỉ 3 cấp, ví dụ
`12 Nguyễn Trãi, Phường Bến Thành, Quận 1, TP.HCM`.

Quy ước xử lý:

- Trường địa chỉ vẫn là **một chuỗi tự do** trong cơ sở dữ liệu, không đổi schema,
  không xóa dữ liệu cũ.
- Ô **“Số nhà, tên đường”** luôn cho phép nhập tự do.
- Khi mở hồ sơ cũ, form hiển thị tỉnh/thành phố và phường/xã theo danh mục mới
  (bỏ qua cấp quận/huyện khi hiển thị) nhưng **không tự ghi đè** giá trị đã lưu.
- Chỉ khi người dùng thật sự sửa một trường địa chỉ, giá trị mới được ghép và lưu
  theo mô hình 2 cấp. Nhờ vậy, chỉ mở/xem hồ sơ sẽ không làm mất thông tin cũ.
- Phường/xã cũ không còn trong danh mục mới được giữ ở ô nhập tay để người dùng
  đối chiếu và chọn lại khi cần.
