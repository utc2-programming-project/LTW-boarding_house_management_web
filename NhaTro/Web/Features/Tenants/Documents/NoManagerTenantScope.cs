namespace Web.Features.Tenants.Documents;

/// <summary>
/// Mặc định deny-by-default khi chưa có dữ liệu phân công Quản lý theo tòa nhà (VHT-24 chưa bàn giao).
/// Nhờ vậy giấy tờ không bị mở cho mọi Quản lý; khi VHT-24 có bảng phân công chỉ cần thay implementation.
/// </summary>
public sealed class NoManagerTenantScope : IManagerTenantScope
{
    public Task<bool> IsTenantInScopeAsync(
        string managerUserId,
        int tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
