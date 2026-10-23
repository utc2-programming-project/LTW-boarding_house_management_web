using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using Web.Features.Maintenance.Models;

namespace Web.Features.Maintenance;

// Một instance cho mỗi trang; không đăng ký singleton, không ghi SQL/storage thật.
public sealed class IncidentDemoSession(AuthenticationStateProvider authentication, TimeProvider time)
{
    public const string Policy = "IncidentDemo";
    private readonly List<IncidentDemoItem> _items = [];
    private readonly Dictionary<IncidentDemoAction, IncidentDemoMode> _modes = new()
    {
        [IncidentDemoAction.Create] = IncidentDemoMode.Approval,
        [IncidentDemoAction.Update] = IncidentDemoMode.Approval,
        [IncidentDemoAction.ResultPhoto] = IncidentDemoMode.Approval
    };
    private string? _sessionUserId;
    private int _nextId = 104;
    private static readonly IncidentDemoRoom[] DemoRooms =
    [
        new(101, 1, "An Bình (demo)", "A101"),
        new(102, 1, "An Bình (demo)", "A102"),
        new(201, 2, "Bình Minh (demo)", "B201")
    ];

    public static bool IsAllowed(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && !user.IsInRole(Roles.Admin)
        && (user.IsInRole(Roles.ChuTro) || user.IsInRole(Roles.QuanLy) || user.IsInRole(Roles.KhachThue))
        && !string.IsNullOrWhiteSpace(user.FindFirstValue(ClaimTypes.NameIdentifier));

    private static bool IsTenant(ClaimsPrincipal user) =>
        !user.IsInRole(Roles.ChuTro) && !user.IsInRole(Roles.QuanLy);

    private async Task<ClaimsPrincipal> CallerAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (!IsAllowed(user)) throw new IncidentDemoException("AccessDenied");
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (_sessionUserId is not null && _sessionUserId != id)
            throw new IncidentDemoException("SessionChanged");
        _sessionUserId = id;
        return user;
    }

    private static bool InScope(ClaimsPrincipal user, int roomId) => DemoRooms.Any(r => r.Id == roomId
        && (user.IsInRole(Roles.ChuTro) || (user.IsInRole(Roles.QuanLy) ? r.PropertyId == 1 : r.Id == 101)));

    public async Task InitializeAsync()
    {
        var user = await CallerAsync();
        if (_items.Count > 0) return;
        var now = time.GetUtcNow().UtcDateTime;
        // Phiếu mẫu ở hai nhà/phòng để kiểm tra phạm vi; không chứa dữ liệu người thuê thật.
        _items.Add(new(101, 101, "SampleTitleWater", "SampleDescriptionWater", IncidentDemoCategory.Water,
            IncidentDemoUrgency.Urgent, IncidentDemoStatus.New, now.AddDays(-2), "", [],
            [new(now.AddDays(-2), "ActorTenant", "EventCreated")], []));
        _items.Add(new(102, 102, "SampleTitleElectrical", "SampleDescriptionElectrical", IncidentDemoCategory.Electrical,
            IncidentDemoUrgency.Emergency, IncidentDemoStatus.InProgress, now.AddDays(-1), "", [],
            [new(now.AddDays(-1), "ActorTenant", "EventCreated"), new(now.AddHours(-4), "ActorManager", "EventStarted")], []));
        _items.Add(new(103, 201, "SampleTitleEquipment", "SampleDescriptionEquipment", IncidentDemoCategory.Equipment,
            IncidentDemoUrgency.Normal, IncidentDemoStatus.Completed, now.AddDays(-3), "", [],
            [new(now.AddDays(-3), "ActorTenant", "EventCreated"), new(now.AddDays(-2), "ActorOwner", "EventStarted"),
                new(now.AddDays(-1), "ActorOwner", "EventCompleted")], []));
        // Một ảnh PNG minh họa mẫu trong bộ nhớ, gắn đúng người báo mô phỏng.
        var illustration = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a5QAAAABJRU5ErkJggg==");
        _items[0] = _items[0] with { Photos = [new("sample.png", illustration, UploadedBy: user.FindFirstValue(ClaimTypes.NameIdentifier)!)] };
        for (var i = 0; i < _items.Count; i++) _items[i] = _items[i] with { IsSeeded = true };
    }

    public async Task<IReadOnlyList<IncidentDemoRoom>> RoomsAsync()
    {
        var user = await CallerAsync();
        return DemoRooms.Where(r => InScope(user, r.Id)).ToArray();
    }

    public async Task<IncidentDemoMode> ModeAsync(IncidentDemoAction action)
    {
        var user = await CallerAsync();
        return Mode(user, action);
    }

    private IncidentDemoMode Mode(ClaimsPrincipal user, IncidentDemoAction action) => user.IsInRole(Roles.ChuTro)
        || (IsTenant(user) && action == IncidentDemoAction.Create) ? IncidentDemoMode.Allow
        : IsTenant(user) ? IncidentDemoMode.Deny : _modes[action];

    public async Task SetModeAsync(IncidentDemoAction action, IncidentDemoMode mode)
    {
        var user = await CallerAsync();
        if (!user.IsInRole(Roles.QuanLy) || !Enum.IsDefined(action) || !Enum.IsDefined(mode))
            throw new IncidentDemoException("AccessDenied");
        _modes[action] = mode;
    }

    private static IncidentDemoItem Visible(ClaimsPrincipal user, IncidentDemoItem item) => !IsTenant(user) ? item : item with
    {
        TechnicalNotes = "",
        Photos = item.Photos.Where(p => p.UploadedBy == user.FindFirstValue(ClaimTypes.NameIdentifier)).ToArray(),
        Pending = []
    };

    public async Task<IReadOnlyList<IncidentDemoItem>> ListAsync()
    {
        var user = await CallerAsync();
        return _items.Where(i => InScope(user, i.RoomId) && (!IsTenant(user) || !i.AwaitingCreation))
            .Select(i => Visible(user, i)).ToArray();
    }

    public async Task<IncidentDemoItem> DetailAsync(int id)
    {
        var user = await CallerAsync();
        var item = Find(user, id);
        return Visible(user, item);
    }

    private IncidentDemoItem Find(ClaimsPrincipal user, int id) =>
        _items.FirstOrDefault(i => i.Id == id && InScope(user, i.RoomId) && (!IsTenant(user) || !i.AwaitingCreation))
        ?? throw new IncidentDemoException("NotFound");

    private static void CheckPhotos(IReadOnlyList<IncidentDemoPhoto> photos, int occupied = 0)
    {
        if (photos.Count + occupied > IncidentDemoPhoto.MaxCount) throw new IncidentDemoException("PhotoLimit");
        if (photos.Any(p => !p.IsValid)) throw new IncidentDemoException("PhotoInvalid");
    }

    public async Task<bool> CreateAsync(IncidentDemoForm form)
    {
        var user = await CallerAsync();
        var mode = Mode(user, IncidentDemoAction.Create);
        if (mode == IncidentDemoMode.Deny) throw new IncidentDemoException("ActionDenied");
        if (!InScope(user, form.RoomId) || !DemoRooms.Any(r => r.Id == form.RoomId && r.PropertyId == form.PropertyId))
            throw new IncidentDemoException("RoomRequired");
        if (form.Title.Trim().Length < 5) throw new IncidentDemoException("TitleInvalid");
        if (form.Description.Trim().Length < 10) throw new IncidentDemoException("DescriptionInvalid");
        if (!Enum.IsDefined(form.Category) || !Enum.IsDefined(form.Urgency)) throw new IncidentDemoException("ValueInvalid");
        CheckPhotos(form.Photos);
        var now = time.GetUtcNow().UtcDateTime;
        var photos = form.Photos.Select(p => p with { UploadedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)!, IsResult = false }).ToArray();
        var pending = mode == IncidentDemoMode.Approval;
        _items.Add(new(_nextId++, form.RoomId, form.Title.Trim(), form.Description.Trim(), form.Category,
            form.Urgency, IncidentDemoStatus.New, now, "", pending ? [] : photos,
            [new(now, Actor(user), pending ? "EventRequested" : "EventCreated")],
            pending ? [new(IncidentDemoAction.Create, null, "", photos, now)] : [], pending));
        return pending;
    }

    public async Task<bool> UpdateAsync(int id, IncidentDemoStatus next, string notes)
    {
        var user = await CallerAsync();
        var item = Find(user, id);
        var mode = Mode(user, IncidentDemoAction.Update);
        if (mode == IncidentDemoMode.Deny) throw new IncidentDemoException("ActionDenied");
        if (item.AwaitingCreation || !Enum.IsDefined(next) || item.Status == IncidentDemoStatus.Completed
            || (int)next != (int)item.Status + 1) throw new IncidentDemoException("TransitionInvalid");
        if (item.Pending.Any(p => p.Action == IncidentDemoAction.Update)) throw new IncidentDemoException("AlreadyPending");
        var now = time.GetUtcNow().UtcDateTime;
        var pending = mode == IncidentDemoMode.Approval;
        Replace(item, item with
        {
            Status = pending ? item.Status : next,
            TechnicalNotes = pending ? item.TechnicalNotes : notes.Trim(),
            Pending = pending ? [.. item.Pending, new(IncidentDemoAction.Update, next, notes.Trim(), [], now)] : item.Pending,
            History = [.. item.History, new(now, Actor(user), pending ? "EventRequested" : next == IncidentDemoStatus.InProgress ? "EventStarted" : "EventCompleted")]
        });
        return pending;
    }

    public async Task<bool> AddResultPhotosAsync(int id, IReadOnlyList<IncidentDemoPhoto> photos)
    {
        var user = await CallerAsync();
        var item = Find(user, id);
        var mode = Mode(user, IncidentDemoAction.ResultPhoto);
        if (mode == IncidentDemoMode.Deny) throw new IncidentDemoException("ActionDenied");
        if (item.AwaitingCreation || photos.Count == 0) throw new IncidentDemoException("PhotoRequired");
        CheckPhotos(photos, item.Photos.Count + item.Pending.Sum(p => p.Photos.Count));
        var now = time.GetUtcNow().UtcDateTime;
        var uploaded = photos.Select(p => p with { IsResult = true, UploadedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)! }).ToArray();
        var pending = mode == IncidentDemoMode.Approval;
        Replace(item, item with
        {
            Photos = pending ? item.Photos : [.. item.Photos, .. uploaded],
            Pending = pending ? [.. item.Pending, new(IncidentDemoAction.ResultPhoto, null, "", uploaded, now)] : item.Pending,
            History = [.. item.History, new(now, Actor(user), pending ? "EventRequested" : "EventPhotos")]
        });
        return pending;
    }

    private void Replace(IncidentDemoItem old, IncidentDemoItem updated) => _items[_items.IndexOf(old)] = updated;
    private static string Actor(ClaimsPrincipal user) => user.IsInRole(Roles.ChuTro) ? "ActorOwner"
        : user.IsInRole(Roles.QuanLy) ? "ActorManager" : "ActorTenant";
}
