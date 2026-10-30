using Web.Features.Accounts;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Services;
using Web.Features.Properties;

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

    [Theory]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.KhachThue)]
    public async Task ApproveContractAsync_RejectsRolesOtherThanChuTro(string role)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ContractAuthorizationException>(() => service.ApproveContractAsync(
            Guid.NewGuid(),
            new ApproveContractRequestDto { IsApproved = true },
            new PropertyCaller("caller-id", role)));

        Assert.Equal(ContractErrorCodes.Forbidden, exception.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApproveContractAsync_RequiresReasonWhenRejecting(string? rejectReason)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ContractValidationException>(() => service.ApproveContractAsync(
            Guid.NewGuid(),
            new ApproveContractRequestDto { IsApproved = false, RejectReason = rejectReason },
            OwnerCaller));

        Assert.Equal("RejectReason", exception.ParamName);
        Assert.Equal(ContractErrorCodes.RejectReasonRequired, exception.ErrorCode);
    }

    [Fact]
    public async Task ApproveContractAsync_RejectsReasonLongerThan500Characters()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ContractValidationException>(() => service.ApproveContractAsync(
            Guid.NewGuid(),
            new ApproveContractRequestDto
            {
                IsApproved = false,
                RejectReason = new string('x', ApproveContractRequestDto.MaxRejectReasonLength + 1),
            },
            OwnerCaller));

        Assert.Equal(ContractErrorCodes.RejectReasonTooLong, exception.ErrorCode);
    }

    [Fact]
    public void ApproveContractAsync_AllowsReasonAt500CharactersForDatabaseValidation()
    {
        var dto = new ApproveContractRequestDto
        {
            IsApproved = false,
            RejectReason = new string('x', ApproveContractRequestDto.MaxRejectReasonLength),
        };

        var validation = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            dto,
            new System.ComponentModel.DataAnnotations.ValidationContext(dto),
            [],
            validateAllProperties: true);

        Assert.True(validation);
    }

    [Fact]
    public async Task ApproveContractAsync_RejectsNullRequest()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ApproveContractAsync(
            Guid.NewGuid(),
            null!,
            OwnerCaller));
    }

    private static PropertyCaller OwnerCaller => new("owner-id", Roles.ChuTro);

    private static ContractService CreateService() => new(null!);

    private static CreateContractRequest CreateRequest(
        DateTime startDate,
        DateTime endDate,
        List<int> tenantIds) => new(1, startDate, endDate, tenantIds);
}
