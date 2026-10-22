namespace Web.Features.Accounts.DTOs;

/// <summary>
/// VHT-8: ket qua thao tac tai khoan. Cung khuon Succeeded/ErrorCode nhu TenantResult
/// de UI xu ly thong nhat.
/// </summary>
public sealed class AccountResult
{
    private AccountResult()
    {
    }

    public required bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }

    /// <summary>Tham so bo sung cho thong bao (vd ten vai tro). Null khi thanh cong.</summary>
    public string? ErrorDetail { get; init; }

    public static AccountResult Success() => new() { Succeeded = true };

    public static AccountResult Failure(string errorCode, string? detail = null) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorDetail = detail };

    public static AccountResult Failure<T>(AccountResultOf<T> result) =>
        new() { Succeeded = false, ErrorCode = result.ErrorCode, ErrorDetail = result.ErrorDetail };
}

/// <summary>Ket qua kem du lieu (vd ho so sau khi cap nhat).</summary>
public sealed class AccountResultOf<T>
{
    private AccountResultOf()
    {
    }

    public required bool Succeeded { get; init; }

    public T? Value { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorDetail { get; init; }

    public static AccountResultOf<T> Success(T value) => new() { Succeeded = true, Value = value };

    public static AccountResultOf<T> Failure(string errorCode, string? detail = null) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorDetail = detail };
}
