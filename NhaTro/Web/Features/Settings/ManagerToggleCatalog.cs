using Web.Features.Settings.Enums;

namespace Web.Features.Settings;

/// <summary>
/// Danh mục 21 công tắc Quản lý (VHT-94): 16 công tắc chính SW-01…SW-16 và 5 công tắc phụ
/// SW-15a…SW-15e. Nguồn: docs/task/thao_tac_va_quyen.xlsx, sheet "Công tắc Quản lý".
/// Đây là hợp đồng mã công tắc dùng chung cho service, policy và UI (NEW-04).
/// </summary>
public static class ManagerToggleCatalog
{
    /// <summary>Một công tắc: mã, tên hiển thị, phân hệ, chế độ mặc định và các chế độ được phép.</summary>
    public sealed record Definition(
        string Code,
        string Name,
        string Group,
        ManagerToggleMode DefaultMode,
        IReadOnlyList<ManagerToggleMode> AllowedModes)
    {
        public bool Allows(ManagerToggleMode mode) => AllowedModes.Contains(mode);

        /// <summary>Công tắc phụ SW-15a…SW-15e (dài hơn SW-15 đúng một ký tự).</summary>
        public bool IsSubSwitch => Code.Length > 5;
    }

    // 14 công tắc P2–P7 + SW-16 P8: đủ 3 chế độ, mặc định Duyệt.
    private static readonly ManagerToggleMode[] ThreeModes =
        [ManagerToggleMode.Allow, ManagerToggleMode.Approve, ManagerToggleMode.Deny];

    // SW-15 và công tắc phụ theo mục báo cáo: chỉ Cho phép / Không thể (D12), mặc định Cho phép.
    private static readonly ManagerToggleMode[] TwoModes =
        [ManagerToggleMode.Allow, ManagerToggleMode.Deny];

    /// <summary>Toàn bộ công tắc theo đúng thứ tự trình bày: 16 chính, rồi 5 công tắc phụ.</summary>
    public static IReadOnlyList<Definition> All { get; } =
    [
        new("SW-01", "Sửa thông tin phòng", "P2", ManagerToggleMode.Approve, ThreeModes),
        new("SW-02", "Thiết lập / đổi giá phòng", "P2", ManagerToggleMode.Approve, ThreeModes),
        new("SW-03", "Đổi trạng thái phòng", "P2", ManagerToggleMode.Approve, ThreeModes),
        new("SW-04", "Tạo hồ sơ khách thuê", "P3", ManagerToggleMode.Approve, ThreeModes),
        new("SW-05", "Sửa hồ sơ khách thuê", "P3", ManagerToggleMode.Approve, ThreeModes),
        new("SW-06", "Xóa hồ sơ khách thuê", "P3", ManagerToggleMode.Approve, ThreeModes),
        new("SW-07", "Gia hạn / thanh lý hợp đồng", "P4", ManagerToggleMode.Approve, ThreeModes),
        new("SW-08", "Cập nhật tiền cọc", "P4", ManagerToggleMode.Approve, ThreeModes),
        new("SW-09", "Sửa chỉ số kỳ liền trước", "P5", ManagerToggleMode.Approve, ThreeModes),
        new("SW-10", "Tạo / lập hóa đơn", "P6", ManagerToggleMode.Approve, ThreeModes),
        new("SW-11", "Phát hành hóa đơn", "P6", ManagerToggleMode.Approve, ThreeModes),
        new("SW-12", "Tạo phiếu bảo trì", "P7", ManagerToggleMode.Approve, ThreeModes),
        new("SW-13", "Cập nhật trạng thái xử lý sự cố", "P7", ManagerToggleMode.Approve, ThreeModes),
        new("SW-14", "Đính kèm ảnh kết quả", "P7", ManagerToggleMode.Approve, ThreeModes),
        new("SW-15", "Xem báo cáo", "P8", ManagerToggleMode.Allow, TwoModes),
        new("SW-16", "Xuất báo cáo (Excel)", "P8", ManagerToggleMode.Approve, ThreeModes),
        new("SW-15a", "Xem báo cáo — mục Phòng", "P8", ManagerToggleMode.Allow, TwoModes),
        new("SW-15b", "Xem báo cáo — mục Khách thuê", "P8", ManagerToggleMode.Allow, TwoModes),
        new("SW-15c", "Xem báo cáo — mục Hợp đồng", "P8", ManagerToggleMode.Allow, TwoModes),
        new("SW-15d", "Xem báo cáo — mục Hóa đơn", "P8", ManagerToggleMode.Allow, TwoModes),
        new("SW-15e", "Xem báo cáo — mục Doanh thu / công nợ", "P8", ManagerToggleMode.Allow, TwoModes),
    ];

    private static readonly Dictionary<string, Definition> ByCode =
        All.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Chuẩn hóa mã người dùng/DB nhập: bỏ khoảng trắng, đổi '_' thành '-', thêm '-' nếu thiếu,
    /// không phân biệt hoa thường. Nhờ vậy "sw 02", "SW_02", "SW02" đều về "SW-02".
    /// </summary>
    public static string NormalizeCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim()
            .Replace(" ", string.Empty)
            .Replace('_', '-')
            .ToUpperInvariant();

        // "SW02" / "SW15A" → "SW-02" / "SW-15A".
        if (normalized.StartsWith("SW", StringComparison.Ordinal)
            && !normalized.StartsWith("SW-", StringComparison.Ordinal)
            && normalized.Length > 2)
        {
            normalized = "SW-" + normalized[2..];
        }

        return normalized;
    }

    /// <summary>Tìm công tắc theo mã đã chuẩn hóa.</summary>
    public static bool TryFind(string? code, out Definition definition) =>
        ByCode.TryGetValue(NormalizeCode(code), out definition!);

    public static Definition? Find(string? code) =>
        TryFind(code, out var definition) ? definition : null;

    public static bool IsKnown(string? code) => TryFind(code, out _);

    /// <summary>Mã công tắc của một thao tác: SW01 → "SW-01".</summary>
    public static string ToCode(ManagerAction action) => $"SW-{(int)action:D2}";

    /// <summary>Mã công tắc của một thao tác; null khi giá trị enum không thuộc 16 thao tác.</summary>
    public static bool TryToAction(string? code, out ManagerAction action)
    {
        action = default;
        var normalized = NormalizeCode(code);
        foreach (var value in Enum.GetValues<ManagerAction>())
        {
            if (string.Equals($"SW-{(int)value:D2}", normalized, StringComparison.OrdinalIgnoreCase))
            {
                action = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>Chế độ mặc định theo ma trận (Duyệt; riêng SW-15 nhóm báo cáo là Cho phép).</summary>
    public static ManagerToggleMode DefaultModeOf(string? code) =>
        TryFind(code, out var definition) ? definition.DefaultMode : ManagerToggleMode.Approve;

    public static bool IsAllowed(string? code, ManagerToggleMode mode) =>
        TryFind(code, out var definition) && definition.Allows(mode);
}
