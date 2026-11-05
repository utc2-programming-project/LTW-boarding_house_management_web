using Web.Features.Contracts;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;

namespace NhaTro.UnitTests.Features.Contracts;

public sealed class ContractLifecycleOperationsTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    [Theory]
    [InlineData(ContractStatus.Approved, 2026, 10, 22, 2027, 10, 21, true)]
    [InlineData(ContractStatus.Approved, 2026, 11, 1, 2026, 11, 1, true)]
    [InlineData(ContractStatus.Approved, 2026, 11, 2, 2027, 10, 21, false)]
    [InlineData(ContractStatus.Approved, 2026, 10, 1, 2026, 10, 31, false)]
    [InlineData(ContractStatus.Pending, 2026, 10, 22, 2027, 10, 21, false)]
    [InlineData(ContractStatus.Rejected, 2026, 10, 22, 2027, 10, 21, false)]
    [InlineData(ContractStatus.Liquidated, 2026, 10, 22, 2027, 10, 21, false)]
    public void IsActionable_RequiresApprovedStatusWithinInclusiveTerm(
        ContractStatus status,
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay,
        bool expected)
    {
        Assert.Equal(expected, ContractLifecycleOperations.IsActionable(
            status,
            new DateTime(startYear, startMonth, startDay),
            new DateTime(endYear, endMonth, endDay),
            Today));
    }

    [Theory]
    [InlineData(2026, 10, 20, false)]
    [InlineData(2026, 10, 21, false)]
    [InlineData(2026, 10, 22, true)]
    public void EnsureRenewalEndDate_RequiresDateAfterCurrentEnd(
        int year, int month, int day, bool valid)
    {
        var currentEnd = new DateTime(2026, 10, 21);
        var newEnd = new DateTime(year, month, day);

        if (valid)
        {
            ContractLifecycleOperations.EnsureRenewalEndDate(currentEnd, newEnd);
            return;
        }

        var exception = Assert.Throws<ContractValidationException>(() =>
            ContractLifecycleOperations.EnsureRenewalEndDate(currentEnd, newEnd));

        Assert.Equal(ContractErrorCodes.InvalidRenewalEndDate, exception.ErrorCode);
        Assert.Equal("EndDate", exception.ParamName);
    }
}
