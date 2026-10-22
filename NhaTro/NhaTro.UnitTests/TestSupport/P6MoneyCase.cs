namespace NhaTro.UnitTests.TestSupport;

/// <summary>Loại case nền VHT-14: thành công, sai dữ liệu, sai quyền.</summary>
public enum P6CaseKind
{
    Success = 0,
    BadData = 1,
    Unauthorized = 2,
}

/// <summary>
/// VHT-14 (đợt 1A): dữ liệu tiền mẫu P6 để dựng case trước khi VHT-34 chốt DTO/rule thật.
/// Không phải logic production; khi VHT-34 có DTO thật thì thay số liệu và gọi service thật.
/// </summary>
public sealed record P6MoneyCase(
    string Code,
    string Name,
    P6CaseKind Kind,
    decimal Rent,
    decimal Electricity,
    decimal Water,
    decimal Service,
    decimal Discount,
    string CallerRole,
    string RequiredRole,
    string? ExpectedError = null)
{
    /// <summary>Tổng tiền tham chiếu: tiền thuê + điện + nước + dịch vụ - giảm giá.</summary>
    public decimal ExpectedTotal => Rent + Electricity + Water + Service - Discount;

    public bool HasValidAmounts =>
        Rent >= 0m && Electricity >= 0m && Water >= 0m && Service >= 0m && Discount >= 0m;

    public bool IsAuthorized => string.Equals(CallerRole, RequiredRole, StringComparison.Ordinal);
}

/// <summary>Bộ case P6 tối thiểu theo tiêu chí VHT-14.</summary>
public static class P6MoneyCaseCatalog
{
    public static readonly P6MoneyCase Success = new(
        Code: "P6-MONEY-OK",
        Name: "Hóa đơn đủ khoản, có giảm giá",
        Kind: P6CaseKind.Success,
        Rent: 3_000_000m,
        Electricity: 700_000m,
        Water: 120_000m,
        Service: 100_000m,
        Discount: 50_000m,
        CallerRole: "ChuTro",
        RequiredRole: "ChuTro");

    public static readonly P6MoneyCase BadData = new(
        Code: "P6-MONEY-BAD",
        Name: "Số điện âm",
        Kind: P6CaseKind.BadData,
        Rent: 3_000_000m,
        Electricity: -3_500m,
        Water: 120_000m,
        Service: 100_000m,
        Discount: 0m,
        CallerRole: "ChuTro",
        RequiredRole: "ChuTro",
        ExpectedError: "Số điện không được âm");

    public static readonly P6MoneyCase Unauthorized = new(
        Code: "P6-MONEY-DENY",
        Name: "Khách thuê không được lập hóa đơn",
        Kind: P6CaseKind.Unauthorized,
        Rent: 3_000_000m,
        Electricity: 700_000m,
        Water: 120_000m,
        Service: 100_000m,
        Discount: 0m,
        CallerRole: "KhachThue",
        RequiredRole: "ChuTro",
        ExpectedError: "Không có quyền lập hóa đơn");

    public static IReadOnlyList<P6MoneyCase> All { get; } = [Success, BadData, Unauthorized];
}
