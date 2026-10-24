using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilitySampleStateServiceTests
{
    [Fact]
    public void GetServices_InitialCount_HasDefaultFiveServices()
    {
        var service = new UtilitySampleStateService();
        var services = service.GetServices();

        Assert.Equal(5, services.Count);
        Assert.Contains(services, s => s.Code == "ELEC" && s.IsReadOnly);
        Assert.Contains(services, s => s.Code == "WATER" && s.IsReadOnly);
        Assert.Contains(services, s => s.Code == "NET" && !s.IsReadOnly);
        Assert.Contains(services, s => s.Code == "CLEAN" && !s.IsReadOnly);
        Assert.Contains(services, s => s.Code == "PARK" && !s.IsReadOnly);
    }

    [Fact]
    public void GetServices_FilterBySearchTerm_ReturnsMatchingServices()
    {
        var service = new UtilitySampleStateService();

        var internetServices = service.GetServices(searchTerm: "Internet");
        Assert.Single(internetServices);
        Assert.Equal("NET", internetServices[0].Code);
    }

    [Fact]
    public void GetServices_FilterByStatus_ReturnsActiveOrInactive()
    {
        var service = new UtilitySampleStateService();

        var activeServices = service.GetServices(status: "active");
        Assert.All(activeServices, s => Assert.True(s.Active));

        var inactiveServices = service.GetServices(status: "inactive");
        Assert.All(inactiveServices, s => Assert.False(s.Active));
    }

    [Fact]
    public void AddService_WithValidRequest_AddsNewServiceSuccessfully()
    {
        var service = new UtilitySampleStateService();
        var req = new CreateServiceRequest
        {
            Name = "Thang máy tiện ích",
            Rate = 50_000m,
            Unit = "Phòng",
            BuildingId = "b1",
            Description = "Phí bảo trì thang máy",
        };

        var (success, error, created) = service.AddService(req);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal("Thang máy tiện ích", created.Name);
        Assert.Equal(50_000m, created.CurrentRate);

        var allServices = service.GetServices();
        Assert.Equal(6, allServices.Count);
        Assert.Contains(allServices, s => s.Name == "Thang máy tiện ích");
    }

    [Fact]
    public void UpdateServicePrice_ForReadOnlyTariff_RejectsUpdatePerD7()
    {
        var service = new UtilitySampleStateService();
        var req = new UpdatePriceRequest
        {
            ServiceId = "srv-1", // ELEC
            NewRate = 3_500m,
            EffectiveFrom = new DateOnly(2026, 12, 1),
        };

        var (success, error, version) = service.UpdateServicePrice(req);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("D7", error);
        Assert.Null(version);
    }

    [Fact]
    public void UpdateServicePrice_ForCustomService_UpdatesPriceAndRecordsHistory()
    {
        var service = new UtilitySampleStateService();
        var req = new UpdatePriceRequest
        {
            ServiceId = "srv-3", // Internet
            NewRate = 120_000m,
            Unit = "Phòng",
            BuildingScope = "b1",
            EffectiveFrom = new DateOnly(2026, 12, 1),
            AutoActivate = true,
            Note = "Tăng gói cước lên 500Mbps",
        };

        var (success, error, version) = service.UpdateServicePrice(req, "Chủ trọ An");

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(version);
        Assert.Equal(120_000m, version.Rate);

        var updatedService = service.GetServiceById("srv-3");
        Assert.NotNull(updatedService);
        Assert.Equal(120_000m, updatedService.CurrentRate);
        Assert.Equal("120.000 ₫", updatedService.RateDisplay);

        var history = service.GetPriceVersions("srv-3");
        Assert.Contains(history, v => v.Rate == 120_000m);
    }

    [Fact]
    public void ToggleServiceActive_ForCustomService_TogglesSuccessfully()
    {
        var service = new UtilitySampleStateService();
        var srv = service.GetServiceById("srv-5"); // PARK is inactive by default
        Assert.NotNull(srv);
        Assert.False(srv.Active);

        var toggled = service.ToggleServiceActive("srv-5");
        Assert.True(toggled);

        srv = service.GetServiceById("srv-5");
        Assert.NotNull(srv);
        Assert.True(srv.Active);
    }

    [Fact]
    public void ToggleServiceActive_ForReadOnlyService_CannotBeToggled()
    {
        var service = new UtilitySampleStateService();
        var toggled = service.ToggleServiceActive("srv-1"); // ELEC
        Assert.False(toggled);
    }

    [Fact]
    public void SaveRoomReading_WithChangedValue_CreatesAuditLogRecord()
    {
        var service = new UtilitySampleStateService();
        var initialRoom = service.GetRoomReadingById("room-101");
        Assert.NotNull(initialRoom);
        var initialLogCount = initialRoom.AuditLogs.Count;

        var (success, error, updated) = service.SaveRoomReading(
            roomId: "room-101",
            electricEnd: 1_600m, // Changed from 1570
            waterEnd: 130m,
            reason: "Chốt lại thực tế đồng hồ",
            modifierName: "Chủ trọ An",
            role: "Chủ trọ");

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(updated);
        Assert.Equal(1_600m, updated.ElectricEnd);
        Assert.Equal(initialLogCount + 1, updated.AuditLogs.Count);
        Assert.Equal("Chốt lại thực tế đồng hồ", updated.AuditLogs[0].Reason);
    }
}

