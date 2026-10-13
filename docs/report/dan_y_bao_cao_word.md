**DÀN Ý BÁO CÁO WORD VÀ PHÂN CÔNG NỘI DUNG**

**Đề tài: Web vận hành nhà trọ (NhaTro)**

| Hạng mục | Nội dung |
|----------------|--------------------------------------------------------------|
| Mục đích | Chia chương báo cáo, phân công người viết và đưa mẫu nội dung để mỗi người gửi phần kỹ thuật của mình. |
| Phạm vi hiện trạng | Theo trạng thái INIT của repo (commit `352a070`). |
| Người ghép báo cáo | Phúc. Cả nhóm cung cấp nội dung kỹ thuật phần mình. |
| Nguyên tắc | Chỉ mô tả những gì đã làm thật. Chức năng chưa có code phải ghi rõ "Chưa triển khai" và chỉ trình bày thiết kế. |
| Tài liệu nguồn | `docs/thiet_ke_he_thong.md`, `docs/tech-stack/cong_nghe_su_dung.md`, `docs/architecture/cau_truc_thu_muc.md`, `docs/design/phong_cach_giao_dien.md`, `docs/task/phan_cong_cong_viec.md`, `docs/task/cong_viec_theo_tuan.md`. |

# 1. Cách sử dụng tài liệu

1. Xem mục 3 để biết mình phụ trách chương và mục nào.
2. Lấy mẫu phù hợp ở mục 6 và xem phần việc riêng của mình ở mục 8. Mục 7 là nội dung đã điền sẵn từ INIT.
3. Điền theo mốc ở mục 4. Phần chưa có code thì giữ nguyên tiêu đề và ghi "Trạng thái: Chưa triển khai".
4. Gửi cho Phúc theo cách ở mục 9. Phúc ghép vào file Word chung, không viết hộ nội dung kỹ thuật.

Lưu ý: hai tệp `docs/task/timeline_song_song.md` và `Phan_cong_chi_tiet.xlsx` được nhắc trong yêu cầu nhưng chưa có trong repo. Dàn ý này căn cứ vào hai tệp phân công đang có. Khi có hai tệp đó, cần đối chiếu lại mục 3 và mục 4.

# 2. Quy tắc chung

Các quy tắc định dạng dưới đây là đề xuất. Nếu giảng viên có quy định riêng (font, lề, số trang, mẫu bìa) thì theo quy định đó.

| Hạng mục | Quy tắc |
|----------------|--------------------------------------------------------------|
| Định dạng văn bản | Font Times New Roman, cỡ 13, căn đều hai bên, chữ màu đen, không dùng icon hay emoji |
| Ngôn ngữ | Tiếng Việt. Tên class, bảng, cột, route giữ nguyên tiếng Anh như trong code |
| Tiêu đề | Heading 1 cho chương, Heading 2 cho mục `x.y`, Heading 3 cho `x.y.z`; không quá 3 cấp |
| Hình, bảng | Có chú thích đánh số theo chương, ví dụ Hình 4.1, Bảng 6.2; mọi hình và bảng đều được nhắc trong văn bản |
| Mã nguồn | Chỉ trích đoạn ngắn, tối đa khoảng 20 dòng, kèm đường dẫn tệp trong repo |
| Ảnh chụp màn hình | Định dạng PNG, chụp từ bản chạy thật; không lộ mật khẩu, chuỗi kết nối, CCCD hoặc dữ liệu cá nhân thật |
| Ảnh sơ đồ luồng | Dùng ảnh PNG có sẵn trong `docs/image/` (P1 đến P8), không chụp lại |
| Số liệu, kết quả test | Chỉ ghi kết quả đã chạy thật, kèm ngày chạy và commit; không điền số liệu mẫu giả làm thật |
| Chức năng chưa xong | Ghi "Chưa triển khai" ở đầu mục; không mô tả như đã chạy |
| Bảo mật | Không đưa secret, chuỗi kết nối có mật khẩu hoặc đường dẫn riêng của máy cá nhân vào báo cáo |
| Trích dẫn | Ghi nguồn tài liệu và công nghệ ở cuối chương hoặc ở phần Tài liệu tham khảo |
| Nơi lưu ảnh | `docs/report/images/[ten_nguoi]/hinh_[chuong]_[stt]_[mo_ta].png` |

# 3. Phân chia chương và người phụ trách

## 3.1. Phân chia theo chương

| Chương | Nội dung | Người viết chính | Hỗ trợ, cung cấp | Viết ngay được từ INIT |
|------|----------------|----------|--------------|----------------|
| Phần đầu | Bìa, lời cảm ơn, mục lục, danh mục hình, bảng, từ viết tắt | Phúc | Cả nhóm góp tên và thông tin | Có |
| 1 | Giới thiệu | Phúc | Cả nhóm góp ý | Có |
| 2 | Phân tích nghiệp vụ | Mỗi người một mục P# (2.3 đến 2.9) | Tính: 2.1, 2.2, 2.10 | Có, từ tài liệu thiết kế |
| 3 | Công nghệ và kiến trúc | Tính | Mai: 3.4; Phúc: 3.3 | Có một phần |
| 4 | Thiết kế cơ sở dữ liệu | Tính | Mỗi người: bảng của phần mình | Có: Identity, `Property`, `Room`, `Tenant` |
| 5 | Thiết kế giao diện | Phú | Phúc: 5.3 | Có phần quy chuẩn; chưa có màn hình thật |
| 6 | Cài đặt chức năng | Mỗi người một mục (6.1 đến 6.10) | Không | Một phần: chỉ P1, P2, P3 có entity |
| 7 | Kiểm thử | Phát (điều phối) | Mỗi người: test phần mình | Rất ít: mới có 3 test rỗng |
| 8 | Triển khai và vận hành | Phú | Tính: 8.3, 8.4 | Có một phần: CI |
| 9 | Kết luận và hướng phát triển | Phúc (ghép) | Cả nhóm | Chưa, chờ bản cuối |
| Cuối | Tài liệu tham khảo, phụ lục | Phúc | Cả nhóm | Có |

## 3.2. Phân chia theo mục

| Mục | Tên mục | Người viết | Hỗ trợ |
|----|----------------------------|------------|----------------------|
| 2.1 | Tác nhân và phạm vi quyền | Tính | Không |
| 2.2 | P1 - Tài khoản và phân quyền | Tính | Không |
| 2.3 | P2 - Quản lý phòng | Phúc | Không |
| 2.4 | P3 - Quản lý khách thuê | Phúc | Không |
| 2.5 | P4 - Quản lý hợp đồng | Mai | Không |
| 2.6 | P5 - Dịch vụ và điện nước | Mai | Phú |
| 2.7 | P6 - Hóa đơn và thanh toán | Phát | Không |
| 2.8 | P7 - Bảo trì và sự cố | Phú | Không |
| 2.9 | P8 - Báo cáo và thống kê | Phú | Phúc, Mai, Phát (nguồn số liệu) |
| 2.10 | Mối liên hệ giữa các chức năng | Tính | Không |
| 4.2 | Bảng Identity | Tính | Không |
| 4.3 | `Property`, `Room`, `Tenant` | Phúc | Tính rà ràng buộc |
| 4.4 | Hợp đồng, cọc, dịch vụ, điện nước | Mai | Không |
| 4.5 | Hóa đơn, thanh toán | Phát | Không |
| 4.6 | Sự cố bảo trì | Phú | Không |
| 6.1 | Tài khoản và phân quyền (P1) | Tính | Không |
| 6.2 | Phòng (P2) | Phúc | Không |
| 6.3 | Khách thuê (P3) | Phúc | Không |
| 6.4 | Hợp đồng và tiền cọc (P4) | Mai | Không |
| 6.5 | Điện nước và dịch vụ (P5) | Mai (backend), Phú (giao diện) | Không |
| 6.6 | Hóa đơn và thanh toán (P6) | Phát | Phúc: phần tệp minh chứng |
| 6.7 | Bảo trì và sự cố (P7) | Phú | Phúc: ảnh |
| 6.8 | Báo cáo và thống kê (P8) | Phú | Phúc, Mai, Phát: query nguồn |
| 6.9 | Lưu trữ tệp và giấy tờ | Phúc | Không |
| 6.10 | Bảo mật, log, lỗi, health | Tính | Không |

# 4. Mốc nội dung

Nội dung nghiệp vụ mới được cập nhật tại W2-22 và W3-20, chốt tại W4-14 (mã việc trong bảng phân công chi tiết). Mỗi mốc có mức nội dung tối thiểu như sau.

| Mốc | Mức nội dung mỗi mục chức năng | Ghi chú |
|----------|------------------------------------|----------------------------|
| Đợt 1A (hiện tại) | Tiêu đề đầy đủ; `x.1` mục tiêu, `x.2` luồng dữ liệu, `x.3` dữ liệu theo thiết kế; trạng thái đúng thực tế | Viết từ tài liệu thiết kế và entity đã có |
| W2-22 | Thêm `x.4` quy tắc, `x.5` phân quyền, `x.6` giao diện, `x.7` cài đặt cho phần đã chạy thật | Phần chưa chạy giữ "Chưa triển khai" |
| W3-20 | Thêm `x.8` kiểm thử với kết quả thật; cập nhật `x.9` và `x.10`; chương 7 và 8 có nội dung | Ghi rõ lỗi còn lại |
| W4-14 | Chốt toàn bộ: bỏ dòng nháp, đúng phiên bản cuối, hình ảnh từ bản chạy cuối | Phúc xuất bản cuối; không dùng ảnh hoặc số liệu bản cũ |

# 5. Dàn ý chi tiết

## Phần đầu

- Bìa: tên đề tài, môn học, giảng viên, danh sách thành viên, ngày nộp.
- Lời cảm ơn, mục lục tự động, danh mục hình, danh mục bảng, danh mục từ viết tắt (TT, YC, KQ, P1 đến P8, D1 đến D7, DFD, EF, DI, UI).

## Chương 1. Giới thiệu

- 1.1. Bối cảnh và vấn đề quản lý nhà trọ cần giải quyết.
- 1.2. Mục tiêu đề tài.
- 1.3. Đối tượng sử dụng: Chủ trọ, Quản lý, Khách thuê.
- 1.4. Phạm vi: tám tiến trình P1 đến P8; những gì không làm (không tích hợp cổng thanh toán, không ký số, theo mục 6 của tài liệu thiết kế).
- 1.5. Phương pháp thực hiện và kế hoạch 4 tuần.
- 1.6. Phân công nhóm (tóm tắt từ bảng phân công).
- 1.7. Bố cục báo cáo.

## Chương 2. Phân tích nghiệp vụ

- 2.1. Tác nhân và phạm vi quyền (bảng tác nhân ở mục 2 của tài liệu thiết kế).
- 2.2 đến 2.9. Mỗi tiến trình một mục, viết theo mẫu M-NV (mục 6.1).
- 2.10. Mối liên hệ giữa các chức năng (mục 5 của tài liệu thiết kế); chèn sơ đồ tổng hợp đã xuất thành ảnh.

## Chương 3. Công nghệ và kiến trúc

- 3.1. Bộ công nghệ (bảng ở mục 7.1, nêu phiên bản thực tế trong `Web.csproj`).
- 3.2. Kiến trúc tổng thể: monolith Blazor Web App; luồng Component, Service, EF Core, SQL Server; vẽ Hình 3.1.
- 3.3. Cấu trúc thư mục: phân biệt rõ hiện trạng và kế hoạch.
- 3.4. Tiện ích dùng chung: ngày và tiền, `TimeProvider`, Options validation ("Chưa triển khai" cho đến khi Mai gửi code).
- 3.5. Quy ước: mapping thủ công, không generic repository, kiểm quyền ở Service, `IDbContextFactory`.
- 3.6. Những công nghệ không dùng và lý do (mục 5 của tài liệu công nghệ).

## Chương 4. Thiết kế cơ sở dữ liệu

- 4.1. Nguyên tắc: xóa mềm, `CreatedAt`, `UpdatedAt`, `IsDeleted`, filtered unique index, khóa ngoại `Restrict`, tiền dùng `decimal`.
- 4.2. Bảng Identity (`AspNetUsers`, `AspNetRoles`, `AspNetUserPasskeys`, ...).
- 4.3. Bảng `Properties`, `Rooms`, `Tenants` (đã điền sẵn ở mục 7.2).
- 4.4. Hợp đồng, cọc, dịch vụ, điện nước ("Chưa triển khai").
- 4.5. Hóa đơn, thanh toán ("Chưa triển khai").
- 4.6. Sự cố bảo trì ("Chưa triển khai").
- 4.7. Sơ đồ quan hệ (ERD), Hình 4.1; Tính vẽ khi các bảng được gộp.
- 4.8. Migrations: danh sách, quy tắc cộng thêm, cách chạy.

## Chương 5. Thiết kế giao diện

- 5.1. Phong cách và quy chuẩn: font, màu, khoảng cách, icon, bố cục (tóm tắt từ tài liệu giao diện).
- 5.2. Theme, layout, sidebar, topbar, chế độ sáng và tối, responsive ("Chưa triển khai": `MainLayout` hiện chỉ có `@Body`).
- 5.3. Component dùng chung: form, bảng, dialog, trạng thái loading, rỗng, lỗi.
- 5.4. Khả năng truy cập: bàn phím, độ tương phản, giảm chuyển động, các viewport kiểm thử.

## Chương 6. Cài đặt chức năng

- 6.1 đến 6.10: mỗi mục theo mẫu M-CN (mục 6.2). Thứ tự và người viết ở mục 3.2.

## Chương 7. Kiểm thử

- 7.1. Chiến lược: unit, integration (SQL Server riêng, không dùng EF InMemory), E2E bằng Playwright.
- 7.2. Môi trường và cách chạy (`dotnet test`); nêu rõ hiện chỉ có 3 test rỗng nếu chưa có test thật.
- 7.3. Test theo chức năng: mỗi người một bảng theo mẫu M-TC (mục 6.3).
- 7.4. Test liên chức năng: ba vai trò, trùng hóa đơn, xác nhận đồng thời, rollback, mất kết nối (Phát).
- 7.5. Kết quả tổng hợp và lỗi còn lại, kèm người phụ trách.

## Chương 8. Triển khai và vận hành

- 8.1. CI GitHub Actions (restore, build, test; nêu đúng như `ci.yml`).
- 8.2. Triển khai IIS: HTTPS, WebSocket, reconnect ("Chưa triển khai").
- 8.3. Cấu hình và secrets: User Secrets, biến môi trường, Data Protection keys.
- 8.4. Sao lưu và phục hồi: database, tệp, keys ("Chưa triển khai").
- 8.5. Hướng dẫn cài đặt: tham chiếu README ở phụ lục.

## Chương 9. Kết luận

- 9.1. Kết quả đạt được: liệt kê theo chức năng, ghi rõ đã chạy thật hay chưa.
- 9.2. Hạn chế và việc còn lại (bắt đầu từ danh sách cuối mục 7.3).
- 9.3. Hướng phát triển (chỉ những gì tài liệu thiết kế đã nêu là ngoài phạm vi hiện tại).
- 9.4. Bài học rút ra.

## Tài liệu tham khảo và phụ lục

- Tài liệu tham khảo: tài liệu .NET, Blazor, MudBlazor, EF Core, Identity (ghi tên, phiên bản, ngày truy cập).
- Phụ lục A: README cài đặt.
- Phụ lục B: Hướng dẫn sử dụng theo vai trò ("Chưa triển khai" cho đến khi có giao diện).
- Phụ lục C: Bảng phân công và đóng góp từng người.
- Phụ lục D: Danh sách hình, bảng và nơi lưu ảnh gốc.

# 6. Mẫu nội dung

## 6.1. Mẫu M-NV: mục phân tích nghiệp vụ (chương 2)

Mỗi người lấy phần P# của mình trong mục 4 của tài liệu thiết kế, viết lại bằng lời của mình, không sao chép nguyên văn.

| Phần | Nội dung cần viết | Gợi ý độ dài |
|----------|------------------------------------------|------------|
| Tiêu đề | `2.x P[số] - [Tên tiến trình]`; người viết; ngày cập nhật | 1 dòng |
| Mục đích | Tiến trình giúp [tác nhân] làm được [việc gì] | 2 đến 4 câu |
| Tác nhân tham gia | Mỗi tác nhân (Chủ trọ, Quản lý, Khách thuê) làm gì trong tiến trình | 3 dòng |
| Luồng dữ liệu | Bảng "Nguồn - Đích - Dữ liệu trao đổi" lấy từ tài liệu thiết kế; kèm ảnh `pX_....png` trong `docs/image/` | 1 bảng, 1 hình |
| Diễn giải | Kho dữ liệu D# nào được đọc, được ghi; liên hệ với tiến trình khác | 1 đến 2 đoạn |
| Ngoài phạm vi | Những gì tài liệu thiết kế nói rõ là không làm | 1 đến 3 dòng |

## 6.2. Mẫu M-CN: mục cài đặt chức năng (chương 6)

Đầu mỗi mục ghi: người viết; trạng thái (Chưa triển khai, Đang làm, Đã chạy thật); ngày cập nhật; mã commit hoặc PR.

| Mục | Nội dung cần viết |
|------------|-------------------------------------------------------------|
| `6.x.1` Mục tiêu và tác nhân | 3 đến 5 câu: chức năng làm gì, cho ai |
| `6.x.2` Luồng dữ liệu | Tham chiếu mục ở chương 2; chỉ nêu phần đã cài đặt |
| `6.x.3` Dữ liệu | Bảng gồm cột Cột, Kiểu, Bắt buộc, Ràng buộc, Ý nghĩa; quan hệ khóa ngoại, index, unique (có filter xóa mềm hay không); enum và các giá trị |
| `6.x.4` Quy tắc nghiệp vụ | Đánh số BR-[mã]-01, BR-[mã]-02; mỗi quy tắc một dòng nêu điều kiện và kết quả. Với trạng thái: bảng "Từ - Sang - Điều kiện - Ai được làm". Với tính tiền: công thức, làm tròn, nguồn giá (giá lịch sử hay giá hiện tại) |
| `6.x.5` Phân quyền | Bảng vai trò và thao tác (Xem, Tạo, Sửa, Xóa, Duyệt hoặc Xác nhận); nêu nơi kiểm tra quyền (Service) |
| `6.x.6` Giao diện | Bảng gồm Màn hình, Route, Vai trò, Hình; mô tả trạng thái loading, rỗng, lỗi |
| `6.x.7` Cài đặt | Class và Service chính, đường dẫn tệp, ranh giới transaction (Service nào mở context và commit); đoạn mã minh họa tối đa 20 dòng kèm giải thích |
| `6.x.8` Kiểm thử | Tham chiếu mục 7.3: số test và kết quả |
| `6.x.9` Hạn chế và chưa làm | Liệt kê trung thực; không để trống nếu còn lỗi đã biết |
| `6.x.10` Phụ thuộc | Nhận từ ai (DTO, dữ liệu), bàn giao cho ai, trạng thái bàn giao |

## 6.3. Mẫu M-TC: bảng test (chương 7)

| Mã | Mô tả và dữ liệu vào | Kết quả mong đợi | Kết quả thực tế | Đạt | Ngày, commit |
|--------|------------------|--------------|-------------|----|----------|
| TC-[mã]-01 | | | | | |

## 6.4. Danh sách kiểm tra chất lượng cho mỗi mục

| STT | Tiêu chí | Người viết tự kiểm | Phúc kiểm |
|----|------------------------------------------|----------|----------|
| 1 | Trạng thái ghi đúng thực tế, không mô tả điều chưa chạy như đã chạy | | |
| 2 | Mọi bảng và hình có chú thích, đánh số, được nhắc trong văn bản | | |
| 3 | Tên cột, kiểu, ràng buộc khớp với code hoặc migration hiện tại | | |
| 4 | Ảnh chụp từ bản chạy thật, không lộ dữ liệu cá nhân hoặc secret | | |
| 5 | Đoạn mã có đường dẫn tệp và nằm trong giới hạn độ dài | | |
| 6 | Kết quả test có ngày chạy và commit | | |
| 7 | Phần hạn chế được viết trung thực | | |
| 8 | Đã báo người bị ảnh hưởng nếu đổi schema hoặc DTO | | |

# 7. Nội dung điền sẵn từ INIT

## 7.1. Bộ công nghệ thực tế (cho mục 3.1)

Lấy từ `NhaTro/Web/Web.csproj` và các tệp `.csproj` của project test.

| Thành phần | Công nghệ và phiên bản trong repo |
|----------------|--------------------------------------------------------------|
| Nền tảng | .NET 10 (`net10.0`), `Nullable` và `ImplicitUsings` bật |
| Web | Blazor Web App, Interactive Server |
| Giao diện | MudBlazor 9.11.0 |
| ORM, database | EF Core 10.0.12, `Microsoft.EntityFrameworkCore.SqlServer` |
| Đăng nhập | ASP.NET Core Identity (EF stores, schema Version3), cookie |
| Logging | Serilog.AspNetCore 10.0.0, Serilog.Sinks.Console 6.1.1, Serilog.Sinks.File 7.0.0 |
| Kiểm thử | xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, Mvc.Testing 10.0.12, Playwright 1.63.0 |
| CI | GitHub Actions, `windows-latest`, .NET `10.0.x` |
| Cơ sở dữ liệu mặc định | SQL Server LocalDB (`(localdb)\mssqllocaldb`) |

## 7.2. Ba bảng lõi (cho mục 4.3)

Lấy từ `Data/Configurations/` và migration `20261008142750_CoreSchema`. Cả ba bảng đều có khóa chính `Id` (kiểu `int`, tự tăng), các cột `CreatedAt` (`datetime2`), `UpdatedAt` (`datetime2`, cho phép null), `IsDeleted` (`bit`) và query filter `!IsDeleted`.

**Bảng `Properties` (nhà trọ)**

| Cột | Kiểu | Bắt buộc | Ràng buộc | Ý nghĩa |
|----------|----------|------|----------------------|------------|
| `Name` | `nvarchar(200)` | Có | Không | Tên nhà trọ |
| `Address` | `nvarchar(500)` | Không | Không | Địa chỉ |
| `OwnerId` | `nvarchar(450)` | Có | Khóa ngoại tới `AspNetUsers.Id` (Restrict); index `IX_Properties_OwnerId` | Chủ sở hữu |
| `IsActive` | `bit` | Có | Mặc định `true` trong entity | Đang hoạt động |

**Bảng `Rooms` (phòng)**

| Cột | Kiểu | Bắt buộc | Ràng buộc | Ý nghĩa |
|----------|----------|------|----------------------|------------|
| `PropertyId` | `int` | Có | Khóa ngoại tới `Properties.Id` (Restrict) | Thuộc nhà trọ nào |
| `Code` | `nvarchar(50)` | Có | Unique `(PropertyId, Code)` khi `IsDeleted = 0` | Mã phòng |
| `Floor` | `int` | Không | Không | Tầng |
| `AreaM2` | `decimal(10,2)` | Không | Không | Diện tích (m2) |
| `StandardPrice` | `decimal(18,2)` | Có | Không | Giá chuẩn |
| `Status` | `int` | Có | Enum `RoomStatus`: 0 `Available`, 1 `Rented`, 2 `Maintenance`; mặc định `Available` | Trạng thái phòng |
| `IsActive` | `bit` | Có | Mặc định `true` | Đang hoạt động |

**Bảng `Tenants` (khách thuê)**

| Cột | Kiểu | Bắt buộc | Ràng buộc | Ý nghĩa |
|----------|----------|------|----------------------|------------|
| `FullName` | `nvarchar(200)` | Có | Không | Họ tên |
| `Phone` | `nvarchar(20)` | Có | Không | Số điện thoại |
| `Email` | `nvarchar(256)` | Không | Không | Email |
| `CitizenId` | `nvarchar(20)` | Không | Unique khi `CitizenId IS NOT NULL AND IsDeleted = 0` | CCCD |
| `UserId` | `nvarchar(450)` | Không | Khóa ngoại tới `AspNetUsers.Id` (Restrict); index `IX_Tenants_UserId` | Tài khoản đăng nhập liên kết |

Điểm cần nói trung thực trong báo cáo: `Room` và `Tenant` chưa có thuộc tính điều hướng; `Tenant` chưa gắn với `Property`, nên chưa có cách giới hạn khách theo nhà trọ. Khi Phúc hoặc Tính thay đổi, cần cập nhật lại mục này.

## 7.3. Hiện trạng cần ghi đúng trong báo cáo

| Chủ đề | Tài liệu thiết kế nói | Hiện trạng code (commit `352a070`) | Cách viết trong báo cáo |
|----------|----------------|----------------------|----------------|
| Tên project, DbContext | `NhaTro.Web`, `AppDbContext` | Project `Web`, `ApplicationDbContext` | Dùng tên thực tế |
| Thư mục | `Authorization/`, `Infrastructure/`, `Common/`, ... | Chưa tồn tại | Ghi là kế hoạch |
| Phân quyền | Ba vai trò, policy | Chưa có vai trò, seed, policy; chưa trang nào dùng `[Authorize]` | Chưa triển khai |
| Layout, theme | Sidebar, topbar, token | `MainLayout` chỉ có `@Body`; trang `/` là trang thử MudBlazor | Chưa triển khai |
| Nhà trọ, phòng, khách | Service, DTO, giao diện | Chỉ có entity và cấu hình EF | Chỉ báo cáo phần dữ liệu |
| Hợp đồng đến báo cáo | P4 đến P8 | Chưa có bảng, chưa có code | Chưa triển khai |
| Email | Gửi email xác nhận | `IdentityNoOpEmailSender`; tài khoản xác nhận bằng liên kết hiện trên trang | Nêu là hạn chế |
| Health, Options validation, lưu trữ tệp | Có kế hoạch | Chưa có | Chưa triển khai |
| Test | xUnit, integration, Playwright | 3 test rỗng | Ghi đúng, không nêu số test giả |
| Tệp gốc của repo | `README`, `global.json`, `.editorconfig` | Có README (bản đầu); chưa có hai tệp còn lại | Nêu nếu liên quan |

Các điểm kỹ thuật nên đưa vào mục hạn chế (9.2) nếu chưa được sửa:

1. `Home.razor` đặt `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider` trong trang; các provider này nên nằm ở layout.
2. `RequireConfirmedAccount = true` nhưng chưa có dịch vụ gửi email thật.
3. `AddIdentityCore` chưa gọi `AddRoles`, nên chưa dùng được `RoleManager` hoặc `[Authorize(Roles = ...)]`.
4. Cùng đăng ký `AddDbContext` và `AddDbContextFactory`; cần thống nhất cách dùng.
5. Chuỗi kết nối mặc định dùng LocalDB, chỉ chạy trên Windows có LocalDB.

# 8. Phần việc riêng của từng người

## 8.1. Phúc: ghép báo cáo, README, phòng, khách thuê, lưu trữ tệp

| Hạng mục | Nội dung |
|------------|--------------------------------------------------------------|
| Mục phụ trách | Phần đầu, chương 1, 2.3, 2.4, 3.3, 4.3, 5.3, 6.2, 6.3, 6.9, chương 9, phụ lục; ghép toàn bộ báo cáo |
| Viết ngay | Chương 1 (từ tài liệu thiết kế); 2.3 và 2.4 (P2, P3); 4.3 (dùng bảng ở mục 7.2); 3.3 (cấu trúc hiện trạng và kế hoạch); phụ lục A (README) |
| Chờ chức năng | 6.2 và 6.3 từ `x.4` đến `x.8` khi có Service và giao diện phòng, khách; 6.9 khi có lưu trữ tệp; chương 9 khi chốt |
| Hình, bảng | Hình 2.x (P2, P3 từ `docs/image`), Bảng 4.x (ba bảng lõi), ảnh màn hình phòng và khách |
| Việc ghép | Giữ mục lục, danh mục hình và bảng; kiểm tra chú thích, phiên bản, ảnh; nhắc từng người theo mốc ở mục 4 |

## 8.2. Tính: tài khoản, cơ sở dữ liệu, bảo mật

| Hạng mục | Nội dung |
|------------|--------------------------------------------------------------|
| Mục phụ trách | 2.1, 2.2, 2.10, chương 3 (3.1, 3.2, 3.5, 3.6), 4.1, 4.2, 4.7, 4.8, 6.1, 6.10, 8.3, 8.4 |
| Viết ngay | 2.1, 2.2, 2.10 (từ tài liệu thiết kế); 3.1 (dùng bảng 7.1); 4.1, 4.2, 4.8 (migration `CreateIdentitySchema`, `CoreSchema`, quy tắc cộng thêm) |
| Chờ chức năng | 6.1 từ `x.4` đến `x.7` khi có vai trò và kiểm quyền; 6.10 khi có health, log, lỗi; 4.7 khi các bảng được gộp; 8.4 khi thử sao lưu và phục hồi |
| Hình, bảng | Hình 3.1 (kiến trúc), Hình 4.1 (ERD), bảng vai trò và quyền, bảng Identity |
| Lưu ý | Rà các bảng 4.3 đến 4.6 của người khác có đúng ràng buộc và quan hệ |

## 8.3. Mai: hợp đồng, điện nước (backend), tiện ích

| Hạng mục | Nội dung |
|------------|--------------------------------------------------------------|
| Mục phụ trách | 2.5, 2.6, 3.4, 4.4, 6.4, 6.5 (backend), phần test hợp đồng và chi phí ở 7.3 |
| Viết ngay | 2.5 và 2.6 (P4, P5 từ tài liệu thiết kế); `x.1` đến `x.3` của 6.4 và 6.5 theo thiết kế, ghi "Chưa triển khai" |
| Chờ chức năng | 3.4 khi có tiện ích, Options, `TimeProvider`; 6.4 và 6.5 từ `x.4` đến `x.8` khi backend chạy |
| Hình, bảng | Hình 2.x (P4, P5), bảng trạng thái hợp đồng, bảng chuyển trạng thái, bảng test hợp đồng |
| Lưu ý | Nêu rõ cơ chế chống hợp đồng trùng hiệu lực và ranh giới transaction khi cập nhật trạng thái phòng |

## 8.4. Phát: hóa đơn, thanh toán, kiểm thử tổng

| Hạng mục | Nội dung |
|------------|--------------------------------------------------------------|
| Mục phụ trách | 2.7, 4.5, 6.6, chương 7 (điều phối 7.1, 7.2, 7.4, 7.5) |
| Viết ngay | 2.7 (P6, kèm luồng thanh toán phía Khách thuê gồm 5 bước); 7.1 chiến lược kiểm thử; `x.1` đến `x.3` của 6.6 theo thiết kế, ghi "Chưa triển khai" |
| Chờ chức năng | 6.6 từ `x.4` đến `x.8`; 7.4 và 7.5 khi các phần được ghép thật |
| Hình, bảng | Hình 2.x (P6), bảng trạng thái hóa đơn và thanh toán, bảng test tài chính, bảng tổng hợp lỗi |
| Lưu ý | Gửi yêu cầu hoặc minh chứng thanh toán không đồng nghĩa hóa đơn đã thanh toán; nêu rõ trong mục 6.6.4 |

## 8.5. Phú: giao diện, sự cố, báo cáo, triển khai

| Hạng mục | Nội dung |
|------------|--------------------------------------------------------------|
| Mục phụ trách | 2.8, 2.9, 4.6, chương 5 (5.1, 5.2, 5.4), 6.5 (giao diện), 6.7, 6.8, chương 8 (8.1, 8.2, 8.5) |
| Viết ngay | 2.8 và 2.9 (P7, P8); 5.1 và 5.4 (tóm tắt quy chuẩn giao diện); 8.1 (CI đúng như `ci.yml`) |
| Chờ chức năng | 5.2 khi có theme và layout; 6.7 và 6.8 khi chạy thật; 8.2 khi thử IIS |
| Hình, bảng | Hình 2.x (P7, P8), ảnh layout và theme sáng, tối, ảnh dashboard, bảng token màu và cỡ chữ |
| Lưu ý | Báo cáo quản trị không dành cho Khách thuê; nguồn chưa ghép thì ghi rõ chưa ghép, không dùng số mẫu giả làm thật |

# 9. Cách gửi nội dung và ghép

| Hạng mục | Nội dung |
|------------|--------------------------------------------------------------|
| Nơi gửi | Mỗi người gửi Phúc một tệp Markdown hoặc Word theo mục của mình, kèm thư mục ảnh `docs/report/images/[ten_nguoi]/` |
| Đặt tên tệp | `muc_[so_muc]_[ten_ngan].md`, ví dụ `muc_6-2_phong.md` |
| Mẫu tin nhắn gửi | "Mình gửi mục [số mục] - [tên]. Trạng thái: [Chưa triển khai, Đang làm hoặc Đã chạy thật]. Dựa trên commit hoặc PR: [mã]. Ảnh ở `docs/report/images/[ten]/`. Còn thiếu: [liệt kê]. Phần này cần từ bạn: [người] - [nội dung]." |
| Phúc kiểm khi nhận | (1) Đúng mẫu M-NV hoặc M-CN và đúng mức nội dung của mốc hiện tại. (2) Trạng thái khớp thực tế; hỏi người viết nếu nghi ngờ, không tự coi là đã xong. (3) Hình và bảng đúng chú thích, đánh số theo chương. (4) Không có secret, đường dẫn riêng của máy hoặc dữ liệu cá nhân thật. (5) Tên cột, kiểu, ràng buộc khớp với code hoặc migration. |
| Khi hai người cùng sửa một chương | Mỗi người chỉ sửa mục của mình; muốn đổi mục của người khác thì nhắn người đó. Tệp chung sửa theo lượt, như quy tắc ghép PR và migrations. |

# 10. Bảng theo dõi tiến độ nội dung

Cập nhật theo từng mốc bằng một trong các trạng thái: Chưa bắt đầu, Đã có khung, Đã gửi, Đã duyệt, Đã chốt.

| Mục | Người viết | Đợt 1A | W2-22 | W3-20 | W4-14 |
|--------------|--------------|------|------|------|------|
| Phần đầu, chương 1 | Phúc | | | | |
| 2.1, 2.2, 2.10 | Tính | | | | |
| 2.3, 2.4 | Phúc | | | | |
| 2.5, 2.6 | Mai | | | | |
| 2.7 | Phát | | | | |
| 2.8, 2.9 | Phú | | | | |
| Chương 3 | Tính (3.3 Phúc, 3.4 Mai) | | | | |
| Chương 4 | Tính (4.3 Phúc, 4.4 Mai, 4.5 Phát, 4.6 Phú) | | | | |
| Chương 5 | Phú (5.3 Phúc) | | | | |
| 6.1, 6.10 | Tính | | | | |
| 6.2, 6.3, 6.9 | Phúc | | | | |
| 6.4, 6.5 | Mai (6.5 giao diện: Phú) | | | | |
| 6.6 | Phát | | | | |
| 6.7, 6.8 | Phú | | | | |
| Chương 7 | Phát (mỗi người test phần mình) | | | | |
| Chương 8 | Phú (8.3, 8.4 Tính) | | | | |
| Chương 9, phụ lục | Phúc | | | | |
