// Điểm khởi động của web: đăng ký DI (Blazor, Identity + cookie, EF Core, dịch vụ nghiệp vụ,
// email/SMTP, localization, rate limit), cấu hình middleware, seed vai trò/tài khoản mẫu rồi map endpoint.
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Components;
using Web.Components.Account;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Web.Configuration.Options;
using Web.Configuration.Validation;
using Web.Data;
using Web.Data.Seeding;
using Web.Components.Shared.Feedback;
using Web.Endpoints.Files;
using Web.Endpoints.Tenants;
using Web.Features.Accounts;
using Web.Features.Accounts.Contexts;
using Web.Features.Accounts.Otp;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Rooms;
using Web.Features.Rooms.Services;
using Web.Features.Settings.Services;
using Web.Features.Maintenance.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.Services;
using Web.Features.Storage.Services;
using Web.Infrastructure.ErrorHandling;
using Web.Infrastructure.Email;
using Web.Infrastructure.Addresses;
using Web.Infrastructure.Storage;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using Web.Endpoints.Health;
using Web.Infrastructure.Logging;
using Web.Infrastructure.Security;
var builder = WebApplication.CreateBuilder(args);

// ── 1. Ghi log (Serilog) ───────────────────────────────────────────────────
// IIS có thể đặt log ngoài thư mục publish; chạy dev thì dùng LocalApplicationData.
var configuredLogDirectory = builder.Configuration["Logging:Directory"] ?? string.Empty;
string logDirectory;
try
{
    // Windows có thể cắt chuỗi tại NUL khi mở rộng biến môi trường; phải chặn trước khi dùng path.
    if (configuredLogDirectory.Contains('\0'))
    {
        throw new ArgumentException();
    }

    configuredLogDirectory = Environment.ExpandEnvironmentVariables(configuredLogDirectory).Trim();
    logDirectory = configuredLogDirectory.Length == 0 ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NhaTro",
        "logs") : Path.GetFullPath(configuredLogDirectory, builder.Environment.ContentRootPath);
}
catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
{
    // Chỉ nêu tên cấu hình, không đưa giá trị đường dẫn vào lỗi khởi động.
    throw new InvalidOperationException("Logging:Directory must be a valid directory path.");
}

Directory.CreateDirectory(logDirectory);

// Console + file xoay theo ngày (giữ 7 ngày). Scrubber che thuộc tính nhạy cảm (mật khẩu, token,
// CCCD) và SafeExceptionEnricher làm sạch exception trước khi ghi để không lộ connection string/SQL.
builder.Services.AddSerilog(config => config.MinimumLevel.Information().MinimumLevel.Override("Microsoft", LogEventLevel.Warning).Enrich.FromLogContext()
    .Enrich.With<SensitiveDataScrubber>()
    .Enrich.With<SafeExceptionEnricher>()
    .WriteTo.Console(outputTemplate: LogTemplates.Console)
    .WriteTo.File(
        Path.Combine(logDirectory, "web-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: LogTemplates.File));

// ── 2. Blazor Server + Identity: xác thực và cookie ────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// Cần cho claims factory/OTP đọc IP và cookie ngữ cảnh trong mọi luồng.
builder.Services.AddHttpContextAccessor();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    }).AddIdentityCookies();

// /api trả 401/403 đúng hợp đồng mục 5, không chuyển hướng về trang đăng nhập.
builder.Services.ConfigureApplicationCookie(options =>
{
    // PLAN-027: cookie phiên chỉ gửi qua HTTPS ở production, không đọc được bằng JS.
    // Testing chạy TestServer trên http://localhost nên phải SameAsRequest,
    // nếu không cookie Secure bị bỏ và mọi test đăng nhập sẽ bị 302 về /Account/Login.
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    var redirectToLogin = options.Events.OnRedirectToLogin;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return redirectToLogin(context);
    };

    var redirectToAccessDenied = options.Events.OnRedirectToAccessDenied;
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return redirectToAccessDenied(context);
    };
});

// ── 3. EF Core ─────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── 4. Identity: đăng nhập, mật khẩu, khóa tài khoản ───────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Tài khoản do quản trị tạo không qua bước xác nhận email nên không bắt buộc xác nhận.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // Theo đặc tả: tối thiểu 8 ký tự, có chữ hoa/thường/số; không bắt ký tự đặc biệt.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        // Không tự khóa khi gõ sai mật khẩu (kẻ tấn công có thể khóa tài khoản chủ trọ = DoS):
        // chống dò mật khẩu bằng LoginAttemptGuard chặn theo IP, khóa tài khoản chỉ do quản trị chủ động.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        // Tên đăng nhập là email nên cấm trùng email.
        options.User.RequireUniqueEmail = true;
    }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>()
      // VHT-95: SignInManager riêng chặn tài khoản "Chờ duyệt".
      .AddSignInManager<AccountSignInManager>()
      .AddClaimsPrincipalFactory<NhaTroClaimsPrincipalFactory>()
      .AddDefaultTokenProviders();

// PLAN-022/027: token đặt lại mật khẩu/xác nhận email chỉ sống 15 phút.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromMinutes(15));

// PLAN-025: security stamp được kiểm lại sau 2 phút để khoá chủ trọ có hiệu lực nhanh.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(2));

// ── 5. Policy phân quyền ───────────────────────────────────────────────────
// Policy nghiệp vụ loại trừ Admin ngay từ endpoint (kể cả tài khoản có thêm vai trò nghiệp vụ);
// service gọi BusinessGuard để chặn lớp 2 và tự lọc phạm vi dữ liệu theo vai trò.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    options.AddPolicy("Business", p => p.RequireRole(Roles.ChuTro, Roles.QuanLy, Roles.KhachThue));
    options.AddPolicy("ContractBusiness", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        (context.User.IsInRole(Roles.ChuTro) ||
         context.User.IsInRole(Roles.QuanLy) ||
         context.User.IsInRole(Roles.KhachThue))));
    options.AddPolicy("ContractManage", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        (context.User.IsInRole(Roles.ChuTro) || context.User.IsInRole(Roles.QuanLy))));
    options.AddPolicy(Web.Features.Maintenance.IncidentAccess.Policy, p => p
        .RequireAuthenticatedUser()
        .RequireAssertion(context => Web.Features.Maintenance.IncidentAccess.IsAllowed(context.User)));
    options.AddPolicy("UtilityBusiness", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        (context.User.IsInRole(Roles.ChuTro) ||
         context.User.IsInRole(Roles.QuanLy) ||
         context.User.IsInRole(Roles.KhachThue))));
    options.AddPolicy("UtilityManage", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        context.User.IsInRole(Roles.ChuTro)));
    options.AddPolicy("UtilityRecord", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        (context.User.IsInRole(Roles.ChuTro) || context.User.IsInRole(Roles.QuanLy))));
    options.AddPolicy("InvoiceView", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        (context.User.IsInRole(Roles.ChuTro) ||
         context.User.IsInRole(Roles.QuanLy) ||
         context.User.IsInRole(Roles.KhachThue))));
    // VHT-56: chỉ Quản lý/Chủ trọ được vào hàng đợi xác nhận thanh toán.
    options.AddPolicy("PaymentReview", p => p.RequireAssertion(context =>
        context.User.Identity?.IsAuthenticated == true &&
        !context.User.IsInRole(Roles.Admin) &&
        (context.User.IsInRole(Roles.ChuTro) || context.User.IsInRole(Roles.QuanLy))));
});

// ── 6. Service nghiệp vụ (đọc/ghi SQL, tự kiểm quyền và phạm vi) ───────────
builder.Services.AddScoped<RoleService>();
// PLAN-022: OTP một lần cho đăng ký/xác thực tài khoản.
builder.Services.AddOptions<OtpOptions>().Bind(builder.Configuration.GetSection(OtpOptions.SectionName));
builder.Services.AddScoped<IOtpService, OtpService>();
// PLAN-023: ngữ cảnh đa vai trò + cookie ngữ cảnh đang chọn.
builder.Services.AddScoped<RoleContextService>();
builder.Services.AddScoped<ActiveRoleCookie>();
builder.Services.AddScoped<PostLoginRouter>();
// PLAN-025: khoá lan chủ trọ + màn Admin quản lý chủ trọ.
builder.Services.AddScoped<Web.Features.Accounts.Owners.OwnerAccessGuard>();
builder.Services.AddScoped<Web.Features.Accounts.Owners.OwnerAdminService>();
// VHT-95: hồ sơ cá nhân + quản trị tài khoản; quyền kiểm ở server.
builder.Services.AddScoped<IAccountService, AccountService>();

builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IRoomReportService, RoomReportService>();
builder.Services.AddScoped<ITenantReportService, TenantReportService>();
builder.Services.AddScoped<Web.Features.Dashboard.Services.DashboardPageService>();
builder.Services.AddScoped<Web.Features.Home.Services.HomePageService>();
builder.Services.AddScoped<IContractService, ContractService>();
// VHT-125: khách thuê gửi yêu cầu gia hạn.
builder.Services.AddScoped<IContractRenewalService, ContractRenewalService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IncidentPhotoService>();
builder.Services.AddScoped<IncidentPageService>();
// Mỗi loại yêu cầu duyệt có một IApprovalApplier; loại chưa có applier sẽ bị chặn (fail closed).
builder.Services.AddScoped<IApprovalApplier, IncidentResultPhotoApprovalApplier>();
builder.Services.AddScoped<IIncidentFileScope, IncidentFileScope>();
builder.Services.AddScoped<IApprovalApplier, IncidentCreateApprovalApplier>();
builder.Services.AddScoped<IApprovalApplier, IncidentStatusApprovalApplier>();
builder.Services.AddScoped<IApprovalApplier, ContractApprovalApplier>();
builder.Services.AddScoped<IApprovalApplier, ContractDepositApprovalApplier>();
builder.Services.AddScoped<ContractApprovalSummaryService>();
// VHT-99: duyệt SW-06 = đưa hồ sơ khách thuê vào thùng rác (hoặc bật/tắt trạng thái).
builder.Services.AddScoped<IApprovalApplier, TenantDeleteApprovalApplier>();
// P3: duyệt SW-04 (tạo) và SW-05 (sửa) hồ sơ khách thuê.
builder.Services.AddScoped<IApprovalApplier, Web.Features.Tenants.Services.TenantCreateApprovalApplier>();
builder.Services.AddScoped<IApprovalApplier, Web.Features.Tenants.Services.TenantUpdateApprovalApplier>();
// P2: duyệt SW-01/02/03 = sửa thông tin / giá / trạng thái phòng.
builder.Services.AddScoped<IApprovalApplier>(sp => new Web.Features.Rooms.Services.RoomUpdateApprovalApplier(
    sp.GetRequiredService<TimeProvider>(), "SW-01"));
builder.Services.AddScoped<IApprovalApplier>(sp => new Web.Features.Rooms.Services.RoomUpdateApprovalApplier(
    sp.GetRequiredService<TimeProvider>(), "SW-02"));
builder.Services.AddScoped<IApprovalApplier>(sp => new Web.Features.Rooms.Services.RoomUpdateApprovalApplier(
    sp.GetRequiredService<TimeProvider>(), "SW-03"));
// SW-10/SW-11: chờ Chủ trọ duyệt trước khi vào sổ / phát hành hóa đơn.
builder.Services.AddScoped<IApprovalApplier, Web.Features.Invoices.Services.InvoiceCreateApprovalApplier>();
builder.Services.AddScoped<IApprovalApplier, Web.Features.Invoices.Services.InvoiceIssueApprovalApplier>();
builder.Services.AddScoped<ContractPageService>();
// VHT-125: Chủ trọ / Quản lý duyệt yêu cầu gia hạn.
builder.Services.AddScoped<ContractRenewalPageService>();
// VHT-24: phân công Quản lý theo tòa nhà (Chủ trọ sở hữu gán/bỏ gán).
builder.Services.AddScoped<IPropertyManagerService, PropertyManagerService>();
builder.Services.AddScoped<IRoomUsageChecker, ActiveContractRoomUsageChecker>();
builder.Services.AddScoped<P2CallerProvider>();
builder.Services.AddScoped<PropertyPageService>();
builder.Services.AddScoped<RoomPageService>();
// PLAN-010: nháp thiết lập nhà trọ ở local (ProtectedLocalStorage do Blazor đăng ký sẵn;
// khai báo lại để chắc chắn resolve được trong mọi cấu hình host).
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage>();
builder.Services.AddScoped<Web.Features.Properties.Services.PropertySetupDraftStore>();

// Gợi ý địa chỉ: đọc một lần dữ liệu JSON 34 tỉnh/thành phố và 3.321 phường/xã trong wwwroot.
builder.Services.AddSingleton<VietnamAddressCatalog>();

// 21 công tắc Quản lý lưu trong SQL (không đặt ở appsettings). Service ghi chỉ dành cho Chủ trọ;
// policy chỉ đọc, dùng chung để quyết định có thay Quản lý hay không.
builder.Services.AddScoped<ManagerToggleService>();
builder.Services.AddScoped<IManagerToggleService>(sp => sp.GetRequiredService<ManagerToggleService>());
builder.Services.AddScoped<IManagerToggleReader>(sp => sp.GetRequiredService<ManagerToggleService>());
builder.Services.AddScoped<ManagerTogglePolicy>();
builder.Services.AddScoped<IManagerTogglePolicy>(sp => sp.GetRequiredService<ManagerTogglePolicy>());
builder.Services.AddScoped<IRoomChangePolicy>(sp => sp.GetRequiredService<ManagerTogglePolicy>());

// VHT-96: hàng đợi duyệt thao tác Quản lý; mã SW chưa có applier sẽ bị chặn khi duyệt.
builder.Services.AddScoped<ManagerApprovalService>();
builder.Services.AddScoped<IManagerApprovalService>(sp => sp.GetRequiredService<ManagerApprovalService>());
// Trang Hàng đợi duyệt (/approvals): làm giàu yêu cầu bằng tên đối tượng và nội dung đề xuất.
builder.Services.AddScoped<ApprovalQueuePageService>();

// Khách thuê + tài liệu thuê: kiểm quyền và phạm vi trong service.
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<TenantPageService>();
builder.Services.AddScoped<TenantDocumentPageService>();
// Banner + thẻ "Phòng đang thuê" cho khách thấy Chủ trọ/Quản lý của tòa đang ở.
builder.Services.AddScoped<TenantContactPageService>();

// ── 7. Chặn trường lạ trong JSON của HTTP endpoint ─────────────────────────
// Không cho client tự gửi trường nhạy cảm (UserId/OwnerId).
// Blazor Server gọi service trực tiếp (không qua JSON) nên không bị ảnh hưởng.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling =
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);

// ── 8. Bảo mật nền tảng, health, rate limit ────────────────────────────────
// Ngưỡng đọc từ section "Security"; thiếu cấu hình thì dùng mặc định an toàn.
var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()
    ?? new SecurityOptions();

// Bật DPAPI mà chưa đặt đường dẫn keys là cấu hình sai: báo lỗi ngay, không âm thầm bỏ qua.
if (securityOptions.ProtectKeysWithDpapi &&
    string.IsNullOrWhiteSpace(securityOptions.DataProtectionKeysPath))
{
    throw new InvalidOperationException(
        "Security:ProtectKeysWithDpapi requires Security:DataProtectionKeysPath.");
}

// Giữ khóa Data Protection ngoài thư mục publish để cookie đăng nhập không mất khi triển khai lại.
if (!string.IsNullOrWhiteSpace(securityOptions.DataProtectionKeysPath))
{
    var dataProtection = builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
            Environment.ExpandEnvironmentVariables(securityOptions.DataProtectionKeysPath))).SetApplicationName("NhaTro.Web");

    // IIS một máy: chỉ identity Windows hiện tại giải mã được keys mới; không đổi keys cũ.
    if (securityOptions.ProtectKeysWithDpapi)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Security:ProtectKeysWithDpapi requires Windows.");
        }

        dataProtection.ProtectKeysWithDpapi();
    }
}

// CAPTCHA sau nhiều lần đăng nhập sai; giai đoạn 1 dùng thử thách cục bộ, không thêm phụ thuộc.
builder.Services.AddSingleton<ICaptchaVerifier, LocalCaptchaVerifier>();

// Chống dò mật khẩu: đếm số lần sai theo IP và chặn IP tạm thời thay vì khóa tài khoản.
builder.Services.AddSingleton<ILoginAttemptGuard, LoginAttemptGuard>();

// Health live/ready; ready kiểm kết nối DB qua EF HealthChecks.
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database", tags: [HealthEndpoints.ReadyTag]);

// Rate limit: limiter toàn cục chỉ áp POST đăng nhập; /api/files và /culture/set có policy riêng.
builder.Services.AddRateLimiter(rateLimiter =>
{
    rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiter.AddPolicy(RateLimitPolicies.FileUpload, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPolicies.ClientKey(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = securityOptions.RateLimit.FileUploadPermitLimit,
                Window = TimeSpan.FromSeconds(securityOptions.RateLimit.FileUploadWindowSeconds),
                QueueLimit = 0,
            }));

    rateLimiter.AddPolicy(RateLimitPolicies.CultureSet, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPolicies.ClientKey(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = securityOptions.RateLimit.CultureSetPermitLimit,
                Window = TimeSpan.FromSeconds(securityOptions.RateLimit.CultureSetWindowSeconds),
                QueueLimit = 0,
            }));

    rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPolicies.IsLoginRequest(http)
            ? RateLimitPartition.GetFixedWindowLimiter(
                RateLimitPolicies.ClientKey(http),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = securityOptions.RateLimit.LoginPermitLimit,
                    Window = TimeSpan.FromSeconds(securityOptions.RateLimit.LoginWindowSeconds),
                    QueueLimit = 0,
                })
            // PLAN-008: chặn spam đăng ký chủ trọ (5 lần/giờ/IP).
            : RateLimitPolicies.IsRegisterRequest(http)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "register:" + RateLimitPolicies.ClientKey(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    })
                : RateLimitPolicies.IsOtpRequest(http)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        "otp:" + RateLimitPolicies.ClientKey(http),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(15),
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("no-limit"));

    rateLimiter.OnRejected = RateLimitPolicies.WriteRejectionAsync;
});

// ── 9. Lưu trữ tệp + giới hạn upload ───────────────────────────────────────
// Giới hạn đọc từ cấu hình và kiểm tra ngay lúc khởi động.
builder.Services.AddOptions<StorageOptions>().Bind(builder.Configuration.GetSection(StorageOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();
// Tài khoản ngân hàng nhận tiền cho VietQR thanh toán hóa đơn (BIDV 8800084909).
builder.Services.AddOptions<Web.Features.Invoices.BankAccountOptions>()
    .Bind(builder.Configuration.GetSection(Web.Features.Invoices.BankAccountOptions.SectionName));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileAccessAuditWriter, FileAccessAuditWriter>();
builder.Services.AddScoped<FileService>();

// VHT-26: giấy tờ P3 — phạm vi hồ sơ + audit; Quản lý theo phân công tòa nhà (VHT-24).
builder.Services.AddScoped<IManagerTenantScope, ContractManagerTenantScope>();
builder.Services.AddScoped<ITenantDocumentScopeService, TenantDocumentScopeService>();
builder.Services.AddScoped<ITenantDocumentService, TenantDocumentService>();

// Chặn ở tầng server: 10 MB + overhead multipart (12 MB), trước khi request vào service.
const long maxUploadRequestBytes = 12L * 1024 * 1024;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadRequestBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadRequestBytes);

// ── 10. Localization + antiforgery ─────────────────────────────────────────
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
    // Chỉ dùng cookie của selector; chưa chọn ngôn ngữ thì mặc định tiếng Việt.
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});

// ── 11. Gửi thư: SMTP hoặc dự phòng NoOp ───────────────────────────────────
// SMTP Gmail (App Password trong User Secrets); thiếu cấu hình thì thư chỉ ghi log để dev vẫn chạy.
builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
builder.Services.AddSingleton<EmailRecipientResolver>();
var emailSettings = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
    ?? new EmailOptions();
if (emailSettings.Smtp.IsConfigured)
{
    builder.Services.AddSingleton<SmtpEmailSender>();
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpEmailSender>());
    builder.Services.AddSingleton<IOutboundEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
}
else
{
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
    builder.Services.AddSingleton<IOutboundEmailSender, LogOnlyOutboundEmailSender>();
}

// ── 12. Service còn lại: tiện ích, hóa đơn, báo cáo, audit, UI ─────────────
// PLAN-006: lời mời khách thuê qua email.
builder.Services.AddScoped<Web.Features.Tenants.Invitations.TenantInvitationService>();
// PLAN-024: QR nhiều mốc hạn + mời khách đã có tài khoản.
builder.Services.AddScoped<Web.Features.Tenants.Invitations.TenantQrInvitationService>();
builder.Services.AddSingleton<Web.Features.Tenants.Invitations.QrCodeRenderer>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.UtilitySampleStateService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.MeterReadingPageService>();
// VHT-58: màn hình chỉ số/chi phí P5 đọc SQL thật (kỳ, snapshot biểu giá, hóa đơn P6).
builder.Services.AddScoped<Web.Features.Utilities.Services.MonthlyUtilityCalculationPageService>();
// Khách thuê đăng ký dịch vụ hằng tháng theo hợp đồng.
builder.Services.AddScoped<Web.Features.Utilities.Services.ContractServiceRegistrationService>();
// Quy trình đăng ký dịch vụ có xét duyệt + tạo hóa đơn từ yêu cầu.
builder.Services.AddScoped<Web.Features.Utilities.Services.ServiceRegistrationService>();
builder.Services.AddScoped<Web.Features.Notifications.INotificationService, Web.Features.Notifications.NotificationService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.IUtilityTariffService, Web.Features.Utilities.Services.UtilityTariffService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.UtilityPeriodCostSnapshotService>();
// VHT-51: ghi chỉ số thật, chốt chi phí snapshot và cấp nguồn chi phí cho hóa đơn.
builder.Services.AddScoped<Web.Features.Utilities.Services.IUtilityReadingService,
    Web.Features.Utilities.Services.UtilityReadingPeriodService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.IServiceCatalogService,
    Web.Features.Utilities.Services.ServiceCatalogService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.ServiceCatalogPageService>();
// D16: nhật ký thao tác tách sổ hệ thống/nghiệp vụ.
builder.Services.AddScoped<Web.Features.Audit.Services.AuditLogService>();
// P5: chỉ số điện/nước bền vững + lịch sử sửa chỉ số (D10, SW-09).
builder.Services.AddScoped<Web.Features.Utilities.Services.UtilityReadingService>();
builder.Services.AddScoped<IApprovalApplier, Web.Features.Utilities.Services.UtilityReadingApprovalApplier>();
// P6: báo cáo doanh thu/công nợ (OP39); thanh toán dùng IPaymentService VHT-54 bên dưới.
builder.Services.AddScoped<Web.Features.Reports.Services.RevenueReportService>();
// D8: xuất Excel P8 + SW-16.
builder.Services.AddScoped<Web.Features.Reports.Services.ReportExportService>();
builder.Services.AddScoped<IApprovalApplier, Web.Features.Reports.Services.ReportExportApprovalApplier>();
// VHT-35: query hóa đơn P6 tự giới hạn phạm vi theo vai trò người gọi.
builder.Services.AddScoped<Web.Features.Invoices.Services.IInvoiceQueryService,
    Web.Features.Invoices.Services.InvoiceQueryService>();
// VHT-53: lập/phát hành hóa đơn từ nguồn chi phí đã chốt, chống trùng kỳ.
builder.Services.AddScoped<Web.Features.Invoices.Services.IInvoiceService,
    Web.Features.Invoices.Services.InvoiceService>();
// VHT-54: gửi/xác nhận/từ chối thanh toán, ghi tiền và audit trong một transaction.
builder.Services.AddScoped<Web.Features.Invoices.Services.IPaymentService,
    Web.Features.Invoices.Services.PaymentService>();
// VHT-55: adapter UI khách gửi yêu cầu thanh toán + minh chứng, tra kết quả trước khi thử lại.
builder.Services.AddScoped<Web.Features.Invoices.Services.PaymentPageService>();
// VHT-56: hàng đợi đọc theo phạm vi + adapter UI Quản lý xác nhận/từ chối và tra kết quả.
builder.Services.AddScoped<Web.Features.Invoices.Services.IPaymentReviewQueryService,
    Web.Features.Invoices.Services.PaymentReviewQueryService>();
builder.Services.AddScoped<Web.Features.Invoices.Services.PaymentReviewPageService>();
builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);
var app = builder.Build();

// ── 13. Pipeline: log, lỗi, culture, rate limit, antiforgery ───────────────
app.UseSerilogRequestLogging();
// Đổi lỗi 413 của Kestrel/multipart về cùng cấu trúc JSON FileSizeExceeded.
app.UseMiddleware<FileUploadExceptionMiddleware>();
app.Logger.LogInformation("Web vận hành nhà trọ đã khởi động");

var smtpForLog = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value.Smtp;
if (smtpForLog.IsConfigured)
{
    app.Logger.LogInformation(
        "Gửi thư qua SMTP {Host}:{Port}, người gửi {From}.",
        smtpForLog.Host, smtpForLog.Port, smtpForLog.EffectiveFromAddress);
}
else
{
    app.Logger.LogWarning(
        "Chưa cấu hình Email:Smtp (thiếu UserName/Password/FromAddress). " +
        "Thư hệ thống sẽ chỉ ghi log, không gửi thật.");
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // Lỗi ngoài dự kiến ở /api trả JSON an toàn, không lộ chi tiết/stack trace.
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/api"),
        apiApp => apiApp.UseMiddleware<ApiExceptionHandlerMiddleware>());

    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRequestLocalization();

app.UseRateLimiter();

// Token antiforgery gắn với trạng thái đăng nhập: form login mở trước khi tab khác đăng nhập
// (hoặc phiên hết hạn) sẽ bị từ chối 400 text thô. Đổi thành redirect để tải lại form + token mới.
app.Use(async (context, next) =>
{
    var isLoginPost = HttpMethods.IsPost(context.Request.Method)
        && (context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase));

    if (!isLoginPost)
    {
        await next();
        return;
    }

    var originalBody = context.Response.Body;
    await using var buffer = new MemoryStream();
    context.Response.Body = buffer;
    try
    {
        await next();

        if (context.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            context.Response.Headers.Clear();
            context.Response.Redirect("/login?stale=1");
            return;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
    }
    finally
    {
        context.Response.Body = originalBody;
    }
});

// PLAN-027: header bảo mật + chặn POST cross-site; đặt trước antiforgery/endpoint.
app.UseMiddleware<Web.Infrastructure.Security.SecurityHeadersMiddleware>();

app.UseAntiforgery();
// PLAN-022: tài khoản chưa xác thực Gmail chỉ được vào trang OTP.
app.UseMiddleware<PendingVerificationMiddleware>();
// PLAN-023: tài khoản nhiều ngữ cảnh phải chọn vai trò trước khi vào nghiệp vụ.
app.UseMiddleware<PendingRoleMiddleware>();

// ── 14. Endpoint ───────────────────────────────────────────────────────────
// Đổi ngôn ngữ (VI/EN) cho bộ chọn ở topbar và trang auth: ghi cookie ".AspNetCore.Culture"
// sống 1 năm, chỉ nhận vi-VN/en-US; tiền VND và logic tính không đổi theo culture.
app.MapPost("/culture/set", async Task<IResult> (HttpContext http, string culture, IAntiforgery antiforgery) =>
{
    // Xác thực trước khi ghi cookie, kể cả endpoint chỉ nhận culture từ query.
    try
    {
        await antiforgery.ValidateRequestAsync(http);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    if (culture != "vi-VN" && culture != "en-US")
    {
        return Results.BadRequest("Ngon ngu khong duoc ho tro.");
    }

    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            IsEssential = true,
        });
    return Results.Ok(new { culture });
}).RequireRateLimiting(RateLimitPolicies.CultureSet);

app.MapGet("/culture/current", (HttpContext http) =>
{
    var feature = http.Features.Get<IRequestCultureFeature>();
    var current = feature?.RequestCulture.Culture.Name ?? "vi-VN";
    return Results.Ok(new { current, supported = new[] { "vi-VN", "en-US" } });
});

app.MapStaticAssets();
// Tệp dùng chung: kiểm quyền trong service, chặn Admin bằng policy "Business".
app.MapFileEndpoints();
// Health live/ready (ẩn danh, chỉ trả trạng thái).
app.MapHealthEndpoints();
// VHT-26: giấy tờ P3 (metadata, upload, tải, thay, xóa mềm) có kiểm phạm vi hồ sơ.
app.MapTenantDocumentEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Endpoint bổ sung cho các Razor component Identity /Account.
app.MapAdditionalIdentityEndpoints();

// ── 15. Khởi tạo dữ liệu ───────────────────────────────────────────────────
// Nạp/xoá dữ liệu demo bằng file JSON rời: chạy xong thoát, không mở web server.
// Không truyền cờ thì bỏ qua hoàn toàn; thiếu file JSON cũng là no-op.
var seedDemo = args.Any(a => string.Equals(a, "--seed-demo", StringComparison.OrdinalIgnoreCase));
var clearDemo = args.Any(a => string.Equals(a, "--clear-demo", StringComparison.OrdinalIgnoreCase));
if (seedDemo || clearDemo)
{
    var succeeded = await DemoDataSeeder.RunAsync(
        app.Services, app.Environment, app.Configuration, app.Logger, clearDemo);
    Environment.ExitCode = succeeded ? 0 : 1;
    return;
}

// Chạy dev: áp migration còn thiếu để schema DB khớp model.
if (app.Environment.IsDevelopment())
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await migrationDb.Database.MigrateAsync();
}

// Gieo vai trò, Admin đầu tiên (từ secret), rồi tài khoản mẫu dev.
using (var scope = app.Services.CreateScope())
{
    try
    {
        await RoleSeeder.EnsureAsync(scope.ServiceProvider);
        await AdminSeeder.EnsureAsync(scope.ServiceProvider, app.Configuration, app.Logger);
        // Có thể tắt tài khoản dev trên DB đang dùng dữ liệu thật bằng DevAccounts:Skip.
        if (app.Environment.IsDevelopment()
            && !app.Configuration.GetValue("DevAccounts:Skip", false))
        {
            await DevAccountSeeder.EnsureAsync(scope.ServiceProvider, app.Configuration, app.Logger);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Gieo role/user mau that bai");
        throw;
    }
}

app.Run();

// VHT-14: WebApplicationFactory cần class Program công khai để test tích hợp HTTP/auth tìm được assembly.
public partial class Program { }
