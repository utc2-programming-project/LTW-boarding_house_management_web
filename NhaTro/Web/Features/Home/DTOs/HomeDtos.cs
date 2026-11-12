namespace Web.Features.Home.DTOs;

public enum HomeLoadState
{
    Loading,
    Loaded,
    Error,
    Denied,
}

/// <summary>Một khối dữ liệu trang chủ: danh sách hiển thị kèm trạng thái tải riêng.</summary>
public sealed record HomeSection<T>(IReadOnlyList<T> Items, HomeLoadState State)
{
    public static HomeSection<T> Loading() => new([], HomeLoadState.Loading);
}

/// <summary>Thẻ số liệu. Key là khóa resource; Value/Hint đã định dạng sẵn.</summary>
/// <param name="Unavailable">Nguồn của thẻ lỗi/chưa ghép: hiện "—" và nút thử lại, không hiện số 0 giả.</param>
public sealed record HomeKpi(
    string Key,
    string Value,
    string? HintKey = null,
    string? Hint = null,
    string? Href = null,
    bool Unavailable = false);

/// <summary>Một việc cần xử lý kèm số lượng và đường dẫn.</summary>
public sealed record HomeAction(string Key, int Count, string Href);

public sealed record HomeContractItem(
    string RoomCode,
    string PropertyName,
    string TenantNames,
    DateOnly EndDate,
    int DaysLeft);

public sealed record HomeInvoiceItem(
    int InvoiceId,
    string RoomCode,
    string PropertyName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Outstanding);

public sealed record HomeAccountItem(string DisplayName, string Role);

public sealed record HomeSnapshot(
    string Role,
    HomeSection<HomeKpi> Kpis,
    HomeSection<HomeAction> Actions,
    HomeSection<HomeContractItem> ExpiringContracts,
    HomeSection<HomeInvoiceItem> OpenInvoices,
    HomeSection<HomeAccountItem> PendingAccounts)
{
    public string? DisplayName { get; init; }
}
