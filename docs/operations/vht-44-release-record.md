# VHT-44 — Theo dõi phát hành IIS thử

Hướng dẫn: [IIS deployment](iis-deployment.md). Chỉ điền kết quả thực tế; không ghi secret, cookie, thông tin giấy tờ hoặc keys.

## Hiện trạng chuẩn bị ngày 27/10/2026

- Nguồn chuẩn bị: `dev` tại `1daaf4f77ad9dbc06a9e0715e076a67f232049a2`; đây là base nhánh triển khai, **chưa phải SHA deploy**.
- Nhánh: `VHT-44-ghep-pr-va-phat-hanh-iis-thu`.
- Tra cứu GitHub tại thời điểm chuẩn bị không thấy PR mở về `dev`; PR #38 (P7), #39 (UI P5), #40 (bảo mật), #42 (CODEOWNERS) đã xuất hiện trong lịch sử base local sau fetch. Chưa coi các PR này là danh sách phát hành được chốt hoặc chứng minh CI/review đạt cho bản deploy.
- Chưa được cung cấp server/URL/certificate, SQL thử/migration được bàn giao và tài khoản smoke. Chưa chạy build/publish/smoke; chưa thao tác IIS hoặc database server.

## Bản phát hành theo đợt

### Kiểm local ngày 29/10/2026

Người dùng xác nhận chưa deploy. Đã restore, build Release và publish vào `publish/vht-44-local` thành công trên working tree VHT-44 dựa trên base ở trên; chưa phải artifact từ SHA phát hành sạch. Build có 1 cảnh báo analyzer xUnit2031 có sẵn trong test P7, 0 error. Unit storage đạt 87/87, integration security/storage dùng LocalDB thử đạt 9/9, không skip. Artifact có `web.config` InProcess/AspNetCoreModuleV2 và đủ file chạy. Log được ghi vào thư mục cấu hình thử; metadata key mới xác nhận mã hóa DPAPI theo user Windows local. Không sửa code trong lượt test.

Kết quả này chưa kiểm chứng IIS/HTTPS/WebSocket/reconnect, ACL app pool, migration server hoặc cookie/tệp qua lần deploy. Hai đợt bên dưới vẫn chờ môi trường thực tế.

| Thông tin | 2B — Bản nền | 2E — Tích hợp tuần 2 |
| --- | --- | --- |
| Trạng thái | Chờ môi trường, chốt nguồn và kiểm thử | Chờ nguồn tích hợp và môi trường |
| URL/site/app pool | Chưa xác định | Chưa xác định |
| PR và review/CI URL | Chưa chốt | Chưa chốt |
| SHA nguồn artifact / SHA deploy | Chưa có / chưa deploy | Chưa có / chưa deploy |
| Artifact/vị trí lưu/checksum nếu có | Chưa tạo | Chưa tạo |
| Migration/schema trước → sau | Chờ Tính xác nhận | Chờ Tính xác nhận |
| Người thực hiện/thời gian | Chưa thực hiện | Chưa thực hiện |
| HTTPS/health/login/SQL | Chưa chạy | Chưa chạy |
| Tệp đúng quyền/WebSocket/reconnect | Chưa chạy | Chưa chạy |
| Publish lại giữ keys/tệp/cookie | Chưa chạy | Chưa chạy |
| SHA rollback/tương thích schema | Chưa xác định | Chưa xác định |
| Lỗi triển khai | Chưa có kết quả thực thi | Chưa có kết quả thực thi |

## Đầu vào đang chờ

| Đầu vào | Phụ trách theo task | Cần trước |
| --- | --- | --- |
| Server/cách truy cập, HTTPS, site/pool/đường dẫn/ACL | Phú, phối hợp Tính | Cấu hình và publish thật |
| SQL thử, migration, secrets và keys server | Tính | Khởi động bản cần SQL/keys |
| Chốt PR, approval độc lập, CI và SHA tích hợp | Cả nhóm; Phú ghép | Tạo artifact phát hành |
| Tài khoản/dữ liệu thử, luồng thật sẵn sàng, smoke | Phát phối hợp các tác giả | Smoke IIS |

## Ghi lỗi sau mỗi lần thử

Mỗi lỗi ghi: mã nội bộ, đợt/SHA/thời gian, bước tái hiện, kỳ vọng/thực tế, bằng chứng đã lọc nhạy cảm, mức ảnh hưởng, người xử lý theo phân công, trạng thái và kết quả thử lại. Nếu chỉ thiếu đầu vào, ghi vào bảng đang chờ; không gán thành lỗi ứng dụng chưa tái hiện.
