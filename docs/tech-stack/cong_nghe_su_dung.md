# Công nghệ sử dụng — Web vận hành nhà trọ

## 1. Định hướng

- Backend và frontend chung **một project web**, phát triển bằng **Visual Studio**.
- Dùng **C#** cho logic giao diện và nghiệp vụ; Razor markup và CSS cho phần trình bày.
- Giao diện hiện đại, responsive, hỗ trợ chế độ sáng/tối.
- Tham khảo công nghệ từ `Backend_CSharp_DotNet_Job_Ready_Roadmap_2026.md`; chỉ chọn những phần phù hợp với dự án.
- Đây là kế hoạch công nghệ cho **toàn bộ dự án**, không chia theo giai đoạn. Tài liệu xác định bộ công nghệ sẽ dùng, **không có nghĩa ứng dụng đã được triển khai hoặc kiểm thử**.

## 2. Bộ công nghệ

| Thành phần | Công nghệ | Mục đích |
| --- | --- | --- |
| IDE | Visual Studio phiên bản hỗ trợ .NET 10 | Viết code, chạy và debug toàn bộ web |
| Ngôn ngữ, nền tảng | C# 14, .NET 10 LTS | Nền tảng phát triển |
| Web | Blazor Web App — Interactive Server | Giao diện tương tác bằng C#, cùng ứng dụng với backend |
| Giao diện | MudBlazor, Razor markup, CSS | Component Material Design, form, bảng, dialog, responsive |
| Dashboard, biểu đồ | MudChart của MudBlazor | Trình bày doanh thu, công nợ và tỷ lệ sử dụng phòng |
| Backend | Service C# trong project web | Xử lý nghiệp vụ, kiểm tra dữ liệu và quyền truy cập |
| Kiến trúc | Monolith phân lớp đơn giản + DI tích hợp | Component, Service và Data chung ứng dụng; không tách nhiều service |
| Cơ sở dữ liệu | SQL Server | Lưu dữ liệu nghiệp vụ |
| Quản lý database | SQL Server Management Studio (SSMS) | Kiểm tra dữ liệu, truy vấn và sao lưu |
| ORM | EF Core 10 + migrations | Truy vấn LINQ, ánh xạ và quản lý schema |
| Đăng nhập | ASP.NET Core Identity + cookie | Quản lý tài khoản, mật khẩu băm và vai trò |
| Phân quyền | Authorization policies + kiểm tra phạm vi dữ liệu | Bảo vệ dữ liệu theo chủ trọ, tòa nhà và hợp đồng |
| Validation | DataAnnotations + quy tắc trong Service | Kiểm tra form và ràng buộc nghiệp vụ |
| Xử lý lỗi | ASP.NET Core Exception Handler + Blazor ErrorBoundary | Xử lý lỗi HTTP và lỗi component, không lộ stack trace |
| Logging | `ILogger<T>` + Serilog Console/File | Log có cấu trúc, xoay vòng file; không ghi mật khẩu, token hoặc giấy tờ |
| Kiểm tra hoạt động | ASP.NET Core Health Checks | Kiểm tra ứng dụng và kết nối database |
| Lưu tệp | File storage ngoài `wwwroot` | Lưu giấy tờ, biên lai; tải qua endpoint có kiểm tra quyền |
| Bảo mật web | HTTPS/HSTS, Antiforgery, Data Protection, Rate Limiting | Bảo vệ cookie, form và endpoint nhạy cảm |
| Kiểm thử | xUnit + WebApplicationFactory | Unit test và integration test nghiệp vụ, phân quyền |
| Kiểm thử giao diện | Microsoft.Playwright cho .NET | Kiểm tra luồng đăng nhập, nhập liệu và thao tác trên trình duyệt bằng C# |
| Mã nguồn | Git + GitHub | Quản lý phiên bản |
| CI | GitHub Actions | Restore, build, test, kiểm tra NuGet Audit và tạo bản publish |
| Triển khai | IIS trên Windows + ASP.NET Core Hosting Bundle | Chạy ứng dụng Blazor, bật WebSocket và HTTPS |
| Cấu hình, secret | appsettings, User Secrets, biến môi trường | Cấu hình ứng dụng; bảo vệ thông tin kết nối |
| Cấu hình kiểu dữ liệu rõ ràng | `IOptions<T>` + Options Validation | Đọc cấu hình giới hạn tệp, đường dẫn lưu trữ, thiết lập nghiệp vụ; kiểm tra bằng `ValidateOnStart` |
| Quản lý thời gian | `TimeProvider`, `DateOnly`, UTC | Thống nhất thời gian và kiểm thử hạn thanh toán, ngày hóa đơn, thời hạn hợp đồng |
| Sao lưu, phục hồi | SQL Server Backup/Restore + sao lưu thư mục tệp và Data Protection keys | Khôi phục dữ liệu và cấu hình bảo mật khi có sự cố |

### Thành phần phụ thuộc yêu cầu nghiệp vụ

Các mục dưới đây là lựa chọn theo chức năng, **không phải công nghệ dành cho giai đoạn sau**. Chỉ đưa vào bộ triển khai khi yêu cầu tương ứng được xác nhận trong tài liệu thiết kế.

| Thành phần | Công nghệ | Điều kiện sử dụng |
| --- | --- | --- |
| Xử lý định kỳ | `BackgroundService` + `PeriodicTimer` | Có yêu cầu tự động kiểm tra hợp đồng/công nợ hoặc lập hóa đơn theo lịch; dùng thành phần tích hợp .NET, không thêm job framework |
| Xuất Excel | ClosedXML | Có yêu cầu xuất danh sách hoặc báo cáo Excel |
| Tạo PDF | QuestPDF | Có yêu cầu tạo hợp đồng, hóa đơn hoặc phiếu thu PDF; kiểm tra giấy phép theo tổ chức sử dụng trước khi chọn |

## 3. Cách kết hợp

```text
Component Blazor → Service C# → EF Core → SQL Server
```

- Không tách frontend riêng; giao diện gọi Service trực tiếp, không xây REST API riêng cho các thao tác nội bộ.
- Dùng `IDbContextFactory` để tạo DbContext theo từng thao tác, tránh giữ DbContext lâu dài trong giao diện.
- Interactive Server cần kết nối liên tục với server; mất mạng có thể gián đoạn thao tác.
- Kiểm tra quyền tại backend, không chỉ ẩn nút trên giao diện.
- Secret dùng User Secrets khi phát triển và cấu hình môi trường khi triển khai; không commit vào Git.
- CCCD và biên lai lưu ngoài `wwwroot`, kiểm tra quyền khi tải xuống.
- Đăng nhập/đăng xuất dùng luồng HTTP của Identity; không sửa cookie trực tiếp trong circuit Blazor.
- SignalR là cơ chế kết nối tích hợp của Interactive Server, không xây thêm hub riêng khi không có nghiệp vụ yêu cầu.

## 4. Quy ước sử dụng

| Nhóm | Quyết định |
| --- | --- |
| Phiên bản | EF Core và provider SQL Server dùng cùng dòng 10.x; MudBlazor và các package phải tương thích .NET 10; ghim phiên bản SDK/package trong repo |
| Giao diện | Một theme thống nhất, font hỗ trợ tiếng Việt, responsive, sáng/tối, loading/empty/error state; không dùng thêm Bootstrap |
| Dữ liệu | LINQ async, projection, phân trang; tiền dùng `decimal`; transaction và optimistic concurrency cho thao tác quan trọng. Phí dịch vụ dạng danh mục + dòng chi tiết (`InvoiceItem`), không ghim cột `TienDien/TienNuoc` cứng; đa tòa nhà (`Property`) từ đầu; lưu giá lịch sử (snapshot) vào hợp đồng/hóa đơn thay vì chỉ trỏ giá hiện tại |
| Chống ghi nhận trùng | Dùng idempotency key hoặc mã giao dịch duy nhất cho xác nhận thu tiền; unique theo hợp đồng và kỳ hóa đơn; lưu kết quả và khóa chống trùng trong cùng transaction với thay đổi nghiệp vụ, không chỉ khóa nút trên UI |
| Toàn vẹn database | Thiết lập unique index (dùng filtered index khi có xóa mềm), foreign key, check constraint và quy tắc xóa phù hợp; không xóa dây chuyền lịch sử hợp đồng, hóa đơn và thu tiền. Bảng quan trọng có `CreatedAt/UpdatedAt/IsDeleted` có chọn lọc + query filter; trạng thái hóa đơn/thanh toán dùng enum và kiểm tra chuyển trạng thái hợp lệ trong Service, chỉ các trạng thái đúng luồng P6 |
| Thời gian nghiệp vụ | Lấy thời gian hiện tại qua `TimeProvider`; timestamp lưu UTC, hiển thị theo múi giờ Việt Nam; dùng `DateOnly` cho ngày thuần túy, xác định kỳ hóa đơn theo lịch Việt Nam |
| Cấu hình nghiệp vụ | Bind cấu hình qua Options và kiểm tra khi khởi động; không tự thêm tiền phạt hay quy tắc tài chính chưa được xác nhận |
| Mất kết nối Blazor | Hiển thị mất kết nối/kết nối lại; circuit không phải nơi lưu dữ liệu bền vững. Cảnh báo dữ liệu chưa lưu, hỗ trợ bản nháp phía server cho biểu mẫu quan trọng; tra lại kết quả giao dịch trước khi gửi lại, không tự thực hiện lại thao tác thu tiền |
| Mapping | Mapping thủ công giữa entity và DTO; không thêm AutoMapper hoặc Mapster |
| Phân quyền | Kiểm tra vai trò và phạm vi tòa nhà/hợp đồng trong Service; không chỉ dựa vào `AuthorizeView` |
| Mật khẩu | Dùng password hasher của Identity, không tự viết thuật toán băm |
| Tệp tải lên | Giới hạn dung lượng ngay khi đọc stream, kiểm tra loại/nội dung, sinh tên lưu phía server; không tin tên và extension từ client; thư mục lưu không có quyền thực thi, tích hợp quét mã độc nếu điều kiện vận hành cho phép |
| Dữ liệu cá nhân | Giới hạn quyền xem/tải CCCD, audit truy cập không chứa nội dung giấy tờ; quy định thời hạn lưu và xóa khi đủ điều kiện, không xóa hồ sơ phải giữ theo nghĩa vụ nghiệp vụ/pháp lý; đánh giá nhu cầu mã hóa khi lưu và quản lý khóa riêng |
| Kiểm thử dữ liệu | Dùng SQL Server database riêng cho integration test; không dùng EF InMemory để chứng minh transaction/concurrency |
| Kiểm thử Blazor | WebApplicationFactory kiểm tra HTTP/auth; Playwright kiểm tra tương tác component trong trình duyệt |
| Logging, audit | Serilog lưu log kỹ thuật; lịch sử thao tác nghiệp vụ lưu trong SQL Server, không dùng log thay cho dữ liệu thu tiền |
| Triển khai | `dotnet publish` → IIS; bật WebSocket, HTTPS; apply migrations bằng bước triển khai riêng, migration cộng thêm không sửa vỡ bảng cũ, không tự migrate trong mỗi circuit |
| Lưu trữ vận hành | Tệp, log và Data Protection keys nằm ngoài thư mục publish; giới hạn quyền truy cập và thời gian giữ log |
| Phạm vi vận hành | Một instance web; sao lưu database, tệp và kiểm tra phục hồi; health live/ready không công khai thông tin nhạy cảm |
| Data Protection keys | Lưu bền vững ngoài thư mục publish, giới hạn ACL cho tài khoản ứng dụng; bảo vệ khóa trên đĩa bằng Windows DPAPI hoặc chứng thư phù hợp. Khi khôi phục, cần cả khóa và khả năng giải mã chúng, không chỉ sao chép thư mục keys |
| Tác vụ định kỳ nếu được chọn | Tạo DI scope/DbContext theo lần chạy, hỗ trợ cancellation, retry có giới hạn và log lỗi; lưu kỳ xử lý/trạng thái bền vững, có unique constraint/idempotency để khởi động lại không tạo trùng; phục hồi kỳ bị bỏ lỡ, không dựa riêng vào timer trong bộ nhớ |
| Excel/PDF nếu được chọn | Kiểm tra quyền và phạm vi dữ liệu trước khi xuất, giới hạn khối lượng; không xuất CCCD mặc định. Excel ghi dữ liệu người dùng dưới dạng giá trị, không coi là công thức; kiểm tra license và phiên bản package |

## 5. Không nằm trong kế hoạch công nghệ

- React/Vue, JWT, microservices, CQRS/MediatR, generic repository.
- Redis, message broker, background job framework bên ngoài (Hangfire/Quartz), Docker và Kubernetes. `BackgroundService` tích hợp .NET chỉ dùng theo điều kiện ở mục 2.
- Không thêm công cụ chỉ để dùng hết roadmap; thay đổi danh sách phải cập nhật lại kế hoạch này.

> Chọn phiên bản MudBlazor và các package tương thích với .NET 10. Kế hoạch này không thay đổi phạm vi nghiệp vụ trong `../thiet_ke_he_thong.md`.
