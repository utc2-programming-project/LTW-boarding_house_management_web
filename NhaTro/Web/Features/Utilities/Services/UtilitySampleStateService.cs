using Web.Common.Utils;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.MockData;
using Web.Features.Utilities.Rules;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Quản lý dữ liệu mẫu P5 (Đợt 2A) trong bộ nhớ phiên.
/// Cho phép thêm dịch vụ mới, cập nhật mốc giá, bật/tắt kích hoạt và nhập chỉ số xem trước.
/// </summary>
public sealed class UtilitySampleStateService
{
    private readonly List<ServiceItemDto> _services;
    private readonly List<PriceVersionDto> _priceVersions;
    private readonly List<RoomReadingSampleDto> _roomReadings;

    public event Action? OnChange;

    public UtilitySampleStateService()
    {
        _services = UtilitySampleData.CreateDefaultServices();
        _priceVersions = UtilitySampleData.CreateDefaultPriceVersions();
        _roomReadings = UtilitySampleData.CreateDefaultRoomReadings();
    }

    public IReadOnlyList<BuildingOptionDto> Buildings => UtilitySampleData.Buildings;

    public IReadOnlyList<ServiceItemDto> GetServices(
        string? buildingId = null,
        string? type = null,
        string? status = null,
        string? searchTerm = null)
    {
        var query = _services.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(s => s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                     s.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(buildingId) && buildingId != "all")
        {
            query = query.Where(s => s.BuildingId == buildingId || s.BuildingId == "all");
        }

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
        {
            query = type switch
            {
                "meter" => query.Where(s => s.Type == ServiceTypeCategory.Meter),
                "fixed_monthly" => query.Where(s => s.Type == ServiceTypeCategory.FixedMonthly),
                "custom" => query.Where(s => s.Type == ServiceTypeCategory.Custom),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            query = status switch
            {
                "active" => query.Where(s => s.Active),
                "inactive" => query.Where(s => !s.Active),
                _ => query
            };
        }

        return query.Select(s => s.Copy()).ToList();
    }

    public ServiceItemDto? GetServiceById(string id) =>
        _services.FirstOrDefault(s => s.Id == id)?.Copy();

    public (bool Success, string? Error, ServiceItemDto? Item) AddService(CreateServiceRequest request)
    {
        var issues = UtilityDraftRules.ValidateCreateService(request);
        if (issues.Count > 0)
        {
            return (false, issues[0].Message, null);
        }

        var buildingObj = Buildings.FirstOrDefault(b => b.Id == request.BuildingId);
        var buildingName = buildingObj?.Name ?? "Tất cả nhà trọ";

        var newId = $"srv-{_services.Count + 1}";
        var newService = new ServiceItemDto
        {
            Id = newId,
            Name = request.Name.Trim(),
            Code = $"SRV{_services.Count + 1:D2}",
            Type = request.Type,
            TypeLabel = request.Type switch
            {
                ServiceTypeCategory.Meter => "Theo chỉ số",
                ServiceTypeCategory.FixedMonthly => "Cố định hàng tháng",
                _ => "Tùy chọn"
            },
            Unit = request.Unit,
            RateType = ServiceRateType.Flat,
            CurrentRate = request.Rate,
            RateDisplay = MoneyFormatter.Format(request.Rate),
            EffectiveFrom = new DateOnly(2026, 11, 1),
            EffectiveTo = null,
            Active = true,
            IsReadOnly = false,
            BuildingId = request.BuildingId,
            BuildingName = buildingName,
            Description = request.Description ?? "Dịch vụ bổ sung",
            HistoryCount = 1,
        };

        _services.Add(newService);

        _priceVersions.Insert(0, new PriceVersionDto
        {
            Id = $"pv-{newId}-1",
            ServiceId = newId,
            ServiceName = newService.Name,
            Unit = newService.Unit,
            Rate = newService.CurrentRate,
            RateType = ServiceRateType.Flat,
            EffectiveFrom = newService.EffectiveFrom,
            EffectiveTo = null,
            Status = "active",
            BuildingScope = newService.BuildingId,
            BuildingName = buildingName,
            AutoActivate = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Chủ trọ An",
            Note = "Khởi tạo dịch vụ mới",
        });

        NotifyStateChanged();
        return (true, null, newService.Copy());
    }

    public (bool Success, string? Error, PriceVersionDto? Version) UpdateServicePrice(
        UpdatePriceRequest request,
        string modifierName = "Chủ trọ An")
    {
        var issues = UtilityDraftRules.ValidateUpdatePrice(request);
        if (issues.Count > 0)
        {
            return (false, issues[0].Message, null);
        }

        var service = _services.FirstOrDefault(s => s.Id == request.ServiceId);
        if (service is null)
        {
            return (false, "Dịch vụ không tồn tại / Service not found.", null);
        }

        if (service.IsReadOnly)
        {
            return (false, "Biểu giá điện/nước là chỉ đọc, không thể thay đổi đơn giá (D7) / Electricity and water tariffs are read-only (D7).", null);
        }

        var buildingObj = Buildings.FirstOrDefault(b => b.Id == request.BuildingScope);
        var buildingName = buildingObj?.Name ?? "Tất cả nhà trọ";

        // Expire older active versions of this service
        foreach (var pv in _priceVersions.Where(v => v.ServiceId == request.ServiceId && v.Status == "active"))
        {
            pv.Status = "expired";
            pv.EffectiveTo ??= request.EffectiveFrom.AddDays(-1);
        }

        var newVersion = new PriceVersionDto
        {
            Id = $"pv-{request.ServiceId}-{_priceVersions.Count + 1}",
            ServiceId = request.ServiceId,
            ServiceName = service.Name,
            Unit = request.Unit,
            Rate = request.NewRate,
            RateType = service.RateType,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = request.AutoActivate ? "active" : "upcoming",
            BuildingScope = request.BuildingScope,
            BuildingName = buildingName,
            AutoActivate = request.AutoActivate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = modifierName,
            Note = request.Note ?? $"Điều chỉnh đơn giá mới có hiệu lực từ {DateFormatter.Format(request.EffectiveFrom)}",
        };

        _priceVersions.Insert(0, newVersion);

        // If auto activate, update current service
        if (request.AutoActivate)
        {
            service.CurrentRate = request.NewRate;
            service.RateDisplay = MoneyFormatter.Format(request.NewRate);
            service.Unit = request.Unit;
            service.EffectiveFrom = request.EffectiveFrom;
            service.EffectiveTo = request.EffectiveTo;
        }

        service.HistoryCount = _priceVersions.Count(v => v.ServiceId == request.ServiceId);

        NotifyStateChanged();
        return (true, null, newVersion);
    }

    public bool ToggleServiceActive(string id)
    {
        var service = _services.FirstOrDefault(s => s.Id == id);
        if (service is null || service.IsReadOnly) return false;

        service.Active = !service.Active;
        NotifyStateChanged();
        return true;
    }

    public IReadOnlyList<PriceVersionDto> GetPriceVersions(string? serviceId = null)
    {
        var query = _priceVersions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(serviceId) && serviceId != "all")
        {
            query = query.Where(v => v.ServiceId == serviceId);
        }
        return query.OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.CreatedAt).ToList();
    }

    public IReadOnlyList<RoomReadingSampleDto> GetRoomReadings() =>
        _roomReadings.Select(r => r.Copy()).ToList();

    public RoomReadingSampleDto? GetRoomReadingById(string id) =>
        _roomReadings.FirstOrDefault(r => r.Id == id)?.Copy();

    public (bool Success, string? Error, RoomReadingSampleDto? Room) SaveRoomReading(
        string roomId,
        decimal electricEnd,
        decimal waterEnd,
        string? reason,
        string modifierName = "Chủ trọ An",
        string role = "Chủ trọ")
    {
        var room = _roomReadings.FirstOrDefault(r => r.Id == roomId);
        if (room is null)
        {
            return (false, "Không tìm thấy phòng / Room not found.", null);
        }

        var hasElectricChanged = electricEnd != room.ElectricEnd;
        var hasWaterChanged = waterEnd != room.WaterEnd;

        var electricIssues = UtilityDraftRules.ValidateMeterReading(
            room.ElectricStart,
            electricEnd,
            hasElectricChanged,
            reason);

        if (electricIssues.Count > 0)
        {
            return (false, electricIssues[0].Message, null);
        }

        var waterIssues = UtilityDraftRules.ValidateMeterReading(
            room.WaterStart,
            waterEnd,
            hasWaterChanged,
            reason);

        if (waterIssues.Count > 0)
        {
            return (false, waterIssues[0].Message, null);
        }

        // Add audit logs for changes
        if (hasElectricChanged)
        {
            room.AuditLogs.Insert(0, new AuditLogEntryDto
            {
                Modifier = modifierName,
                Role = role,
                Timestamp = DateTime.UtcNow,
                FieldChanged = "Chỉ số điện cuối",
                OldValue = room.ElectricEnd.ToString("N0"),
                NewValue = electricEnd.ToString("N0"),
                DetailDiff = $"{room.ElectricEnd:N0} → {electricEnd:N0} kWh",
                Reason = string.IsNullOrWhiteSpace(reason) ? "Cập nhật định kỳ" : reason.Trim(),
            });
            room.ElectricEnd = electricEnd;
        }

        if (hasWaterChanged)
        {
            room.AuditLogs.Insert(0, new AuditLogEntryDto
            {
                Modifier = modifierName,
                Role = role,
                Timestamp = DateTime.UtcNow,
                FieldChanged = "Chỉ số nước cuối",
                OldValue = room.WaterEnd.ToString("N0"),
                NewValue = waterEnd.ToString("N0"),
                DetailDiff = $"{room.WaterEnd:N0} → {waterEnd:N0} m³",
                Reason = string.IsNullOrWhiteSpace(reason) ? "Cập nhật định kỳ" : reason.Trim(),
            });
            room.WaterEnd = waterEnd;
        }

        NotifyStateChanged();
        return (true, null, room.Copy());
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

