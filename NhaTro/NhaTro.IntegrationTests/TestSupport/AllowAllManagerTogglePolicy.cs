using Web.Features.Rooms;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;

namespace NhaTro.IntegrationTests.TestSupport;

/// <summary>
/// Policy cho phép mọi thao tác, dùng cho test không kiểm công tắc Quản lý.
/// Các test VHT-94 kiểm chế độ thật phải dùng <c>ManagerTogglePolicy</c>.
/// </summary>
internal sealed class AllowAllManagerTogglePolicy : IManagerTogglePolicy
{
    public Task<RoomChangeDecision> EvaluateAsync(
        ManagerAction action, string role, CancellationToken ct = default) =>
        Task.FromResult(RoomChangeDecision.Allowed);

    public Task<RoomChangeDecision> EvaluateAsync(
        string code, string role, CancellationToken ct = default) =>
        Task.FromResult(RoomChangeDecision.Allowed);
}
