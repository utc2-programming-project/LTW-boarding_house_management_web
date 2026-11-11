using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.Entities;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Khách thuê đăng ký/hủy dịch vụ hằng tháng cho phòng (hợp đồng) của mình.
/// Chỉ áp dụng cho dịch vụ "tùy chọn" (không phải điện/nước theo chỉ số);
/// dịch vụ điện/nước được tính theo chỉ số, không đăng ký.
/// </summary>
public sealed class ContractServiceRegistrationService(
    ApplicationDbContext db,
    P2CallerProvider callerProvider,
    ILogger<ContractServiceRegistrationService> logger)
{
    /// <summary>Danh sách ServiceCatalogId mà khách thuê đã đăng ký cho các hợp đồng của mình.</summary>
    public async Task<IReadOnlySet<int>> GetMyRegisteredServiceIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            return new HashSet<int>();
        }

        var contractIds = await OwnActiveContractIdsAsync(caller, cancellationToken);
        if (contractIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var ids = await db.ContractServiceRegistrations
            .AsNoTracking()
            .Where(item => contractIds.Contains(item.ContractId) && item.MonthStart == monthStart)
            .Select(item => item.ServiceCatalogId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    /// <summary>
    /// Bật/tắt đăng ký một dịch vụ cho hợp đồng đang hiệu lực của khách thuê.
    /// Trả về true nếu sau thao tác đã đăng ký. Không-idempotent theo toggle.
    /// </summary>
    public async Task<bool> ToggleAsync(int serviceCatalogId, CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new UnauthorizedAccessException("Chi khách thuê mới được đăng ký dịch vụ.");
        }

        var catalog = await db.ServiceCatalogs
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceCatalogId, cancellationToken);
        if (catalog is null || catalog.IsReadOnly || catalog.IsDeleted || !catalog.IsActive)
        {
            throw new InvalidOperationException("Dịch vụ này không thể đăng ký.");
        }

        var contractIds = await OwnActiveContractIdsAsync(caller, cancellationToken);
        if (contractIds.Count == 0)
        {
            throw new InvalidOperationException("Không tìm thấy hợp đồng hiệu lực của bạn.");
        }

        foreach (var contractId in contractIds)
        {
            var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            var existing = await db.ContractServiceRegistrations
                .SingleOrDefaultAsync(
                    item => item.ContractId == contractId && item.ServiceCatalogId == serviceCatalogId
                        && item.MonthStart == monthStart,
                    cancellationToken);
            if (existing is not null)
            {
                db.ContractServiceRegistrations.Remove(existing);
            }
            else
            {
                db.ContractServiceRegistrations.Add(new ContractServiceRegistration
                {
                    ContractId = contractId,
                    ServiceCatalogId = serviceCatalogId,
                    MonthStart = monthStart,
                    RegisteredByUserId = caller.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Tenant {UserId} toggled service registration {ServiceId} for {Count} contract(s)",
            caller.UserId,
            serviceCatalogId,
            contractIds.Count);

        var nowRegistered = await db.ContractServiceRegistrations
            .AsNoTracking()
            .AnyAsync(
                item => item.ServiceCatalogId == serviceCatalogId && contractIds.Contains(item.ContractId),
                cancellationToken);
        return nowRegistered;
    }

    private async Task<IReadOnlyList<Guid>> OwnActiveContractIdsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var memberContractIds = db.ContractMembers
            .AsNoTracking()
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking(),
                member => member.TenantId,
                tenant => tenant.Id,
                (member, tenant) => new { member.ContractId, tenant.UserId })
            .Where(item => item.UserId == caller.UserId)
            .Select(item => (Guid?)item.ContractId);

        return await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.Status == ContractStatus.Approved
                && memberContractIds.Contains(contract.ContractId))
            .Select(contract => contract.ContractId)
            .ToListAsync(cancellationToken);
    }
}
