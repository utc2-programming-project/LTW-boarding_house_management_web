using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.Entities;
using Web.Infrastructure.Time;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Khách thuê đăng ký/hủy dịch vụ hằng tháng cho phòng (hợp đồng) của mình.
/// Chỉ áp dụng cho dịch vụ "tùy chọn" (không phải điện/nước theo chỉ số);
/// dịch vụ điện/nước được tính theo chỉ số, không đăng ký.
/// </summary>
public sealed class ContractServiceRegistrationService(
    ApplicationDbContext db,
    P2CallerProvider callerProvider,
    ILogger<ContractServiceRegistrationService> logger,
    TimeProvider clock)
{
    public sealed record RegisteredServiceDto(Guid ContractId, int ServiceCatalogId);

    /// <summary>Danh sách (hợp đồng, dịch vụ) mà khách thuê đã đăng ký trong tháng hiện tại.</summary>
    public async Task<IReadOnlyList<RegisteredServiceDto>> GetMyRegisteredServicesAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            return [];
        }

        var contractIds = await OwnActiveContractIdsAsync(caller, cancellationToken);
        if (contractIds.Count == 0)
        {
            return [];
        }

        var monthStart = new DateOnly(clock.TodayVietnam().Year, clock.TodayVietnam().Month, 1);
        return await db.ContractServiceRegistrations
            .AsNoTracking()
            .Where(item => contractIds.Contains(item.ContractId) && item.MonthStart == monthStart)
            .Select(item => new RegisteredServiceDto(item.ContractId, item.ServiceCatalogId))
            .ToListAsync(cancellationToken);
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

        var monthStart = new DateOnly(clock.TodayVietnam().Year, clock.TodayVietnam().Month, 1);
        var activeContractPropertyIds = await db.Contracts
            .AsNoTracking()
            .Where(contract => contractIds.Contains(contract.ContractId))
            .Join(
                db.Rooms.AsNoTracking(),
                contract => contract.RoomId,
                room => room.Id,
                (contract, room) => new { contract.ContractId, room.PropertyId })
            .ToDictionaryAsync(item => item.ContractId, item => item.PropertyId, cancellationToken);

        foreach (var contractId in contractIds)
        {
            // Dịch vụ gắn tòa chỉ áp dụng cho hợp đồng ở đúng tòa đó; dịch vụ dùng chung thì áp dụng mọi hợp đồng.
            if (catalog.PropertyId is int servicePropertyId
                && (!activeContractPropertyIds.TryGetValue(contractId, out var contractPropertyId)
                    || contractPropertyId != servicePropertyId))
            {
                continue;
            }

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
                    CreatedAt = clock.GetUtcNow(),
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
