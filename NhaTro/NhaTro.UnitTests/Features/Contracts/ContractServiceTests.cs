using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Services;

namespace NhaTro.UnitTests.Features.Contracts;

public sealed class ContractServiceTests
{
    [Theory]
    [InlineData(2026, 10, 22, 2026, 10, 22)]
    [InlineData(2026, 10, 23, 2026, 10, 22)]
    public async Task CreateContractAsync_RejectsStartDateThatIsNotBeforeEndDate(
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        var service = CreateService();
        var request = CreateRequest(
            new DateTime(startYear, startMonth, startDay),
            new DateTime(endYear, endMonth, endDay),
            [1]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContractAsync(request));

        Assert.Equal("StartDate", exception.ParamName);
    }

    [Fact]
    public async Task CreateContractAsync_RejectsNullTenantIds()
    {
        var service = CreateService();
        var request = CreateRequest(new DateTime(2026, 10, 22), new DateTime(2026, 11, 22), null!);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContractAsync(request));

        Assert.Equal("TenantIds", exception.ParamName);
    }

    [Fact]
    public async Task CreateContractAsync_RejectsEmptyTenantIds()
    {
        var service = CreateService();
        var request = CreateRequest(new DateTime(2026, 10, 22), new DateTime(2026, 11, 22), []);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContractAsync(request));

        Assert.Equal("TenantIds", exception.ParamName);
    }

    [Fact]
    public async Task CreateContractAsync_RejectsNullRequest()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateContractAsync(null!));
    }

    private static ContractService CreateService() => new(null!, null!);

    private static CreateContractRequest CreateRequest(
        DateTime startDate,
        DateTime endDate,
        List<int> tenantIds) => new(1, startDate, endDate, tenantIds);
}
