namespace Web.Common.SoftDelete;

/// <summary>
/// Bản ghi có thể đưa vào thùng rác. Chỉ yêu cầu hai trường chung vì các entity hiện có
/// dùng tên người xóa khác nhau (<c>DeletedBy</c> và <c>DeletedByUserId</c>).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }
}
