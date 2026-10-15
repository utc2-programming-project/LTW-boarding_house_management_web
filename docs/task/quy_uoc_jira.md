# Quy ước Jira / Jira conventions — project VHT

Tài liệu này là nguồn chuẩn cho Summary, Description, Labels, Components, Issue Type.
Mọi task phải khớp ma trận quyền `thao_tac_va_quyen.xlsx`.

## 1. Summary

- Tối đa 50 ký tự, một việc, động từ tiếng Việt đứng đầu + đối tượng + phạm vi (`P1–P8` nếu có).
- Cấm prefix `[nX]`, suffix `(tách từ...)`, slash dồn 3–4 việc, ngoặc nhét số liệu, viết tắt chưa định nghĩa (`YC, E2E, QL...`).
- Thuần Việt hóa động từ: Query→Dựng nguồn..., Test→Kiểm thử, Review→Rà soát, Fix→Sửa, Deploy→Triển khai/phát hành, Publish→Phát hành.
- Epic giữ mẫu `Pn · Tên phân hệ` (VD `P1 · Tài khoản và phân quyền`).
- Đợt thực hiện ghi ở field **Sprint**, nguồn tách ghi ở **Parent/Linked (Cloners)**, không nhét vào tên.

## 2. Description

- **Task:** 7 mục đúng thứ tự — Mục tiêu | Phạm vi (gồm Không làm gì) | Việc cần làm (bulleted list) | Đầu vào & phụ thuộc (smart-link Jira bắt buộc) | Bàn giao/Đầu ra | Tiêu chí hoàn thành (checkbox đo được) | Nguồn tham chiếu (link click được).
- **Epic:** Mục tiêu | Cách dùng (gom task nào) | Nghiệm thu (theo checklist cuối `cong_viec_theo_tuan.md`).
- **Task tách đợt:** 1 đoạn pointer — `Tách từ VHT-x để hiện đúng Sprint N. Phạm vi đợt này: ... VHT-x gốc giữ đợt M. Chi tiết đầy đủ xem VHT-x.` + Parent là Epic của task gốc + link Cloners về task gốc. Cấm copy full mô tả cha.
- Cấm lặp `Mục tiêu ≈ Việc cần làm`; cấm nhắc ID task mà không gắn smart-link.

## 3. Labels — tối đa 3 chiều, chữ thường-gạch-nối

- Phân hệ: `p1…p8`. Theme: `nen-tang-chung, chat-luong-ban-giao, cong-tac-phe-duyet, thung-rac, bieu-gia, excel, github, lich-su, phan-quyen`. Đợt: `dot-*` (đúng 1 label của sprint mình đứng; task gốc giữ dot của đợt đầu).
- Tầng triển khai (backend/frontend/database/test/docs) ghi ở **Components**, không dùng labels.
- Cấm tạo label mới khi chưa duyệt. Gộp trùng nghĩa: `quyen/admin → phan-quyen`; `phe-duyet → cong-tac-phe-duyet`; `cong-tac` chỉ giữ ở task liên quan công tắc Quản lý (mô tả có nhắc công tắc/SW).

## 4. Issue Type & Parent

- `Epic` (P1–P8, nền tảng, chất lượng/bàn giao) → `Task` → task tách đợt là `Task`, bắt buộc có Parent là Epic của task gốc. Không dùng Sub-task.
- Task tách đợt nằm ở Sprint của đợt nó; task gốc nằm ở Sprint của đợt đầu.

## Nguồn liên quan

- `cong_viec_theo_tuan.md` (kế hoạch 4 tuần, mã `VHT-n` là chuẩn), `timeline_song_song.md`, `thao_tac_va_quyen.xlsx`.
