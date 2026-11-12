using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Enums;
using Web.Features.Tenants.DTOs;
using Web.Infrastructure.Time;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Trả thông tin liên hệ (tên + SĐT) của Chủ trọ và Quản lý đang phụ trách tòa nhà
/// mà khách thuê hiện tại đang ở. Dùng cho banner ở layout và thẻ trong "Phòng đang thuê"
/// để khách biết gọi ai khi cần hỗ trợ.
/// </summary>
public sealed class TenantContactPageService(
    AuthenticationStateProvider authenticationState,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock)
{
    /// <summary>
    /// Liên hệ của tòa nhà ứng với hợp đồng đang hiệu lực của khách đang đăng nhập.
    /// Trả null khi không phải Khách thuê, chưa gắn hồ sơ hoặc không có hợp đồng hiệu lực.
    /// </summary>
    public async Task<PropertyContactDto?> GetMyContactAsync(CancellationToken ct = default)
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        if (Roles.ResolvePrimaryRole(user) != Roles.KhachThue)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var tenantId = await db.Tenants.AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsDeleted)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (tenantId is null)
        {
            return null;
        }

        var today = VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime).Date;

        // Cùng điều kiện "đang thuê" với TenantService: hợp đồng đã duyệt và hôm nay nằm
        // trong [StartDate, EndDate]. Nhiều hợp đồng cùng hiệu lực thì lấy hợp đồng mới nhất.
        var propertyId = await (
            from member in db.ContractMembers.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where member.TenantId == tenantId
                && contract.Status == ContractStatus.Approved
                && contract.StartDate <= today
                && contract.EndDate >= today
            orderby contract.StartDate descending, contract.ContractId descending
            select (int?)room.PropertyId
        ).FirstOrDefaultAsync(ct);

        return propertyId is null
            ? null
            : await GetForPropertyAsync(db, propertyId.Value, ct);
    }

    /// <summary>
    /// Liên hệ của một tòa nhà cụ thể. Chỉ dùng nội bộ sau khi đã xác định được tòa nhà
    /// thuộc phạm vi khách đang đăng nhập (không expose trực tiếp ra endpoint).
    /// </summary>
    public async Task<PropertyContactDto?> GetForPropertyAsync(int propertyId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await GetForPropertyAsync(db, propertyId, ct);
    }

    private static async Task<PropertyContactDto?> GetForPropertyAsync(
        ApplicationDbContext db,
        int propertyId,
        CancellationToken ct)
    {
        var ownerId = await db.Properties.AsNoTracking()
            .Where(p => p.Id == propertyId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return null;
        }

        var owner = await db.Users.AsNoTracking()
            .Where(u => u.Id == ownerId)
            .Select(u => new
            {
                u.FullName,
                u.PhoneNumber,
                u.UserName,
                u.Email,
            })
            .FirstOrDefaultAsync(ct);

        // Chỉ phân công đang hiệu lực (RevokedAt == null). Một tòa có thể có nhiều Quản lý.
        var managers = await (
            from assignment in db.PropertyManagers.AsNoTracking()
            join manager in db.Users.AsNoTracking() on assignment.ManagerUserId equals manager.Id
            where assignment.PropertyId == propertyId && assignment.RevokedAt == null
            orderby manager.FullName, manager.UserName
            select new
            {
                manager.FullName,
                manager.PhoneNumber,
                manager.UserName,
                manager.Email,
            }
        ).ToListAsync(ct);

        return new PropertyContactDto
        {
            Owner = owner is null
                ? null
                : new PropertyContactPersonDto
                {
                    Name = DisplayName(owner.FullName, owner.UserName, owner.Email),
                    PhoneNumber = NormalizePhone(owner.PhoneNumber),
                },
            Managers = managers
                .Select(manager => new PropertyContactPersonDto
                {
                    Name = DisplayName(manager.FullName, manager.UserName, manager.Email),
                    PhoneNumber = NormalizePhone(manager.PhoneNumber),
                })
                .ToList(),
        };
    }

    private static string DisplayName(string? fullName, string? userName, string? email) =>
        FirstNonEmpty(fullName, userName, email) ?? string.Empty;

    private static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
