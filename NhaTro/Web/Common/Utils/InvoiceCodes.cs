namespace Web.Common.Utils;

/// <summary>
/// Mã hiển thị của hóa đơn ở một chỗ duy nhất: UI đang dùng <c>HD-{id}</c>, tìm kiếm
/// cũng phải dùng đúng dạng đó và vẫn nhận kiểu gõ cũ <c>HD{id}</c>.
/// Tiền tố là mã nghiệp vụ, không phải chuỗi bản địa hoá.
/// </summary>
public static class InvoiceCodes
{
    public const string Prefix = "HD";

    public static string Display(int invoiceId) => $"{Prefix}-{invoiceId}";

    public static string Legacy(int invoiceId) => $"{Prefix}{invoiceId}";
}
