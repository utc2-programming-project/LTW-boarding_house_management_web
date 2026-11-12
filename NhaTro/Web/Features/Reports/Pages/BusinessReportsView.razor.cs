using Microsoft.AspNetCore.Components;
using Web.Common.Utils;
using Web.Components.Layout;
using Web.Features.Accounts;
using Web.Features.Dashboard.Models;
using Web.Features.Dashboard.Services;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms.Enums;

namespace Web.Features.Reports.Pages;

public partial class BusinessReportsView
{
    private const int PageSize = 20;
    [SupplyParameterFromQuery] public int? PropertyId { get; set; }

    private IReadOnlyList<DashboardPropertyOption> Properties = [];
    private int? SelectedPropertyId;
    private bool RoomActive = true;
    private RoomStatus? RoomStatusFilter;
    private bool? TenantActive;
    private bool? TenantRenting;
    private int RoomPage = 1;
    private int TenantPage = 1;
    private RoomReportResult? RoomResult;
    private TenantReportResult? TenantResult;
    private DashboardLoadState PropertyState = DashboardLoadState.Loading;
    private DashboardLoadState RoomState = DashboardLoadState.Loading;
    private DashboardLoadState TenantState = DashboardLoadState.Loading;
    private bool IsOwner;
    private bool Refreshing;
    private bool ShowTenantReport;
    private DateTime? LastRefreshed;
    private readonly SemaphoreSlim reloadGate = new(1, 1);
    private int scopeVersion;

    private bool Busy => Refreshing || RoomState == DashboardLoadState.Loading || TenantState == DashboardLoadState.Loading;

    private (int From, int To) RoomItemRange =>
        PaginationHelper.ItemRange(RoomPage, PageSize, RoomResult?.Rooms.TotalCount ?? 0);

    private (int From, int To) TenantItemRange =>
        PaginationHelper.ItemRange(TenantPage, PageSize, TenantResult?.Tenants.TotalCount ?? 0);

    protected override async Task OnInitializedAsync()
    {
        IsOwner = await PageService.GetRoleAsync() == Roles.ChuTro;
        
    }

    protected override Task OnParametersSetAsync() => ReloadAsync(false, readQuery: true);

    private Task RefreshAsync() => ReloadAsync(true);

    private void SelectReport(bool tenant) => ShowTenantReport = tenant;

    private async Task ReloadAsync(bool notify, bool readQuery = false)
    {
        // Xếp hàng khi điều hướng đổi query trong lúc một lần làm mới còn đang chạy.
        await reloadGate.WaitAsync();
        if (readQuery)
        {
            SelectedPropertyId = PropertyId;
            RoomPage = TenantPage = 1;
        }
        scopeVersion++;
        Refreshing = true;
        PropertyState = RoomState = TenantState = DashboardLoadState.Loading;
        RoomResult = null;
        TenantResult = null;
        LastRefreshed = null;
        try
        {
            Properties = await PageService.ListPropertiesAsync();
            if (SelectedPropertyId.HasValue && !Properties.Any(p => p.Id == SelectedPropertyId))
            {
                PropertyState = RoomState = TenantState = DashboardLoadState.Denied;
                return;
            }

            PropertyState = DashboardLoadState.Loaded;
            await Task.WhenAll(LoadRoomsAsync(), LoadTenantsAsync());
            if (RoomState != DashboardLoadState.Error && TenantState != DashboardLoadState.Error)
            {
                LastRefreshed = PageService.GetLastRefreshedAtUtc();
                if (notify && RoomState == DashboardLoadState.Loaded && TenantState == DashboardLoadState.Loaded)
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
            reloadGate.Release();
        }
    }

    private async Task ChangePropertyAsync(ChangeEventArgs args)
    {
        if (Busy) return;
        SelectedPropertyId = int.TryParse(args.Value?.ToString(), out var id) ? id : null;
        RoomPage = TenantPage = 1;
        await ReloadAsync(false);
    }

    private async Task ClearFiltersAsync()
    {
        if (Busy) return;
        SelectedPropertyId = null;
        RoomActive = true;
        RoomStatusFilter = null;
        TenantActive = TenantRenting = null;
        RoomPage = TenantPage = 1;
        await ReloadAsync(false);
    }

    private async Task ChangeRoomFiltersAsync()
    {
        if (Busy) return;
        RoomPage = 1;
        await LoadRoomsAsync();
    }

    // Select gửi chuỗi: chuyển rõ sang bool trước khi cập nhật bộ lọc nguồn.
    private async Task ChangeRoomActivityAsync(ChangeEventArgs args)
    {
        if (Busy || !bool.TryParse(args.Value?.ToString(), out var active)) return;
        RoomActive = active;
        await ChangeRoomFiltersAsync();
    }

    private async Task ChangeTenantActivityAsync(ChangeEventArgs args)
    {
        if (Busy) return;
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
        if (Busy) return;
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

    private async Task ChangeRoomPageAsync(int page)
    {
        if (Busy || RoomResult is null) return;
        RoomPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(RoomResult.Rooms.TotalCount, PageSize));
        await LoadRoomsAsync();
    }

    private async Task ChangeTenantPageAsync(int page)
    {
        if (Busy || TenantResult is null) return;
        TenantPage = PaginationHelper.ClampPage(page, PaginationHelper.TotalPages(TenantResult.Tenants.TotalCount, PageSize));
        await LoadTenantsAsync();
    }

    private Task RetryRoomsAsync()
    {
        if (Busy) return Task.CompletedTask;
        return PropertyState == DashboardLoadState.Loaded ? LoadRoomsAsync() : ReloadAsync(false);
    }

    private Task RetryTenantsAsync()
    {
        if (Busy) return Task.CompletedTask;
        return PropertyState == DashboardLoadState.Loaded ? LoadTenantsAsync() : ReloadAsync(false);
    }

    private async Task LoadRoomsAsync()
    {
        var version = scopeVersion;
        RoomState = DashboardLoadState.Loading;
        RoomResult = null;
        try
        {
            // Mỗi lần lấy một kết quả nguồn dùng chung cho KPI, biểu đồ và trang bảng.
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

            RoomResult = result;
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
        var version = scopeVersion;
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
}
