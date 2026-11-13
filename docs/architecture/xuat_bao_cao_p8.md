# Xuất báo cáo P8

VHT-103 xuất một tệp `.xlsx` cho từng mục: Doanh thu (`DoanhThu`), Phòng (`Phong`), Khách thuê (`KhachThue`), Hợp đồng (`HopDong`), Chi phí (`ChiPhi`) và Sự cố (`SuCo`). Dữ liệu luôn đi qua service báo cáo nguồn để giữ quyền, phạm vi và công tắc xem báo cáo.

Chủ trọ được xuất trong phạm vi nhà của mình. Quản lý phải được service nguồn cho phép và qua SW-16; Admin và Khách thuê bị từ chối. Không sheet nào có cột CCCD, CMND hoặc ảnh giấy tờ.

Giới hạn mặc định là 5.000 dòng/mục, 20.000 dòng/tệp, page chunk 100. Vượt giới hạn trả mã `VolumeExceeded`; không tạo tệp dở.

Endpoint: `GET /api/reports/export/{section}`. Các section hợp lệ là `Revenue`, `Rooms`, `Tenants`, `Contracts`, `Costs`, `Incidents`. Phản hồi Excel có `Cache-Control: private, no-store` và `X-Content-Type-Options: nosniff`.

Mọi dữ liệu chuỗi được ghi bằng `ExcelCellWriter` dưới dạng text, không gán công thức. Chuỗi dài hơn 32.767 ký tự bị cắt theo giới hạn Excel. ClosedXML dùng phiên bản 0.105.1, giấy phép MIT; không thêm NuGet package mới.
