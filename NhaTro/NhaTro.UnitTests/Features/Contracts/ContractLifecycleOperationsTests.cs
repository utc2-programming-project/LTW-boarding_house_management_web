using Web.Features.Contracts;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Contracts.Entities;

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

    [Theory]
    [InlineData(ContractStatus.Approved, false, true)]
    [InlineData(ContractStatus.Pending, false, false)]
    [InlineData(ContractStatus.Liquidated, false, false)]
    [InlineData(ContractStatus.Approved, true, false)]
    public void EnsureDepositEditable_RequiresActiveContractWithoutSuccessor(ContractStatus status, bool hasSuccessor, bool allowed)
    {
        var contract = new Contract { Status = status, StartDate = new DateTime(2026, 10, 1), EndDate = new DateTime(2026, 12, 1) };
        if (allowed)
        {
            ContractLifecycleOperations.EnsureDepositEditable(contract, Today, hasSuccessor);
            return;
        }
        var error = Assert.Throws<ContractOperationException>(() => ContractLifecycleOperations.EnsureDepositEditable(contract, Today, hasSuccessor));
        Assert.Equal(ContractErrorCodes.ContractNotActive, error.ErrorCode);
    }

    [Fact]
    public void EnsureDepositEditable_RejectsExpiredApprovedContract()
    {
        var contract = new Contract { Status = ContractStatus.Approved, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 10, 31) };
        var error = Assert.Throws<ContractOperationException>(() => ContractLifecycleOperations.EnsureDepositEditable(contract, Today, false));
        Assert.Equal(ContractErrorCodes.ContractNotActive, error.ErrorCode);
    }
}
