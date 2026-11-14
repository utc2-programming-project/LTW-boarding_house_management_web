using Microsoft.AspNetCore.Components.Authorization;
using Web.Common.Utils;
using Web.Features.Accounts;
using Web.Features.Accounts.DTOs;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Dashboard.Models;
using Web.Features.Dashboard.Services;
using Web.Features.Home.DTOs;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Models;
using Web.Features.Maintenance.Services;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Infrastructure.Time;

namespace Web.Features.Home.Services;

/// <summary>
/// Trang chủ: ảnh chụp nhanh theo vai trò, chỉ gọi nguồn dữ liệu mà vai trò được phép xem.
/// Mỗi khối tự chịu lỗi riêng để một nguồn hỏng không làm trắng cả trang.
/// </summary>
public sealed class HomePageService(
    AuthenticationStateProvider authentication,
    P2CallerProvider callers,
    DashboardPageService dashboard,
    RevenueReportService revenue,
    ManagerApprovalService approvals,
    IncidentPageService incidents,
    ContractPageService contracts,
    IInvoiceQueryService invoices,
    IAccountService accounts,
    TimeProvider time,
    ILogger<HomePageService> logger)
{
    private const int ExpiringWindowDays = 30;

    /// <summary>Link danh sách hoá đơn chưa thu (dùng cho KPI + mục "Việc cần làm").</summary>
    private const string UnpaidInvoicesHref = "/my/invoices?paymentState=unpaid";

    public async Task<HomeSnapshot> LoadAsync(CancellationToken ct = default, bool includeManagementKpis = true)
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        var role = Roles.ResolvePrimaryRole(principal);
        if (string.IsNullOrEmpty(role))
        {
            throw new UnauthorizedAccessException("A supported role is required for the home page.");
        }

        var caller = await callers.GetCallerAsync();
        var snapshot = role switch
        {
            Roles.ChuTro => await LoadOwnerAsync(caller, includeManagementKpis, ct),
            Roles.QuanLy => await LoadManagerAsync(caller, includeManagementKpis, ct),
            Roles.Admin => await LoadAdminAsync(caller, ct),
            Roles.KhachThue => await LoadTenantAsync(caller, ct),
            _ => throw new UnauthorizedAccessException("A supported role is required for the home page."),
        };

        return snapshot with { DisplayName = await LoadDisplayNameAsync(caller.UserId, ct) };
    }

    private async Task<string?> LoadDisplayNameAsync(string userId, CancellationToken ct)
    {
        try
        {
            var profile = await accounts.GetMyProfileAsync(userId, ct);
            return profile.Succeeded ? profile.Value?.FullName : null;
        }
        catch (Exception ex)
        {
            // Ten ho so la phu tro: loi o day khong duoc lam hong ca trang chu.
            logger.LogWarning(ex, "Home profile lookup failed");
            return null;
        }
    }

    private async Task<HomeSnapshot> LoadOwnerAsync(PropertyCaller caller, bool includeKpis, CancellationToken ct)
    {
        var today = time.TodayVietnam();
        var month = DashboardPeriodRange.Resolve(DashboardPeriod.Month, time);

        var rooms = includeKpis
            ? await TryAsync("rooms", () => dashboard.GetRoomKpisAsync(null, ct), (RoomReportKpis?)null)
            : (State: HomeLoadState.Loaded, Value: (RoomReportKpis?)null);
        var tenants = includeKpis
            ? await TryAsync("tenants", () => dashboard.GetTenantKpisAsync(null, ct), (TenantReportKpis?)null)
            : (State: HomeLoadState.Loaded, Value: (TenantReportKpis?)null);
        var revenueReport = includeKpis
            ? await TryAsync("revenue", () => revenue.GetAsync(new RevenueReportFilter(month.Start, month.End), caller, ct), (RevenueReportDto?)null)
            : (State: HomeLoadState.Loaded, Value: (RevenueReportDto?)null);
        var invoiceList = await TryAsync("invoices",
            () => invoices.GetInvoicesAsync(new InvoiceFilter { Page = 1, PageSize = 20 }, caller, ct),
            (InvoiceListResult?)null);
        var inbox = await TryAsync("approvals",
            () => approvals.ListInboxAsync(new ApprovalCaller(caller.UserId, caller.Role), ct),
            (ApprovalListResult?)null);
        var openIncidents = await TryCountAsync("incidents", () => incidents.CountOpenAsync());
        var contractSnapshot = await TryAsync("contracts", () => contracts.GetHomeSnapshotAsync(today, ct),
            (ContractHomeSnapshotDto?)null);
        var contractRows = (State: contractSnapshot.State, Value: contractSnapshot.Value?.Expiring ?? []);

        var kpis = new List<HomeKpi>();
        if (includeKpis)
        {
            if (rooms.Value is { } roomKpis)
            {
                kpis.Add(new("HomeKpiRented", Count(roomKpis.Rented),
                    "HomeKpiTotalRooms", Count(roomKpis.Total), "/rooms"));
                kpis.Add(new("HomeKpiAvailable", Count(roomKpis.Available),
                    "HomeKpiMaintenance", Count(roomKpis.Maintenance), "/rooms"));
            }
            else
            {
                AddUnavailable(kpis, "HomeKpiRented", "HomeKpiAvailable");
            }

            if (tenants.Value is { } tenantKpis)
            {
                kpis.Add(new("HomeKpiTenants", Count(tenantKpis.Renting),
                    "HomeKpiTenantTotal", Count(tenantKpis.Total), "/tenants"));
            }
            else
            {
                AddUnavailable(kpis, "HomeKpiTenants");
            }

            if (revenueReport.Value is { } revenueData)
            {
                kpis.Add(new("HomeKpiRevenue", MoneyFormatter.Format(revenueData.TotalRevenue),
                    "HomeKpiOutstanding", MoneyFormatter.Format(revenueData.Outstanding)));
            }
            else
            {
                AddUnavailable(kpis, "HomeKpiRevenue");
            }
        }

        var actions = BuildManagerActions(invoiceList.Value, inbox.Value, contractRows.Value, openIncidents.Value, today);

        return new HomeSnapshot(
            Roles.ChuTro,
            KpiSection(kpis, rooms.State, tenants.State, revenueReport.State),
            ActionSection(actions, invoiceList.State, inbox.State, contractRows.State, openIncidents.State),
            ExpiringSection(contractRows, today),
            OpenInvoicesSection(invoiceList.Value, invoiceList.State),
            new([], HomeLoadState.Loaded));
    }

    private async Task<HomeSnapshot> LoadManagerAsync(PropertyCaller caller, bool includeKpis, CancellationToken ct)
    {
        var today = time.TodayVietnam();

        var rooms = includeKpis
            ? await TryAsync("rooms", () => dashboard.GetRoomKpisAsync(null, ct), (RoomReportKpis?)null)
            : (State: HomeLoadState.Loaded, Value: (RoomReportKpis?)null);
        var tenants = includeKpis
            ? await TryAsync("tenants", () => dashboard.GetTenantKpisAsync(null, ct), (TenantReportKpis?)null)
            : (State: HomeLoadState.Loaded, Value: (TenantReportKpis?)null);
        var invoiceList = await TryAsync("invoices",
            () => invoices.GetInvoicesAsync(new InvoiceFilter { Page = 1, PageSize = 20 }, caller, ct),
            (InvoiceListResult?)null);
        var mine = await TryAsync("approvals",
            () => approvals.ListMineAsync(new ApprovalCaller(caller.UserId, caller.Role), ct),
            (ApprovalListResult?)null);
        var openIncidents = await TryCountAsync("incidents", () => incidents.CountOpenAsync());
        var contractSnapshot = await TryAsync("contracts", () => contracts.GetHomeSnapshotAsync(today, ct),
            (ContractHomeSnapshotDto?)null);
        var contractRows = (State: contractSnapshot.State, Value: contractSnapshot.Value?.Expiring ?? []);

        // Quản lý không xem doanh thu/công nợ tổng hợp (VHT-40 D15).
        var kpis = new List<HomeKpi>();
        if (includeKpis)
        {
            if (rooms.Value is { } roomKpis)
            {
                kpis.Add(new("HomeKpiRented", Count(roomKpis.Rented),
                    "HomeKpiTotalRooms", Count(roomKpis.Total), "/rooms"));
                kpis.Add(new("HomeKpiAvailable", Count(roomKpis.Available),
                    "HomeKpiMaintenance", Count(roomKpis.Maintenance), "/rooms"));
            }
            else
            {
                AddUnavailable(kpis, "HomeKpiRented", "HomeKpiAvailable");
            }

            if (tenants.Value is { } tenantKpis)
            {
                kpis.Add(new("HomeKpiTenants", Count(tenantKpis.Renting),
                    "HomeKpiTenantTotal", Count(tenantKpis.Total), "/tenants"));
            }
            else
            {
                AddUnavailable(kpis, "HomeKpiTenants");
            }

            if (contractSnapshot.State == HomeLoadState.Loaded)
            {
                kpis.Add(new("HomeKpiExpiring", Count(contractSnapshot.Value?.ExpiringCount ?? 0),
                    "HomeKpiExpiringHint", null, "/contracts"));
            }
            else
            {
                AddUnavailable(kpis, "HomeKpiExpiring");
            }
        }

        var actions = BuildManagerActions(invoiceList.Value, mine.Value, contractRows.Value, openIncidents.Value, today);

        return new HomeSnapshot(
            Roles.QuanLy,
            KpiSection(kpis, rooms.State, tenants.State, contractRows.State),
            ActionSection(actions, invoiceList.State, mine.State, contractRows.State, openIncidents.State),
            ExpiringSection(contractRows, today),
            OpenInvoicesSection(invoiceList.Value, invoiceList.State),
            new([], HomeLoadState.Loaded));
    }

    private async Task<HomeSnapshot> LoadAdminAsync(PropertyCaller caller, CancellationToken ct)
    {
        var summary = await TryAsync("accounts",
            () => accounts.GetSummaryAsync(caller.UserId, caller.Role, ct),
            AccountSummaryDto.Empty);

        var kpis = new List<HomeKpi>();
        var actions = new List<HomeAction>();
        var pending = new List<HomeAccountItem>();

        if (summary.State == HomeLoadState.Loaded)
        {
            var data = summary.Value;
            kpis.Add(new("HomeKpiAccountActive", Count(data.Active), Href: "/admin/users"));
            kpis.Add(new("HomeKpiAccountPending", Count(data.Pending), Href: "/admin/users"));
            kpis.Add(new("HomeKpiAccountLocked", Count(data.Locked), Href: "/admin/users"));
            kpis.Add(new("HomeKpiAccountTotal", Count(data.Total), Href: "/admin/users"));
            actions.Add(new("HomeActionPendingAccounts", data.Pending, "/admin/users"));
            actions.Add(new("HomeActionLockedAccounts", data.Locked, "/admin/users"));
            pending.AddRange(data.PendingAccounts.Select(account =>
                new HomeAccountItem(account.FullName ?? account.Email ?? account.UserName, account.Role)));
        }

        return new HomeSnapshot(
            Roles.Admin,
            new(kpis, summary.State),
            new(actions, summary.State),
            new([], HomeLoadState.Loaded),
            new([], HomeLoadState.Loaded),
            new(pending, summary.State));
    }

    private async Task<HomeSnapshot> LoadTenantAsync(PropertyCaller caller, CancellationToken ct)
    {
        var today = time.TodayVietnam();

        var invoiceList = await TryAsync("invoices",
            () => invoices.GetInvoicesAsync(new InvoiceFilter { Page = 1, PageSize = 20 }, caller, ct),
            (InvoiceListResult?)null);
        var openIncidents = await TryCountAsync("incidents", () => incidents.CountOpenAsync());
        var contractSnapshot = await TryAsync("contracts", () => contracts.GetHomeSnapshotAsync(today, ct),
            (ContractHomeSnapshotDto?)null);
        var contractRows = (State: contractSnapshot.State, Value: contractSnapshot.Value?.Expiring ?? []);

        List<ContractListItemDto> activeContracts = contractSnapshot.Value?.ActiveForTenant
            .OrderBy(contract => contract.EndDate)
            .ToList() ?? [];
        var earliestEnd = activeContracts.Count == 0 ? (DateOnly?)null : DateOnly.FromDateTime(activeContracts[0].EndDate);
        var openIncidentCount = openIncidents.Value ?? 0;

        var kpis = new List<HomeKpi>();
        if (invoiceList.Value is { } invoiceResult)
        {
            kpis.Add(new("HomeKpiUnpaidInvoices", Count(invoiceResult.Summary.IssuedCount), Href: UnpaidInvoicesHref));
            kpis.Add(new("HomeKpiOutstandingAmount", MoneyFormatter.Format(invoiceResult.Summary.OutstandingAmount),
                Href: UnpaidInvoicesHref));
        }
        else
        {
            AddUnavailable(kpis, "HomeKpiUnpaidInvoices", "HomeKpiOutstandingAmount");
        }

        if (openIncidents.State == HomeLoadState.Loaded)
        {
            kpis.Add(new("HomeKpiOpenIncidents", Count(openIncidentCount), Href: "/incidents"));
        }
        else
        {
            AddUnavailable(kpis, "HomeKpiOpenIncidents");
        }

        if (contractRows.State == HomeLoadState.Loaded)
        {
            kpis.Add(earliestEnd is { } end
                ? new HomeKpi("HomeKpiContractDaysLeft", Count(end.DayNumber - today.DayNumber),
                    "HomeKpiContractEnd", DateFormatter.Format(end), "/contracts")
                : new HomeKpi("HomeKpiContractDaysLeft", "—", "HomeKpiNoContract", null, "/contracts"));
        }
        else
        {
            AddUnavailable(kpis, "HomeKpiContractDaysLeft");
        }

        var actions = new List<HomeAction>();
        if (invoiceList.Value is { } invoiceResultForActions)
        {
            actions.Add(new("HomeActionUnpaidInvoices", invoiceResultForActions.Summary.IssuedCount, UnpaidInvoicesHref));
        }

        if (openIncidents.State == HomeLoadState.Loaded)
        {
            actions.Add(new("HomeActionOpenIncidents", openIncidentCount, "/incidents"));
        }

        if (contractRows.State == HomeLoadState.Loaded)
        {
            actions.Add(new("HomeActionExpiringContracts", Expiring(contractRows.Value ?? [], today).Count, "/contracts"));
        }

        var expiringItems = activeContracts
            .Where(contract => DateOnly.FromDateTime(contract.EndDate) <= today.AddDays(ExpiringWindowDays))
            .Take(5)
            .Select(contract => ToContractItem(contract, today))
            .ToList();

        return new HomeSnapshot(
            Roles.KhachThue,
            KpiSection(kpis, invoiceList.State, openIncidents.State, contractRows.State),
            ActionSection(actions, invoiceList.State, openIncidents.State, contractRows.State),
            new(expiringItems, contractRows.State),
            OpenInvoicesSection(invoiceList.Value, invoiceList.State),
            new([], HomeLoadState.Loaded));
    }

    private static List<HomeAction> BuildManagerActions(
        InvoiceListResult? invoiceList,
        ApprovalListResult? approvalList,
        IReadOnlyList<ContractListItemDto>? contractRows,
        int? openIncidentCount,
        DateOnly today)
    {
        var actions = new List<HomeAction>();
        if (invoiceList is not null)
        {
            actions.Add(new("HomeActionUnpaidInvoices", invoiceList.Summary.IssuedCount, "/invoices"));
        }

        if (approvalList is { Succeeded: true })
        {
            var pending = approvalList.Items.Count(item => item.Status == ApprovalRequestStatus.Pending);
            actions.Add(new("HomeActionPendingApprovals", pending, "/approvals"));
        }

        if (contractRows is not null)
        {
            actions.Add(new("HomeActionExpiringContracts", Expiring(contractRows, today).Count, "/contracts"));
        }

        if (openIncidentCount is { } incidentCount)
        {
            actions.Add(new("HomeActionOpenIncidents", incidentCount, "/incidents"));
        }

        return actions;
    }

    private static HomeSection<HomeKpi> KpiSection(List<HomeKpi> items, params HomeLoadState[] states) =>
        new(items, items.Count > 0 ? HomeLoadState.Loaded : Combine(states));

    private static HomeSection<HomeAction> ActionSection(List<HomeAction> items, params HomeLoadState[] states) =>
        new(items, items.Count > 0 ? HomeLoadState.Loaded : Combine(states));

    private static HomeSection<HomeContractItem> ExpiringSection(
        (HomeLoadState State, IReadOnlyList<ContractListItemDto> Value) source, DateOnly today) =>
        new(source.Value is null ? [] : Expiring(source.Value, today).Take(5).Select(contract => ToContractItem(contract, today)).ToList(),
            source.State);

    private static HomeSection<HomeInvoiceItem> OpenInvoicesSection(InvoiceListResult? invoiceList, HomeLoadState state) =>
        new(invoiceList is null
                ? []
                : invoiceList.Items
                    .Where(invoice => invoice.Status == InvoiceStatus.Issued)
                    .Take(5)
                    .Select(invoice => new HomeInvoiceItem(
                        invoice.Id,
                        invoice.RoomCode,
                        invoice.PropertyName,
                        invoice.PeriodStart,
                        invoice.PeriodEnd,
                        invoice.OutstandingAmount))
                    .ToList(),
            invoiceList is null ? state : HomeLoadState.Loaded);

    private static List<ContractListItemDto> Expiring(IReadOnlyList<ContractListItemDto> rows, DateOnly today) =>
        rows.Where(contract => contract.Status == ContractStatus.Approved
                && DateOnly.FromDateTime(contract.EndDate) >= today
                && DateOnly.FromDateTime(contract.EndDate) <= today.AddDays(ExpiringWindowDays))
            .OrderBy(contract => contract.EndDate)
            .ToList();

    private static HomeContractItem ToContractItem(ContractListItemDto contract, DateOnly today)
    {
        var end = DateOnly.FromDateTime(contract.EndDate);
        return new HomeContractItem(
            contract.RoomCode,
            contract.PropertyName,
            string.Join(", ", contract.TenantNames),
            end,
            end.DayNumber - today.DayNumber);
    }

    private static string Count(int value) => value.ToString("N0", NumberFormatter.Display);

    private static void AddUnavailable(List<HomeKpi> kpis, params string[] keys) =>
        kpis.AddRange(keys.Select(key => new HomeKpi(key, "—", Unavailable: true)));

    private static HomeLoadState Combine(IReadOnlyList<HomeLoadState> states) =>
        states.Contains(HomeLoadState.Error) ? HomeLoadState.Error
        : states.Contains(HomeLoadState.Denied) ? HomeLoadState.Denied
        : HomeLoadState.Loaded;

    private async Task<(HomeLoadState State, int? Value)> TryCountAsync(string scope, Func<Task<int>> load)
    {
        try
        {
            return (HomeLoadState.Loaded, await load());
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Home section {Scope} denied", scope);
            return (HomeLoadState.Denied, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Home section {Scope} failed", scope);
            return (HomeLoadState.Error, null);
        }
    }

    private async Task<(HomeLoadState State, T Value)> TryAsync<T>(
        string scope, Func<Task<T>> load, T? fallback) where T : class
    {
        try
        {
            return (HomeLoadState.Loaded, await load());
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Home section {Scope} denied", scope);
            return (HomeLoadState.Denied, fallback!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Home section {Scope} failed", scope);
            return (HomeLoadState.Error, fallback!);
        }
    }
}
