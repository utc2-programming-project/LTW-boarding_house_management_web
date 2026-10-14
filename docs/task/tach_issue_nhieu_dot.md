# Tách issue nhiều đợt ra nhiều sprint — Before → After + checklist hoàn thiện

Ngày tách: 14/10/2026. Sprint đang active: **1B** (12–15 Oct).

## 1. Vấn đề gốc

- Một số issue Jira ghi **2–4 đợt** trong Description (VD `Đợt 1B` + `Đợt 1D`) nhưng Jira chỉ cho 1 issue nằm **1 sprint** tại 1 thời điểm.
- Cách sửa đã chốt: **giữ issue gốc ở sprint của đợt đầu, clone Task mới cho từng đợt còn lại**, link `Cloners` qua lại.
- Đã thử tách bằng Subtask → **sai**: subtask không hiện riêng trên board/sprint list, phải xóa làm lại bằng Task.

## 2. Quy ước tách (bắt buộc cho mọi lần tách sau)

| Thuộc tính | Giá trị chuẩn |
| --- | --- |
| Project | `VHT` (id `10001`), board `KAN board` (id `2`) |
| Loại bản tách | `Task` (id `10007`). KHÔNG dùng Subtask |
| Tên bản tách | `[<ĐỢT>] <phạm vi đợt> (tách từ <VHT-n>)`. VD: `[1D] Sửa layout/selector sau ghép (tách từ VHT-19)` |
| Link | `Cloners`: issue gốc `is cloned by` bản tách (inward=gốc, outward=bản tách) |
| Mô tả bản tách | 1 đoạn: phạm vi đợt này + câu `VHT-n gốc giữ đợt <X>` + `Chi tiết đầy đủ xem <VHT-n>.` |
| Issue gốc | Giữ nguyên mô tả (không xóa heading đợt cũ), nằm ở sprint của **đợt đầu** |
| Trạng thái bản tách | `To Do` (mặc định lúc tạo) |
| Assignee bản tách | **Kế thừa assignee issue gốc** (hiện 48 bản tách chưa có assignee → xem §5) |
| Label bản tách | Giữ label nhóm của gốc (`backend/ui/testing/...`, `p1..p8`, ...) + **đúng 1 label `dot-*`** của đợt nó (hiện 48 bản tách chưa có label → xem §5) |

## 3. Bảng sprint (id dùng cho API)

| Sprint | ID Jira | State 14/10 |
| --- | --- | --- |
| 1A | 1 | closed |
| 1B | 2 | **active** |
| 1C | 3 | future |
| 1D | 4 | future |
| 2A | 5 | future |
| 2B | 6 | future |
| 2C | 7 | future |
| 2D | 8 | future |
| 2E | 9 | future |
| 3A | 10 | future |
| 3B | 11 | future |
| 3C | 12 | future |
| 3D | 13 | future |
| 3E | 14 | future |
| 4A | 15 | future |
| 4B | 16 | future |
| 4C | 17 | future |
| 4D | 18 | future |

API gán sprint: `POST /rest/agile/1.0/sprint/{id}/issue` body `{"issues":["VHT-n"]}`.
API kiểm tra: `GET /rest/agile/1.0/sprint/{id}/issue?maxResults=60&fields=key,summary`.
KHÔNG thêm issue vào sprint **closed** (1A) — API báo thành công giả nhưng không có tác dụng.

## 4. Bảng Before → After

Cột "Trước": sprint issue gốc đang nằm trước khi tách. Cột "Sau": issue gốc (giữ đợt đầu) + các bản tách đã tạo.

### Tuần 1 — đã tách 7 (VHT-112–118)

| Gốc (assignee / status) | Đợt trong mô tả | Trước | Sau |
| --- | --- | --- | --- |
| VHT-7 Chốt DTO phòng/service P2 (Tính / To Do) | 1B, 1C | 1C | Gốc giữ 1B→ **1C**; `VHT-112` [1B] Chốt DTO → **1B** |
| VHT-23 Chốt DTO khách/service P3 (Given / To Do) | 1B, 1C | 1C | Gốc giữ 1B→ **1C**; `VHT-113` [1B] Chốt DTO khách → **1B** |
| VHT-11 Form hợp đồng mẫu (Đào / To Do) | 1C, 1D | 1D | Gốc giữ 1C→ **1D**; `VHT-114` [1C] Dựng form mẫu → **1C** |
| VHT-16 UI phòng (Phúc / To Do) | 1C, 1D | 1D | Gốc giữ 1C→ **1D**; `VHT-115` [1C] Dựng UI phòng mẫu → **1C** |
| VHT-17 UI khách (Phúc / To Do) | 1C, 1D | 1D | Gốc giữ 1C→ **1D**; `VHT-116` [1C] Dựng UI khách mẫu → **1C** |
| VHT-21 Quy trình nhánh/PR (Phú / To Do) | 1C, 1D | 1D | Gốc giữ 1C→ **1D**; `VHT-117` [1C] Chuẩn bị review/merge → **1C** |
| VHT-19 Theme/layout (Phú / To Do) | 1B, 1D | 1B | Gốc giữ 1B→ **1B**; `VHT-118` [1D] Sửa layout sau ghép → **1D** |
| VHT-14 Kiểm thử thật/case P6 (Given / To Do) | 1A, 1D | 1D | **KHÔNG tách** (1A đã closed). Gốc ở **1D** |

Sprint 1B/1C/1D sau tách: **9 / 7 / 9** issue.

### Tuần 2 — đã tách 12 (VHT-119–130)

| Gốc (assignee / status) | Đợt | Trước | Sau |
| --- | --- | --- | --- |
| VHT-24 Phạm vi QL (Tính / To Do) | 2A, 2C | 2A | Gốc giữ 2A→ **2A**; `VHT-119` [2C] Rà phạm vi sau ghép → **2C** |
| VHT-25 Liên kết thuê (Given / To Do) | 2B, 2C | 2C | Gốc giữ 2B→ **2B**; `VHT-120` [2C] Ghép liên kết thật → **2C** |
| VHT-28 Migration/ERD (Tính / To Do) | 2A, 2C, 2D, 2E | 2E | Gốc giữ 2A→ **2A**; `VHT-121` [2C] migration hóa đơn → **2C**; `VHT-122` [2D] cập nhật ERD → **2D**; `VHT-123` [2E] kiểm trên commit chung → **2E** |
| VHT-31 UI hợp đồng (Đào / To Do) | 2C, 2D | 2D | Gốc giữ 2C→ **2C**; `VHT-124` [2D] Sửa UI sau ghép → **2D** |
| VHT-36 Giấy tờ/phòng thuê (Phúc / To Do) | 2B, 2D | 2D | Gốc giữ 2B→ **2B**; `VHT-125` [2D] Ghép phòng thuê thật → **2D** |
| VHT-37 Đơn giá (Phúc / To Do) | 2A, 2C | 2C | Gốc giữ 2A→ **2A**; `VHT-126` [2C] Ghép service thật → **2C** |
| VHT-39 Sự cố (Phú / To Do) | 2A, 2D | 2D | Gốc giữ 2A→ **2A**; `VHT-127` [2D] Ghép sự cố thật+ảnh → **2D** |
| VHT-40 Dashboard (Phú / To Do) | 2C, 2E | 2E | Gốc giữ 2C→ **2C**; `VHT-128` [2E] Ghép nguồn khách → **2E** |
| VHT-42 Test P4/giấy tờ (Given / To Do) | 2D, 2E | 2E | Gốc giữ 2D→ **2D**; `VHT-129` [2E] Regression tuần 2 → **2E** |
| VHT-44 Publish IIS (Phú / To Do) | 2B, 2E | 2E | Gốc giữ 2B→ **2B**; `VHT-130` [2E] Publish bản tích hợp → **2E** |

Sprint 2A/2B/2C/2D/2E sau tách: **8 / 8 / 10 / 7 / 9** issue.

### Tuần 3 — đã tách 18 (VHT-131–148)

| Gốc (assignee / status) | Đợt | Trước | Sau |
| --- | --- | --- | --- |
| VHT-47 Scope P4–P7 (Tính / To Do) | 3A, 3D | 3D | Gốc giữ 3A→ **3A**; `VHT-131` [3D] Kiểm scope sau tích hợp → **3D** |
| VHT-48 Schema/ERD tài chính (Tính / To Do) | 3A, 3C, 3D, 3E | 3E | Gốc giữ 3A→ **3A**; `VHT-132` [3C] cập nhật ERD → **3C**; `VHT-133` [3D] chốt ERD → **3D**; `VHT-134` [3E] kiểm trên release → **3E** |
| VHT-49 Demo/DB sạch (Tính / To Do) | 3A, 3C | 3C | Gốc giữ 3A→ **3A**; `VHT-135` [3C] Bổ sung demo hóa đơn → **3C** |
| VHT-50 Gia hạn/cọc (Đào / To Do) | 3B, 3D | 3D | Gốc giữ 3B→ **3B**; `VHT-136` [3D] Sửa sau tích hợp → **3D** |
| VHT-53 Hóa đơn thật (Given / To Do) | 3A, 3B | 3B | Gốc giữ 3A→ **3A**; `VHT-137` [3B] Phát hành thật → **3B** |
| VHT-59 Rà UI (Phúc / To Do) | 3A, 3C, 3E | 3E | Gốc giữ 3A→ **3A**; `VHT-138` [3C] Sửa theo dữ liệu thật → **3C**; `VHT-139` [3E] Kiểm trên bản chung → **3E** |
| VHT-61 Report P8 (Phú / To Do) | 3B, 3C, 3D, 3E | 3E | Gốc giữ 3B→ **3B**; `VHT-140` [3C] nguồn mới → **3C**; `VHT-141` [3D] đủ nguồn+KPI → **3D**; `VHT-142` [3E] regression report → **3E** |
| VHT-62 Reconnect (Phú / To Do) | 3A, 3C, 3D | 3D | Gốc giữ 3A→ **3A**; `VHT-143` [3C] Ghép tra kết quả → **3C**; `VHT-144` [3D] Thử ngắt/nối mạng → **3D** |
| VHT-63 Test tiền (Given / To Do) | 3A, 3D, 3E | 3E | Gốc giữ 3A→ **3A**; `VHT-145` [3D] Test tiền thật → **3D**; `VHT-146` [3E] Regression tiền → **3E** |
| VHT-66 Word/README (Phúc / To Do) | 3C, 3D, 3E | 3E | Gốc giữ 3C→ **3C**; `VHT-147` [3D] Cập nhật sau tích hợp → **3D**; `VHT-148` [3E] Ảnh IIS thật → **3E** |

Sprint 3A/3B/3C/3D/3E sau tách: **9 / 5 / 11 / 8 / 8** issue.

### Tuần 4 — đã tách 11 (VHT-149–159)

| Gốc (assignee / status) | Đợt | Trước | Sau |
| --- | --- | --- | --- |
| VHT-68 Regression (Given / To Do) | 4A, 4B, 4C | 4B | Gốc giữ 4A→ **4A**; `VHT-149` [4B] Test lại PR sửa → **4B**; `VHT-150` [4C] Regression cuối → **4C** |
| VHT-70 Sửa P4/P5/P7 (Đào / To Do) | 4A, 4B | 4B | Gốc giữ 4A→ **4A**; `VHT-151` [4B] Sửa lỗi regression → **4B** |
| VHT-71 Sửa P3/P6 (Given / To Do) | 4A, 4B | 4B | Gốc giữ 4A→ **4A**; `VHT-152` [4B] Sửa lỗi tiền/storage → **4B** |
| VHT-72 Rà UI/VI-EN (Phúc / To Do) | 4A, 4B | 4B | Gốc giữ 4A→ **4A**; `VHT-153` [4B] Kiểm sau PR sửa → **4B** |
| VHT-74 Backup/restore (Tính / To Do) | 4A, 4B, 4D | 4D | Gốc giữ 4A→ **4A**; `VHT-154` [4B] Thực hiện restore → **4B**; `VHT-155` [4D] Backup cuối → **4D** |
| VHT-77 Deploy cuối (Phú / To Do) | 4B, 4C | 4C | Gốc giữ 4B→ **4B**; `VHT-156` [4C] Deploy+smoke → **4C** |
| VHT-80 Word/README (Phúc / To Do) | 4A, 4C, 4D | 4D | Gốc giữ 4A→ **4A**; `VHT-157` [4C] Ghép ERD/ảnh/test → **4C**; `VHT-158` [4D] Chốt theo release → **4D** |
| VHT-81 Slide/demo (Phú / To Do) | 4C, 4D | 4D | Gốc giữ 4C→ **4C**; `VHT-159` [4D] Slide release+demo → **4D** |

Sprint 4A/4B/4C/4D sau tách: **8 / 9 / 5 / 5** issue.

## 5. Checklist hoàn thiện "chuẩn nhất" (việc còn lại)

Đánh dấu xong từng dòng khi làm:

- [ ] **Assignee**: 48 bản tách (VHT-112–159) đang `Unassigned`. Gán mỗi bản tách = assignee issue gốc (bảng §4 cột Gốc).
- [ ] **Label bản tách**: mỗi bản tách gắn label nhóm của gốc + đúng 1 label `dot-*` của đợt nó. VD `VHT-118` ← `ui` + `dot-1d` (lấy label nhóm từ gốc, bỏ các `dot-*` khác).
- [ ] **Sửa label `dot-*` stale trên issue gốc** (mô tả đã sửa nhưng label chưa theo):
  - `VHT-19`: `dot-1a` → `dot-1b` (giữ `dot-1d`).
  - `VHT-28`: `dot-2b` → `dot-2a` (giữ `dot-2c`, `dot-2d`, `dot-2e`).
  - `VHT-48`: `dot-3b` → `dot-3a` (giữ `dot-3c`, `dot-3d`, `dot-3e`).
- [ ] **VHT-19 mô tả**: heading `Đợt 1B` đã sửa (làm mới, chuyển từ 1A) + `VHT-20` đã ghi rõ input layout cùng nhịp 1B — đối chiếu lại sau khi gán label.
- [ ] **Verify cuối**: chạy `GET /rest/agile/1.0/sprint/{id}/issue` từng sprint, đếm khớp số lượng §4; mỗi bản tách mở ra phải thấy link `is cloned from` về gốc.
- [ ] **Quy ước lâu dài**: issue mới chỉ ghi 1 đợt trong mô tả; việc kéo dài nhiều nhịp thì tạo issue mới ngay từ đầu thay vì nhét nhiều `Đợt` vào 1 issue.

## 6. Prompt mẫu paste cho AI (để AI khác viết prompt chuẩn cho coder)

> Tôi có Jira site `https://du-an-lap-trinh-utc2.atlassian.net`, project `VHT`, board id `2`.
> Đã tách các issue nhiều đợt thành Task riêng theo file `docs/task/tach_issue_nhieu_dot.md` (§4).
> Hãy giúp tôi HOÀN THIỆN: với mỗi bản tách `VHT-112`–`VHT-159`, (1) gán assignee = assignee issue gốc trong bảng, (2) gắn labels = label nhóm của gốc + đúng 1 label `dot-*` của đợt nó;
> sửa label `VHT-19` (`dot-1a`→`dot-1b`), `VHT-28` (`dot-2b`→`dot-2a`), `VHT-48` (`dot-3b`→`dot-3a`);
> sau đó verify số lượng từng sprint khớp §4 và báo bảng kết quả.
> API: `PUT /rest/api/3/issue/{key}` body `{"fields":{"assignee":{"accountId":"..."},"labels":[...]}}`
> (lấy `accountId` qua `GET /rest/api/3/user/search?query=<tên>`),
> `GET /rest/agile/1.0/sprint/{id}/issue?maxResults=60&fields=key,summary` để đếm.
> Auth Basic email+token. Chỉ làm đúng danh sách trên, không tạo/xóa issue, không đụng sprint closed (id 1).

## 7. Nguồn tham chiếu

- `docs/task/cong_viec_theo_tuan.md` (kế hoạch 4 tuần, mã `VHT-n` là chuẩn)
- `docs/task/timeline_song_song.md` (nhịp song song; đã gộp VHT-19 vào 1B, phân biệt B=làm mới / D=chỉ sửa)
- `docs/task/thao_tac_va_quyen.xlsx` (ma trận quyền chuẩn)
