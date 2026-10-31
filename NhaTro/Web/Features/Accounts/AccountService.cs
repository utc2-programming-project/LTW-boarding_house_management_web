using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Accounts.DTOs;

namespace Web.Features.Accounts;

/// <summary>
/// VHT-8 / VHT-95: trien khai <see cref="IAccountService"/> tren UserManager + SQL.
///
/// Nguyen tac:
///  - Moi thao tac ghi deu kiem quyen o day, khong chi an nut o UI.
///  - Pham vi du lieu tinh o server: Admin xem tat ca; ChuTro chi tai khoan thuoc toa minh;
///    QuanLy chi chinh minh + khach trong toa duoc giao; KhachThue khong thay ai.
///  - Trang thai: Khoa (LockoutEnd = MaxValue) &gt; Cho duyet (ApprovedAt == null) &gt; Hoat dong.
///  - Khong bao gio tra PasswordHash ra ngoai.
/// </summary>
public sealed class AccountService(
    UserManager<ApplicationUser> users,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<PagedResult<AccountListItemDto>> ListAsync(
        string? keyword, string? roleFilter, int page, int pageSize,
        string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (!AccountPermissionRules.CanManageAccounts(callerRole))
        {
            return new PagedResult<AccountListItemDto>();
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Mot tai khoan chi giu mot vai tro (he thong chot 1 vai tro), join de loc vai tro o SQL.
        var query =
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            select new { User = user, RoleName = role.Name! };

        // Pham vi phai ap truoc keyword/count de khong lo tong so tai khoan ngoai pham vi.
        var scopedUserIds = ScopedUserIds(db, callerUserId, callerRole);
        if (scopedUserIds is not null)
        {
            query = query.Where(row => scopedUserIds.Contains(row.User.Id));
        }

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            var visibleRoles = AccountPermissionRules.VisibleRoles(callerRole);
            if (!visibleRoles.Contains(roleFilter) && roleFilter != callerRole)
            {
                return new PagedResult<AccountListItemDto>();
            }

            query = query.Where(row => row.RoleName == roleFilter);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(row =>
                (row.User.UserName != null && row.User.UserName.Contains(term)) ||
                (row.User.Email != null && row.User.Email.Contains(term)) ||
                (row.User.FullName != null && row.User.FullName.Contains(term)) ||
                (row.User.PhoneNumber != null && row.User.PhoneNumber.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var rows = await query
            .OrderBy(row => row.User.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.User.Id,
                row.User.UserName,
                row.User.Email,
                row.User.PhoneNumber,
                row.User.FullName,
                row.User.LockoutEnd,
                row.User.ApprovedAt,
                Role = row.RoleName,
            })
            .ToListAsync(ct);

        var items = rows
            .Select(row => new AccountListItemDto
            {
                Id = row.Id,
                UserName = row.UserName ?? string.Empty,
                Email = row.Email,
                PhoneNumber = row.PhoneNumber,
                FullName = row.FullName,
                Role = row.Role,
                IsLocked = AccountStatusRules.IsPermanentlyLocked(row.LockoutEnd),
                LockoutEnd = row.LockoutEnd,
                Status = AccountStatusRules.Derive(row.ApprovedAt, row.LockoutEnd),
                ApprovedAt = row.ApprovedAt,
            })
            .ToList();

        return new PagedResult<AccountListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AccountResultOf<MyProfileDto>> GetMyProfileAsync(
        string callerUserId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(callerUserId);
        if (user is null)
        {
            return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.UserNotFound);
        }

        return AccountResultOf<MyProfileDto>.Success(await ToProfileAsync(user));
    }

    public async Task<AccountResultOf<MyProfileDto>> UpdateMyProfileAsync(
        UpdateMyProfileRequest request, string callerUserId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(callerUserId);
        if (user is null)
        {
            return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.UserNotFound);
        }

        var fullName = string.IsNullOrWhiteSpace(request.FullName) ? null : CollapseSpaces(request.FullName);
        if (fullName is not null && fullName.Length > 200)
        {
            return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.OperationFailed, "FullName");
        }

        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (phone is not null && !IsValidVietnamPhone(phone))
        {
            return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.InvalidPhone);
        }

        user.FullName = fullName;
        user.PhoneNumber = phone;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            logger.LogWarning("Cap nhat ho so {UserId} that bai: {Errors}",
                callerUserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.OperationFailed);
        }

        return AccountResultOf<MyProfileDto>.Success(await ToProfileAsync(user));
    }

    public async Task<AccountResult> CreateAsync(
        CreateAccountRequest request, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        var role = request.Role?.Trim() ?? string.Empty;
        if (!AccountPermissionRules.CanCreateRole(callerRole, role))
        {
            return AccountResult.Failure(AccountErrorCodes.RoleNotAllowed);
        }

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0)
        {
            return AccountResult.Failure(AccountErrorCodes.InvalidEmail);
        }

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? string.Empty
            : CollapseSpaces(request.FullName);
        if (fullName.Length is 0 or > 200)
        {
            return AccountResult.Failure(AccountErrorCodes.OperationFailed, "FullName");
        }

        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (phone is not null && !IsValidVietnamPhone(phone))
        {
            return AccountResult.Failure(AccountErrorCodes.InvalidPhone);
        }

        int? tenantId = null;
        if (role == Roles.KhachThue)
        {
            if (request.TenantId is not { } requestedTenantId)
            {
                return AccountResult.Failure(AccountErrorCodes.TenantRequired);
            }

            await using var scopeDb = await dbContextFactory.CreateDbContextAsync(ct);
            var tenant = await scopeDb.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == requestedTenantId, ct);

            if (tenant is null)
            {
                return AccountResult.Failure(AccountErrorCodes.TenantRequired);
            }

            if (tenant.UserId is not null)
            {
                return AccountResult.Failure(AccountErrorCodes.TenantAlreadyLinked);
            }

            if (!await ScopedTenantIds(scopeDb, callerUserId, callerRole)
                    .AnyAsync(id => id == requestedTenantId, ct))
            {
                return AccountResult.Failure(AccountErrorCodes.OutOfScope);
            }

            tenantId = requestedTenantId;
        }

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            PhoneNumber = phone,
            // O #5: thao tac cua Admin/ChuTro hieu luc ngay; QuanLy tao ra o trang thai Cho duyet.
            ApprovedAt = callerRole == Roles.QuanLy ? null : now,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            logger.LogWarning("Tao tai khoan {Email} that bai: {Errors}",
                email, string.Join(", ", created.Errors.Select(e => e.Description)));
            return MapCreateErrors(created);
        }

        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            logger.LogError("Gan vai tro {Role} cho {Email} that bai: {Errors}",
                role, email, string.Join(", ", added.Errors.Select(e => e.Description)));
            await users.DeleteAsync(user);
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        if (tenantId is { } linkTenantId)
        {
            // Bu tru: gan ho so that bai thi xoa tai khoan vua tao, khong de tai khoan mo côi.
            await using var linkDb = await dbContextFactory.CreateDbContextAsync(ct);
            var linked = await linkDb.Tenants
                .Where(t => t.Id == linkTenantId && t.UserId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.UserId, user.Id)
                    .SetProperty(t => t.UpdatedAt, now), ct);

            if (linked == 0)
            {
                // Thua dua: ho so vua duoc tai khoan khac gan sau buoc kiem tra.
                await users.DeleteAsync(user);
                logger.LogWarning("Ho so khach {TenantId} da co tai khoan truoc khi gan {Email}.", linkTenantId, email);
                return AccountResult.Failure(AccountErrorCodes.TenantAlreadyLinked);
            }
        }

        logger.LogInformation("Tai khoan {Email} vai tro {Role} duoc tao boi {Caller}.", email, role, callerUserId);
        return AccountResult.Success();
    }

    public async Task<AccountResult> ApproveAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        var target = await users.FindByIdAsync(targetUserId);
        if (target is null)
        {
            return AccountResult.Failure(AccountErrorCodes.UserNotFound);
        }

        var targetRole = (await users.GetRolesAsync(target)).FirstOrDefault() ?? string.Empty;
        if (!AccountPermissionRules.CanApprove(callerRole, targetRole))
        {
            return AccountResult.Failure(AccountErrorCodes.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await IsUserInScopeAsync(db, targetUserId, callerUserId, callerRole, ct))
        {
            return AccountResult.Failure(AccountErrorCodes.OutOfScope);
        }

        if (target.ApprovedAt is not null)
        {
            return AccountResult.Failure(AccountErrorCodes.NotPendingApproval);
        }

        target.ApprovedAt = DateTime.UtcNow;

        var result = await users.UpdateAsync(target);
        if (!result.Succeeded)
        {
            logger.LogError("Duyet tai khoan {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        logger.LogInformation("Tai khoan {UserId} duoc duyet boi {Caller}.", targetUserId, callerUserId);
        return AccountResult.Success();
    }

    public async Task<AccountResult> ChangeRoleAsync(
        string targetUserId, string newRole,
        string callerUserId, string callerRole, CancellationToken ct = default)
    {
        var target = await users.FindByIdAsync(targetUserId);
        if (target is null)
        {
            return AccountResult.Failure(AccountErrorCodes.UserNotFound);
        }

        var currentRoles = await users.GetRolesAsync(target);
        var currentRole = currentRoles.FirstOrDefault() ?? string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var targetIsLastAdmin = currentRole == Roles.Admin
            && await CountActiveAdminsAsync(db, ct) <= 1;
        var targetIsLinkedTenant = await db.Tenants.AnyAsync(t => t.UserId == targetUserId, ct);

        var reason = AccountPermissionRules.CheckChangeRole(
            callerRole, callerUserId, targetUserId, currentRole,
            targetIsLastAdmin, newRole, targetIsLinkedTenant);

        if (reason is not null)
        {
            return AccountResult.Failure(reason);
        }

        // D1: vai tro moi phai nam trong danh sach nguoi goi duoc phep gan.
        if (!AccountPermissionRules.AssignableRoles(callerRole).Contains(newRole))
        {
            return AccountResult.Failure(AccountErrorCodes.RoleNotAllowed);
        }

        if (currentRoles.Count > 0)
        {
            var remove = await users.RemoveFromRolesAsync(target, currentRoles);
            if (!remove.Succeeded)
            {
                logger.LogError("Go vai tro cu cua {UserId} that bai: {Errors}",
                    targetUserId, string.Join(", ", remove.Errors.Select(e => e.Description)));
                return AccountResult.Failure(AccountErrorCodes.OperationFailed);
            }
        }

        var add = await users.AddToRoleAsync(target, newRole);
        if (!add.Succeeded)
        {
            logger.LogError("Gan vai tro moi cho {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", add.Errors.Select(e => e.Description)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        logger.LogInformation("Vai tro cua {UserId} doi tu {Old} sang {New} boi {Caller}.",
            targetUserId, currentRole, newRole, callerUserId);

        return AccountResult.Success();
    }

    public async Task<AccountResult> LockAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        var target = await users.FindByIdAsync(targetUserId);
        if (target is null)
        {
            return AccountResult.Failure(AccountErrorCodes.UserNotFound);
        }

        var targetRole = (await users.GetRolesAsync(target)).FirstOrDefault() ?? string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var targetIsLastAdmin = targetRole == Roles.Admin
            && await CountActiveAdminsAsync(db, ct) <= 1;

        var reason = AccountPermissionRules.CheckLock(
            callerRole, callerUserId, targetUserId, targetRole, unlock: false, targetIsLastAdmin);
        if (reason is not null)
        {
            return AccountResult.Failure(reason);
        }

        if (!await IsUserInScopeAsync(db, targetUserId, callerUserId, callerRole, ct))
        {
            return AccountResult.Failure(AccountErrorCodes.OutOfScope);
        }

        // Khoa cua Identity chi co tac dung khi LockoutEnabled = true.
        if (!target.LockoutEnabled)
        {
            target.LockoutEnabled = true;
        }

        target.LockoutEnd = AccountStatusRules.PermanentLockout;

        var result = await users.UpdateAsync(target);
        if (!result.Succeeded)
        {
            logger.LogError("Khoa tai khoan {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        // Dang nhap hien tai (neu co) van con hieu luc vi cookie chua het han;
        // doi SecurityStamp de Identity tu choi phien cu o lan kiem tra tiep theo.
        await users.UpdateSecurityStampAsync(target);

        logger.LogInformation("Tai khoan {UserId} bi khoa boi {Caller}.", targetUserId, callerUserId);
        return AccountResult.Success();
    }

    public async Task<AccountResult> UnlockAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        var target = await users.FindByIdAsync(targetUserId);
        if (target is null)
        {
            return AccountResult.Failure(AccountErrorCodes.UserNotFound);
        }

        var targetRole = (await users.GetRolesAsync(target)).FirstOrDefault() ?? string.Empty;
        var reason = AccountPermissionRules.CheckLock(
            callerRole, callerUserId, targetUserId, targetRole, unlock: true);
        if (reason is not null)
        {
            return AccountResult.Failure(reason);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await IsUserInScopeAsync(db, targetUserId, callerUserId, callerRole, ct))
        {
            return AccountResult.Failure(AccountErrorCodes.OutOfScope);
        }

        // Mở khóa không tự duyệt: ApprovedAt giữ nguyên nên tài khoản chưa duyệt quay về Chờ duyệt.
        target.LockoutEnd = null;
        target.AccessFailedCount = 0;

        var result = await users.UpdateAsync(target);
        if (!result.Succeeded)
        {
            logger.LogError("Mo khoa tai khoan {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        logger.LogInformation("Tai khoan {UserId} duoc mo khoa boi {Caller}.", targetUserId, callerUserId);
        return AccountResult.Success();
    }

    public async Task<IReadOnlyList<AssignableTenantDto>> SearchAssignableTenantsAsync(
        string? keyword, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (!AccountPermissionRules.CanManageAccounts(callerRole))
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Admin: moi ho so chua co tai khoan. ChuTro/QuanLy: chi ho so co hop dong trong pham vi.
        var query = db.Tenants.AsNoTracking().Where(t => t.UserId == null);
        if (callerRole != Roles.Admin)
        {
            var scopedTenantIds = ScopedTenantIds(db, callerUserId, callerRole);
            query = query.Where(t => scopedTenantIds.Contains(t.Id));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(t => t.FullName.Contains(term) || t.Phone.Contains(term));
        }

        var tenants = await query
            .OrderBy(t => t.FullName)
            .Take(20)
            .Select(t => new { t.Id, t.FullName, t.Phone })
            .ToListAsync(ct);

        if (tenants.Count == 0)
        {
            return [];
        }

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var propertyRows = await (
                from member in db.ContractMembers.AsNoTracking()
                join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                where tenantIds.Contains(member.TenantId)
                select new { member.TenantId, property.Name })
            .Distinct()
            .ToListAsync(ct);

        var propertyNames = propertyRows
            .GroupBy(row => row.TenantId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group.Select(row => row.Name).Where(name => name.Length > 0)));

        return tenants
            .Select(t => new AssignableTenantDto
            {
                TenantId = t.Id,
                FullName = t.FullName,
                PhoneNumber = t.Phone,
                PropertyNames = propertyNames.GetValueOrDefault(t.Id),
            })
            .ToList();
    }

    private async Task<MyProfileDto> ToProfileAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new MyProfileDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FullName = user.FullName,
            Role = roles.FirstOrDefault() ?? string.Empty,
            HasPassword = await users.HasPasswordAsync(user),
        };
    }

    /// <summary>
    /// Pham vi tai khoan nguoi goi duoc thay/thao tac. Null = khong gioi han (Admin).
    /// ChuTro: chinh minh + QuanLy dang phan cong vao toa minh + KhachThue co hop dong tai toa minh.
    /// QuanLy: chinh minh + KhachThue co hop dong tai toa duoc giao.
    /// </summary>
    private static IQueryable<string>? ScopedUserIds(
        ApplicationDbContext db, string callerUserId, string callerRole)
    {
        if (callerRole == Roles.Admin)
        {
            return null;
        }

        if (callerRole == Roles.ChuTro)
        {
            var managedUserIds =
                from assignment in db.PropertyManagers
                join property in db.Properties on assignment.PropertyId equals property.Id
                where assignment.RevokedAt == null && property.OwnerId == callerUserId
                select assignment.ManagerUserId;

            var tenantUserIds = TenantUserIds(
                db, db.Properties.Where(p => p.OwnerId == callerUserId).Select(p => p.Id));

            return db.Users
                .Where(user => user.Id == callerUserId ||
                               managedUserIds.Contains(user.Id) ||
                               tenantUserIds.Contains(user.Id))
                .Select(user => user.Id);
        }

        if (callerRole == Roles.QuanLy)
        {
            var assignedPropertyIds = db.PropertyManagers
                .Where(assignment => assignment.ManagerUserId == callerUserId && assignment.RevokedAt == null)
                .Select(assignment => assignment.PropertyId);

            var tenantUserIds = TenantUserIds(db, assignedPropertyIds);

            return db.Users
                .Where(user => user.Id == callerUserId || tenantUserIds.Contains(user.Id))
                .Select(user => user.Id);
        }

        return db.Users.Where(_ => false).Select(user => user.Id);
    }

    /// <summary>UserId cua ho so khach co hop dong (ke ca lich su) tai cac toa duoc truyen vao.</summary>
    private static IQueryable<string> TenantUserIds(
        ApplicationDbContext db, IQueryable<int> propertyIds) =>
        from tenant in db.Tenants
        join member in db.ContractMembers on tenant.Id equals member.TenantId
        join contract in db.Contracts on member.ContractId equals contract.ContractId
        join room in db.Rooms on contract.RoomId equals room.Id
        where propertyIds.Contains(room.PropertyId) && tenant.UserId != null
        select tenant.UserId!;

    /// <summary>Id ho so khach co hop dong tai toa thuoc pham vi nguoi goi.</summary>
    private static IQueryable<int> ScopedTenantIds(
        ApplicationDbContext db, string callerUserId, string callerRole) => callerRole switch
    {
        Roles.Admin => db.Tenants.Select(tenant => tenant.Id),
        Roles.ChuTro =>
            from tenant in db.Tenants
            join member in db.ContractMembers on tenant.Id equals member.TenantId
            join contract in db.Contracts on member.ContractId equals contract.ContractId
            join room in db.Rooms on contract.RoomId equals room.Id
            join property in db.Properties on room.PropertyId equals property.Id
            where property.OwnerId == callerUserId
            select tenant.Id,
        Roles.QuanLy =>
            from tenant in db.Tenants
            join member in db.ContractMembers on tenant.Id equals member.TenantId
            join contract in db.Contracts on member.ContractId equals contract.ContractId
            join room in db.Rooms on contract.RoomId equals room.Id
            join assignment in db.PropertyManagers on room.PropertyId equals assignment.PropertyId
            where assignment.ManagerUserId == callerUserId && assignment.RevokedAt == null
            select tenant.Id,
        _ => db.Tenants.Where(_ => false).Select(tenant => tenant.Id),
    };

    private static async Task<bool> IsUserInScopeAsync(
        ApplicationDbContext db, string targetUserId, string callerUserId, string callerRole,
        CancellationToken ct)
    {
        var scopedUserIds = ScopedUserIds(db, callerUserId, callerRole);
        return scopedUserIds is null
            || await scopedUserIds.AnyAsync(id => id == targetUserId, ct);
    }

    /// <summary>Admin dang hoat dong = da duyet va khong bi khoa vinh vien.</summary>
    private static async Task<int> CountActiveAdminsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var admins = await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                join user in db.Users on userRole.UserId equals user.Id
                where role.Name == Roles.Admin
                select new { user.ApprovedAt, user.LockoutEnd })
            .ToListAsync(ct);

        return admins.Count(admin =>
            admin.ApprovedAt is not null && !AccountStatusRules.IsPermanentlyLocked(admin.LockoutEnd));
    }

    private static AccountResult MapCreateErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            switch (error.Code)
            {
                case nameof(IdentityErrorDescriber.DuplicateEmail):
                case nameof(IdentityErrorDescriber.DuplicateUserName):
                    return AccountResult.Failure(AccountErrorCodes.DuplicateEmail);
                case nameof(IdentityErrorDescriber.InvalidEmail):
                case nameof(IdentityErrorDescriber.InvalidUserName):
                    return AccountResult.Failure(AccountErrorCodes.InvalidEmail);
                case nameof(IdentityErrorDescriber.PasswordTooShort):
                case nameof(IdentityErrorDescriber.PasswordRequiresDigit):
                case nameof(IdentityErrorDescriber.PasswordRequiresLower):
                case nameof(IdentityErrorDescriber.PasswordRequiresUpper):
                case nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric):
                case nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars):
                    return AccountResult.Failure(AccountErrorCodes.WeakPassword);
            }
        }

        return AccountResult.Failure(AccountErrorCodes.OperationFailed);
    }

    /// <summary>SĐT Viet Nam: 10 chu so, bat dau bang 0 (dac ta VHT-8).</summary>
    internal static bool IsValidVietnamPhone(string phone) =>
        phone.Length == 10 && phone[0] == '0' && phone.All(char.IsAsciiDigit);

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
