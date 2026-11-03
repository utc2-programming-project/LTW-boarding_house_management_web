using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;

namespace NhaTro.UnitTests.Features.Contracts;

public sealed class ContractStatusPresentationTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    [Theory]
    [InlineData(ContractStatus.Pending, ContractDisplayStatus.Pending)]
    [InlineData(ContractStatus.Rejected, ContractDisplayStatus.Rejected)]
    public void GetDisplayStatus_PreservesStoredTerminalOrPendingStatus(
        ContractStatus status,
        ContractDisplayStatus expected)
    {
        Assert.Equal(expected, ContractStatusPresentation.GetDisplayStatus(
            status, new DateTime(2026, 10, 1), new DateTime(2026, 11, 10), Today));
    }

    [Fact]
    public void GetDisplayStatus_ApprovedContractStartingLaterIsUpcoming()
    {
        Assert.Equal(ContractDisplayStatus.Upcoming, ContractStatusPresentation.GetDisplayStatus(
            ContractStatus.Approved, new DateTime(2026, 11, 2), new DateTime(2027, 11, 1), Today));
    }

    [Theory]
    [InlineData(0, ContractDisplayStatus.ExpiringSoon)]
    [InlineData(30, ContractDisplayStatus.ExpiringSoon)]
    [InlineData(31, ContractDisplayStatus.Active)]
    [InlineData(-1, ContractDisplayStatus.Expired)]
    public void GetDisplayStatus_UsesInclusiveThirtyDayWindow(int endOffset, ContractDisplayStatus expected)
    {
        var endDate = Today.AddDays(endOffset).ToDateTime(TimeOnly.MinValue);
        Assert.Equal(expected, ContractStatusPresentation.GetDisplayStatus(
            ContractStatus.Approved, Today.ToDateTime(TimeOnly.MinValue), endDate, Today));
    }
}
