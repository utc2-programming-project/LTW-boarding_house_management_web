# VHT-101 – Báo cáo kiểm thử ma trận quyền tự động

- Sinh lúc: 2026-11-07 11:11:44 UTC
- Nguồn yêu cầu: `docs/task/thao_tac_va_quyen.xlsx` (sheet Ma trận quyền / Công tắc Quản lý / Quyết định)
- Số liệu dưới đây là kết quả THẬT của các test hành vi đã chạy trong cùng lần chạy; không ô nào được tô hồng.
- Chống rửa điểm: Skip không tính là PASS, FAIL không biến thành BLOCKED, không đổi kỳ vọng cho khớp code.

## A. Workbook và manifest

- Workbook: `docs/task/thao_tac_va_quyen.xlsx`
- SHA-256: `48fdd0c2c64a9fde3fff2ecb983da6432bbedb770c43e321a15adb4b347799a3`
- Sheet đã đọc: Ma trận quyền, Công tắc Quản lý, Quyết định
- Thao tác: 45 | Vai trò: 4 | Ô: 180
- Phân bố ký hiệu: ✅ 67 | ⚠️ 14 | ❌ 76 | ⚙️ 16 | — 7
- Ô bắt buộc có test (không phải `—`): 173
- Công tắc Quản lý: 16 | Quyết định nghiệp vụ: 17
- Problems của manifest: không có

## B. Coverage

| Chỉ số | Giá trị |
|---|---:|
| Tổng ô ma trận | 180 |
| Ô `—` (không áp dụng, Mục VI.5) | 7 |
| Ô bắt buộc chứng minh | 173 |
| Ô đã gọi backend thật (executable) | 173 |
| Ô PASS | 173 |
| Ô FAIL | 0 |
| Ô BLOCKED – khai báo tĩnh thiếu API | 0 |
| Ô BLOCKED – phát sinh lúc chạy | 0 |
| Tỷ lệ PASS trên ô đã thực thi | 100.0% |

Đẳng thức coverage: executable 173 + BLOCKED khai báo 0 + ô `—` 7 = 180. Mỗi ô đã thực thi đều có test hành vi riêng; con số này là coverage ô, không phải tuyên bố mọi luồng UI đã được kiểm thử.

### B.1 Coverage theo phân hệ

| Phân hệ | Ô khả dụng | Đã thực thi | PASS | FAIL | BLOCKED |
|---|---:|---:|---:|---:|---:|
| P1 | 40 | 40 | 40 | 0 | 0 |
| P2 | 24 | 24 | 24 | 0 | 0 |
| P3 | 20 | 20 | 20 | 0 | 0 |
| P4 | 24 | 24 | 24 | 0 | 0 |
| P5 | 20 | 20 | 20 | 0 | 0 |
| P6 | 21 | 21 | 21 | 0 | 0 |
| P7 | 16 | 16 | 16 | 0 | 0 |
| P8 | 8 | 8 | 8 | 0 | 0 |

## C. Kết quả theo từng ô

| Ô | Thao tác | Vai trò | Ký hiệu | Expected | Actual | Kết quả | Tầng | Service | Tình huống con | Test ID |
|---|---|---|---|---|---|---|---|---|---|---|
| D2 | OP01 Đăng nhập / xác thực | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_D2_OP01_Admin |
| E2 | OP01 Đăng nhập / xác thực | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E2_OP01_ChuTro |
| F2 | OP01 Đăng nhập / xác thực | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_F2_OP01_QuanLy |
| G2 | OP01 Đăng nhập / xác thực | KhachThue | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G2_OP01_KhachThue |
| D3 | OP02 Xem thông tin cá nhân (của chính mình) | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_D3_OP02_Admin |
| E3 | OP02 Xem thông tin cá nhân (của chính mình) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E3_OP02_ChuTro |
| F3 | OP02 Xem thông tin cá nhân (của chính mình) | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_F3_OP02_QuanLy |
| G3 | OP02 Xem thông tin cá nhân (của chính mình) | KhachThue | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G3_OP02_KhachThue |
| D4 | OP03 Xem thông tin người khác | Admin | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Accounts.AccountService | 2 | P1_D4_OP03_Admin |
| E4 | OP03 Xem thông tin người khác | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E4_OP03_ChuTro |
| F4 | OP03 Xem thông tin người khác | QuanLy | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Accounts.AccountService | 2 | P1_F4_OP03_QuanLy |
| G4 | OP03 Xem thông tin người khác | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G4_OP03_KhachThue |
| D5 | OP04 Sửa thông tin cá nhân | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_D5_OP04_Admin |
| E5 | OP04 Sửa thông tin cá nhân | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E5_OP04_ChuTro |
| F5 | OP04 Sửa thông tin cá nhân | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_F5_OP04_QuanLy |
| G5 | OP04 Sửa thông tin cá nhân | KhachThue | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G5_OP04_KhachThue |
| D6 | OP05 Tạo tài khoản | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_D6_OP05_Admin |
| E6 | OP05 Tạo tài khoản | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E6_OP05_ChuTro |
| F6 | OP05 Tạo tài khoản | QuanLy | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Accounts.AccountService | 2 | P1_F6_OP05_QuanLy |
| G6 | OP05 Tạo tài khoản | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G6_OP05_KhachThue |
| D7 | OP06 Duyệt / kích hoạt / khóa tài khoản | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_D7_OP06_Admin |
| E7 | OP06 Duyệt / kích hoạt / khóa tài khoản | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E7_OP06_ChuTro |
| F7 | OP06 Duyệt / kích hoạt / khóa tài khoản | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_F7_OP06_QuanLy |
| G7 | OP06 Duyệt / kích hoạt / khóa tài khoản | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G7_OP06_KhachThue |
| D8 | OP07 Sửa / cấp role | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_D8_OP07_Admin |
| E8 | OP07 Sửa / cấp role | ChuTro | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_E8_OP07_ChuTro |
| F8 | OP07 Sửa / cấp role | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_F8_OP07_QuanLy |
| G8 | OP07 Sửa / cấp role | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Accounts.AccountService | 1 | P1_G8_OP07_KhachThue |
| D9 | OP08 [MỚI] Cấu hình công tắc Quản lý | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Settings.ManagerToggleService | 1 | P1_D9_OP08_Admin |
| E9 | OP08 [MỚI] Cấu hình công tắc Quản lý | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Settings.ManagerToggleService | 1 | P1_E9_OP08_ChuTro |
| F9 | OP08 [MỚI] Cấu hình công tắc Quản lý | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Settings.ManagerToggleService | 1 | P1_F9_OP08_QuanLy |
| G9 | OP08 [MỚI] Cấu hình công tắc Quản lý | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Settings.ManagerToggleService | 1 | P1_G9_OP08_KhachThue |
| D10 | OP09 [MỚI] Xem / xử lý hàng đợi chờ duyệt | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Approvals.ManagerApprovalService | 1 | P1_D10_OP09_Admin |
| E10 | OP09 [MỚI] Xem / xử lý hàng đợi chờ duyệt | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Approvals.ManagerApprovalService | 1 | P1_E10_OP09_ChuTro |
| F10 | OP09 [MỚI] Xem / xử lý hàng đợi chờ duyệt | QuanLy | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Approvals.ManagerApprovalService | 2 | P1_F10_OP09_QuanLy |
| G10 | OP09 [MỚI] Xem / xử lý hàng đợi chờ duyệt | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Approvals.ManagerApprovalService | 1 | P1_G10_OP09_KhachThue |
| D11 | OP10 [MỚI] Xem nhật ký thao tác (audit) | Admin | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Audit.Services.AuditLogService | 1 | P1_D11_OP10_Admin |
| E11 | OP10 [MỚI] Xem nhật ký thao tác (audit) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Audit.Services.AuditLogService | 1 | P1_E11_OP10_ChuTro |
| F11 | OP10 [MỚI] Xem nhật ký thao tác (audit) | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Audit.Services.AuditLogService | 1 | P1_F11_OP10_QuanLy |
| G11 | OP10 [MỚI] Xem nhật ký thao tác (audit) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Audit.Services.AuditLogService | 1 | P1_G11_OP10_KhachThue |
| D12 | OP11 Xem phòng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_D12_OP11_Admin |
| E12 | OP11 Xem phòng | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_E12_OP11_ChuTro |
| F12 | OP11 Xem phòng | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_F12_OP11_QuanLy |
| G12 | OP11 Xem phòng | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Rooms.RoomService | 2 | P2_G12_OP11_KhachThue |
| D13 | OP12 Sửa thông tin phòng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_D13_OP12_Admin |
| E13 | OP12 Sửa thông tin phòng | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_E13_OP12_ChuTro |
| F13 | OP12 Sửa thông tin phòng | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Rooms.RoomService | 3 | P2_F13_OP12_QuanLy |
| G13 | OP12 Sửa thông tin phòng | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_G13_OP12_KhachThue |
| D14 | OP13 Thiết lập / đổi giá phòng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_D14_OP13_Admin |
| E14 | OP13 Thiết lập / đổi giá phòng | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_E14_OP13_ChuTro |
| F14 | OP13 Thiết lập / đổi giá phòng | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Rooms.RoomService | 3 | P2_F14_OP13_QuanLy |
| G14 | OP13 Thiết lập / đổi giá phòng | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_G14_OP13_KhachThue |
| D15 | OP14 Đổi trạng thái phòng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_D15_OP14_Admin |
| E15 | OP14 Đổi trạng thái phòng | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_E15_OP14_ChuTro |
| F15 | OP14 Đổi trạng thái phòng | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Rooms.RoomService | 3 | P2_F15_OP14_QuanLy |
| G15 | OP14 Đổi trạng thái phòng | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_G15_OP14_KhachThue |
| D16 | OP15 Xóa phòng (vào thùng rác) | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_D16_OP15_Admin |
| E16 | OP15 Xóa phòng (vào thùng rác) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_E16_OP15_ChuTro |
| F16 | OP15 Xóa phòng (vào thùng rác) | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_F16_OP15_QuanLy |
| G16 | OP15 Xóa phòng (vào thùng rác) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_G16_OP15_KhachThue |
| D17 | OP16 [MỚI] Khôi phục phòng từ thùng rác | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_D17_OP16_Admin |
| E17 | OP16 [MỚI] Khôi phục phòng từ thùng rác | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_E17_OP16_ChuTro |
| F17 | OP16 [MỚI] Khôi phục phòng từ thùng rác | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_F17_OP16_QuanLy |
| G17 | OP16 [MỚI] Khôi phục phòng từ thùng rác | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Rooms.RoomService | 1 | P2_G17_OP16_KhachThue |
| D18 | OP17 Xem hồ sơ khách thuê | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_D18_OP17_Admin |
| E18 | OP17 Xem hồ sơ khách thuê | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_E18_OP17_ChuTro |
| F18 | OP17 Xem hồ sơ khách thuê | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_F18_OP17_QuanLy |
| G18 | OP17 Xem hồ sơ khách thuê | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Tenants.TenantService | 2 | P3_G18_OP17_KhachThue |
| D19 | OP18 Tạo hồ sơ khách thuê | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_D19_OP18_Admin |
| E19 | OP18 Tạo hồ sơ khách thuê | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_E19_OP18_ChuTro |
| F19 | OP18 Tạo hồ sơ khách thuê | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Tenants.TenantService | 3 | P3_F19_OP18_QuanLy |
| G19 | OP18 Tạo hồ sơ khách thuê | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_G19_OP18_KhachThue |
| D20 | OP19 Sửa hồ sơ khách thuê | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_D20_OP19_Admin |
| E20 | OP19 Sửa hồ sơ khách thuê | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_E20_OP19_ChuTro |
| F20 | OP19 Sửa hồ sơ khách thuê | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Tenants.TenantService | 3 | P3_F20_OP19_QuanLy |
| G20 | OP19 Sửa hồ sơ khách thuê | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Tenants.TenantService | 2 | P3_G20_OP19_KhachThue |
| D21 | OP20 Xóa hồ sơ khách thuê (vào thùng rác) | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_D21_OP20_Admin |
| E21 | OP20 Xóa hồ sơ khách thuê (vào thùng rác) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_E21_OP20_ChuTro |
| F21 | OP20 Xóa hồ sơ khách thuê (vào thùng rác) | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Tenants.TenantService | 3 | P3_F21_OP20_QuanLy |
| G21 | OP20 Xóa hồ sơ khách thuê (vào thùng rác) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_G21_OP20_KhachThue |
| D22 | OP21 [MỚI] Khôi phục hồ sơ khách từ thùng rác | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_D22_OP21_Admin |
| E22 | OP21 [MỚI] Khôi phục hồ sơ khách từ thùng rác | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_E22_OP21_ChuTro |
| F22 | OP21 [MỚI] Khôi phục hồ sơ khách từ thùng rác | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_F22_OP21_QuanLy |
| G22 | OP21 [MỚI] Khôi phục hồ sơ khách từ thùng rác | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Tenants.TenantService | 1 | P3_G22_OP21_KhachThue |
| D23 | OP22 Xem hợp đồng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_D23_OP22_Admin |
| E23 | OP22 Xem hợp đồng | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_E23_OP22_ChuTro |
| F23 | OP22 Xem hợp đồng | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_F23_OP22_QuanLy |
| G23 | OP22 Xem hợp đồng | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Contracts.Services.ContractService | 2 | P4_G23_OP22_KhachThue |
| D24 | OP23 Tạo hợp đồng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_D24_OP23_Admin |
| E24 | OP23 Tạo hợp đồng | ChuTro | ✅ | ALLOW | PENDING_APPROVAL | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_E24_OP23_ChuTro |
| F24 | OP23 Tạo hợp đồng | QuanLy | ✅ | ALLOW | PENDING_APPROVAL | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_F24_OP23_QuanLy |
| G24 | OP23 Tạo hợp đồng | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_G24_OP23_KhachThue |
| D25 | OP24 Duyệt / từ chối hợp đồng | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_D25_OP24_Admin |
| E25 | OP24 Duyệt / từ chối hợp đồng | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_E25_OP24_ChuTro |
| F25 | OP24 Duyệt / từ chối hợp đồng | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_F25_OP24_QuanLy |
| G25 | OP24 Duyệt / từ chối hợp đồng | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_G25_OP24_KhachThue |
| D26 | OP25 Gia hạn / thanh lý | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_D26_OP25_Admin |
| E26 | OP25 Gia hạn / thanh lý | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_E26_OP25_ChuTro |
| F26 | OP25 Gia hạn / thanh lý | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Contracts.Services.ContractService | 3 | P4_F26_OP25_QuanLy |
| G26 | OP25 Gia hạn / thanh lý | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_G26_OP25_KhachThue |
| D27 | OP26 Xem tiền cọc | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_D27_OP26_Admin |
| E27 | OP26 Xem tiền cọc | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_E27_OP26_ChuTro |
| F27 | OP26 Xem tiền cọc | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_F27_OP26_QuanLy |
| G27 | OP26 Xem tiền cọc | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Contracts.Services.ContractService | 2 | P4_G27_OP26_KhachThue |
| D28 | OP27 Cập nhật tiền cọc | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_D28_OP27_Admin |
| E28 | OP27 Cập nhật tiền cọc | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_E28_OP27_ChuTro |
| F28 | OP27 Cập nhật tiền cọc | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Contracts.Services.ContractService | 3 | P4_F28_OP27_QuanLy |
| G28 | OP27 Cập nhật tiền cọc | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Contracts.Services.ContractService | 1 | P4_G28_OP27_KhachThue |
| D29 | OP28 Xem chỉ số / chi phí | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_D29_OP28_Admin |
| E29 | OP28 Xem chỉ số / chi phí | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_E29_OP28_ChuTro |
| F29 | OP28 Xem chỉ số / chi phí | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_F29_OP28_QuanLy |
| G29 | OP28 Xem chỉ số / chi phí | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 2 | P5_G29_OP28_KhachThue |
| D30 | OP29 Nhập chỉ số điện nước | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_D30_OP29_Admin |
| E30 | OP29 Nhập chỉ số điện nước | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_E30_OP29_ChuTro |
| F30 | OP29 Nhập chỉ số điện nước | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_F30_OP29_QuanLy |
| G30 | OP29 Nhập chỉ số điện nước | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_G30_OP29_KhachThue |
| D31 | OP30 Tính tiền điện nước (tự động theo chỉ số) | Admin | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Utilities.Services.UtilityReadingService | 0 | P5_D31_OP30_Admin |
| E31 | OP30 Tính tiền điện nước (tự động theo chỉ số) | ChuTro | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Utilities.Services.UtilityReadingService | 0 | P5_E31_OP30_ChuTro |
| F31 | OP30 Tính tiền điện nước (tự động theo chỉ số) | QuanLy | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Utilities.Services.UtilityReadingService | 0 | P5_F31_OP30_QuanLy |
| G31 | OP30 Tính tiền điện nước (tự động theo chỉ số) | KhachThue | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Utilities.Services.UtilityReadingService | 0 | P5_G31_OP30_KhachThue |
| D32 | OP31 Sửa chỉ số kỳ liền trước (đã chốt) | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_D32_OP31_Admin |
| E32 | OP31 Sửa chỉ số kỳ liền trước (đã chốt) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_E32_OP31_ChuTro |
| F32 | OP31 Sửa chỉ số kỳ liền trước (đã chốt) | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 3 | P5_F32_OP31_QuanLy |
| G32 | OP31 Sửa chỉ số kỳ liền trước (đã chốt) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_G32_OP31_KhachThue |
| D33 | OP32 [MỚI] Xem lịch sử sửa chỉ số | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_D33_OP32_Admin |
| E33 | OP32 [MỚI] Xem lịch sử sửa chỉ số | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_E33_OP32_ChuTro |
| F33 | OP32 [MỚI] Xem lịch sử sửa chỉ số | QuanLy | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 2 | P5_F33_OP32_QuanLy |
| G33 | OP32 [MỚI] Xem lịch sử sửa chỉ số | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.UtilityReadingService | 1 | P5_G33_OP32_KhachThue |
| D34 | OP33 [MỚI] Thiết lập danh mục dịch vụ khác và đơn giá (không phải điện/nước) | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.ServiceCatalogService | 1 | P5_D34_OP33_Admin |
| E34 | OP33 [MỚI] Thiết lập danh mục dịch vụ khác và đơn giá (không phải điện/nước) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Utilities.Services.ServiceCatalogService | 1 | P5_E34_OP33_ChuTro |
| F34 | OP33 [MỚI] Thiết lập danh mục dịch vụ khác và đơn giá (không phải điện/nước) | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.ServiceCatalogService | 1 | P5_F34_OP33_QuanLy |
| G34 | OP33 [MỚI] Thiết lập danh mục dịch vụ khác và đơn giá (không phải điện/nước) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Utilities.Services.ServiceCatalogService | 1 | P5_G34_OP33_KhachThue |
| D35 | OP34 Xem hóa đơn | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.InvoiceQueryService | 1 | P6_D35_OP34_Admin |
| E35 | OP34 Xem hóa đơn | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.InvoiceQueryService | 1 | P6_E35_OP34_ChuTro |
| F35 | OP34 Xem hóa đơn | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.InvoiceQueryService | 1 | P6_F35_OP34_QuanLy |
| G35 | OP34 Xem hóa đơn | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Invoices.InvoiceQueryService | 2 | P6_G35_OP34_KhachThue |
| D36 | OP35 Tạo / lập hóa đơn | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.InvoiceService | 1 | P6_D36_OP35_Admin |
| E36 | OP35 Tạo / lập hóa đơn | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.InvoiceService | 1 | P6_E36_OP35_ChuTro |
| F36 | OP35 Tạo / lập hóa đơn | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Invoices.InvoiceService | 3 | P6_F36_OP35_QuanLy |
| G36 | OP35 Tạo / lập hóa đơn | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.InvoiceService | 1 | P6_G36_OP35_KhachThue |
| D37 | OP36 Phát hành hóa đơn | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.InvoiceService | 1 | P6_D37_OP36_Admin |
| E37 | OP36 Phát hành hóa đơn | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.InvoiceService | 1 | P6_E37_OP36_ChuTro |
| F37 | OP36 Phát hành hóa đơn | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Invoices.InvoiceService | 3 | P6_F37_OP36_QuanLy |
| G37 | OP36 Phát hành hóa đơn | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.InvoiceService | 1 | P6_G37_OP36_KhachThue |
| D38 | OP37 Gửi yêu cầu thanh toán | Admin | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Invoices.Services.PaymentService | 0 | P6_D38_OP37_Admin |
| E38 | OP37 Gửi yêu cầu thanh toán | ChuTro | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Invoices.Services.PaymentService | 0 | P6_E38_OP37_ChuTro |
| F38 | OP37 Gửi yêu cầu thanh toán | QuanLy | — | NOT_APPLICABLE | NOT_APPLICABLE | NOT_APPLICABLE | Service | Web.Features.Invoices.Services.PaymentService | 0 | P6_F38_OP37_QuanLy |
| G38 | OP37 Gửi yêu cầu thanh toán | KhachThue | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.Services.PaymentService | 1 | P6_G38_OP37_KhachThue |
| D39 | OP38 Xác nhận thanh toán | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.Services.PaymentService | 1 | P6_D39_OP38_Admin |
| E39 | OP38 Xác nhận thanh toán | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.Services.PaymentService | 1 | P6_E39_OP38_ChuTro |
| F39 | OP38 Xác nhận thanh toán | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Invoices.Services.PaymentService | 1 | P6_F39_OP38_QuanLy |
| G39 | OP38 Xác nhận thanh toán | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Invoices.Services.PaymentService | 1 | P6_G39_OP38_KhachThue |
| D40 | OP39 Xem doanh thu / công nợ (tổng hợp) | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.Services.RevenueReportService | 1 | P6_D40_OP39_Admin |
| E40 | OP39 Xem doanh thu / công nợ (tổng hợp) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Reports.Services.RevenueReportService | 1 | P6_E40_OP39_ChuTro |
| F40 | OP39 Xem doanh thu / công nợ (tổng hợp) | QuanLy | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.Services.RevenueReportService | 1 | P6_F40_OP39_QuanLy |
| G40 | OP39 Xem doanh thu / công nợ (tổng hợp) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.Services.RevenueReportService | 1 | P6_G40_OP39_KhachThue |
| D41 | OP40 Xem phiếu sự cố | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_D41_OP40_Admin |
| E41 | OP40 Xem phiếu sự cố | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_E41_OP40_ChuTro |
| F41 | OP40 Xem phiếu sự cố | QuanLy | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_F41_OP40_QuanLy |
| G41 | OP40 Xem phiếu sự cố | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Maintenance.IncidentService | 2 | P7_G41_OP40_KhachThue |
| D42 | OP41 Tạo phiếu bảo trì | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_D42_OP41_Admin |
| E42 | OP41 Tạo phiếu bảo trì | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_E42_OP41_ChuTro |
| F42 | OP41 Tạo phiếu bảo trì | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Maintenance.IncidentService | 3 | P7_F42_OP41_QuanLy |
| G42 | OP41 Tạo phiếu bảo trì | KhachThue | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_G42_OP41_KhachThue |
| D43 | OP42 Cập nhật trạng thái xử lý | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_D43_OP42_Admin |
| E43 | OP42 Cập nhật trạng thái xử lý | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_E43_OP42_ChuTro |
| F43 | OP42 Cập nhật trạng thái xử lý | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Maintenance.IncidentService | 3 | P7_F43_OP42_QuanLy |
| G43 | OP42 Cập nhật trạng thái xử lý | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_G43_OP42_KhachThue |
| D44 | OP43 Đính kèm ảnh kết quả | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_D44_OP43_Admin |
| E44 | OP43 Đính kèm ảnh kết quả | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Maintenance.IncidentService | 1 | P7_E44_OP43_ChuTro |
| F44 | OP43 Đính kèm ảnh kết quả | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Maintenance.IncidentService | 3 | P7_F44_OP43_QuanLy |
| G44 | OP43 Đính kèm ảnh kết quả | KhachThue | ⚠️ | CONDITIONAL | CONDITIONAL | PASS | Service | Web.Features.Maintenance.IncidentService | 2 | P7_G44_OP43_KhachThue |
| D45 | OP44 Xem báo cáo | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.RoomReportService | 1 | P8_D45_OP44_Admin |
| E45 | OP44 Xem báo cáo | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Reports.RoomReportService | 1 | P8_E45_OP44_ChuTro |
| F45 | OP44 Xem báo cáo | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Reports.RoomReportService | 2 | P8_F45_OP44_QuanLy |
| G45 | OP44 Xem báo cáo | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.RoomReportService | 1 | P8_G45_OP44_KhachThue |
| D46 | OP45 Xuất báo cáo (Excel) | Admin | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.Services.ReportExportService | 1 | P8_D46_OP45_Admin |
| E46 | OP45 Xuất báo cáo (Excel) | ChuTro | ✅ | ALLOW | ALLOWED | PASS | Service | Web.Features.Reports.Services.ReportExportService | 1 | P8_E46_OP45_ChuTro |
| F46 | OP45 Xuất báo cáo (Excel) | QuanLy | ⚙️ | SPECIAL_MODES | SPECIAL_MODES | PASS | Service | Web.Features.Reports.Services.ReportExportService | 3 | P8_F46_OP45_QuanLy |
| G46 | OP45 Xuất báo cáo (Excel) | KhachThue | ❌ | DENY | DENIED | PASS | Service | Web.Features.Reports.Services.ReportExportService | 1 | P8_G46_OP45_KhachThue |

### C.1 Bằng chứng và tình huống con của từng ô

<details><summary>Bấm để xem bằng chứng / hiệu lực DB / từng chế độ công tắc của 180 ô</summary>

- **D2 · OP01 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đăng nhập thật thành công cho Admin (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
    - `default` = Allowed: Đăng nhập thật thành công cho Admin (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
- **E2 · OP01 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đăng nhập thật thành công cho ChuTro (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
    - `default` = Allowed: Đăng nhập thật thành công cho ChuTro (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
- **F2 · OP01 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đăng nhập thật thành công cho QuanLy (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
    - `default` = Allowed: Đăng nhập thật thành công cho QuanLy (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
- **G2 · OP01 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đăng nhập thật thành công cho KhachThue (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
    - `default` = Allowed: Đăng nhập thật thành công cho KhachThue (đúng mật khẩu đã băm, đã duyệt lúc 2026-11-07)
- **D3 · OP02 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đọc hồ sơ cá nhân thành công: 
    - `default` = Allowed: Đọc hồ sơ cá nhân thành công: 
- **E3 · OP02 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đọc hồ sơ cá nhân thành công: ChuTro Test Modified
    - `default` = Allowed: Đọc hồ sơ cá nhân thành công: ChuTro Test Modified
- **F3 · OP02 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đọc hồ sơ cá nhân thành công: 
    - `default` = Allowed: Đọc hồ sơ cá nhân thành công: 
- **G3 · OP02 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Đọc hồ sơ cá nhân thành công: KhachThue Test Modified
    - `default` = Allowed: Đọc hồ sơ cá nhân thành công: KhachThue Test Modified
- **D4 · OP03 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: xem tài khoản // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: xem tài khoản
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: xem tài khoản
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: xem tài khoản
- **E4 · OP03 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Chủ trọ xem danh sách toàn bộ tài khoản (3 users)
    - `default` = Allowed: Chủ trọ xem danh sách toàn bộ tài khoản (3 users)
- **F4 · OP03 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Quản lý xem 1 khách trong tòa được giao, dữ liệu đã che theo D9 // Case B (B-ngoai-pham-vi): Không trả về khách của tòa nhà khác được giao
    - `A-trong-pham-vi` = Allowed: Quản lý xem 1 khách trong tòa được giao, dữ liệu đã che theo D9
    - `B-ngoai-pham-vi` = Denied: Không trả về khách của tòa nhà khác được giao
- **G4 · OP03 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối xem danh sách người dùng khác (TotalCount = 0)
    - `default` = Denied: Bị từ chối xem danh sách người dùng khác (TotalCount = 0)
- **D5 · OP04 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Vai trò Admin sửa hồ sơ cá nhân thành công
    - `default` = Allowed: Vai trò Admin sửa hồ sơ cá nhân thành công
- **E5 · OP04 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Vai trò ChuTro sửa hồ sơ cá nhân thành công
    - `default` = Allowed: Vai trò ChuTro sửa hồ sơ cá nhân thành công
- **F5 · OP04 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Vai trò QuanLy sửa hồ sơ cá nhân thành công
    - `default` = Allowed: Vai trò QuanLy sửa hồ sơ cá nhân thành công
- **G5 · OP04 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Vai trò KhachThue sửa hồ sơ cá nhân thành công
    - `default` = Allowed: Vai trò KhachThue sửa hồ sơ cá nhân thành công
- **D6 · OP05 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Tạo tài khoản 'pm-mgr_49@test.local' có hiệu lực thật trong AspNetUsers (10 -> 11)
    - `default` = Allowed: Tạo tài khoản 'pm-mgr_49@test.local' có hiệu lực thật trong AspNetUsers (10 -> 11)
- **E6 · OP05 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Tạo tài khoản 'pm-mgr_31@test.local' có hiệu lực thật trong AspNetUsers (8 -> 9)
    - `default` = Allowed: Tạo tài khoản 'pm-mgr_31@test.local' có hiệu lực thật trong AspNetUsers (8 -> 9)
- **F6 · OP05 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Quản lý tạo tài khoản cho khách trong tòa được giao (tai khoản ở trạng thái chờ duyệt theo D1/A49) // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: ErrorCode=OutOfScope
    - `A-trong-pham-vi` = Allowed: Quản lý tạo tài khoản cho khách trong tòa được giao (tai khoản ở trạng thái chờ duyệt theo D1/A49)
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: ErrorCode=OutOfScope
- **G6 · OP05 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối tạo tài khoản (ErrorCode=RoleNotAllowed), AspNetUsers không thêm dòng (8 -> 8)
    - `default` = Denied: Bị từ chối tạo tài khoản (ErrorCode=RoleNotAllowed), AspNetUsers không thêm dòng (8 -> 8)
- **D7 · OP06 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Vai trò Admin khóa rồi mở khóa có hiệu lực thật trong DB (LockoutEnd đổi đúng hai lần)
    - `default` = Allowed: Vai trò Admin khóa rồi mở khóa có hiệu lực thật trong DB (LockoutEnd đổi đúng hai lần)
- **E7 · OP06 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Vai trò ChuTro khóa rồi mở khóa có hiệu lực thật trong DB (LockoutEnd đổi đúng hai lần)
    - `default` = Allowed: Vai trò ChuTro khóa rồi mở khóa có hiệu lực thật trong DB (LockoutEnd đổi đúng hai lần)
- **F7 · OP06 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối khóa tài khoản (ErrorCode=Forbidden), trạng thái khóa không đổi (False -> False)
    - `default` = Denied: Bị từ chối khóa tài khoản (ErrorCode=Forbidden), trạng thái khóa không đổi (False -> False)
- **G7 · OP06 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối khóa tài khoản (ErrorCode=Forbidden), trạng thái khóa không đổi (False -> False)
    - `default` = Denied: Bị từ chối khóa tài khoản (ErrorCode=Forbidden), trạng thái khóa không đổi (False -> False)
- **D8 · OP07 · Admin → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Admin đổi vai trò có hiệu lực thật trong DB (QuanLy -> KhachThue)
    - `default` = Allowed: Admin đổi vai trò có hiệu lực thật trong DB (QuanLy -> KhachThue)
- **E8 · OP07 · ChuTro → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối đổi vai trò (ErrorCode=Forbidden), vai trò trong DB không đổi (QuanLy)
    - `default` = Denied: Bị từ chối đổi vai trò (ErrorCode=Forbidden), vai trò trong DB không đổi (QuanLy)
- **F8 · OP07 · QuanLy → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối đổi vai trò (ErrorCode=Forbidden), vai trò trong DB không đổi (QuanLy)
    - `default` = Denied: Bị từ chối đổi vai trò (ErrorCode=Forbidden), vai trò trong DB không đổi (QuanLy)
- **G8 · OP07 · KhachThue → PASS** (tầng Service, service `Web.Features.Accounts.AccountService`)
  - Bằng chứng: Bị từ chối đổi vai trò (ErrorCode=Forbidden), vai trò trong DB không đổi (QuanLy)
    - `default` = Denied: Bị từ chối đổi vai trò (ErrorCode=Forbidden), vai trò trong DB không đổi (QuanLy)
- **D9 · OP08 · Admin → PASS** (tầng Service, service `Web.Features.Settings.ManagerToggleService`)
  - Bằng chứng: Bị từ chối cấu hình công tắc Quản lý: Forbidden
    - `default` = Denied: Bị từ chối cấu hình công tắc Quản lý: Forbidden
- **E9 · OP08 · ChuTro → PASS** (tầng Service, service `Web.Features.Settings.ManagerToggleService`)
  - Bằng chứng: Chủ trọ ghi công tắc SW-01 = Deny có hiệu lực thật trong DB (trước đó Approve)
    - `default` = Allowed: Chủ trọ ghi công tắc SW-01 = Deny có hiệu lực thật trong DB (trước đó Approve)
- **F9 · OP08 · QuanLy → PASS** (tầng Service, service `Web.Features.Settings.ManagerToggleService`)
  - Bằng chứng: Bị từ chối cấu hình công tắc Quản lý: Forbidden
    - `default` = Denied: Bị từ chối cấu hình công tắc Quản lý: Forbidden
- **G9 · OP08 · KhachThue → PASS** (tầng Service, service `Web.Features.Settings.ManagerToggleService`)
  - Bằng chứng: Bị từ chối cấu hình công tắc Quản lý: Forbidden
    - `default` = Denied: Bị từ chối cấu hình công tắc Quản lý: Forbidden
- **D10 · OP09 · Admin → PASS** (tầng Service, service `Web.Features.Approvals.ManagerApprovalService`)
  - Bằng chứng: Bị từ chối truy cập hàng đợi phê duyệt: Forbidden
    - `default` = Denied: Bị từ chối truy cập hàng đợi phê duyệt: Forbidden
- **E10 · OP09 · ChuTro → PASS** (tầng Service, service `Web.Features.Approvals.ManagerApprovalService`)
  - Bằng chứng: Chủ trọ có toàn quyền xem và phê duyệt hàng đợi yêu cầu
    - `default` = Allowed: Chủ trọ có toàn quyền xem và phê duyệt hàng đợi yêu cầu
- **F10 · OP09 · QuanLy → PASS** (tầng Service, service `Web.Features.Approvals.ManagerApprovalService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: xem yêu cầu của mình // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: xem yêu cầu của mình
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: xem yêu cầu của mình
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: xem yêu cầu của mình
- **G10 · OP09 · KhachThue → PASS** (tầng Service, service `Web.Features.Approvals.ManagerApprovalService`)
  - Bằng chứng: Bị từ chối truy cập hàng đợi phê duyệt: Forbidden
    - `default` = Denied: Bị từ chối truy cập hàng đợi phê duyệt: Forbidden
- **D11 · OP10 · Admin → PASS** (tầng Service, service `Web.Features.Audit.Services.AuditLogService`)
  - Bằng chứng: Admin đọc 1 dòng nhật ký hệ thống, không lẫn sổ nghiệp vụ.
    - `default` = Allowed: Admin đọc 1 dòng nhật ký hệ thống, không lẫn sổ nghiệp vụ.
- **E11 · OP10 · ChuTro → PASS** (tầng Service, service `Web.Features.Audit.Services.AuditLogService`)
  - Bằng chứng: Chủ trọ đọc 1 dòng nhật ký nghiệp vụ trong đúng nhà A.
    - `default` = Allowed: Chủ trọ đọc 1 dòng nhật ký nghiệp vụ trong đúng nhà A.
- **F11 · OP10 · QuanLy → PASS** (tầng Service, service `Web.Features.Audit.Services.AuditLogService`)
  - Bằng chứng: Bị từ chối đọc nhật ký: ErrorCode=Forbidden: Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.
    - `default` = Denied: Bị từ chối đọc nhật ký: ErrorCode=Forbidden: Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.
- **G11 · OP10 · KhachThue → PASS** (tầng Service, service `Web.Features.Audit.Services.AuditLogService`)
  - Bằng chứng: Bị từ chối đọc nhật ký: ErrorCode=Forbidden: Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.
    - `default` = Denied: Bị từ chối đọc nhật ký: ErrorCode=Forbidden: Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.
- **D12 · OP11 · Admin → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Admin bị từ chối xem phòng nghiệp vụ (D1 BusinessGuard): Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu phong.
    - `default` = Denied: Admin bị từ chối xem phòng nghiệp vụ (D1 BusinessGuard): Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu phong.
- **E12 · OP11 · ChuTro → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Chủ trọ xem toàn bộ danh mục phòng (31 phòng)
    - `default` = Allowed: Chủ trọ xem toàn bộ danh mục phòng (31 phòng)
- **F12 · OP11 · QuanLy → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Quản lý xem được 9 phòng của nhà A được giao và không thấy phòng nhà B (0 phòng)
    - `default` = Allowed: Quản lý xem được 9 phòng của nhà A được giao và không thấy phòng nhà B (0 phòng)
- **G12 · OP11 · KhachThue → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Case A (A-phong-cua-minh): Đọc được phòng đang thuê trong hợp đồng // Case B (B-danh-muc-chung): Bị từ chối đọc danh mục phòng chung: UnauthorizedAccessException
    - `A-phong-cua-minh` = Allowed: Đọc được phòng đang thuê trong hợp đồng
    - `B-danh-muc-chung` = Denied: Bị từ chối đọc danh mục phòng chung: UnauthorizedAccessException
- **D13 · OP12 · Admin → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối sửa phòng và dữ liệu không đổi: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối sửa phòng và dữ liệu không đổi: UnauthorizedAccessException
- **E13 · OP12 · ChuTro → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Chủ trọ sửa thông tin phòng thành công trực tiếp
    - `default` = Allowed: Chủ trọ sửa thông tin phòng thành công trực tiếp
- **F13 · OP12 · QuanLy → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: SW-01: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý sửa thông tin phòng SW01-0054 (chỉ đổi tầng) có hiệu lực ngay (hiệu lực DB: 'SW01-0054/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW01-0054/4/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-01: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: chờ duyệt: ApprovalRequired (hiệu lực DB: 'SW01-0055/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW01-0055/3/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-01: 0 -> 0)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Bạn không có quyền thực hiện thao tác này (hiệu lực DB: 'SW01-0056/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW01-0056/3/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-01: 0 -> 0)
- **G13 · OP12 · KhachThue → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối sửa phòng và dữ liệu không đổi: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối sửa phòng và dữ liệu không đổi: UnauthorizedAccessException
- **D14 · OP13 · Admin → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối thiết lập giá phòng, dữ liệu không đổi: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối thiết lập giá phòng, dữ liệu không đổi: UnauthorizedAccessException
- **E14 · OP13 · ChuTro → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Chủ trọ đổi giá phòng thành công
    - `default` = Allowed: Chủ trọ đổi giá phòng thành công
- **F14 · OP13 · QuanLy → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: SW-02: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý đổi giá phòng SW02-0042 có hiệu lực ngay (hiệu lực DB: 'SW02-0042/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW02-0042/3/25.00/1600000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-02: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: chờ duyệt: ApprovalRequired (hiệu lực DB: 'SW02-0043/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW02-0043/3/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-02: 0 -> 0)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Bạn không có quyền thực hiện thao tác này (hiệu lực DB: 'SW02-0045/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW02-0045/3/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-02: 0 -> 0)
- **G14 · OP13 · KhachThue → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối thiết lập giá phòng, dữ liệu không đổi: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối thiết lập giá phòng, dữ liệu không đổi: UnauthorizedAccessException
- **D15 · OP14 · Admin → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối đổi trạng thái phòng, dữ liệu không đổi: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đổi trạng thái phòng, dữ liệu không đổi: UnauthorizedAccessException
- **E15 · OP14 · ChuTro → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Chủ trọ đổi trạng thái phòng thành công
    - `default` = Allowed: Chủ trọ đổi trạng thái phòng thành công
- **F15 · OP14 · QuanLy → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: SW-03: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý đổi trạng thái phòng SW03-0032 có hiệu lực ngay (hiệu lực DB: 'SW03-0032/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW03-0032/3/25.00/1500000.00/Maintenance/del=False/active=Tr…'; yêu cầu chờ duyệt SW-03: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: chờ duyệt: ApprovalRequired (hiệu lực DB: 'SW03-0033/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW03-0033/3/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-03: 0 -> 0)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Bạn không có quyền thực hiện thao tác này (hiệu lực DB: 'SW03-0034/3/25.00/1500000.00/Available/del=False/active=True' -> 'SW03-0034/3/25.00/1500000.00/Available/del=False/active=True'; yêu cầu chờ duyệt SW-03: 0 -> 0)
- **G15 · OP14 · KhachThue → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối đổi trạng thái phòng, dữ liệu không đổi: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đổi trạng thái phòng, dữ liệu không đổi: UnauthorizedAccessException
- **D16 · OP15 · Admin → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối đưa phòng vào thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đưa phòng vào thùng rác: UnauthorizedAccessException
- **E16 · OP15 · ChuTro → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Chủ trọ đưa phòng vào thùng rác có hiệu lực thật (IsDeleted=true); khôi phục nền: True
    - `default` = Allowed: Chủ trọ đưa phòng vào thùng rác có hiệu lực thật (IsDeleted=true); khôi phục nền: True
- **F16 · OP15 · QuanLy → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối đưa phòng vào thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đưa phòng vào thùng rác: UnauthorizedAccessException
- **G16 · OP15 · KhachThue → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối đưa phòng vào thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đưa phòng vào thùng rác: UnauthorizedAccessException
- **D17 · OP16 · Admin → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối khôi phục phòng từ thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối khôi phục phòng từ thùng rác: UnauthorizedAccessException
- **E17 · OP16 · ChuTro → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Chủ trọ khôi phục phòng từ thùng rác có hiệu lực thật (IsDeleted=false)
    - `default` = Allowed: Chủ trọ khôi phục phòng từ thùng rác có hiệu lực thật (IsDeleted=false)
- **F17 · OP16 · QuanLy → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối khôi phục phòng từ thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối khôi phục phòng từ thùng rác: UnauthorizedAccessException
- **G17 · OP16 · KhachThue → PASS** (tầng Service, service `Web.Features.Rooms.RoomService`)
  - Bằng chứng: Bị từ chối khôi phục phòng từ thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối khôi phục phòng từ thùng rác: UnauthorizedAccessException
- **D18 · OP17 · Admin → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Admin bị từ chối truy cập hồ sơ khách thuê (D1 BusinessGuard): Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue.
    - `default` = Denied: Admin bị từ chối truy cập hồ sơ khách thuê (D1 BusinessGuard): Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue.
- **E18 · OP17 · ChuTro → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Chủ trọ xem danh sách toàn bộ khách thuê (6 khách)
    - `default` = Allowed: Chủ trọ xem danh sách toàn bộ khách thuê (6 khách)
- **F18 · OP17 · QuanLy → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Quản lý xem được 10 hồ sơ khách của nhà A được giao và không thấy khách nhà B
    - `default` = Allowed: Quản lý xem được 10 hồ sơ khách của nhà A được giao và không thấy khách nhà B
- **G18 · OP17 · KhachThue → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Xem được hồ sơ của mình (TenantId=1) // Case B (B-ngoai-pham-vi): Bị từ chối đọc danh sách khách: UnauthorizedAccessException
    - `A-trong-pham-vi` = Allowed: Xem được hồ sơ của mình (TenantId=1)
    - `B-ngoai-pham-vi` = Denied: Bị từ chối đọc danh sách khách: UnauthorizedAccessException
- **D19 · OP18 · Admin → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối tạo hồ sơ khách thuê: Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue.
    - `default` = Denied: Bị từ chối tạo hồ sơ khách thuê: Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue.
- **E19 · OP18 · ChuTro → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Chủ trọ tạo hồ sơ khách thuê thành công (TenantId=14)
    - `default` = Allowed: Chủ trọ tạo hồ sơ khách thuê thành công (TenantId=14)
- **F19 · OP18 · QuanLy → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: SW-04: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý tạo hồ sơ khách có hiệu lực ngay (hiệu lực DB: 'tenants=28;rooms=35;contracts=16;invoices=8;incidents=13;cat…' -> 'tenants=29;rooms=35;contracts=16;invoices=8;incidents=13;cat…'; yêu cầu chờ duyệt SW-04: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: chờ duyệt: ApprovalRequired (hiệu lực DB: 'tenants=29;rooms=35;contracts=16;invoices=8;incidents=13;cat…' -> 'tenants=29;rooms=35;contracts=16;invoices=8;incidents=13;cat…'; yêu cầu chờ duyệt SW-04: 0 -> 0)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Bạn không có quyền thực hiện thao tác này (hiệu lực DB: 'tenants=29;rooms=35;contracts=16;invoices=8;incidents=13;cat…' -> 'tenants=29;rooms=35;contracts=16;invoices=8;incidents=13;cat…'; yêu cầu chờ duyệt SW-04: 0 -> 0)
- **G19 · OP18 · KhachThue → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối tạo hồ sơ khách thuê: Bạn không có quyền thực hiện thao tác này
    - `default` = Denied: Bị từ chối tạo hồ sơ khách thuê: Bạn không có quyền thực hiện thao tác này
- **D20 · OP19 · Admin → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Admin bị từ chối sửa hồ sơ khách thuê: Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue.
    - `default` = Denied: Admin bị từ chối sửa hồ sơ khách thuê: Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue.
- **E20 · OP19 · ChuTro → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Chủ trọ sửa hồ sơ khách có hiệu lực thật trong DB (Khach A Master Update/0900112233/del=False/active=True)
    - `default` = Allowed: Chủ trọ sửa hồ sơ khách có hiệu lực thật trong DB (Khach A Master Update/0900112233/del=False/active=True)
- **F20 · OP19 · QuanLy → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: SW-05: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý sửa hồ sơ khách có hiệu lực ngay (hiệu lực DB: 'Khach A Master Update/0900112233/del=False/active=True' -> 'Khach A Switch Edit/0900112233/del=False/active=True'; yêu cầu chờ duyệt SW-05: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: chờ duyệt: ApprovalRequired (hiệu lực DB: 'Khach A Switch Edit/0900112233/del=False/active=True' -> 'Khach A Switch Edit/0900112233/del=False/active=True'; yêu cầu chờ duyệt SW-05: 0 -> 0)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Bạn không có quyền thực hiện thao tác này (hiệu lực DB: 'Khach A Switch Edit/0900112233/del=False/active=True' -> 'Khach A Switch Edit/0900112233/del=False/active=True'; yêu cầu chờ duyệt SW-05: 0 -> 0)
- **G20 · OP19 · KhachThue → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: sửa hồ sơ mình // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: sửa hồ sơ mình
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: sửa hồ sơ mình
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: sửa hồ sơ mình
- **D21 · OP20 · Admin → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối đưa hồ sơ khách vào thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đưa hồ sơ khách vào thùng rác: UnauthorizedAccessException
- **E21 · OP20 · ChuTro → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Chủ trọ đưa hồ sơ khách vào thùng rác có hiệu lực thật (Khach can tai khoan 17/0910000017/del=True/active=True)
    - `default` = Allowed: Chủ trọ đưa hồ sơ khách vào thùng rác có hiệu lực thật (Khach can tai khoan 17/0910000017/del=True/active=True)
- **F21 · OP20 · QuanLy → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: SW-06: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý xóa hồ sơ khách có hiệu lực ngay (hiệu lực DB: 'Khach can tai khoan 4/0910000004/del=False/active=True' -> 'Khach can tai khoan 4/0910000004/del=True/active=True'; yêu cầu chờ duyệt SW-06: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: chờ duyệt: ApprovalRequired (hiệu lực DB: 'Khach can tai khoan 5/0910000005/del=False/active=True' -> 'Khach can tai khoan 5/0910000005/del=False/active=True'; yêu cầu chờ duyệt SW-06: 0 -> 1)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Bạn không có quyền thực hiện thao tác này (hiệu lực DB: 'Khach can tai khoan 6/0910000006/del=False/active=True' -> 'Khach can tai khoan 6/0910000006/del=False/active=True'; yêu cầu chờ duyệt SW-06: 1 -> 1)
- **G21 · OP20 · KhachThue → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối đưa hồ sơ khách vào thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối đưa hồ sơ khách vào thùng rác: UnauthorizedAccessException
- **D22 · OP21 · Admin → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối khôi phục khách thuê từ thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối khôi phục khách thuê từ thùng rác: UnauthorizedAccessException
- **E22 · OP21 · ChuTro → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Chủ trọ khôi phục hồ sơ khách từ thùng rác có hiệu lực thật (Khach can tai khoan 57/0910000057/del=False/active=True)
    - `default` = Allowed: Chủ trọ khôi phục hồ sơ khách từ thùng rác có hiệu lực thật (Khach can tai khoan 57/0910000057/del=False/active=True)
- **F22 · OP21 · QuanLy → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối khôi phục khách thuê từ thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối khôi phục khách thuê từ thùng rác: UnauthorizedAccessException
- **G22 · OP21 · KhachThue → PASS** (tầng Service, service `Web.Features.Tenants.TenantService`)
  - Bằng chứng: Bị từ chối khôi phục khách thuê từ thùng rác: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối khôi phục khách thuê từ thùng rác: UnauthorizedAccessException
- **D23 · OP22 · Admin → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Admin bị từ chối truy cập hợp đồng P4 (D1 BusinessGuard): Bạn không có quyền thực hiện thao tác hợp đồng này.
    - `default` = Denied: Admin bị từ chối truy cập hợp đồng P4 (D1 BusinessGuard): Bạn không có quyền thực hiện thao tác hợp đồng này.
- **E23 · OP22 · ChuTro → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Chủ trọ xem toàn bộ danh mục hợp đồng (1 hợp đồng)
    - `default` = Allowed: Chủ trọ xem toàn bộ danh mục hợp đồng (1 hợp đồng)
- **F23 · OP22 · QuanLy → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Quản lý xem được 5 hợp đồng của nhà A được giao và không thấy hợp đồng nhà B
    - `default` = Allowed: Quản lý xem được 5 hợp đồng của nhà A được giao và không thấy hợp đồng nhà B
- **G23 · OP22 · KhachThue → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: xem hợp đồng mình // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: xem hợp đồng mình
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: xem hợp đồng mình
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: xem hợp đồng mình
- **D24 · OP23 · Admin → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối tạo hợp đồng, không phát sinh dòng mới: ContractAuthorizationException
    - `default` = Denied: Bị từ chối tạo hợp đồng, không phát sinh dòng mới: ContractAuthorizationException
- **E24 · OP23 · ChuTro → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Hợp đồng tạo ra ở trạng thái Chờ duyệt (Status=Pending)
    - `default` = PendingApproval: Hợp đồng tạo ra ở trạng thái Chờ duyệt (Status=Pending)
- **F24 · OP23 · QuanLy → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Hợp đồng của Quản lý vào Chờ duyệt đúng D3 (ContractId=76181705-9ee6-47a2-8952-fc80db141028)
    - `default` = PendingApproval: Hợp đồng của Quản lý vào Chờ duyệt đúng D3 (ContractId=76181705-9ee6-47a2-8952-fc80db141028)
- **G24 · OP23 · KhachThue → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối tạo hợp đồng, không phát sinh dòng mới: ContractAuthorizationException
    - `default` = Denied: Bị từ chối tạo hợp đồng, không phát sinh dòng mới: ContractAuthorizationException
- **D25 · OP24 · Admin → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối duyệt hợp đồng, trạng thái vẫn Pending: ContractAuthorizationException
    - `default` = Denied: Bị từ chối duyệt hợp đồng, trạng thái vẫn Pending: ContractAuthorizationException
- **E25 · OP24 · ChuTro → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Chủ trọ duyệt hợp đồng có hiệu lực thật (Status=Approved, ContractResponseDto)
    - `default` = Allowed: Chủ trọ duyệt hợp đồng có hiệu lực thật (Status=Approved, ContractResponseDto)
- **F25 · OP24 · QuanLy → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối duyệt hợp đồng, trạng thái vẫn Pending: ContractAuthorizationException
    - `default` = Denied: Bị từ chối duyệt hợp đồng, trạng thái vẫn Pending: ContractAuthorizationException
- **G25 · OP24 · KhachThue → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối duyệt hợp đồng, trạng thái vẫn Pending: ContractAuthorizationException
    - `default` = Denied: Bị từ chối duyệt hợp đồng, trạng thái vẫn Pending: ContractAuthorizationException
- **D26 · OP25 · Admin → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối thanh lý / gia hạn hợp đồng, trạng thái vẫn Approved: ContractAuthorizationException
    - `default` = Denied: Bị từ chối thanh lý / gia hạn hợp đồng, trạng thái vẫn Approved: ContractAuthorizationException
- **E26 · OP25 · ChuTro → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Chủ trọ thanh lý hợp đồng có hiệu lực ngay (Status=Liquidated)
    - `default` = Allowed: Chủ trọ thanh lý hợp đồng có hiệu lực ngay (Status=Liquidated)
- **F26 · OP25 · QuanLy → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: SW-07: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Service chấp nhận lệnh thanh lý hợp đồng a52ce5fe-43c1-45a3-aeb6-db614605c82a (hiệu lực DB: 'Approved' -> 'Liquidated'; yêu cầu chờ duyệt SW-07: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: Service chấp nhận lệnh thanh lý hợp đồng 5a31d1e1-d264-4343-92d0-7900eef87660 (hiệu lực DB: 'Approved' -> 'Approved'; yêu cầu chờ duyệt SW-07: 0 -> 1)
    - `mode:Không thể` = Denied: Bị chặn thanh lý: ContractOperationException (hiệu lực DB: 'Approved' -> 'Approved'; yêu cầu chờ duyệt SW-07: 1 -> 1)
- **G26 · OP25 · KhachThue → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Bị từ chối thanh lý / gia hạn hợp đồng, trạng thái vẫn Approved: ContractAuthorizationException
    - `default` = Denied: Bị từ chối thanh lý / gia hạn hợp đồng, trạng thái vẫn Approved: ContractAuthorizationException
- **D27 · OP26 · Admin → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Admin bị từ chối xem tiền cọc hợp đồng: Bạn không có quyền thực hiện thao tác hợp đồng này.
    - `default` = Denied: Admin bị từ chối xem tiền cọc hợp đồng: Bạn không có quyền thực hiện thao tác hợp đồng này.
- **E27 · OP26 · ChuTro → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Xem tiền cọc thành công (375,000 đ)
    - `default` = Allowed: Xem tiền cọc thành công (375,000 đ)
- **F27 · OP26 · QuanLy → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Xem tiền cọc thành công (375,000 đ)
    - `default` = Allowed: Xem tiền cọc thành công (375,000 đ)
- **G27 · OP26 · KhachThue → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: xem cọc hợp đồng mình // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: xem cọc hợp đồng mình
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: xem cọc hợp đồng mình
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: xem cọc hợp đồng mình
- **D28 · OP27 · Admin → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: ContractAuthorizationException: Bạn không có quyền thực hiện thao tác hợp đồng này.
    - `default` = Denied: ContractAuthorizationException: Bạn không có quyền thực hiện thao tác hợp đồng này.
- **E28 · OP27 · ChuTro → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: Ghi nhận tiền cọc 300,000 -> 325,000 đ
    - `default` = Allowed: Ghi nhận tiền cọc 300,000 -> 325,000 đ
- **F28 · OP27 · QuanLy → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: SW-08: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Áp dụng ngay tiền cọc 375,000 đ (hiệu lực DB: '325000.00' -> '375000.00'; yêu cầu chờ duyệt SW-08: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: Đã tạo yêu cầu duyệt SW-08 (hiệu lực DB: '375000.00' -> '375000.00'; yêu cầu chờ duyệt SW-08: 0 -> 1)
    - `mode:Không thể` = Denied: ContractOperationException: Công tắc SW-07 đang chặn thao tác gia hạn / thanh lý hợp đồng. (hiệu lực DB: '375000.00' -> '375000.00'; yêu cầu chờ duyệt SW-08: 1 -> 1)
- **G28 · OP27 · KhachThue → PASS** (tầng Service, service `Web.Features.Contracts.Services.ContractService`)
  - Bằng chứng: ContractAuthorizationException: Bạn không có quyền thực hiện thao tác hợp đồng này.
    - `default` = Denied: ContractAuthorizationException: Bạn không có quyền thực hiện thao tác hợp đồng này.
- **D29 · OP28 · Admin → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **E29 · OP28 · ChuTro → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Đọc 4 dòng chỉ số/chi phí trong phạm vi.
    - `default` = Allowed: Đọc 4 dòng chỉ số/chi phí trong phạm vi.
- **F29 · OP28 · QuanLy → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Đọc 9 dòng chỉ số/chi phí trong phạm vi.
    - `default` = Allowed: Đọc 9 dòng chỉ số/chi phí trong phạm vi.
- **G29 · OP28 · KhachThue → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: xem chỉ số phòng mình // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: xem chỉ số phòng mình
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: xem chỉ số phòng mình
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: xem chỉ số phòng mình
- **D30 · OP29 · Admin → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **E30 · OP29 · ChuTro → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Ghi và đọc lại được chỉ số phòng 9.
    - `default` = Allowed: Ghi và đọc lại được chỉ số phòng 9.
- **F30 · OP29 · QuanLy → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Ghi và đọc lại được chỉ số phòng 40.
    - `default` = Allowed: Ghi và đọc lại được chỉ số phòng 40.
- **G30 · OP29 · KhachThue → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **D31 · OP30 · Admin → NOT_APPLICABLE** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **E31 · OP30 · ChuTro → NOT_APPLICABLE** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **F31 · OP30 · QuanLy → NOT_APPLICABLE** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **G31 · OP30 · KhachThue → NOT_APPLICABLE** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **D32 · OP31 · Admin → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **E32 · OP31 · ChuTro → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Cập nhật chỉ số kỳ liền trước và ghi lịch sử.
    - `default` = Allowed: Cập nhật chỉ số kỳ liền trước và ghi lịch sử.
- **F32 · OP31 · QuanLy → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: SW-09: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Áp dụng ngay chỉ số mới 310/105. (hiệu lực DB: '300.00' -> '310.00'; yêu cầu chờ duyệt SW-09: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: UtilityReadingException: Sửa chỉ số đang chờ Chủ trọ duyệt. (hiệu lực DB: '310.00' -> '310.00'; yêu cầu chờ duyệt SW-09: 0 -> 1)
    - `mode:Không thể` = Denied: UtilityReadingException: Công tắc SW-09 đang chặn sửa chỉ số kỳ trước. (hiệu lực DB: '310.00' -> '310.00'; yêu cầu chờ duyệt SW-09: 1 -> 1)
- **G32 · OP31 · KhachThue → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **D33 · OP32 · Admin → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **E33 · OP32 · ChuTro → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Đọc 1 dòng lịch sử sửa chỉ số.
    - `default` = Allowed: Đọc 1 dòng lịch sử sửa chỉ số.
- **F33 · OP32 · QuanLy → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: xem lịch sử sửa chỉ số // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: xem lịch sử sửa chỉ số
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: xem lịch sử sửa chỉ số
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: xem lịch sử sửa chỉ số
- **G33 · OP32 · KhachThue → PASS** (tầng Service, service `Web.Features.Utilities.Services.UtilityReadingService`)
  - Bằng chứng: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
    - `default` = Denied: UtilityReadingAuthorizationException: Không có quyền thực hiện thao tác chỉ số điện/nước.
- **D34 · OP33 · Admin → PASS** (tầng Service, service `Web.Features.Utilities.Services.ServiceCatalogService`)
  - Bằng chứng: Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: UnauthorizedAccessException
- **E34 · OP33 · ChuTro → PASS** (tầng Service, service `Web.Features.Utilities.Services.ServiceCatalogService`)
  - Bằng chứng: Chủ trọ thiết lập danh mục dịch vụ có hiệu lực thật (ServiceCatalogId=2, đơn giá 120.000đ).
    - `default` = Allowed: Chủ trọ thiết lập danh mục dịch vụ có hiệu lực thật (ServiceCatalogId=2, đơn giá 120.000đ).
- **F34 · OP33 · QuanLy → PASS** (tầng Service, service `Web.Features.Utilities.Services.ServiceCatalogService`)
  - Bằng chứng: Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: UnauthorizedAccessException
- **G34 · OP33 · KhachThue → PASS** (tầng Service, service `Web.Features.Utilities.Services.ServiceCatalogService`)
  - Bằng chứng: Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: UnauthorizedAccessException
    - `default` = Denied: Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: UnauthorizedAccessException
- **D35 · OP34 · Admin → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceQueryService`)
  - Bằng chứng: Admin bị từ chối truy cập hóa đơn P6 (D1 BusinessGuard): Vai trò này không được xem hóa đơn ngoài phạm vi được giao.
    - `default` = Denied: Admin bị từ chối truy cập hóa đơn P6 (D1 BusinessGuard): Vai trò này không được xem hóa đơn ngoài phạm vi được giao.
- **E35 · OP34 · ChuTro → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceQueryService`)
  - Bằng chứng: Chủ trọ xem toàn bộ hóa đơn (8 hóa đơn)
    - `default` = Allowed: Chủ trọ xem toàn bộ hóa đơn (8 hóa đơn)
- **F35 · OP34 · QuanLy → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceQueryService`)
  - Bằng chứng: Quản lý xem được 8 hóa đơn của nhà A được giao và không thấy hóa đơn nhà B
    - `default` = Allowed: Quản lý xem được 8 hóa đơn của nhà A được giao và không thấy hóa đơn nhà B
- **G35 · OP34 · KhachThue → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceQueryService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: 2 hóa đơn hợp đồng mình // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: 2 hóa đơn hợp đồng mình
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: 2 hóa đơn hợp đồng mình
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: 2 hóa đơn hợp đồng mình
- **D36 · OP35 · Admin → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: Bị từ chối lập hóa đơn, không phát sinh dòng mới: InvoiceAuthorizationException
    - `default` = Denied: Bị từ chối lập hóa đơn, không phát sinh dòng mới: InvoiceAuthorizationException
- **E36 · OP35 · ChuTro → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: Chủ trọ lập hóa đơn nháp có hiệu lực thật (InvoiceId=4, Status=Draft)
    - `default` = Allowed: Chủ trọ lập hóa đơn nháp có hiệu lực thật (InvoiceId=4, Status=Draft)
- **F36 · OP35 · QuanLy → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: SW-10: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Lập hóa đơn nháp có hiệu lực DB (InvoiceId=5) (hiệu lực DB: 'invoices=0' -> 'invoices=1'; yêu cầu chờ duyệt SW-10: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: InvoiceOperationException: Thao tác đang chờ Chủ trọ duyệt; hóa đơn chưa được ghi. (hiệu lực DB: 'invoices=0' -> 'invoices=0'; yêu cầu chờ duyệt SW-10: 0 -> 1)
    - `mode:Không thể` = Denied: InvoiceOperationException: Công tắc Quản lý đang chặn thao tác với hóa đơn. (hiệu lực DB: 'invoices=0' -> 'invoices=0'; yêu cầu chờ duyệt SW-10: 1 -> 1)
- **G36 · OP35 · KhachThue → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: Bị từ chối lập hóa đơn, không phát sinh dòng mới: InvoiceAuthorizationException
    - `default` = Denied: Bị từ chối lập hóa đơn, không phát sinh dòng mới: InvoiceAuthorizationException
- **D37 · OP36 · Admin → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: Bị từ chối phát hành hóa đơn, trạng thái vẫn Draft: InvoiceAuthorizationException
    - `default` = Denied: Bị từ chối phát hành hóa đơn, trạng thái vẫn Draft: InvoiceAuthorizationException
- **E37 · OP36 · ChuTro → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: Chủ trọ phát hành hóa đơn có hiệu lực thật (InvoiceId=3, Status=Issued)
    - `default` = Allowed: Chủ trọ phát hành hóa đơn có hiệu lực thật (InvoiceId=3, Status=Issued)
- **F37 · OP36 · QuanLy → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: SW-11: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Service chấp nhận lệnh phát hành hóa đơn 6 (hiệu lực DB: 'Draft' -> 'Issued'; yêu cầu chờ duyệt SW-11: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: InvoiceOperationException: Thao tác đang chờ Chủ trọ duyệt; hóa đơn chưa được ghi. (hiệu lực DB: 'Draft' -> 'Draft'; yêu cầu chờ duyệt SW-11: 0 -> 1)
    - `mode:Không thể` = Denied: InvoiceOperationException: Công tắc Quản lý đang chặn thao tác với hóa đơn. (hiệu lực DB: 'Draft' -> 'Draft'; yêu cầu chờ duyệt SW-11: 1 -> 1)
- **G37 · OP36 · KhachThue → PASS** (tầng Service, service `Web.Features.Invoices.InvoiceService`)
  - Bằng chứng: Bị từ chối phát hành hóa đơn, trạng thái vẫn Draft: InvoiceAuthorizationException
    - `default` = Denied: Bị từ chối phát hành hóa đơn, trạng thái vẫn Draft: InvoiceAuthorizationException
- **D38 · OP37 · Admin → NOT_APPLICABLE** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **E38 · OP37 · ChuTro → NOT_APPLICABLE** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **F38 · OP37 · QuanLy → NOT_APPLICABLE** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: Ô '—' — workbook đánh dấu không áp dụng (Mục VI.5)
- **G38 · OP37 · KhachThue → PASS** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: Khách thuê gửi được minh chứng thanh toán cho hóa đơn 2 (PaymentId=1, Pending).
    - `default` = Allowed: Khách thuê gửi được minh chứng thanh toán cho hóa đơn 2 (PaymentId=1, Pending).
- **D39 · OP38 · Admin → PASS** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: PaymentAuthorizationException: Bạn không có quyền thực hiện thao tác thanh toán này.
    - `default` = Denied: PaymentAuthorizationException: Bạn không có quyền thực hiện thao tác thanh toán này.
- **E39 · OP38 · ChuTro → PASS** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: Xác nhận thanh toán PaymentId=1 có hiệu lực thật (Confirmed).
    - `default` = Allowed: Xác nhận thanh toán PaymentId=1 có hiệu lực thật (Confirmed).
- **F39 · OP38 · QuanLy → PASS** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: Xác nhận thanh toán PaymentId=1 có hiệu lực thật (Confirmed).
    - `default` = Allowed: Xác nhận thanh toán PaymentId=1 có hiệu lực thật (Confirmed).
- **G39 · OP38 · KhachThue → PASS** (tầng Service, service `Web.Features.Invoices.Services.PaymentService`)
  - Bằng chứng: PaymentAuthorizationException: Bạn không có quyền thực hiện thao tác thanh toán này.
    - `default` = Denied: PaymentAuthorizationException: Bạn không có quyền thực hiện thao tác thanh toán này.
- **D40 · OP39 · Admin → PASS** (tầng Service, service `Web.Features.Reports.Services.RevenueReportService`)
  - Bằng chứng: RevenueReportAuthorizationException: Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
    - `default` = Denied: RevenueReportAuthorizationException: Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
- **E40 · OP39 · ChuTro → PASS** (tầng Service, service `Web.Features.Reports.Services.RevenueReportService`)
  - Bằng chứng: Chủ trọ đọc báo cáo doanh thu: 5 hóa đơn, thu 1,500,000 đ, công nợ 1,500,000 đ.
    - `default` = Allowed: Chủ trọ đọc báo cáo doanh thu: 5 hóa đơn, thu 1,500,000 đ, công nợ 1,500,000 đ.
- **F40 · OP39 · QuanLy → PASS** (tầng Service, service `Web.Features.Reports.Services.RevenueReportService`)
  - Bằng chứng: RevenueReportAuthorizationException: Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
    - `default` = Denied: RevenueReportAuthorizationException: Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
- **G40 · OP39 · KhachThue → PASS** (tầng Service, service `Web.Features.Reports.Services.RevenueReportService`)
  - Bằng chứng: RevenueReportAuthorizationException: Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
    - `default` = Denied: RevenueReportAuthorizationException: Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
- **D41 · OP40 · Admin → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Service sự cố trả 0 phiếu và từ chối đọc phiếu cụ thể cho vai trò Admin
    - `default` = Denied: Service sự cố trả 0 phiếu và từ chối đọc phiếu cụ thể cho vai trò Admin
- **E41 · OP40 · ChuTro → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Chủ trọ xem toàn bộ phiếu sự cố (1 phiếu)
    - `default` = Allowed: Chủ trọ xem toàn bộ phiếu sự cố (1 phiếu)
- **F41 · OP40 · QuanLy → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Quản lý đọc được 7 phiếu sự cố, gồm phiếu nhà A được giao và không thấy phiếu nhà khác
    - `default` = Allowed: Quản lý đọc được 7 phiếu sự cố, gồm phiếu nhà A được giao và không thấy phiếu nhà khác
- **G41 · OP40 · KhachThue → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Làm được trong phạm vi: đọc phiếu sự cố phòng mình / phòng khác // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: đọc phiếu sự cố phòng mình / phòng khác
    - `A-trong-pham-vi` = Allowed: Làm được trong phạm vi: đọc phiếu sự cố phòng mình / phòng khác
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: đọc phiếu sự cố phòng mình / phòng khác
- **D42 · OP41 · Admin → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Bị từ chối tạo phiếu sự cố (ErrorCode=Forbidden), DB không thêm dòng
    - `default` = Denied: Bị từ chối tạo phiếu sự cố (ErrorCode=Forbidden), DB không thêm dòng
- **E42 · OP41 · ChuTro → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: ChuTro tạo phiếu sự cố có hiệu lực thật (IncidentId=12)
    - `default` = Allowed: ChuTro tạo phiếu sự cố có hiệu lực thật (IncidentId=12)
- **F42 · OP41 · QuanLy → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: SW-12: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý tạo phiếu sự cố có hiệu lực ngay (hiệu lực DB: 'tenants=15;rooms=10;contracts=6;invoices=2;incidents=5;catal…' -> 'tenants=15;rooms=10;contracts=6;invoices=2;incidents=6;catal…'; yêu cầu chờ duyệt SW-12: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: Quản lý tạo phiếu sự cố có hiệu lực ngay (hiệu lực DB: 'tenants=15;rooms=10;contracts=6;invoices=2;incidents=6;catal…' -> 'tenants=15;rooms=10;contracts=6;invoices=2;incidents=6;catal…'; yêu cầu chờ duyệt SW-12: 0 -> 1)
    - `mode:Không thể` = Denied: ErrorCode=Forbidden (hiệu lực DB: 'tenants=15;rooms=10;contracts=6;invoices=2;incidents=6;catal…' -> 'tenants=15;rooms=10;contracts=6;invoices=2;incidents=6;catal…'; yêu cầu chờ duyệt SW-12: 1 -> 1)
- **G42 · OP41 · KhachThue → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: KhachThue tạo phiếu sự cố có hiệu lực thật (IncidentId=7)
    - `default` = Allowed: KhachThue tạo phiếu sự cố có hiệu lực thật (IncidentId=7)
- **D43 · OP42 · Admin → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Bị từ chối cập nhật trạng thái sự cố (ErrorCode=Forbidden), dữ liệu không đổi
    - `default` = Denied: Bị từ chối cập nhật trạng thái sự cố (ErrorCode=Forbidden), dữ liệu không đổi
- **E43 · OP42 · ChuTro → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Chủ trọ cập nhật trạng thái sự cố có hiệu lực thật (New/hist=0 -> InProgress/hist=0)
    - `default` = Allowed: Chủ trọ cập nhật trạng thái sự cố có hiệu lực thật (New/hist=0 -> InProgress/hist=0)
- **F43 · OP42 · QuanLy → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: SW-13: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý cập nhật trạng thái sự cố có hiệu lực ngay (hiệu lực DB: 'New/hist=0' -> 'InProgress/hist=0'; yêu cầu chờ duyệt SW-13: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: Quản lý cập nhật trạng thái sự cố có hiệu lực ngay (hiệu lực DB: 'New/hist=0' -> 'New/hist=0'; yêu cầu chờ duyệt SW-13: 0 -> 1)
    - `mode:Không thể` = Denied: ErrorCode=Forbidden (hiệu lực DB: 'New/hist=0' -> 'New/hist=0'; yêu cầu chờ duyệt SW-13: 1 -> 1)
- **G43 · OP42 · KhachThue → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Bị từ chối cập nhật trạng thái sự cố (ErrorCode=Forbidden), dữ liệu không đổi
    - `default` = Denied: Bị từ chối cập nhật trạng thái sự cố (ErrorCode=Forbidden), dữ liệu không đổi
- **D44 · OP43 · Admin → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Bị từ chối đính kèm ảnh kết quả (ErrorCode=Forbidden), DB không gắn thêm ảnh nào
    - `default` = Denied: Bị từ chối đính kèm ảnh kết quả (ErrorCode=Forbidden), DB không gắn thêm ảnh nào
- **E44 · OP43 · ChuTro → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Chủ trọ đính kèm ảnh kết quả có hiệu lực thật (FileId=74601a29-7a6f-463b-b8e4-c3b1e5b0c9ea, ảnh gắn vào phiếu=attached=3)
    - `default` = Allowed: Chủ trọ đính kèm ảnh kết quả có hiệu lực thật (FileId=74601a29-7a6f-463b-b8e4-c3b1e5b0c9ea, ảnh gắn vào phiếu=attached=3)
- **F44 · OP43 · QuanLy → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: SW-14: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: ảnh kết quả gắn thẳng vào phiếu 9 (FileId=274c9c37-504d-40a6-8c24-02d364cb5d9c) (hiệu lực DB: 'incident=9 attached=0' -> 'incident=9 attached=1'; yêu cầu chờ duyệt SW-14: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: phiếu 10 tạo yêu cầu chờ duyệt 326d00d0-cad4-43a4-9c49-bca42a2db887 (FileId=e5fa4e75-450b-4112-abad-286278aaa1e1) (hiệu lực DB: 'incident=10 attached=0' -> 'incident=10 attached=0'; yêu cầu chờ duyệt SW-14: 0 -> 1)
    - `mode:Không thể` = Denied: ErrorCode=Forbidden (hiệu lực DB: 'incident=11 attached=0' -> 'incident=11 attached=0'; yêu cầu chờ duyệt SW-14: 1 -> 1)
- **G44 · OP43 · KhachThue → PASS** (tầng Service, service `Web.Features.Maintenance.IncidentService`)
  - Bằng chứng: Case A (A-trong-pham-vi): Khách thuê đọc được ảnh của phiếu thuộc phòng mình đang thuê // Case B (B-ngoai-pham-vi): Bị chặn ngoài phạm vi đúng: access=None, đính kèm ảnh bị từ chối (Forbidden)
    - `A-trong-pham-vi` = Allowed: Khách thuê đọc được ảnh của phiếu thuộc phòng mình đang thuê
    - `B-ngoai-pham-vi` = Denied: Bị chặn ngoài phạm vi đúng: access=None, đính kèm ảnh bị từ chối (Forbidden)
- **D45 · OP44 · Admin → PASS** (tầng Service, service `Web.Features.Reports.RoomReportService`)
  - Bằng chứng: Bị từ chối xem báo cáo tổng hợp: Khong duoc xem bao cao phong.
    - `default` = Denied: Bị từ chối xem báo cáo tổng hợp: Khong duoc xem bao cao phong.
- **E45 · OP44 · ChuTro → PASS** (tầng Service, service `Web.Features.Reports.RoomReportService`)
  - Bằng chứng: Chủ trọ xem báo cáo tỷ lệ lấp đầy và tổng quan thành công
    - `default` = Allowed: Chủ trọ xem báo cáo tỷ lệ lấp đầy và tổng quan thành công
- **F45 · OP44 · QuanLy → PASS** (tầng Service, service `Web.Features.Reports.RoomReportService`)
  - Bằng chứng: SW-15: Cho phép -> Allowed; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Quản lý xem báo cáo phòng theo phạm vi được giao (hiệu lực DB: 'n/a' -> 'n/a'; yêu cầu chờ duyệt SW-15: 0 -> 0)
    - `mode:Không thể` = Denied: UnauthorizedAccessException: Khong duoc xem bao cao phong. (hiệu lực DB: 'n/a' -> 'n/a'; yêu cầu chờ duyệt SW-15: 0 -> 0)
- **G45 · OP44 · KhachThue → PASS** (tầng Service, service `Web.Features.Reports.RoomReportService`)
  - Bằng chứng: Bị từ chối xem báo cáo tổng hợp: Khong duoc xem bao cao phong.
    - `default` = Denied: Bị từ chối xem báo cáo tổng hợp: Khong duoc xem bao cao phong.
- **D46 · OP45 · Admin → PASS** (tầng Service, service `Web.Features.Reports.Services.ReportExportService`)
  - Bằng chứng: ReportExportAuthorizationException: Chỉ Chủ trọ hoặc Quản lý được xuất báo cáo Excel.
    - `default` = Denied: ReportExportAuthorizationException: Chỉ Chủ trọ hoặc Quản lý được xuất báo cáo Excel.
- **E46 · OP45 · ChuTro → PASS** (tầng Service, service `Web.Features.Reports.Services.ReportExportService`)
  - Bằng chứng: Chủ trọ xuất Excel hợp lệ (6762 byte, chữ ký ZIP).
    - `default` = Allowed: Chủ trọ xuất Excel hợp lệ (6762 byte, chữ ký ZIP).
- **F46 · OP45 · QuanLy → PASS** (tầng Service, service `Web.Features.Reports.Services.ReportExportService`)
  - Bằng chứng: SW-16: Cho phép -> Allowed; Duyệt -> PendingApproval; Không thể -> Denied
    - `mode:Cho phép` = Allowed: Xuất Excel 6572 byte (hiệu lực DB: 'n/a' -> 'n/a'; yêu cầu chờ duyệt SW-16: 0 -> 0)
    - `mode:Duyệt` = PendingApproval: ReportExportException: Xuất báo cáo đang chờ Chủ trọ duyệt. (hiệu lực DB: 'n/a' -> 'n/a'; yêu cầu chờ duyệt SW-16: 0 -> 1)
    - `mode:Không thể` = Denied: ReportExportException: Công tắc SW-16 đang chặn xuất báo cáo. (hiệu lực DB: 'n/a' -> 'n/a'; yêu cầu chờ duyệt SW-16: 1 -> 1)
- **G46 · OP45 · KhachThue → PASS** (tầng Service, service `Web.Features.Reports.Services.ReportExportService`)
  - Bằng chứng: ReportExportAuthorizationException: Chỉ Chủ trọ hoặc Quản lý được xuất báo cáo Excel.
    - `default` = Denied: ReportExportAuthorizationException: Chỉ Chủ trọ hoặc Quản lý được xuất báo cáo Excel.

</details>

## D. Danh sách BUG (ô FAIL)

Không có ô FAIL: mọi ô đã thực thi đều khớp kỳ vọng của workbook.

## E. Danh sách BLOCKED

Không có ô BLOCKED: toàn bộ 173 ô khả dụng đều gọi backend thật và PASS.
`PermissionMatrixProbeExecutor.UnsupportedCells` rỗng và được guard
`No_Cell_Is_Declared_Unsupported` + `No_Forbidden_Cell_Is_Declared_Unsupported` giữ ở trạng thái đó.

## F. Chú thích legend (nguyên văn từ workbook)

- **A48** (sheet Ma trận quyền): Chú thích ⚙️ (Quản lý): theo công tắc chung do Chủ trọ đặt — Cho phép (có hiệu lực ngay) / Duyệt (chờ Chủ trọ duyệt, mặc định) / Không thể. Chi tiết 16 công tắc ở sheet Công tắc Quản lý. 'Xem báo cáo' chỉ có Cho phép / Không thể.
- **A49** (sheet Ma trận quyền): Cố định, không theo công tắc: duyệt/kích hoạt/khóa tài khoản (Chủ trọ cho Quản lý/Khách; Admin cho mọi tài khoản), sửa/cấp role (chỉ Admin), duyệt/từ chối hợp đồng (chỉ Chủ trọ), tạo hợp đồng của Quản lý (luôn Chờ duyệt), xác nhận thanh toán của Quản lý. Thao tác của Chủ trọ luôn có hiệu lực ngay.
- **A50** (sheet Ma trận quyền): Admin là vai trò hệ thống (D1): không có quyền nghiệp vụ P2–P8. Mọi ô ❌ của bất kỳ vai trò nào là case cấm khi test.
- Sheet Công tắc Quản lý: Ý nghĩa chế độ: Cho phép = hiệu lực ngay; Duyệt = tạo yêu cầu Chờ duyệt, chỉ có hiệu lực sau khi Chủ trọ duyệt; Không thể = bị chặn. Thao tác của Chủ trọ luôn hiệu lực ngay.
- Sheet Công tắc Quản lý: Lưu ý vận hành: với mặc định Duyệt, các thao tác thường ngày như Cập nhật trạng thái xử lý (SW-13) sẽ chờ Chủ trọ; Chủ trọ nên chuyển sang Cho phép cho các thao tác tin cậy. Khi demo, thiết lập sẵn cả ba chế độ.

| Ký hiệu | Kỳ vọng quyền | Số ô |
|---|---|---:|
| ❌ | DENY | 76 |
| ✅ | ALLOW | 67 |
| ⚠️ | CONDITIONAL | 14 |
| ⚙️ | SPECIAL_MODES | 16 |
| — | NOT_APPLICABLE | 7 |

## G. Quyết định nghiệp vụ (sheet Quyết định, nguyên văn)

| Mã | Vấn đề | Đề xuất / chốt |
|---|---|---|
| D1 | Admin làm gì | Admin là vai trò hệ thống: tạo/khóa/mở khóa mọi tài khoản, cấp/đổi vai trò (kể cả Chủ trọ), xem dữ liệu tài khoản. Không có quyền nghiệp vụ P2–P8. |
| D2 | Số Chủ trọ | Một Chủ trọ (phạm vi toàn hệ thống = toàn bộ dữ liệu của chủ trọ đó). |
| D3 | Quản lý tạo hợp đồng | Bỏ công tắc ở Tạo hợp đồng: Quản lý luôn tạo được, hợp đồng vào trạng thái Chờ duyệt; bước Duyệt hợp đồng (chỉ Chủ trọ) là kiểm soát duy nhất. |
| D4 | Chủ trọ tự tạo + tự duyệt | Cho phép (Chủ trọ là cấp cao nhất; thao tác của Chủ trọ luôn có hiệu lực ngay). Lệnh cấm tự duyệt chỉ áp dụng cho Quản lý. |
| D5 | Xem doanh thu / công nợ | Báo cáo tổng hợp chỉ Chủ trọ. Quản lý xem công nợ từng hóa đơn qua dòng Xem hóa đơn. |
| D6 | Xác nhận thanh toán | Quản lý luôn xác nhận được (cố định, không công tắc). |
| D7 | Đơn giá | Điện/nước: biểu giá theo quy định nhà nước, cấu hình sẵn, khóa, hiệu lực theo ngày. Dịch vụ khác: Chủ trọ tự thiết lập. |
| D8 | Excel / PDF | Excel vào kế hoạch (chỉ Xuất báo cáo P8, ClosedXML). PDF vẫn ngoài kế hoạch. |
| D9 | Trường khách thuê Quản lý được xem | Họ tên, SĐT, phòng, trạng thái; che số CCCD; giấy tờ chỉ xem trong tòa nhà được giao, không xuất. |
| D10 | Sửa chỉ số kỳ đã chốt | Chỉ sửa kỳ liền trước, chỉ khi chưa phát hành hóa đơn; bắt buộc lưu lịch sử. Kỳ đã phát hành: chặn. |
| D11 | Quy tắc sprint | Sprint = nhịp bàn giao đầu ra chính; các nhịp sau trong timeline là kiểm lại. |
| D12 | Công tắc Quản lý | 16 thao tác ⚙️, mặc định Duyệt, phạm vi toàn hệ thống. Xem báo cáo chỉ Cho phép/Không thể, mặc định Cho phép. |
| D13 | Thanh toán một phần / từ chối / hủy | Không trả một phần; có từ chối minh chứng (khách gửi lại); không hủy hóa đơn trong v1.0. |
| D14 | Phiếu sự cố | Giữ đúng thiết kế: không luồng duyệt chi phí sửa chữa, không khiếu nại, không trạng thái Chờ vật tư/Đã hủy trừ khi chốt thêm. |
| D15 | Trang tổng quan của Khách | Cho phép trang tổng quan cá nhân từ service P5/P6/P7, không thuộc P8 và không tính vào nghiệm thu P8. |
| D16 | Xem nhật ký thao tác (audit) | Có màn hình (đề xuất): Admin xem nhật ký tài khoản/hệ thống; Chủ trọ xem nhật ký nghiệp vụ. Ô ma trận để ❓ đến khi chốt. |
| D17 | Quy cách minh chứng thanh toán | Tệp ảnh/PDF theo giới hạn tệp chung; bắt buộc số tiền và thời điểm chuyển, mã giao dịch tùy chọn. Ô để ❓. |

### G.1 Công tắc Quản lý và ô ⚙️ tương ứng

| Mã | Ô | Thao tác | Chế độ | Mặc định | Ghi chú |
|---|---|---|---|---|---|
| SW-01 | F13 | OP12 Sửa thông tin phòng | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-02 | F14 | OP13 Thiết lập / đổi giá phòng | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-03 | F15 | OP14 Đổi trạng thái phòng | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-04 | F19 | OP18 Tạo hồ sơ khách thuê | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-05 | F20 | OP19 Sửa hồ sơ khách thuê | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-06 | F21 | OP20 Xóa hồ sơ khách thuê (vào thùng rác) | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-07 | F26 | OP25 Gia hạn / thanh lý hợp đồng | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-08 | F28 | OP27 Cập nhật tiền cọc | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-09 | F32 | OP31 Sửa chỉ số kỳ liền trước | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-10 | F36 | OP35 Tạo / lập hóa đơn | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-11 | F37 | OP36 Phát hành hóa đơn | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-12 | F42 | OP41 Tạo phiếu bảo trì | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-13 | F43 | OP42 Cập nhật trạng thái xử lý | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-14 | F44 | OP43 Đính kèm ảnh kết quả | Cho phép / Duyệt / Không thể | Duyệt |  |
| SW-15 | F45 | OP44 Xem báo cáo | Cho phép / Không thể | Cho phép | Công tắc phụ theo mục: SW-15a phòng, SW-15b khách thuê, SW-15c hợp đồng, SW-15d chi phí điện nước/dịch vụ, SW-15e sự cố. Doanh thu/công nợ tổng hợp luôn khóa với Quản lý (D5). |
| SW-16 | F46 | OP45 Xuất báo cáo (Excel) | Cho phép / Duyệt / Không thể | Duyệt | Excel; không xuất CCCD |

## H. Hạn chế của bộ kiểm thử

- Tầng kiểm tra ghi ở cột `Tầng`: `Service` là gọi service nghiệp vụ thật trên database test riêng và đọc lại DB để xác nhận hiệu lực ghi; `Policy` là mới chứng minh tới cổng quyền/công tắc; `SampleState` là trạng thái mẫu trong bộ nhớ. Ô ở tầng Policy/SampleState **không được tính là đã chứng minh thao tác nghiệp vụ** và chỉ xuất hiện khi workbook cho phép hoặc ô ❌.
- Chưa có `WebApplicationFactory` cho toàn bộ phân hệ nên ô chưa được chứng minh qua HTTP/form thật; đây là khoảng trống của VHT-101 lần này, không phải kết luận 'đạt'.
- Không mock bộ phân quyền đang test: probe gọi thẳng service thật, ngoại lệ chỉ là dữ liệu nền do `PermissionMatrixWorld` dựng có chủ đích (2 tòa, 6 tài khoản) để tạo tình huống trong/ngoài phạm vi.
- Không sửa workbook, không sửa logic nghiệp vụ/UI để làm báo cáo đẹp hơn. Khi code và ma trận khác nhau, ô ghi FAIL và mô tả bug ở mục D.
- Kỳ vọng lấy nguyên văn từ manifest sinh bởi `scripts/permission_matrix/build_manifest.py`; guard hash bắt buộc chạy lại script khi workbook đổi.
- Credential đã redact khỏi evidence và ba file báo cáo (guard `EnsureNoCredentials`); database kiểm thử là `NhaTro_It_*` tách biệt, không dùng tài khoản thật.
- 7 ô `—` không chạy probe; test `Cell_Not_Applicable_Is_Excluded_From_Execution` chỉ xác nhận manifest đánh dấu đúng.

