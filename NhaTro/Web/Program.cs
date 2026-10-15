using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Components;
using Web.Components.Account;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Web.Data;
using Web.Data.Seeding;
using Web.Components.Shared.Feedback;
using Web.Features.Accounts;
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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
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
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);  
var app = builder.Build();

app.UseSerilogRequestLogging();
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
