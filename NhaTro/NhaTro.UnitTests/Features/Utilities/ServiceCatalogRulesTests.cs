using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Rules;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class ServiceCatalogRulesTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void CreateValidation_RejectsNegativeUnitPrice(decimal unitPrice)
    {
        var request = ValidCreateRequest();
        request.UnitPrice = unitPrice;

        Assert.Contains(nameof(request.UnitPrice), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void CreateValidation_RejectsMissingUnit()
    {
        var request = ValidCreateRequest();
        request.Unit = "  ";

        Assert.Contains(nameof(request.Unit), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void CreateValidation_RejectsMissingName()
    {
        var request = ValidCreateRequest();
        request.Name = " ";

        Assert.Contains(nameof(request.Name), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void CreateValidation_AllowsZeroPrice()
    {
        var request = ValidCreateRequest();
        request.UnitPrice = 0m;

        Assert.Empty(ServiceCatalogRules.Validate(request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void UpdateValidation_RejectsNegativeUnitPrice(decimal unitPrice)
    {
        var request = ValidUpdateRequest();
        request.UnitPrice = unitPrice;

        Assert.Contains(nameof(request.UnitPrice), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void UpdateValidation_RejectsMissingUnit()
    {
        var request = ValidUpdateRequest();
        request.Unit = string.Empty;

        Assert.Contains(nameof(request.Unit), ServiceCatalogRules.Validate(request));
    }

    private static CreateServiceCatalogDto ValidCreateRequest() => new()
    {
        Name = "Internet",
        Unit = "phòng",
        CalculationMethod = "fixed-monthly",
        UnitPrice = 100_000m,
        EffectiveDate = new DateOnly(2026, 11, 1),
    };

    private static UpdateServicePriceDto ValidUpdateRequest() => new()
    {
        Name = "Internet",
        Unit = "phòng",
        CalculationMethod = "fixed-monthly",
        UnitPrice = 120_000m,
        EffectiveDate = new DateOnly(2026, 12, 1),
    };
}
