using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.DTOs;
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
                existing.IsDeleted = true;
                existing.DeletedAt = clock.GetUtcNow().UtcDateTime;
                existing.DeletedBy = caller.UserId;
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

    /// <summary>Danh sách đăng ký dịch vụ đã xóa mềm trong phạm vi nhà trọ của chủ trọ.</summary>
    public async Task<PagedResult<ServiceRegistrationTrashItemDto>> ListDeletedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsChuTro)
        {
            return new PagedResult<ServiceRegistrationTrashItemDto>();
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var rows = await (
                from registration in db.ContractServiceRegistrations.IgnoreQueryFilters().AsNoTracking()
                join contract in db.Contracts.AsNoTracking() on registration.ContractId equals contract.ContractId
                join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
                join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                where registration.IsDeleted && property.OwnerId == caller.UserId
                select new { registration, contract, room, property })
            .ToListAsync(cancellationToken);

        var serviceIds = rows.Select(row => row.registration.ServiceCatalogId).Distinct().ToList();
        var histories = await db.ServicePriceHistories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(version => serviceIds.Contains(version.ServiceCatalogId))
            .ToListAsync(cancellationToken);
        var serviceNames = histories
            .GroupBy(version => version.ServiceCatalogId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(version => version.EffectiveDate)
                    .ThenByDescending(version => version.Id)
                    .First().Name);

        var term = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var items = rows
            .Select(row => new ServiceRegistrationTrashItemDto(
                row.registration.Id,
                row.registration.ContractId,
                row.contract.ContractNumber,
                row.room.Code,
                row.property.Name,
                row.registration.ServiceCatalogId,
                serviceNames.GetValueOrDefault(row.registration.ServiceCatalogId, $"#{row.registration.ServiceCatalogId}"),
                row.registration.MonthStart,
                row.registration.DeletedAt,
                row.registration.DeletedBy))
            .Where(item => term is null
                || item.ContractNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.RoomCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.PropertyName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.ServiceName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.DeletedAt)
            .ThenByDescending(item => item.Id)
            .ToList();

        return new PagedResult<ServiceRegistrationTrashItemDto>
        {
            Items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = items.Count,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsChuTro)
        {
            throw new UnauthorizedAccessException("Chi chu tro moi khoi phuc duoc dang ky dich vu.");
        }

        var registration = await db.ContractServiceRegistrations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == id && item.IsDeleted, cancellationToken)
            ?? throw new ServiceRegistrationException(
                "Dang ky khong con trong thung rac.", "TrashItemNotFound");

        await EnsureOwnedByCallerAsync(registration, caller, cancellationToken);
        if (!await db.ServiceCatalogs.AnyAsync(item => item.Id == registration.ServiceCatalogId, cancellationToken))
        {
            throw new ServiceRegistrationException(
                "Dich vu da bi xoa; hay khoi phuc dich vu truoc.", "ServiceDeleted");
        }

        var duplicate = await db.ContractServiceRegistrations.AnyAsync(item =>
            item.Id != id
            && item.ContractId == registration.ContractId
            && item.ServiceCatalogId == registration.ServiceCatalogId
            && item.MonthStart == registration.MonthStart,
            cancellationToken);
        if (duplicate)
        {
            throw new ServiceRegistrationException(
                "Da co dang ky dich vu nay trong cung thang.", "Duplicate");
        }

        registration.IsDeleted = false;
        registration.DeletedAt = null;
        registration.DeletedBy = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PurgeAsync(int id, CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsChuTro)
        {
            throw new UnauthorizedAccessException("Chi chu tro moi xoa vinh vien duoc dang ky dich vu.");
        }

        var registration = await db.ContractServiceRegistrations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == id && item.IsDeleted, cancellationToken)
            ?? throw new ServiceRegistrationException(
                "Dang ky khong con trong thung rac.", "TrashItemNotFound");

        var propertyId = await EnsureOwnedByCallerAsync(registration, caller, cancellationToken);
        db.ContractServiceRegistrations.Remove(registration);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Category = AuditLogCategory.Business,
            Action = "service-registration.purge",
            ActorUserId = caller.UserId,
            PropertyId = propertyId,
            Metadata = $"ContractServiceRegistration #{registration.Id}",
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrashPurgeSummary> PurgeAllAsync(CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsChuTro)
        {
            return new TrashPurgeSummary(0, 0);
        }

        var propertyIds = await db.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id)
            .ToListAsync(cancellationToken);

        var rows = await (
                from registration in db.ContractServiceRegistrations.IgnoreQueryFilters()
                join contract in db.Contracts on registration.ContractId equals contract.ContractId
                join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                where registration.IsDeleted && propertyIds.Contains(room.PropertyId)
                select registration)
            .ToListAsync(cancellationToken);

        db.ContractServiceRegistrations.RemoveRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        return new TrashPurgeSummary(rows.Count, 0);
    }

    private async Task<int> EnsureOwnedByCallerAsync(
        ContractServiceRegistration registration,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var propertyId = await (
                from contract in db.Contracts.AsNoTracking()
                join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
                join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                where contract.ContractId == registration.ContractId && property.OwnerId == caller.UserId
                select (int?)property.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (propertyId is null)
        {
            throw new UnauthorizedAccessException("Ban khong co quyen thao tac dang ky nay.");
        }

        return propertyId.Value;
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
