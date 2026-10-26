# Lỗi chú ý – 2A VHT-39

- **Nhánh:** `VHT-39-dung-ui-bao-va-xu-ly-su-co-p7`
- **Đã fetch/pull:** `946af7f` – “VHT-39: complete demo approvals and repair assignment details”
- **Phạm vi:** UI/demo báo và xử lý sự cố P7 (2A).
- **Không gồm:** khung VHT-40 và các hạng mục 2D.
- **Kiểm tra lại:** build `0 warning / 0 error`; `362/362` unit test pass; đã chạy tay lại luồng khách → quản lý → chủ trọ → khách.

## Đã sửa sau các lần pull

- Tách hạn mức **3 ảnh báo cáo** và **3 ảnh kết quả**.
- Thêm phiếu **Hoàn thành** mẫu ở phòng 101.
- Tạo phiếu xong tự chuyển kịch bản về **Có dữ liệu mẫu**.
- Validate tự xóa khi sửa đúng field.
- Thêm luồng **duyệt/từ chối yêu cầu mẫu** cho chủ trọ.
- Thêm **thợ được phân công** và **chi phí sửa chữa**.
- Đổi ô “Bước xử lý tiếp theo” từ select disabled sang text.

## 1. Lỗi còn lại

### 1.1. Lịch sử chi tiết chưa dùng số tabular (Thấp)

Class `.tabular-nums` chưa set `font-variant-numeric: tabular-nums`, nên số ngày/giờ và tiền chưa thẳng cột.

- Vị trí: `NhaTro/Web/Features/Maintenance/Components/IncidentDetailModal.razor.css:25`
- Hướng xử lý: thêm `font-variant-numeric: tabular-nums`.

### 1.2. Key lỗi tải danh sách không được dùng (Thấp)

`SampleLoadError` được ném ra nhưng không có trong resx; DataTable bắt lỗi và hiển thị thông báo chung `LoadFailed`.

- Vị trí: `NhaTro/Web/Features/Maintenance/Components/IncidentListView.razor:100`
- Hướng xử lý: xóa key chết, hoặc bổ sung resource nếu muốn thông báo riêng.

## 2. Lưu ý khi demo

- Nút **Vai trò demo** chỉ hiện khi chạy Development (`NhaTro/Web/Features/Maintenance/Pages/Incidents.razor:143`). Chạy môi trường khác sẽ không đổi vai trò được.
- Công cụ đổi vai trò cho phép tài khoản khách xem dữ liệu giả của mọi phòng. Đây là cơ chế demo, không dùng ngoài màn hình demo.
- Công tắc SW-12/13/14 trong khung demo chỉ hiện với vai trò quản lý (`Incidents.razor:71`), trong khi D12 quy định chủ trọ đặt công tắc. Đây là công cụ mô phỏng, không phải phân quyền thật.
- Hàng đợi duyệt hiện là **mẫu trong bộ nhớ**, chưa phải hàng đợi thật.

## 3. Ưu tiên xử lý

1. Thêm `font-variant-numeric: tabular-nums`.
2. Dọn key `SampleLoadError`.

## 4. Ghi chú quy trình

- Nhánh đang **3 commit ahead / 10 commit behind** so với `dev`; merge thử với `origin/dev` sạch, nên merge `dev` trước khi mở PR.
- Các thay đổi đổi vai trò demo và file này đang ở **local, chưa commit/push**.
