# Quy chuẩn giao diện — Web vận hành nhà trọ

## 1. Phong cách chung

- Tham khảo [June.so](https://www.june.so/integration/hubspot): sạch, thoáng, nền sáng, điểm nhấn tím indigo. Thông số dưới đây là của dự án, không phải bộ thông số chính thức của June.
- Icon: [Lucide Animated](https://lucide-animated.com/). **Không dùng emoji, icon bàn phím hoặc trộn icon Material.**
- Giữ Blazor/MudBlazor; chuyển SVG/hiệu ứng Lucide Animated sang dạng tương thích Blazor, giữ giấy phép MIT; không thêm React.
- Dùng chung quy chuẩn cho mọi chức năng và vai trò; không bổ sung nghiệp vụ.
- Giao diện hỗ trợ tiếng Việt (`vi-VN`) và tiếng Anh (`en-US`), mặc định tiếng Việt; mỗi feature có cùng resource key ở hai ngôn ngữ. Quy chuẩn font/màu/layout dùng chung, kiểm tra cả text tiếng Anh dài; xem phân công localization tại `docs/task/cong_viec_theo_tuan.md`. INIT hiện chỉ có trang giới thiệu song ngữ, không phải toàn ứng dụng đã được dịch.
- Đơn vị: CSS px; chữ triển khai bằng rem, gốc 16px. Chiều cao component là tối thiểu, cho phép giãn theo nội dung.

## 2. Font và cỡ chữ

**Font:** `Inter, "Segoe UI", Roboto, Helvetica, Arial, sans-serif`; hỗ trợ tiếng Việt. Độ đậm: 400/500/600/700. Số liệu dùng `tabular-nums`.

| Thành phần | Cỡ chữ / dòng | Đậm | Màu |
| --- | --- | --- | --- |
| Tên ứng dụng | 18 / 24 | 700 | text-primary |
| Tiêu đề đăng nhập | 32 / 40 | 700 | text-primary |
| Tiêu đề trang desktop / mobile | 28 / 36; 24 / 32 | 700 | text-primary |
| Tiêu đề khu vực | 20 / 28 | 600 | text-primary |
| Tiêu đề card/dialog | 18 / 26 | 600 | text-primary |
| KPI desktop / mobile | 32 / 40; 28 / 36 | 700 | text-primary |
| Nội dung, ô bảng | 14 / 22 | 400 | text-primary |
| Mô tả | 14 / 22 | 400 | text-secondary |
| Menu thường / chọn | 14 / 20 | 500 / 600 | text-secondary / accent-text |
| Label | 14 / 20 | 500 | text-primary |
| Input desktop / mobile | 14 / 22; 16 / 24 | 400 | text-primary |
| Placeholder | Như input | 400 | text-muted |
| Nút | 14 / 20 | 600 | Theo loại nút |
| Header bảng | 13 / 20 | 600 | text-secondary |
| Badge | 12 / 18 | 500 | Theo trạng thái |
| Breadcrumb thường / hiện tại | 13 / 20 | 400 / 500 | text-secondary / text-primary |
| Helper, thời gian, chú thích | 12 / 18 | 400 | text-secondary |
| Lỗi field | 12 / 18 | 400 | Màu chữ lỗi |
| Tooltip | 12 / 18 | 500 | tooltip-text |
| Trục/legend biểu đồ | 12 / 18 | 400 | text-secondary |
| Giá trị nổi bật | 16 / 24 | 600 | text-primary |

Letter spacing: mặc định 0; tiêu đề 28–32px: -0.02em; 20–24px: -0.01em. Không dùng chữ dưới 12px.

## 3. Màu sắc

### Màu giao diện

| Token | Sáng | Tối |
| --- | --- | --- |
| page-bg | #F8FAFC | #0F172A |
| surface | #FFFFFF | #1E293B |
| surface-subtle | #F1F5F9 | #263449 |
| sidebar-bg, topbar-bg | #FFFFFF | #111827 |
| text-primary | #0D131B | #F8FAFC |
| text-secondary | #475569 | #CBD5E1 |
| text-muted | #64748B | #94A3B8 |
| text-disabled | #94A3B8 | #64748B |
| border — đường chia/card | #E2E8F0 | #334155 |
| control-border — input/checkbox | #64748B | #94A3B8 |
| border-hover | #475569 | #CBD5E1 |
| primary | #4F46E5 | #4F46E5 |
| primary-hover | #4338CA | #4338CA |
| primary-pressed | #3730A3 | #3730A3 |
| on-primary | #FFFFFF | #FFFFFF |
| accent-text — link/icon chọn | #4338CA | #C7D2FE |
| accent-soft, selected-bg | #EEF2FF | #312E81 |
| accent-soft-hover | #E0E7FF | #3730A3 |
| accent-border | #C7D2FE | #818CF8 |
| hover-bg | #F1F5F9 | #334155 |
| pressed-bg | #E2E8F0 | #475569 |
| disabled-bg | #F1F5F9 | #263449 |
| focus-ring | #4F46E5 | #A5B4FC |
| tooltip-bg | #0F172A | #F8FAFC |
| tooltip-text | #FFFFFF | #0F172A |
| scrollbar-thumb | #CBD5E1 | #475569 |

### Màu trạng thái — thứ tự: chữ / nền / viền

| Trạng thái | Sáng | Tối |
| --- | --- | --- |
| Thành công | #166534 / #F0FDF4 / #BBF7D0 | #86EFAC / #052E16 / #166534 |
| Cảnh báo | #92400E / #FFFBEB / #FDE68A | #FCD34D / #451A03 / #92400E |
| Lỗi | #B91C1C / #FEF2F2 / #FECACA | #FCA5A5 / #450A0A / #991B1B |
| Thông tin | #1E40AF / #EFF6FF / #BFDBFE | #93C5FD / #172554 / #1E40AF |
| Trung tính | #475569 / #F1F5F9 / #CBD5E1 | #CBD5E1 / #334155 / #64748B |

Nút nguy hiểm: nền #B91C1C, hover #991B1B, nhấn #7F1D1D, chữ trắng ở cả hai theme. Trạng thái luôn có nhãn chữ.

### Màu biểu đồ

| Series | Sáng | Tối |
| --- | --- | --- |
| 1 | #4F46E5 | #A5B4FC |
| 2 | #0F766E | #5EEAD4 |
| 3 | #0369A1 | #7DD3FC |
| 4 | #A16207 | #FCD34D |
| 5 | #7E22CE | #D8B4FE |
| 6 | #BE185D | #F9A8D4 |

## 4. Khoảng cách và hình dạng

| Thuộc tính | Giá trị |
| --- | --- |
| Thang spacing | 4, 8, 12, 16, 20, 24, 32, 40, 48, 64px |
| Icon–chữ; label–input; giữa nút; tiêu đề–mô tả | 8px |
| Input–helper/lỗi | 4px |
| Giữa field | 20px |
| Giữa nhóm thao tác | 16px |
| Đầu trang–nội dung | 24px |
| Gap/padding card desktop / mobile | 24 / 16px |
| Padding trang desktop / tablet / mobile | 32 / 24 / 16px |
| Giữa section chi tiết | 32px |
| Bo checkbox / badge | 4 / 6px |
| Bo nút/input/select/menu/tooltip | 8px |
| Bo card/bảng/dropdown/toast | 12px |
| Bo dialog | 16px |
| Avatar/radio/switch | Tròn, radius 999px |
| Viền thường | 1px solid |
| Focus bàn phím | Outline 2px focus-ring, offset 2px |

| Bóng/lớp phủ | Sáng | Tối |
| --- | --- | --- |
| Card | 0 1px 2px rgba(15,23,42,0.04) | 0 1px 2px rgba(0,0,0,0.20) |
| Popover | 0 8px 24px rgba(15,23,42,0.10) | 0 8px 24px rgba(0,0,0,0.30) |
| Dialog | 0 24px 64px rgba(15,23,42,0.18) | 0 24px 64px rgba(0,0,0,0.45) |
| Overlay | rgba(15,23,42,0.40) | rgba(0,0,0,0.60) |

Z-index: nội dung 0; thanh cố định 100; dropdown 200; overlay drawer 300/drawer 310; overlay dialog 400/dialog 410; toast 500; tooltip 600. Popover trong dialog nằm trên dialog cha. Không dùng gradient, glow hoặc glassmorphism.

## 5. Bố cục

| Thuộc tính | Desktop ≥1200px | Tablet 768–1199px | Mobile <768px |
| --- | --- | --- | --- |
| Sidebar | 240px | Thu gọn 72px | Drawer tối đa min(280px, viewport − 48px) |
| Topbar | 64px | 64px | 56px |
| KPI | 4 cột | 2 cột | 1 cột |
| Biểu đồ | 2 cột, cao 280px | 1 cột, cao 280px | 1 cột, cao 240px |
| Form | Tối đa 2 cột | Tối đa 2 cột nếu đủ chỗ | 1 cột |
| Đầu trang | Tiêu đề trái, nút phải | Như desktop nếu đủ chỗ | Xếp dọc, gap 16px |

- Nội dung tối đa 1440px; form nghiệp vụ 960px; form tài khoản 640px.
- Sidebar padding 16px; logo cao 40px, cách menu 24px; item cao 44px, gap 4px. Mục chọn: accent-soft/accent-text; hover: hover-bg.
- Đăng nhập: card tối đa 420px, padding desktop/mobile 32/24px.
- Breadcrumb cách tiêu đề 8px. Bảng rộng cuộn trong khung, không làm tràn trang.

## 6. Thông số component

| Component | Quy chuẩn |
| --- | --- |
| Nút nhỏ / thường / lớn | Cao 32/40/44px; padding ngang 12/16/20px; mobile tối thiểu 44px |
| Nút chính | primary/on-primary; hover primary-hover; nhấn primary-pressed |
| Nút phụ | surface/text-primary, viền control-border; hover hover-bg; nhấn pressed-bg |
| Ghost | Nền trong suốt/text-secondary; hover hover-bg; nhấn pressed-bg |
| Disabled | disabled-bg/text-disabled; không tương tác |
| Nút loading | Giữ chiều rộng; icon 16px và nhãn đang xử lý; chặn gửi lặp |
| Link | accent-text; hover gạch chân; link trong đoạn văn luôn gạch chân |
| Input/select | Cao 44px, padding ngang 12px, surface/text-primary, viền control-border |
| Textarea | Cao tối thiểu 112px, padding 12px, cho đổi chiều cao |
| Field hover/focus/lỗi | Viền border-hover/focus-ring/màu chữ lỗi; giữ outline khi focus |
| Readonly | surface-subtle/text-primary; cho sao chép |
| Dropdown item/menu | Item cao desktop/touch 40/44px, padding 8px 12px; menu cao tối đa 320px |
| Checkbox/radio | Hình 18px; vùng chạm 44px; chọn primary, dấu SVG trắng |
| Switch | Track 40×24px, thumb 20px, inset 2px; tắt text-muted, bật primary, thumb trắng |
| Chọn ngày | Ngày chọn primary/on-primary; ngày hiện tại viền focus-ring; vùng touch 44px |
| Card | surface; viền border; bóng card; KPI nhãn–số–chú thích gap 8px |
| Icon KPI | Nền accent-soft 40×40px, bo 8px; icon accent-text 24px |
| Bảng | Header cao 44px, surface-subtle; hàng cao 52px; ô padding 12px 16px |
| Toolbar bảng | Padding 16px, gap 12px |
| Hàng bảng | Viền dưới border; hover hover-bg; chọn selected-bg; không zebra |
| Tab | Cao 44px, padding ngang 16px; chọn accent-text, đậm 600, gạch dưới focus-ring 2px |
| Badge | Cao 24px, padding 2px 8px, viền 1px; màu trạng thái; icon 16px, gap 4px |
| Phân trang | Nút desktop/touch 36/44px, bo 8px, gap 4px; chọn primary/on-primary |
| Dialog | Rộng tối đa 400/560/800px và viewport − 32px; cao tối đa viewport − 48px |
| Nội dung dialog | Padding desktop/mobile 24/16px; tiêu đề–body 16px; footer gap 8px |
| Tooltip | Padding 6px 10px, rộng tối đa 240px, bóng popover; trễ 300ms |
| Toast | Desktop rộng tối đa 360px, cách mép 24px; mobile rộng viewport − 32px, cách mép 16px |
| Nội dung toast | Padding 16px, icon 20px, gap 12px; bóng popover; nền/chữ theo trạng thái |
| Alert | Padding 16px, bo 8px, viền 1px, icon 20px; màu trạng thái |
| Empty state | Padding desktop/mobile 32/24px; icon 48px; tiêu đề 16/24 đậm 600; mô tả 14/22; gap 12px |
| Skeleton | surface-subtle; bo theo component; shimmer 1.2s |
| Loading vùng | Icon 20px, chữ 14/22: Đang tải… |

Label nằm trên field; lỗi nằm dưới. Chữ căn trái, số liệu căn phải; không cắt số tiền. Toast thông thường đóng sau 5s, tạm dừng khi hover/focus; toast lỗi giữ tới khi đóng. Dialog quản lý và trả focus về nút mở.

## 7. Icon, chuyển động và biểu đồ

- SVG 24×24, stroke 2, nét/góc bo tròn, màu currentColor.
- Icon: inline/badge 16px; nút/menu/field 20px; KPI/section 24px; empty state 48px.
- Nút chỉ icon: vùng bấm desktop 40×40px, touch 44×44px; có tên truy cập.
- Icon mặc định text-secondary; mục chọn accent-text; trong nút theo màu chữ nút.

| Chuyển động | Thời lượng | Hiệu ứng |
| --- | --- | --- |
| Hover/focus | 150ms | Đổi màu |
| Dropdown | 150ms | Fade, dịch tối đa 4px |
| Drawer | 200ms | Trượt |
| Dialog | 200ms | Fade, scale 0.98 → 1 |
| Toast | 200ms | Fade, dịch tối đa 8px |
| Icon | 200–400ms | Một lượt khi tương tác |

Easing: `cubic-bezier(0.2, 0, 0, 1)`. Giảm chuyển động: tắt animation icon, trượt, scale và shimmer. Không chạy icon trang trí lặp liên tục.

Biểu đồ: đường 2px, marker 4px, đầu cột bo 4px; grid border 1px; legend gap 16px, swatch 12px, cách chữ 8px. Tooltip surface/text-primary, viền border, bo 8px, padding 12px, bóng popover. Có nhãn/chú giải và nội dung thay thế; không dùng 3D.

## 8. Quy tắc chung

- Ngày `dd/MM/yyyy`, giờ `HH:mm`, số Việt Nam, tiền `1.500.000 ₫`; thiếu dữ liệu ghi `Chưa có dữ liệu`.
- Có đủ trạng thái hover, focus, nhấn, disabled, loading, rỗng và lỗi; trạng thái không chỉ thể hiện bằng màu.
- Hỗ trợ bàn phím, nhãn truy cập, zoom và giảm chuyển động. Mục tiêu tương phản: chữ thường 4.5:1; chữ lớn và điều khiển 3:1, kiểm thử khi triển khai.
- Kiểm tra viewport 360, 390, 768, 1024, 1280, 1440px và zoom 200%.
- Dùng theme/token chung; không tự đổi font, màu, icon hoặc thông số theo từng trang.

## 9. Vai trò, phê duyệt và thùng rác (bổ sung theo ma trận quyền)

- Nguồn chuẩn quyền: `docs/task/thao_tac_va_quyen.xlsx` (4 vai trò Admin/Chủ trọ/Quản lý/Khách thuê, 16 công tắc Quản lý).
- Menu theo vai trò: Admin chỉ thấy Tài khoản, Phân quyền, Nhật ký (không menu nghiệp vụ P2–P8); Chủ trọ có thêm Công tắc, Hàng đợi duyệt, Thùng rác.
- Nhãn trạng thái `Chờ duyệt` dùng badge cảnh báo, có đủ VI/EN; trạng thái tài khoản (Chờ duyệt/Hoạt động/Khóa) dùng màu thông tin/thành công/trung tính kèm chữ, không chỉ dùng màu.
- Trang thùng rác (phòng, hồ sơ khách) dùng empty state khi trống; nút Khôi phục chỉ Chủ trọ thấy.
- Số CCCD của khách thuê luôn che với Quản lý; không xuất CCCD ra Excel.
