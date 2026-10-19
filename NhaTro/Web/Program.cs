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
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Tenants;
using Web.Features.Storage.Services;
using Web.Infrastructure.ErrorHandling;
using Web.Infrastructure.Storage;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
var builder = WebApplication.CreateBuilder(args);

var logDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "NhaTro",
    "logs");

Directory.CreateDirectory(logDirectory);

// log ở đây nè, công dụng của serilog đó.
builder.Services.AddSerilog(config => config
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDirectory, "web-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// VHT-13: endpoint /api tra 401/403 dung hop dong VHT-12 muc 5, khong chuyen huong trang dang nhap.
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
        // VHT-8: tam thoi tat xac thuc email (chua cau hinh SMTP - lam sau).
        // Bat lai khi co IEmailSender that, neu khong tai khoan do quan tri tao se khong dang nhap duoc.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // Do manh mat khau theo dac ta VHT-8: toi thieu 8 ky tu, co chu hoa, chu thuong, so.
        // Dac ta KHONG yeu cau ky tu dac biet nen tat de tranh bao loi kho hieu.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        // Khoa tam thoi khi dang nhap sai nhieu lan (khac voi khoa tai khoan do quan tri - D1).
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        // Ten dang nhap = email (VHT-8): khong cho trung email.
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    // Nghiep vu P2-P8: tu choi Admin ngay tu endpoint, service goi BusinessGuard de chan them lop 2.
    options.AddPolicy("Business", p => p.RequireRole(Roles.ChuTro, Roles.QuanLy, Roles.KhachThue));
});
builder.Services.AddScoped<RoleService>();
// VHT-8: service tai khoan P1 (ho so ca nhan + quan tri tai khoan theo D1).
builder.Services.AddScoped<IAccountService, AccountService>();

// VHT-7 (dot 1C): service nha tro/phong P2. Policy cong tac va kiem hop dong la diem goi cho VHT-94/VHT-98.
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IRoomChangePolicy, RoomChangePolicyStub>();
builder.Services.AddScoped<IRoomUsageChecker, NoActiveContractRoomUsageChecker>();

// VHT-23 (dot 1C): service khach thue P3, kiem quyen/pham vi trong service.
builder.Services.AddScoped<ITenantService, TenantService>();

// VHT-23: chan client gui truong la ngoai DTO (do truong nhay cam nhu UserId/OwnerId tu form).
// Ap cho JSON cua HTTP endpoint; Blazor Server goi service noi bo (khong qua JSON) nen khong anh huong.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling =
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);

// VHT-13: storage dung chung. Gioi han doc tu cau hinh, kiem tra ngay khi khoi dong.
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<FileService>();

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
    // Thu tu: cookie cua selector -> Accept-Language cua trinh duyet.
});

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);  
var app = builder.Build();

app.UseSerilogRequestLogging();
// VHT-13: map loi 413 cua Kestrel/multipart ve cung cau truc JSON FileSizeExceeded.
app.UseMiddleware<FileUploadExceptionMiddleware>();
app.Logger.LogInformation("Web vận hành nhà trọ đã khởi động");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRequestLocalization();

app.UseAntiforgery();

// VHT-6 (3d): endpoint cho selector cua Phu (VHT-20).
// Dat cookie chuan ".AspNetCore.Culture", song sau reload/dang nhap (1 nam).
// Chi nhan vi-VN/en-US; tien VND va logic tinh khong doi theo culture (VHT-10).
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
});

app.MapGet("/culture/current", (HttpContext http) =>
{
    var feature = http.Features.Get<IRequestCultureFeature>();
    var current = feature?.RequestCulture.Culture.Name ?? "vi-VN";
    return Results.Ok(new { current, supported = new[] { "vi-VN", "en-US" } });
});

app.MapStaticAssets();
// VHT-13: endpoint tep dung chung, da kiem quyen trong service va chan Admin o policy "Business".
app.MapFileEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// VHT-6 (3a/3b): gieo role, Admin dau tien tu secret, roi den user mau dev.
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
