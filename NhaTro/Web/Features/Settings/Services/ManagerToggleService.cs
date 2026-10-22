using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Settings.DTOs;
using Web.Features.Settings.Entities;
using Web.Features.Settings.Enums;

namespace Web.Features.Settings.Services;

/// <summary>
/// VHT-94 — đọc/ghi công tắc Quản lý trên SQL. Không cache: bảng nhỏ và Chủ trọ cần thấy
/// hiệu lực ngay sau khi đổi (cùng circuit hoặc circuit khác).
/// </summary>
public sealed class ManagerToggleService(
    ApplicationDbContext db,
    TimeProvider clock,
    ILogger<ManagerToggleService> logger) : IManagerToggleService, IManagerToggleReader
{
    public async Task<ManagerToggleListResult> GetAllAsync(
        ManagerToggleCaller caller, CancellationToken ct = default)
    {
        // Ma trận dòng 8: Admin không đọc công tắc nghiệp vụ; Quản lý không đọc/ghi công tắc của mình.
        if (!caller.CanConfigure)
        {
            return ManagerToggleListResult.Failure(
                ManagerToggleErrorCodes.Forbidden, ManagerToggleErrorMessages.Forbidden);
        }

        var rows = await db.ManagerToggles
            .AsNoTracking()
            .ToListAsync(ct);

        var byCode = rows.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

        var items = ManagerToggleCatalog.All
            .Select(definition => ToItem(definition, byCode.GetValueOrDefault(definition.Code)))
            .ToList();

        return ManagerToggleListResult.Success(items);
    }

    public async Task<ManagerToggleResult> SetAsync(
        string code,
        ManagerToggleMode mode,
        ManagerToggleCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.CanConfigure)
        {
            return ManagerToggleResult.Failure(
                ManagerToggleErrorCodes.Forbidden, ManagerToggleErrorMessages.Forbidden);
        }

        if (!ManagerToggleCatalog.TryFind(code, out var definition))
        {
            return ManagerToggleResult.Failure(
                ManagerToggleErrorCodes.UnknownToggle, ManagerToggleErrorMessages.UnknownToggle);
        }

        if (!Enum.IsDefined(mode))
        {
            return ManagerToggleResult.Failure(
                ManagerToggleErrorCodes.InvalidMode, ManagerToggleErrorMessages.InvalidMode);
        }

        // SW-15 / SW-15a…e: chỉ Cho phép hoặc Không thể (D12), không nhận Duyệt.
        if (!definition.Allows(mode))
        {
            return ManagerToggleResult.Failure(
                ManagerToggleErrorCodes.ModeNotAllowed, ManagerToggleErrorMessages.ModeNotAllowed);
        }

        var entity = await db.ManagerToggles
            .FirstOrDefaultAsync(t => t.Code == definition.Code, ct);

        if (entity is null)
        {
            // Dòng seed bị thiếu (DB chưa áp migration): tạo mới theo danh mục thay vì bỏ qua công tắc.
            entity = new ManagerToggle { Code = definition.Code };
            db.ManagerToggles.Add(entity);
        }

        entity.Mode = mode;
        entity.UpdatedAt = clock.GetUtcNow();
        entity.UpdatedByUserId = caller.UserId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Đổi công tắc {Code} thất bại", definition.Code);
            return ManagerToggleResult.Failure(
                ManagerToggleErrorCodes.OperationFailed, ManagerToggleErrorMessages.OperationFailed);
        }

        logger.LogInformation(
            "Chủ trọ {UserId} đổi công tắc {Code} sang {Mode}",
            caller.UserId, definition.Code, mode);

        return ManagerToggleResult.Success(ToItem(definition, entity));
    }

    public async Task<ManagerToggleMode> GetModeAsync(string code, CancellationToken ct = default)
    {
        if (!ManagerToggleCatalog.TryFind(code, out var definition))
        {
            // Mã lạ: trả mặc định Duyệt để policy tự quyết định từ chối (fail closed) ở tầng trên.
            return ManagerToggleMode.Approve;
        }

        var mode = await db.ManagerToggles
            .AsNoTracking()
            .Where(t => t.Code == definition.Code)
            .Select(t => (ManagerToggleMode?)t.Mode)
            .FirstOrDefaultAsync(ct);

        return mode ?? definition.DefaultMode;
    }

    private static ManagerToggleItem ToItem(ManagerToggleCatalog.Definition definition, ManagerToggle? row) =>
        new(
            definition.Code,
            definition.Name,
            definition.Group,
            row?.Mode ?? definition.DefaultMode,
            definition.AllowedModes,
            row?.UpdatedAt ?? default,
            row?.UpdatedByUserId);
}
