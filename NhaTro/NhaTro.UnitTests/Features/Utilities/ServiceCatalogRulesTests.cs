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
    public void CreateValidation_RejectsEffectiveToBeforeEffectiveDate()
    {
        var request = ValidCreateRequest();
        request.EffectiveDate = new DateOnly(2026, 6, 1);
        request.EffectiveTo = new DateOnly(2026, 5, 31);

        Assert.Contains(nameof(request.EffectiveTo), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void CreateValidation_AcceptsOpenEndedAndSameDayEffectiveTo()
    {
        var request = ValidCreateRequest();
        request.EffectiveDate = new DateOnly(2026, 6, 1);

        request.EffectiveTo = null;
        Assert.DoesNotContain(nameof(request.EffectiveTo), ServiceCatalogRules.Validate(request));

        request.EffectiveTo = new DateOnly(2026, 6, 1);
        Assert.DoesNotContain(nameof(request.EffectiveTo), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void UpdateValidation_RejectsEffectiveToBeforeEffectiveDate()
    {
        var request = new UpdateServicePriceDto
        {
            Name = "Internet",
            Unit = "phòng",
            CalculationMethod = "fixed-monthly",
            UnitPrice = 100_000m,
            EffectiveDate = new DateOnly(2026, 6, 1),
            EffectiveTo = new DateOnly(2026, 5, 1),
        };

        Assert.Contains(nameof(request.EffectiveTo), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void CreateValidation_RejectsNoteExceeding500Characters()
    {
        var request = ValidCreateRequest();
        request.Note = new string('A', 501);

        Assert.Contains(nameof(request.Note), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void CreateValidation_AcceptsNoteWithin500Characters()
    {
        var request = ValidCreateRequest();
        request.Note = new string('A', 500);

        Assert.DoesNotContain(nameof(request.Note), ServiceCatalogRules.Validate(request));
    }

    [Fact]
    public void UpdateValidation_RejectsNoteExceeding500Characters()
    {
        var request = new UpdateServicePriceDto
        {
            Name = "WiFi",
            Unit = "phòng",
            CalculationMethod = "fixed-monthly",
            UnitPrice = 100_000m,
            EffectiveDate = new DateOnly(2026, 6, 1),
            Note = new string('B', 501),
        };

        Assert.Contains(nameof(request.Note), ServiceCatalogRules.Validate(request));
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
    public void CreateValidation_RejectsZeroPrice()
    {
        var request = ValidCreateRequest();
        request.UnitPrice = 0m;

        Assert.Contains(nameof(request.UnitPrice), ServiceCatalogRules.Validate(request));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void CreateValidation_AllowsPositivePrice(decimal unitPrice)
    {
        var request = ValidCreateRequest();
        request.UnitPrice = unitPrice;

        Assert.Empty(ServiceCatalogRules.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void UpdateValidation_RejectsNonPositiveUnitPrice(decimal unitPrice)
    {
        var request = ValidUpdateRequest();
        request.UnitPrice = unitPrice;

        Assert.Contains(nameof(request.UnitPrice), ServiceCatalogRules.Validate(request));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120_000)]
    public void UpdateValidation_AllowsPositiveUnitPrice(decimal unitPrice)
    {
        var request = ValidUpdateRequest();
        request.UnitPrice = unitPrice;

        Assert.Empty(ServiceCatalogRules.Validate(request));
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
