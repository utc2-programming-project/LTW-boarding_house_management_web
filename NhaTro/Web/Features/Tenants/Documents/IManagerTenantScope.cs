namespace Web.Features.Tenants.Documents;

/// <summary>
/// Điểm mở rộng cho phạm vi Quản lý theo tòa nhà (VHT-24). VHT-26 chỉ gọi interface này;
/// khi VHT-24 có bảng phân công, thay implementation, không sửa service giấy tờ.
/// </summary>
public interface IManagerTenantScope
{
    /// <summary>Quản lý có được giao tòa nhà chứa hồ sơ khách thuê này không.</summary>
    Task<bool> IsTenantInScopeAsync(
        string managerUserId,
        int tenantId,
        CancellationToken cancellationToken = default);
}
