using System.Globalization;
using System.Resources;
using System.Text.Json;
using Web.Common.Utils;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Rules;

namespace NhaTro.UnitTests.Features.Contracts;

public sealed class ContractDraftRulesTests
{
    [Fact]
    public void Validate_ReportsMissingPropertyRoomTenantDatesAndMoney()
    {
        var issues = ContractDraftRules.Validate(new RentalContractDraft());

        Assert.Contains(issues, issue => issue.Error == ContractDraftError.PropertyRequired);
        Assert.Contains(issues, issue => issue.Error == ContractDraftError.RoomRequired);
        Assert.Contains(issues, issue => issue.Error == ContractDraftError.RepresentativeRequired);
        Assert.Contains(issues, issue => issue.Error == ContractDraftError.StartDateRequired);
        Assert.Contains(issues, issue => issue.Error == ContractDraftError.EndDateRequired);
        Assert.Contains(issues, issue => issue.Error == ContractDraftError.RentRequired);
        Assert.Contains(issues, issue => issue.Error == ContractDraftError.DepositRequired);
    }

    [Fact]
    public void Validate_AcceptsEndDateExactlyOneMonthAfterStart()
    {
        var draft = ValidDraft();
        draft.StartDate = new DateOnly(2026, 10, 22);
        draft.EndDate = new DateOnly(2026, 11, 22);

        Assert.Empty(ContractDraftRules.Validate(draft));
    }

    [Theory]
    [InlineData(2026, 10, 22, 2026, 10, 22)]
    [InlineData(2026, 10, 22, 2026, 11, 21)]
    public void Validate_RejectsEndDateLessThanOneMonthAfterStart(
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        var draft = ValidDraft();
        draft.StartDate = new DateOnly(startYear, startMonth, startDay);
        draft.EndDate = new DateOnly(endYear, endMonth, endDay);

        Assert.Contains(ContractDraftRules.Validate(draft), issue =>
            issue.Error == ContractDraftError.EndDateMustBeAtLeastOneMonthAfterStart);
    }

    [Fact]
    public void Validate_RejectsRoomThatIsUnavailableOrBelongsToAnotherProperty()
    {
        var draft = ValidDraft();
        draft.RoomId = 103; // This room is not vacant by the selected lease start.
        Assert.Contains(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.RoomUnavailable);

        draft.RoomId = 201; // Vacant, but belongs to property 2 rather than property 1.
        Assert.Contains(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.RoomUnavailable);
    }

    [Fact]
    public void Validate_AllowsUpcomingRoomOnlyWhenLeaseStartsOnOrAfterAvailabilityDate()
    {
        var draft = ValidDraft();
        draft.RoomId = 103;
        draft.StartDate = new DateOnly(2026, 10, 31);
        Assert.Contains(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.RoomUnavailable);

        draft.StartDate = new DateOnly(2026, 11, 1);
        Assert.DoesNotContain(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.RoomUnavailable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsRentThatIsNotPositive(decimal rent)
    {
        var draft = ValidDraft();
        draft.MonthlyRentVnd = rent;

        Assert.Contains(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.RentMustBePositive);
    }

    [Fact]
    public void Validate_AllowsZeroDepositAndRejectsNegativeDeposit()
    {
        var draft = ValidDraft();
        draft.DepositVnd = 0;
        Assert.DoesNotContain(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.DepositCannotBeNegative);

        draft.DepositVnd = -1;
        Assert.Contains(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.DepositCannotBeNegative);
    }

    [Fact]
    public void Validate_RejectsUnsupportedPaymentCycle()
    {
        var draft = ValidDraft();
        draft.PaymentCycleMonths = 2;

        Assert.Contains(ContractDraftRules.Validate(draft), issue => issue.Error == ContractDraftError.PaymentCycleInvalid);
    }

    [Fact]
    public void CalculateInitialPayment_AddsDepositToRentForSelectedCycleInVnd()
    {
        var total = ContractDraftRules.CalculateInitialPayment(4_500_000m, 2_000_000m, 3);

        Assert.Equal(15_500_000m, total);
    }

    [Fact]
    public void ContractFormCultureSnapshot_DeserializesBrowserStateIncludingDateOnlyAndMembers()
    {
        const string savedState = """
            {"PropertyId":1,"RoomId":101,"RepresentativeTenantId":2,"StartDate":"2026-10-22","EndDate":"2027-04-22","MonthlyRentVnd":4500000,"DepositVnd":2000000,"PaymentCycleMonths":3,"MeterReadingDay":25,"Notes":"test note","Members":[{"FullName":"Test Tenant","PhoneNumber":"0900000000","Relationship":"Colleague"}]}
            """;

        var snapshot = JsonSerializer.Deserialize<ContractFormCultureSnapshot>(savedState,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.PropertyId);
        Assert.Equal(new DateOnly(2026, 10, 22), snapshot.StartDate);
        Assert.Equal(4_500_000m, snapshot.MonthlyRentVnd);
        Assert.Equal("Test Tenant", Assert.Single(snapshot.Members!).FullName);
    }

    [Fact]
    public void ContractResources_ResolveVietnameseAndEnglishWithoutChangingVndFormatting()
    {
        var resources = new ResourceManager(
            "Web.Resources.Features.Contracts.ContractResources",
            typeof(ContractResources).Assembly);

        Assert.Equal("Tạo hợp đồng thuê phòng mới",
            resources.GetString("ContractCreateHeading", CultureInfo.GetCultureInfo("vi-VN")));
        Assert.Equal("Create a new rental contract",
            resources.GetString("ContractCreateHeading", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("4.500.000 ₫", MoneyFormatter.Format(4_500_000m));
    }

    private static RentalContractDraft ValidDraft() => new()
    {
        PropertyId = 1,
        RoomId = 101,
        RepresentativeTenantId = 1,
        StartDate = new DateOnly(2026, 10, 22),
        EndDate = new DateOnly(2027, 4, 22),
        MonthlyRentVnd = 4_500_000m,
        DepositVnd = 0m,
        PaymentCycleMonths = 1,
    };
}
