using Web.Data;
using Web.Features.Approvals.Entities;

namespace Web.Features.Approvals.Services;

/// <summary>
/// VHT-96 — điểm cắm áp dụng thao tác khi Chủ trọ duyệt, mỗi mã SW một applier.
/// Applier chạy <b>trong transaction</b> của <see cref="ManagerApprovalService"/>:
/// chỉ sửa entity đang được track, <b>không</b> tự <c>SaveChanges</c>/<c>Commit</c>.
/// Muốn chặn duyệt thì ném <see cref="ApprovalApplyException"/>.
/// </summary>
public interface IApprovalApplier
{
    /// <summary>Mã công tắc SW-xx mà applier này xử lý.</summary>
    string OperationCode { get; }

    /// <summary>
    /// Áp dụng dữ liệu đề xuất lên đối tượng hiện tại. Để chống ghi đè, applier gán
    /// <c>ExpectedToken</c> vào <c>OriginalValues</c> của cột rowversion; lệch sẽ làm
    /// <c>SaveChanges</c> ném <c>DbUpdateConcurrencyException</c> và service trả <c>Conflict</c>.
    /// </summary>
    Task ApplyAsync(
        ManagerApprovalRequest request,
        ApplicationDbContext db,
        CancellationToken ct = default);
}
