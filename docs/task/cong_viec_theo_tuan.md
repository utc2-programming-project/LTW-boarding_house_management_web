# Kế hoạch 4 tuần / Four-week plan — Web vận hành nhà trọ

**GitHub:** https://github.com/TinhSsc/boarding-house-management-web.git

**Phạm vi hoàn thành:** P1–P8, Admin / Chủ trọ / Quản lý / Khách thuê, Việt/Anh, bản IIS và hồ sơ nộp. “100%” được kiểm bằng checklist cuối file, không suy ra từ số màn hình hoặc số task đã tick.

## Cách dùng / Working rules

- **Đây là kế hoạch duy nhất.** Mỗi mã là một đầu việc để đưa lên Jira/GitHub. Quyền theo vai trò: **xlsx `thao_tac_va_quyen.xlsx` là nguồn chuẩn**; mọi mô tả task phải khớp ma trận này. Trạng thái Jira hiện dùng `To Do → In Progress → Testing → Done`; việc bị chặn được đánh dấu và liên kết với task chặn, không chuyển thành Done. Xem [quy ước Jira](quy_uoc_jira.md). Các tuần là dự kiến; cuối tuần đối chiếu đầu ra, không tự tick vì hết thời gian.
- **Nhãn Jira là nguồn chuẩn / Labels:** tầng triển khai ghi ở **Components** (`backend` = service/quyền/logic; `frontend` = giao diện; `database` = schema/migration/ERD; `test` = kiểm thử; `docs` = README/Word/slide); labels chỉ giữ `p1…p8`, `dot-*`, `github`, `phan-quyen`, `cong-tac`, `cong-tac-phe-duyet`, `thung-rac`, `bieu-gia`, `excel`, `lich-su`, `nen-tang-chung`, `chat-luong-ban-giao`. Chi tiết xem [quy ước Jira](quy_uoc_jira.md). Tài liệu không gắn tag `[Backend]`… vào tên việc.
- **Nội dung thực hiện / What to do** tóm tắt việc cần làm trong từng task; tiêu chí hoàn thành tập trung ở cổng cuối tuần và checklist nghiệm thu cuối file.
- **Xem [timeline chạy song song](timeline_song_song.md):** mỗi cột là một người, các ô cùng hàng làm đồng thời; hàng sau ghi đầu ra cần chờ. `A // B` = đồng thời; `A → B` = phụ thuộc. Timeline không thêm task/đổi phân công; trong một ô có nhiều việc thì người đó làm lần lượt.
- **Mã Jira:** tài liệu này dùng thẳng mã `VHT-n` của Jira (VD `VHT-4` Chốt thao tác và quyền P1–P8). Không dùng mã tuần `W…` riêng để tránh hai hệ mã song song. 11 task mới `VHT-94 … VHT-104` được thêm cho Admin/công tắc/thùng rác/biểu giá/Excel; 48 task tách đợt `VHT-112 … VHT-159` nằm ở mục riêng từng tuần.

### Epic / Nhóm việc (nguồn parent trên Jira)

| Epic | Nhóm | Task con (đếm) |
| --- | --- | --- |
| VHT-83 | P1 · Tài khoản và phân quyền | 8 |
| VHT-84 | P2 · Nhà trọ và phòng | 5 |
| VHT-85 | P3 · Khách thuê và hồ sơ | 10 |
| VHT-86 | P4 · Hợp đồng | 11 |
| VHT-87 | P5 · Điện nước và dịch vụ | 8 |
| VHT-88 | P6 · Hóa đơn và thu tiền | 9 |
| VHT-89 | P7 · Sự cố | 4 |
| VHT-90 | P8 · Dashboard và báo cáo | 11 |
| VHT-91 | Nền tảng chung · UI, SQL, storage | 30 |
| VHT-92 | Chất lượng và bàn giao | 39 |
| VHT-93 | Công tắc Quản lý và phê duyệt | 3 |

Mỗi task tách đợt (VHT-112…VHT-159) đều có Parent là Epic của task gốc.

**“Phối hợp” không luôn nghĩa là “phải chờ”.** Người review có thể review khi có PR; UI bắt đầu sau khi chốt DTO bằng dữ liệu mẫu, không chờ backend hoàn chỉnh. Chỉ chờ khi ghép SQL/giao dịch thật hoặc cần đầu ra cụ thể đã ghi.
- **Không song song trên cùng file/DB dùng chung:** chia file theo feature; chốt DTO rồi mới tách nhánh. `Program.cs`, resource dùng chung, model snapshot và migrations ghép theo lượt; test dùng DB riêng. Nếu đổi DTO, báo ngay người đang làm UI/test.
- INIT đã có repo/CI, project, Identity nền, SQL lõi, test scaffold, folder và trang giới thiệu. **Không giao nhóm tạo lại INIT.** Sidebar/theme thật, localization toàn app, nghiệp vụ và test thật chưa xong.
- Tên rút gọn: **Tính** = Tính Ct; **Đào** = Mai Đào; **Phát** = Hình Vĩnh Phát; **Phúc** = Đinh Hoàng Phúc; **Phú** = Phú. Người ở cột **Chính** chịu trách nhiệm bàn giao; người ở cột **Phối hợp** cung cấp/review phần ghi rõ, không làm trùng.
- Backend: **Tính — P1/P2; Đào — P4/P5/P7; Phát — P3/P6/storage/query tài chính**. Task mới: Tính — công tắc/duyệt/vòng đời tài khoản/thùng rác phòng; Phát — thùng rác khách; Đào — biểu giá, sửa chỉ số, xuất Excel; Phú — UI công tắc, nhật ký; Phúc — test ma trận quyền. UI: **Tính — P1; Đào — P4; Phát — P6; Phúc — P2/P3/P5/shared forms; Phú — layout/P7/P8**. Tính điều phối DB/ERD; Phú chính GitHub/IIS, Tính dự phòng; Phúc chính Word/README, Phú slide; Phát điều phối test.
- **Giảm tải Tính:** Phát nhận toàn bộ backend P3, hoàn thành nền tuần 1–2 trước cao điểm P6 tuần 3; Phúc vẫn làm UI P3 và trực tiếp hỏi Phát về DTO/service. Tính chỉ cung cấp quyền chung, query phòng và review schema, không phải người sửa mọi lỗi P3. Phúc/Phú tự chạy case UI theo checklist để Phát không gánh cả backend lẫn mọi thao tác test.
- Mỗi tác giả backend làm entity/EF/rules/test phần mình; **Tính review schema/migration và cập nhật ERD**, không gánh hết SQL. Mỗi tác giả UI dịch phần mình (`vi-VN`/`en-US`) và kiểm trạng thái loading/rỗng/lỗi. Không dịch nội dung người dùng nhập; đổi ngôn ngữ không đổi VND hoặc cách tính.
- **Bàn giao một task:** PR + cách chạy + kết quả test + giới hạn còn lại; DTO kèm mẫu input/output khi có người khác dùng. PR vào `main` phải được người phối hợp review, CI đạt và chạy lại trên bản ghép.
- **Bị chặn:** hỏi người/mã task trong cột phối hợp: “Chưa làm / đang làm / đã xong? Gửi DTO, PR, lúc bàn giao.” Chốt DTO để làm UI/test bằng mẫu; ghi rõ `Blocked by …`. Không giả dữ liệu thật hoặc sửa đè phần người khác.
- Service kiểm quyền và dùng context ngắn hạn; thao tác nhiều bước dùng cùng transaction. Migration/file chung sửa theo lượt. Mỗi người tự sửa lỗi phần mình; người test/tổng hợp không làm thay.

**English:** One weekly backlog, small deliverables, explicit owners and handoffs. Backend is owned by Tính/Đào/Phát; all five build UI. Every feature must pass real-data, authorization and bilingual acceptance checks before it is Done.

## Tuần 1 / Week 1 — Nền dùng chung, tài khoản, phòng và khách

**Thứ tự:** đầu tuần chốt quyền/DTO; giữa tuần giao service + style; cuối tuần ghép P1–P3. Việc UI/test độc lập làm song song, không chờ mọi service xong.

### Đầu việc chi tiết / Task details

| Mã | Việc nhỏ / Task | Chính | Phối hợp / Handoff | Nội dung thực hiện / What to do |
| --- | --- | --- | --- | --- |
| VHT-4 | Chốt thao tác và quyền P1–P8 | Tính | Cả nhóm: xác nhận ai xem/sửa/duyệt; Phát: case cấm | Hoàn thiện và chốt `thao_tac_va_quyen.xlsx` (45 thao tác × 4 vai trò, 16 công tắc Quản lý, quyết định D1–D17); hỏi nhóm các điểm còn ❓. |
| VHT-5 | Vẽ ERD lõi từ SQL INIT | Tính | Phúc: nhận sơ đồ Word; Đào/Phát: review FK | Vẽ Identity/Property/Room/Tenant; ghi PK/FK, quan hệ, nullable; đối chiếu migrations, chỉ vẽ bảng đã có. |
| VHT-6 | Hoàn thiện role và phạm vi người dùng | Tính | VHT-4; Đào/Phát: review cách gọi kiểm quyền | Viết service/policy kiểm role, sở hữu và phân công; chuẩn bị user 4 vai trò (Admin/Chủ trọ/Quản lý/Khách thuê); seed Admin đầu tiên bằng secret, không commit; policy Admin chỉ quản trị tài khoản/vai trò (không có quyền nghiệp vụ); chặn nâng quyền qua đăng ký công khai. |
| VHT-7 | Chốt DTO nhà trọ/phòng và service P2 | Tính | Phúc: trường form; Đào: dữ liệu chọn phòng | Viết DTO list/detail/filter/save và service đọc/cập nhật SQL; kiểm mã phòng và quyền; bàn giao dữ liệu chọn phòng. |
| VHT-8 | UI P1: đăng nhập, cá nhân, tài khoản/role | Tính | Phú: VHT-19/VHT-20; Phát: test đăng nhập | Ghép UI Identity HTTP; làm trang cá nhân/quản trị tài khoản/role; kiểm quyền truy cập và dịch VI/EN. |
| VHT-9 | Chốt trạng thái, kỳ và dữ liệu P4/P5 | Đào | Tính: schema; Phát: tiền thuê/chi phí hóa đơn | Chốt trường/DTO mẫu, trạng thái và bước chuyển; thống nhất giá, cọc, kỳ, công thức với nhóm trước code. |
| VHT-10 | Chuẩn hóa ngày, tiền và thời gian | Đào | Tính/Phát: review; Phúc/Phú: nhận hàm dùng | Viết formatter VND/ngày, tích hợp TimeProvider, chuyển UTC/Việt Nam; viết unit test; giữ tiền thống nhất khi đổi ngôn ngữ. |
| VHT-11 | Form tạo hợp đồng bằng DTO mẫu | Đào | VHT-9; Phúc: component VHT-15; Phú: style | Làm form chọn phòng/khách, ngày, giá/cọc; validation và VI/EN; gắn nhãn dữ liệu mẫu chưa lưu SQL. |
| VHT-12 | Chốt hợp đồng storage và giới hạn tệp | Phát | Tính: kiểm quyền/Options; Phúc/Phú: nhu cầu tệp | Thống nhất DTO upload/download, metadata, loại/dung lượng và kiểm quyền; hỏi nhóm để chốt giới hạn còn thiếu. |
| VHT-13 | Tạo storage backend dùng chung | Phát | VHT-12; Tính: review bảo mật | Viết lưu/tải tệp ngoài wwwroot, sinh tên server; kiểm stream/loại/quyền, ẩn đường dẫn vật lý; viết test. |
| VHT-14 | Chuẩn bị kiểm thử thật và case P6 | Phát | INIT/test scaffold có sẵn; Tính xác nhận user/DB, Đào xác nhận rule VHT-9 sau bản nháp | Từ INIT, dựng ngay DB test riêng, fixture và case mẫu; cập nhật theo DTO/rule được chốt, rồi chạy auth/UI thật trên bản ghép ở 1D. |
| VHT-15 | Component form/bảng tối thiểu | Phúc | Phú: VHT-19; cả nhóm: chọn component thực sự dùng | Làm label/lỗi, filter/paging, badge, confirm/loading/rỗng; viết ví dụ sử dụng; chỉ tách component nhóm thực sự cần. |
| VHT-16 | UI nhà trọ/phòng: list, detail, form | Phúc | Tính: VHT-7; Phú: style | Làm list/detail/form, lọc nhà trọ, hiển thị giá/trạng thái; gọi service theo DTO, xử lý lỗi/rỗng và dịch VI/EN. |
| VHT-17 | UI khách thuê và thông tin cá nhân | Phúc | Phát: VHT-23; Tính: VHT-6/VHT-8; Đào: lựa chọn khách | Làm list/detail/form và liên kết user; ghép service hồ sơ, giới hạn trường khách được sửa; dịch VI/EN. |
| VHT-18 | README setup bản đầu và dàn ý Word | Phúc | Dùng repo/INIT đã có; Tính/Phát/Phú rà đúng phần SQL/Identity, test, GitHub sau khi có bản nháp | Từ repo hiện có, viết hướng dẫn clone/secrets/migrate/run bản đầu và tự chạy kiểm tra; lập dàn ý Word, chia chương và mẫu nội dung cho từng người. Không chờ Tính hoàn thành ERD hoặc backend mới bắt đầu. Tài liệu theo nghiệp vụ mới được cập nhật ở VHT-45, VHT-66 và chốt tại VHT-80. |
| VHT-19 | Theme và layout dùng chung | Phú | Phúc: VHT-15; Tính: menu/quyền | Cấu hình Inter, light/dark tokens, Lucide SVG; làm sidebar/topbar/breadcrumb responsive theo docs, không trộn Material icons. |
| VHT-20 | Localization chung và selector | Phú | Tính: Identity; Đào: định dạng; mỗi người: resource | Cấu hình .resx/IStringLocalizer VI/EN, mặc định VI; làm selector lưu lựa chọn qua reload/Identity, không đổi culture toàn người dùng. |
| VHT-21 | Quy trình nhánh, PR và ghép tuần 1 | Phú | Tính: dự phòng/review; cả nhóm: PR + test | Quy định nhánh/người review; kiểm CI có sẵn, ghép PR P1–P3/shared và chạy lại bản chung; không tạo lại repo/workflow. |
| VHT-22 | Test P1–P3 và tài liệu kiểm thử | Phát | Tính: service/quyền; Phúc: UI; Phú: bản ghép | Test 4 vai trò (gồm Admin bị chặn nghiệp vụ), tòa nhà khác, validation và tải tệp trái quyền; ghi kết quả, bước tái hiện và giao người sửa. |
| VHT-23 | Chốt DTO khách thuê và service P3 | Phát | Phúc: trường form; Đào: chọn khách; Tính: quyền/schema | Mở rộng Tenant INIT; viết DTO/service list/detail/filter/cập nhật và chọn khách theo quyền; test, bàn giao Phúc/Đào. |
| VHT-94 | Nền công tắc Quản lý | Tính | VHT-4, VHT-6; Đào/Phát: review cách gọi policy | Làm cấu hình lưu 16 công tắc (Cho phép/Duyệt/Không thể, mặc định Duyệt; SW-15 chỉ Cho phép/Không thể) và policy handler dùng chung: kiểm công tắc trước khi thực thi thao tác ⚙️ của Quản lý; chưa làm hàng đợi. Unit test 3 chế độ × 16 thao tác. |

### Task tách đợt / Split tasks (tuần 1)

7 task tách từ task gốc nhiều đợt, mỗi task nằm đúng sprint của đợt nó, Parent là Epic của task gốc, mô tả là pointer về task gốc.

| Mã | Việc / Task | Chính | Tách từ | Đợt |
| --- | --- | --- | --- | --- |
| VHT-112 | Chốt DTO nhà trọ và phòng P2 | Tính | VHT-7 — Chốt DTO nhà trọ/phòng và service P2 | 1B |
| VHT-113 | Chốt DTO khách thuê P3 | Phát | VHT-23 — Chốt DTO khách thuê và service P3 | 1B |
| VHT-114 | Dựng form hợp đồng mẫu P4 | Đào | VHT-11 — Dựng form tạo hợp đồng mẫu P4 | 1C |
| VHT-115 | Dựng UI phòng mẫu P2 | Phúc | VHT-16 — Dựng UI tra cứu và quản lý phòng P2 | 1C |
| VHT-116 | Dựng UI khách mẫu P3 | Phúc | VHT-17 — Dựng UI khách thuê và hồ sơ cá nhân P3 | 1C |
| VHT-117 | Rà soát và merge PR tuần 1 | Phú | VHT-21 — Chốt quy trình nhánh và ghép tuần 1 | 1C |
| VHT-118 | Sửa layout và selector sau ghép | Phú | VHT-19 — Dựng theme và layout dùng chung | 1D |

**Cổng cuối tuần / Gate:** P1–P3 chạy SQL trên bản chung, style/resource dùng được, storage nền và test thật chạy. 4 vai trò đăng nhập được; nền công tắc (VHT-94) có policy handler. Phúc gửi phòng/khách cho Đào; Phát gửi storage cho Tính/Phúc/Phú. Nếu chưa đạt, ghi task thiếu và người xử lý trước khi ghép P4.

## Tuần 2 / Week 2 — Hợp đồng thật, chi phí và sự cố

**Thứ tự:** phòng/khách và quyền → hợp đồng; DTO P5/P7 → UI song song; chi phí hợp lệ mới được bàn giao lập hóa đơn.

### Đầu việc chi tiết / Task details

| Mã | Việc nhỏ / Task | Chính | Phối hợp / Handoff | Nội dung thực hiện / What to do |
| --- | --- | --- | --- | --- |
| VHT-24 | Hoàn thiện phạm vi quản lý/tòa nhà P1–P3 | Tính | Phúc: UI; Đào/Phát: service liên quan | Làm phân công quản lý theo tòa nhà; ghép kiểm phạm vi vào query/update và menu, không chỉ ẩn nút. |
| VHT-25 | Gắn user với hồ sơ khách và liên kết thuê | Phát | Tính: user/quyền/query phòng; Đào: VHT-29/VHT-30; Phúc: trang khách | Gắn user–hồ sơ–hợp đồng; truy vấn dữ liệu khách theo liên kết thuê, ghép query phòng của Tính thay vì suy từ trạng thái Room. |
| VHT-26 | Hoàn thiện tệp giấy tờ P3 | Phát | Storage VHT-13; Tính: review quyền/schema; Phúc: VHT-36 | Gắn metadata/tệp vào hồ sơ; làm đường tải có kiểm quyền, chặn ngoài phạm vi; ghi audit không chứa nội dung giấy tờ. |
| VHT-27 | Bảo mật và xử lý lỗi dùng chung | Tính | Phát: endpoint tệp; Phú: error UI/IIS | Kiểm/cấu hình antiforgery, giới hạn đăng nhập/upload, lọc log nhạy cảm, lỗi an toàn và health live/ready. |
| VHT-28 | Review migration P4/P5/P7, cập nhật ERD | Tính | Đào: entity/config; Phát: kiểm DB; Phúc: nhận sơ đồ | Review/merge migrations theo lượt; chạy trên DB sạch và DB có dữ liệu, kiểm FK/unique; cập nhật ERD gửi Phúc. |
| VHT-29 | Entity và tạo hợp đồng thật | Đào | Tính: schema; Phúc: phòng/khách; Phát: review | Viết entity/EF/migration và service tạo hợp đồng; lưu thành viên, giá snapshot/cọc; kiểm dữ liệu và phạm vi. |
| VHT-30 | Duyệt hợp đồng và cập nhật phòng | Đào | Tính: query phòng; Phát: transaction test | Viết duyệt theo role/trạng thái, chống hợp đồng trùng hiệu lực; cập nhật hợp đồng và phòng cùng transaction. |
| VHT-31 | Ghép list/detail/tạo/duyệt hợp đồng | Đào | VHT-11, VHT-29/VHT-30; Phúc: component | Ghép UI với service SQL; làm thao tác chủ trọ duyệt, QL xử lý, khách xem hợp đồng/cọc; dịch VI/EN. |
| VHT-32 | Danh mục dịch vụ và đơn giá P5 | Đào | Phúc: VHT-37; Phát: đầu vào hóa đơn | Viết service đọc/lưu danh mục và đơn giá theo quyền: biểu giá điện/nước **chỉ đọc** (cấu hình sẵn, từ VHT-100); Chủ trọ thiết lập đơn giá **dịch vụ khác** có hiệu lực theo ngày; mở rộng bằng dòng dịch vụ, không cột phí cố định; test giá. |
| VHT-33 | Backend sự cố và chuyển trạng thái P7 | Đào | Phú: form/DTO; Tính: quyền; Phát: storage | Viết tạo/đọc/cập nhật phiếu sự cố vào SQL; kiểm phòng được phép, vai trò và bước chuyển trạng thái. |
| VHT-34 | Rule/DTO hóa đơn và dữ liệu tài chính | Phát | Đào: VHT-9/VHT-32; Tính: constraint | Chốt làm tròn/kỳ; viết DTO dòng hóa đơn, rule tính tổng và unit test bằng mẫu; chưa phát hành từ dữ liệu mẫu. |
| VHT-35 | Entity/migration hóa đơn và UI list/detail | Phát | Tính: review schema; Đào: nguồn thật; Phúc: table | Tạo Invoice/InvoiceItem, EF/migration và unique hợp đồng/kỳ; viết query và ghép list/detail theo quyền, VI/EN. |
| VHT-36 | UI giấy tờ và phòng đang thuê | Phúc | Phát: VHT-25/VHT-26 + storage; Tính: query phòng | Làm upload/tải qua service kiểm quyền, hiển thị lỗi loại/dung lượng; ghép trang phòng đang thuê qua hợp đồng thật. |
| VHT-37 | UI đơn giá/danh mục dịch vụ | Phúc | Đào: VHT-32; Tính: quyền | Làm form/table VI/EN cho danh mục/đơn giá; ghép service và quyền thiết lập/xem/cập nhật đã chốt; biểu giá điện/nước hiển thị chỉ đọc, chỉ form dịch vụ khác cho Chủ trọ sửa. |
| VHT-38 | UI chỉ số và bảng chi phí bằng DTO | Phúc | Đào: DTO VHT-9/VHT-32; Phát: kỳ hóa đơn | Làm form kỳ/chỉ số/dịch vụ, bảng tổng xem trước và validation; dùng mẫu có nhãn khi service kỳ thật chưa sẵn. |
| VHT-39 | UI báo sự cố, list/detail xử lý | Phú | Đào: VHT-33; Phát: storage; Phúc: shared forms | Làm gửi/theo dõi cho khách, cập nhật cho QL, xem cho chủ trọ; ghép ảnh private, trạng thái hợp lệ và VI/EN. |
| VHT-40 | Khung dashboard/báo cáo theo vai trò | Phú | Tính/Đào/Phát: chốt Report DTO/filter | Làm bộ lọc, KPI/table/MudChart theo DTO; giới hạn màn hình theo role (Quản lý không thấy doanh thu/công nợ tổng hợp; chỉ mục báo cáo Chủ trọ cho phép; Khách thuê không có dashboard quản trị, trang tổng quan cá nhân tính riêng — D15); ghi rõ nguồn mẫu/chưa ghép. |
| VHT-41 | Query nguồn phòng P8 | Tính | Phú: VHT-40; Phúc: đối chiếu phòng | Viết DTO/count/filter phòng theo phạm vi P2; bàn giao Phú, đối chiếu danh sách; nguồn khách do Phát làm riêng. |
| VHT-42 | Test hợp đồng/phòng và giấy tờ | Phát | Đào/Tính: P4/P2; Phúc: chạy case UI P3; Phú: bản ghép | Test tạo/duyệt trùng, rollback, đổi giá chuẩn không đổi snapshot, truy cập hồ sơ/tệp sai quyền; ghi lỗi/người sửa. |
| VHT-43 | Sửa thiếu bản dịch và lỗi form P1–P5/P7 | Phúc | Mỗi tác giả sửa phần mình; Phú: resource; Phát: lỗi | Rà/dịch bổ sung VI/EN, sửa loading/rỗng/lỗi và tràn mobile; giữ dữ liệu nhập khi lỗi, báo tác giả sửa phần riêng. |
| VHT-44 | Ghép PR và publish IIS thử sớm | Phú | Tính: cấu hình/keys; Phát: smoke; cả nhóm: PR | Ghép PR, publish IIS, cấu hình HTTPS/WebSocket/storage/secrets môi trường; chạy smoke và ghi lỗi triển khai. |
| VHT-45 | Cập nhật Word/ảnh và slide khung | Phúc | Dùng Word/README hiện có; Tính gửi ERD đã merge, từng người gửi ảnh/nội dung phần đã chạy; Phú nhận bản nháp làm slide | Cập nhật Word P1–P4 ngay từ bản hiện có, đánh dấu phần chưa ghép; thêm ảnh thật và ERD khi tương ứng đã sẵn, gửi Phú nội dung slide. |
| VHT-46 | Query nguồn khách thuê P8 | Phát | Phú: VHT-40; Phúc: đối chiếu khách; Tính: review scope | Viết DTO/count/filter khách theo P3; tránh đếm trùng user/hợp đồng, đối chiếu danh sách; bàn giao Phú trước tuần 3. |
| VHT-95 | Vòng đời tài khoản | Tính | VHT-6, VHT-24; Phú: UI quản trị | Trạng thái Chờ duyệt/Hoạt động/Khóa; Quản lý tạo khách → Chờ duyệt; Chủ trọ tạo Quản lý duyệt ngay; Admin tạo mọi vai trò; Chủ trọ/Admin duyệt, khóa, mở khóa; chặn đăng nhập khi chưa duyệt/khóa; không đăng ký công khai. |
| VHT-96 | Hàng đợi duyệt thao tác Quản lý | Tính | VHT-94; Phát: transaction test | Entity yêu cầu duyệt (loại thao tác, dữ liệu đề xuất, trạng thái Chờ/Đã duyệt/Từ chối/Hủy, người gửi/duyệt, thời điểm); khi duyệt kiểm lại trạng thái hiện tại (chống dữ liệu đã đổi), áp dụng thao tác và ghi kết quả **cùng transaction**; chống duyệt trùng bằng khóa duy nhất. |
| VHT-97 | UI công tắc, hàng đợi duyệt, trạng thái chờ duyệt | Phú | VHT-96; Phúc: component badge | Màn hình Chủ trọ đặt công tắc, hàng đợi duyệt/từ chối; Quản lý thấy “Yêu cầu của tôi” và nhãn Chờ duyệt; VI/EN. |
| VHT-98 | Thùng rác phòng | Tính | Phúc: UI; VHT-29/VHT-30 (quy tắc hợp đồng) | Xóa mềm phòng; chặn khi còn hợp đồng hiệu lực; trang thùng rác + khôi phục (Chủ trọ); chặn khôi phục khi trùng mã phòng; ẩn khỏi mọi query mặc định. |
| VHT-99 | Thùng rác hồ sơ khách | Phát | Phúc: UI; VHT-25 | Xóa mềm hồ sơ khách (Chủ trọ; Quản lý theo công tắc); chặn khi còn hợp đồng hiệu lực; khôi phục; giữ liên kết user–hồ sơ và tệp giấy tờ theo hạn lưu. |
| VHT-100 | Nạp biểu giá điện/nước cấu hình khóa | Đào | VHT-9; Phát: đầu vào hóa đơn | Nạp biểu giá bậc thang theo quy định hiện hành (nhóm xác nhận số liệu và ngày hiệu lực), có hiệu lực theo ngày, snapshot vào chi phí; không có đường sửa qua giao diện; unit test từng bậc, biên bậc và đổi kỳ. |
| VHT-101 | Test ma trận quyền tự động từ xlsx | Phúc | VHT-94, VHT-96; Phát: điều phối test | Từ `thao_tac_va_quyen.xlsx` sinh case 45 thao tác × 4 vai trò (ô ❌ = case cấm; ô ⚙️ × 3 chế độ); chạy qua HTTP/service. |

### Task tách đợt / Split tasks (tuần 2)

12 task tách từ task gốc nhiều đợt, mỗi task nằm đúng sprint của đợt nó, Parent là Epic của task gốc, mô tả là pointer về task gốc.

| Mã | Việc / Task | Chính | Tách từ | Đợt |
| --- | --- | --- | --- | --- |
| VHT-119 | Rà phạm vi quản lý sau ghép P1–P3 | Tính | VHT-24 — Hoàn thiện phạm vi quản lý/tòa nhà P1–P3 | 2C |
| VHT-120 | Ghép liên kết thuê thật P3 | Phát | VHT-25 — Gắn tài khoản với hồ sơ và liên kết thuê | 2C |
| VHT-121 | Rà soát migration hóa đơn theo lượt | Tính | VHT-28 — Rà soát migration và cập nhật ERD P4/P5/P7 | 2C |
| VHT-122 | Cập nhật ERD theo migration đã ghép | Tính | VHT-28 — Rà soát migration và cập nhật ERD P4/P5/P7 | 2D |
| VHT-123 | Kiểm migration và ERD trên bản chung | Tính | VHT-28 — Rà soát migration và cập nhật ERD P4/P5/P7 | 2E |
| VHT-124 | Sửa UI hợp đồng sau ghép P4 | Đào | VHT-31 — Ghép tạo và duyệt hợp đồng P4 | 2D |
| VHT-125 | Ghép phòng đang thuê thật P3 | Phúc | VHT-36 — Dựng UI giấy tờ và phòng thuê P3 | 2D |
| VHT-126 | Ghép service đơn giá thật P5 | Phúc | VHT-37 — Dựng UI đơn giá và dịch vụ P5 | 2C |
| VHT-127 | Ghép sự cố thật và ảnh P7 | Phú | VHT-39 — Dựng UI báo và xử lý sự cố P7 | 2D |
| VHT-128 | Ghép nguồn khách cho báo cáo P8 | Phú | VHT-40 — Dựng dashboard và báo cáo P8 | 2E |
| VHT-129 | Regression tuần 2 | Phát | VHT-42 — Kiểm thử hợp đồng và giấy tờ P4 | 2E |
| VHT-130 | Phát hành bản tích hợp tuần 2 | Phú | VHT-44 — Ghép PR và phát hành IIS thử | 2E |

**Cổng cuối tuần / Gate:** tạo → duyệt hợp đồng đổi phòng thật; khách xem hồ sơ/phòng/hợp đồng; dịch vụ/đơn giá và sự cố chạy. Có nguồn report phòng/khách, khung hóa đơn và IIS thử. Công tắc Quản lý + hàng đợi duyệt chạy với 3 chế độ; thùng rác phòng/khách; biểu giá điện/nước đã nạp và test các bậc; 4 vai trò đăng nhập. Không bắt đầu thu tiền thật nếu hợp đồng/chi phí chưa đạt.

## Tuần 3 / Week 3 — Đủ P1–P8 và đối chiếu tiền

**Thứ tự:** chi phí kỳ thật → lập/phát hành hóa đơn → khách gửi yêu cầu → quản lý xác nhận → report từ tiền đã xác nhận. Hợp đồng và sự cố hoàn thiện song song.

### Đầu việc chi tiết / Task details

| Mã | Việc nhỏ / Task | Chính | Phối hợp / Handoff | Nội dung thực hiện / What to do |
| --- | --- | --- | --- | --- |
| VHT-47 | Kiểm scope xuyên hợp đồng/hóa đơn/sự cố | Tính | Đào/Phát: service; Phú/Phúc: UI | Thử thay ID/tòa nhà/user để kiểm đọc/sửa ngoài phạm vi; rà menu với service, sửa quyền chung và giao lỗi feature cho tác giả. |
| VHT-48 | Review schema tài chính và ERD hoàn chỉnh | Tính | Phát: VHT-53/VHT-54; Đào: kỳ/hợp đồng; Phúc: nhận ERD | Review constraints, concurrency và quy tắc giữ/xóa lịch sử; Review bảng công tắc, hàng đợi duyệt, lịch sử sửa chỉ số, biểu giá; cột xóa mềm của Room/Tenant; đối chiếu migrations, cập nhật ERD toàn schema đã ghép. |
| VHT-49 | Dữ liệu demo và migrate từ DB sạch | Tính | Cả nhóm: dữ liệu phần mình; Phát: DB test riêng | Tạo dữ liệu 4 vai trò, nhiều phạm vi, 2 kỳ; công tắc ở cả 3 chế độ; yêu cầu chờ duyệt mẫu; mục trong thùng rác; lịch sử sửa chỉ số mẫu; chạy migrations từ đầu; dùng dữ liệu giả, tách DB demo/test. |
| VHT-50 | Gia hạn/thanh lý và thông tin cọc P4 | Đào | Tính: quyền/phòng; Phát: review case tiền | Viết service + UI gia hạn/thanh lý/cọc VI/EN; kiểm trạng thái/quyền, giữ lịch sử và cập nhật phòng cùng transaction. |
| VHT-51 | Ghi chỉ số/sử dụng/tính chi phí kỳ thật | Đào | Phúc: VHT-58; Phát: VHT-53 | Lấy hợp đồng/giá/chỉ số kỳ trước; kiểm đầu vào, tính/lưu sử dụng và chi phí snapshot; gửi nguồn hợp lệ cho hóa đơn. |
| VHT-52 | Query hợp đồng/chi phí/sự cố P8 | Đào | Phú: filter DTO; Phát: đối chiếu tài chính | Viết query/DTO theo kỳ và phạm vi; kiểm đếm trùng, lấy trạng thái sự cố từ P7; bàn giao nguồn cho Phú. |
| VHT-53 | Lập và phát hành hóa đơn từ nguồn thật | Phát | Đào: VHT-51; Tính: VHT-48 | Ghép tiền thuê/dịch vụ thật; viết lập/phát hành và query list/detail theo quyền; lưu snapshot và chống trùng kỳ. |
| VHT-54 | Backend yêu cầu thanh toán và xác nhận | Phát | Tính: constraint/quyền; Đào: tiền; storage VHT-13 | Lưu YC/TT/minh chứng; viết xác nhận tiền/hóa đơn/audit cùng transaction, chống trùng/đồng thời; tách gửi YC với đã trả. |
| VHT-55 | UI khách gửi YC và xem kết quả | Phát | Phúc: component/upload; Phú: reconnect | Làm xem hóa đơn, gửi TT/minh chứng và xem kết quả VI/EN; thêm loading/chặn gửi lặp, hiển thị lỗi từ service. |
| VHT-56 | UI quản lý xác nhận/tra giao dịch | Phát | VHT-54; Tính: role; Phú: style | Làm danh sách/detail YC, thao tác xác nhận và tra kết quả sau gián đoạn; không tự gửi lại khoản thu. |
| VHT-57 | Query doanh thu/công nợ P8 | Phát | Phú: DTO/filter; Đào: định nghĩa tiền | Viết query thu đã xác nhận/công nợ theo kỳ và phạm vi; đối chiếu hóa đơn, test số liệu; bàn giao Phú. |
| VHT-58 | Ghép UI chỉ số/chi phí P5 thật | Phúc | Đào: VHT-51; Phát: hóa đơn | Ghép service nhập/lưu/đọc kỳ thật; làm khách xem chi phí mình và chủ trọ/QL xem theo phạm vi; dịch VI/EN. |
| VHT-59 | Rà UI P2/P3/P5 và phần chọn hợp đồng | Phúc | Tính/Đào: data; Phát: case; Phú: layout | Test/sửa filter/paging, lỗi, quyền và refresh; rà DTO binding, che trường nhạy cảm và dữ liệu chọn hợp đồng. |
| VHT-60 | Hoàn thiện kết quả/ảnh sự cố P7 | Phú | Đào: trạng thái/query; Phát: storage; Tính: quyền | Ghép luồng khách báo–QL xử lý–khách nhận KQ, chủ trọ theo dõi; hoàn thiện ảnh private và thử tải trái quyền. |
| VHT-61 | Ghép dashboard và báo cáo thật P8 | Phú | Tính: VHT-41; Đào: VHT-52; Phát: VHT-46/VHT-57 | Thay toàn bộ mẫu bằng nguồn SQL; ghép filter/KPI/table/chart theo kỳ/phạm vi, đối chiếu số và thêm nội dung thay thế biểu đồ. |
| VHT-62 | Reconnect và biểu mẫu quan trọng | Phú | Đào/Phát: service kết quả/draft; Phúc: forms | Làm cảnh báo chưa lưu/reconnect; phối hợp draft server cho form quan trọng khi cần; tra kết quả trước gửi lại, không lặp thu tiền. |
| VHT-63 | Test tiền và transaction P4–P6 | Phát | Đào/Tính: sửa backend; Phúc: test nhập | Viết/chạy test tự động tính tiền, snapshot, trùng kỳ, xác nhận đồng thời, rollback và sai quyền; giao lỗi tác giả sửa. |
| VHT-64 | E2E đủ luồng và VI/EN | Phát | Cả nhóm: chạy/sửa phần mình; Phú: IIS | Viết/chạy Playwright 4 vai trò, P1→P6, P7/report; luồng Quản lý chờ duyệt → Chủ trọ duyệt; Admin tạo Chủ trọ; thùng rác/khôi phục; xuất Excel; test ngôn ngữ/reload/mất kết nối; ghi lỗi, kiểm PR sửa. |
| VHT-65 | CI và triển khai bản tích hợp | Phú | Tính: DB/keys; Phát: smoke; cả nhóm: PR | Ghép PR; hoàn thiện CI build/test/NuGet Audit/publish artifact; deploy IIS, apply migration riêng và chạy smoke. |
| VHT-66 | Word, README dùng và ảnh toàn luồng | Phúc | Dùng tài liệu hiện có; từng người bổ sung nội dung/test/giới hạn khi xong phần mình; Phú nhận bản nháp slide | Viết ngay hướng dẫn 4 vai trò (gồm tạo Admin đầu tiên) và setup VI/EN cho phần đã chạy; đánh dấu phần chưa xong, thêm ảnh thật/kết quả test theo từng bản ghép P1–P8. |
| VHT-102 | Sửa chỉ số kỳ liền trước + lịch sử | Đào | VHT-51; Phúc: VHT-58 (UI); Tính: constraint | Chỉ sửa kỳ liền trước chưa phát hành hóa đơn; bắt buộc lý do; bảng lịch sử (ai, khi nào, cũ → mới, lý do); tính lại chi phí snapshot trong cùng transaction; theo công tắc SW-09. |
| VHT-103 | Xuất báo cáo Excel | Đào | VHT-52, VHT-57, VHT-61 | Thêm ClosedXML; xuất theo quyền/phạm vi và công tắc SW-16; không xuất CCCD; ghi giá trị (không công thức) từ dữ liệu người dùng; giới hạn khối lượng; kiểm giấy phép/phiên bản package. |
| VHT-104 | Màn hình nhật ký thao tác (**có điều kiện D16**) | Phú | VHT-96; Tính: log | Admin xem nhật ký tài khoản/hệ thống, Chủ trọ xem nhật ký nghiệp vụ trong phạm vi; không chứa mật khẩu/token/nội dung giấy tờ. Bỏ task nếu D16 không được chốt. |

### Task tách đợt / Split tasks (tuần 3)

18 task tách từ task gốc nhiều đợt, mỗi task nằm đúng sprint của đợt nó, Parent là Epic của task gốc, mô tả là pointer về task gốc.

| Mã | Việc / Task | Chính | Tách từ | Đợt |
| --- | --- | --- | --- | --- |
| VHT-131 | Kiểm tra phạm vi sau tích hợp | Tính | VHT-47 — Kiểm tra phạm vi xuyên P4/P6/P7 | 3D |
| VHT-132 | Cập nhật ERD theo migration | Tính | VHT-48 — Rà soát schema và ERD tài chính | 3C |
| VHT-133 | Chốt ERD toàn hệ thống | Tính | VHT-48 — Rà soát schema và ERD tài chính | 3D |
| VHT-134 | Kiểm migration và ERD trên release | Tính | VHT-48 — Rà soát schema và ERD tài chính | 3E |
| VHT-135 | Bổ sung demo hóa đơn và giao dịch | Tính | VHT-49 — Dựng dữ liệu demo và migrate DB | 3C |
| VHT-136 | Sửa gia hạn và thanh lý P4 | Đào | VHT-50 — Gia hạn/thanh lý và thông tin cọc P4 | 3D |
| VHT-137 | Phát hành hóa đơn thật P6 | Phát | VHT-53 — Lập và phát hành hóa đơn từ nguồn thật | 3B |
| VHT-138 | Sửa UI P2/P3/P5 theo dữ liệu thật | Phúc | VHT-59 — Rà UI P2/P3/P5 và phần chọn hợp đồng | 3C |
| VHT-139 | Kiểm thử UI P2/P3/P5 trên bản chung | Phúc | VHT-59 — Rà UI P2/P3/P5 và phần chọn hợp đồng | 3E |
| VHT-140 | Ghép nguồn dashboard P8 | Phú | VHT-61 — Ghép dashboard và báo cáo thật P8 | 3C |
| VHT-141 | Ghép đủ nguồn và đối chiếu KPI P8 | Phú | VHT-61 — Ghép dashboard và báo cáo thật P8 | 3D |
| VHT-142 | Regression báo cáo trên release | Phú | VHT-61 — Ghép dashboard và báo cáo thật P8 | 3E |
| VHT-143 | Ghép tra cứu kết quả thanh toán P6 | Phú | VHT-62 — Xử lý reconnect và form quan trọng | 3C |
| VHT-144 | Thử ngắt và nối mạng | Phú | VHT-62 — Xử lý reconnect và form quan trọng | 3D |
| VHT-145 | Kiểm thử tiền thật P4–P6 | Phát | VHT-63 — Kiểm thử tiền và transaction P4–P6 | 3D |
| VHT-146 | Regression tiền trên release | Phát | VHT-63 — Kiểm thử tiền và transaction P4–P6 | 3E |
| VHT-147 | Cập nhật Word sau tích hợp | Phúc | VHT-66 — Viết Word, README và ảnh toàn luồng | 3D |
| VHT-148 | Chụp ảnh IIS thật cho Word | Phúc | VHT-66 — Viết Word, README và ảnh toàn luồng | 3E |

**Cổng cuối tuần / Gate:** demo trọn phòng → khách → hợp đồng → chi phí → hóa đơn → YC/xác nhận → báo cáo; thêm sự cố và dữ liệu phạm vi khác. P1–P8 có SQL thật trên IIS. Còn lỗi phải có mã, chủ sửa và hạn tuần 4. Sửa chỉ số kỳ liền trước có lịch sử; xuất Excel đúng quyền (không CCCD).

## Tuần 4 / Week 4 — Kiểm chứng, sửa lỗi và nộp

**Thứ tự:** khóa chức năng mới → test trên commit chung → sửa/test lại → restore/clone sạch → deploy cuối → chốt tài liệu/demo. Không mở rộng PDF/jobs trong tuần này (Excel xuất báo cáo đã làm ở VHT-103).

### Đầu việc chi tiết / Task details

| Mã | Việc nhỏ / Task | Chính | Phối hợp / Handoff | Nội dung thực hiện / What to do |
| --- | --- | --- | --- | --- |
| VHT-67 | Chốt release candidate và danh sách lỗi | Phú | Phát: kết quả test; cả nhóm: trạng thái task | Chọn commit/tag chung để test; tổng hợp lỗi, ưu tiên và người sửa; thông báo bản kiểm thử cho cả nhóm. |
| VHT-68 | Regression 4 vai trò, tòa nhà khác nhau | Phát | Tính: users/policies; cả nhóm: UI/service | Chạy lại xem/sửa/duyệt/xác nhận và trường hợp cấm qua HTTP/service/trình duyệt; lưu kết quả theo release; chạy lại ma trận xlsx (45 × 4) bằng bộ test VHT-101. |
| VHT-69 | Sửa P1/P2, quyền chung và lỗi database | Tính | Phát: VHT-68 + backend P3; Phúc: UI; Phú: deploy | Sửa/test lại P1/P2, quyền chung và DB; kiểm dữ liệu cũ, không reset để che lỗi; giao backend P3 cho Phát. |
| VHT-70 | Sửa P4/P5/P7 và lịch sử | Đào | Phát: tài chính; Phúc/Phú: UI | Sửa service/UI phần mình; rà trạng thái/quyền/snapshot và rollback khi lỗi giữa thao tác; chạy regression. |
| VHT-71 | Sửa P3/P6/storage và đối chiếu tiền | Phát | Phúc: UI khách; Đào: nguồn; Tính: constraints; Phú: reports | Sửa hồ sơ/giấy tờ, hóa đơn/thu tiền/UI P6; test trùng/đồng thời, đối chiếu report với thu xác nhận và regression. |
| VHT-72 | Rà UI VI/EN P2/P3/P5 và form chung | Phúc | Tính/Đào: validation; Phú: theme | Sửa bản dịch/key thiếu, nhãn trạng thái và giữ input khi lỗi; thử từng link/nút và form chung. |
| VHT-73 | Rà layout/P7/P8 và ngôn ngữ chung | Phú | Cả nhóm: màn hình; Phát: E2E | Test light/dark, 6 viewport 360–1440px, zoom 200%, bàn phím/giảm chuyển động; sửa tràn UI và icon lệch quy chuẩn. |
| VHT-74 | Backup/restore DB, tệp và keys | Tính | Phát: metadata/tệp; Phú: IIS/ACL; Phúc: hướng dẫn | Backup DB/tệp/keys ra bản sao độc lập; restore môi trường riêng, thử đọc dữ liệu/tệp và giải mã keys; ghi cách phục hồi. |
| VHT-75 | Kiểm secrets, log và dữ liệu cá nhân | Tính | Phát: upload/download; Phú: cấu hình | Rà repo/publish/secrets/uploads/keys; thử giới hạn tệp/login, kiểm endpoint/health/log không lộ dữ liệu nhạy cảm. |
| VHT-76 | Clone sạch theo README | Phúc | Tính: connection/migrate; Phát: test; Phú: repo | Nhờ người khác clone mới, làm theo README VI/EN để restore/migrate/build/test/run; sửa hướng dẫn và phụ thuộc file máy cá nhân. |
| VHT-77 | Deploy cuối và smoke IIS | Phú | Tính: migration/keys; Phát: smoke; cả nhóm: PR | Deploy bản CI đạt; thử HTTPS/WebSocket/storage/health/reconnect; chuẩn bị rollback code, không downgrade schema chưa kiểm. |
| VHT-78 | Duyệt nghiệm thu P1–P8 | Phát | Cả 5 người: trình diễn phần mình; Phú: bản deploy | Chạy checklist cuối file trên bản deploy; ghi commit/case/kết quả, tổng hợp lỗi cản luồng/tiền/quyền để sửa trước nghiệm thu. |
| VHT-79 | Đồng bộ ERD/kiến trúc cuối | Tính | Phúc: Word; Đào/Phát: schema; Phú: release | Đối chiếu source/migrations cuối, cập nhật ERD và cây folder; gửi Phúc thay sơ đồ cũ trong báo cáo. |
| VHT-80 | Chốt Word và README | Phúc | Dùng bản VHT-66; Tính gửi ERD cuối, Phát gửi kết quả test, từng người rà phần mình khi có | Cập nhật liên tục mục tiêu/phạm vi, thiết kế, triển khai, test, phân công và hướng dẫn; chỉ chốt ảnh/số liệu/giới hạn theo release đã nghiệm thu. |
| VHT-81 | Chốt slide và kịch bản demo | Phú | Dùng nội dung/ảnh đã có; Phúc gửi bản Word mới, Phát gửi bộ test; cả nhóm rà lời trình bày | Dựng slide và chia phần trình bày sớm từ nội dung hiện có; sau release, thay ảnh/số liệu bằng bản thật và chạy thử kịch bản 4 vai trò (thêm Admin). |
| VHT-82 | Chạy thử demo và đóng gói | Phúc | Phú: release/slide; Tính/Đào/Phát: kiểm source | Chạy thử demo cùng 5 người; đóng gói source/release/report/slide theo yêu cầu môn; loại secret và dữ liệu thật khỏi bản nộp. |

### Task tách đợt / Split tasks (tuần 4)

11 task tách từ task gốc nhiều đợt, mỗi task nằm đúng sprint của đợt nó, Parent là Epic của task gốc, mô tả là pointer về task gốc.

| Mã | Việc / Task | Chính | Tách từ | Đợt |
| --- | --- | --- | --- | --- |
| VHT-149 | Kiểm thử lại sau sửa lỗi | Phát | VHT-68 — Regression 4 vai trò, tòa nhà khác nhau | 4B |
| VHT-150 | Regression cuối trước deploy | Phát | VHT-68 — Regression 4 vai trò, tòa nhà khác nhau | 4C |
| VHT-151 | Sửa lỗi regression P4/P5/P7 | Đào | VHT-70 — Sửa lỗi P4/P5/P7 và lịch sử | 4B |
| VHT-152 | Sửa lỗi tiền và storage P6 | Phát | VHT-71 — Sửa lỗi P3/P6 và đối chiếu tiền | 4B |
| VHT-153 | Kiểm thử UI sau sửa lỗi | Phúc | VHT-72 — Rà UI VI/EN P2/P3/P5 và form chung | 4B |
| VHT-154 | Thực hiện restore DB và tệp | Tính | VHT-74 — Backup/restore DB, tệp và keys | 4B |
| VHT-155 | Backup cuối theo release | Tính | VHT-74 — Backup/restore DB, tệp và keys | 4D |
| VHT-156 | Triển khai bản cuối và smoke | Phú | VHT-77 — Triển khai cuối và smoke IIS | 4C |
| VHT-157 | Ghép ERD, ảnh và test vào Word | Phúc | VHT-80 — Chốt Word và README | 4C |
| VHT-158 | Chốt Word theo release | Phúc | VHT-80 — Chốt Word và README | 4D |
| VHT-159 | Làm slide và chạy demo release | Phú | VHT-81 — Chốt slide và kịch bản demo | 4D |

**Cổng cuối tuần / Gate:** checklist nghiệm thu đạt trên bản deploy, không còn lỗi cản luồng/tiền/quyền; clone sạch và restore đã thử; tài liệu/source/demo cùng phiên bản. Không cam kết hoàn thành chỉ vì lịch 4 tuần đã kết thúc.

## Checklist nghiệm thu / Definition of Done

Phát ghi kết quả; tác giả backend/UI cùng trình diễn. Chỉ tick khi có bằng chứng; scope chưa rõ phải chốt rồi cập nhật case.

| Đạt | Phần | Backend / UI | Bằng chứng cần có |
| --- | --- | --- | --- |
| [ ] | P1 — Accounts / Tài khoản, quyền | Tính / Tính | Login/logout, cá nhân, Admin quản lý tài khoản/vai trò, Chủ trọ duyệt/khóa tài khoản, vòng đời tài khoản (chờ duyệt/hoạt động/khóa), phạm vi quản lý; 4 vai trò và truy cập trái quyền đã test; ma trận xlsx đạt. |
| [ ] | P2 — Rooms / Phòng | Tính / Phúc | Nhà trọ/phòng, giá/hiện trạng, chủ trọ xem/thiết lập giá, QL cập nhật, khách xem phòng thuê; đọc lại SQL. QL đổi giá/sửa theo công tắc; xóa vào thùng rác và khôi phục. |
| [ ] | P3 — Tenants / Khách thuê | Phát / Phúc | Hồ sơ/liên kết user, khách sửa cá nhân đúng quyền; giấy tờ private, query nguồn report đúng phạm vi; thùng rác hồ sơ khách; Quản lý chỉ thấy trường được phép (CCCD che). |
| [ ] | P4 — Contracts / Hợp đồng | Đào / Đào | Tạo/duyệt/gia hạn/thanh lý/cọc theo đặc tả; cập nhật phòng nguyên tử, snapshot, khách xem đúng hợp đồng; QL tạo → Chờ duyệt; Chủ trọ duyệt/từ chối; gia hạn/thanh lý/cọc theo công tắc. |
| [ ] | P5 — Utilities / Điện nước, dịch vụ | Đào / Phúc | Đơn giá/dịch vụ, chỉ số kỳ trước/mới, chi phí lưu thật, giá lịch sử, khách xem đúng dữ liệu; nguồn hóa đơn hợp lệ; biểu giá điện/nước cấu hình khóa, test từng bậc; sửa chỉ số kỳ liền trước có lịch sử; kỳ đã phát hành hóa đơn bị chặn. |
| [ ] | P6 — Invoices & payments / Hóa đơn, thu tiền | Phát / Phát | Lập/phát hành/xem; khách gửi YC/TT, QL xác nhận, khách nhận kết quả; chống trùng/rollback, công nợ khớp; QL xác nhận trực tiếp (D6); Quản lý không xem doanh thu tổng hợp; không trả một phần/hủy hóa đơn (D13). |
| [ ] | P7 — Maintenance / Sự cố | Đào / Phú | Khách báo → QL xử lý → khách nhận trạng thái/KQ; chủ trọ theo dõi, ảnh kiểm quyền và trạng thái hợp lệ. |
| [ ] | P8 — Reports / Báo cáo | Tính/Đào/Phát: nguồn / Phú | Nguồn phòng/khách/hợp đồng/chi phí/thu tiền/sự cố; filter/KPI/bảng/chart khớp, QL đúng phạm vi, khách không có quản trị; xuất Excel đúng quyền, không CCCD; QL chỉ thấy mục được phép. |
| [ ] | Admin / công tắc / phê duyệt | Tính / Phú | Công tắc 16 thao tác ở 3 chế độ; yêu cầu Chờ duyệt → duyệt/từ chối/hủy; chống duyệt trùng; Admin bị chặn mọi thao tác nghiệp vụ. |
| Shared UI & localization / UI và song ngữ | Cả 5; Phú/Phúc phối hợp | VI/EN toàn màn hình + validation; selector lưu lựa chọn, VND không đổi; light/dark/mobile/keyboard/loading/rỗng/lỗi; nhãn trạng thái Chờ duyệt/Admin có đủ VI/EN. |
| [ ] | Safety & recovery / An toàn, phục hồi | Tính/Phát/Phú | SQL constraints, keys/secrets/antiforgery, tệp private, safe errors/log; reconnect không lặp tiền; backup/restore đạt. |
| [ ] | Tests & delivery / Test, bàn giao | Phát điều phối; Phúc/Phú tổng hợp | Unit/integration SQL/E2E thật + CI; IIS smoke, clone sạch; README/Word/ERD/slide/demo đúng release. |

## Khi khối lượng lệch / Rebalancing

- **Cuối mỗi tuần**, mỗi người báo: mã xong, mã chặn, thời gian còn cần; cân theo thời gian/độ khó, không đếm dòng task. Không chuyển việc mà không cập nhật người nhận và bàn giao.
- Tính bị dồn P1/P2/DB (sau khi thêm 5 task, Tính có 20 task, 12 Highest; nếu quá tải: chuyển VHT-98 thùng rác phòng sang Đào): Đào review validation P2; Phú tự xử lý PR/CI và cấu hình IIS theo phần đã giao, chỉ hỏi Tính điểm quyền/DB cần chốt. Không chuyển lỗi P3 trở lại Tính mặc định.
- Đào bị dồn P4/P5/P7: Phát chỉ hỗ trợ query/test P7 khi P3 đã bàn giao và chưa vào cao điểm P6; không lấy thời gian hoàn thiện P3 hoặc giao dịch tài chính để chạy việc phụ.
- Phát ưu tiên P3 + storage tuần 1–2, P6 tuần 3. Nếu P3 còn chặn cuối tuần 2, báo mã/đầu ra/thời gian thiếu để cả nhóm chia lại trước khi triển khai thu tiền; không âm thầm ôm cả hai luồng.
- Phát bị dồn backend/test: Tính/Đào tự viết và chạy test service của mình; Phúc/Phú chạy case UI theo checklist. Phát chỉ tổng hợp, giữ thời gian cho P6.
- Phúc/Phú bị dồn UI/tài liệu: từng người nộp nội dung + ảnh + test phần mình, Tính nhận PR/CI khi Phú deploy; không để người làm Word/slide viết lại cả hệ thống.
- Quy tắc chưa rõ: ghi quyết định với cả nhóm trước code. **Không tự thêm** PDF/jobs/payment gateway/tiền phạt, trả một phần, hủy hóa đơn hoặc nghiệp vụ cọc ngoài phần đã chốt. Excel chỉ ở “Xuất báo cáo” P8 (VHT-103).

**Nguồn:** [P1–P8](../thiet_ke_he_thong.md) · [UI](../design/phong_cach_giao_dien.md) · [công nghệ](../tech-stack/cong_nghe_su_dung.md) · [cây folder](../architecture/cau_truc_thu_muc.md).
