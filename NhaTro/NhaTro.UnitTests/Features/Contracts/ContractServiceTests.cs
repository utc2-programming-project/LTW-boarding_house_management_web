using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Services;

namespace NhaTro.UnitTests.Features.Contracts;

public sealed class ContractServiceTests
{
    [Theory]
    [InlineData(2026, 10, 22, 2026, 10, 22)]
    [InlineData(2026, 10, 23, 2026, 10, 22)]
    public void CreateContractAsync_RejectsStartDateThatIsNotBeforeEndDate(
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        var service = CreateService();
        var request = CreateRequest(
            new DateTime(startYear, startMonth, startDay),
            new DateTime(endYear, endMonth, endDay),
            [Guid.NewGuid()]);

        var exception = Assert.Throws<ArgumentException>(() => service.CreateContractAsync(request));

        Assert.Equal("StartDate", exception.ParamName);
    }

    [Fact]
    public void CreateContractAsync_RejectsNullTenantIds()
    {
        var service = CreateService();
        var request = CreateRequest(new DateTime(2026, 10, 22), new DateTime(2026, 11, 22), null!);

        var exception = Assert.Throws<ArgumentException>(() => service.CreateContractAsync(request));

        Assert.Equal("TenantIds", exception.ParamName);
    }

    [Fact]
    public void CreateContractAsync_RejectsEmptyTenantIds()
    {
        var service = CreateService();
        var request = CreateRequest(new DateTime(2026, 10, 22), new DateTime(2026, 11, 22), []);

        var exception = Assert.Throws<ArgumentException>(() => service.CreateContractAsync(request));

        Assert.Equal("TenantIds", exception.ParamName);
    }

    private static ContractService CreateService() => new(null!, null!);

    private static CreateContractRequest CreateRequest(
        DateTime startDate,
        DateTime endDate,
        List<Guid> tenantIds) => new(Guid.NewGuid(), startDate, endDate, tenantIds);
}
