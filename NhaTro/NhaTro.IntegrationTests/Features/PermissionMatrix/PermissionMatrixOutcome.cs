namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>Kết quả một lần gọi nghiệp vụ thật, chuẩn hóa về ba trạng thái quyền.</summary>
public enum ProbeVerdict
{
    /// <summary>Thao tác được thực hiện (dữ liệu thật sự thay đổi/trả về trong phạm vi).</summary>
    Allowed,

    /// <summary>Thao tác bị từ chối ở tầng backend (exception hoặc result mang mã lỗi quyền/phạm vi).</summary>
    Denied,

    /// <summary>Không có hiệu lực ngay: nghiệp vụ trả "chờ duyệt" theo công tắc Quản lý (ma trận ⚙️).</summary>
    PendingApproval,

    /// <summary>Ô chưa chứng minh được: thiếu service/endpoint thật hoặc bộ probe chưa覆盖.</summary>
    Blocked,

    /// <summary>
    /// Lỗi không phải tín hiệu phân quyền (validation, trùng dữ liệu, lỗi DB...).
    /// Không bao giờ được tính là "bị từ chối": mọi ô đều FAIL để tránh rửa điểm.
    /// </summary>
    Error,
}

/// <summary>Tầng kiểm tra, kèm lý do chọn tầng theo yêu cầu task VHT-101 mục VIII.3.</summary>
public enum ProbeTier
{
    /// <summary>Gọi endpoint/form thật qua <see cref="Infrastructure.WebAppFixture"/> (đăng nhập, phân quyền trang).</summary>
    Http,

    /// <summary>Gọi service nghiệp vụ thật trên database test, có kiểm tra hiệu lực ghi dữ liệu.</summary>
    Service,

    /// <summary>
    /// Chỉ chạy được tới policy/công tắc (đọc quyết định từ DB) vì nghiệp vụ phía sau chưa có API.
    /// Không được tính là đã chứng minh thao tác nghiệp vụ.
    /// </summary>
    Policy,

    /// <summary>Gọi service mẫu P5 (trạng thái trong bộ nhớ, chưa bền vững).</summary>
    SampleState,
}

/// <summary>
/// Một tình huống con bắt buộc phải chứng minh trong cùng một ô ma trận.
/// Ô ⚠️ cần Case A (trong phạm vi -> được phép) và Case B (ngoài phạm vi -> bị từ chối);
/// ô ⚙️ cần một subcase cho mỗi chế độ ghi trong sheet Công tắc Quản lý.
/// </summary>
public sealed record ProbeSubcase(
    string Name,
    ProbeVerdict Verdict,
    string Evidence,
    string? ErrorCode = null);

public sealed record ProbeOutcome(
    ProbeVerdict Verdict,
    string Evidence,
    ProbeTier Tier = ProbeTier.Service,
    IReadOnlyList<ProbeSubcase>? Subcases = null,
    string? ErrorCode = null)
{
    /// <summary>Tình huống con của ô này; ô một tình huống có đúng một phần tử.</summary>
    public IReadOnlyList<ProbeSubcase> Cases { get; } =
        Subcases is { Count: > 0 } ? Subcases : [new ProbeSubcase("default", Verdict, Evidence, ErrorCode)];

    public static ProbeOutcome Allowed(string evidence = "thao tác thành công", ProbeTier tier = ProbeTier.Service) =>
        new(ProbeVerdict.Allowed, evidence, tier);

    public static ProbeOutcome Denied(
        string evidence,
        ProbeTier tier = ProbeTier.Service,
        string? errorCode = null) =>
        new(ProbeVerdict.Denied, evidence, tier, null, errorCode ?? InferDenialCode(evidence));

    /// <summary>
    /// Mã lỗi quyền ghi kèm mọi phán quyết Denied. Các probe cũ chỉ nêu exception/message;
    /// ở đây lấy mã service công bố nếu có, nếu không thì chuẩn hóa về Forbidden để báo cáo
    /// luôn có mã lỗi quyền truy vết được (yêu cầu VHT-101), không để trống.
    /// </summary>
    private static string InferDenialCode(string evidence)
    {
        const string marker = "ErrorCode=";
        var index = evidence.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var start = index + marker.Length;
            var length = 0;
            while (start + length < evidence.Length
                   && (char.IsLetterOrDigit(evidence[start + length]) || evidence[start + length] is '_' or '.'))
            {
                length++;
            }

            if (length > 0)
            {
                return evidence.Substring(start, length);
            }
        }

        if (evidence.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("Authorization", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("BusinessGuard", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("không có quyền", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("không được", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("bị từ chối", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("bị chặn", StringComparison.OrdinalIgnoreCase))
        {
            return "Forbidden";
        }

        // Denied luôn là kết luận quyền; mặc định dùng mã quyền chuẩn thay vì bỏ trống.
        return "Forbidden";
    }

    public static ProbeOutcome Pending(
        string evidence,
        ProbeTier tier = ProbeTier.Service,
        string? errorCode = null) =>
        new(ProbeVerdict.PendingApproval, evidence, tier, null, errorCode);

    public static ProbeOutcome Error(
        string evidence,
        ProbeTier tier = ProbeTier.Service,
        string? errorCode = null) =>
        new(ProbeVerdict.Error, evidence, tier, null, errorCode);

    public static ProbeOutcome Blocked(string reason) => new(ProbeVerdict.Blocked, reason, ProbeTier.Service);

    /// <summary>
    /// Gom nhiều tình huống con (Case A/Case B, hoặc từng chế độ công tắc) vào một kết quả ô.
    /// Verdict tổng chỉ mang tính tham khảo; bộ đối chiếu bắt buộc đọc <see cref="Cases"/>.
    /// </summary>
    public static ProbeOutcome Multi(ProbeTier tier, params ProbeSubcase[] subcases)
    {
        if (subcases.Length == 0)
        {
            throw new ArgumentException("Ô ma trận phải có ít nhất một tình huống con.", nameof(subcases));
        }

        var aggregate = subcases.Any(s => s.Verdict == ProbeVerdict.Error)
            ? ProbeVerdict.Error
            : subcases.All(s => s.Verdict == ProbeVerdict.Denied)
                ? ProbeVerdict.Denied
                : subcases.All(s => s.Verdict == ProbeVerdict.PendingApproval)
                    ? ProbeVerdict.PendingApproval
                    : ProbeVerdict.Allowed;

        var evidence = string.Join("; ", subcases.Select(s => $"{s.Name}={s.Verdict}: {s.Evidence}"));
        var errorCode = subcases.Select(s => s.ErrorCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));
        return new ProbeOutcome(aggregate, evidence, tier, subcases, errorCode);
    }

    /// <summary>
    /// Ô ⚠️: Case A = làm được trong phạm vi, Case B = bị chặn ngoài phạm vi.
    /// Hai boolean này do probe tính từ dữ liệu thật, không suy ra từ ký hiệu ma trận.
    /// </summary>
    public static ProbeOutcome ScopePair(
        bool inScopeAllowed,
        bool outOfScopeDenied,
        string what,
        string? outOfScopeErrorCode = null) =>
        Multi(
            ProbeTier.Service,
            new ProbeSubcase(
                "A-trong-pham-vi",
                inScopeAllowed ? ProbeVerdict.Allowed : ProbeVerdict.Denied,
                inScopeAllowed ? $"Làm được trong phạm vi: {what}" : $"KHÔNG làm được trong phạm vi: {what}",
                inScopeAllowed ? null : "IN_SCOPE_BLOCKED"),
            new ProbeSubcase(
                "B-ngoai-pham-vi",
                outOfScopeDenied ? ProbeVerdict.Denied : ProbeVerdict.Allowed,
                outOfScopeDenied ? $"Bị chặn ngoài phạm vi đúng: {what}" : $"RÒ RỠ: không bị chặn khi {what} ngoài phạm vi",
                outOfScopeDenied ? outOfScopeErrorCode ?? "OUT_OF_SCOPE" : null));
}

/// <summary>Dịch exception của service thật sang kết quả quyền.</summary>
public static class ProbeRunner
{
    // VHT-51 dùng thuộc tính Code; các service cũ dùng ErrorCode.
    public static string? ErrorCodeOf(Exception exception) =>
        (exception.GetType().GetProperty("ErrorCode") ?? exception.GetType().GetProperty("Code"))
            ?.GetValue(exception) as string;
}

/// <summary>Kết quả một ô ma trận sau khi chạy test, dùng cho báo cáo theo ô.</summary>
public sealed record CellResult(
    MatrixCell Cell,
    string Case,
    string TestId,
    ProbeTier Tier,
    string Expected,
    string Actual,
    string Status,
    string Evidence,
    string? ErrorCode,
    DateTimeOffset At,
    IReadOnlyList<ProbeSubcase> Subcases = null!,
    string? Service = null,
    string? UnsupportedReason = null)
{
    /// <summary>Số tình huống con đã chứng minh (Case A/B với ⚠️, số chế độ với ⚙️).</summary>
    public int SubcaseCount => Subcases?.Count ?? 0;

    public string SubcaseDetail => Subcases is null
        ? string.Empty
        : string.Join(" | ", Subcases.Select(s => $"{s.Name}: {s.Verdict}"));
}
