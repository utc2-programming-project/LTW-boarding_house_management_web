using Web.Data;

namespace Web.Features.Accounts.Owners;

/// <summary>
/// PLAN-001 #7: danh sách chủ trọ đang bị khoá vĩnh viễn, dạng subquery để nhúng thẳng vào
/// các query phạm vi (EXISTS) thay vì kéo danh sách về bộ nhớ.
/// </summary>
public static class OwnerLockQuery
{
    public static IQueryable<string> BlockedOwnerIds(ApplicationDbContext db)
    {
        var permanent = AccountStatusRules.PermanentLockout - TimeSpan.FromMinutes(1);
        return
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == Roles.ChuTro
                && !user.IsDeleted
                && user.LockoutEnd != null
                && user.LockoutEnd >= permanent
            select user.Id;
    }
}
