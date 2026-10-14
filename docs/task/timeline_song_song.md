# Timeline chạy song song / Parallel timeline

**Web vận hành nhà trọ / Boarding House Operations Web**

**Đọc ngang:** các ô **cùng hàng** là việc 5 người có thể làm đồng thời. Đọc dọc để biết nhịp tiếp theo của từng người. Chi tiết từng mã task giữ tại [kế hoạch tuần](cong_viec_theo_tuan.md).

- Mỗi hàng là **một nhịp làm việc**, không phải đúng một ngày. Trong một ô có nhiều việc thì người đó tự làm lần lượt; không coi một người chạy nhiều task cùng lúc.
- Nguồn sprint chính thức là Jira (D11): sprint = nhịp bàn giao đầu ra chính; nhịp sau trong bảng này là kiểm lại/ghép.
- Không cần đợi cả hàng xong mới chuyển nhịp: chỉ đợi đầu ra mình thực sự cần ở cột cuối. Việc không phụ thuộc người đang chậm vẫn tiếp tục.
- Một mã có thể xuất hiện ở nhịp **chuẩn bị/mẫu** rồi **ghép thật**; vẫn là cùng task, không tạo nhiệm vụ mới. UI mẫu không được tính là đã hoàn thành nghiệp vụ.
- `[Backend]`, `[UI]`, `[Database]`, `[Testing]`, `[GitHub]`, `[Deploy]`, `[Docs]` ở đầu ô chỉ là gợi ý nhóm việc trong timeline, giữ cùng nghĩa với nhãn Jira trên task tương ứng. File chung/migrations ghép theo lượt, không sửa đồng thời cùng file/DB.

## Tuần 1 / Week 1

| Nhịp / Stage | Tính | Đào | Phát | Phúc | Phú | Đầu ra phải chờ / Dependencies |
| --- | --- | --- | --- | --- | --- | --- |
| 1A — Bắt đầu độc lập | **[Backend/Database]** VHT-4 chốt quyền; VHT-5 ERD lõi | **[Backend]** VHT-10 formatter/clock | **[Testing]** VHT-14 dựng fixture/DB test | **[Docs]** VHT-18 README bản đầu/dàn ý Word | **[UI]** VHT-19 theme/layout | Dùng repo/INIT đã có. Phúc tự viết và chạy thử setup, không chờ Tính xong VHT-4/VHT-5; Tính/Phát/Phú chỉ rà đúng phần mình sau khi nhận bản nháp. |
| 1B — Chốt đầu vào | **[Backend]** VHT-6 quyền; VHT-7 chốt DTO phòng | **[Backend/Database]** VHT-9 DTO/quy tắc P4/P5 | **[Backend]** VHT-12 contract storage; VHT-23 chốt DTO khách | **[UI]** VHT-15 component theo design doc | **[Backend/UI]** VHT-20 resource/selector | Quyền theo VHT-4; chốt DTO giữa người làm backend và UI. Chưa cần service hoàn chỉnh. |
| 1C — Backend // UI mẫu | **[Backend]** VHT-7 service phòng; **[Backend]** VHT-94 chuẩn bị DTO/policy công tắc | **[UI]** VHT-11 form hợp đồng mẫu | **[Backend]** VHT-13 storage; VHT-23 service khách | **[UI]** VHT-16/VHT-17 UI phòng/khách mẫu | **[GitHub]** VHT-21 chuẩn bị review/merge | UI dùng DTO đã chốt ở 1B; service kiểm quyền dùng VHT-6. Không chờ backend để dựng form. |
| 1D — Ghép và kiểm từng phần | **[UI]** VHT-8 UI P1/Identity; **[Backend/Database]** VHT-94 nền công tắc | **[UI]** VHT-11 ghép component/style | **[Testing/Docs]** VHT-14 chạy auth/UI; VHT-22 test P1–P3 | **[UI]** VHT-16/VHT-17 ghép SQL | **[GitHub/UI]** VHT-21 merge; VHT-19/VHT-20 sửa thiếu | UI phòng nhận VHT-7, khách nhận VHT-23; style/resource nhận VHT-19/VHT-20. Test theo phần đã ghép, không đợi tất cả. |

**Cùng hàng 1C:** Tính viết backend phòng, Phát viết backend khách, Phúc dựng UI theo DTO; Đào dựng form hợp đồng và Phú chuẩn bị PR vẫn làm cùng lúc.

## Tuần 2 / Week 2

| Nhịp / Stage | Tính | Đào | Phát | Phúc | Phú | Đầu ra phải chờ / Dependencies |
| --- | --- | --- | --- | --- | --- | --- |
| 2A — Phát triển độc lập | **[Backend]** VHT-24 phạm vi QL; VHT-27 bảo mật | **[Backend/Database]** VHT-29 tạo hợp đồng | **[Backend]** VHT-26 giấy tờ; VHT-34 rule hóa đơn | **[UI]** VHT-37/VHT-38 form P5 mẫu | **[UI]** VHT-39/VHT-40 sự cố/report mẫu | Nền tuần 1 đã bàn giao; DTO P5/P7 được chốt trước khi dựng UI. Rule hóa đơn dùng mẫu, chưa phát hành. |
| 2B — Duyệt và đơn giá | **[Database/Backend]** VHT-28 review migration đã gửi; VHT-41 query phòng; **[Backend/UI]** VHT-95 vòng đời tài khoản | **[Backend]** VHT-30 duyệt hợp đồng; VHT-32 đơn giá; **[Backend/Database]** VHT-100 biểu giá điện/nước | **[Backend]** VHT-25 gắn user/hồ sơ, chuẩn bị liên kết thuê | **[UI]** VHT-36 ghép giấy tờ | **[Deploy]** VHT-44 chuẩn bị IIS/config | Duyệt cần VHT-29; upload thật cần VHT-26. Phát chưa ghép liên kết thuê đến khi VHT-30 chạy được. |
| 2C — Ghép từng nguồn | **[Backend/Database]** VHT-24/VHT-27 kiểm lại quyền/lỗi; VHT-28 review theo PR; **[Backend/Database]** VHT-96 hàng đợi duyệt | **[UI/Backend]** VHT-31 UI hợp đồng; VHT-33 backend sự cố; VHT-100 hoàn tất test bậc | **[Backend/Database/UI]** VHT-25 ghép liên kết thuê; VHT-35 khung hóa đơn/list | **[UI]** VHT-37 ghép đơn giá; VHT-38 hoàn thiện form kỳ | **[UI]** VHT-40 ghép nguồn phòng | UI hợp đồng cần VHT-29/VHT-30; P5 đơn giá cần VHT-32; report phòng cần VHT-41. Chưa phát hành hóa đơn từ mẫu. |
| 2D — Sự cố // khách // rà UI | **[Database]** VHT-28 cập nhật ERD phần đã merge | **[Backend/UI]** Kiểm/sửa P4/P5/P7 trong VHT-31/VHT-32/VHT-33 | **[Backend/Testing]** VHT-46 query khách; VHT-42 test luồng đã ghép | **[UI]** VHT-36 ghép phòng thuê; VHT-43 rà VI/EN/form | **[UI]** VHT-39 ghép sự cố | Phòng thuê cần VHT-25 + query phòng; sự cố cần VHT-33 + storage. Lỗi trả tác giả, không chờ test cả hệ thống. |
| 2E — Tích hợp và tài liệu | **[Database]** VHT-28 kiểm migrations/ERD chung; **[Backend/Database/UI]** VHT-98 thùng rác phòng | **[Backend/UI]** Sửa lỗi phần đã giao | **[Testing]** VHT-42 test bản ghép; **[Backend/Database/UI]** VHT-99 thùng rác khách | **[Docs]** VHT-45 Word/ảnh; **[Testing]** VHT-101 test ma trận quyền; VHT-98/VHT-99 UI thùng rác | **[GitHub/Deploy/UI]** VHT-44 publish thử; VHT-40 ghép nguồn khách; **[UI]** VHT-97 UI công tắc/hàng đợi duyệt | Nhận VHT-46 để ghép report khách; deploy từ PR đã review/CI, ảnh dùng bản thật. |

**Không phải chờ hợp đồng để làm đơn giá/UI P5, giấy tờ hoặc khung sự cố.** Chỉ phần liên kết thuê và giao dịch thực sự cần hợp đồng đã có.

## Tuần 3 / Week 3

| Nhịp / Stage | Tính | Đào | Phát | Phúc | Phú | Đầu ra phải chờ / Dependencies |
| --- | --- | --- | --- | --- | --- | --- |
| 3A — Chuẩn bị cùng lúc | **[Testing/Database]** VHT-47 scope; VHT-49 dữ liệu demo nền | **[Backend]** VHT-51 chi phí kỳ thật | **[Backend/UI/Testing]** Chuẩn bị VHT-53/VHT-54/VHT-55/VHT-56 theo DTO; case VHT-63 | **[UI/Testing]** VHT-59 rà phòng/khách/P5 | **[UI]** VHT-60 hoàn thiện P7; VHT-62 cảnh báo/reconnect | Dùng nền tuần 2. Phát chuẩn bị P6 bằng mẫu/DTO; chưa phát hành hoặc xác nhận tiền thật. |
| 3B — Chi phí → hóa đơn | **[Database]** VHT-48 review schema tài chính | **[Backend/UI]** VHT-50 gia hạn/thanh lý/cọc; **[Backend/Database]** VHT-102 sửa chỉ số + lịch sử | **[Backend]** VHT-53 lập/phát hành hóa đơn thật | **[UI]** VHT-58 ghép chi phí thật | **[UI]** VHT-61 ghép nguồn phòng/khách có sẵn | Phát và Phúc nhận VHT-51 đã chạy; hóa đơn cần nguồn hợp lệ, migrations tài chính cần review trước ghép. |
| 3C — Thu tiền // query nguồn | **[Database]** VHT-49 bổ sung demo; VHT-48 ERD theo migration đã merge | **[Backend]** VHT-52 query hợp đồng/chi phí/sự cố | **[Backend/UI]** VHT-54 xác nhận; VHT-55/VHT-56 ghép UI P6 | **[UI/Docs]** VHT-59 sửa UI; VHT-66 hướng dẫn/ảnh phần sẵn | **[UI]** VHT-62 ghép draft/tra kết quả; VHT-61 ghép nguồn đã bàn giao | Xác nhận cần hóa đơn thật VHT-53. Phú chỉ ghép draft/tra kết quả khi Đào/Phát bàn giao service; UI tài chính thật nhận VHT-54. |
| 3D — Report tiền // test | **[Testing/Database]** VHT-47 kiểm scope chung; VHT-48 chốt ERD | **[Backend/UI]** Sửa lỗi VHT-50/VHT-51/VHT-52 | **[Backend/Testing]** VHT-57 query tiền; VHT-63 test tiền/transaction | **[Docs]** VHT-66 cập nhật Word/README | **[UI]** VHT-61 ghép nguồn Đào; VHT-62 kiểm reconnect; **[Backend/UI]** VHT-104 nhật ký thao tác (nếu D16) | Query tiền dùng thu đã xác nhận từ VHT-54; Phú ghép phần tiền sau khi nhận VHT-57, không lấy YC thanh toán làm doanh thu. |
| 3E — Kiểm bản tích hợp | **[Database]** Hỗ trợ kiểm DB/demo ở VHT-48/VHT-49 | **[Backend/UI]** Sửa phần mình theo kết quả test; **[Backend/UI]** VHT-103 xuất Excel | **[Testing]** VHT-64 E2E; VHT-63 regression | **[UI/Docs]** VHT-59 sửa UI; VHT-66 ảnh bản thật | **[GitHub/Deploy/UI]** VHT-65 deploy; VHT-61 ghép tiền | Nhận VHT-57 cho report tiền; E2E theo commit/bản deploy được chọn. Nếu deploy chưa có, chuẩn bị script/test localhost trước. |

**Chuỗi tiền bắt buộc:** Đào `VHT-51` → Phát `VHT-53 → VHT-54 → VHT-57` → Phú `VHT-61` phần tài chính. Trong các khoảng chờ này, 4 người còn lại vẫn làm những ô độc lập cùng hàng. Nhánh phụ: Đào VHT-51 → VHT-102 (sửa chỉ số có lịch sử) → Phúc VHT-58 (UI).

## Tuần 4 / Week 4

| Nhịp / Stage | Tính | Đào | Phát | Phúc | Phú | Đầu ra phải chờ / Dependencies |
| --- | --- | --- | --- | --- | --- | --- |
| 4A — Chuẩn bị kiểm bản cuối | **[Testing/Deploy]** VHT-75 rà secrets/log; chuẩn bị VHT-74 restore riêng | **[Backend/UI]** VHT-70 sửa lỗi đã biết | **[Testing]** Chuẩn bị case VHT-68; VHT-71 sửa lỗi đã biết | **[UI/Docs]** VHT-72 rà form; VHT-80 chuẩn bị nội dung | **[GitHub/Testing]** VHT-67 chọn release candidate | Regression chung bắt đầu sau khi Phú thông báo commit/tag; chuẩn bị/test riêng không cần đợi. |
| 4B — Test // sửa // phục hồi | **[Backend/Database/Deploy]** VHT-69 sửa P1/P2; VHT-74 thử restore | **[Backend/UI]** VHT-70 sửa/regression P4/P5/P7 | **[Testing/Backend]** VHT-68 regression; VHT-71 sửa P3/P6 | **[Testing/Docs]** VHT-76 clone sạch; VHT-72 rà UI | **[UI/Deploy]** VHT-73 rà layout; chuẩn bị VHT-77 | Test ghi commit; lỗi trả ngay từng người. Phát tự xếp test/sửa, chia case UI cho Phúc/Phú. Restore không dùng DB live/test chung. |
| 4C — Ghép sửa và đồng bộ | **[Database/Docs]** VHT-79 ERD/cây folder cuối | **[Backend/UI]** Kiểm PR sửa của mình | **[Testing]** VHT-68/VHT-71 test lại PR sửa | **[Docs]** VHT-80 ghép Word/README | **[Deploy/Docs]** VHT-77 deploy/smoke; VHT-81 slide/demo | ERD theo migrations đã merge; PR phải review/CI. Phát smoke trên deploy khi sẵn, tài liệu vẫn chuẩn bị trước. |
| 4D — Nghiệm thu và nộp | **[Database/Docs]** Xác nhận DB/ERD VHT-74/VHT-79; tự demo P1/P2 | **[Backend/UI]** Tự demo và giải thích P4/P5/P7 | **[Testing]** VHT-78 nghiệm thu trên bản deploy | **[Docs/Testing]** Chốt VHT-80; chuẩn bị VHT-82 gói nộp | **[Docs]** Chốt VHT-81 theo bản thật | Nghiệm thu cần deploy/smoke VHT-77; chốt tài liệu nhận kết quả VHT-78. **Demo cả nhóm/đóng gói VHT-82 làm sau khi đã đạt**, không trước nghiệm thu. |

**Không chờ Phát test hết mới sửa:** Phát phát hiện lỗi nào thì người phụ trách nhận lỗi đó ngay; mọi người sửa trên nhánh riêng và báo lại để merge/test đúng phiên bản.

> Đây là bản nhìn ngang để phối hợp; người phụ trách, nội dung thực hiện và nghiệm thu chính thức vẫn theo [90 task trong kế hoạch tuần](cong_viec_theo_tuan.md).
