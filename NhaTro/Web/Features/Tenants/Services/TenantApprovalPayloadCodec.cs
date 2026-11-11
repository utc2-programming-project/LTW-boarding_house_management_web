using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Mã hoá số giấy tờ trong payload duyệt (SW-04/05) để không lưu PII dạng plaintext
/// trong bảng ManagerApprovalRequests. Payload cũ chưa mã hoá vẫn đọc được.
/// </summary>
public static class TenantApprovalPayloadCodec
{
    private const string Prefix = "dp:v1:";
    private const string Purpose = "NhaTro.Tenants.ApprovalPayload.IdNumber.v1";

    public static string? Protect(IDataProtectionProvider? provider, string? value)
    {
        if (provider is null || string.IsNullOrEmpty(value))
        {
            return value;
        }

        var protector = provider.CreateProtector(Purpose);
        return Prefix + protector.Protect(value);
    }

    public static string? Unprotect(IDataProtectionProvider? provider, string? value)
    {
        if (provider is null || string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            return provider.CreateProtector(Purpose).Unprotect(value[Prefix.Length..]);
        }
        catch (CryptographicException ex)
        {
            // Fail closed: không được âm thầm bỏ số giấy tờ khi key xoay/ hỏng.
            throw new InvalidOperationException("Không giải mã được số giấy tờ trong yêu cầu duyệt.", ex);
        }
    }
}
