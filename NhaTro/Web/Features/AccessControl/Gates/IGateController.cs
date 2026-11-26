using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Gates;

/// <summary>
/// Cổng vật lý. Mức A chỉ ghi nhận "hợp lệ" để bảo vệ mở tay; mức B cắm adapter
/// relay/OSDP/Wiegand vào cùng interface này mà không đổi service nghiệp vụ.
/// </summary>
public interface IGateController
{
    GateLevel Level { get; }

    Task<GateOpenResult> OpenAsync(GateOpenRequest request, CancellationToken ct = default);
}

/// <summary>Mức A: không có relay — kiosk hiện "Hợp lệ" và bảo vệ mở tay.</summary>
public sealed class SoftOpenGateController : IGateController
{
    public GateLevel Level => GateLevel.A;

    public Task<GateOpenResult> OpenAsync(GateOpenRequest request, CancellationToken ct = default) =>
        Task.FromResult(new GateOpenResult(true));
}
