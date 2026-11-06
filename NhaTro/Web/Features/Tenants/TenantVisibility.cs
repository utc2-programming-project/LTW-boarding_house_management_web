using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Tenants.Entities;

namespace Web.Features.Tenants;

/// <summary>
/// VHT-119 Q1-A: phạm vi hồ sơ khách thống nhất cho đọc lẫn ghi.
/// Hồ sơ đã gắn hợp đồng tại tòa thuộc người gọi, cộng kho hồ sơ chưa gắn hợp đồng nào
/// (dùng chung cho mọi Chủ trọ/Quản lý). Khách thuê chỉ thấy hồ sơ của mình.
/// </summary>
public static class TenantVisibility
{
    public static IQueryable<Tenant> Apply(ApplicationDbContext db, string userId, string role)
    {
        if (role == Roles.KhachThue)
        {
            var self = userId;
            return db.Tenants.Where(tenant => tenant.UserId == self);
        }

        if (role != Roles.ChuTro && role != Roles.QuanLy)
        {
            return db.Tenants.Where(_ => false);
        }

        var linked = ContractLinkedTenantIds(db, userId, role);
        // ContractLinkedTenantIds dùng IgnoreQueryFilters để giữ hợp đồng khi phòng/tòa xóa mềm,
        // nhưng cờ đó tắt query filter của cả câu query — phải lọc lại thùng rác hồ sơ tại đây.
        if (role == Roles.QuanLy)
        {
            // Quan ly: ho so thuoc chu tro cua toa minh duoc giao, hoac da gan hop dong o toa minh quan ly.
            var ownerIds =
                from assignment in db.PropertyManagers
                join property in db.Properties.IgnoreQueryFilters() on assignment.PropertyId equals property.Id
                where assignment.ManagerUserId == userId && assignment.RevokedAt == null
                select property.OwnerId;

            var ownedTenantIds = db.TenantOwnerLinks
                .Where(link => ownerIds.Contains(link.OwnerId))
                .Select(link => link.TenantId);

            return db.Tenants.Where(tenant =>
                !tenant.IsDeleted
                && (ownedTenantIds.Contains(tenant.Id) || linked.Contains(tenant.Id)));
        }

        // Chu tro: chi thay ho so co lien ket voi minh hoac da gan hop dong o toa minh so huu.
        // Mot khach co the co lien ket voi nhieu chu tro (thue nhieu noi) — lien ket nam o bang TenantOwnerLinks.
        var myTenantIds = db.TenantOwnerLinks
            .Where(link => link.OwnerId == userId)
            .Select(link => link.TenantId);

        return db.Tenants.Where(tenant =>
            !tenant.IsDeleted
            && (myTenantIds.Contains(tenant.Id) || linked.Contains(tenant.Id)));
    }

    /// <summary>
    /// Id hồ sơ có hợp đồng (mọi trạng thái) tại tòa thuộc phạm vi người gọi.
    /// Dùng <see cref="IQueryable{T}"/> để EF dịch thành EXISTS thay vì IN hàng nghìn tham số.
    /// </summary>
    public static IQueryable<int> ContractLinkedTenantIds(
        ApplicationDbContext db, string userId, string role)
    {
        if (role == Roles.ChuTro)
        {
            return
                from member in db.ContractMembers
                join contract in db.Contracts on member.ContractId equals contract.ContractId
                // Phòng/tòa xóa mềm vẫn giữ hồ sơ trong phạm vi để chủ sửa được, không rơi về kho chung.
                join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                where property.OwnerId == userId
                select member.TenantId;
        }

        if (role == Roles.QuanLy)
        {
            return
                from member in db.ContractMembers
                join contract in db.Contracts on member.ContractId equals contract.ContractId
                join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join assignment in db.PropertyManagers on room.PropertyId equals assignment.PropertyId
                where assignment.ManagerUserId == userId && assignment.RevokedAt == null
                select member.TenantId;
        }

        return db.ContractMembers.Where(_ => false).Select(member => member.TenantId);
    }
}
