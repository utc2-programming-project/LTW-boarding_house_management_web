namespace Web.Features.Vehicles;

/// <summary>Mã lỗi nghiệp vụ cho module Quản lý xe; đồng bộ với key trong VehicleResource (.resx).</summary>
public static class VehicleErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string PropertyNotFound = nameof(PropertyNotFound);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string NoActiveContract = nameof(NoActiveContract);
    public const string TenantNotInRoom = nameof(TenantNotInRoom);
    public const string VehicleNotFound = nameof(VehicleNotFound);
    public const string InvalidPlateNumber = nameof(InvalidPlateNumber);
    public const string DuplicateActivePlate = nameof(DuplicateActivePlate);
    public const string RoomVehicleLimitReached = nameof(RoomVehicleLimitReached);
    public const string InvalidFeeAmount = nameof(InvalidFeeAmount);
    public const string InvalidSettingValue = nameof(InvalidSettingValue);
    public const string MaxVehiclesBelowActiveCount = nameof(MaxVehiclesBelowActiveCount);
    public const string ParkingZonesDisabled = nameof(ParkingZonesDisabled);
    public const string ParkingZonesInUse = nameof(ParkingZonesInUse);
    public const string ZoneNotFound = nameof(ZoneNotFound);
    public const string DuplicateZoneName = nameof(DuplicateZoneName);
    public const string InvalidZoneCapacity = nameof(InvalidZoneCapacity);
    public const string ZoneFull = nameof(ZoneFull);
    public const string VehicleTypeNotAllowedInZone = nameof(VehicleTypeNotAllowedInZone);
    public const string ZoneHasActiveVehicles = nameof(ZoneHasActiveVehicles);
    public const string ZoneTypeExcludesActiveVehicles = nameof(ZoneTypeExcludesActiveVehicles);
    public const string ZoneHasOccupiedSlots = nameof(ZoneHasOccupiedSlots);
    public const string SlotNotFound = nameof(SlotNotFound);
    public const string DuplicateSlotCode = nameof(DuplicateSlotCode);
    public const string SlotNotAvailable = nameof(SlotNotAvailable);
    public const string SlotCurrentlyOccupied = nameof(SlotCurrentlyOccupied);
    public const string BlockingAlertDisabled = nameof(BlockingAlertDisabled);
    public const string BlockingAlertRateLimited = nameof(BlockingAlertRateLimited);
    public const string BlockingAlertAmbiguousPlate = nameof(BlockingAlertAmbiguousPlate);
    public const string GuestNoticeDisabled = nameof(GuestNoticeDisabled);
    public const string GuestNoticeLimitReached = nameof(GuestNoticeLimitReached);
    public const string GuestNoticeNotFound = nameof(GuestNoticeNotFound);
    public const string InvalidTimeRange = nameof(InvalidTimeRange);
    public const string AuditSessionNotFound = nameof(AuditSessionNotFound);
    public const string AuditSessionAlreadyCompleted = nameof(AuditSessionAlreadyCompleted);
    public const string AuditSessionAlreadyInProgress = nameof(AuditSessionAlreadyInProgress);
    public const string InvalidAuditResult = nameof(InvalidAuditResult);
    public const string ReasonRequired = nameof(ReasonRequired);
    public const string InvalidStatus = nameof(InvalidStatus);
    public const string PhotoInvalid = nameof(PhotoInvalid);
    public const string SaveFailed = nameof(SaveFailed);
}
