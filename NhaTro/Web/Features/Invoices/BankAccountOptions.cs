namespace Web.Features.Invoices;

/// <summary>
/// Tài khoản ngân hàng nhận tiền đã cấu hình (cho VietQR thanh toán hóa đơn).
/// Cấu hình qua khóa `BankAccount` trong appsettings + User Secrets.
/// </summary>
public sealed class BankAccountOptions
{
    public const string SectionName = "BankAccount";

    /// <summary>Mã ngân hàng (VD: BIDV, VCB, OCB...).</summary>
    public string Code { get; set; } = "BIDV";

    /// <summary>Tên ngân hàng hiển thị.</summary>
    public string Name { get; set; } = "BIDV";

    public string AccountNumber { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(AccountName);

    public string DisplayAccount => $"{Name} · {AccountNumber} · {AccountName}";
}
