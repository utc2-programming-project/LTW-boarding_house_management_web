using Microsoft.AspNetCore.DataProtection;
using Web.Features.Tenants.Services;

namespace NhaTro.UnitTests.Features.Tenants;

public sealed class TenantApprovalPayloadCodecTests
{
    private readonly IDataProtectionProvider _provider = new EphemeralDataProtectionProvider();

    [Fact]
    public void Protect_then_Unprotect_round_trips_id_number()
    {
        const string idNumber = "079201000123";

        var protectedValue = TenantApprovalPayloadCodec.Protect(_provider, idNumber);

        Assert.NotNull(protectedValue);
        Assert.NotEqual(idNumber, protectedValue);
        Assert.DoesNotContain(idNumber, protectedValue);
        Assert.Equal(idNumber, TenantApprovalPayloadCodec.Unprotect(_provider, protectedValue));
    }

    [Fact]
    public void Unprotect_keeps_legacy_plain_payload()
    {
        const string legacy = "079201000123";

        Assert.Equal(legacy, TenantApprovalPayloadCodec.Unprotect(_provider, legacy));
    }

    [Fact]
    public void Protect_without_provider_keeps_plain_value()
    {
        const string idNumber = "079201000123";

        Assert.Equal(idNumber, TenantApprovalPayloadCodec.Protect(null, idNumber));
        Assert.Equal(idNumber, TenantApprovalPayloadCodec.Unprotect(null, idNumber));
    }

    [Fact]
    public void Null_or_empty_values_are_preserved()
    {
        Assert.Null(TenantApprovalPayloadCodec.Protect(_provider, null));
        Assert.Null(TenantApprovalPayloadCodec.Unprotect(_provider, null));
        Assert.Equal(string.Empty, TenantApprovalPayloadCodec.Protect(_provider, string.Empty));
    }
}
