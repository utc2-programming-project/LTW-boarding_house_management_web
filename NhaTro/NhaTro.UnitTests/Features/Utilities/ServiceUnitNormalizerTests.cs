using Web.Features.Utilities.Rules;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class ServiceUnitNormalizerTests
{
    [Theory]
    [InlineData("kWh")]
    [InlineData("kWh/tháng")]
    [InlineData("KW H")]
    [InlineData("m3")]
    [InlineData("m³/tháng")]
    public void Classify_RecognizesMeteredVariants(string unit) =>
        Assert.Equal(ServiceUnitKind.Metered, ServiceUnitNormalizer.Classify(unit));

    [Theory]
    [InlineData("Người")]
    [InlineData("nguoi/thang")]
    [InlineData("person/month")]
    public void Classify_RecognizesPerPersonVariants(string unit) =>
        Assert.Equal(ServiceUnitKind.PerPerson, ServiceUnitNormalizer.Classify(unit));

    [Theory]
    [InlineData("tháng")]
    [InlineData("lần")]
    public void Classify_RecognizesFixedVariants(string unit) =>
        Assert.Equal(ServiceUnitKind.Fixed, ServiceUnitNormalizer.Classify(unit));

    [Theory]
    [InlineData("phòng m3")]
    [InlineData("superperson")]
    public void Classify_DoesNotTreatEmbeddedWordsAsSpecialUnits(string unit) =>
        Assert.Equal(ServiceUnitKind.Fixed, ServiceUnitNormalizer.Classify(unit));
}
