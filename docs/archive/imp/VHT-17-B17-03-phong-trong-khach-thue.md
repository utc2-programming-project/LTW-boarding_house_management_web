# VHT-17 — Ghi chú gap B17-03

## Tình trạng

**Chưa xử lý — chờ VHT-24 và VHT-25.**

| Trường | Nội dung |
| --- | --- |
| Mã | B17-03 |
| Mức độ | Medium |
| Khu vực | Cột Phòng trong danh sách khách thuê |
| Hiện trạng | Cột Phòng đang hiển thị “Chưa có dữ liệu”. |
| Nguyên nhân | Chưa có liên kết dữ liệu khách thuê với phòng qua hợp đồng. |
| Phụ thuộc | VHT-24 hoàn thiện phạm vi quản lý/tòa nhà; VHT-25 gắn user với hồ sơ khách và liên kết thuê. |

## Hướng xử lý khi hoàn tất phụ thuộc

Khi VHT-24/VHT-25 đã có dữ liệu liên kết thuê cần thiết, cập nhật nguồn truy vấn/DTO danh sách khách thuê để lấy phòng từ liên kết hợp đồng thật và hiển thị ở cột Phòng. Không giả lập hoặc suy đoán phòng khi chưa có liên kết.

Kiểm tra lại danh sách sau khi ghép dữ liệu thật, gồm trường hợp khách có và chưa có phòng liên kết; giữ trạng thái “Chưa có dữ liệu” cho trường hợp thực sự chưa có dữ liệu.

Tham chiếu: [kế hoạch công việc](../task/cong_viec_theo_tuan.md) — VHT-24, VHT-25.
