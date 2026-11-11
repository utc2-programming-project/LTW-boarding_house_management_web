using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Tenants.Entities;
using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.Invitations;

/// <summary>
/// PLAN-024: QR 24h (preset 15m/1h/6h/24h) và mời khách đã có tài khoản.
/// Token 32 byte, chỉ lưu SHA-256, dùng một lần, bind owner/phòng/người nhận.
/// </summary>
public sealed class TenantQrInvitationService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> users,
    TimeProvider clock,
    ILogger<TenantQrInvitationService> logger)
{
    public const int EmailLifetimeMinutes = 15;

    public async Task<TenantInvitationResult> CreateQrAsync(
        CreateTenantQrRequest request,
        TenantCaller caller,
        string inviteBaseUrl,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Forbidden, "Chỉ Chủ trọ hoặc Quản lý được tạo QR.");
        }

        var minutes = request.Expiry switch
        {
            TenantInvitationExpiry.Minutes15 => 15,
            TenantInvitationExpiry.Hour1 => 60,
            TenantInvitationExpiry.Hours6 => 360,
            TenantInvitationExpiry.Hours24 => 1440,
            _ => 0,
        };
        if (minutes == 0)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.InvalidRequest, "Mốc hạn không hợp lệ.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ownerId = await ResolveOwnerIdAsync(db, caller, ct);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Forbidden, "Tài khoản chưa được gán cho chủ trọ nào.");
        }

        int? propertyId = request.PropertyId;
        int? roomId = request.RoomId;
        if (roomId is { } rid)
        {
            var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(item => item.Id == rid, ct);
            if (room is null || room.IsDeleted)
            {
                return TenantInvitationResult.Failure(
                    TenantInvitationErrorCodes.InvalidRequest, "Phòng không hợp lệ.");
            }

            propertyId ??= room.PropertyId;
            if (propertyId != room.PropertyId)
            {
                return TenantInvitationResult.Failure(
                    TenantInvitationErrorCodes.InvalidRequest, "Phòng không thuộc nhà trọ đã chọn.");
            }
        }

        if (propertyId is { } pid)
        {
            var owned = await db.Properties.AsNoTracking()
                .AnyAsync(property => property.Id == pid
                    && !property.IsDeleted
                    && property.OwnerId == ownerId, ct);
            if (!owned)
            {
                return TenantInvitationResult.Failure(
                    TenantInvitationErrorCodes.Forbidden, "Nhà trọ ngoài phạm vi.");
            }
        }

        ApplicationUser? target = null;
        if (request.Kind == TenantInvitationKind.ExistingAccount)
        {
            if (string.IsNullOrWhiteSpace(request.TargetUserId))
            {
                return TenantInvitationResult.Failure(
                    TenantInvitationErrorCodes.InvalidRequest, "Chưa chọn khách đã có tài khoản.");
            }

            target = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == request.TargetUserId && !user.IsDeleted, ct);
            if (target is null)
            {
                return TenantInvitationResult.Failure(
                    TenantInvitationErrorCodes.NotFound, "Không tìm thấy tài khoản khách.");
            }

            // Khách đã có tài khoản có thể đang thuê chủ trọ khác (mục 7); chỉ cần
            // hồ sơ khách tồn tại. Việc chấp nhận mới tạo liên kết sang chủ trọ này.
            var targetTenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(tenant => tenant.UserId == target.Id && !tenant.IsDeleted, ct);
            if (targetTenant is null)
            {
                return TenantInvitationResult.Failure(
                    TenantInvitationErrorCodes.NotFound, "Khách chưa có hồ sơ thuê.");
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        var invitation = new TenantInvitation
        {
            Email = target?.Email ?? string.Empty,
            FullName = target?.FullName ?? string.Empty,
            Phone = target?.PhoneNumber,
            OwnerId = ownerId,
            InvitedByUserId = caller.UserId,
            CreatedByUserId = caller.UserId,
            Channel = TenantInvitationChannel.Qr,
            Kind = request.Kind,
            ExpiryPresetMinutes = minutes,
            PropertyId = propertyId,
            RoomId = roomId,
            TargetUserId = target?.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddMinutes(minutes),
            CreatedAt = now,
        };

        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "[QR] Tạo lời mời {InvitationId} ({Kind}, {Minutes} phút) cho owner {OwnerId}.",
            invitation.Id, invitation.Kind, minutes, ownerId);

        // Link do nơi gọi dựng từ PublicBaseUrl cấu hình; service chỉ trả token.
        return new TenantInvitationResult(
            true,
            invitation.Id,
            rawToken,
            ExpiresAt: invitation.ExpiresAt);
    }

    public async Task<TenantInvitationDetails?> PeekDetailsAsync(
        string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var hash = HashToken(rawToken.Trim());
        var invitation = await db.TenantInvitations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TokenHash == hash, ct);
        if (invitation is null)
        {
            return null;
        }

        var ownerName = await db.Users.AsNoTracking()
            .Where(user => user.Id == invitation.OwnerId)
            .Select(user => user.FullName ?? user.Email ?? "Chủ trọ")
            .FirstOrDefaultAsync(ct) ?? "Chủ trọ";

        var propertyName = invitation.PropertyId is { } propertyId
            ? await db.Properties.AsNoTracking()
                .Where(property => property.Id == propertyId)
                .Select(property => property.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        var roomCode = invitation.RoomId is { } roomId
            ? await db.Rooms.AsNoTracking()
                .Where(room => room.Id == roomId)
                .Select(room => room.Code)
                .FirstOrDefaultAsync(ct)
            : null;

        var now = clock.GetUtcNow().UtcDateTime;
        return new TenantInvitationDetails(
            invitation.Id,
            invitation.Channel,
            invitation.Kind,
            invitation.OwnerId,
            ownerName,
            propertyName,
            roomCode,
            invitation.Email,
            invitation.FullName,
            invitation.Phone,
            invitation.TargetUserId,
            invitation.ExpiresAt,
            invitation.AcceptedAt is not null,
            invitation.RevokedAt is not null,
            invitation.ExpiresAt <= now);
    }

    public async Task<TenantInvitationResult> BeginNewAccountAsync(
        string rawToken,
        NewTenantAccountRequest request,
        CancellationToken ct = default)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var fullName = CollapseSpaces(request.FullName ?? string.Empty);
        if (fullName.Length is 0 or > 200 || !IsValidEmail(email))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.InvalidRequest, "Họ tên hoặc email không hợp lệ.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invitation = await FindUsableAsync(db, rawToken, ct);
        if (invitation.Error is not null)
        {
            return invitation.Error;
        }

        var item = invitation.Invitation!;
        if (item.Kind != TenantInvitationKind.NewAccount)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.InvalidRequest, "Lời mời này dành cho tài khoản đã có.");
        }

        if (await users.FindByEmailAsync(email) is not null
            || await db.Users.AsNoTracking().AnyAsync(user => user.Email == email, ct))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.EmailAlreadyRegistered, "Email này đã có tài khoản.");
        }

        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? string.Empty : request.PhoneNumber.Trim();
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            ApprovedAt = now,
            FullName = fullName,
            PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
        };

        var created = await users.CreateAsync(user, request.Password ?? string.Empty);
        if (!created.Succeeded)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.InvalidRequest,
                string.Join(" ", created.Errors.Select(error => error.Description)));
        }

        await users.AddToRoleAsync(user, Roles.KhachThue);

        var tenant = new Tenant
        {
            FullName = fullName,
            Phone = phone,
            Email = email,
            UserId = user.Id,
            IsActive = true,
            CreatedAt = now,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        db.TenantOwnerLinks.Add(new TenantOwnerLink
        {
            TenantId = tenant.Id,
            OwnerId = item.OwnerId,
            CreatedAt = now,
        });

        if (item.RoomId is { } roomId)
        {
            db.PendingRoomAssignments.Add(new PendingRoomAssignment
            {
                TenantId = tenant.Id,
                PropertyId = item.PropertyId ?? 0,
                RoomId = roomId,
                InvitationId = item.Id,
                CreatedByUserId = item.InvitedByUserId,
                CreatedAt = now,
            });
        }

        item.Email = email;
        item.FullName = fullName;
        item.Phone = phone;
        item.TenantId = tenant.Id;
        item.AcceptedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "[QR] Khách mới {Email} nhận lời mời {InvitationId}, hồ sơ {TenantId}.",
            email, item.Id, tenant.Id);

        return TenantInvitationResult.SuccessAccount(item.Id, user.Id, email);
    }

    public async Task<TenantInvitationResult> AcceptExistingAsync(
        string rawToken, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Forbidden, "Cần đăng nhập bằng tài khoản được mời.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invitation = await FindUsableAsync(db, rawToken, ct);
        if (invitation.Error is not null)
        {
            return invitation.Error;
        }

        var item = invitation.Invitation!;
        if (item.Kind != TenantInvitationKind.ExistingAccount)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.InvalidRequest, "Lời mời này dành cho khách mới.");
        }

        if (!string.IsNullOrWhiteSpace(item.TargetUserId)
            && !string.Equals(item.TargetUserId, userId, StringComparison.Ordinal))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Forbidden, "Tài khoản đăng nhập không đúng người được mời.");
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, ct);
        if (user is null || user.IsDeleted)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.NotFound, "Không tìm thấy tài khoản.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(tenantRow => tenantRow.UserId == userId && !tenantRow.IsDeleted, ct);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                FullName = user.FullName ?? user.Email ?? "Khách thuê",
                Phone = user.PhoneNumber ?? string.Empty,
                Email = user.Email,
                UserId = user.Id,
                IsActive = true,
                CreatedAt = now,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.TenantOwnerLinks.AnyAsync(
                link => link.TenantId == tenant.Id && link.OwnerId == item.OwnerId, ct))
        {
            db.TenantOwnerLinks.Add(new TenantOwnerLink
            {
                TenantId = tenant.Id,
                OwnerId = item.OwnerId,
                CreatedAt = now,
            });
        }

        if (item.RoomId is { } roomId
            && !await db.PendingRoomAssignments.AnyAsync(
                assignment => assignment.TenantId == tenant.Id
                    && assignment.RoomId == roomId
                    && assignment.Status == PendingRoomAssignmentStatus.Pending, ct))
        {
            db.PendingRoomAssignments.Add(new PendingRoomAssignment
            {
                TenantId = tenant.Id,
                PropertyId = item.PropertyId ?? 0,
                RoomId = roomId,
                InvitationId = item.Id,
                CreatedByUserId = item.InvitedByUserId,
                CreatedAt = now,
            });
        }

        item.AcceptedAt = now;
        item.TenantId = tenant.Id;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "[QR] Tài khoản {UserId} nhận lời mời {InvitationId}.",
            userId, item.Id);

        return TenantInvitationResult.SuccessAccount(item.Id, userId, user.Email ?? string.Empty);
    }

    public async Task<IReadOnlyList<ExistingTenantOptionDto>> SearchExistingTenantsAsync(
        string? keyword, TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Mục 7: khách đã có tài khoản có thể đang thuê chủ trọ khác; tìm theo hồ sơ khách
        // (không lộ dữ liệu hợp đồng/chủ trọ cũ), chỉ trả tên/email/số điện thoại tối thiểu.
        var query = db.Tenants.AsNoTracking()
            .Where(tenant => tenant.UserId != null && !tenant.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(tenant =>
                tenant.FullName.Contains(term)
                || (tenant.Email != null && tenant.Email.Contains(term))
                || tenant.Phone.Contains(term));
        }

        return await query
            .OrderBy(tenant => tenant.FullName)
            .Take(20)
            .Select(tenant => new ExistingTenantOptionDto(
                tenant.UserId!,
                tenant.Id,
                tenant.FullName,
                tenant.Email,
                tenant.Phone))
            .ToListAsync(ct);
    }

    private async Task<(TenantInvitation? Invitation, TenantInvitationResult? Error)> FindUsableAsync(
        ApplicationDbContext db, string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return (null, TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.NotFound, "Liên kết không hợp lệ."));
        }

        var hash = HashToken(rawToken.Trim());
        var invitation = await db.TenantInvitations
            .FirstOrDefaultAsync(item => item.TokenHash == hash, ct);

        if (invitation is null)
        {
            return (null, TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.NotFound, "Liên kết không hợp lệ."));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (invitation.AcceptedAt is not null)
        {
            return (null, TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.AlreadyUsed, "Lời mời đã được sử dụng."));
        }

        if (invitation.RevokedAt is not null || invitation.ExpiresAt <= now)
        {
            return (null, TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Expired, "Lời mời đã hết hạn."));
        }

        return (invitation, null);
    }

    private static async Task<string?> ResolveOwnerIdAsync(
        ApplicationDbContext db, TenantCaller caller, CancellationToken ct)
    {
        if (caller.IsChuTro)
        {
            return caller.UserId;
        }

        return await (
            from assignment in db.PropertyManagers.AsNoTracking()
            join property in db.Properties.IgnoreQueryFilters() on assignment.PropertyId equals property.Id
            where assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null
            orderby property.Id
            select (string?)property.OwnerId
        ).FirstOrDefaultAsync(ct);
    }

    private static async Task<List<string>> OwnerIdsAsync(
        ApplicationDbContext db, TenantCaller caller, CancellationToken ct)
    {
        if (caller.IsChuTro)
        {
            return [caller.UserId];
        }

        return await (
            from assignment in db.PropertyManagers.AsNoTracking()
            join property in db.Properties.IgnoreQueryFilters() on assignment.PropertyId equals property.Id
            where assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null
            select property.OwnerId
        ).Distinct().ToListAsync(ct);
    }

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool IsValidEmail(string email) =>
        email.Length is > 3 and <= 256
        && System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
        && string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
}
