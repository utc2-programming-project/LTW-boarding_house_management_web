# Quy tac lam viec cho AI agent

## Nhanh (bat buoc)

- KHONG checkout / pull / commit / push truc tiep tren nhanh `main`.
- Nhanh hoat dong chinh la `dev`. Moi viec tao nhanh feature tu `dev`.
- Dat ten nhanh khong dau, vi du: `VHT-6-hoan-thien-role-va-pham-vi-nguoi-dung`.
- Truoc khi doi nhanh ma co thay doi chua commit: stash hoac commit, khong `reset --hard`, khong `checkout` de mat WIP.
- Day nhanh feature len remote va mo PR ve `dev`, khong merge thang vao `main`.
- Không được tự ý push lên, phải hỏi ý.

## Docs (`docs/` — AI tra cứu khi cần, không đọc hết)

- `architecture/README.md`: điểm bắt đầu, dẫn tới mọi tài liệu khác.
- `architecture/cau_truc_thu_muc.md`: cấu trúc thư mục hiện tại + mở rộng, đặt code ở đâu.
- `thiet_ke_he_thong.md`: luồng nghiệp vụ P1–P8, kho D1–D7. Chỉ luồng dữ liệu, không có API/DB/UI.
- `design/phong_cach_giao_dien.md`: chuẩn UI (font, màu, layout, song ngữ). Code UI phải theo.
- `design/database/`: ERD/SQL/SVG chuẩn (`so_do_quan_he_vht5.*`). Không tự suy luận schema.
- `tech-stack/cong_nghe_su_dung.md`: bộ công nghệ duy nhất được dùng.
- `task/cong_viec_theo_tuan.md`: kế hoạch 4 tuần, mã Jira `VHT-n` là chuẩn.
- `task/timeline_song_song.md`: ai làm song song với ai, việc nào chờ việc nào.
- `task/thao_tac_va_quyen.xlsx`: ma trận quyền theo vai trò — nguồn chuẩn, mô tả task phải khớp.
- `Imp/`: kế hoạch chi tiết task **đang làm** (VD `VHT-27-*.md`). Task xong chuyển sang `archive/imp/`. Làm task nào đọc file đó.
- `operations/secrets.md`: quản lý secret (User Secrets / env), cấm commit secret.
- `report/dan_y_bao_cao_word.md`: dàn ý báo cáo Word.
- `quy_uoc_ngay_tien_gio.md`: format ngày/tiền/giờ chung, dùng hàm có sẵn, không tự `ToString`.
- Bỏ qua: `archive/`, `image/`, `*.docx`, `*.excalidraw` (file sinh ra / ảnh minh họa).

## Kiem thu (tranh lam nang repo)

- Thay doi nho (1–2 file, config/log/UI nhỏ, sửa lỗi cục bộ): **không viết thêm test**. Chỉ cần `dotnet build`, chạy app và xem log.
- Chỉ viết nhiều test khi thay đổi **lan rộng**: nhiều file, nhiều tầng, hoặc đụng nghiệp vụ tiền/quyền/dữ liệu, hoặc lỗi đã từng tái diễn.
- Không lặp cùng một hành vi ở cả Unit và Integration. Chọn tầng thấp nhất còn kiểm được: hàm thuần → Unit; cần HTTP/DB → Integration.
- Gộp test cùng nhóm vào một class/file, dùng chung fixture. Không tách mỗi test một class chỉ để có DB riêng (mỗi class là một DB fixture).
- Giữ test bảo vệ nghiệp vụ, tiền, phân quyền, bảo mật. Xóa/gộp test trùng lặp hoặc test chỉ kiểm hành vi framework.
- Không thêm test để đủ số lượng; mỗi test phải bảo vệ một rủi ro cụ thể.
