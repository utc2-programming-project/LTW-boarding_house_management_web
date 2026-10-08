# Cấu trúc thư mục — Web vận hành nhà trọ

## 1. Nguyên tắc

- Một project **Blazor Web App** chứa frontend và backend, mở và debug bằng Visual Studio.
- Chia **theo chức năng** trong `Features/`: giao diện, DTO, xử lý nghiệp vụ và quy tắc của chức năng nằm gần nhau.
- Phần tái sử dụng chia rõ: `Shared/` cho giao diện, `Common/` cho kiểu dữ liệu và tiện ích thuần, `Infrastructure/` cho tích hợp kỹ thuật.
- EF Core và cấu hình database tập trung trong `Data/`; không thêm generic repository hoặc framework mới.
- Đây là cấu trúc kế hoạch cho toàn hệ thống, chưa phải mã nguồn đã triển khai. Chỉ tạo folder khi có file thực tế.
- Tham chiếu: [công nghệ](../tech-stack/cong_nghe_su_dung.md), [nghiệp vụ](../thiet_ke_he_thong.md).

## 2. Cấu trúc toàn bộ repository

```text
LTW/
├── NhaTro.sln                       # Solution cho Visual Studio
├── global.json                      # Ghim .NET SDK
├── .editorconfig                    # Quy ước định dạng C# và Razor
├── .gitignore
├── README.md
├── .github/
│   └── workflows/                   # Build, test, audit và publish
├── src/
│   └── NhaTro.Web/
│       ├── NhaTro.Web.csproj
│       ├── Program.cs               # Khởi tạo host, DI và pipeline
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── Components/
│       │   ├── App.razor
│       │   ├── Routes.razor
│       │   ├── Layout/              # Layout, menu, header
│       │   ├── Account/             # Component Identity, luồng HTTP đăng nhập
│       │   └── Shared/
│       │       ├── Forms/           # Input và hiển thị validation dùng chung
│       │       ├── Tables/          # Thành phần bảng dùng chung
│       │       ├── Dialogs/         # Xác nhận, thông báo
│       │       ├── Feedback/        # Loading, empty, error, reconnect
│       │       └── Theme/           # Theme MudBlazor, màu và typography
│       ├── Features/
│       │   ├── Accounts/            # P1: tài khoản, vai trò, phân công quản lý
│       │   ├── Properties/          # P2: nhà trọ/tòa nhà và phạm vi sở hữu
│       │   ├── Rooms/               # P2: phòng, giá chuẩn, hiện trạng
│       │   ├── Tenants/             # P3: hồ sơ khách thuê, giấy tờ
│       │   ├── Contracts/           # P4: hợp đồng, thành viên, gia hạn, thanh lý
│       │   ├── Deposits/            # P4: thu, hoàn và xử lý tiền cọc
│       │   ├── Utilities/           # P5: dịch vụ, đơn giá, chỉ số điện nước
│       │   ├── Invoices/            # P6: lập/phát hành hóa đơn, dòng chi phí
│       │   ├── Payments/            # P6: yêu cầu/minh chứng thanh toán, xác nhận thu, biên lai
│       │   ├── Maintenance/         # P7: sự cố, tiến độ và chi phí bảo trì
│       │   ├── Reports/             # P8: doanh thu, công nợ, thống kê
│       │   └── Dashboard/           # Màn hình tổng quan theo vai trò
│       ├── Common/
│       │   ├── Constants/           # Hằng số dùng chung thực sự
│       │   ├── Models/              # PagedResult, kiểu kết quả dùng chung
│       │   ├── Exceptions/          # Lỗi dùng chung, không chứa dữ liệu nhạy cảm
│       │   ├── Extensions/          # Extension thuần, tên và phạm vi rõ ràng
│       │   └── Utils/               # Tiện ích thuần, không có I/O hoặc nghiệp vụ
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   ├── Configurations/      # Fluent API, chia theo feature
│       │   ├── Migrations/          # EF Core migrations
│       │   └── Seeding/             # Dữ liệu khởi tạo, tách dữ liệu demo
│       ├── Authorization/
│       │   ├── Policies/            # Vai trò và tên policy
│       │   ├── Requirements/        # Yêu cầu quyền theo resource
│       │   ├── Handlers/            # Kiểm tra sở hữu/phân công/thành viên
│       │   └── Services/            # Truy vấn phạm vi truy cập dùng chung
│       ├── Infrastructure/
│       │   ├── Identity/            # ApplicationUser, tích hợp Identity
│       │   ├── Storage/             # Lưu/đọc tệp vật lý, không quyết định quyền
│       │   ├── Auditing/            # Ghi lịch sử nghiệp vụ vào SQL Server
│       │   ├── Idempotency/         # Lưu khóa và kết quả chống ghi trùng
│       │   ├── Time/                # Chuyển UTC/múi giờ Việt Nam khi cần
│       │   ├── Logging/             # Cấu hình Serilog, redaction
│       │   ├── HealthChecks/        # Liveness/readiness
│       │   ├── ErrorHandling/       # Xử lý lỗi HTTP, chuyển lỗi an toàn
│       │   └── Security/            # Data Protection và cấu hình bảo mật
│       ├── Configuration/
│       │   ├── Options/             # Cấu hình kiểu dữ liệu rõ ràng
│       │   └── Validation/          # Options Validation
│       ├── Endpoints/
│       │   ├── Files/               # HTTP tải tệp có kiểm tra quyền
│       │   └── Health/              # Endpoint live/ready
│       ├── DependencyInjection/     # Nhóm đăng ký DI, giữ Program.cs gọn
│       └── wwwroot/
│           ├── css/                 # Style toàn ứng dụng, design tokens
│           ├── fonts/               # Font hỗ trợ tiếng Việt
│           ├── images/              # Chỉ ảnh công khai
│           └── js/                  # Chỉ khi cần JS interop
├── tests/
│   ├── NhaTro.UnitTests/
│   │   ├── Features/                # Test quy tắc nghiệp vụ theo feature
│   │   ├── Common/                  # Test utils và extension
│   │   └── TestSupport/             # Fake TimeProvider, test data, helper
│   ├── NhaTro.IntegrationTests/
│   │   ├── Features/                # SQL Server, transaction, concurrency
│   │   ├── Authorization/           # Test xuyên user/tòa nhà/hợp đồng
│   │   ├── Endpoints/               # Auth HTTP, tải tệp, health
│   │   └── TestSupport/             # WebApplicationFactory, DB cô lập
│   └── NhaTro.E2ETests/
│       ├── Features/                # Playwright .NET theo luồng chức năng
│       └── TestSupport/             # Browser fixture, dữ liệu và page helpers
├── scripts/
│   ├── database/                    # Migrate, backup/restore có kiểm soát
│   └── deployment/                  # Publish và triển khai IIS
└── docs/
    ├── thiet_ke_he_thong.md
    ├── architecture/               # Cấu trúc, quyết định kiến trúc
    ├── tech-stack/                 # Công nghệ sử dụng
    ├── design/                     # Database, UI và trạng thái nghiệp vụ
    ├── api/                        # HTTP contract đăng nhập/tệp/health
    ├── image/                      # Sơ đồ hiện hành
    ├── archive/                    # Bản cũ, không dùng làm đặc tả hiện hành
    └── operations/                 # Hướng dẫn triển khai, phục hồi, xử lý sự cố
```

Các file sơ đồ hiện có trong `docs/` được giữ nguyên; cây trên không yêu cầu di chuyển hoặc xóa tài liệu cũ. Các project test không làm thay đổi việc frontend và backend cùng một project web.

### Ranh giới trách nhiệm cần thống nhất

- `Components/Account/`: giao diện và luồng HTTP đăng nhập/đăng xuất của Identity; không chứa chức năng quản trị tài khoản.
- `Features/Accounts/`: quản trị tài khoản, vai trò và phân công Quản lý theo nghiệp vụ P1.
- `Infrastructure/Identity/`: kiểu `ApplicationUser` và tích hợp kỹ thuật Identity; không chứa màn hình hoặc quy tắc phân công nghiệp vụ.
- `Features/Payments/`: khách thuê gửi yêu cầu, thông tin/minh chứng thanh toán; Quản lý kiểm tra và xác nhận thu tiền. **Gửi yêu cầu/minh chứng không đồng nghĩa hóa đơn đã thanh toán**. Đối soát là kiểm tra thủ công tiền mặt/chuyển khoản, không ngụ ý tích hợp ngân hàng.
- Cây folder là **cách tổ chức mã nguồn dự kiến**, không tự xác nhận thêm chức năng. `Properties/` tổ chức dữ liệu nhà trọ/tòa nhà; `Deposits/` vẫn thuộc P4, không phải tiến trình mới. Chi tiết thu/hoàn/xử lý cọc và chi phí bảo trì phải theo đặc tả đã được thống nhất; không tự bổ sung chức năng chỉ vì có folder.

## 3. Cấu trúc bên trong mỗi chức năng

Ví dụ chức năng hóa đơn:

```text
Features/Invoices/
├── Pages/
│   ├── InvoiceList.razor
│   ├── InvoiceList.razor.cs         # Code-behind nếu component dài
│   ├── InvoiceList.razor.css        # CSS isolation khi cần
│   └── InvoiceDetail.razor
├── Components/                     # Chỉ dùng trong chức năng hóa đơn
│   ├── InvoiceForm.razor
│   └── InvoiceItemsTable.razor
├── Entities/                       # Invoice, InvoiceItem
├── DTOs/                           # Request, response, filter; không bind entity
├── Services/                       # InvoiceService: điều phối nghiệp vụ
├── Rules/                          # Tính tiền, phát hành, chuyển trạng thái
├── Mapping/                        # Mapping thủ công nếu cần tách riêng
└── Enums/                          # Trạng thái hóa đơn
```

- Áp dụng cùng cách chia cho các feature khác, nhưng **không bắt buộc feature nào cũng có đủ thư mục**.
- Component có `@page` vẫn được Blazor Router tìm thấy khi đặt trong `Features/`; bảo đảm namespace và `_Imports.razor` phù hợp.
- Entity ở feature; cấu hình EF tương ứng ở `Data/Configurations/Invoices/`. Một `AppDbContext` quản lý toàn bộ database.
- Interface chỉ thêm khi có ranh giới cần thay thế hoặc nhiều implementation, không tạo một interface cho mọi class theo máy móc.
- `Utilities/` là nghiệp vụ **điện nước/dịch vụ**; `Common/Utils/` là tiện ích lập trình, không trộn hai loại này.

## 4. Quy tắc tái sử dụng và phụ thuộc

| Nơi đặt | Được chứa | Không được chứa |
| --- | --- | --- |
| `Common/Utils/` | Hàm thuần định dạng, chuẩn hóa dữ liệu dùng chung, dễ unit test | DbContext, HTTP, đọc tệp, trạng thái người đăng nhập, tính công nợ |
| `Common/Extensions/` | Extension có tên rõ và trách nhiệm nhỏ | Lệnh ghi database hoặc thao tác tài chính bị che trong extension |
| `Components/Shared/` | Component UI dùng cho nhiều feature | Truy vấn SQL trực tiếp hoặc quyết định quyền nghiệp vụ |
| `Features/<Tên>/Rules/` | Logic nghiệp vụ ưu tiên thuần như tính hóa đơn, kiểm tra điều kiện duyệt; nhận dữ liệu từ Service | Truy cập database trực tiếp, lưu tệp hoặc cấu hình logger |
| `Infrastructure/` | Adapter kỹ thuật, I/O, cơ chế dùng chung | Quyết định khách nào được xem CCCD, khoản thu nào hợp lệ |
| `Authorization/` | Kiểm tra quyền và phạm vi dữ liệu dùng chung | Chỉ kiểm tra UI mà bỏ kiểm tra trong Service |
| `tests/*/TestSupport/` | Fixture, fake và dữ liệu test tái sử dụng | Helper test được đưa vào project chạy thật |

**Hướng phụ thuộc:**

```text
Feature UI → Feature Service → Rules + Authorization + Data/Infrastructure
Shared UI → Common
Data → Entity cần ánh xạ EF + Configuration
Infrastructure → Kiểu dữ liệu cần cho tích hợp + Common/Configuration
Common → .NET và các kiểu dùng chung; không tham chiếu ngược Feature
```

- UI không truy cập DbContext trực tiếp. **Service đầu vào của một thao tác** tạo và giải phóng DbContext bằng `IDbContextFactory`, quản lý transaction khi cần và chịu trách nhiệm `SaveChangesAsync`/commit.
- Các bước nội bộ tham gia cùng thao tác nhận DbContext của Service đầu vào qua phương thức nội bộ hoặc làm việc trên entity đã được tải; không tự tạo context, tự commit hoặc giải phóng context được truyền vào. Không lưu context trong field dùng lâu dài theo circuit; không dùng đồng thời một context trong các tác vụ song song.
- `Data/` tham chiếu entity để ánh xạ EF. `Infrastructure/` chỉ dùng kiểu dữ liệu cần thiết cho tích hợp, không phụ thuộc component hoặc DTO dành riêng cho form. Service chuyển dữ liệu form sang kiểu phù hợp trước khi gọi tích hợp.
- `Rules/` nhận dữ liệu đã được Service chuẩn bị, ưu tiên hàm thuần; việc truy vấn, kiểm tra quyền và điều phối ghi dữ liệu thuộc Service.
- Feature có thể gọi Service của feature khác khi có nghiệp vụ thực, nhưng không tạo vòng phụ thuộc. Ví dụ lập hóa đơn đọc hợp đồng/dịch vụ, không gọi ngược Service thanh toán để lập hóa đơn.
- Nếu một transaction thay đổi nhiều feature, một Service điều phối dùng **cùng DbContext/transaction**; không gọi chuỗi Service tự tạo context rồi cho rằng tất cả đều nguyên tử.
- Chống ghi trùng, audit và thay đổi thu tiền cần được ghi cùng transaction khi yêu cầu nguyên tử; không dùng helper tự ghi database ngoài transaction đó.
- Ví dụ xác nhận thanh toán: `PaymentService` đầu vào tạo context/transaction, kiểm tra quyền và khóa chống trùng, ghi nhận khoản thu, cập nhật trạng thái hóa đơn theo số tiền đã xác nhận và ghi audit; tất cả dùng cùng context. Chỉ trả thành công khi commit hoàn tất; lỗi phải rollback. Không cần generic repository hoặc framework Unit of Work.
- Không chuyển mọi thứ vào `Utils`. Logic dùng một nơi giữ tại feature; chỉ đưa ra dùng chung khi trách nhiệm và nhu cầu tái sử dụng rõ ràng.
- Tiện ích không giữ state theo circuit trong biến static; thời gian hiện tại lấy qua `TimeProvider`, không tạo một global clock riêng.

## 5. Thành phần có điều kiện theo kế hoạch công nghệ

| Yêu cầu được xác nhận | Vị trí |
| --- | --- |
| Chạy nghiệp vụ định kỳ | `BackgroundJobs/` chứa hosted service; gọi Service nghiệp vụ, lưu kỳ xử lý bền vững, không chỉ dùng timer trong bộ nhớ |
| Xuất Excel | `Infrastructure/Exports/Excel/` cho ClosedXML; chọn dữ liệu và kiểm tra quyền tại feature |
| Tạo PDF | `Infrastructure/Exports/Pdf/` cho QuestPDF; template theo hợp đồng/hóa đơn/phiếu thu, kiểm tra license |

Không tạo các folder này hoặc cài package nếu chưa có yêu cầu tương ứng. Đây là điều kiện chức năng, không phải chia giai đoạn phát triển.

## 6. Dữ liệu vận hành nằm ngoài repository và thư mục publish

Ví dụ đường dẫn trên máy chủ; cấu hình thực tế qua Options và biến môi trường:

```text
D:/NhaTroData/
├── uploads/
│   ├── identity-documents/          # CCCD, giấy tờ; không public
│   ├── payment-evidence/            # Biên lai; không public
│   └── maintenance/                 # Ảnh sự cố, vẫn kiểm tra quyền
├── logs/                            # Log kỹ thuật, có giới hạn lưu giữ
└── data-protection-keys/            # Lưu bền vững, bảo vệ khóa và ACL

Kho backup riêng được bảo vệ:
├── database/
├── uploads/
└── keys/                            # Có phương án phục hồi khả năng giải mã
```

- Không commit uploads, log, backup, keys hoặc secret; không đặt chúng trong `wwwroot`.
- File storage không có quyền thực thi; tên tệp do server sinh, metadata và liên kết resource lưu trong SQL Server.
- Backup cần bản sao độc lập với máy chạy ứng dụng; tạo folder `backups` trên cùng ổ không đủ để bảo vệ khi mất máy/ổ đĩa.
- Truy cập tệp phải qua Service kiểm quyền và endpoint tải tệp; không đưa đường dẫn vật lý lên giao diện.

## 7. Quy ước đặt tên

- Folder, namespace và mã nguồn dùng tiếng Anh, PascalCase; nội dung giao diện dùng tiếng Việt.
- Đặt tên cụ thể: `PaymentService`, `InvoiceCalculation`, `StorageOptions`; tránh `Helper`, `Manager`, `Utils` cho class chứa nhiều trách nhiệm.
- Tên namespace theo đường dẫn, ví dụ `NhaTro.Web.Features.Invoices.Services`.
- `_Imports.razor` dùng tại phạm vi phù hợp cho component trong `Components/` và `Features/`, không import tất cả feature vào mọi component.
- CSS riêng đặt cạnh component (`.razor.css`); CSS toàn hệ thống ở `wwwroot/css/`.
- Không lưu `bin/`, `obj/`, `.vs/`, kết quả test hoặc bản publish trong Git.

> Cấu trúc này giữ một ứng dụng thống nhất, tách rõ từng chức năng và phần tái sử dụng, không bổ sung kiến trúc phân tán hay framework ngoài kế hoạch công nghệ.
