# NhaTro

**Web vận hành nhà trọ** · Boarding House Management Web

Ứng dụng Blazor Web App giúp Chủ trọ, Quản lý và Khách thuê quản lý phòng, khách thuê, hợp đồng, điện nước, hóa đơn, thanh toán và sự cố.

> **Trạng thái: INIT.** Repo hiện có khung kỹ thuật và schema lõi; chưa có chức năng nghiệp vụ hoàn chỉnh.
> Các lệnh trong tài liệu này lấy từ cấu hình của repo và **chưa được xác nhận trên máy sạch**. Vui lòng hoàn thành [danh sách xác nhận](#xác-nhận-setup) ở cuối trang.

**Ngôn ngữ:** [Tiếng Việt](#tiếng-việt) · [English](#english)

---

## Tiếng Việt

### Mục lục

1. [Hiện trạng](#hiện-trạng)
2. [Công nghệ](#công-nghệ)
3. [Bắt đầu nhanh](#bắt-đầu-nhanh)
4. [Yêu cầu môi trường](#yêu-cầu-môi-trường)
5. [Cấu hình và secrets](#cấu-hình-và-secrets)
6. [Cơ sở dữ liệu](#cơ-sở-dữ-liệu)
7. [Chạy ứng dụng](#chạy-ứng-dụng)
8. [Kiểm thử và CI](#kiểm-thử-và-ci)
9. [Cấu trúc thư mục](#cấu-trúc-thư-mục)
10. [Xử lý lỗi thường gặp](#xử-lý-lỗi-thường-gặp)
11. [Tài liệu và quy tắc làm việc](#tài-liệu-và-quy-tắc-làm-việc)
12. [Xác nhận setup](#xác-nhận-setup)

### Hiện trạng

| Đã có | Chưa có |
| --- | --- |
| Blazor Web App, .NET 10, Interactive Server | Vai trò Chủ trọ, Quản lý, Khách thuê; seed vai trò; policy. Chưa trang nào có `[Authorize]` |
| ASP.NET Core Identity (cookie) với bộ trang tài khoản scaffold mặc định | Gửi email thật (đang dùng `IdentityNoOpEmailSender`) |
| EF Core 10 và SQL Server, 2 migration: `CreateIdentitySchema`, `CoreSchema` | Layout sidebar và topbar, theme theo [quy chuẩn giao diện](docs/design/phong_cach_giao_dien.md). Trang `/` chỉ là trang thử MudBlazor |
| Entity và cấu hình EF cho `Property`, `Room`, `Tenant` (xóa mềm, unique index có filter) | Service, DTO, giao diện cho nhà trọ, phòng, khách thuê |
| Serilog ghi ra Console và file | Hợp đồng, cọc, điện nước, hóa đơn, thanh toán, sự cố, báo cáo (chưa có bảng, chưa có code) |
| 3 project test xUnit, mỗi project 1 test rỗng | Test nghiệp vụ, lưu trữ tệp, health check, Options validation |
| CI GitHub Actions: restore, build, test | `global.json`, `.editorconfig`, tool manifest cho `dotnet-ef` |

Cột "Chưa có" **chưa được triển khai**. Các tài liệu trong `docs/` mô tả thiết kế dự kiến, không phải mã nguồn đã chạy.

### Công nghệ

| Thành phần | Công nghệ |
| --- | --- |
| Nền tảng | .NET 10 (`net10.0`) |
| Web | Blazor Web App, Interactive Server |
| Giao diện | MudBlazor 9.11.0 |
| Dữ liệu | EF Core 10.0.12, SQL Server |
| Đăng nhập | ASP.NET Core Identity, cookie |
| Logging | Serilog (Console, File) |
| Kiểm thử | xUnit, `Mvc.Testing`, Playwright |
| CI | GitHub Actions (Windows) |

### Bắt đầu nhanh

Dành cho máy **Windows có .NET SDK 10 và SQL Server LocalDB**. Chạy từ thư mục gốc repo.

```bash
git clone https://github.com/utc2-programming-project/LTW-boarding_house_management_web.git
cd LTW-boarding_house_management_web

dotnet restore NhaTro/NhaTro.slnx
dotnet build NhaTro/NhaTro.slnx --no-restore --nologo

dotnet tool install --global dotnet-ef --version 10.*
dotnet ef database update --project NhaTro/Web

dotnet dev-certs https --trust
dotnet run --project NhaTro/Web --launch-profile https
```

Mở https://localhost:7256, bấm nút **Kiểm tra**. Thông báo "MudBlazor hoạt động!" nghĩa là giao diện chạy được. Nếu dùng SQL Server khác hoặc hệ điều hành khác, xem [Cấu hình và secrets](#cấu-hình-và-secrets).

### Yêu cầu môi trường

| Công cụ | Phiên bản | Ghi chú |
| --- | --- | --- |
| Git | Bản gần đây | |
| .NET SDK | **10.x** | Project nhắm `net10.0`, solution dùng định dạng `.slnx`. Kiểm tra bằng `dotnet --version` |
| SQL Server | LocalDB hoặc SQL Server / Express | LocalDB đi kèm Visual Studio và chỉ có trên Windows; cấu hình mặc định dùng LocalDB |
| Visual Studio | Bản hỗ trợ .NET 10 | Khuyến nghị theo tài liệu dự án; có thể chỉ dùng CLI |
| `dotnet-ef` | **10.x** | Cài một lần: `dotnet tool install --global dotnet-ef --version 10.*` |
| SSMS | Tùy chọn | Xem dữ liệu và chạy truy vấn |

CI chạy trên Windows. Dự án chưa được kiểm chứng trên macOS hoặc Linux; nếu dùng các hệ điều hành này, cần một SQL Server riêng và cấu hình bằng User Secrets.

Với Visual Studio: mở `NhaTro/NhaTro.slnx` và đặt `Web` làm startup project.

### Cấu hình và secrets

`NhaTro/Web/appsettings.json` đã có chuỗi kết nối mặc định tới LocalDB bằng đăng nhập Windows, không chứa mật khẩu:

```text
Server=(localdb)\mssqllocaldb;Database=aspnet-Web-fe3a4465-6d62-438e-9626-5895e0d57ca0;Trusted_Connection=True;MultipleActiveResultSets=true
```

Trên Windows có LocalDB, bạn có thể bỏ qua bước này. Với SQL Server khác hoặc khi cần tài khoản và mật khẩu, đặt chuỗi kết nối bằng **User Secrets** (lưu ngoài repo, riêng từng máy):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<MAY_CHU>;Database=<TEN_DB>;User Id=<TAI_KHOAN>;Password=<MAT_KHAU>;TrustServerCertificate=True;MultipleActiveResultSets=true" --project NhaTro/Web
dotnet user-secrets list --project NhaTro/Web
```

Thay các giá trị `<...>` bằng giá trị của máy bạn. Trong Visual Studio: chuột phải project `Web`, chọn **Manage User Secrets**.

| Quy tắc | Chi tiết |
| --- | --- |
| Không commit secret | Mật khẩu, chuỗi kết nối có mật khẩu, `secrets.json`, `*.pfx`, `*.key` (đã có trong `.gitignore`) |
| Phạm vi áp dụng | User Secrets chỉ được đọc ở môi trường `Development`, đã đặt sẵn trong `launchSettings.json` |
| Database riêng | Mỗi thành viên nên dùng tên database riêng (`Database=`) để không ghi đè dữ liệu của nhau |

### Cơ sở dữ liệu

Ứng dụng **không tự migrate khi khởi động**. Tạo hoặc cập nhật database bằng lệnh:

```bash
dotnet ef database update --project NhaTro/Web
dotnet ef migrations list --project NhaTro/Web
```

Danh sách migration mong đợi:

| Migration | Nội dung |
| --- | --- |
| `00000000000000_CreateIdentitySchema` | Các bảng Identity (`AspNetUsers`, `AspNetRoles`, `AspNetUserPasskeys`, ...) |
| `20261008142750_CoreSchema` | Ba bảng nghiệp vụ `Properties`, `Rooms`, `Tenants` |

Trong Visual Studio, có thể dùng Package Manager Console: `Update-Database` (Default project là `Web`).

**Lệnh hữu ích khác**

```bash
# Xuất script SQL mà không cần kết nối database (không commit file kết quả)
dotnet ef migrations script --idempotent --project NhaTro/Web

# Xóa database DEV của chính bạn để tạo lại từ đầu (không chạy trên database dùng chung)
dotnet ef database drop --project NhaTro/Web

# Thêm migration mới (chỉ sau khi nhóm thống nhất thay đổi schema)
dotnet ef migrations add <TenMigration> --project NhaTro/Web --output-dir Data/Migrations
```

Quy ước nhóm: migration mới theo hướng cộng thêm, không sửa migration đã gộp. File chung và migrations sửa theo lượt, không đổi schema hoặc DTO âm thầm.

### Chạy ứng dụng

```bash
dotnet dev-certs https --trust    # lần đầu, tin cậy chứng chỉ HTTPS dev
dotnet run --project NhaTro/Web --launch-profile https
```

| Profile | Địa chỉ |
| --- | --- |
| `https` | https://localhost:7256 và http://localhost:5177 |
| `http` | http://localhost:5177 |

Trong Visual Studio, chọn profile `https` rồi nhấn F5 hoặc Ctrl+F5.

**Log** ghi ra Console và file `%LocalAppData%\NhaTro\logs\web-<yyyyMMdd>.log`, giữ 7 file gần nhất.

**Tạo tài khoản thử đầu tiên.** Vì chưa có dịch vụ gửi email, cần làm đúng thứ tự:

1. Mở `/Account/Register`, nhập email và mật khẩu.
2. Ở trang "Register confirmation", bấm liên kết **Click here to confirm your account**. Trang tự hiện liên kết này vì chưa cấu hình dịch vụ email.
3. Đăng nhập tại `/Account/Login`.

Hệ thống đang bật `RequireConfirmedAccount`, nên bỏ qua bước 2 sẽ không đăng nhập được. Tài khoản này chưa có vai trò vì phân quyền chưa được triển khai.

### Kiểm thử và CI

```bash
dotnet test NhaTro/NhaTro.slnx --no-build --nologo
```

Hiện chỉ có 3 test rỗng, mỗi project một test. Kết quả mong đợi là 3 test đạt và 0 lỗi; việc này chỉ xác nhận solution biên dịch được và trình chạy test hoạt động.

| Loại test | Định hướng |
| --- | --- |
| Unit | xUnit, kiểm tra quy tắc nghiệp vụ |
| Integration | SQL Server database riêng, không dùng EF InMemory |
| E2E | Playwright, cần cài trình duyệt sau khi build (hướng dẫn bổ sung khi có test E2E thật) |

**CI** (`.github/workflows/ci.yml`) chạy khi push hoặc pull request vào `main`: Windows, .NET `10.0.x`, lần lượt `restore`, `build`, `test` trên `NhaTro/NhaTro.slnx`. Nên chạy đủ ba lệnh này ở máy trước khi mở PR.

### Cấu trúc thư mục

```text
.
├── .github/workflows/ci.yml
├── docs/                          Thiết kế, kiến trúc, công nghệ, giao diện, phân công, sơ đồ
└── NhaTro/
    ├── NhaTro.slnx
    ├── Web/                       Blazor Web App (frontend và backend)
    │   ├── Program.cs
    │   ├── Components/            App, Routes, Layout, Pages, Account (Identity scaffold)
    │   ├── Data/                  ApplicationDbContext, Configurations/, Migrations/
    │   ├── Features/              Properties, Rooms, Tenants (hiện chỉ có Entities; Rooms có thêm Enums)
    │   └── wwwroot/
    ├── NhaTro.UnitTests/
    ├── NhaTro.IntegrationTests/
    └── NhaTro.E2ETests/
```

Cấu trúc đầy đủ dự kiến cho toàn hệ thống nằm ở [docs/architecture/cau_truc_thu_muc.md](docs/architecture/cau_truc_thu_muc.md). Thư mục chỉ được tạo khi có file thực tế.

### Xử lý lỗi thường gặp

| Triệu chứng | Cách xử lý |
| --- | --- |
| `dotnet: command not found`, hoặc SDK không phải 10.x | Cài .NET SDK 10, mở terminal mới, kiểm tra `dotnet --list-sdks` |
| Không mở hoặc build được `NhaTro.slnx` | SDK hoặc Visual Studio quá cũ; nâng lên bản hỗ trợ .NET 10 |
| `dotnet ef` không nhận lệnh | Cài tool như trong [Bắt đầu nhanh](#bắt-đầu-nhanh), mở terminal mới |
| Lỗi kết nối SQL Server hoặc LocalDB | Windows: chạy `sqllocaldb info`, rồi `sqllocaldb start mssqllocaldb`. Hoặc trỏ sang SQL Server khác bằng User Secrets |
| `Connection string 'DefaultConnection' not found` | Chạy lệnh từ thư mục gốc repo với `--project NhaTro/Web`; kiểm tra `appsettings.json` còn nguyên |
| Đăng ký xong nhưng không đăng nhập được | Tài khoản chưa xác nhận; làm bước 2 trong phần [Chạy ứng dụng](#chạy-ứng-dụng) |
| Trình duyệt cảnh báo chứng chỉ HTTPS | Chạy `dotnet dev-certs https --trust` |
| Cổng 5177 hoặc 7256 đang bị chiếm | Chạy tạm với `--urls`, ví dụ `dotnet run --project NhaTro/Web --urls http://localhost:5200`; không sửa `launchSettings.json` chung |

### Tài liệu và quy tắc làm việc

| Nội dung | Tệp |
| --- | --- |
| Luồng nghiệp vụ P1 đến P8, kho dữ liệu D1 đến D7 | [docs/thiet_ke_he_thong.md](docs/thiet_ke_he_thong.md) |
| Cấu trúc thư mục và quy tắc phụ thuộc | [docs/architecture/cau_truc_thu_muc.md](docs/architecture/cau_truc_thu_muc.md) |
| Công nghệ và quy ước | [docs/tech-stack/cong_nghe_su_dung.md](docs/tech-stack/cong_nghe_su_dung.md) |
| Quy chuẩn giao diện | [docs/design/phong_cach_giao_dien.md](docs/design/phong_cach_giao_dien.md) |
| Phân công và lịch theo tuần | [docs/task/phan_cong_cong_viec.md](docs/task/phan_cong_cong_viec.md), [docs/task/cong_viec_theo_tuan.md](docs/task/cong_viec_theo_tuan.md) |
| Dàn ý báo cáo Word | [docs/report/dan_y_bao_cao_word.md](docs/report/dan_y_bao_cao_word.md) |

**Quy tắc ngắn cho cả nhóm**

- Không commit secret, log, `bin/`, `obj/`, hoặc đường dẫn chỉ tồn tại trên máy cá nhân.
- Chạy `restore`, `build`, `test` ở máy trước khi mở PR.
- Chức năng chưa triển khai phải được ghi rõ trong tài liệu, không mô tả như đã chạy.

### Xác nhận setup

Ai chạy xong từ bản clone sạch thì ghi tên và ngày vào PR hoặc nhóm chat. Mục nào lỗi thì báo kèm thông báo lỗi.

- [ ] `dotnet restore` và `dotnet build` thành công, 0 lỗi
- [ ] `dotnet ef database update` thành công, `migrations list` có đủ 2 migration
- [ ] `dotnet run` mở được `/`, nút **Kiểm tra** hiện thông báo
- [ ] Đăng ký, xác nhận, đăng nhập được
- [ ] `dotnet test` đạt 3 test
- [ ] Không có secret hoặc đường dẫn riêng của máy trong thay đổi của bạn

---

## English

### Contents

1. [Status](#status)
2. [Tech stack](#tech-stack)
3. [Quick start](#quick-start)
4. [Prerequisites](#prerequisites)
5. [Configuration and secrets](#configuration-and-secrets)
6. [Database](#database)
7. [Run](#run)
8. [Tests and CI](#tests-and-ci)
9. [Troubleshooting](#troubleshooting)
10. [Setup checklist](#setup-checklist)

### Status

NhaTro is a Blazor Web App for managing a boarding house: rooms, tenants, contracts, utilities, invoices, payments and maintenance incidents, for three roles (Owner, Manager, Tenant). The repo is at the **INIT** stage.

| Included | Not yet implemented |
| --- | --- |
| Blazor Web App, .NET 10, Interactive Server | Owner / Manager / Tenant roles, role seeding, policies; no page uses `[Authorize]` |
| ASP.NET Core Identity (cookie) with the default scaffolded account pages | Real email sending (`IdentityNoOpEmailSender` is used) |
| EF Core 10 and SQL Server, 2 migrations: `CreateIdentitySchema`, `CoreSchema` | Sidebar and topbar layout, theme per the [UI guide](docs/design/phong_cach_giao_dien.md); `/` is only a MudBlazor smoke-test page |
| Entities and EF configuration for `Property`, `Room`, `Tenant` (soft delete, filtered unique indexes) | Services, DTOs and UI for properties, rooms and tenants |
| Serilog to Console and file | Contracts, deposits, utilities, invoices, payments, incidents, reports (no tables, no code) |
| 3 xUnit test projects, one empty test each | Real business tests, file storage, health checks, Options validation |
| GitHub Actions CI: restore, build, test | `global.json`, `.editorconfig`, a tool manifest for `dotnet-ef` |

The right-hand column is **not implemented**. The documents in `docs/` (in Vietnamese) describe the intended design, not running code. The commands below come from the repo configuration and have **not yet been verified on a clean machine**; please complete the [setup checklist](#setup-checklist).

### Tech stack

| Area | Technology |
| --- | --- |
| Platform | .NET 10 (`net10.0`) |
| Web | Blazor Web App, Interactive Server |
| UI | MudBlazor 9.11.0 |
| Data | EF Core 10.0.12, SQL Server |
| Authentication | ASP.NET Core Identity, cookie |
| Logging | Serilog (Console, File) |
| Testing | xUnit, `Mvc.Testing`, Playwright |
| CI | GitHub Actions (Windows) |

### Quick start

For a **Windows machine with .NET SDK 10 and SQL Server LocalDB**. Run from the repository root.

```bash
git clone https://github.com/utc2-programming-project/LTW-boarding_house_management_web.git
cd LTW-boarding_house_management_web

dotnet restore NhaTro/NhaTro.slnx
dotnet build NhaTro/NhaTro.slnx --no-restore --nologo

dotnet tool install --global dotnet-ef --version 10.*
dotnet ef database update --project NhaTro/Web

dotnet dev-certs https --trust
dotnet run --project NhaTro/Web --launch-profile https
```

Open https://localhost:7256 and click **Kiểm tra** ("Check"). A "MudBlazor hoạt động!" message means the UI works. For another SQL Server or operating system, see [Configuration and secrets](#configuration-and-secrets).

### Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| Git | Recent version | |
| .NET SDK | **10.x** | Projects target `net10.0`; the solution uses the `.slnx` format. Check with `dotnet --version` |
| SQL Server | LocalDB, or SQL Server / Express | LocalDB ships with Visual Studio and is Windows only; the default configuration uses LocalDB |
| Visual Studio | A version that supports .NET 10 | Recommended by the project docs; the CLI also works |
| `dotnet-ef` | **10.x** | Install once: `dotnet tool install --global dotnet-ef --version 10.*` |
| SSMS | Optional | Inspect data and run queries |

CI runs on Windows. The project has not been verified on macOS or Linux; on those systems use a separate SQL Server configured through User Secrets. In Visual Studio, open `NhaTro/NhaTro.slnx` and set `Web` as the startup project.

### Configuration and secrets

`NhaTro/Web/appsettings.json` already contains a default **LocalDB** connection string using Windows authentication and no password. On Windows with LocalDB you can skip this step. For another SQL Server or credentials, use **User Secrets** (stored outside the repo, per machine):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<HOST>;Database=<DB_NAME>;User Id=<USER>;Password=<PASSWORD>;TrustServerCertificate=True;MultipleActiveResultSets=true" --project NhaTro/Web
dotnet user-secrets list --project NhaTro/Web
```

Replace every `<...>` with your own values. In Visual Studio: right-click `Web`, then **Manage User Secrets**.

| Rule | Detail |
| --- | --- |
| Never commit secrets | Passwords, connection strings with passwords, `secrets.json`, `*.pfx`, `*.key` (already in `.gitignore`) |
| Scope | User Secrets are only read in the `Development` environment, set by `launchSettings.json` |
| Personal database | Each member should use a personal database name (`Database=`) to avoid overwriting each other's data |

### Database

The app does **not** apply migrations on startup. Create or update the database with:

```bash
dotnet ef database update --project NhaTro/Web
dotnet ef migrations list --project NhaTro/Web
```

| Expected migration | Contents |
| --- | --- |
| `00000000000000_CreateIdentitySchema` | Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserPasskeys`, ...) |
| `20261008142750_CoreSchema` | Business tables `Properties`, `Rooms`, `Tenants` |

In Visual Studio you can use Package Manager Console: `Update-Database` (Default project is `Web`).

```bash
# Generate a SQL script without a database connection (do not commit the output)
dotnet ef migrations script --idempotent --project NhaTro/Web

# Drop YOUR OWN development database to start over (never on a shared database)
dotnet ef database drop --project NhaTro/Web

# Add a migration (only after the team agrees on the schema change)
dotnet ef migrations add <MigrationName> --project NhaTro/Web --output-dir Data/Migrations
```

Team rules: migrations are additive and already-merged migrations are not edited; shared files and migrations are changed in turns, and schemas or DTOs are never changed silently.

### Run

```bash
dotnet dev-certs https --trust    # first time, trusts the dev HTTPS certificate
dotnet run --project NhaTro/Web --launch-profile https
```

| Profile | URLs |
| --- | --- |
| `https` | https://localhost:7256 and http://localhost:5177 |
| `http` | http://localhost:5177 |

In Visual Studio, select the `https` profile and press F5 or Ctrl+F5. **Logs** go to the Console and to `%LocalAppData%\NhaTro\logs\web-<yyyyMMdd>.log` (the last 7 files are kept).

**Create the first test account.** No email service exists, so follow this order:

1. Open `/Account/Register` and enter an email and password.
2. On the "Register confirmation" page, click **Click here to confirm your account**. The page shows this link because no email sender is configured.
3. Sign in at `/Account/Login`.

`RequireConfirmedAccount` is enabled, so skipping step 2 prevents sign-in. The account has no role because authorization is not implemented yet.

### Tests and CI

```bash
dotnet test NhaTro/NhaTro.slnx --no-build --nologo
```

There are only three empty tests today, one per project. Expect 3 passed and 0 failed; this only confirms that the solution compiles and the test runner works. Real integration tests will use a dedicated SQL Server database (not EF InMemory); E2E tests will use Playwright and need browsers installed after a build (instructions will be added with the first real E2E test).

**CI** (`.github/workflows/ci.yml`) runs on pushes and pull requests to `main`: Windows, .NET `10.0.x`, then `restore`, `build`, `test` on `NhaTro/NhaTro.slnx`. Run the same three commands locally before opening a PR.

The current folder layout is shown in the Vietnamese section "Cấu trúc thư mục"; the planned full structure is in [docs/architecture/cau_truc_thu_muc.md](docs/architecture/cau_truc_thu_muc.md).

### Troubleshooting

| Symptom | Fix |
| --- | --- |
| `dotnet: command not found`, or SDK is not 10.x | Install .NET SDK 10, open a new terminal, check `dotnet --list-sdks` |
| `NhaTro.slnx` fails to open or build | SDK or Visual Studio too old; upgrade to a .NET 10 capable version |
| `dotnet ef` not recognized | Install the tool as in [Quick start](#quick-start), open a new terminal |
| SQL Server or LocalDB connection error | Windows: `sqllocaldb info`, then `sqllocaldb start mssqllocaldb`; or point to another SQL Server with User Secrets |
| `Connection string 'DefaultConnection' not found` | Run from the repo root with `--project NhaTro/Web`; check `appsettings.json` is intact |
| Registered but cannot sign in | The account is not confirmed; do step 2 in [Run](#run) |
| HTTPS certificate warning | Run `dotnet dev-certs https --trust` |
| Port 5177 or 7256 already in use | Run temporarily with `--urls`, for example `dotnet run --project NhaTro/Web --urls http://localhost:5200`; do not edit the shared `launchSettings.json` |

### Setup checklist

Whoever completes a clean-clone run should note their name and the date in the PR or team chat, and report any failing item with the error message.
- [ ] `dotnet restore` and `dotnet build` succeed with 0 errors
- [ ] `dotnet ef database update` succeeds and `migrations list` shows both migrations
- [ ] `dotnet run` serves `/` and the **Kiểm tra** button shows a message
- [ ] Register, confirm and sign in work
- [ ] `dotnet test` passes 3 tests
- [ ] No secrets or machine-specific paths in your changes
