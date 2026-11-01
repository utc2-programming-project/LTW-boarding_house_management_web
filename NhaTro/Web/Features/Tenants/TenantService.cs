using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Rooms;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;
using Web.Features.Tenants.Enums;
using Web.Features.Tenants.Mapping;

namespace Web.Features.Tenants;

/// <summary>
/// Service SQL khách thuê P3: query/save/chọn khách trên SQL, kiểm quyền và phạm vi bên trong.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 7.
/// </summary>
public sealed class TenantService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IManagerTogglePolicy togglePolicy) : ITenantService
{
    public async Task<TenantListSummaryDto> GetSummaryAsync(TenantCaller caller, CancellationToken ct = default)
    {
        EnsureManager(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = ScopeQuery(db, caller).AsNoTracking();
        return new TenantListSummaryDto
        {
            TotalCount = await query.CountAsync(ct),
            ActiveCount = await query.CountAsync(t => t.IsActive, ct),
            LinkedAccountCount = caller.Role == Accounts.Roles.ChuTro
                ? await query.CountAsync(t => t.UserId != null, ct)
                : 0,
            WithIdNumberCount = await query.CountAsync(t => t.CitizenId != null && t.CitizenId != "", ct),
        };
    }

    public async Task<PagedResult<TenantListItemDto>> ListAsync(
        TenantFilterRequest filter, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureManager(caller);
        filter.Normalize();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = ApplyKeyword(ScopeQuery(db, caller).AsNoTracking(), filter.Keyword);
        if (filter.HasAccount is not null && !caller.CanRevealIdNumber)
        {
            // Không cho Quản lý suy ra trạng thái liên kết tài khoản qua số dòng trả về (D9).
            throw Forbidden(caller);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(t => t.IsActive == isActive);
        }

        if (filter.HasAccount is { } hasAccount)
        {
            query = hasAccount
                ? query.Where(t => t.UserId != null)
                : query.Where(t => t.UserId == null);
        }

        // 2C (VHT-120): lọc theo hợp đồng đang hiệu lực ngay trong SQL, trước phân trang/tính tổng.
        var today = Today();
        if (filter.HasActiveContract is { } hasActiveContract)
        {
            var activeTenantIds = ActiveTenantIds(db, today);
            query = hasActiveContract
                ? query.Where(t => activeTenantIds.Contains(t.Id))
                : query.Where(t => !activeTenantIds.Contains(t.Id));
        }

        var total = await query.CountAsync(ct);
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await ApplySort(query, filter.SortBy, filter.SortDirection)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .ToListAsync(ct);

        // Nạp hợp đồng hiện tại một lần cho cả trang (tránh N+1) rồi ghép phòng/trạng thái thuê thật.
        var activeContracts = await LoadActiveContractsAsync(db, rows.Select(t => t.Id).ToArray(), today, ct);

        return new PagedResult<TenantListItemDto>
        {
            Items = rows.Select(t =>
            {
                var active = activeContracts.GetValueOrDefault(t.Id);
                return TenantMapping.ToListItem(
                    t,
                    active is not null,
                    caller.CanRevealIdNumber,
                    active?.RoomCode);
            }).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total,
        };
    }

    public async Task<List<TenantAccountOptionDto>> SearchUnlinkedAccountsAsync(
        string? keyword, int limit, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureOwner(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        limit = Math.Clamp(limit, 1, 50);
        keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();

        var tenantRoleIds = db.Roles.AsNoTracking()
            .Where(role => role.Name == Accounts.Roles.KhachThue)
            .Select(role => role.Id);

        var query = db.Users.AsNoTracking()
            .Where(user => db.UserRoles.Any(userRole =>
                userRole.UserId == user.Id && tenantRoleIds.Contains(userRole.RoleId)))
            .Where(user => !db.Tenants.Any(tenant => tenant.UserId == user.Id));

        if (keyword is not null)
        {
            query = query.Where(user =>
                (user.FullName != null && user.FullName.Contains(keyword))
                || (user.Email != null && user.Email.Contains(keyword))
                || (user.PhoneNumber != null && user.PhoneNumber.Contains(keyword)));
        }

        var users = await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Take(limit)
            .Select(user => new TenantAccountOptionDto
            {
                UserId = user.Id,
                DisplayName = user.FullName != null && user.FullName != ""
                    ? user.FullName
                    : user.Email ?? user.UserName ?? user.Id,
                Email = user.Email,
            })
            .ToListAsync(ct);

        return users;
    }

    public async Task<TenantResult> GetAsync(int id, TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager && !caller.IsTenant)
        {
            throw Forbidden(caller);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (caller.IsManager)
        {
            var entity = await ScopeQuery(db, caller).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            return entity is null
                ? NotFound()
                : TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
                    revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
        }

        if (caller.IsTenant)
        {
            var own = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == caller.UserId, ct);

            // Không lộ hồ sơ ngoài phạm vi: trả TenantNotFound thay vì Forbidden (mục 9).
            return own is null || own.Id != id
                ? NotFound()
                : TenantProfile(own, caller);
        }

        throw Forbidden(caller);
    }

    public async Task<TenantResult> GetOwnAsync(TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsTenant)
        {
            throw Forbidden(caller);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var own = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == caller.UserId, ct);

        return own is null
            ? NotFound()
            : TenantProfile(own, caller);
    }

    // 2B (VHT-25) — hồ sơ + hợp đồng + phòng đang thuê, lấy từ liên kết thật.
    public async Task<TenantRentalResult> GetRentalOverviewAsync(
        int id, TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager && !caller.IsTenant)
        {
            throw Forbidden(caller);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var tenant = caller.IsManager
            ? await ScopeQuery(db, caller).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            : await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == caller.UserId, ct);

        // Khách gọi hồ sơ khác: trả NotFound, không lộ hồ sơ/phòng của người khác.
        return tenant is null
            ? RentalNotFound()
            : await BuildRentalOverviewAsync(db, tenant, ct);
    }

    public async Task<TenantRentalResult> GetOwnRentalOverviewAsync(
        TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsTenant)
        {
            throw Forbidden(caller);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var own = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == caller.UserId, ct);

        return own is null
            ? RentalNotFound()
            : await BuildRentalOverviewAsync(db, own, ct);
    }

    public async Task<TenantResult> CreateAsync(
        CreateTenantRequest request, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureManager(caller);
        var blocked = await CheckToggleAsync(caller, ManagerAction.SW04, ct);
        if (blocked is not null)
        {
            return blocked;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var submittedIdNumber = request.IdNumber;
        request.Normalize();

        var invalid = ValidateProfile(
            request.FullName, request.PhoneNumber, request.Email, request.IdNumber,
            request.PermanentAddress, request.IdIssuedPlace, request.Note,
            request.DateOfBirth, request.IdIssuedDate);
        if (invalid is not null)
        {
            return invalid;
        }

        // Tạo mới thì không có số cũ để giữ: chuỗi chứa dấu * hoặc quá ngắn đều là nhập sai, không được lưu.
        if (ContainsMaskCharacter(submittedIdNumber) || !IsValidIdNumberFormat(request.IdNumber))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidIdNumber);
        }

        // Trùng IdNumber: toàn hệ thống (mục 3). Chặn cứng.
        if (!string.IsNullOrEmpty(request.IdNumber))
        {
            var duplicateId = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive && t.CitizenId == request.IdNumber, ct);
            if (duplicateId is not null)
            {
                return await DuplicateTenantResultAsync(db, duplicateId, caller, ct);
            }
        }

        // Nghi trùng PhoneNumber: chỉ trong phạm vi người gọi (mục 3), cảnh báo nhập liệu.
        if (!request.ConfirmDuplicatePhone)
        {
            var phoneDuplicates = await ScopeQuery(db, caller).AsNoTracking()
                .Where(t => t.IsActive && t.Phone == request.PhoneNumber)
                .ToListAsync(ct);
            if (phoneDuplicates.Count > 0)
            {
                return TenantResult.Failure(
                    TenantErrorCodes.DuplicatePhone,
                    TenantErrorMessages.DuplicatePhone,
                    phoneDuplicates.Select(t => TenantMapping.ToListItem(
                        t, includeAccountStatus: caller.CanRevealIdNumber)).ToList());
            }
        }

        var entity = new Tenant();
        TenantMapping.ApplyCreate(request, entity);
        entity.CreatedAt = clock.GetUtcNow().UtcDateTime;

        db.Tenants.Add(entity);

        // Bắt vi phạm chỉ mục duy nhất khi hai request đồng thời cùng qua bước kiểm trùng.
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return TenantResult.Failure(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
        }

        return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
            revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
    }

    public async Task<TenantResult> UpdateAsync(
        int id, UpdateTenantRequest request, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureManager(caller);
        var blocked = await CheckToggleAsync(caller, ManagerAction.SW05, ct);
        if (blocked is not null)
        {
            return blocked;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        // D9: Quản lý không thấy số CCCD đầy đủ nên form có thể gửi lại chuỗi che; giữ bản gốc để nhận diện.
        var submittedIdNumber = request.IdNumber;
        request.Normalize();

        var invalid = ValidateProfile(
            request.FullName, request.PhoneNumber, request.Email, request.IdNumber,
            request.PermanentAddress, request.IdIssuedPlace, request.Note,
            request.DateOfBirth, request.IdIssuedDate);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
        }

        var entity = await ScopeQuery(db, caller).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Quản lý gửi lại đúng chuỗi che => coi như không đổi số CCCD (tránh ghi đè bằng mask).
        if (!caller.CanRevealIdNumber
            && submittedIdNumber is not null
            && submittedIdNumber == TenantNormalizer.MaskIdNumber(entity.CitizenId))
        {
            request.IdNumber = entity.CitizenId;
        }
        else if (ContainsMaskCharacter(submittedIdNumber))
        {
            // Chuỗi che đã bị sửa dở (vd. xóa bớt dấu *): Normalize sẽ bỏ hết dấu * và chỉ còn 4 số cuối,
            // nếu cho qua thì ghi đè mất CCCD thật. Chặn lại thay vì đoán ý người dùng.
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidIdNumber);
        }

        // Chỉ kiểm định dạng khi số thật sự đổi, để hồ sơ cũ có số ngắn vẫn sửa được các trường khác.
        if (request.IdNumber != entity.CitizenId && !IsValidIdNumberFormat(request.IdNumber))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidIdNumber);
        }

        // Đổi IdNumber: kiểm trùng toàn hệ thống, trừ chính hồ sơ đang sửa.
        if (!string.IsNullOrEmpty(request.IdNumber) && request.IdNumber != entity.CitizenId)
        {
            var duplicateId = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive && t.Id != id && t.CitizenId == request.IdNumber, ct);
            if (duplicateId is not null)
            {
                return await DuplicateTenantResultAsync(db, duplicateId, caller, ct);
            }
        }

        if (!request.ConfirmDuplicatePhone && request.PhoneNumber != entity.Phone)
        {
            var phoneDuplicates = await ScopeQuery(db, caller).AsNoTracking()
                .Where(t => t.IsActive && t.Id != id && t.Phone == request.PhoneNumber)
                .ToListAsync(ct);
            if (phoneDuplicates.Count > 0)
            {
                return TenantResult.Failure(
                    TenantErrorCodes.DuplicatePhone,
                    TenantErrorMessages.DuplicatePhone,
                    phoneDuplicates.Select(t => TenantMapping.ToListItem(
                        t, includeAccountStatus: caller.CanRevealIdNumber)).ToList());
            }
        }

        db.Entry(entity).Property(t => t.RowVersion).OriginalValue = rowVersion;
        TenantMapping.ApplyUpdate(request, entity);
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TenantResult.Failure(TenantErrorCodes.ConcurrencyConflict, TenantErrorMessages.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return TenantResult.Failure(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
        }

        return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
            revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
    }

    public async Task<TenantResult> UpdateOwnAsync(
        UpdateOwnProfileRequest request, TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsTenant)
        {
            throw Forbidden(caller);
        }
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        request.Normalize();
        // Kiểm độ dài sau chuẩn hóa (DTO [MaxLength] chạy trên chuỗi thô) để khớp cột DB.
        if (request.PhoneNumber.Length > 20
            || (request.Email?.Length ?? 0) > 256
            || (request.PermanentAddress?.Length ?? 0) > 300)
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidLength);
        }

        if (!TenantNormalizer.IsValidVietnamesePhone(request.PhoneNumber))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidPhone);
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
        }

        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.UserId == caller.UserId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        TenantScope.EnsureCanAccess(entity.UserId, caller.UserId, caller.Role);

        // Khách tự sửa: KHÔNG kiểm trùng số điện thoại, KHÔNG trả PossibleDuplicates (mục 6.7).
        db.Entry(entity).Property(t => t.RowVersion).OriginalValue = rowVersion;
        TenantMapping.ApplyOwnUpdate(request, entity);
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TenantResult.Failure(TenantErrorCodes.ConcurrencyConflict, TenantErrorMessages.ConcurrencyConflict);
        }

        return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: false, revealIdNumber: true));
    }

    public async Task<TenantResult> SetActiveAsync(
        int id, bool isActive, string rowVersion, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureManager(caller);
        var blocked = await CheckToggleAsync(caller, ManagerAction.SW06, ct);
        if (blocked is not null)
        {
            return blocked;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (!TryParseRowVersion(rowVersion, out var expectedVersion))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
        }

        var entity = await ScopeQuery(db, caller).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Trạng thái không đổi: trả về hồ sơ hiện tại, không ghi UpdatedAt.
        if (entity.IsActive == isActive)
        {
            return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
                revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
        }

        if (isActive)
        {
            // Kích hoạt lại phải kiểm trùng IdNumber với hồ sơ đang sử dụng (mục 7).
            if (!string.IsNullOrEmpty(entity.CitizenId))
            {
                var duplicateId = await db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.IsActive && t.Id != id && t.CitizenId == entity.CitizenId, ct);
                if (duplicateId is not null)
                {
                    return await DuplicateTenantResultAsync(db, duplicateId, caller, ct);
                }
            }
        }
        // Ngừng sử dụng khi còn hợp đồng hiệu lực (TenantInUse) chờ bảng hợp đồng VHT-25/VHT-29.

        db.Entry(entity).Property(t => t.RowVersion).OriginalValue = expectedVersion;
        entity.IsActive = isActive;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TenantResult.Failure(TenantErrorCodes.ConcurrencyConflict, TenantErrorMessages.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return TenantResult.Failure(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
        }

        return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
            revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
    }

    public async Task<TenantResult> LinkAccountAsync(
        int id, string accountUserId, string rowVersion, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureOwner(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (string.IsNullOrWhiteSpace(accountUserId) ||
            !TryParseRowVersion(rowVersion, out var expectedVersion))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
        }

        var entity = await ScopeQuery(db, caller).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Hồ sơ đã có tài khoản: không cho ghi đè (muốn đổi phải gỡ trước).
        if (entity.UserId is not null)
        {
            return TenantResult.Failure(TenantErrorCodes.AccountAlreadyLinked, TenantErrorMessages.AccountAlreadyLinked);
        }

        // Chặn liên kết nhầm: tài khoản phải tồn tại, đúng vai trò Khách thuê và chưa gắn hồ sơ khác.
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == accountUserId, ct))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.AccountNotFound);
        }

        if (!await IsTenantAccountAsync(db, accountUserId, ct))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.AccountNotTenant);
        }

        if (await db.Tenants.AsNoTracking().AnyAsync(t => t.UserId == accountUserId && t.Id != id, ct))
        {
            return TenantResult.Failure(TenantErrorCodes.AccountAlreadyLinked, TenantErrorMessages.AccountAlreadyLinked);
        }

        db.Entry(entity).Property(t => t.RowVersion).OriginalValue = expectedVersion;
        entity.UserId = accountUserId;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TenantResult.Failure(TenantErrorCodes.ConcurrencyConflict, TenantErrorMessages.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hai request đồng thời cùng gắn một tài khoản cho hai hồ sơ.
            return TenantResult.Failure(TenantErrorCodes.AccountAlreadyLinked, TenantErrorMessages.AccountAlreadyLinked);
        }

        return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
            revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
    }

    public async Task<TenantResult> UnlinkAccountAsync(
        int id, string rowVersion, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureOwner(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (!TryParseRowVersion(rowVersion, out var expectedVersion))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
        }

        var entity = await ScopeQuery(db, caller).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Chưa gắn tài khoản: trả nguyên trạng, không ghi UpdatedAt.
        if (entity.UserId is null)
        {
            return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
                revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
        }

        db.Entry(entity).Property(t => t.RowVersion).OriginalValue = expectedVersion;
        entity.UserId = null;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TenantResult.Failure(TenantErrorCodes.ConcurrencyConflict, TenantErrorMessages.ConcurrencyConflict);
        }

        return TenantResult.Success(TenantMapping.ToDetail(entity, includeNote: true,
            revealIdNumber: caller.CanRevealIdNumber, includeAccountStatus: caller.CanRevealIdNumber));
    }

    public async Task<List<TenantPickerItemDto>> SearchForPickerAsync(
        TenantPickerRequest request, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureManager(caller);
        request.Normalize();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = ApplyKeyword(ScopeQuery(db, caller).AsNoTracking(), request.Keyword)
            .Where(t => t.IsActive);

        if (request.ExcludeTenantIds is { Count: > 0 } exclude)
        {
            query = query.Where(t => !exclude.Contains(t.Id));
        }

        var rows = await query
            .OrderBy(t => t.FullName)
            .ThenBy(t => t.Id)
            .Take(request.Limit)
            .ToListAsync(ct);

        // 2C (VHT-120): cùng nguồn hợp đồng đang hiệu lực như danh sách, nạp theo lô cho cả trang chọn.
        var activeContracts = await LoadActiveContractsAsync(
            db, rows.Select(t => t.Id).ToArray(), Today(), ct);

        return rows
            .Select(t => TenantMapping.ToPickerItem(t, activeContracts.ContainsKey(t.Id)))
            .ToList();
    }

    // Phạm vi dữ liệu (theo dõi được, dùng cả cho ghi): Chủ trọ/Quản lý hiện xem toàn hệ thống
    // vì chưa có liên kết tòa nhà. Khi VHT-24/VHT-25 chốt phạm vi Quản lý, chỉ sửa tại đây;
    // query đọc thêm .AsNoTracking() tại chỗ gọi.
    private static IQueryable<Tenant> ScopeQuery(ApplicationDbContext db, TenantCaller caller) => db.Tenants;

    // 2B (VHT-25) — truy vấn hợp đồng/phòng đang thuê theo liên kết thật.
    private async Task<TenantRentalResult> BuildRentalOverviewAsync(
        ApplicationDbContext db, Tenant tenant, CancellationToken ct)
    {
        var today = Today();
        var rows = await RentalRows(db, tenant.Id).ToListAsync(ct);
        var contractIds = rows.Select(row => row.ContractId).Distinct().ToArray();
        var roommateRows = await (
            from member in db.ContractMembers.AsNoTracking()
            join roommate in db.Tenants.AsNoTracking() on member.TenantId equals roommate.Id
            where contractIds.Contains(member.ContractId)
            select new
            {
                member.ContractId,
                Roommate = new TenantRoommateDto
                {
                    TenantId = roommate.Id,
                    FullName = roommate.FullName,
                    PhoneNumber = roommate.Phone,
                }
            }
        ).ToListAsync(ct);
        var roommatesByContract = roommateRows
            .GroupBy(row => row.ContractId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TenantRoommateDto>)group.Select(row => row.Roommate).ToList());

        // Đang thuê trước, rồi lịch sử mới nhất trước; giữ đủ nhiều hợp đồng lịch sử của cùng khách.
        var rentals = rows
            .Select(row => ToRentalDto(
                row,
                today,
                roommatesByContract.GetValueOrDefault(row.ContractId, [])))
            .OrderByDescending(rental => rental.IsCurrent)
            .ThenByDescending(rental => rental.StartDate)
            .ThenByDescending(rental => rental.ContractId)
            .ToList();

        return TenantRentalResult.Success(new TenantRentalOverviewDto
        {
            TenantId = tenant.Id,
            FullName = tenant.FullName,
            Rentals = rentals,
            CurrentRental = rentals.FirstOrDefault(rental => rental.IsCurrent),
        });
    }

    private static IQueryable<TenantRentalRow> RentalRows(ApplicationDbContext db, int tenantId) =>
        from member in db.ContractMembers.AsNoTracking()
        join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
        join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
        join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
        where member.TenantId == tenantId
        select new TenantRentalRow(
            member.TenantId,
            contract.ContractId,
            contract.ContractNumber,
            contract.RoomId,
            room.Code,
            property.Id,
            property.Name,
            property.Address,
            room.AreaM2,
            contract.StartDate,
            contract.EndDate,
            contract.SnapshotPrice,
            contract.DepositAmount,
            contract.Status);

    // 2C (VHT-120) — hợp đồng đang hiệu lực của từng khách theo liên kết thật:
    // Tenant -> ContractMember -> Contract -> Room, chỉ hợp đồng đã duyệt và hôm nay nằm trong [StartDate, EndDate].
    // Tách riêng truy vấn chỉ trả TenantId để lọc danh sách trong SQL mà EF dịch được.
    private static IQueryable<int> ActiveTenantIds(ApplicationDbContext db, DateTime today) =>
        from member in db.ContractMembers.AsNoTracking()
        join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
        where contract.Status == ContractStatus.Approved
            && contract.StartDate <= today
            && contract.EndDate >= today
        select member.TenantId;

    // Lọc theo danh sách khách ngay trong SQL (không lọc sau khi đã chiếu sang record,
    // vì EF không dịch được Contains trên biểu thức dựng record).
    private static IQueryable<TenantActiveContractRow> ActiveContractRows(
        ApplicationDbContext db, int[] tenantIds, DateTime today) =>
        from member in db.ContractMembers.AsNoTracking()
        join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
        join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
        where tenantIds.Contains(member.TenantId)
            && contract.Status == ContractStatus.Approved
            && contract.StartDate <= today
            && contract.EndDate >= today
        select new TenantActiveContractRow(
            member.TenantId,
            contract.ContractId,
            room.Code,
            contract.StartDate);

    // Nhiều hợp đồng cùng hiệu lực: chọn xác định theo StartDate mới nhất rồi ContractId,
    // để danh sách/picker không đổi phòng theo thứ tự query.
    private static async Task<Dictionary<int, TenantActiveContractRow>> LoadActiveContractsAsync(
        ApplicationDbContext db, int[] tenantIds, DateTime today, CancellationToken ct)
    {
        if (tenantIds.Length == 0)
        {
            return [];
        }

        var rows = await ActiveContractRows(db, tenantIds, today).ToListAsync(ct);

        return rows
            .GroupBy(row => row.TenantId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.StartDate)
                    .ThenByDescending(row => row.ContractId)
                    .First());
    }

    private static TenantRentalContractDto ToRentalDto(
        TenantRentalRow row,
        DateTime today,
        IReadOnlyList<TenantRoommateDto> roommates) => new()
    {
        ContractId = row.ContractId,
        ContractNumber = row.ContractNumber,
        RoomId = row.RoomId,
        RoomCode = row.RoomCode,
        PropertyId = row.PropertyId,
        PropertyName = row.PropertyName,
        PropertyAddress = row.PropertyAddress,
        AreaM2 = row.AreaM2,
        Roommates = roommates,
        StartDate = DateOnly.FromDateTime(row.StartDate),
        EndDate = DateOnly.FromDateTime(row.EndDate),
        SnapshotPrice = row.SnapshotPrice,
        DepositAmount = row.DepositAmount,
        Status = row.Status,
        IsCurrent = row.Status == ContractStatus.Approved
            && row.StartDate <= today
            && row.EndDate >= today,
    };

    private DateTime Today() => clock.GetUtcNow().UtcDateTime.Date;

    private static TenantRentalResult RentalNotFound() =>
        TenantRentalResult.Failure(TenantErrorCodes.TenantNotFound, TenantErrorMessages.TenantNotFound);

    private static TenantResult TenantProfile(Tenant tenant, TenantCaller caller)
    {
        TenantScope.EnsureCanAccess(tenant.UserId, caller.UserId, caller.Role);
        return TenantResult.Success(TenantMapping.ToDetail(
            tenant, includeNote: false, revealIdNumber: true));
    }

    private static IQueryable<Tenant> ApplyKeyword(IQueryable<Tenant> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var term = keyword.Trim();
        var nameTerm = TenantNormalizer.NormalizeFullName(term);
        var phone = TenantNormalizer.NormalizePhone(term);
        var idNumber = TenantNormalizer.NormalizeIdNumber(term);

        return query.Where(t =>
            (nameTerm.Length > 0 && t.FullName.Contains(nameTerm))
            // Bỏ qua từ khóa quá ngắn để "+84" => "0" không khớp gần như mọi số.
            || (phone.Length >= 3 && t.Phone.Contains(phone))
            || (idNumber.Length >= 3 && t.CitizenId != null && t.CitizenId.Contains(idNumber)));
    }

    private static IQueryable<Tenant> ApplySort(
        IQueryable<Tenant> query, TenantSortBy sortBy, SortDirection direction)
    {
        var desc = direction == SortDirection.Desc;
        return sortBy switch
        {
            TenantSortBy.CreatedAt => desc
                ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
            _ => desc
                ? query.OrderByDescending(t => t.FullName).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.FullName).ThenBy(t => t.Id),
        };
    }

    // Kiểm SAU Normalize để khớp đúng độ dài cột DB: DTO [MaxLength] chạy trên chuỗi thô,
    // có thể dài hơn kết quả sau chuẩn hóa (tránh SqlException truncation -> 500).
    private TenantResult? ValidateProfile(
        string fullName, string phoneNumber, string? email, string? idNumber,
        string? permanentAddress, string? idIssuedPlace, string? note,
        DateOnly? dateOfBirth, DateOnly? idIssuedDate)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
        }

        if (fullName.Length > 200 || phoneNumber.Length > 20
            || (email?.Length ?? 0) > 256 || (idNumber?.Length ?? 0) > 20
            || (permanentAddress?.Length ?? 0) > 300 || (idIssuedPlace?.Length ?? 0) > 200
            || (note?.Length ?? 0) > 500)
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidLength);
        }

        if (!TenantNormalizer.IsValidVietnamesePhone(phoneNumber))
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidPhone);
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (dateOfBirth is { } dob && dob > today)
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidDateOfBirth);
        }

        if (idIssuedDate is { } issued && dateOfBirth is { } birth && issued < birth)
        {
            return TenantResult.Failure(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidIdIssuedDate);
        }

        return null;
    }

    // Chi kem thong tin ho so trung khi nam trong pham vi nguoi goi (muc 3); ngoai pham vi thi khong.
    private async Task<TenantResult> DuplicateTenantResultAsync(
        ApplicationDbContext db, Tenant duplicate, TenantCaller caller, CancellationToken ct)
    {
        var inScope = await ScopeQuery(db, caller).AsNoTracking().AnyAsync(t => t.Id == duplicate.Id, ct);
        return inScope
            ? TenantResult.Failure(
                TenantErrorCodes.DuplicateTenant,
                TenantErrorMessages.DuplicateTenant,
                [TenantMapping.ToListItem(duplicate, includeAccountStatus: caller.CanRevealIdNumber)])
            : TenantResult.Failure(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
    }

    // Tai khoan duoc phep gan lam khach thue: ton tai va co vai tro KhachThue.
    private static async Task<bool> IsTenantAccountAsync(
        ApplicationDbContext db, string userId, CancellationToken ct) =>
        await (from userRole in db.UserRoles
               join role in db.Roles on userRole.RoleId equals role.Id
               where userRole.UserId == userId && role.Name == Accounts.Roles.KhachThue
               select userRole).AnyAsync(ct);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool TryParseRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Số giấy tờ rỗng là hợp lệ (không bắt buộc); có giá trị thì tối thiểu 8 ký tự (hộ chiếu 8-9, CMND 9, CCCD 12).</summary>
    private static bool IsValidIdNumberFormat(string? normalizedIdNumber) =>
        string.IsNullOrEmpty(normalizedIdNumber) || normalizedIdNumber.Length >= MinIdNumberLength;

    private static bool ContainsMaskCharacter(string? rawIdNumber) =>
        rawIdNumber is not null && rawIdNumber.Contains('*');

    private const int MinIdNumberLength = 8;

    private static void EnsureManager(TenantCaller caller)
    {
        if (!caller.IsManager)
        {
            throw Forbidden(caller);
        }
    }

    private static void EnsureOwner(TenantCaller caller)
    {
        if (!caller.CanRevealIdNumber)
        {
            throw Forbidden(caller);
        }
    }

    // Điểm gọi policy công tắc Quản lý (VHT-94): Quản lý tạo/sửa/ngừng hồ sơ khách thuê phải qua
    // SW-04/05/06. Chủ trọ luôn có hiệu lực ngay (D4) vì policy trả Allowed trước khi đọc công tắc.
    // Kiểm trước mọi truy vấn dữ liệu để người bị chặn không dò được hồ sơ trùng hay hồ sơ có tồn tại.
    // Chờ duyệt hiện chỉ trả lời; hàng đợi duyệt thuộc VHT-96.
    private async Task<TenantResult?> CheckToggleAsync(
        TenantCaller caller, ManagerAction action, CancellationToken ct)
    {
        var decision = await togglePolicy.EvaluateAsync(action, caller.Role, ct);

        return decision switch
        {
            RoomChangeDecision.Denied => throw Forbidden(caller),
            RoomChangeDecision.RequiresApproval => TenantResult.Failure(
                TenantErrorCodes.ApprovalRequired, TenantErrorMessages.ApprovalRequired),
            _ => null,
        };
    }

    private static UnauthorizedAccessException Forbidden(TenantCaller caller) =>
        new(caller.IsAdmin
            ? "Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu khach thue."
            : TenantErrorMessages.Forbidden);

    private static TenantResult NotFound() =>
        TenantResult.Failure(TenantErrorCodes.TenantNotFound, TenantErrorMessages.TenantNotFound);

    /// <summary>Dòng join Tenant -> ContractMember -> Contract -> Room -> Property.</summary>
    private sealed record TenantRentalRow(
        int TenantId,
        Guid ContractId,
        string ContractNumber,
        int RoomId,
        string RoomCode,
        int PropertyId,
        string PropertyName,
        string? PropertyAddress,
        decimal? AreaM2,
        DateTime StartDate,
        DateTime EndDate,
        decimal SnapshotPrice,
        decimal DepositAmount,
        ContractStatus Status);

    /// <summary>Hợp đồng đang hiệu lực của một khách, kèm phòng để hiển thị danh sách/picker.</summary>
    private sealed record TenantActiveContractRow(
        int TenantId,
        Guid ContractId,
        string RoomCode,
        DateTime StartDate);
}
