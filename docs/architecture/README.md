# Kiến trúc / Architecture

**Web vận hành nhà trọ / Boarding House Operations Web**

Repository: https://github.com/TinhSsc/boarding-house-management-web.git

## Đọc từ đây / Start here

- [Cây thư mục / Folder structure](cau_truc_thu_muc.md): cây hiện tại, cây mở rộng, vị trí code, transaction và Việt/Anh.
- [Công nghệ / Technology](../tech-stack/cong_nghe_su_dung.md).
- [Luồng nghiệp vụ / Business flows](../thiet_ke_he_thong.md).
- [Quy chuẩn giao diện / UI guidelines](../design/phong_cach_giao_dien.md).
- [Localization VI/EN và selector](localization.md).
- [Kế hoạch theo tuần / Weekly plan](../task/cong_viec_theo_tuan.md).

## Quy tắc cập nhật / Maintenance

- `cau_truc_thu_muc.md` là bản hiện hành. `cau_truc_thu_muc.docx` được giữ nguyên, **chưa đồng bộ với lần sửa này**; Phúc xuất lại khi chuẩn bị báo cáo.
- Phân biệt **folder đã tạo / scaffolded** và **chức năng đã triển khai / implemented**. Khung folder INIT giữ bằng `.gitkeep`; khi thêm/đổi vị trí code, sửa cây tương ứng và báo người dùng chung.
- Giữ solution `NhaTro/NhaTro.slnx`, project `NhaTro/Web/Web.csproj`, namespace `Web`; không tạo lại INIT.
- Folder rỗng trong khung INIT đã được tạo theo yêu cầu; `.gitkeep` chỉ giữ folder trong Git. Không thêm framework, class mẫu, dịch vụ hoặc nghiệp vụ chỉ vì có folder.

**English:** Use the Markdown structure document as the current reference. The Word copy has not been updated. Empty scaffold folders are tracked with `.gitkeep`; they do not represent implemented features. Keep the existing project layout.
