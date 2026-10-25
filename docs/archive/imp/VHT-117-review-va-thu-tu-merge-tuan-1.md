# VHT-117 — Review và thứ tự merge tuần 1

## Phạm vi và mốc rà soát

VHT-117 thực hiện **đợt 1C: chuẩn bị review/merge** theo ảnh Jira người giao việc cung cấp. VHT-21 giữ **đợt 1D: merge và kiểm bản tích hợp P1–P3**. Tài liệu này là bàn giao cho 1D, không xác nhận bản tích hợp đã chạy đạt.

- Ngày rà soát: **16/10/2026**, múi giờ Việt Nam.
- Repository thực tế theo `origin`: [utc2-programming-project/LTW-boarding_house_management_web](https://github.com/utc2-programming-project/LTW-boarding_house_management_web).
- Nền `dev` đã fetch: `4055762c952fc38b117f55de79fe7eaebc285ede` (merge PR #17).
- Phú điều phối GitHub; Tính dự phòng; Phát điều phối test. Tác giả chịu trách nhiệm sửa lỗi phần mình.
- Danh sách GitHub API tại thời điểm rà có 17 PR, không có PR mở. Lịch sử merge được giữ nguyên, không merge lại hoặc viết lại lịch sử để áp thứ tự đề xuất.

Nguồn: [kế hoạch tuần](../task/cong_viec_theo_tuan.md), [timeline](../task/timeline_song_song.md), [ma trận quyền](../task/thao_tac_va_quyen.xlsx), [cấu trúc code](../architecture/cau_truc_thu_muc.md), [quy ước migration](../../README.md), `.github/CODEOWNERS` và API GitHub của repo trên.

Các tài liệu cũ còn ghi URL repo TinhSsc và PR vào `main`. Với task này, dùng remote thực tế và quy tắc hiện hành của repo: **feature từ `dev`, PR về `dev`**. Không suy diễn trạng thái Jira từ tên PR hoặc mã task cũ chưa được cập nhật trong timeline.

## Quy tắc nhánh và review

1. Nhánh task mang tên `VHT-<mã>-<mô-tả-không-dấu>`, tạo từ `dev` mới nhất. Sửa tiếp task dùng lại nhánh task; không thêm tiền tố `codex/`.
2. Trước khi chuyển nhánh, kiểm WIP và stash/commit phù hợp; bảo toàn file local. Không checkout/pull/commit/push trực tiếp trên `main`.
3. PR ghi mã Jira, phạm vi thay đổi, phụ thuộc, cách chạy, kết quả test và giới hạn. DTO có mẫu input/output; migration nêu tác động dữ liệu và thứ tự áp dụng.
4. Reviewer phải khác **tài khoản tác giả PR thực tế**. Phân công công việc trong kế hoạch không chứng minh ai là tác giả code hoặc người đã review.
5. Bảng reviewer dưới đây là **phân công đề xuất để nhóm chốt**, chưa gửi request review, chưa xác nhận thành viên nhận việc. Người phụ trách GitHub thực hiện gán reviewer khi nhóm thống nhất.
6. CODEOWNERS hiện có một dòng `* @HinhVinhPhat @Piu-01 @TinhSsc`. File này không chứng minh PR đã được duyệt hoặc branch protection đang bắt buộc approval. Không thay CODEOWNERS hoặc quyền repo trong VHT-117.
7. Không commit/push `.agent/`, `AGENTS.md` hoặc secret. Trước commit xem staged diff; chỉ stage đường dẫn thuộc task.

## PR tuần 1 đã quan sát

**Cách đọc:** “Đã merge” là trạng thái GitHub, không đồng nghĩa review/CI đạt. “Chưa xác nhận approval” nghĩa là chưa có bằng chứng approval hợp lệ để bàn giao; không khẳng định không ai từng review. Với PR đã merge, reviewer đề xuất dùng cho rà lại hoặc PR sửa tiếp, không gán ngược approval lịch sử.

| PR | Phần việc theo PR | Tác giả GitHub | Nhánh đích / trạng thái | Review được xác minh | CI trên head đã quan sát |
| --- | --- | --- | --- | --- | --- |
| [#1](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/1) | VHT-18 README | TinhSsc | dev / đã merge | Chưa xác nhận approval | Chưa kiểm tra chi tiết |
| [#2](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/2) | VHT-10 formatter/clock | TinhSsc | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#3](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/3) | INIT | TinhSsc | main / đóng, không merge | Chưa kiểm tra chi tiết | Chưa kiểm tra chi tiết; không dùng làm PR tích hợp |
| [#4](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/4) | INIT/khung dùng chung | TinhSsc | dev / đã merge | Chưa xác nhận approval | Có `build-test: success` trên head; xem giới hạn dưới bảng |
| [#5](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/5) | VHT-5 ERD/SQL đối chiếu | TinhSsc | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#6](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/6) | VHT-18 báo cáo | dinhhoangphuc123 | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#7](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/7) | VHT-6 role/scope | TinhSsc | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#8](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/8) | Quy ước Jira/kế hoạch | TinhSsc | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#9](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/9) | VHT-19 layout | Piu-01 | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#10](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/10) | VHT-112 DTO P2 | TinhSsc | main / đóng, không merge | Chưa xác nhận approval | Cùng head #11 có check xanh; không đưa PR này vào hàng merge |
| [#11](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/11) | VHT-112 DTO P2 | TinhSsc | dev / đã merge | Chưa xác nhận approval | Có `build-test: success` trên head; chưa chứng minh kiểm bản ghép dev |
| [#12](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/12) | VHT-20 localization | Piu-01 | dev / đóng, không merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#13](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/13) | VHT-12 DTO storage | TinhSsc | dev / đã merge | Chưa xác nhận approval | API không trả check/status trên head |
| [#14](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/14) | VHT-23 DTO khách | TinhSsc | dev / đã merge | Chưa xác nhận approval | Chưa xác minh đủ do API giới hạn lượt đọc |
| [#15](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/15) | VHT-19 cập nhật layout/style | Piu-01 | dev / đã merge | Chưa xác minh do API giới hạn lượt đọc | Chưa xác minh do API giới hạn lượt đọc |
| [#16](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/16) | VHT-19 CSRF/menu | Piu-01 | dev / đã merge | Chưa xác minh do API giới hạn lượt đọc | Chưa xác minh do API giới hạn lượt đọc |
| [#17](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/pull/17) | VHT-15 component chung | dinhhoangphuc123 | dev / đã merge | Chưa xác minh do API giới hạn lượt đọc | Chưa xác minh do API giới hạn lượt đọc |

Bằng chứng CI cụ thể:

- #4: head `b5ca924efcbb08e11f9105d4b25c447538337436`, [job thành công](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/38369874936/job/115203916648). Cùng head từng xuất hiện ở PR đích main; chưa xác minh run kiểm đúng bản ghép dev.
- #10/#11: head `17d1bca179ae6e117e1f3d07b74080c3fadb498a`, [job thành công](https://github.com/utc2-programming-project/LTW-boarding_house_management_web/actions/runs/38730796925/job/116407777726). Check gắn commit dùng chung hai PR; không dùng check này để xác nhận riêng PR #11 đã được kiểm trên merge ref dev.
- API public bị rate limit trong lúc đọc chi tiết #14 trở đi. Không dùng dữ liệu rỗng sau lỗi API làm bằng chứng “không có check/review”.
- API không trả check/status trên head cũng chưa loại trừ check trên merge ref. Cần xem tab Checks/Actions và SHA/event/base của run để chốt.

## Reviewer chính và dự phòng đề xuất

Phân công dựa trên kế hoạch tuần; **tất cả dòng đang chờ nhóm chốt**. Tài khoản thấy trong PR/CODEOWNERS: Tính `TinhSsc`, Phú `Piu-01`, Phúc `dinhhoangphuc123`; `HinhVinhPhat` đang có trong CODEOWNERS nhưng chưa xác minh người này nhận review. Chưa xác minh tài khoản GitHub của Đào.

| Phần / PR tham chiếu | Reviewer chính đề xuất | Dự phòng đề xuất | Lưu ý |
| --- | --- | --- | --- |
| Quyền/scope VHT-6, DTO P2 #7/#11 | Phát | Đào | Không dùng Tính review PR do TinhSsc tạo; cần kiểm cách gọi quyền và scope |
| ERD/schema #5 và migration phát sinh | Tính khi không là tác giả; nếu tác giả Tính thì Phát | Đào | Người review đối chiếu nguồn schema, tác giả tự sửa migration |
| Formatter/clock #2 | Phát | Phúc | PR thực tế do TinhSsc tạo, không tự review |
| Storage #13, DTO khách #14 | Phát nếu không là tác giả thực tế; nếu trùng tác giả thì Đào | Phúc cho hợp đồng DTO/UI | PR hiện do TinhSsc tạo; schema/bảo mật cần reviewer có chuyên môn, không dùng UI review thay |
| Theme/localization #9/#12/#15/#16, PR VHT-20 tiếp theo | Phúc | Tính | #12 đã đóng không merge; kiểm nội dung đang có trên dev trước khi mở PR tiếp |
| Component/UI P2/P3 #17 và PR tiếp theo | Phú | Tính | Phát phối hợp kiểm DTO P3; Tính kiểm quyền/P2 nếu không là tác giả |
| README/báo cáo #1/#6/#8 | Phú | Phát | Review setup hoặc tài liệu theo diff thực tế |
| PR VHT-117 CI và hướng dẫn merge | Tính | Phát | Nếu tài khoản tạo PR trùng reviewer, chọn người khác trước khi merge |

Tính là dự phòng điều phối chung khi Phú vắng, không thay reviewer độc lập cho PR của mình. Nếu cả chính/dự phòng không sẵn sàng hoặc không đủ chuyên môn, giữ PR chờ review và ghi blocker; không tự bỏ review.

## Thứ tự ghép cho PR còn lại hoặc PR sửa tiếp

Đây là **thứ tự phụ thuộc**, không phải lịch sử merge và không yêu cầu chờ tất cả PR của một hàng nếu hai PR độc lập. Tại mốc rà, chưa có PR mở để xếp hàng merge trực tiếp.

| Lượt | Đầu vào / phần việc | Phụ thuộc và điều kiện chuyển lượt | Người điều phối |
| --- | --- | --- | --- |
| 0 | VHT-117: CI và quy tắc nhánh | Review độc lập, kiểm workflow với dev trước khi dùng CI làm cổng cho PR tiếp | Phú; Tính dự phòng |
| 1 | Quyền VHT-4/VHT-6; DTO P2 VHT-112/VHT-7, DTO P3 VHT-23, storage VHT-12 | Chốt hợp đồng input/output và scope; phần đã trên dev chỉ rà delta, không merge lại | Tác giả backend; người nhận UI tham gia DTO |
| 2 | Schema/migration phục vụ service | DTO chốt; review schema; từng migration áp trên nền dev mới nhất. PR gộp DTO/schema/service thì review nội bộ theo thứ tự này, không bắt tách PR máy móc | Tác giả; reviewer schema độc lập |
| 3 | Service phòng VHT-7, service khách VHT-23, storage thật VHT-13 | Quyền, DTO và schema tương ứng đã sẵn sàng; service có test phần mình | Tính P2; Phát P3/storage theo kế hoạch |
| Song song | Formatter VHT-10, layout VHT-19, localization VHT-20, component VHT-15 | Ghép theo phụ thuộc thực tế; component/layout chốt style và resource trước UI nghiệp vụ | Đào/Phú/Phúc theo phần việc |
| 4 | UI P1 VHT-8; UI P2/P3 VHT-16/VHT-17 | Nhận service và phần shared cần dùng; UI mẫu không được báo đã nối SQL thật | Tính P1; Phúc P2/P3 |
| 5 — bàn giao 1D | VHT-21 và VHT-14/VHT-22 | Chạy bản ghép theo từng phần đã sẵn sàng, ghi commit và kết quả; giao lỗi về tác giả | Phú điều phối merge; Phát test |

VHT-11 là form P4 mẫu trong lịch tuần 1; chỉ phối hợp DTO/component/style có liên quan, không đưa nghiệp vụ hợp đồng P4 vào tiêu chí nghiệm thu P1–P3 của VHT-117.

Chưa thấy PR mở cho service P2/P3/storage thật và UI P1/P2/P3 tại thời điểm rà. Tên #14 có “service P3” nhưng trên nền dev quan sát mới có DTO/entity/scope; chưa thấy thư mục service P3. Không coi tên PR là bằng chứng service hoàn chỉnh. VHT-20 #12 đóng không merge cũng không chứng minh dev thiếu toàn bộ localization: cần rà phần đã được đưa vào qua PR layout.

## File chung và migration

Phú giữ thứ tự ghép, reviewer kiểm chuyên môn, tác giả chịu trách nhiệm giải quyết xung đột trên nhánh task. Sau mỗi PR chạm file chung, PR tiếp cập nhật từ dev, giữ phần đã merge và chạy lại kiểm tra liên quan. Không tự sửa nhánh của tác giả khác.

| File/khu vực | Thứ tự và trách nhiệm |
| --- | --- |
| `NhaTro/Web/Program.cs` | Quyền/DI nền → đăng ký service tương ứng → localization/middleware. #2/#7/#12 đã từng chạm file; đối chiếu dev hiện tại trước khi thêm. Không ghi đè cả file |
| `NhaTro/Web/Data/ApplicationDbContext.cs`, `Data/Configurations/`, `Data/Migrations/` | Entity/config → migration → snapshot đồng bộ trong cùng PR; mỗi lượt một tác giả. Tính review khi không là tác giả; nếu trùng tác giả, Phát/Đào review độc lập |
| `Features/*/DTOs/`, `Common/Models/` | Chốt DTO trước service/UI; #11/#13 là đầu vào đã có. Thay đổi breaking phải chỉ rõ người nhận bị ảnh hưởng |
| `Components/Layout/`, `Components/App.razor`, `_Imports.razor`, `wwwroot/css/` | Theme/layout → component dùng chung → UI chức năng; Phú và Phúc thống nhất style, tác giả từng PR sửa delta của mình |
| `Resources/Shared.vi-VN.resx`, `Shared.en-US.resx` | Role/layout → localization → key UI; ghép từng key, giữ đủ cặp VI/EN; không thay toàn file làm mất key của phần đã merge |

Nền dev hiện có `00000000000000_CreateIdentitySchema`, `20261008142750_CoreSchema` và `ApplicationDbContextModelSnapshot`. Không đổi schema hoặc tạo migration trong VHT-117.

Quy trình khi có migration ở PR khác:

1. Tác giả khai báo migration mới, nền dev, thay đổi schema/dữ liệu, thứ tự và hướng phục hồi nếu triển khai lỗi; không sửa migration đã merge.
2. Reviewer đối chiếu entity/configuration, migration, snapshot và [nguồn ERD/SQL](../design/database/so_do_quan_he_vht5_schema.sql). Chặn PR nếu snapshot/migration xung đột hoặc có mất dữ liệu chưa được xử lý.
3. Sau khi migration trước đã merge, tác giả PR sau cập nhật dev và tái tạo **migration chưa merge của mình** nếu cần. Không chọn toàn bộ “ours/theirs” cho snapshot; không reset/drop DB dùng chung.
4. Trước khi merge, tác giả cung cấp kết quả `dotnet ef migrations list --project NhaTro/Web`, script idempotent và thử `database update` trên DB test riêng: DB sạch và DB có dữ liệu mẫu không nhạy cảm. Đây là yêu cầu cho PR migration, chưa chạy trong VHT-117.
5. Tính điều phối cập nhật ERD theo migration đã merge; kết quả test/commit được bàn giao cho VHT-21. Không đánh dấu đạt khi chưa có kết quả thực tế.

## CI hiện có và điều kiện merge

Workflow [ci.yml](../../.github/workflows/ci.yml): GitHub Actions, Windows, .NET `10.0.x`, một job `build-test` chạy trên `NhaTro/NhaTro.slnx`:

```text
dotnet restore NhaTro/NhaTro.slnx
dotnet build NhaTro/NhaTro.slnx --no-restore --nologo
dotnet test NhaTro/NhaTro.slnx --no-build --nologo
```

VHT-117 bổ sung `dev` vào `push.branches` và `pull_request.branches`, giữ `main` và toàn bộ job hiện có. `pull_request.branches` lọc nhánh **đích**: PR feature → dev sẽ thuộc phạm vi trigger; push trực tiếp feature không thuộc trigger push, nhưng cập nhật PR mở vào dev sẽ phát sinh sự kiện PR.

Thay đổi local chưa chứng minh workflow đã chạy. Trước khi dựa vào CI cần xem run thực tế của PR VHT-117 và đảm bảo workflow có trong nhánh đích khi tiếp nhận PR sau. Không tự merge VHT-117 để kích hoạt cấu hình.

Điều kiện sẵn sàng merge, áp dụng cho từng PR mới:

- Nhánh đích dev, đúng phạm vi, không có secret hoặc file local bị cấm; phụ thuộc cần thiết đã có.
- Có reviewer độc lập và approval còn phù hợp với bản code hiện tại; thay đổi đáng kể sau review phải review lại.
- CI `build-test` thành công cho phiên bản PR hiện tại với đúng nền dev; không dùng run cũ của PR main hoặc SHA cũ.
- Không xung đột; file chung/migration được ghép đúng lượt, bằng chứng test do tác giả cung cấp phù hợp diff.
- Kiểm tra nghiệp vụ/quyền cần thiết có kết quả hoặc được ghi rõ còn chặn. Test scaffold hoặc CI xanh không chứng minh P1–P3 chạy được.

Nếu CI đỏ/không chạy: Phú ghi run URL, SHA, lỗi và tác giả chịu trách nhiệm. Chỉ xem xét chạy lại khi lỗi hạ tầng hoặc có bản cập nhật; không bỏ qua CI. Branch protection/rulesets chưa được xác minh và không được thay đổi trong task này.

## Bàn giao VHT-21 (1D)

| Việc cần tiếp nhận | Người phụ trách | Trạng thái tại mốc rà |
| --- | --- | --- |
| Chốt reviewer chính/dự phòng và gán trên PR mới | Phú, Tính dự phòng | Bảng đề xuất có sẵn; chưa gửi request hoặc xác nhận nhận việc |
| Rà approval/check còn thiếu của lịch sử PR, nhất là #14–#17 | Phú; reviewer tương ứng | Chưa đủ bằng chứng; API giới hạn lượt đọc |
| Review và đưa cấu hình CI VHT-117 vào dev | Reviewer độc lập; người có quyền merge | Chờ PR và kết quả kiểm; VHT-117 không tự merge |
| PR service thật/UI còn thiếu và thứ tự ghép | Tác giả từng task | Chưa có PR mở tại mốc rà; không kết luận hoàn thành từ tên PR đã merge |
| Kiểm bản chung, ghi SHA tích hợp | Phú phối hợp Phát | Thuộc VHT-21 1D, chưa chạy trong VHT-117 |

Phạm vi kiểm 1D theo VHT-14/VHT-22 và ma trận quyền: đăng nhập/tài khoản P1, phòng P2, khách P3; bốn vai trò, Admin bị chặn nghiệp vụ P2/P3, phạm vi tòa nhà/bản thân, validation, file trái quyền khi có chức năng tương ứng, VI/EN và giao diện. Chỉ kiểm phần tuần 1 đã bàn giao; không thêm triển khai công tắc/hàng đợi/thùng rác của đợt sau.

Mỗi lỗi sau merge ghi task/PR, SHA bản ghép, môi trường, bước tái hiện, kỳ vọng/thực tế, log và tác giả nhận sửa: Tính P1/P2/quyền, Phát P3/storage, Phúc UI P2/P3/component, Phú layout/localization, Đào formatter theo kế hoạch; đối chiếu tác giả thực tế khi giao. Người kiểm thử không tự sửa thay tác giả. Chưa có lỗi sau merge nào được tạo hoặc giao trong VHT-117.

**Trạng thái bàn giao:** đã có phương án nhánh/reviewer/thứ tự ghép và thay đổi CI local. Chưa xác nhận approval của nhóm, CI mới chạy đạt hoặc bản tích hợp P1–P3 đạt; các cổng đó phải có bằng chứng trước khi đánh dấu hoàn thành tương ứng.
