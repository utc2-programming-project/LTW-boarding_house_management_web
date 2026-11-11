using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Tenants.Entities;
using Web.Infrastructure.Email;

namespace Web.Features.Tenants.Invitations;

/// <summary>
/// PLAN-006: lời mời khách thuê. Token ngẫu nhiên 32 byte, chỉ lưu hash SHA-256;
/// lời mời email hết hạn sau 15 phút và dùng được một lần.
/// </summary>
public sealed class TenantInvitationService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    UserManager<ApplicationUser> users,
    IOutboundEmailSender emailSender,
    TimeProvider clock,
    ILogger<TenantInvitationService> logger)
{
    public const int EmailLifetimeMinutes = 15;

    public async Task<TenantInvitationResult> CreateAsync(
        CreateTenantInvitationRequest request,
        TenantCaller caller,
        string inviteBaseUrl,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Forbidden, "Chỉ Chủ trọ hoặc Quản lý được mời khách thuê.");
        }

        var fullName = CollapseSpaces(request.FullName ?? string.Empty);
        var email = (request.Email ?? string.Empty).Trim();
        if (fullName.Length is 0 or > 200 || !IsValidEmail(email))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.InvalidRequest, "Tên hoặc email không hợp lệ.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var ownerId = await ResolveOwnerIdAsync(db, caller, ct);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Forbidden, "Tài khoản chưa được gán cho chủ trọ nào.");
        }

        if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == email, ct))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.EmailAlreadyRegistered, "Email này đã có tài khoản.");
        }

        // Mời lại cùng email: thu hồi lời mời cũ còn treo để chỉ còn một liên kết sống.
        var pending = await db.TenantInvitations
            .Where(i => i.Email == email && i.OwnerId == ownerId
                && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var item in pending)
        {
            item.RevokedAt = clock.GetUtcNow().UtcDateTime;
        }

        var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        var invitation = new TenantInvitation
        {
            Email = email,
            FullName = fullName,
            Phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            OwnerId = ownerId,
            InvitedByUserId = caller.UserId,
            CreatedByUserId = caller.UserId,
            Channel = Web.Features.Tenants.Enums.TenantInvitationChannel.Email,
            Kind = Web.Features.Tenants.Enums.TenantInvitationKind.NewAccount,
            ExpiryPresetMinutes = EmailLifetimeMinutes,
            TokenHash = HashToken(rawToken),
            ExpiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(EmailLifetimeMinutes),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        var ownerName = await db.Users.AsNoTracking()
            .Where(u => u.Id == ownerId)
            .Select(u => u.FullName ?? u.Email ?? "Chủ trọ")
            .FirstOrDefaultAsync(ct) ?? "Chủ trọ";

        var link = $"{inviteBaseUrl.TrimEnd('/')}/invite/{Uri.EscapeDataString(rawToken)}";
        var content = EmailTemplates.TenantInvitation(
            fullName, email, new EmailRecipient(email, email), link, ownerName);

        await emailSender.SendAsync(email, content.Subject, content.Html, ct);
        logger.LogInformation(
            "[INVITE] Đã tạo lời mời {InvitationId} cho {Email} (chủ trọ {OwnerId}).",
            invitation.Id, email, ownerId);

        return TenantInvitationResult.Success(invitation.Id, rawToken);
    }

    public async Task<IReadOnlyList<TenantInvitationDto>> ListAsync(
        TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var ownerIds = await OwnerIdsAsync(db, caller, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var rows = await db.TenantInvitations.AsNoTracking()
            .Where(i => ownerIds.Contains(i.OwnerId))
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return rows.Select(i => new TenantInvitationDto(
            i.Id,
            i.FullName,
            i.Email,
            i.ExpiresAt,
            i.CreatedAt,
            i.AcceptedAt is not null,
            i.RevokedAt is not null,
            i.AcceptedAt is null && i.RevokedAt is null && i.ExpiresAt <= now)).ToList();
    }

    public async Task<bool> RevokeAsync(int id, TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            return false;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var ownerIds = await OwnerIdsAsync(db, caller, ct);
        var invitation = await db.TenantInvitations
            .FirstOrDefaultAsync(i => i.Id == id && ownerIds.Contains(i.OwnerId), ct);
        if (invitation is null || invitation.AcceptedAt is not null)
        {
            return false;
        }

        invitation.RevokedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Khách mở liên kết, đặt mật khẩu: tạo tài khoản + hồ sơ thuộc chủ trọ đã mời.</summary>
    public async Task<TenantInvitationResult> AcceptAsync(
        string rawToken, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.NotFound, "Liên kết không hợp lệ.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var hash = HashToken(rawToken.Trim());
        var invitation = await db.TenantInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

        if (invitation is null)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.NotFound, "Liên kết không hợp lệ.");
        }

        if (invitation.AcceptedAt is not null)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.AlreadyUsed, "Lời mời đã được sử dụng.");
        }

        if (invitation.RevokedAt is not null || invitation.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
        {
            return TenantInvitationResult.Failure(
                TenantInvitationErrorCodes.Expired, "Lời mời đã hết hạn.");
        }

        var user = new ApplicationUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            // PLAN-022: vẫn phải xác thực Gmail ở lần đăng nhập đầu.
            EmailConfirmed = false,
            FullName = invitation.FullName,
            PhoneNumber = invitation.Phone,
            ApprovedAt = clock.GetUtcNow().UtcDateTime,
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            var message = string.Join(" ", created.Errors.Select(e => e.Description));
            return TenantInvitationResult.Failure(TenantInvitationErrorCodes.InvalidRequest, message);
        }

        await users.AddToRoleAsync(user, Roles.KhachThue);

        var tenant = new Tenant
        {
            FullName = invitation.FullName,
            Phone = invitation.Phone ?? string.Empty,
            Email = invitation.Email,
            UserId = user.Id,
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        // Liên kết chủ trọ đã mời (một khách có thể có nhiều liên kết chủ trọ).
        db.TenantOwnerLinks.Add(new TenantOwnerLink
        {
            TenantId = tenant.Id,
            OwnerId = invitation.OwnerId,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        });

        invitation.AcceptedAt = clock.GetUtcNow().UtcDateTime;
        invitation.TenantId = tenant.Id;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "[INVITE] Lời mời {InvitationId} đã được chấp nhận, hồ sơ {TenantId}.",
            invitation.Id, tenant.Id);

        return TenantInvitationResult.Success(invitation.Id, null);
    }

    /// <summary>Địa chỉ email + tên hiển thị của lời mời còn hiệu lực (trang nhận lời mời dùng).</summary>
    public async Task<(string Email, string FullName)?> PeekAsync(string rawToken, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var hash = HashToken((rawToken ?? string.Empty).Trim());
        var row = await db.TenantInvitations.AsNoTracking()
            .Where(i => i.TokenHash == hash
                && i.AcceptedAt == null
                && i.RevokedAt == null
                && i.ExpiresAt > clock.GetUtcNow().UtcDateTime)
            .Select(i => new { i.Email, i.FullName })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.Email, row.FullName);
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
