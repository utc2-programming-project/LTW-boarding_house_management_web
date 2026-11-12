// Triển khai IAccountService trên UserManager + EF Core: tạo, duyệt, khóa/mở khóa tài khoản,
// đổi vai trò và lọc danh sách theo phạm vi dữ liệu. Mọi kiểm quyền nằm ở server.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Accounts.DTOs;
using Web.Features.Audit.Entities;
using Web.Features.Accounts.Otp;
using Web.Features.Accounts.Owners;
using Web.Features.Invoices;

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
    ILogger<AccountService> logger,
    IOtpService? otp = null,
    OwnerAccessGuard? ownerAccess = null) : IAccountService
{
    public async Task<PagedResult<AccountListItemDto>> ListAsync(
        string? keyword, string? roleFilter, int page, int pageSize,
        string callerUserId, string callerRole, string? statusFilter = null,
        CancellationToken ct = default)
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
            where !user.IsDeleted
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

        // VHT-95: loc theo trang thai hien thi (Khoa uu tien cao nhat, roi Cho duyet, Hoat dong).
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var permanentLockout = AccountStatusRules.PermanentLockout - TimeSpan.FromMinutes(1);
            query = statusFilter switch
            {
                "locked" => query.Where(row =>
                    row.User.LockoutEnd != null && row.User.LockoutEnd >= permanentLockout),
                "unverified" => query.Where(row =>
                    (row.User.LockoutEnd == null || row.User.LockoutEnd < permanentLockout) &&
                    !row.User.EmailConfirmed),
                "pending" => query.Where(row =>
                    (row.User.LockoutEnd == null || row.User.LockoutEnd < permanentLockout) &&
                    row.User.EmailConfirmed &&
                    row.User.ApprovedAt == null),
                "active" => query.Where(row =>
                    (row.User.LockoutEnd == null || row.User.LockoutEnd < permanentLockout) &&
                    row.User.EmailConfirmed &&
                    row.User.ApprovedAt != null),
                _ => query,
            };
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
                row.User.EmailConfirmed,
                Role = row.RoleName,
            })
            .ToListAsync(ct);

        var managerUserIds = rows
            .Where(row => row.Role == Roles.QuanLy)
            .Select(row => row.Id)
            .ToList();

        var assignmentRows = new List<ManagerAssignmentRow>();
        if (managerUserIds.Count > 0)
        {
            assignmentRows = await (
                    from assignment in db.PropertyManagers.AsNoTracking()
                    join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
                    join owner in db.Users.AsNoTracking() on property.OwnerId equals owner.Id
                    where assignment.RevokedAt == null && managerUserIds.Contains(assignment.ManagerUserId)
                    orderby property.Name
                    select new ManagerAssignmentRow(
                        assignment.ManagerUserId,
                        property.Id,
                        property.Name,
                        property.Address,
                        owner.Id,
                        owner.FullName,
                        owner.Email))
                .ToListAsync(ct);
        }

        var assignmentsByManager = assignmentRows
            .GroupBy(row => row.ManagerUserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // VHT-24: tài khoản Chủ trọ — lấy các tòa nhà họ sở hữu để cột "Chủ trọ / Nhà trọ"
        // có nghĩa (trước đây mọi vai trò khác Quản lý đều hiện "Chưa có dữ liệu").
        var ownerUserIds = rows
            .Where(row => row.Role == Roles.ChuTro)
            .Select(row => row.Id)
            .ToList();

        var ownedRows = new List<OwnedPropertyRow>();
        if (ownerUserIds.Count > 0)
        {
            ownedRows = await (
                    from property in db.Properties.AsNoTracking()
                    where ownerUserIds.Contains(property.OwnerId) && !property.IsDeleted
                    orderby property.Name
                    select new OwnedPropertyRow(
                        property.OwnerId,
                        property.Id,
                        property.Name,
                        property.Address))
                .ToListAsync(ct);
        }

        var ownedByOwner = ownedRows
            .GroupBy(row => row.OwnerUserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var items = rows
            .Select(row =>
            {
                assignmentsByManager.TryGetValue(row.Id, out var assigned);
                var owner = assigned?.FirstOrDefault();
                ownedByOwner.TryGetValue(row.Id, out var owned);

                return new AccountListItemDto
                {
                    Id = row.Id,
                    UserName = row.UserName ?? string.Empty,
                    Email = row.Email,
                    PhoneNumber = row.PhoneNumber,
                    FullName = row.FullName,
                    Role = row.Role,
                    IsLocked = AccountStatusRules.IsPermanentlyLocked(row.LockoutEnd),
                    LockoutEnd = row.LockoutEnd,
                    Status = AccountStatusRules.Derive(row.ApprovedAt, row.LockoutEnd, row.EmailConfirmed),
                    ApprovedAt = row.ApprovedAt,
                    OwnerUserId = owner?.OwnerUserId,
                    OwnerName = owner?.OwnerName,
                    OwnerEmail = owner?.OwnerEmail,
                    AssignedProperties = assigned is null
                        ? new List<AccountAssignedProperty>()
                        : assigned
                            .Select(item => new AccountAssignedProperty(item.PropertyId, item.PropertyName, item.PropertyAddress))
                            .ToList(),
                    OwnedProperties = owned is null
                        ? new List<AccountAssignedProperty>()
                        : owned
                            .Select(item => new AccountAssignedProperty(item.PropertyId, item.PropertyName, item.PropertyAddress))
                            .ToList(),
                };
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

    /// <summary>Trang chủ Admin: đếm theo trạng thái và vài tài khoản chờ duyệt mới nhất.</summary>
    public async Task<AccountSummaryDto> GetSummaryAsync(
        string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (callerRole != Roles.Admin)
        {
            return AccountSummaryDto.Empty;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rows = await (
                from user in db.Users.AsNoTracking()
                join userRole in db.UserRoles on user.Id equals userRole.UserId
                join role in db.Roles on userRole.RoleId equals role.Id
                select new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.PhoneNumber,
                    user.FullName,
                    user.LockoutEnd,
                    user.ApprovedAt,
                    user.EmailConfirmed,
                    Role = role.Name!,
                })
            .ToListAsync(ct);

        var statuses = rows
            .Select(row => new
            {
                Row = row,
                Status = AccountStatusRules.Derive(row.ApprovedAt, row.LockoutEnd, row.EmailConfirmed),
            })
            .ToList();

        var pending = statuses
            .Where(item => item.Status == AccountStatus.Pending)
            .OrderBy(item => item.Row.UserName)
            .Take(5)
            .Select(item => new AccountListItemDto
            {
                Id = item.Row.Id,
                UserName = item.Row.UserName ?? string.Empty,
                Email = item.Row.Email,
                PhoneNumber = item.Row.PhoneNumber,
                FullName = item.Row.FullName,
                Role = item.Row.Role,
                IsLocked = AccountStatusRules.IsPermanentlyLocked(item.Row.LockoutEnd),
                LockoutEnd = item.Row.LockoutEnd,
                Status = item.Status,
                ApprovedAt = item.Row.ApprovedAt,
            })
            .ToList();

        return new AccountSummaryDto(
            statuses.Count(item => item.Status == AccountStatus.Active),
            statuses.Count(item => item.Status == AccountStatus.Pending),
            statuses.Count(item => item.Status == AccountStatus.Locked),
            pending);
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

        // PLAN-028: tai khoan nhan tien chi danh cho ChuTro; gui du 3 truong hoac xoa het.
        var bankCode = NormalizeBankCode(request.BankCode);
        var bankNumber = NormalizeBankAccountNumber(request.BankAccountNumber);
        var bankName = NormalizeBankAccountName(request.BankAccountName);
        var hasBankInput = bankCode is not null || bankNumber is not null || bankName is not null;
        if (hasBankInput)
        {
            var roles = await users.GetRolesAsync(user);
            if (!roles.Contains(Roles.ChuTro))
            {
                return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.Forbidden);
            }

            if (bankCode is null || bankNumber is null || bankName is null
                || VietQrBankCatalog.Find(bankCode) is null
                || bankNumber.Length is < 6 or > 20
                || bankName.Length is < 5 or > 50)
            {
                return AccountResultOf<MyProfileDto>.Failure(AccountErrorCodes.InvalidBankAccount);
            }
        }

        user.FullName = fullName;
        user.PhoneNumber = phone;
        user.BankCode = bankCode;
        user.BankAccountNumber = bankNumber;
        user.BankAccountName = bankName;

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
            // PLAN-022: mọi tài khoản tạo mới phải xác thực Gmail ở lần đăng nhập đầu.
            EmailConfirmed = false,
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

    public async Task<AccountResult> RegisterOwnerAsync(
        RegisterOwnerRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? string.Empty
            : CollapseSpaces(request.FullName);
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        if (email.Length == 0)
        {
            return AccountResult.Failure(AccountErrorCodes.InvalidEmail);
        }

        if (fullName.Length is 0 or > 200)
        {
            return AccountResult.Failure(AccountErrorCodes.OperationFailed, "FullName");
        }

        if (phone is not null && !IsValidVietnamPhone(phone))
        {
            return AccountResult.Failure(AccountErrorCodes.InvalidPhone);
        }

        if (await users.FindByEmailAsync(email) is not null)
        {
            return AccountResult.Failure(AccountErrorCodes.DuplicateEmail);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = fullName,
            PhoneNumber = phone,
            // Chờ Admin duyệt: AccountSignInManager chặn đăng nhập khi ApprovedAt null.
            ApprovedAt = null,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            logger.LogWarning(
                "Dang ky chu tro {Email} that bai: {Errors}",
                email, string.Join(", ", created.Errors.Select(e => e.Code)));
            return AccountResult.Failure(
                AccountErrorCodes.OperationFailed, created.Errors.First().Code);
        }

        await users.AddToRoleAsync(user, Roles.ChuTro);

        // PLAN-022: kích hoạt bằng OTP thay vì chờ Admin duyệt. Lỗi gửi thư không làm hỏng
        // đăng ký; người dùng có thể bấm gửi lại ở trang xác thực.
        if (otp is not null)
        {
            try
            {
                var issued = await otp.IssueAsync(user.Id, email, OtpPurpose.OwnerRegistration, ct);
                if (issued.Status is not (OtpIssueStatus.Success or OtpIssueStatus.Cooldown))
                {
                    logger.LogWarning(
                        "Da tao chu tro {Email} nhung chua gui duoc OTP: {Status}.", email, issued.Status);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Khong phat duoc OTP cho chu tro {Email}.", email);
            }
        }

        logger.LogInformation("Dang ky chu tro moi {Email}, cho xac thuc OTP.", email);
        return AccountResult.Success();
    }

    public async Task<AccountResult> DeleteAsync(
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

        var reason = AccountPermissionRules.CheckDelete(
            callerRole, callerUserId, targetUserId, targetRole, targetIsLastAdmin);
        if (reason is not null)
        {
            return AccountResult.Failure(reason);
        }

        if (!await IsUserInScopeAsync(db, targetUserId, callerUserId, callerRole, ct))
        {
            return AccountResult.Failure(AccountErrorCodes.OutOfScope);
        }

        // Còn hồ sơ khách gắn tài khoản thì phải bỏ liên kết trước (giữ toàn vẹn dữ liệu P3).
        if (await db.Tenants.AnyAsync(t => t.UserId == targetUserId, ct))
        {
            return AccountResult.Failure(AccountErrorCodes.AccountInUse);
        }

        // Xoá mềm: nhả email cho người mới đăng ký, giữ hồ sơ để khôi phục.
        var originalEmail = target.Email ?? target.UserName ?? string.Empty;
        var placeholder = $"deleted-{Guid.NewGuid():N}@deleted.local";

        target.OriginalEmail = originalEmail;
        target.Email = placeholder;
        target.UserName = placeholder;
        target.IsDeleted = true;
        target.DeletedAt = DateTime.UtcNow;
        target.DeletedByUserId = callerUserId;

        var deleted = await users.UpdateAsync(target);
        if (!deleted.Succeeded)
        {
            logger.LogWarning(
                "Xoa tai khoan {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", deleted.Errors.Select(e => e.Code)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        logger.LogInformation("Da xoa mem tai khoan {UserId} boi {CallerId}.", targetUserId, callerUserId);
        return AccountResult.Success();
    }

    public async Task<AccountResult> RestoreAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (callerRole != Roles.Admin && callerRole != Roles.ChuTro)
        {
            return AccountResult.Failure(AccountErrorCodes.Forbidden);
        }

        var target = await users.FindByIdAsync(targetUserId);
        if (target is null || !target.IsDeleted)
        {
            return AccountResult.Failure(AccountErrorCodes.UserNotFound);
        }

        var originalEmail = target.OriginalEmail;
        if (string.IsNullOrWhiteSpace(originalEmail))
        {
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await IsUserInScopeAsync(db, targetUserId, callerUserId, callerRole, ct))
        {
            return AccountResult.Failure(AccountErrorCodes.OutOfScope);
        }

        // Khôi phục mà email đã bị người khác dùng lại thì báo lỗi, không ghi đè.
        if (await users.FindByEmailAsync(originalEmail) is not null)
        {
            return AccountResult.Failure(AccountErrorCodes.DuplicateEmail);
        }

        target.Email = originalEmail;
        target.UserName = originalEmail;
        target.IsDeleted = false;
        target.DeletedAt = null;
        target.DeletedByUserId = null;
        target.OriginalEmail = null;

        var restored = await users.UpdateAsync(target);
        if (!restored.Succeeded)
        {
            logger.LogWarning(
                "Khoi phuc tai khoan {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", restored.Errors.Select(e => e.Code)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        logger.LogInformation("Da khoi phuc tai khoan {UserId} boi {CallerId}.", targetUserId, callerUserId);
        return AccountResult.Success();
    }

    public async Task<IReadOnlyList<DeletedAccountDto>> ListDeletedAsync(
        string callerRole, CancellationToken ct = default)
    {
        if (callerRole != Roles.Admin)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var rows = await db.Users.AsNoTracking()
            .Where(user => user.IsDeleted)
            .OrderByDescending(user => user.DeletedAt)
            .Take(200)
            .ToListAsync(ct);

        return rows.Select(user => new DeletedAccountDto(
            user.Id,
            string.IsNullOrWhiteSpace(user.OriginalEmail) ? "(không rõ)" : user.OriginalEmail!,
            user.FullName,
            user.DeletedAt)).ToList();
    }

    public async Task<AccountResult> PurgeAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (callerRole != Roles.Admin)
        {
            return AccountResult.Failure(AccountErrorCodes.Forbidden);
        }

        var target = await users.FindByIdAsync(targetUserId);
        if (target is null || !target.IsDeleted)
        {
            return AccountResult.Failure(AccountErrorCodes.UserNotFound);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var inUse = await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.UserId == targetUserId, ct)
            || await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.OwnerId == targetUserId, ct)
            || await db.PropertyManagers.AnyAsync(m =>
                m.ManagerUserId == targetUserId
                || m.AssignedByUserId == targetUserId
                || m.RevokedByUserId == targetUserId, ct)
            || await db.ManagerApprovalRequests.AnyAsync(r =>
                r.RequestedByUserId == targetUserId
                || r.DecidedByUserId == targetUserId, ct)
            || await db.MaintenanceIncidents.IgnoreQueryFilters()
                .AnyAsync(i => i.ReportedByUserId == targetUserId, ct)
            || await db.ServicePriceHistories.IgnoreQueryFilters()
                .AnyAsync(v => v.CreatedByUserId == targetUserId, ct)
            || await db.ManagerToggles.AnyAsync(t => t.UpdatedByUserId == targetUserId, ct);
        if (inUse)
        {
            return AccountResult.Failure(AccountErrorCodes.AccountInUse);
        }

        var deleted = await users.DeleteAsync(target);
        if (!deleted.Succeeded)
        {
            logger.LogWarning(
                "Xoa vinh vien tai khoan {UserId} that bai: {Errors}",
                targetUserId, string.Join(", ", deleted.Errors.Select(e => e.Code)));
            return AccountResult.Failure(AccountErrorCodes.OperationFailed);
        }

        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Category = AuditLogCategory.Account,
            Action = "account.purge",
            ActorUserId = callerUserId,
            Metadata = $"User {targetUserId}",
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return AccountResult.Success();
    }

    public async Task<TrashPurgeSummary> PurgeAllAsync(
        string callerUserId, string callerRole, CancellationToken ct = default)
    {
        if (callerRole != Roles.Admin)
        {
            return new TrashPurgeSummary(0, 0);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var ids = await db.Users.AsNoTracking()
            .Where(user => user.IsDeleted)
            .Select(user => user.Id)
            .ToListAsync(ct);

        var purged = 0;
        var skipped = 0;
        foreach (var id in ids)
        {
            var result = await PurgeAsync(id, callerUserId, callerRole, ct);
            if (result.Succeeded) purged++;
            else skipped++;
        }

        return new TrashPurgeSummary(purged, skipped);
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

        // PLAN-025: khoá chủ trọ từ /admin/users cũng phải khoá lan + thu hồi QR.
        if (targetRole == Roles.ChuTro && ownerAccess is not null)
        {
            await ownerAccess.CascadeLockAsync(targetUserId, ct);
            await ownerAccess.RevokeOpenInvitationsAsync(targetUserId, ct);
        }

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
            BankCode = user.BankCode,
            BankName = string.IsNullOrWhiteSpace(user.BankCode)
                ? null
                : VietQrBankCatalog.GetName(user.BankCode),
            BankAccountNumber = user.BankAccountNumber,
            BankAccountName = user.BankAccountName,
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
                where role.Name == Roles.Admin && !user.IsDeleted
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

    // PLAN-028: chuan hoa 3 truong tai khoan nhan tien; null = nguoi dung de trong.
    private static string? NormalizeBankCode(string? code)
    {
        var trimmed = code?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeBankAccountNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var digits = new string(number.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? NormalizeBankAccountName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return CollapseSpaces(name).ToUpperInvariant();
    }

    /// <summary>Dòng phân công Quản lý → tòa nhà, kèm Chủ trọ sở hữu tòa.</summary>
    private sealed record ManagerAssignmentRow(
        string ManagerUserId,
        int PropertyId,
        string PropertyName,
        string? PropertyAddress,
        string OwnerUserId,
        string? OwnerName,
        string? OwnerEmail);

    /// <summary>Tòa nhà thuộc sở hữu của một tài khoản Chủ trọ (dùng cho cột "Chủ trọ / Nhà trọ").</summary>
    private sealed record OwnedPropertyRow(
        string OwnerUserId,
        int PropertyId,
        string PropertyName,
        string? PropertyAddress);
}
