using System.Reflection;
using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Web.Common.Models;
using Web.Components.Shared.Feedback;
using Web.Configuration.Options;
using Web.Features.Accounts;
using Web.Features.Contracts.Services;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Maintenance.Services;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Storage.Services;
using Web.Features.Tenants.Documents;
using Web.Infrastructure.Storage;

namespace NhaTro.UnitTests.TestSupport;

/// <summary>
/// bUnit chỉ xử lý IAsyncDisposable đúng khi test được dispose bất đồng bộ; lớp cha này
/// đảm bảo DI async (PaymentRecoveryService) không bị dispose đồng bộ trong lúc dọn test.
/// </summary>
public abstract class P6RecoveryBunitContext : BunitContext, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "Điều kiện không đạt trong thời gian chờ.");
    }
}

/// <summary>
/// VHT-143: dựng hai trang P6 thật với service đọc/ghi giả và JS module giả, để kiểm tra
/// marker tra cứu, khóa thao tác và việc reconnect chỉ gọi truy vấn đọc.
/// </summary>
public sealed class P6RecoveryBackend
{
    public const string ModulePath = "./js/payment-recovery.js";

    public List<int> DetailCalls { get; } = [];
    public List<int> HistoryCalls { get; } = [];
    public List<int> LatestCalls { get; } = [];
    public List<int> ConfirmCalls { get; } = [];
    public List<int> RejectCalls { get; } = [];
    public List<int> SubmitCalls { get; } = [];

    public Func<int, Task<PaymentReviewItemDto>> Detail { get; set; } =
        id => Task.FromResult(ReviewItem(P6RecoveryData.Payment(id, 1, PaymentStatus.Pending)));

    public Func<int, Task<PaymentDto?>> Latest { get; set; } =
        _ => Task.FromResult<PaymentDto?>(null);

    public Func<int, Task<IReadOnlyList<PaymentDto>>> History { get; set; } =
        _ => Task.FromResult<IReadOnlyList<PaymentDto>>([]);

    public Func<InvoiceDto> Invoice { get; set; } = P6RecoveryData.Invoice;

    public PaymentPageService CreatePaymentPage(AuthenticationStateProvider authentication) =>
        new(authentication,
            CreatePaymentService(),
            CreateDepositService(),
            CreateFileService(),
            Options.Create(new StorageOptions()));

    private static IContractDepositPaymentService CreateDepositService() =>
        Proxy<IContractDepositPaymentService>((method, _) =>
            throw new NotSupportedException(method!.Name));

    public PaymentReviewPageService CreateReviewPage(
        AuthenticationStateProvider authentication,
        IPaymentReviewQueryService reviewQuery) =>
        new(CreatePaymentPage(authentication), reviewQuery, CreatePaymentService());

    private IPaymentService CreatePaymentService() =>
        Proxy<IPaymentService>((method, args) => method!.Name switch
        {
            nameof(IPaymentService.GetLatestPaymentAsync) => Track(LatestCalls, (int)args![0]!, Latest),
            nameof(IPaymentService.GetPaymentsAsync) => Track(HistoryCalls, (int)args![0]!, History),
            nameof(IPaymentService.ConfirmAsync) => TrackConfirm((int)args![0]!),
            nameof(IPaymentService.RejectAsync) => TrackReject((int)args![0]!),
            nameof(IPaymentService.SubmitAsync) => TrackSubmit(args!),
            _ => throw new NotSupportedException(method.Name),
        });

    private Task<PaymentDto> TrackConfirm(int paymentId)
    {
        ConfirmCalls.Add(paymentId);
        return Task.FromResult(P6RecoveryData.Payment(paymentId, 1, PaymentStatus.Confirmed));
    }

    private Task<PaymentDto> TrackReject(int paymentId)
    {
        RejectCalls.Add(paymentId);
        return Task.FromResult(P6RecoveryData.Payment(paymentId, 1, PaymentStatus.Rejected));
    }

    private Task<PaymentDto> TrackSubmit(object?[] args)
    {
        var invoiceId = (int)args[0]!;
        SubmitCalls.Add(invoiceId);
        return Task.FromResult(P6RecoveryData.Payment(99, invoiceId, PaymentStatus.Pending));
    }

    private static object Track<T>(List<int> calls, int id, Func<int, Task<T>> handler)
    {
        calls.Add(id);
        return handler(id);
    }

    public IPaymentReviewQueryService CreateReviewQuery()
    {
        return Proxy<IPaymentReviewQueryService>((method, args) => method!.Name switch
        {
            nameof(IPaymentReviewQueryService.GetRequestAsync) => TrackDetail((int)args![0]!),
            _ => throw new NotSupportedException(method.Name),
        });
    }

    public IInvoiceQueryService CreateInvoiceQuery()
    {
        return Proxy<IInvoiceQueryService>((method, args) => method!.Name switch
        {
            nameof(IInvoiceQueryService.GetInvoiceAsync) => Task.FromResult(Invoice()),
            _ => throw new NotSupportedException(method.Name),
        });
    }

    private Task<PaymentReviewItemDto> TrackDetail(int paymentId)
    {
        DetailCalls.Add(paymentId);
        return Detail(paymentId);
    }

    public IInvoiceService CreateInvoiceService() =>
        Proxy<IInvoiceService>((method, _) => throw new NotSupportedException(method!.Name));

    public INotificationService CreateNotificationService() =>
        Proxy<INotificationService>((method, _) => throw new NotSupportedException(method!.Name));

    private static FileService CreateFileService() => new(
        new ThrowingDbContextFactory(),
        Proxy<IFileStorage>((_, _) => null),
        Options.Create(new StorageOptions()),
        TimeProvider.System,
        Proxy<ITenantDocumentScopeService>((_, _) => null),
        Proxy<IIncidentFileScope>((_, _) => null),
        Proxy<IFileAccessAuditWriter>((_, _) => null),
        NullLogger<FileService>.Instance);

    public static BunitJSModuleInterop Register(BunitContext context, AuthenticationStateProvider authentication,
        P6RecoveryBackend backend, PaymentReviewPageService? reviewPage = null,
        OwnerBankAccount? bankAccount = null)
    {
        context.Services.AddLogging();
        context.Services.AddLocalization(o => o.ResourcesPath = "Resources");
        context.Services.AddSingleton<AuthenticationStateProvider>(authentication);
        context.Services.AddSingleton(new ToastService());
        context.Services.AddSingleton<IOwnerBankAccountService>(new StubBankAccounts(bankAccount));
        context.Services.AddSingleton(backend.CreatePaymentPage(authentication));
        context.Services.AddSingleton(reviewPage ?? backend.CreateReviewPage(authentication, backend.CreateReviewQuery()));
        context.Services.AddTransient<PaymentRecoveryService>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = context.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        return module;
    }

    private sealed class StubBankAccounts(OwnerBankAccount? account) : IOwnerBankAccountService
    {
        public Task<OwnerBankAccount?> GetForUserAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(account);

        public Task<OwnerBankAccount?> GetForContractAsync(Guid contractId, CancellationToken ct = default) =>
            Task.FromResult(account);

        public Task<OwnerBankAccount?> GetForInvoiceAsync(int invoiceId, CancellationToken ct = default) =>
            Task.FromResult(account);
    }

    public static PaymentReviewItemDto ReviewItem(PaymentDto payment) => new()
    {
        Payment = payment,
        InvoiceId = payment.InvoiceId,
        ContractNumber = "HD-1",
        RoomCode = "P101",
        PropertyName = "House 1",
        TenantNames = ["Tenant"],
        PeriodStart = new DateOnly(2026, 1, 1),
        PeriodEnd = new DateOnly(2026, 1, 31),
        InvoiceStatus = InvoiceStatus.Issued,
        InvoiceTotal = payment.Amount,
    };

    private static T Proxy<T>(Func<MethodInfo?, object?[]?, object?> handler) where T : class
    {
        var instance = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)instance).Handler = handler;
        return instance;
    }

    public class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo?, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method, args);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<Web.Data.ApplicationDbContext>
    {
        public Web.Data.ApplicationDbContext CreateDbContext() =>
            throw new InvalidOperationException("Không dùng DB trong test đơn vị.");

        public Task<Web.Data.ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromException<Web.Data.ApplicationDbContext>(
                new InvalidOperationException("Không dùng DB trong test đơn vị."));
    }
}

public sealed class P6AuthProvider(string userId, string role) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
        new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(Roles.ActiveRoleClaim, role),
            ],
            "test"))));
}

public static class P6RecoveryData
{
    public const string UserId = "user-1";

    public static PaymentDto Payment(int id, int invoiceId, PaymentStatus status, string? rejectionReason = null) => new()
    {
        Id = id,
        InvoiceId = invoiceId,
        Amount = 1_000_000m,
        TransferredAt = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero),
        Status = status,
        ProofFileIds = [],
        RequestedAt = new DateTimeOffset(2026, 1, 5, 8, 5, 0, TimeSpan.Zero),
        RequestedByUserId = "tenant-1",
        RejectionReason = rejectionReason,
    };

    public static InvoiceDto Invoice() => new()
    {
        Id = 1,
        ContractId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ContractNumber = "HD-1",
        RoomCode = "P101",
        PropertyName = "House 1",
        TenantNames = ["Tenant"],
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        PeriodStart = new DateOnly(2026, 1, 1),
        PeriodEnd = new DateOnly(2026, 1, 31),
        Status = InvoiceStatus.Issued,
        Items = [],
        TotalAmount = 1_000_000m,
        PaidAmount = 0m,
        OutstandingAmount = 1_000_000m,
        LatestPayment = null,
    };
}
