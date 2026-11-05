namespace Web.Features.Rooms;

/// <summary>
/// Kiểm phòng còn hợp đồng hiệu lực trước khi xóa mềm (D: không xóa phòng còn hợp đồng hiệu lực).
/// Production dùng <see cref="ActiveContractRoomUsageChecker"/> (VHT-98).
/// </summary>
public interface IRoomUsageChecker
{
    Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default);
}

/// <summary>
/// Chỉ dùng trong test làm checker "không có hợp đồng"; production đăng ký
/// <see cref="ActiveContractRoomUsageChecker"/> (VHT-98).
/// </summary>
public sealed class NoActiveContractRoomUsageChecker : IRoomUsageChecker
{
    public Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default) =>
        Task.FromResult(false);
}
