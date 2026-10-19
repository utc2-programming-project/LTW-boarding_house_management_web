namespace Web.Features.Rooms;

/// <summary>
/// Kiểm phòng còn hợp đồng hiệu lực trước khi xóa mềm (D: không xóa phòng còn hợp đồng hiệu lực).
/// VHT-7 chỉ chừa điểm gọi: bảng Contracts thuộc VHT-29/VHT-30, VHT-98 nối kiểm tra thật.
/// </summary>
public interface IRoomUsageChecker
{
    Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default);
}

/// <summary>Chưa có bảng hợp đồng nên chưa chặn; thay bằng implementation thật ở VHT-98.</summary>
public sealed class NoActiveContractRoomUsageChecker : IRoomUsageChecker
{
    public Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default) =>
        Task.FromResult(false);
}
