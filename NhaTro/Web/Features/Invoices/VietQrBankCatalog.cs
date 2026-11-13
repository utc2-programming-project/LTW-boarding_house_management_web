namespace Web.Features.Invoices;

/// <summary>
/// Danh mục ngân hàng hỗ trợ VietQR: mã lưu trong hồ sơ chủ trọ → tên hiển thị + BIN
/// (BIN được ghi vào field 38 của payload EMV). BIN lấy theo danh sách Napas/VietQR.
/// </summary>
public static class VietQrBankCatalog
{
    public sealed record Bank(string Code, string Name, string Bin);

    public static IReadOnlyList<Bank> All { get; } =
    [
        new("VIETCOMBANK", "Vietcombank", "970436"),
        new("VIETINBANK", "VietinBank", "970415"),
        new("BIDV", "BIDV", "970418"),
        new("AGRIBANK", "Agribank", "970405"),
        new("TECHCOMBANK", "Techcombank", "970407"),
        new("MBBANK", "MB Bank", "970422"),
        new("ACB", "ACB", "970416"),
        new("VPBANK", "VPBank", "970432"),
        new("TPBANK", "TPBank", "970423"),
        new("SACOMBANK", "Sacombank", "970403"),
        new("HDBANK", "HDBank", "970437"),
        new("VIB", "VIB", "970441"),
        new("SHB", "SHB", "970443"),
        new("EXIMBANK", "Eximbank", "970431"),
        new("MSB", "MSB", "970426"),
        new("OCB", "OCB", "970448"),
        new("LPBANK", "LPBank", "970449"),
        new("SEABANK", "SeABank", "970440"),
        new("PVCOMBANK", "PVcomBank", "970412"),
        new("NAMABANK", "Nam A Bank", "970428"),
        new("BACABANK", "Bac A Bank", "970409"),
        new("SCB", "SCB", "970429"),
        new("PGBANK", "PGBank", "970430"),
        new("VIETABANK", "VietABank", "970427"),
        new("OCEANBANK", "OceanBank", "970414"),
        new("KIENLONGBANK", "KienLongBank", "970452"),
        new("BAOVIETBANK", "BaoViet Bank", "970438"),
        new("SAIGONBANK", "Saigonbank", "970400"),
        new("VIETBANK", "VietBank", "970433"),
        new("ABBANK", "ABBank", "970425"),
        new("BVBANK", "BVBank", "970454"),
        new("GPBANK", "GPBank", "970408"),
    ];

    /// <summary>Tra cứu theo mã (không phân biệt hoa thường, chấp nhận mã cũ như ICB/VCB).</summary></summary>
    public static Bank? Find(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return normalized switch
        {
            "ICB" => FindExact("VIETINBANK"),
            "VCB" => FindExact("VIETCOMBANK"),
            "VBA" => FindExact("AGRIBANK"),
            "MB" => FindExact("MBBANK"),
            "TCB" => FindExact("TECHCOMBANK"),
            "VPB" => FindExact("VPBANK"),
            "TPB" => FindExact("TPBANK"),
            "STB" => FindExact("SACOMBANK"),
            "HDB" => FindExact("HDBANK"),
            "EIB" => FindExact("EXIMBANK"),
            _ => FindExact(normalized),
        };
    }

    /// <summary>BIN của ngân hàng; chuỗi rỗng khi mã không nằm trong danh mục.</summary>
    public static string GetBin(string bankCode) => Find(bankCode)?.Bin ?? string.Empty;

    /// <summary>Tên hiển thị của ngân hàng; rơi về chính mã khi không tìm thấy.</summary>
    public static string GetName(string bankCode) => Find(bankCode)?.Name ?? bankCode?.Trim() ?? string.Empty;

    private static Bank? FindExact(string code) =>
        All.FirstOrDefault(bank => string.Equals(bank.Code, code, StringComparison.OrdinalIgnoreCase));
}
