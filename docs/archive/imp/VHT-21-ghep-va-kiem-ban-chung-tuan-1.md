# VHT-21 — Ghép và kiểm bản chung tuần 1

## Mốc bàn giao

- Rà ngày 19/10/2026, giờ Việt Nam; Sprint 1D, phạm vi chuẩn bị 1C và tích hợp 1D của P1–P3/shared.
- Repo: [utc2-programming-project/LTW-boarding_house_management_web](https://github.com/utc2-programming-project/LTW-boarding_house_management_web).
- Nền tích hợp đang có trên dev: `b8198d1c1d005c75a51d97eef02bbaf4b19bbca3` (merge #22). Đây là commit có trước lượt VHT-21, không phải commit merge do lượt này tạo.
- Nhánh bàn giao: `VHT-21-chot-quy-trinh-nhanh-va-ghep-tuan-1`, tạo từ origin/dev tại SHA trên. WIP local được bảo toàn.
- Phú điều phối; Tính dự phòng điều phối; Phát điều phối test. Reviewer độc lập với tài khoản tác giả PR thực tế.

Kế thừa [bàn giao VHT-117](VHT-117-review-va-thu-tu-merge-tuan-1.md), [kế hoạch tuần](../task/cong_viec_theo_tuan.md), [timeline](../task/timeline_song_song.md) và [ma trận quyền](../task/thao_tac_va_quyen.xlsx). Không tạo lại repo/workflow.

## PR và bằng chứng mới

Đọc API danh sách toàn bộ 23 PR, reviews và check-runs trên head của từng PR. API reviews trả danh sách rỗng cho cả 23 PR ở mốc rà; chưa có approval GitHub được xác minh. Điều này không phủ nhận trao đổi review ngoài GitHub, nhưng chưa đủ bằng chứng cho cổng approval. Check-runs rỗng trên head không loại trừ run trên merge ref; không kết luận CI thất bại từ dữ liệu rỗng.

| PR | Phần việc / tác giả GitHub | Trạng thái | CI quan sát được | Còn thiếu |
| --- | --- | --- | --- | --- |
| [#18](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/18) | VHT-117 / Piu-01 | Đã merge dev | [Head xanh](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/38910902572) | Bằng chứng review độc lập |
| [#20](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/20) | Storage VHT-13 / Piu-01 | Đã merge dev | [Head xanh](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/39019989760) | Bằng chứng review độc lập |
| [#21](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/21) | Tenant service VHT-23 / Piu-01 | Đã merge dev | [Head xanh](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/39020242104) | Bằng chứng review độc lập |
| [#22](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/22) | Service P2 VHT-7 / TinhSsc | Đã merge dev | [PR event xanh](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/39233063695), head `a2e692e5122cb78c5b31ef105b29a9ac5c5ce8f6` | Bằng chứng review độc lập |
| [#23](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/23) | Layout/culture VHT-118 / Piu-01 | **Mở, chờ approval** | [PR event xanh](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/39239315912), head `76a0045fe76e4bb01580f72cfbc8cc37a8ce929d` | Đã request TinhSsc/HinhVinhPhat, chưa có review; chưa merge |

#23 chỉ sửa `NhaTro/Web/Program.cs` và `NhaTro/Web/wwwroot/css/theme.css`; API báo mergeable=true, mergeable_state=clean. Mergeability và CI xanh không thay thế approval. Trước merge phải đọc lại head/base/checks và approval, vì trạng thái trên có thể thay đổi.

Lịch sử trước #18 giữ nguyên: #1/#6/#8 tài liệu, #2 formatter, #4 INIT, #5 schema/ERD, #7 role/scope, #9/#15/#16 layout, #11 DTO P2, #13 DTO storage, #14 DTO khách, #17 component đều đã merge dev. #3/#10 đóng không merge vào main; #12 và #19 đóng không merge vào dev. Không đưa các PR đóng này vào hàng đợi hoặc merge lại PR đã ghép. Chi tiết lịch sử #1–#17 ở VHT-117; review mới rà vẫn chưa có approval. Head #15 có [check xanh](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/38750190638); #14/#17 chưa có check trên head trong phản hồi hiện tại.

## Reviewer và thứ tự ghép

Các reviewer dưới đây là phân công đề xuất kế thừa chuyên môn, chưa xác nhận nhận việc. Riêng #23 đã có request review thực tế; lượt VHT-21 không gửi thêm request hoặc tin nhắn.

| Phần | Chính đề xuất | Dự phòng đề xuất | Quy tắc |
| --- | --- | --- | --- |
| #23 layout/culture, #18 tài liệu/quy trình | Tính (TinhSsc) | Phát (HinhVinhPhat) | Tác giả Piu-01; khác reviewer |
| #22 P2/quyền | Phát | Đào | Tác giả TinhSsc không tự duyệt; tài khoản Đào chưa xác minh |
| #20/#21 storage/P3 | Tính | Đào | Reviewer bảo mật/schema; đối chiếu tác giả thực tế Piu-01 và người viết phần nghiệp vụ |
| UI P1 VHT-8 khi có PR | Phát | Đào | Không dùng Tính duyệt PR của mình |
| UI P2/P3 VHT-16/VHT-17 khi có PR | Phú | Tính | Phúc nhận service/DTO, reviewer phải khác tác giả PR thực tế |
| Migration phát sinh | Tính nếu khác tác giả, nếu trùng thì Phát | Đào | Rà entity/configuration, migration, snapshot và ERD/SQL |

Thứ tự cho phần còn lại:

1. Rà DTO/quyền đã có: #7/#11/#13/#14, mẫu input/output và phạm vi người nhận UI.
2. Schema/migration đi trước service cần schema đó. Phần đã trên dev được giữ, không sắp lại lịch sử.
3. Service P2 #22, storage #20, P3 #21 đã ghép. Rà delta/giới hạn trước bàn giao UI.
4. Shared #23 chờ approval, sau đó kiểm lại CI với dev hiện tại và ghép theo lượt file chung. Shared độc lập không cần chờ UI.
5. UI P1/P2/P3 khi có PR thực tế: nhận đúng service/shared, approval và CI đạt rồi mới merge. Hiện không có PR mở tương ứng trong danh sách 23 PR.
6. Chốt SHA dev sau từng lượt, kiểm phần đã sẵn sàng và ghi lỗi đúng tác giả.

## Nhánh, file chung và migration

- Nhánh task `VHT-<mã>-<mô-tả-không-dấu>` từ dev; PR về dev; sửa tiếp dùng lại nhánh task. Không thao tác trực tiếp main; không commit/push `.agent/`, `AGENTS.md` hay secret.
- `Program.cs`: giữ đăng ký quyền, P2/P3/storage, rồi ghép delta middleware/culture của #23. Không thay toàn file. Sau mỗi PR, tác giả PR kế tiếp cập nhật nền dev.
- Resource VI/EN ghép theo từng key; layout/theme/component rồi đến UI chức năng. Không mất key hoặc selector/theme đã có.
- DbContext/configuration → migration → snapshot trong cùng lượt. Mỗi lượt một tác giả; không chọn nguyên ours/theirs cho snapshot. Xung đột trả tác giả xử lý.
- Migration trên nền hiện có: `00000000000000_CreateIdentitySchema`, `20261008142750_CoreSchema`, `20261016142707_AddStorageStoredFiles`, `20261017005536_ExtendTenantProfile`. Không tạo/sửa migration trong VHT-21.
- PR migration mới phải nêu tác động dữ liệu, thứ tự, script idempotent, kết quả DB sạch và nâng cấp DB có mẫu; đối chiếu [nguồn schema](../design/database/). Không sửa migration đã merge hoặc reset/drop DB dùng chung. CI DB trống không chứng minh nâng cấp dữ liệu cũ an toàn.

## CI và trạng thái bản tích hợp

Workflow hiện có trigger push/PR cho dev; Windows, .NET 10, restore tool/solution, build, kiểm pending model changes, áp migration DB trống rồi test solution. Không thay workflow trong lượt này.

[Run dev 39233077195](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/39233077195) có event `push`, conclusion `success`, head **`b8198d1c1d005c75a51d97eef02bbaf4b19bbca3`**. Đây là bằng chứng CI cho nền dev tích hợp hiện có. Chưa đọc log/TRX để xác nhận số test chạy/skip; không suy từ run xanh thành E2E hay nghiệm thu quyền đạt.

Rà source trên SHA này:

- Có service P2/P3/storage và unit/integration test tương ứng.
- Chưa có route trang nghiệp vụ phòng/khách trong source; thư mục Pages hiện chỉ có home, error/not-found và components demo, bên cạnh trang Identity. VHT-16/VHT-17 cần bàn giao UI thật trước khi nghiệm thu qua UI.
- `NhaTro/NhaTro.E2ETests/UnitTest1.cs` chỉ có Fact rỗng; E2E xanh không chứng minh tương tác trình duyệt P1–P3.
- P2 vẫn có `RoomChangePolicyStub` và `NoActiveContractRoomUsageChecker` chờ task liên quan; test dùng policy/checker thay thế không chứng minh công tắc/hợp đồng thật đã ghép.
- Fixture SQL hiện dùng EnsureCreated cho test service và có thể skip khi thiếu LocalDB; migration thật phải kiểm riêng.

Nguồn quyền đã đọc: sheet `Ma trận quyền`, hàng 2–22 (P1–P3), chú thích hàng 48–50; sheet `Công tắc Quản lý` SW-01…SW-06 và sheet `Quyết định`. Admin không có nghiệp vụ P2/P3, Quản lý theo tòa được giao, khách chỉ bản thân/phòng thuê, CCCD Quản lý phải che. Không triển khai thêm công tắc/thùng rác/hàng đợi thuộc đợt khác trong VHT-21; ghi giới hạn, không đánh dấu các phần chưa có là đạt.

## Kết quả thực hiện VHT-21 và phần chờ

- Đã rà PR/review/CI, lập bảng người review và thứ tự ghép; không có PR đủ điều kiện để merge mới trong lượt này do #23 thiếu approval.
- Sau khi người dùng duyệt test ngày 19/10/2026, đã chạy kiểm trên source SHA `b8198d1c1d005c75a51d97eef02bbaf4b19bbca3`, SDK 10.0.401, SQL Server LocalDB thật và DB riêng.
- Restore/tool restore và build đạt, 0 warning/error. Unit **191 đạt**, SQL integration **30 đạt**, scaffold E2E **1 đạt**; tổng 222, không fail/skip. Scaffold E2E vẫn chỉ là Fact rỗng, không tính là nghiệm thu UI.
- Không pending model changes; sinh script idempotent thành công; áp migration lên DB riêng đạt, áp lần hai đạt. Đọc `__EFMigrationsHistory` xác nhận đủ 4 migration nêu trên. Chưa thử nâng cấp DB có dữ liệu cũ.
- Smoke Development tại localhost đạt: trang chủ/login/component demo tải được; trang cá nhân chưa đăng nhập chuyển login; selector EN và theme tối giữ sau reload, mẫu VND không đổi. Màn hình kiểm khoảng 692 px rộng; chưa kiểm đủ breakpoint mobile/desktop hoặc đăng nhập 4 vai trò.
- Lần thử Production bằng `dotnet run --no-build` từ output build bị lỗi static web assets (log báo chưa chạy published output, thiếu MudBlazor/Blazor JS). Chạy lại Development dùng cùng source/DB đã tải assets và tương tác được. Đây là giới hạn cách khởi chạy kiểm, chưa phải kết quả publish/IIS; không sửa code hoặc thay cấu hình dự án trong test.
- Không có fix nào được áp trong lượt test. Các mục dưới là thiếu đầu vào/giới hạn bàn giao; chưa có lỗi nghiệp vụ P1–P3 mới được tái hiện.

| Phần chờ | Người phụ trách theo phân công | Cách đóng |
| --- | --- | --- |
| Approval #23 | Tính/Phát theo request hiện có | Review trên đúng head; nếu đạt thì merge sau khi kiểm lại CI/base |
| Bằng chứng review PR đã merge | Phú điều phối, reviewer độc lập phần tương ứng | Ghi nhận rà hiện tại, không gán ngược approval lịch sử |
| UI P1 nghiệp vụ, UI P2/P3 SQL thật | Tính P1; Phúc UI P2/P3; phối hợp tác giả service | Bàn giao PR/test, review/CI rồi ghép; không suy từ UI mẫu |
| Kiểm đầy đủ bản chung P1–P3 | Phát điều phối, Phúc/Phú UI, Tính/Phát service | Kiểm tự động/smoke nền đã đạt trong phạm vi trên; còn UI nghiệp vụ và 4 vai trò cần đầu vào rồi chạy tiếp trên SHA chốt |

Lỗi khi kiểm: ghi PR/task, SHA, môi trường, bước tái hiện, kỳ vọng/thực tế và bằng chứng. Tính nhận P1/P2/quyền; Phát P3/storage; Phúc UI P2/P3/component; Phú layout/localization; Đào formatter; đối chiếu tác giả thực tế trước giao. Không gửi thông báo hoặc sửa thay tác giả trong lượt này.

**Kết luận mốc rà:** đã có bản nền dev và CI xanh; chưa đủ approval để ghép #23, chưa có bằng chứng chạy đầy đủ P1–P3. VHT-21 chưa đạt tiêu chí hoàn thành 1D.
