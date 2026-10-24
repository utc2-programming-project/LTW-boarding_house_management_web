using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Rules;
using Web.Features.Utilities.Services;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilitySampleStateServiceTests
{
    private const string OwnerEmail = "owner@nhatro.local";
    private const string ManagerEmail = "quanly@nhatro.local";
    private const string TenantEmail = "khachthue@nhatro.local";

    [Fact]
    public void GetServicesForCaller_ReturnsCatalogAndSupportsFilters()
    {
        var state = CreateState();

        var services = state.GetServicesForCaller(Roles.ChuTro, OwnerEmail);
        Assert.Equal(5, services.Count);
        Assert.Contains(services, service => service.Code == "ELEC" && service.IsReadOnly);
        Assert.Contains(services, service => service.Code == "WATER" && service.IsReadOnly);
        Assert.Contains(services, service => service.Code == "NET" && !service.IsReadOnly);

        var internet = state.GetServicesForCaller(Roles.ChuTro, OwnerEmail, searchTerm: "Internet");
        Assert.Single(internet);
        Assert.Equal("NET", internet[0].Code);

        Assert.All(state.GetServicesForCaller(Roles.ChuTro, OwnerEmail, status: "active"), item => Assert.True(item.Active));
        Assert.All(state.GetServicesForCaller(Roles.ChuTro, OwnerEmail, status: "inactive"), item => Assert.False(item.Active));
    }

    [Fact]
    public void GetServicesForCaller_RestrictsManagerTenantAndAdminScopes()
    {
        var state = CreateState();

        Assert.Equal(5, state.GetServicesForCaller(Roles.QuanLy, ManagerEmail).Count);
        Assert.Equal(5, state.GetServicesForCaller(Roles.KhachThue, TenantEmail).Count);
        Assert.Empty(state.GetServicesForCaller(Roles.Admin, OwnerEmail));
        Assert.Empty(state.GetServicesForCaller(Roles.QuanLy, "unknown@nhatro.local"));
        Assert.Empty(state.GetServicesForCaller(Roles.QuanLy, ManagerEmail, buildingId: "b2"));
    }

    [Fact]
    public void AddService_IsOwnerOnlyAndRequiresKnownBuilding()
    {
        var state = CreateState();
        var request = ValidCreateRequest(buildingId: "b2");

        var (managerSuccess, managerError, _) = state.AddService(request, Roles.QuanLy, "Manager");
        var (adminSuccess, adminError, _) = state.AddService(request, Roles.Admin, "Admin");
        Assert.False(managerSuccess);
        Assert.Equal(UtilityErrorCode.AccessDenied, managerError);
        Assert.False(adminSuccess);
        Assert.Equal(UtilityErrorCode.AccessDenied, adminError);

        request.BuildingId = "not-a-building";
        var (invalidScopeSuccess, invalidScopeError, _) = state.AddService(request, Roles.ChuTro, "Owner");
        Assert.False(invalidScopeSuccess);
        Assert.Equal(UtilityErrorCode.InvalidBuildingScope, invalidScopeError);

        request.BuildingId = "b2";
        var (success, error, created) = state.AddService(request, Roles.ChuTro, "Owner");
        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal(PriceVersionStatus.Active, created.PriceStatus);
        Assert.Contains(state.GetServicesForCaller(Roles.ChuTro, OwnerEmail), item => item.Id == created.Id);
        Assert.DoesNotContain(state.GetServicesForCaller(Roles.QuanLy, ManagerEmail), item => item.Id == created.Id);

        var allBuildingsRequest = ValidCreateRequest("all");
        var (allSuccess, allError, allBuildingsService) = state.AddService(allBuildingsRequest, Roles.ChuTro, "Owner");
        Assert.True(allSuccess);
        Assert.Null(allError);
        Assert.NotNull(allBuildingsService);
        Assert.Contains(state.GetServicesForCaller(Roles.QuanLy, ManagerEmail), item => item.Id == allBuildingsService.Id);
    }

    [Theory]
    [InlineData("srv-1")]
    [InlineData("srv-2")]
    public void UpdateServicePrice_ForElectricityAndWater_IsLockedByD7(string serviceId)
    {
        var state = CreateState();
        var (success, error, version) = state.UpdateServicePrice(
            ValidPriceRequest(serviceId, new DateOnly(2026, 12, 1)),
            Roles.ChuTro,
            "Owner");

        Assert.False(success);
        Assert.Equal(UtilityErrorCode.ReadOnlyTariff, error);
        Assert.Null(version);
    }

    [Fact]
    public void UpdateServicePrice_OnlyOwnerCanChangeAnEditableTariff()
    {
        var state = CreateState();
        var request = ValidPriceRequest("srv-3", new DateOnly(2026, 12, 1));

        var (managerSuccess, managerError, _) = state.UpdateServicePrice(request, Roles.QuanLy, "Manager");
        var (tenantSuccess, tenantError, _) = state.UpdateServicePrice(request, Roles.KhachThue, "Tenant");

        Assert.False(managerSuccess);
        Assert.Equal(UtilityErrorCode.AccessDenied, managerError);
        Assert.False(tenantSuccess);
        Assert.Equal(UtilityErrorCode.AccessDenied, tenantError);
    }

    [Fact]
    public void UpdateServicePrice_RejectsInvalidScopeAndConflictingDate()
    {
        var state = CreateState();
        var invalidScope = ValidPriceRequest("srv-3", new DateOnly(2026, 12, 1));
        invalidScope.BuildingScope = "b2";
        var (_, scopeError, _) = state.UpdateServicePrice(invalidScope, Roles.ChuTro, "Owner");
        Assert.Equal(UtilityErrorCode.InvalidBuildingScope, scopeError);

        var first = ValidPriceRequest("srv-3", new DateOnly(2026, 12, 1));
        var (success, _, _) = state.UpdateServicePrice(first, Roles.ChuTro, "Owner");
        Assert.True(success);

        var duplicate = ValidPriceRequest("srv-3", new DateOnly(2026, 12, 1));
        var (duplicateSuccess, duplicateError, _) = state.UpdateServicePrice(duplicate, Roles.ChuTro, "Owner");
        Assert.False(duplicateSuccess);
        Assert.Equal(UtilityErrorCode.PriceVersionConflict, duplicateError);
    }

    [Fact]
    public void UpdateServicePrice_FuturePriceDoesNotReplaceCurrentPriceOrCloseOtherScope()
    {
        var state = CreateState();
        var original = state.GetServicesForCaller(Roles.ChuTro, OwnerEmail).Single(item => item.Id == "srv-3");
        var before = state.GetPriceVersionsForCaller(Roles.ChuTro, OwnerEmail, "srv-3");
        var globalVersionBefore = Assert.Single(before, version => version.BuildingScope == "all");

        var request = ValidPriceRequest("srv-3", new DateOnly(2026, 12, 1));
        request.NewRate = 120_000m;
        var (success, error, created) = state.UpdateServicePrice(request, Roles.ChuTro, "Owner");

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal(PriceVersionStatus.Upcoming, created.Status);
        Assert.Equal(original.CurrentRate, state.GetServicesForCaller(Roles.ChuTro, OwnerEmail).Single(item => item.Id == "srv-3").CurrentRate);

        var after = state.GetPriceVersionsForCaller(Roles.ChuTro, OwnerEmail, "srv-3");
        Assert.Equal(before.Count + 1, after.Count);
        Assert.Equal(globalVersionBefore.EffectiveTo,
            after.Single(version => version.Id == globalVersionBefore.Id).EffectiveTo);
        Assert.Equal(new DateOnly(2026, 11, 30),
            after.Single(version => version.Id == "pv-3-1").EffectiveTo);
        Assert.Contains(after, version => version.Id == created.Id && version.Status == PriceVersionStatus.Upcoming);
    }

    [Fact]
    public void ToggleServiceActive_IsOwnerOnlyAndKeepsUtilityTariffsReadOnly()
    {
        var state = CreateState();
        Assert.False(state.ToggleServiceActive("srv-5", Roles.QuanLy));
        Assert.False(state.ToggleServiceActive("srv-5", Roles.KhachThue));

        var initiallyInactive = state.GetServicesForCaller(Roles.ChuTro, OwnerEmail).Single(item => item.Id == "srv-5");
        Assert.False(initiallyInactive.Active);
        Assert.True(state.ToggleServiceActive("srv-5", Roles.ChuTro));
        Assert.True(state.GetServicesForCaller(Roles.ChuTro, OwnerEmail).Single(item => item.Id == "srv-5").Active);

        Assert.False(state.ToggleServiceActive("srv-1", Roles.ChuTro));
        Assert.False(state.ToggleServiceActive("srv-2", Roles.ChuTro));
    }

    [Fact]
    public void GetPriceVersionsForCaller_RestrictsHistoryToAllowedBuildings()
    {
        var state = CreateState();
        var request = ValidCreateRequest("b2");
        var (_, _, added) = state.AddService(request, Roles.ChuTro, "Owner");
        Assert.NotNull(added);

        Assert.Contains(state.GetPriceVersionsForCaller(Roles.ChuTro, OwnerEmail), version => version.ServiceId == added.Id);
        Assert.DoesNotContain(state.GetPriceVersionsForCaller(Roles.QuanLy, ManagerEmail), version => version.ServiceId == added.Id);
        Assert.DoesNotContain(state.GetPriceVersionsForCaller(Roles.KhachThue, TenantEmail), version => version.ServiceId == added.Id);
        Assert.Empty(state.GetPriceVersionsForCaller(Roles.Admin, OwnerEmail));
    }

    [Fact]
    public void GetRoomReadingsForCaller_RestrictsTenantToOwnRoomAndManagerToBuilding()
    {
        var state = CreateState();

        var ownerRooms = state.GetRoomReadingsForCaller(Roles.ChuTro, OwnerEmail);
        var managerRooms = state.GetRoomReadingsForCaller(Roles.QuanLy, ManagerEmail);
        var tenantRooms = state.GetRoomReadingsForCaller(Roles.KhachThue, TenantEmail);

        Assert.True(ownerRooms.Count > managerRooms.Count);
        Assert.All(managerRooms, room => Assert.Equal("b1", room.BuildingId));
        Assert.Contains(ownerRooms, room => room.Id == "room-301");
        Assert.DoesNotContain(managerRooms, room => room.Id == "room-301");
        Assert.Equal("room-101", Assert.Single(tenantRooms).Id);
        Assert.Empty(state.GetRoomReadingsForCaller(Roles.Admin, OwnerEmail));
    }

    [Fact]
    public void SaveRoomReading_ManagerCanSaveWithinBuildingAndCreatesAuditEntry()
    {
        var state = CreateState();
        var initial = state.GetRoomReadingsForCaller(Roles.QuanLy, ManagerEmail).Single(room => room.Id == "room-101");

        var (success, error, saved) = state.SaveRoomReading(
            roomId: "room-101",
            electricEnd: 1570m,
            waterEnd: 131m,
            periodStart: new DateOnly(2026, 10, 1),
            periodEnd: new DateOnly(2026, 10, 31),
            reason: "Meter correction",
            callerRole: Roles.QuanLy,
            callerEmail: ManagerEmail,
            modifierName: "Manager");

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(saved);
        Assert.Equal(initial.AuditLogs.Count + 1, saved.AuditLogs.Count);
        Assert.Equal(Roles.QuanLy, saved.AuditLogs[0].Role);
        Assert.Equal(131m, saved.WaterEnd);
    }

    [Fact]
    public void SaveRoomReading_TenantAdminAndOutOfScopeRoomAreRejected()
    {
        var state = CreateState();
        var start = new DateOnly(2026, 10, 1);
        var end = new DateOnly(2026, 10, 31);

        var (tenantSuccess, tenantError, _) = state.SaveRoomReading(
            "room-101", 1571m, 130m, start, end, "Reason", Roles.KhachThue, TenantEmail, "Tenant");
        Assert.False(tenantSuccess);
        Assert.Equal(UtilityErrorCode.AccessDenied, tenantError);

        var (adminSuccess, adminError, _) = state.SaveRoomReading(
            "room-101", 1571m, 130m, start, end, "Reason", Roles.Admin, OwnerEmail, "Admin");
        Assert.False(adminSuccess);
        Assert.Equal(UtilityErrorCode.AccessDenied, adminError);

        var (scopeSuccess, scopeError, _) = state.SaveRoomReading(
            "room-301", 836m, 47m, start, end, "Reason", Roles.QuanLy, ManagerEmail, "Manager");
        Assert.False(scopeSuccess);
        Assert.Equal(UtilityErrorCode.RoomNotFound, scopeError);
    }

    [Fact]
    public void SaveRoomReading_RejectsInvalidPeriodAndMissingCorrectionReason()
    {
        var state = CreateState();
        var (_, periodError, _) = state.SaveRoomReading(
            "room-101", 1570m, 130m,
            new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1),
            "Reason", Roles.ChuTro, OwnerEmail, "Owner");
        Assert.Equal(UtilityErrorCode.BillingPeriodEndMustBeOnOrAfterStart, periodError);

        var (_, reasonError, _) = state.SaveRoomReading(
            "room-101", 1571m, 130m,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
            " ", Roles.ChuTro, OwnerEmail, "Owner");
        Assert.Equal(UtilityErrorCode.AdjustmentReasonIsRequired, reasonError);
    }

    [Fact]
    public void Today_UsesInjectedVietnamLocalTime()
    {
        var state = new UtilitySampleStateService(new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 24, 17, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 10, 25), state.Today);
    }

    private static UtilitySampleStateService CreateState() => new(new FixedTimeProvider(
        new DateTimeOffset(2026, 10, 24, 17, 0, 0, TimeSpan.Zero)));

    private static CreateServiceRequest ValidCreateRequest(string buildingId) => new()
    {
        Name = "Laundry",
        Type = ServiceTypeCategory.FixedMonthly,
        Unit = ServiceUnitCategory.Room,
        Rate = 50_000m,
        BuildingId = buildingId,
        EffectiveFrom = new DateOnly(2026, 10, 25),
    };

    private static UpdatePriceRequest ValidPriceRequest(string serviceId, DateOnly effectiveFrom) => new()
    {
        ServiceId = serviceId,
        NewRate = 120_000m,
        Unit = ServiceUnitCategory.Room,
        BuildingScope = "b1",
        EffectiveFrom = effectiveFrom,
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
