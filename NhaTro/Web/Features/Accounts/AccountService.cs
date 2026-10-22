using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Accounts.DTOs;

namespace Web.Features.Accounts;

/// <summary>
/// VHT-8: trien khai <see cref="IAccountService"/> tren UserManager/RoleManager.
///
/// Nguyen tac:
///  - Moi thao tac ghi deu kiem quyen o day, khong chi an nut o UI.
///  - Khoa tai khoan dung LockoutEnd = DateTimeOffset.MaxValue (khoa khong thoi han),
///    khac voi khoa tam thoi do dang nhap sai (Lockout.DefaultLockoutTimeSpan).
///  - Khong bao gio tra PasswordHash ra ngoai.
/// </summary>
public sealed class AccountService(
    UserManager<ApplicationUser> users,
    ILogger<AccountService> logger) : IAccountService
{
    // LockoutEnd = MaxValue la quy uoc "bi quan tri khoa", khong tu mo.
    private static readonly DateTimeOffset PermanentLockout = DateTimeOffset.MaxValue;

    public async Task<PagedResult<AccountListItemDto>> ListAsync(
        string? keyword, string? roleFilter, int page, int pageSize,
        string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (!AccountPermissionRules.CanManageAccounts(callerRole))
        {
            return new PagedResult<AccountListItemDto>();
        }

        var visibleRoles = AccountPermissionRules.VisibleRoles(callerRole);
        if (visibleRoles.Count == 0)
        {
            return new PagedResult<AccountListItemDto>();
        }

        // Loc theo vai tro duoc phep thay truoc, roi moi ap bo loc cua nguoi dung.
        var allowedRoles = roleFilter is not null && visibleRoles.Contains(roleFilter)
            ? [roleFilter]
            : visibleRoles;

        var userIds = new HashSet<string>();
        foreach (var role in allowedRoles)
        {
            var inRole = await users.GetUsersInRoleAsync(role);
            foreach (var user in inRole)
            {
                userIds.Add(user.Id);
            }
        }

        // GetUsersInRoleAsync khong ho tro IQueryable; lay theo Id roi loc phia server.
        var query = users.Users.AsNoTracking().Where(u => userIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(term)) ||
                (u.Email != null && u.Email.Contains(term)) ||
                (u.FullName != null && u.FullName.Contains(term)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var pageUsers = await query
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<AccountListItemDto>(pageUsers.Count);
        foreach (var user in pageUsers)
        {
            var roles = await users.GetRolesAsync(user);
            items.Add(new AccountListItemDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? string.Empty,
                IsLocked = IsPermanentlyLocked(user),
                LockoutEnd = user.LockoutEnd,
            });
        }

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

        var admins = await users.GetUsersInRoleAsync(Roles.Admin);
        var reason = AccountPermissionRules.CheckChangeRole(
            callerRole, callerUserId, targetUserId, currentRole,
            targetIsLastAdmin: admins.Count == 1 && currentRole == Roles.Admin,
            newRole);

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
        var reason = AccountPermissionRules.CheckLock(
            callerRole, callerUserId, targetUserId, targetRole, unlock: false);
        if (reason is not null)
        {
            return AccountResult.Failure(reason);
        }

        // Khoa cua Identity chi co tac dung khi LockoutEnabled = true.
        if (!target.LockoutEnabled)
        {
            target.LockoutEnabled = true;
        }

        target.LockoutEnd = PermanentLockout;

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

    /// <summary>Khoa do quan tri dat (LockoutEnd = MaxValue), khac khoa tam thoi do sai mat khau.</summary>
    private static bool IsPermanentlyLocked(ApplicationUser user) =>
        user.LockoutEnd is { } end && end >= PermanentLockout - TimeSpan.FromMinutes(1);

    /// <summary>SĐT Viet Nam: 10 chu so, bat dau bang 0 (dac ta VHT-8).</summary>
    internal static bool IsValidVietnamPhone(string phone) =>
        phone.Length == 10 && phone[0] == '0' && phone.All(char.IsAsciiDigit);

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
