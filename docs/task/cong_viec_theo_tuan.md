# Thứ tự công việc theo tuần — Web vận hành nhà trọ

Đây là **lịch thực hiện của [bảng phân công theo người](phan_cong_cong_viec.md)**, không phải công việc bổ sung. Giữ nguyên người phụ trách, chỉ sắp lại thứ tự để dễ theo dõi.

**INIT do bạn làm và bàn giao trước tuần 1.** Các dòng được xếp theo đầu ra cần trước; việc độc lập được làm song song, không phải chờ lần lượt từng dòng. UI dùng mẫu sau khi chốt DTO, ghép thật khi backend sẵn sàng.

## Tuần 1 — Schema lõi và phần nền dùng chung

| Việc làm trước → sau | Người làm | Xong khi nào? | Hỏi ai trước / bàn giao cho ai? |
| --- | --- | --- | --- |
| Chốt schema lõi, DTO, quyền và nguyên tắc mở rộng | **Tính + cả nhóm** | Có bảng lõi (`Property`, phòng, khách), contract và quy tắc mở rộng thống nhất; trường chưa rõ để lại, bổ sung sau bằng migration cộng thêm | Hỏi bạn INIT đã bàn giao chưa; gửi bản chốt cho cả nhóm |
| Hoàn thiện theme/layout/icon từ khung có sẵn | **Phú** | Có sidebar/topbar, token sáng/tối, Inter/Lucide, responsive đúng quy chuẩn | Hỏi bạn phần UI nền đã có gì; gửi style cho Phúc, Mai, Phát, Tính |
| Utils ngày/tiền và validation cấu hình nghiệp vụ | **Mai** | Các hàm dùng chung chạy/test được, không viết lại phần đã có | Hỏi bạn tiện ích nền, Tính/Phát quy tắc ngày/tiền; gửi cách dùng cho cả nhóm |
| Tài khoản và kiểm quyền nền | **Tính** | Đăng nhập/đăng xuất, user hiện tại, role/phạm vi dùng được | Hỏi nhóm quyền đã chốt; báo Mai/Phát/Phúc dùng Service, Phú ghép menu |
| Bảo mật/log/lỗi/health cho chức năng | **Tính** | Cấu hình được kiểm tra, không lộ secret/giấy tờ; endpoint health dùng được | Hỏi Phúc/Phát nhu cầu upload/log; báo Phúc, Phát, Phú |
| Mẫu form/bảng/dialog/loading dùng chung | **Phúc** | Component theo theme dùng được, có rỗng/lỗi/validation | Hỏi Phú token/style và nhóm nhu cầu; gửi component cho người làm UI |
| Bắt đầu entity/backend/UI phòng và khách | **Phúc** | Có dữ liệu phòng/khách nền, DTO chọn và màn hình mẫu; chưa cần trang phòng thuê hoàn chỉnh | Hỏi Tính schema/user/quyền; ưu tiên gửi danh sách phòng/khách cho Mai |
| UI hợp đồng mẫu; chuẩn bị DTO/case test tài chính | **Mai: UI; Phát: tài chính** | Form hợp đồng theo DTO, dữ liệu/case tiền để viết backend/test tiếp | Hỏi Phúc trường phòng/khách, Phú layout, Tính schema; Mai/Phát thống nhất nguồn tiền thuê/chi phí |
| Ghép PR/kiểm CI; bắt đầu README, Word và slide | **Phú chính, Tính hỗ trợ luân phiên tuần 1; Phúc: README/Word; mọi người cung cấp nội dung** | Nhánh chung chạy được, có dàn ý và hướng dẫn từ INIT | Hỏi từng người PR đã sẵn chưa; Tính chỉ hỗ trợ khi rảnh vì đang 4 việc, không ghi đè file đang làm |

**Cuối tuần cần có:** contract chung, style/component, đăng nhập/kiểm quyền và dữ liệu phòng/khách nền. Việc độc lập như UI mẫu/tài liệu không phải chờ backend hoàn tất.

## Tuần 2 — Ghép phòng/khách/hợp đồng, chuẩn bị chi phí và hóa đơn

| Việc làm trước → sau | Người làm | Xong khi nào? | Hỏi ai trước / bàn giao cho ai? |
| --- | --- | --- | --- |
| Hoàn thiện phòng, khách và quyền thao tác | **Phúc** | List/detail/cập nhật đúng quyền; dữ liệu chọn phòng/khách và bước ghi trạng thái dùng được | Hỏi Tính kiểm quyền; báo Mai dùng để lập hợp đồng, Phú dùng cho sự cố/báo cáo |
| Hoàn thiện storage/giấy tờ nền | **Phúc** | Upload/download, metadata, giới hạn tệp và contract kiểm quyền dùng được | Hỏi Tính cấu hình; gửi cho Phát làm minh chứng và Phú làm ảnh sự cố |
| Backend + ghép UI hợp đồng/cọc | **Mai** | Tạo/duyệt/gia hạn/thanh lý đúng quyền; phòng cập nhật cùng transaction, chống trùng hiệu lực | Hỏi Phúc phòng/khách đã bàn giao chưa, Tính quyền/schema; gửi liên kết thuê cho Phúc/Phú, tiền thuê cho Phát |
| Ghép phòng đang thuê của khách | **Phúc** | Khách chỉ xem phòng thuộc hợp đồng mình | Hỏi Mai liên kết hợp đồng đã có chưa; không chặn bàn giao phòng nền trước đó |
| Backend đơn giá/chỉ số/dịch vụ | **Mai** | Nhập dữ liệu và rule tính chi phí chạy/test được theo contract; tiếp tục ghép kỳ thật ở tuần 3 | Hỏi Tính quy tắc đã chốt; gửi DTO chi phí cho Phú làm UI và Phát làm hóa đơn |
| UI điện nước/dịch vụ và bảo trì nền | **Phú** | Form/bảng theo DTO; backend sự cố nhận/đọc dữ liệu nền, sẵn ghép quyền/ảnh | Hỏi Mai DTO chi phí/hợp đồng, Phúc phòng/storage, Tính quyền; dùng mẫu nếu nguồn chưa xong |
| Backend + UI hóa đơn/thanh toán mẫu | **Phát** | Tính tổng/test với mẫu, list/form theo DTO; chưa phát hành thật khi nguồn chi phí chưa hợp lệ | Hỏi Mai tiền thuê/chi phí, Phúc shared form và storage; thống nhất đầu vào trước khi ghép |
| Dashboard/báo cáo mẫu và query nguồn đầu tiên | **Phú: UI; Phúc/Mai/Phát: nguồn phần mình** | Layout/filter/DTO báo cáo thống nhất, có số mẫu và query nguồn đã sẵn | Phú hỏi Phúc số liệu phòng/khách, Mai hợp đồng/chi phí, Phát tài chính; ghép từng nguồn |
| Test phần đã xong, cập nhật tài liệu và ghép PR | **Mỗi người test phần mình; Phát phối hợp; Phúc/Phú tổng hợp** | Luồng tài khoản → phòng/khách → hợp đồng chạy; lỗi có người nhận sửa | Hỏi người phụ trách đã ghép thật chưa; báo lỗi đúng người, không đợi hết hệ thống mới test |

**Cuối tuần cần có:** luồng tài khoản/phòng/khách/hợp đồng chạy thật; storage dùng được; điện nước, sự cố và hóa đơn đã có phần nền để ghép tiếp.

## Tuần 3 — Chạy đủ luồng thu tiền và báo cáo

| Việc làm trước → sau | Người làm | Xong khi nào? | Hỏi ai trước / bàn giao cho ai? |
| --- | --- | --- | --- |
| Hoàn thiện/test chi phí điện nước/dịch vụ | **Mai: backend; Phú: UI** | Chi phí kỳ thật được lưu đúng, người dùng xem đúng phạm vi | Phú hỏi Mai Service đã xong phần nào; Mai báo Phát dữ liệu nào được dùng lập hóa đơn |
| Hoàn thiện sự cố và quyền ảnh | **Phú; Phúc hỗ trợ storage** | Khách báo → Quản lý xử lý → khách nhận kết quả, Chủ trọ theo dõi; ảnh không public | Hỏi Phúc storage/phòng, Mai liên kết thuê, Tính quyền; báo Phát để test |
| Ghép lập/phát hành hóa đơn thật | **Phát** | Tiền thuê/chi phí đúng nguồn, giữ giá lịch sử, không trùng kỳ | Hỏi Mai hợp đồng và chi phí đủ điều kiện đã bàn giao chưa; báo Phú dữ liệu hóa đơn |
| Ghép yêu cầu → xác nhận → kết quả thanh toán | **Phát; Phúc hỗ trợ tệp** | Minh chứng được nhận; xác nhận ghi tiền/hóa đơn/audit nguyên tử, tra lại giao dịch được | Hỏi Phúc tệp, Tính bảo mật/log; báo Phú nguồn khoản thu/công nợ đã xác nhận |
| Test/query hợp đồng, chi phí và tài chính | **Mai: hợp đồng/chi phí; Phát: thu tiền; Phúc: phòng/khách** | Có test và số liệu đúng định nghĩa để báo cáo sử dụng | Hỏi Phú filter/DTO cần dùng; gửi query/kết quả mẫu, không để Phú viết lại nghiệp vụ |
| Ghép dashboard/báo cáo thật | **Phú** | KPI/bảng/biểu đồ khớp nguồn và quyền; không có báo cáo quản trị cho khách | Hỏi Phúc phòng/khách, Mai hợp đồng/chi phí, Phát thu tiền; nguồn chưa có ghi rõ chưa ghép, không dùng mẫu giả làm thật |
| Kiểm DB và test toàn luồng | **Tính: DB; Phát: test tổng; cả nhóm sửa phần mình** | Migrations/constraint đúng; số tiền/role/tệp và liên kết P1–P8 được kiểm | Hỏi từng người phần nào đã sẵn; test trùng hóa đơn, xác nhận đồng thời và rollback |
| Publish thử, kiểm reconnect, bổ sung ảnh/tài liệu | **Phú: IIS/reconnect; Phúc: tài liệu; Tính hỗ trợ cấu hình** | Bản tích hợp chạy HTTPS/WebSocket, không tự gửi lại giao dịch; ảnh đúng bản thật | Hỏi Tính cấu hình/keys, Phát lỗi nghiêm trọng còn gì; mỗi người gửi ảnh/nội dung phần mình |

**Cuối tuần cần có:** P1–P8 ghép dữ liệu thật, khoản thu/công nợ/báo cáo khớp nhau và có bản IIS thử nghiệm. Ghi rõ lỗi còn lại để sửa tuần 4.

## Tuần 4 — Sửa lỗi, kiểm bản cuối và nộp

| Việc làm trước → sau | Người làm | Xong khi nào? | Hỏi ai trước / bàn giao cho ai? |
| --- | --- | --- | --- |
| Test ba vai trò, tài chính, mất kết nối và quyền tệp | **Phát điều phối; mỗi người kiểm tra phần mình** | Có kết quả test và lỗi được ghi rõ người phụ trách | Hỏi cả nhóm bản ghép nào dùng test; báo lỗi và bước tái hiện cho người làm feature |
| Sửa lỗi, test hồi quy và rà soát UI | **Tính/Mai/Phát/Phúc/Phú theo phần đã giao** | Lỗi quan trọng được sửa/test lại; UI đúng theme, mobile, bàn phím, zoom và giảm chuyển động | Hỏi Phát lỗi/test nào chưa đạt; báo Phú PR sửa đã sẵn, không sửa đè file |
| Backup/restore và kiểm bản sạch | **Tính + Phú; Phúc hỗ trợ tệp/hướng dẫn** | Khôi phục DB/tệp/keys thử được; clone sạch, migrate/build/test theo README | Hỏi Phú bản triển khai, Phúc đường dẫn tệp/README; báo nhóm nếu dữ liệu hoặc keys chưa phục hồi được |
| Ghép và triển khai bản cuối | **Phú; mọi người bàn giao PR** | CI đạt, IIS/HTTPS/health và các luồng chính chạy sau deploy | Hỏi Phát lỗi quan trọng đã hết chưa, Tính schema/keys đã kiểm chưa; báo cả nhóm bản cuối |
| Chốt Word/README/slide | **Phúc: Word/README; Phú: slide; cả nhóm gửi nội dung** | Tài liệu, ảnh, số liệu và giới hạn đúng phiên bản cuối | Hỏi Phát kết quả test, Tính sơ đồ DB và từng người nội dung còn thiếu; không lấy ảnh/số liệu bản cũ |
| Chạy thử demo và đóng gói nộp | **Cả 5 người** | Demo ba vai trò chạy; mỗi người giải thích phần mình; source/report/slide đủ yêu cầu môn | Hỏi Phúc/Phú file nộp đã chốt chưa; chạy lại trên bản cuối trước khi nộp |

## Khi bị chặn hoặc thay đổi đầu ra

- Hỏi: **“Phần mình cần của bạn chưa làm, đang làm hay đã xong? Cho mình DTO/link PR và lúc bàn giao.”** Ghi rõ đang chờ người nào/phần gì, không đoán đã xong.
- Chưa xong: thống nhất DTO rồi làm UI/rule/test bằng mẫu trước. Các nguồn thật như hợp đồng/chi phí/xác nhận thu vẫn phải đủ trước khi ghép giao dịch thật.
- Xong: bàn giao và báo người bị ảnh hưởng; pull/ghép/test lại. File chung và migrations sửa theo lượt; không đổi schema/DTO âm thầm.
- Không phát sinh người làm INIT hoặc phân công mới. Chi tiết trách nhiệm, hỗ trợ và điều kiện bổ sung chức năng giữ theo [file phân công](phan_cong_cong_viec.md).
