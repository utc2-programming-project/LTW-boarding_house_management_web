using Web.Features.AccessControl;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Rules;

namespace NhaTro.UnitTests.Features.AccessControl;

public sealed class GateRulesTests
{
    private static GatePolicyDto Policy() => GatePolicyDto.Defaults(1) with
    {
        AutoApproveDayVisit = true,
        AutoApproveOvernight = true,
        DayVisitStartHour = 6,
        DayVisitEndHour = 22,
    };

    [Fact]
    public void DayVisitCanAutoApproveWhenPolicyAllows()
    {
        Assert.True(GatePassRules.CanAutoApprove(Policy(), GateStayType.Visit));
    }

    [Fact]
    public void OvernightNeverAutoApprovesEvenIfPolicySaysSo()
    {
        Assert.False(GatePassRules.CanAutoApprove(Policy(), GateStayType.Overnight));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void OnlyOwnerOrAllowedManagerCanApprove(bool isOwner, bool isManager, bool allowManager, bool expected)
    {
        Assert.Equal(expected, GatePassRules.CanApprove(isOwner, isManager, allowManager));
    }

    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(0, 0, true)]
    public void TenantQuotaStopsAtConfiguredLimit(int issuedToday, int maxPerDay, bool exceeded)
    {
        Assert.Equal(exceeded, GatePassRules.TenantQuotaExceeded(issuedToday, maxPerDay));
    }

    [Fact]
    public void DayVisitWindowUsesVietnamTime()
    {
        var at = new DateTimeOffset(2026, 11, 24, 5, 59, 0, TimeSpan.FromHours(7));
        Assert.False(GatePassRules.IsInsideDayVisitWindow(Policy(), at));
        Assert.True(GatePassRules.IsInsideDayVisitWindow(Policy(), at.AddMinutes(1)));
        Assert.False(GatePassRules.IsInsideDayVisitWindow(Policy(), at.AddHours(17)));
    }

    [Theory]
    [InlineData(1, GatePassRules.MinTtlMinutes)]
    [InlineData(1441, GatePassRules.MaxTtlMinutes)]
    public void ComputeExpiryClampsTtl(int ttl, int expectedMinutes)
    {
        var now = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal(now.AddMinutes(expectedMinutes), GatePassRules.ComputeExpiry(now, ttl));
    }

    [Fact]
    public void OvernightCreateRequiresLawfulFields()
    {
        var request = new GatePassCreateRequest
        {
            RoomId = 1,
            VisitorName = "Nguyễn Văn A",
            VisitorPhone = "0901234567",
            StayType = GateStayType.Overnight,
        };

        Assert.Equal(GateErrorCodes.MissingLawfulFields, GatePassRules.ValidateCreate(request, isTenant: true));
    }

    [Fact]
    public void GuestPhoneIsRequired()
    {
        var request = new GatePassCreateRequest { VisitorName = "Nguyễn Văn A", RoomId = 1 };
        Assert.Equal(GateErrorCodes.MissingGuestPhone, GatePassRules.ValidateCreate(request, isTenant: true));
    }

    [Fact]
    public void TenantMustChooseRoom()
    {
        var request = new GatePassCreateRequest { VisitorName = "Nguyễn Văn A", VisitorPhone = "0901234567" };
        Assert.Equal(GateErrorCodes.RoomNotFound, GatePassRules.ValidateCreate(request, isTenant: true));
        Assert.Null(GatePassRules.ValidateCreate(request, isTenant: false));
    }

    [Theory]
    [InlineData("", GateErrorCodes.MissingGuestName)]
    [InlineData("   ", GateErrorCodes.MissingGuestName)]
    public void GuestNameIsRequired(string name, string expected)
    {
        var request = new GatePassCreateRequest { VisitorName = name, RoomId = 1 };
        Assert.Equal(expected, GatePassRules.ValidateCreate(request, isTenant: true));
    }

    [Fact]
    public void StayReportIsDueAt23SameDayForDaytimeArrival()
    {
        var arrival = new DateTimeOffset(2026, 11, 24, 20, 30, 0, TimeSpan.FromHours(7));
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 0, 0, TimeSpan.FromHours(7)),
            StayReportRules.DueAt(arrival));
    }

    [Theory]
    [InlineData(23, 0)]
    [InlineData(23, 45)]
    public void StayReportIsDueAt08NextDayForLateArrival(int hour, int minute)
    {
        var arrival = new DateTimeOffset(2026, 11, 24, hour, minute, 0, TimeSpan.FromHours(7));
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.FromHours(7)),
            StayReportRules.DueAt(arrival));
    }

    [Fact]
    public void StayReportIsDueAt23SameDayForEarlyMorningArrival()
    {
        var arrival = new DateTimeOffset(2026, 11, 24, 2, 0, 0, TimeSpan.FromHours(7));
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 0, 0, TimeSpan.FromHours(7)),
            StayReportRules.DueAt(arrival));
    }
}
