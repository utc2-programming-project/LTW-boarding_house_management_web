# SPEC — Tìm kiếm toàn cục (Global Search)

> Trạng thái: **đặc tả, chưa code**. Phạm vi v1, thiết kế để mở rộng.
> Nền: NhaTro (Blazor .NET 10) — vai trò `Admin`, `ChuTro`, `QuanLy`, `KhachThue`.
> Liên quan: [Ra vào cổng](chuc-nang-ra-vao-cong-spec.md) · [Quản lý xe](chuc-nang-quan-ly-xe-spec.md) · [Trợ lý AI](chuc-nang-tro-ly-ai-spec.md)

## 0. Quy ước chung

| Mục | Quy ước |
|---|---|
| Phạm vi dữ liệu | Search chỉ trả kết quả trong scope của người dùng; trang đích vẫn tự kiểm tra quyền |
| Bỏ dấu | Dùng lại `Web/Common/Utils/TextSearchNormalizer` ("phong 101" khớp "Phòng 101") |
| Mở rộng | Mỗi module một `ISearchProvider`; thêm nhóm mới không đổi lõi |

## 1. Mục tiêu

Ô tìm kiếm **cạnh chuông thông báo** trên topbar (khung giao diện đã có trong `AppTopbar.razor`, hiện chưa gắn chức năng) — gõ một chỗ, nhảy tới đúng mục trong hệ thống.

## 2. Phạm vi kết quả v1

| Nhóm | Tìm theo | Mở tới |
|---|---|---|
| Phòng | Mã/tên phòng, tên nhà | `/rooms` |
| Nhà trọ | Tên, địa chỉ | `/properties` |
| Khách thuê | Họ tên, SĐT (không tìm theo CCCD) | `/tenants` |
| Hợp đồng | Số hợp đồng, tên khách | `/contracts` |
| Hóa đơn | Mã hóa đơn, kỳ, tên khách | `/invoices` |
| Sự cố | Tiêu đề, phòng | `/incidents` |
| Dịch vụ | Tên dịch vụ | `/services` |
| Phiếu ra vào | Tên/SĐT khách, phòng | `/gate` |
| Xe | Biển số/mã thẻ, tên khách, phòng | `/vehicles` |

## 3. Cách hoạt động

1. Focus bằng `Ctrl/Cmd + K` hoặc bấm ô tìm.
2. Gõ ≥ 2 ký tự → debounce ~250ms → server chạy song song các provider (mỗi module một `ISearchProvider`), giới hạn **5 kết quả/nhóm**.
3. Dropdown nhóm kết quả: icon + tiêu đề + phụ đề (phòng–nhà) + đường dẫn; điều hướng bằng ↑↓, Enter, Esc.
4. `Xem tất cả` → trang `/search?q=…` (phân trang, lọc theo nhóm).
5. Tìm theo chuỗi đã bỏ dấu (`TextSearchNormalizer`), ưu tiên khớp đầu chuỗi, khớp số phòng/mã chính xác.
6. Người dùng chỉ thấy kết quả **trong phạm vi quyền**: Chủ trọ = nhà mình; Quản lý = nhà được giao; Khách thuê = phòng/hợp đồng/hóa đơn/tài liệu/xe của mình; Admin = toàn hệ thống. Tìm kiếm chỉ là lối tắt, **không vượt quyền**.

## 4. Dữ liệu & hiệu năng

- v1 **không thêm bảng**: query trực tiếp trên dữ liệu sẵn có, dùng index hiện có (mã phòng, SĐT, số hợp đồng, biển số…).
- Mỗi provider có timeout riêng; một provider lỗi không làm hỏng cả dropdown.
- Ghi log từ khoá (ẩn danh) để cải thiện xếp hạng; không log PII đầy đủ.
- Khi dữ liệu lớn (> ~100k dòng/nhóm) mới cân nhắc full-text search/Elastic — không làm ở v1.

## 5. Liên hệ hệ thống

`AppTopbar` (thay placeholder) · `TextSearchNormalizer` · các scope service (`PropertyScope`, `RoomScope`, `TenantScope`, quyền xem hóa đơn/sự cố/xe) · `AppNavigation` (nhãn/route) · kết quả có thể là phiếu ra vào, xe, đề xuất AI.

## 6. Mở rộng sau

Tìm gần đây, command palette (nhảy chức năng), tìm trong nội dung tệp (OCR giấy tờ), "không có kết quả → hỏi trợ lý AI".

## 7. Liên hệ code hiện có

| Điểm | Vị trí |
|---|---|
| Ô tìm kiếm placeholder | `NhaTro/Web/Components/Layout/AppTopbar.razor` (khối `topbar-search`, chưa gắn sự kiện) |
| Chuẩn hoá bỏ dấu | `NhaTro/Web/Common/Utils/TextSearchNormalizer.cs` |
| Cây điều hướng/nhãn | `NhaTro/Web/Components/Layout/AppNavigation.cs` |
| Scope dữ liệu | `PropertyScope`, `RoomScope`, `TenantScope`, các service quyền xem hóa đơn/sự cố |
