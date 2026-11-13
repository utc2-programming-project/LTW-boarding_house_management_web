using Microsoft.AspNetCore.Components;
using System.Globalization;
using Web.Common.Utils;
using Web.Components.Layout;
using Web.Features.Accounts;
using Web.Features.Contracts.Enums;
using Web.Features.Dashboard.Models;
using Web.Features.Dashboard.Services;
using Web.Features.Maintenance.Enums;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Features.Rooms.Enums;

namespace Web.Features.Reports.Pages;

public partial class BusinessReportsView
{
    private const int PageSize = 20;

    public enum ReportTab { Rooms, Tenants, Contracts, Costs, Incidents, Revenue }

    [SupplyParameterFromQuery] public int? PropertyId { get; set; }
    [SupplyParameterFromQuery] public string? ExportError { get; set; }

    private ReportTab ActiveTab = ReportTab.Rooms;
    private IReadOnlyList<DashboardPropertyOption> Properties = [];
    private readonly HashSet<ReportTab> loadedTabs = [];
    private int? SelectedPropertyId;
    private DateOnly? From;
    private DateOnly? To;
    private bool DateRangeInvalid;
    private bool RoomActive = true;
    private RoomStatus? RoomStatusFilter;
    private bool? TenantActive;
    private bool? TenantRenting;
    private string? ContractKeyword;
    private ContractStatus? ContractStatusFilter;
    private IncidentStatus? IncidentStatusFilter;
    private IncidentCategory? IncidentCategoryFilter;
    private IncidentUrgency? IncidentUrgencyFilter;
    private int RoomPage = 1;
    private int TenantPage = 1;
    private int ContractPage = 1;
    private int CostPage = 1;
    private int IncidentPage = 1;
    private RevenueDateBasis RevenueDateBasis = RevenueDateBasis.InvoicePeriod;

    private RoomReportResult? RoomResult;
    private OccupancyReportResult? OccupancyResult;
    private TenantReportResult? TenantResult;
    private ContractReportResult? ContractResult;
    private CostReportResult? CostResult;
    private IncidentReportResult? IncidentResult;
    private RevenueReportDto? RevenueResult;

    private DashboardLoadState PropertyState = DashboardLoadState.Loading;
    private DashboardLoadState RoomState = DashboardLoadState.Loading;
    private DashboardLoadState TenantState = DashboardLoadState.Loading;
    private DashboardLoadState ContractState = DashboardLoadState.Loaded;
    private DashboardLoadState CostState = DashboardLoadState.Loaded;
    private DashboardLoadState IncidentState = DashboardLoadState.Loaded;
    private DashboardLoadState RevenueState = DashboardLoadState.Loaded;

    private bool IsOwner;
    private bool Refreshing;
    private DateTime? LastRefreshed;
    private readonly SemaphoreSlim reloadGate = new(1, 1);
    private int scopeVersion;

    private DashboardLoadState ActiveState => ActiveTab switch
    {
        ReportTab.Rooms => RoomState,
        ReportTab.Tenants => TenantState,
        ReportTab.Contracts => ContractState,
        ReportTab.Costs => CostState,
        ReportTab.Incidents => IncidentState,
        _ => RevenueState,
    };

    private bool Busy => Refreshing || PropertyState == DashboardLoadState.Loading
        || RoomState == DashboardLoadState.Loading || TenantState == DashboardLoadState.Loading
        || ActiveState == DashboardLoadState.Loading;

    private bool HasAnyError => new[]
    {
        RoomState, TenantState, ContractState, CostState, IncidentState, RevenueState,
    }.Contains(DashboardLoadState.Error);

    private RevenueReportFilter RevenueFilter => new(From, To, SelectedPropertyId, RevenueDateBasis);

    private (int From, int To) RoomItemRange =>
        PaginationHelper.ItemRange(RoomPage, PageSize, RoomResult?.Rooms.TotalCount ?? 0);

    private (int From, int To) TenantItemRange =>
        PaginationHelper.ItemRange(TenantPage, PageSize, TenantResult?.Tenants.TotalCount ?? 0);

    private (int From, int To) ContractItemRange =>
        PaginationHelper.ItemRange(ContractPage, PageSize, ContractResult?.Contracts.TotalCount ?? 0);

    private (int From, int To) CostItemRange =>
        PaginationHelper.ItemRange(CostPage, PageSize, CostResult?.Readings.TotalCount ?? 0);

    private (int From, int To) IncidentItemRange =>
        PaginationHelper.ItemRange(IncidentPage, PageSize, IncidentResult?.Incidents.TotalCount ?? 0);

    protected override async Task OnInitializedAsync()
    {
        IsOwner = await PageService.IsOwnerAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await ReloadAsync(false, readQuery: true);
        if (!string.IsNullOrWhiteSpace(ExportError))
        {
            Toasts.Error(ExportError switch
            {
                "Forbidden" => L["ExportDenied"],
                "ApprovalRequired" => L["ExportApprovalRequired"],
                "ApprovalDenied" => L["ExportApprovalDenied"],
                "VolumeExceeded" => L["ExportTooLarge"],
                _ => L["ExportFailed"],
            });
        }
    }

    private Task RefreshAsync() => ReloadAsync(true);

    private async Task ReloadAsync(bool notify, bool readQuery = false, bool refreshProperties = true)
    {
        // Xếp hàng khi điều hướng đổi query trong lúc một lần làm mới còn đang chạy.
        await reloadGate.WaitAsync();
        try
        {
            // Khoảng ngày sai chỉ hiển thị lỗi tại trường, không gửi bộ lọc sai xuống nguồn.
            if (DateRangeInvalid && !readQuery) return;
            if (readQuery)
            {
                SelectedPropertyId = PropertyId;
                ResetPages();
            }

            scopeVersion++;
            var version = scopeVersion;
            Refreshing = true;
            PropertyState = DashboardLoadState.Loading;
            RoomState = TenantState = DashboardLoadState.Loading;
            RoomResult = null;
            TenantResult = null;
            loadedTabs.Clear();
            try
            {
                if (refreshProperties || Properties.Count == 0)
                {
                    Properties = await PageService.ListPropertiesAsync();
                }

                if (SelectedPropertyId.HasValue && !Properties.Any(p => p.Id == SelectedPropertyId))
                {
                    PropertyState = DashboardLoadState.Denied;
                    RoomState = TenantState = DashboardLoadState.Denied;
                    return;
                }

                PropertyState = DashboardLoadState.Loaded;
                await LoadRoomsCoreAsync(version);
                await LoadTenantsCoreAsync(version);
                if (ActiveTab is not (ReportTab.Rooms or ReportTab.Tenants))
                {
                    await LoadTabCoreAsync(ActiveTab, version);
                }

                LastRefreshed = DateTime.UtcNow;
                if (notify && !HasAnyError)
                {
                    Toasts.Success(D["RefreshSuccess"]);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Properties = [];
                PropertyState = RoomState = TenantState = DashboardLoadState.Denied;
            }
            catch (Exception ex)
            {
                Properties = [];
                PropertyState = RoomState = TenantState = DashboardLoadState.Error;
                Logger.LogError(ex, "Report properties failed");
                Toasts.Error(D["Error"]);
            }
            finally
            {
                Refreshing = false;
            }
        }
        finally
        {
            reloadGate.Release();
        }
    }

    private async Task SelectReportAsync(ReportTab tab)
    {
        if (Busy || DateRangeInvalid) return;
        ActiveTab = tab;
        if (tab is ReportTab.Rooms or ReportTab.Tenants) return;
        if (!loadedTabs.Contains(tab))
        {
            await LoadTabCoreAsync(tab, scopeVersion);
        }
    }

    private async Task ChangePropertyAsync(ChangeEventArgs args)
    {
        if (Busy || DateRangeInvalid) return;
        SelectedPropertyId = int.TryParse(args.Value?.ToString(), out var id) ? id : null;
        ResetPages();
        await ReloadAsync(false, refreshProperties: false);
    }

    private Task ChangeFromAsync(ChangeEventArgs args)
    {
        if (Busy) return Task.CompletedTask;
        From = ParseDate(args.Value?.ToString());
        return ApplyDateFiltersAsync();
    }

    private Task ChangeToAsync(ChangeEventArgs args)
    {
        if (Busy) return Task.CompletedTask;
        To = ParseDate(args.Value?.ToString());
        return ApplyDateFiltersAsync();
    }

    private async Task ApplyDateFiltersAsync()
    {
        DateRangeInvalid = From.HasValue && To.HasValue && From > To;
        if (DateRangeInvalid) return;
        ResetPages();
        await ReloadAsync(false, refreshProperties: false);
    }

    private async Task ChangeRevenueDateBasisAsync(RevenueDateBasis basis)
    {
        if (Busy || DateRangeInvalid) return;
        RevenueDateBasis = basis;
        ResetPages();
        loadedTabs.Remove(ReportTab.Revenue);
        if (ActiveTab == ReportTab.Revenue)
        {
            await LoadTabCoreAsync(ReportTab.Revenue, scopeVersion);
        }
    }

    private async Task ClearFiltersAsync()
    {
        if (Busy) return;
        SelectedPropertyId = null;
        From = To = null;
        DateRangeInvalid = false;
        RoomActive = true;
        RoomStatusFilter = null;
        TenantActive = TenantRenting = null;
        ContractKeyword = null;
        ContractStatusFilter = null;
        IncidentStatusFilter = null;
        IncidentCategoryFilter = null;
        IncidentUrgencyFilter = null;
        RevenueDateBasis = RevenueDateBasis.InvoicePeriod;
        ResetPages();
        await ReloadAsync(false, refreshProperties: false);
    }

    private async Task ClearRoomFiltersAsync()
    {
        if (Busy) return;
        RoomActive = true;
        RoomStatusFilter = null;
        RoomPage = 1;
        await LoadRoomsAsync();
    }

    private async Task ClearTenantFiltersAsync()
    {
        if (Busy) return;
        TenantActive = TenantRenting = null;
        TenantPage = 1;
        await LoadTenantsAsync();
    }

    private async Task ClearContractFiltersAsync()
    {
        if (Busy) return;
        ContractKeyword = null;
        ContractStatusFilter = null;
        ContractPage = 1;
        await LoadContractsAsync();
    }

    private async Task ClearIncidentFiltersAsync()
    {
        if (Busy) return;
        IncidentStatusFilter = null;
        IncidentCategoryFilter = null;
        IncidentUrgencyFilter = null;
        IncidentPage = 1;
        await LoadIncidentsAsync();
    }

    private async Task ChangeRoomFiltersAsync()
    {
        if (Busy || DateRangeInvalid) return;
        RoomPage = 1;
        await LoadRoomsAsync();
    }

    private async Task ChangeRoomActivityAsync(ChangeEventArgs args)
    {
        if (Busy || DateRangeInvalid || !bool.TryParse(args.Value?.ToString(), out var active)) return;
        RoomActive = active;
        await ChangeRoomFiltersAsync();
    }

    private async Task ChangeTenantActivityAsync(ChangeEventArgs args)
    {
        if (Busy || DateRangeInvalid) return;
        var text = args.Value?.ToString();
        bool? active = null;
        if (!string.IsNullOrEmpty(text))
        {
            if (!bool.TryParse(text, out var value)) return;
            active = value;
        }

        TenantActive = active;
        TenantPage = 1;
        await LoadTenantsAsync();
    }

    private async Task ChangeTenantRentalAsync(ChangeEventArgs args)
    {
        if (Busy || DateRangeInvalid) return;
        var text = args.Value?.ToString();
        bool? renting = null;
        if (!string.IsNullOrEmpty(text))
        {
            if (!bool.TryParse(text, out var value)) return;
            renting = value;
        }

        TenantRenting = renting;
        TenantPage = 1;
        await LoadTenantsAsync();
    }

    private async Task ChangeContractFiltersAsync()
    {
        if (Busy || DateRangeInvalid) return;
        ContractPage = 1;
        await LoadContractsAsync();
    }

    private async Task ChangeIncidentFiltersAsync()
    {
        if (Busy || DateRangeInvalid) return;
        IncidentPage = 1;
        await LoadIncidentsAsync();
    }

    private async Task ChangeRoomPageAsync(int page)
    {
        if (Busy || DateRangeInvalid || RoomResult is null) return;
        RoomPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(RoomResult.Rooms.TotalCount, PageSize));
        await LoadRoomsAsync();
    }

    private async Task ChangeTenantPageAsync(int page)
    {
        if (Busy || DateRangeInvalid || TenantResult is null) return;
        TenantPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(TenantResult.Tenants.TotalCount, PageSize));
        await LoadTenantsAsync();
    }

    private async Task ChangeContractPageAsync(int page)
    {
        if (Busy || DateRangeInvalid || ContractResult is null) return;
        ContractPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(ContractResult.Contracts.TotalCount, PageSize));
        await LoadContractsAsync();
    }

    private async Task ChangeCostPageAsync(int page)
    {
        if (Busy || DateRangeInvalid || CostResult is null) return;
        CostPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(CostResult.Readings.TotalCount, PageSize));
        await LoadCostsAsync();
    }

    private async Task ChangeIncidentPageAsync(int page)
    {
        if (Busy || DateRangeInvalid || IncidentResult is null) return;
        IncidentPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(IncidentResult.Incidents.TotalCount, PageSize));
        await LoadIncidentsAsync();
    }

    private Task RetryRoomsAsync() => Busy ? Task.CompletedTask
        : PropertyState == DashboardLoadState.Loaded ? LoadRoomsAsync() : ReloadAsync(false);

    private Task RetryTenantsAsync() => Busy ? Task.CompletedTask
        : PropertyState == DashboardLoadState.Loaded ? LoadTenantsAsync() : ReloadAsync(false);

    private Task RetryContractsAsync() => Busy || DateRangeInvalid ? Task.CompletedTask : LoadContractsAsync();

    private Task RetryCostsAsync() => Busy || DateRangeInvalid ? Task.CompletedTask : LoadCostsAsync();

    private Task RetryIncidentsAsync() => Busy || DateRangeInvalid ? Task.CompletedTask : LoadIncidentsAsync();

    private Task RetryRevenueAsync() => Busy || DateRangeInvalid ? Task.CompletedTask : LoadRevenueAsync();

    private async Task LoadRoomsAsync()
    {
        var version = ++scopeVersion;
        await LoadRoomsCoreAsync(version);
    }

    private async Task LoadRoomsCoreAsync(int version)
    {
        RoomState = DashboardLoadState.Loading;
        RoomResult = null;
        OccupancyResult = null;
        try
        {
            RoomReportResult result;
            do
            {
                result = await PageService.GetRoomReportAsync(new RoomReportFilter
                {
                    PropertyId = SelectedPropertyId, IsActive = RoomActive, Status = RoomStatusFilter,
                    Page = RoomPage, PageSize = PageSize,
                });
                if (version != scopeVersion) return;
                var lastPage = PaginationHelper.TotalPages(result.Rooms.TotalCount, PageSize);
                if (RoomPage <= lastPage) break;
                RoomPage = lastPage;
            } while (true);

            var occupancy = await PageService.GetOccupancyAsync(
                new OccupancyFilter(From, To, SelectedPropertyId));
            if (version != scopeVersion) return;

            RoomResult = result;
            OccupancyResult = occupancy;
            RoomState = DashboardLoadState.Loaded;
        }
        catch (UnauthorizedAccessException) when (version == scopeVersion) { RoomState = DashboardLoadState.Denied; }
        catch (Exception ex)
        {
            if (version != scopeVersion) return;
            RoomState = DashboardLoadState.Error;
            Logger.LogError(ex, "Room report failed");
            Toasts.Error(D["Error"]);
        }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private async Task LoadTenantsAsync()
    {
        var version = ++scopeVersion;
        await LoadTenantsCoreAsync(version);
    }

    private async Task LoadTenantsCoreAsync(int version)
    {
        TenantState = DashboardLoadState.Loading;
        TenantResult = null;
        try
        {
            TenantReportResult result;
            do
            {
                result = await PageService.GetTenantReportAsync(new TenantReportFilter
                {
                    PropertyId = SelectedPropertyId, IsActive = TenantActive, HasActiveContract = TenantRenting,
                    Page = TenantPage, PageSize = PageSize,
                });
                if (version != scopeVersion) return;
                var lastPage = PaginationHelper.TotalPages(result.Tenants.TotalCount, PageSize);
                if (TenantPage <= lastPage) break;
                TenantPage = lastPage;
            } while (true);

            TenantResult = result;
            TenantState = DashboardLoadState.Loaded;
        }
        catch (UnauthorizedAccessException) when (version == scopeVersion) { TenantState = DashboardLoadState.Denied; }
        catch (Exception ex)
        {
            if (version != scopeVersion) return;
            TenantState = DashboardLoadState.Error;
            Logger.LogError(ex, "Tenant report failed");
            Toasts.Error(D["Error"]);
        }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private async Task LoadTabCoreAsync(ReportTab tab, int version)
    {
        switch (tab)
        {
            case ReportTab.Contracts: await LoadContractsCoreAsync(version); break;
            case ReportTab.Costs: await LoadCostsCoreAsync(version); break;
            case ReportTab.Incidents: await LoadIncidentsCoreAsync(version); break;
            case ReportTab.Revenue: await LoadRevenueCoreAsync(version); break;
        }
    }

    private Task LoadContractsAsync() => LoadContractsCoreAsync(++scopeVersion);

    private async Task LoadContractsCoreAsync(int version)
    {
        ContractState = DashboardLoadState.Loading;
        ContractResult = null;
        try
        {
            ContractReportResult result;
            do
            {
                result = await PageService.GetContractReportAsync(new ContractReportFilter
                {
                    From = From, To = To, PropertyId = SelectedPropertyId, Status = ContractStatusFilter,
                    Keyword = ContractKeyword, Page = ContractPage, PageSize = PageSize,
                });
                if (version != scopeVersion) return;
                var lastPage = PaginationHelper.TotalPages(result.Contracts.TotalCount, PageSize);
                if (ContractPage <= lastPage) break;
                ContractPage = lastPage;
            } while (true);

            ContractResult = result;
            ContractState = DashboardLoadState.Loaded;
            loadedTabs.Add(ReportTab.Contracts);
        }
        catch (UnauthorizedAccessException) when (version == scopeVersion) { ContractState = DashboardLoadState.Denied; }
        catch (Exception ex)
        {
            if (version != scopeVersion) return;
            ContractState = DashboardLoadState.Error;
            Logger.LogError(ex, "Contract report failed");
            Toasts.Error(D["Error"]);
        }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private Task LoadCostsAsync() => LoadCostsCoreAsync(++scopeVersion);

    private async Task LoadCostsCoreAsync(int version)
    {
        CostState = DashboardLoadState.Loading;
        CostResult = null;
        try
        {
            CostReportResult result;
            do
            {
                result = await PageService.GetCostReportAsync(new CostReportFilter
                {
                    From = From, To = To, PropertyId = SelectedPropertyId, Page = CostPage, PageSize = PageSize,
                });
                if (version != scopeVersion) return;
                var lastPage = PaginationHelper.TotalPages(result.Readings.TotalCount, PageSize);
                if (CostPage <= lastPage) break;
                CostPage = lastPage;
            } while (true);

            CostResult = result;
            CostState = DashboardLoadState.Loaded;
            loadedTabs.Add(ReportTab.Costs);
        }
        catch (UnauthorizedAccessException) when (version == scopeVersion) { CostState = DashboardLoadState.Denied; }
        catch (Exception ex)
        {
            if (version != scopeVersion) return;
            CostState = DashboardLoadState.Error;
            Logger.LogError(ex, "Cost report failed");
            Toasts.Error(D["Error"]);
        }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private Task LoadIncidentsAsync() => LoadIncidentsCoreAsync(++scopeVersion);

    private async Task LoadIncidentsCoreAsync(int version)
    {
        IncidentState = DashboardLoadState.Loading;
        IncidentResult = null;
        try
        {
            IncidentReportResult result;
            do
            {
                result = await PageService.GetIncidentReportAsync(new IncidentReportFilter
                {
                    From = From, To = To, PropertyId = SelectedPropertyId,
                    Status = IncidentStatusFilter, Category = IncidentCategoryFilter, Urgency = IncidentUrgencyFilter,
                    Page = IncidentPage, PageSize = PageSize,
                });
                if (version != scopeVersion) return;
                var lastPage = PaginationHelper.TotalPages(result.Incidents.TotalCount, PageSize);
                if (IncidentPage <= lastPage) break;
                IncidentPage = lastPage;
            } while (true);

            IncidentResult = result;
            IncidentState = DashboardLoadState.Loaded;
            loadedTabs.Add(ReportTab.Incidents);
        }
        catch (UnauthorizedAccessException) when (version == scopeVersion) { IncidentState = DashboardLoadState.Denied; }
        catch (Exception ex)
        {
            if (version != scopeVersion) return;
            IncidentState = DashboardLoadState.Error;
            Logger.LogError(ex, "Incident report failed");
            Toasts.Error(D["Error"]);
        }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private Task LoadRevenueAsync() => LoadRevenueCoreAsync(++scopeVersion);

    private async Task LoadRevenueCoreAsync(int version)
    {
        RevenueState = DashboardLoadState.Loading;
        RevenueResult = null;
        try
        {
            var result = await PageService.GetRevenueReportAsync(RevenueFilter);
            if (version != scopeVersion) return;
            RevenueResult = result;
            RevenueState = DashboardLoadState.Loaded;
            loadedTabs.Add(ReportTab.Revenue);
        }
        catch (UnauthorizedAccessException) when (version == scopeVersion) { RevenueState = DashboardLoadState.Denied; }
        catch (Exception ex)
        {
            if (version != scopeVersion) return;
            RevenueState = DashboardLoadState.Error;
            Logger.LogError(ex, "Revenue report failed");
            Toasts.Error(D["Error"]);
        }
        finally { await InvokeAsync(StateHasChanged); }
    }

    private void ResetPages() => RoomPage = TenantPage = ContractPage = CostPage = IncidentPage = 1;

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static string? DateValue(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
