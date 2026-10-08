# Phân công 5 người — Web vận hành nhà trọ

> Xem thứ tự làm trước/sau tại [công việc theo tuần](cong_viec_theo_tuan.md).

**Dự kiến 4 tuần, ưu tiên cân bằng khối lượng.** Phúc/Phú nhận thêm backend vừa sức, Tính/Mai/Phát hỗ trợ. Mỗi người làm entity/EF, test cơ bản và nội dung báo cáo của phần mình; không dồn toàn bộ SQL, test hoặc tài liệu cho một người.

**Cột “Hỏi ai / cần xong gì?” ghi người phải liên hệ và đầu ra cần nhận**, không chỉ ghi mã việc. Hỏi rõ phần đó **chưa làm / đang làm / đã xong**, link code/PR và thời điểm bàn giao; chưa có xác nhận thì không tự coi là đã xong. Tuần là mốc dự kiến.

**INIT do bạn chuẩn bị trước:** repo, project, folder, package và cấu hình nền. Nhóm nhận khung có sẵn, không tạo lại hoặc làm các việc init. Các mục dưới đây chỉ là phát triển chức năng, hoàn thiện cấu hình cần cho nghiệp vụ và kiểm tra bản chạy thật.

## Tính Ct

| Việc | Làm gì / bàn giao | Ảnh hưởng tới ai? | Hỏi ai / cần xong gì? | Tuần |
| --- | --- | --- | --- | --- |
| Database/DTO chung | Cùng nhóm chốt schema lõi tối thiểu (Identity, `Property`, phòng, khách) và DTO dùng chung; các bảng hợp đồng/chi phí/hóa đơn/thu tiền/sự cố bổ sung dần theo tuần bằng migration cộng thêm, không sửa vỡ bảng cũ. Giữ nguyên tắc mở rộng: danh mục dịch vụ + `InvoiceItem`, snapshot giá, audit/xóa mềm có chọn lọc, enum đúng luồng; kiểm schema/migrations theo lượt | Mai, Phát, Phúc, Phú: bảng và trường dữ liệu dùng chung | Hỏi bạn đã bàn giao INIT chưa; hỏi từng người đã chốt trường/quan hệ của phần mình chưa | 1 |
| Tài khoản/phân quyền | Entity, backend/UI đăng nhập, tài khoản cá nhân/quản trị, role/phạm vi; bàn giao kiểm quyền | Mai/Phát/Phúc: Service kiểm quyền; Phú: menu và báo cáo | Hỏi nhóm đã chốt quyền chưa; hỏi Phú theme/layout đã dùng được chưa để ghép UI | 1–2 |
| Bảo mật/log chức năng | Hoàn thiện giới hạn đăng nhập/upload, keys, log, lỗi và health từ cấu hình có sẵn | Phúc: tệp; Phát: giao dịch; Phú: triển khai | Hỏi bạn cấu hình nền đã có gì; hỏi Phúc/Phát giới hạn tệp và log cần dùng là gì | 1–2 |
| DB/phục hồi cuối | Kiểm DB sạch, FK/unique, demo; thử backup/restore DB, tệp và keys | Cả nhóm: bản nộp/demo; Phú: phục hồi triển khai | Hỏi các thành viên migrations đã ghép chưa; hỏi Phú bản IIS và Phúc storage đã chạy thật chưa | 3–4 |

## Mai Đào

| Việc | Làm gì / bàn giao | Ảnh hưởng tới ai? | Hỏi ai / cần xong gì? | Tuần |
| --- | --- | --- | --- | --- |
| Backend hợp đồng/cọc | Tạo, duyệt, gia hạn, thanh lý; cập nhật phòng cùng transaction, chống hợp đồng trùng hiệu lực | Phúc: trạng thái phòng; Phát: tiền thuê; Phú: phòng thuê/báo cáo | Hỏi Phúc dữ liệu phòng/khách và bước ghi trạng thái đã có chưa; hỏi Tính schema/kiểm quyền đã dùng được chưa | 2 |
| Giao diện hợp đồng | List/form, chọn phòng/khách, duyệt/gia hạn/thanh lý, cọc; khách xem hợp đồng | Phúc: dữ liệu chọn phòng/khách; Phát: test luồng | Hỏi Phú layout, Phúc danh sách chọn đã có chưa; ghép sau khi backend hợp đồng của mình chạy được | 1–2 |
| Backend điện nước/dịch vụ | Đơn giá, chỉ số, sử dụng, tính/lưu chi phí; query theo quyền và DTO hóa đơn | Phú: UI chi phí; Phát: lập hóa đơn | Hỏi Tính kỳ/công thức/quyền đã chốt chưa; cần dữ liệu hợp đồng của mình. Báo Phát khi DTO chi phí sẵn sàng | 2–3 |
| Utils/cấu hình | Validation Options, TimeProvider, UTC/Việt Nam, định dạng tiền/ngày và test; không chứa nghiệp vụ thu tiền | Tất cả người làm form/ngày/tiền | Hỏi bạn tiện ích nào đã có; hỏi Tính/Phát quy tắc ngày/tiền đã chốt chưa, không viết trùng | 1 |
| Test/query hợp đồng/chi phí | Test quyền, lịch sử, chỉ số và rollback; bàn giao số liệu hợp đồng/chi phí | Phú: dashboard; Phát: test tổng | Backend hợp đồng/chi phí của mình phải chạy thật; hỏi Phú filter/DTO báo cáo và Phát case liên chức năng cần gì | 3 |

## Hình Vĩnh Phát

| Việc | Làm gì / bàn giao | Ảnh hưởng tới ai? | Hỏi ai / cần xong gì? | Tuần |
| --- | --- | --- | --- | --- |
| Backend hóa đơn | Tính/lập/phát hành, list/detail theo quyền; giữ giá lịch sử, chống trùng kỳ | Mai: nguồn chi phí; Phú: báo cáo; Phúc: tài liệu | Hỏi Mai hợp đồng, tiền thuê và chi phí đủ điều kiện đã bàn giao chưa; hỏi Tính schema/quyền đã xong chưa | 2–3 |
| Backend thanh toán | Yêu cầu/minh chứng → xác nhận → kết quả; chống trùng, công nợ/audit cùng transaction, tra lại giao dịch | Phú: doanh thu/công nợ; Tính: dữ liệu/audit | Hóa đơn của mình phải chạy; hỏi Phúc storage đã upload/download được chưa, Tính bảo mật/log đã sẵn chưa | 3 |
| Giao diện hóa đơn/thanh toán | Form/list/detail, gửi yêu cầu, xác nhận, xem kết quả; gửi minh chứng không phải đã trả tiền | Phú: layout/reconnect; Phúc: form và ảnh báo cáo | Hỏi Phú theme/layout, Phúc component form đã có chưa; UI mẫu làm trước, ghép backend của mình sau | 2–3 |
| Test tài chính/query | Test tiền, trùng, đồng thời, rollback/quyền; bàn giao doanh thu/công nợ chỉ từ khoản thu xác nhận | Phú: báo cáo tiền; Tính: ràng buộc DB | Hóa đơn/thanh toán của mình phải chạy; hỏi Phú filter/DTO báo cáo đã chốt chưa | 3–4 |
| Kiểm thử tổng | Test ba vai trò và toàn luồng, mất kết nối; ghi lỗi trả người phụ trách | Cả nhóm: sửa lỗi và nộp bài | Hỏi Tính tài khoản, Phúc phòng/khách/tệp, Mai hợp đồng/chi phí, Phú sự cố/báo cáo đã ghép thật chưa; test từng phần sẵn trước | 3–4 |

## Đinh Hoàng Phúc

| Việc | Làm gì / bàn giao | Ảnh hưởng tới ai? | Hỏi ai / cần xong gì? | Tuần |
| --- | --- | --- | --- | --- |
| Phòng | Entity, backend/UI xem/cập nhật giá/hiện trạng; khách xem phòng thuê; bàn giao dữ liệu và bước cập nhật trạng thái cho hợp đồng | Mai: hợp đồng; Phú: sự cố/thống kê phòng | Hỏi Tính schema/quyền, Phú layout; bàn giao phòng nền cho Mai trước, hỏi Mai liên kết hợp đồng đã có chưa để ghép trang khách | 1–2 |
| Khách thuê | Entity, backend/UI hồ sơ và thông tin cá nhân; liên kết user, query chọn khách/thống kê | Mai: chọn khách lập hợp đồng; Phú: báo cáo khách | Hỏi Tính user/schema/quyền đã có chưa; hỏi Mai/Phú trường cần dùng để chốt DTO trước khi viết | 1–2 |
| Storage/giấy tờ | Tệp ngoài wwwroot, giới hạn/kiểm loại, metadata và tải có kiểm quyền; dùng lại cho CCCD, minh chứng, ảnh | Phát: minh chứng; Phú: ảnh sự cố; Tính: phục hồi | Hỏi bạn storage nền có gì, Tính cấu hình/quyền; bàn giao storage trước, hỏi Phát/Phú cách kiểm quyền theo hóa đơn/phiếu khi ghép | 1–2 nền; 3 ghép |
| Form/bảng dùng chung | Form, filter/phân trang, badge, dialog/loading/rỗng/lỗi; test UI phòng/khách/mobile | Tính, Mai, Phát, Phú: component dùng lại | Hỏi Phú token/theme đã có chưa; hỏi mọi người component nào cần dùng chung, không tự thay contract đang dùng | 1–2; 4 rà soát |
| Word/README | Ghép nội dung, sơ đồ bảng, ảnh, hướng dẫn dùng/cài đặt và kết quả test | Phú: slide; cả nhóm: bài nộp | Hỏi từng người ảnh/nội dung đã gửi chưa; hỏi Phát test và Phú bản triển khai đã chốt chưa trước khi xuất báo cáo cuối | 1–4 |

## Phú

| Việc | Làm gì / bàn giao | Ảnh hưởng tới ai? | Hỏi ai / cần xong gì? | Tuần |
| --- | --- | --- | --- | --- |
| Theme/layout | Font Inter, token sáng/tối, SVG Lucide, sidebar/topbar/breadcrumb, responsive theo quy chuẩn; không trộn Material | Cả nhóm: style; Phúc: shared component | Hỏi bạn khung UI có gì; làm style trước, hỏi Tính quyền/menu đã có chưa để ghép sau | 1 |
| Bảo trì/sự cố | Entity, backend/UI báo sự cố, xử lý/theo dõi và kết quả đúng quyền; query sự cố | Phúc: ảnh/phòng; Phát: test; dashboard | Hỏi Tính schema/quyền, Phúc phòng/storage, Mai liên kết phòng đang thuê đã sẵn chưa | 2–3 |
| Giao diện điện nước/dịch vụ | Đơn giá, nhập chỉ số/dịch vụ, tính/lưu và xem chi phí theo kỳ | Mai: gọi đúng Service; Phát: dữ liệu đầu vào hóa đơn | Hỏi Mai DTO đã chốt và Service đã xong phần nào; lấy mẫu để làm UI trước, chỉ ghép phần chạy thật | 2–3 |
| Dashboard/báo cáo | Ghép query, filter/bảng/MudChart, số liệu đúng phạm vi; khách không có báo cáo quản trị | Mai/Phát/Phúc: query nguồn; Phát: đối chiếu tiền | Hỏi Phúc query phòng/khách, Mai hợp đồng/chi phí, Phát doanh thu/công nợ đã bàn giao chưa; ghép từng nguồn, không chờ tất cả mới làm UI | 2 mẫu; 3 thật |
| GitHub/IIS/slide | Khung repo/CI do bạn làm sẵn. Tuần 1 Phú chính + Tính hỗ trợ luân phiên ghép PR/kiểm CI (Tính ít hơn vì đang 4 việc); triển khai HTTPS/WebSocket/reconnect tuần 3–4; slide/demo, hỗ trợ phục hồi | Cả nhóm: nhánh chung/bản cuối; Phúc: ảnh báo cáo; Tính: phục hồi | Hỏi từng người PR/test đã xong chưa, Tính cấu hình triển khai, Phúc README/Word và Phát lỗi còn lại; không ghi đè phần chưa merge | 1–4 ghép; 3–4 triển khai |

## Quy tắc ngắn để làm song song

- **Trước tuần 1:** bạn bàn giao INIT. **Tuần 1:** chốt database/DTO và phát triển từ khung. **Tuần 2:** phòng/khách/hợp đồng/chi phí/sự cố. **Tuần 3:** hóa đơn/thu tiền/báo cáo. **Tuần 4:** sửa lỗi, test, triển khai, nộp.
- **Mẫu tin nhắn:** “Mình đang làm …, cần phần … của bạn. Bạn chưa làm/đang làm/xong rồi? Gửi mình DTO, link PR và lúc có thể bàn giao nhé. Mình sẽ sửa …, có ảnh hưởng phần bạn không?”
- **Nếu chưa xong:** thống nhất DTO và làm UI/test bằng mẫu trước; nhờ người phụ trách ưu tiên đầu ra đang chặn. Ghi rõ đang chờ ai/phần gì, không tự sửa file của họ. **Nếu đã xong:** pull/nhận PR, ghép và kiểm tra lại; xong thì báo những người ở cột “Ảnh hưởng”.
- **Không chờ vòng:** Phúc bàn giao phòng nền trước, Mai trả liên kết thuê sau; Phúc bàn giao storage trước, Phát/Phú cấp kiểm quyền resource khi ghép; Phú bàn giao style trước, Tính trả quyền menu sau. UI mẫu không được coi là chức năng đã chạy thật.
- Mai/Phát/Phúc/Phú tự làm entity/configuration/test của phần mình; **Tính không gánh toàn bộ SQL**. Tính ghép schema, Phú ghép PR; migrations và file chung sửa theo lượt, không ghi đè.
- Service đầu vào sở hữu context/transaction; bước nội bộ không tự commit. Mỗi người cung cấp query báo cáo phần mình, ảnh/nội dung Word/slide và tự demo; người tổng hợp không làm hộ.
- Phát điều phối test tổng, mỗi người tự test và sửa lỗi phần mình. Rà soát UI gồm bàn phím, giảm chuyển động, viewport 360–1440 và zoom 200%; tệp nhạy cảm không public, log không lộ secret.
- Cân lại sau tuần 1 bằng thời gian thực tế, không đếm số dòng. Nếu Phúc/Phú chưa quen backend, Tính hỗ trợ phòng/khách, Mai hỗ trợ bảo trì; không chuyển cả module cho người hỗ trợ.
- Nghiệp vụ chưa rõ hỏi cả nhóm và chốt trước khi code. Excel/PDF/tác vụ định kỳ chỉ thêm nếu được yêu cầu và chia lại khối lượng; không tự thêm cổng thanh toán hoặc xử lý cọc ngoài phạm vi.

**Nguồn:** [nghiệp vụ](../thiet_ke_he_thong.md) · [giao diện](../design/phong_cach_giao_dien.md) · [kiến trúc](../architecture/cau_truc_thu_muc.md) · [công nghệ](../tech-stack/cong_nghe_su_dung.md).
