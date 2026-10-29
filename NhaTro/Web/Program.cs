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
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.Services;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.Services;
using Web.Features.Storage.Services;
using Web.Infrastructure.ErrorHandling;
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

// IIS co the dat log ngoai thu muc publish; dev giu duong dan mac dinh.
var configuredLogDirectory = builder.Configuration["Logging:Directory"];
var logDirectory = string.IsNullOrWhiteSpace(configuredLogDirectory) ? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "NhaTro",
    "logs") : Path.GetFullPath(
        Environment.ExpandEnvironmentVariables(configuredLogDirectory), builder.Environment.ContentRootPath);

Directory.CreateDirectory(logDirectory);

// log ở đây nè, công dụng của serilog đó.
builder.Services.AddSerilog(config => config.MinimumLevel.Information().MinimumLevel.Override("Microsoft", LogEventLevel.Warning).Enrich.FromLogContext()
    // che cac thuoc tinh nhay cam (mat khau, token, CCCD...) truoc khi ghi log.
    .Enrich.With<SensitiveDataScrubber>()
    // exception chi ghi dang da lam sach (khong lo connection string/SQL/duong dan may).
    .Enrich.With<SafeExceptionEnricher>()
    .WriteTo.Console(outputTemplate: LogTemplates.Console)
    .WriteTo.File(
        Path.Combine(logDirectory, "web-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: LogTemplates.File));

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    }).AddIdentityCookies();

// endpoint /api tra 401/403 dung hop dong muc 5, khong chuyen huong trang dang nhap.
builder.Services.ConfigureApplicationCookie(options =>
{
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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // tam thoi tat xac thuc email (chua cau hinh SMTP - lam sau).
        // Bat lai khi co IEmailSender that, neu khong tai khoan do quan tri tao se khong dang nhap duoc.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // Do manh mat khau theo dac ta: toi thieu 8 ky tu, co chu hoa, chu thuong, so.
        // Dac ta KHONG yeu cau ky tu dac biet nen tat de tranh bao loi kho hieu.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        // Khoa tam thoi khi dang nhap sai nhieu lan (khac voi khoa tai khoan do quan tri - ).
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        // Ten dang nhap = email: khong cho trung email.
        options.User.RequireUniqueEmail = true;
    }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager().AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    // Nghiep vu: tu choi Admin ngay tu endpoint, service goi BusinessGuard de chan them lop 2.
    options.AddPolicy("Business", p => p.RequireRole(Roles.ChuTro, Roles.QuanLy, Roles.KhachThue));
    // P7 mẫu: chặn Admin kể cả khi tài khoản có thêm vai trò nghiệp vụ.
    options.AddPolicy(Web.Features.Maintenance.IncidentDemoSession.Policy, p => p
        .RequireAuthenticatedUser()
        .RequireAssertion(context => Web.Features.Maintenance.IncidentDemoSession.IsAllowed(context.User)));
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
});
builder.Services.AddScoped<RoleService>();
// service tai khoan (ho so ca nhan + quan tri tai khoan theo ).
builder.Services.AddScoped<IAccountService, AccountService>();

// SQL services. Approval policy and active-contract checks are extension points.
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IRoomUsageChecker, NoActiveContractRoomUsageChecker>();
builder.Services.AddScoped<P2CallerProvider>();
builder.Services.AddScoped<PropertyPageService>();
builder.Services.AddScoped<RoomPageService>();

// 21 cong tac Quan ly luu SQL (khong dat trong appsettings) + policy handler dung chung.
// Service doc/ghi chi cho Chu tro; policy chi doc de quyet dinh thay Quan ly.
builder.Services.AddScoped<ManagerToggleService>();
builder.Services.AddScoped<IManagerToggleService>(sp => sp.GetRequiredService<ManagerToggleService>());
builder.Services.AddScoped<IManagerToggleReader>(sp => sp.GetRequiredService<ManagerToggleService>());
builder.Services.AddScoped<ManagerTogglePolicy>();
builder.Services.AddScoped<IManagerTogglePolicy>(sp => sp.GetRequiredService<ManagerTogglePolicy>());
builder.Services.AddScoped<IRoomChangePolicy>(sp => sp.GetRequiredService<ManagerTogglePolicy>());

// service khach thue, kiem quyen/pham vi trong service.
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<TenantPageService>();

// chan client gui truong la ngoai DTO (do truong nhay cam nhu UserId/OwnerId tu form).
// Ap cho JSON cua HTTP endpoint; Blazor Server goi service noi bo (khong qua JSON) nen khong anh huong.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling =
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);

// bao mat nen tang. Moi nguong doc tu cau hinh "Security", co mac dinh an toan neu thieu.
var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()
    ?? new SecurityOptions();

// Giu khoa Data Protection ngoai thu muc publish de cookie dang nhap khong bi mat khi trien khai lai.
if (!string.IsNullOrWhiteSpace(securityOptions.DataProtectionKeysPath))
{
    var dataProtection = builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
            Environment.ExpandEnvironmentVariables(securityOptions.DataProtectionKeysPath))).SetApplicationName("NhaTro.Web");

    // IIS mot may: chi identity Windows hien tai giai ma duoc keys moi; khong doi keys cu.
    if (securityOptions.ProtectKeysWithDpapi)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Security:ProtectKeysWithDpapi requires Windows.");
        }

        dataProtection.ProtectKeysWithDpapi();
    }
}

// CAPTCHA sau nhieu lan dang nhap sai. Giai doan 1 dung thach thuc cuc bo (khong them phu thuoc).
builder.Services.AddSingleton<ICaptchaVerifier, LocalCaptchaVerifier>();

// health live/ready. Ready kiem ket noi DB qua goi EF HealthChecks.
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database", tags: [HealthEndpoints.ReadyTag]);

// rate limiting. Global chi ap cho POST dang nhap; policy rieng cho /api/files va /culture/set.
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
            : RateLimitPartition.GetNoLimiter("no-limit"));

    rateLimiter.OnRejected = RateLimitPolicies.WriteRejectionAsync;
});

// storage dung chung. Gioi han doc tu cau hinh, kiem tra ngay khi khoi dong.
builder.Services.AddOptions<StorageOptions>().Bind(builder.Configuration.GetSection(StorageOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileAccessAuditWriter, FileAccessAuditWriter>();
builder.Services.AddScoped<FileService>();

// VHT-26: giấy tờ P3 — phạm vi hồ sơ, audit và điểm mở rộng phạm vi Quản lý theo tòa nhà (VHT-24).
builder.Services.AddScoped<IManagerTenantScope, NoManagerTenantScope>();
builder.Services.AddScoped<ITenantDocumentScopeService, TenantDocumentScopeService>();
builder.Services.AddScoped<ITenantDocumentService, TenantDocumentService>();

// Chan o tang server truoc khi vao service: lon hon gioi han cao nhat mot chut (10 MB + overhead multipart).
const long maxUploadRequestBytes = 12L * 1024 * 1024;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadRequestBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadRequestBytes);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
    // Chỉ dùng lựa chọn cookie; chưa chọn thì dùng VI, không theo query/trình duyệt.
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
    // Chỉ dùng cookie của selector; chưa chọn ngôn ngữ thì mặc định tiếng Việt.
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<Web.Features.Utilities.Services.UtilitySampleStateService>();
builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);  
var app = builder.Build();

app.UseSerilogRequestLogging();
// map loi 413 cua Kestrel/multipart ve cung cau truc JSON FileSizeExceeded.
app.UseMiddleware<FileUploadExceptionMiddleware>();
app.Logger.LogInformation("Web vận hành nhà trọ đã khởi động");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // loi ngoai du kien o /api tra JSON an toan, khong lo chi tiet/stack trace.
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/api"),
        apiApp => apiApp.UseMiddleware<ApiExceptionHandlerMiddleware>());

    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRequestLocalization();

// gioi han tan suat (global chi ap cho POST dang nhap; policy rieng gan theo endpoint).
app.UseRateLimiter();

app.UseAntiforgery();

// endpoint cho selector cua Phu.
// Dat cookie chuan ".AspNetCore.Culture", song sau reload/dang nhap (1 nam).
// Chi nhan vi-VN/en-US; tien VND va logic tinh khong doi theo culture.
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
// endpoint tep dung chung, da kiem quyen trong service va chan Admin o policy "Business".
app.MapFileEndpoints();
// health live/ready (an danh, chi tra trang thai).
app.MapHealthEndpoints();
// VHT-26: endpoint giay to P3 (metadata, upload, tai, thay, xoa mem) co kiem pham vi ho so.
app.MapTenantDocumentEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// gieo role, Admin dau tien tu secret, roi den user mau dev.
using (var scope = app.Services.CreateScope())
{
    try
    {
        await RoleSeeder.EnsureAsync(scope.ServiceProvider);
        await AdminSeeder.EnsureAsync(scope.ServiceProvider, app.Configuration, app.Logger);
        if (app.Environment.IsDevelopment())
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

// VHT-14: WebApplicationFactory can resolve the entry-point assembly for HTTP/auth integration tests.
public partial class Program { }
