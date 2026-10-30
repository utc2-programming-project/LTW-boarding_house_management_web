# VHT-36 Đợt 2B — Biên bản bàn giao

## Đã hoàn thành

- Trang `/documents`: list, empty, loading, error, success
- Dialog upload với validate extension (PDF/JPG/PNG) + size (10MB) + file rỗng
- Download qua service có kiểm quyền (không lộ path)
- Mobile responsive (card list, touch ≥44px)
- i18n VI/EN đầy đủ
- Xử lý `NoTenantLinked` (empty state riêng)
- Permission rules theo D9

## Còn mở (blocker)

- 2 test fail: xem comment trong `TenantDocumentPermissionRules.cs` — cần nhóm chốt interpretation
- Bug service Phát: `DeleteAsync` check UploaderId → Chủ trọ không xóa được file của khách

## Test

- Build: 0 error, 0 warning
- Unit: ___/508 pass (2 fail do interpretation)
- Integration: 9 pass, 82 skip
- Permission tests: 6/8 pass (2 fail do interpretation)
- Browser test: chưa chạy (sẽ chạy sau khi họp nhóm)

## Giới hạn

- Mock data chỉ dùng cho UI, không phải dữ liệu thật
- Không làm `/tenant/my-room` (thuộc 2D)
- Không làm nút Export (chờ nhóm chốt)
