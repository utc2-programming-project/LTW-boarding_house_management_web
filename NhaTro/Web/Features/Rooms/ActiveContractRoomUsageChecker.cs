using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;

namespace Web.Features.Rooms;

/// <summary>
/// VHT-98: phòng còn hợp đồng hiệu lực thì không cho vào thùng rác.
/// "Hiệu lực" = đã duyệt và hôm nay nằm trong [StartDate, EndDate] — cùng định nghĩa
/// với khách đang thuê ở <c>TenantService.ActiveTenantIds</c>.
/// </summary>
public sealed class ActiveContractRoomUsageChecker(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock) : IRoomUsageChecker
{
    public async Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default)
    {
        var today = clock.GetUtcNow().UtcDateTime.Date;

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.Contracts
            .AsNoTracking()
            .AnyAsync(
                contract => contract.RoomId == roomId
                    && contract.Status == ContractStatus.Approved
                    && contract.StartDate <= today
                    && contract.EndDate >= today,
                ct);
    }
}
