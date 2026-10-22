# Cấu trúc thư mục / Folder structure — Web vận hành nhà trọ

**English name:** Boarding House Operations Web

**GitHub:** https://github.com/TinhSsc/boarding-house-management-web.git

## 1. Nguyên tắc / Principles

- Một project **Blazor Web App** chứa frontend và backend; Visual Studio mở `NhaTro/NhaTro.slnx`.
- Chia theo chức năng / **Feature-oriented**: UI, entity, DTO, service và rule nằm trong cùng feature.
- Không chuyển repo sang `src/` và `tests/`, không đổi tên project/namespace chỉ để khớp một cây mẫu.
- Cây **hiện tại** và cây **khung phát triển** tách riêng. Theo yêu cầu INIT, các folder trong cây mở rộng đã được tạo và giữ bằng `.gitkeep`; folder có sẵn không có nghĩa là tính năng đã triển khai.
- EF Core tập trung ở `Data/`; giữ `ApplicationDbContext` và `ApplicationUser` hiện có. Không thêm generic repository hoặc framework mới.
- Tham chiếu: [công nghệ](../tech-stack/cong_nghe_su_dung.md), [nghiệp vụ P1–P8](../thiet_ke_he_thong.md), [UI](../design/phong_cach_giao_dien.md), [phân công](../task/cong_viec_theo_tuan.md).

## 2. Cây hiện tại / Current repository tree

Các nhánh dưới đây đã có; cây lược bỏ `bin/`, `obj/`, `.vs/`, file cá nhân và chi tiết template Identity.

```text
LTW/
├── .gitignore
├── .github/
│   └── workflows/
│       └── ci.yml                       # Windows: restore/build/test .NET 10
├── NhaTro/
│   ├── NhaTro.slnx                      # Solution Visual Studio
│   ├── Web/
│   │   ├── Web.csproj                   # Một web project; namespace Web
│   │   ├── Program.cs                   # DI, Identity, EF, MudBlazor, Serilog
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── Components/
│   │   │   ├── App.razor
│   │   │   ├── Routes.razor
│   │   │   ├── _Imports.razor
│   │   │   ├── Layout/                  # MainLayout, ReconnectModal
│   │   │   ├── Pages/                   # Home: giới thiệu INIT; trang lỗi
│   │   │   │   ├── Home.razor
│   │   │   │   └── Home.razor.css
│   │   │   └── Account/                 # Template Identity + HTTP auth
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── ApplicationUser.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── Properties/
│   │   │   │   ├── Rooms/
│   │   │   │   └── Tenants/
│   │   │   └── Migrations/              # Identity, CoreSchema, snapshot
│   │   ├── Features/
│   │   │   ├── Properties/
│   │   │   │   ├── Entities/Property.cs
│   │   │   │   ├── DTOs/                # PropertyFilter/ListItem/Detail/Save
│   │   │   │   ├── Mapping/PropertyMapping.cs
│   │   │   │   ├── PropertyCaller.cs, PropertyResult.cs, PropertyErrorCodes.cs
│   │   │   │   ├── IPropertyService.cs, PropertyService.cs   # VHT-7 đợt 1C
│   │   │   │   └── PropertyScope.cs
│   │   │   ├── Rooms/
│   │   │   │   ├── Entities/Room.cs
│   │   │   │   ├── Enums/RoomStatus.cs
│   │   │   │   ├── DTOs/                # Filter/ListItem/Detail/Save/Option/OptionRequest
│   │   │   │   ├── Mapping/RoomMapping.cs
│   │   │   │   ├── IRoomService.cs, RoomService.cs          # VHT-7 đợt 1C
│   │   │   │   ├── IRoomChangePolicy.cs                     # điểm gọi VHT-94
│   │   │   │   ├── IRoomUsageChecker.cs                     # điểm gọi VHT-98
│   │   │   │   └── RoomScope.cs, RoomResult.cs, RoomErrorCodes.cs
│   │   │   └── Tenants/
│   │   │       └── Entities/Tenant.cs
│   │   └── wwwroot/                    # app.css và static assets template
│   ├── NhaTro.UnitTests/                # xUnit: scaffold
│   ├── NhaTro.IntegrationTests/         # xUnit + WebApplicationFactory
│   └── NhaTro.E2ETests/                 # xUnit + Playwright .NET
└── docs/
    ├── thiet_ke_he_thong.md
    ├── thiet_ke_he_thong.docx
    ├── bieudochot.excalidraw
    ├── architecture/                   # Tài liệu tổ chức code
    │   ├── README.md
    │   ├── cau_truc_thu_muc.md          # Cây và quy tắc hiện hành
    │   └── cau_truc_thu_muc.docx        # Bản Word; cần đồng bộ khi xuất
    ├── tech-stack/
    ├── design/
    ├── task/
    ├── api/
    ├── image/
    └── archive/
```

**Current status:** The database core and Identity scaffold exist. The home page is a simple bilingual introduction, not a dashboard. Test projects are scaffolds, not proof of full feature coverage. P2 (nhà trọ/phòng) đã có DTO + service SQL ở `Features/Properties/` và `Features/Rooms/` (VHT-7), chưa có trang UI.

**VHT-94 (đã triển khai - nền công tắc Quản lý):** `Features/Settings/` gồm `ManagerToggleCatalog` (21 mã SW-01…SW-16 + SW-15a…e), `Enums/`, `Entities/ManagerToggle`, `Services/ManagerToggleService` (đọc/ghi SQL + guard Chủ trọ), `Services/ManagerTogglePolicy` (mode → Allowed/RequiresApproval/Denied, cài thêm `IRoomChangePolicy` để P2 dùng lại), `DTOs/`; `Data/Configurations/Settings/ManagerToggleConfiguration.cs` + migration `AddManagerToggles` (seed 21 dòng, check constraint SW-15). Công tắc lưu SQL, **không** dùng `IOptions`/appsettings. UI cấu hình vẫn là NEW-04 (VHT-97), hàng đợi duyệt là VHT-96.

## 3. Khung thư mục đã tạo / Created folder scaffold

Các folder dưới **`NhaTro/Web/`** đã được tạo; folder rỗng giữ bằng `.gitkeep`, không tạo project mới hoặc class mẫu. Các chú thích chức năng vẫn là **kế hoạch triển khai**, không phải nghiệp vụ đã hoàn thành.

```text
Web/
├── Components/
│   ├── Layout/                         # Phú: sidebar/topbar sau INIT
│   └── Shared/
│       ├── Forms/                      # Phúc: input/validation
│       ├── Tables/                     # Phúc: filter/paging
│       ├── Dialogs/                    # Phúc: confirmation
│       ├── Feedback/                   # Loading/empty/error
│       ├── Theme/                      # Phú: tokens/MudTheme/Lucide
│       └── Localization/               # Phú: language selector
├── Features/
│   ├── Accounts/                       # P1 — Tính
│   ├── Properties/                     # P2 — Phúc, mở rộng entity đã có
│   ├── Rooms/                          # P2 — Phúc, mở rộng entity đã có
│   ├── Tenants/                        # P3 — Phúc, mở rộng entity đã có
│   ├── Contracts/                      # P4 — Mai
│   ├── Deposits/                       # P4 — Mai; chỉ theo đặc tả đã chốt
│   ├── Utilities/                      # P5 — Mai backend, Phú UI
│   ├── Invoices/                       # P6 — Phát
│   ├── Payments/                       # P6 — Phát
│   ├── Maintenance/                    # P7 — Phú
│   ├── Reports/                        # P8 — Phú; nguồn từ mỗi người
│   └── Dashboard/                      # Phú; số liệu theo quyền
├── Common/
│   ├── Constants/
│   ├── Models/                         # Kết quả/phân trang dùng chung
│   ├── Exceptions/
│   ├── Extensions/
│   ├── Utils/                          # Mai: hàm thuần ngày/tiền
│   └── Localization/                   # SharedResource.cs (resource marker)
├── Resources/                          # Bản dịch .resx vi-VN/en-US
├── Data/
│   ├── Configurations/                 # Bổ sung theo feature
│   ├── Migrations/                     # Migrations cộng thêm, theo lượt
│   └── Seeding/                        # Tách dữ liệu nền và dữ liệu demo
├── Authorization/
│   ├── Policies/
│   ├── Requirements/
│   ├── Handlers/
│   └── Services/                       # Tính: phạm vi truy cập
├── Infrastructure/
│   ├── Storage/                        # Phúc: I/O tệp ngoài wwwroot
│   ├── Auditing/                       # Audit theo nhu cầu nghiệp vụ
│   ├── Idempotency/                    # Chống ghi giao dịch trùng
│   ├── Time/                           # UTC ↔ Việt Nam khi cần
│   ├── Logging/                        # Tách Serilog khi cần
│   ├── HealthChecks/
│   ├── ErrorHandling/
│   └── Security/                       # Keys, Data Protection
├── Configuration/
│   ├── Options/
│   └── Validation/
├── Endpoints/
│   ├── Files/                          # Tải tệp qua kiểm quyền
│   ├── Health/
│   └── Localization/                   # Đặt culture cookie khi triển khai
├── DependencyInjection/                # Chỉ tách khi Program.cs cần gọn
└── wwwroot/
    ├── app.css                         # Giữ file hiện có
    ├── css/                            # Tokens khi theme thật được làm
    ├── fonts/
    ├── images/                         # Chỉ ảnh công khai
    └── js/                             # Chỉ khi cần interop
```

- Giữ `ApplicationUser` ở `Data/`, không tạo bản thứ hai tại `Infrastructure/Identity/`.
- `Components/Account/` là luồng đăng nhập Identity; `Features/Accounts/` là quản trị tài khoản/role/phân công P1.
- `Utilities/` là điện nước/dịch vụ; `Common/Utils/` là tiện ích lập trình.
- `Deposits/` vẫn thuộc P4. Có folder không có nghĩa là tự bổ sung nghiệp vụ cọc.

**VHT-13 (đã triển khai — storage backend dùng chung):** các folder trên không còn rỗng ở phần storage. Code thật gồm `Infrastructure/Storage/` (`IFileStorage`, `LocalFileStorage`, tên tệp do server sinh, chặn path traversal), `Features/Storage/` (`Rules/` quyền + loại/kích thước + nhận dạng nội dung, `Services/FileService`, `Entities/StoredFile`, `Mapping/`, `DTOs/`), `Endpoints/Files/FileEndpoints.cs` (`POST/GET/DELETE /api/files`), `Configuration/Options/StorageOptions.cs` + `Configuration/Validation/StorageOptionsValidator.cs`, `Infrastructure/ErrorHandling/FileUploadExceptionMiddleware.cs`, `Data/Configurations/Storage/StoredFileConfiguration.cs` và migration `AddStorageStoredFiles`. `.gitkeep` tương ứng đã bỏ.

## 4. Một chức năng / Feature template

```text
Features/Invoices/
├── Pages/                              # InvoiceList.razor, InvoiceDetail.razor
├── Components/                         # Chỉ dùng trong feature
├── Entities/                           # Invoice, InvoiceItem khi chốt schema
├── DTOs/                               # Request/response/filter; không bind entity
├── Services/                           # InvoiceService điều phối thao tác
├── Rules/                              # Quy tắc tính tiền/chuyển trạng thái
├── Mapping/                            # Mapping thủ công nếu cần tách
└── Enums/

Data/Configurations/Invoices/           # Fluent API của entity hóa đơn
```

- Khung `Invoices/` đã tạo đủ các folder trong ví dụ; các feature khác giữ folder nền và mở rộng theo nhu cầu, không bắt buộc có đủ mọi nhánh. `.razor.cs` và `.razor.css` đặt cạnh component khi cần.
- Component `@page` trong `Features/` được Router tìm thấy; thêm `_Imports.razor` ở phạm vi thích hợp, không import mọi feature toàn cục.
- Namespace ví dụ: **`Web.Features.Invoices.Services`**, không phải `NhaTro.Web.*`.
- Interface chỉ thêm khi cần thay thế implementation; không tạo một interface cho mọi class.

## 5. Phụ thuộc và transaction / Dependencies and transactions

```text
Feature UI → Feature Service → Rules + Authorization + Data/Infrastructure
Shared UI → Common + Resources
Data → Feature entities + EF configurations
Infrastructure → Common/Configuration + kiểu dữ liệu tích hợp cần thiết
Common → .NET; không phụ thuộc ngược Feature hoặc UI
```

- UI/shared component không truy vấn SQL hoặc quyết định quyền thay backend.
- Service đầu vào tạo context ngắn hạn qua **`IDbContextFactory<ApplicationDbContext>`**, kiểm quyền, điều phối transaction và chịu trách nhiệm `SaveChangesAsync`/commit.
- Bước nội bộ dùng **cùng context/transaction** hoặc entity đã tải; không tự tạo context/commit/dispose context được truyền vào. Không giữ context theo circuit, không dùng cùng context song song.
- Rules ưu tiên hàm thuần; service chuẩn bị dữ liệu và thực hiện I/O. Feature có thể dùng service feature khác nhưng không tạo vòng phụ thuộc.
- Xác nhận thu tiền: quyền + chống trùng + khoản thu + cập nhật hóa đơn + audit dùng cùng transaction khi yêu cầu nguyên tử; lỗi rollback. **Gửi minh chứng không đồng nghĩa đã thanh toán**.
- Không dồn nghiệp vụ vào Utils; Utils không chứa DbContext, HTTP, storage hoặc logic công nợ. Thời gian trong nghiệp vụ dùng `TimeProvider` khi nhóm triển khai.

## 6. Hai ngôn ngữ / Localization

- Hỗ trợ **`vi-VN` / `en-US`**, mặc định tiếng Việt. Folder, class, namespace và resource key bằng tiếng Anh.
- INIT chỉ đổi nội dung giới thiệu tại `Home.razor`, chưa lưu lựa chọn qua trang/tải lại và chưa dịch Identity.
- Phú thiết lập `AddLocalization`, `RequestLocalization`, resource và selector chung; cookie culture được đặt qua HTTP endpoint để dùng cho lần tải trang/circuit mới. Không đổi global culture của mọi người dùng.
- Shared resource marker tại `Common/Localization/SharedResource.cs`; dùng `IStringLocalizer<SharedResource>` với `ResourcesPath = "Resources"`.
- Ví dụ đúng theo namespace marker: `Resources/Common/Localization/SharedResource.vi-VN.resx` và `SharedResource.en-US.resx`. Resource feature dùng marker/namespace riêng khi cần; hai ngôn ngữ phải có cùng key.
- Mỗi người dịch UI/validation/trạng thái phần mình. Tính phụ trách Identity; Mai thống nhất ngày/tiền; Phát kiểm song ngữ; Phúc cập nhật hướng dẫn.
- Không dịch nội dung nhập, enum lưu SQL hoặc tên riêng. Chuyển ngôn ngữ không đổi VND, quy tắc tính, quyền hoặc dữ liệu.
- Dùng định dạng chung của dự án: `dd/MM/yyyy`, `HH:mm`, số Việt Nam và VND; nếu muốn định dạng theo culture thì nhóm phải chốt lại, không đổi ngầm khi chọn English.

## 7. Test, tài liệu và scripts / Tests, docs and scripts

```text
NhaTro/
├── NhaTro.UnitTests/
│   ├── Features/                       # Rules theo chức năng
│   ├── Common/                         # Utils/formatting
│   └── TestSupport/                    # Fake clock/data/helper
├── NhaTro.IntegrationTests/
│   ├── Features/                       # SQL/transaction/concurrency
│   ├── Authorization/
│   ├── Endpoints/                      # HTTP auth/files/health/culture
│   └── TestSupport/                    # Factory + database test cô lập
└── NhaTro.E2ETests/
    ├── Features/                       # Playwright, gồm Việt/Anh
    └── TestSupport/                    # Browser fixtures/page helpers

docs/design/database/                   # .gitkeep; Tính bổ sung ERD theo migrations
docs/operations/                        # .gitkeep; hướng dẫn IIS/restore bổ sung sau
scripts/database/                       # .gitkeep; chưa có script thật
scripts/deployment/                     # .gitkeep; chưa có script thật
README.md                               # Phúc: setup/use bằng Việt/Anh, bổ sung sau
```

Các folder trong mục này đã được tạo và giữ bằng `.gitkeep`; riêng `README.md` ở root vẫn chưa tạo. Chưa có test/fixture hoặc script nghiệp vụ trong các folder mới. Solution folder trong Visual Studio không bắt buộc trùng folder vật lý trên ổ đĩa. Phú ghép PR/CI, Tính hỗ trợ; không tự di chuyển project test.

**`.gitkeep`:** file placeholder để Git giữ folder rỗng, không phải cấu hình hoặc mã chạy. Khi folder có file thật có thể bỏ `.gitkeep`; không xóa file của người khác chỉ để làm sạch khung.

## 8. Dữ liệu ngoài Git / Runtime data outside Git

- Không commit secret, connection string chứa mật khẩu, uploads, log, backup hoặc Data Protection keys.
- SQL và User Secrets của mỗi máy không chia sẻ qua Git. Thành viên cần cấu hình kết nối rồi áp dụng migrations; không tạo lại migration INIT.
- Giấy tờ/minh chứng/ảnh sự cố nằm ngoài `wwwroot` và thư mục publish; đường dẫn cấu hình qua Options. Server sinh tên tệp, metadata lưu SQL, tải tệp phải kiểm quyền.
- Log local hiện ở `%LOCALAPPDATA%/NhaTro/logs`; không mô tả đường dẫn máy chủ dự kiến như cấu hình đã có.
- Khi triển khai IIS: thống nhất thư mục uploads/logs/keys, ACL và backup độc lập. Backup cùng ổ không đủ bảo vệ mất máy; phải thử restore DB/tệp/keys.
- Không đưa `bin/`, `obj/`, `.vs/`, `*.csproj.user`, kết quả test hay publish vào Git.

## 9. Phần có điều kiện / Optional scope

Không tạo folder hoặc cài package cho **PDF, background jobs, payment gateway** nếu chưa xác nhận yêu cầu. Excel (ClosedXML) đã được chốt cho “Xuất báo cáo” P8 (D8): khi triển khai VHT-103 thêm `Infrastructure/Exports/` phù hợp.

Dự kiến bổ sung theo ma trận quyền (chỉ tạo khi làm task tương ứng, không tạo class rỗng trước):
- `Features/Accounts/` mở rộng: quản trị Admin, vòng đời tài khoản (VHT-95).
- Công tắc và phê duyệt: `Services/Approvals`, `Services/Settings` (VHT-94, VHT-96, VHT-97).
- Thùng rác: xóa mềm trong `Rooms`, `Tenants` (VHT-98, VHT-99).
- Biểu giá và lịch sử chỉ số: `Data/Tariffs`, bảng lịch sử sửa chỉ số (VHT-100, VHT-102).
- Xuất Excel: `Infrastructure/Exports/` (VHT-103); nhật ký thao tác (VHT-104, nếu D16 chốt).

> Một ứng dụng thống nhất / One web application. INIT cung cấp folder scaffold; nhóm bổ sung file thật theo từng chức năng, không tạo class rỗng hoặc nghiệp vụ ngoài phạm vi.
