using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Assistant.DTOs;
using Web.Features.Assistant.Entities;
using Web.Features.Assistant.Enums;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Services;
using Web.Features.Invoices.Services;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Enums;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace Web.Features.Assistant.Services;

/// <summary>
/// U2 — gửi tin nhắc thanh toán tới khách thuê của các hợp đồng trong phạm vi đề xuất.
/// Nội dung do Chủ trọ duyệt (có thể sửa) nằm trong OutputJson.
/// </summary>
public sealed class ReminderAiSuggestionApplier(
    IInvoiceQueryService invoices,
    INotificationService notifications,
    ILogger<ReminderAiSuggestionApplier> logger) : IAiSuggestionApplier
{
    private const int MaxRecipients = 200;

    public AiUseCase UseCase => AiUseCase.ReminderMessage;

    public async Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default)
    {
        AiNotificationOutput? message;
        try
        {
            message = JsonSerializer.Deserialize<AiNotificationOutput>(
                outputJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Title)
            || string.IsNullOrWhiteSpace(message.Body))
        {
            return AiApplyResult.Failure(
                AiErrorCodes.InvalidOutput, "Nội dung nhắc thanh toán không hợp lệ.");
        }

        var contractIds = ReadContractIds(suggestion.InputRefsJson);
        if (contractIds.Count == 0)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.InvalidOutput, "Đề xuất không còn hợp đồng nào để gửi nhắc.");
        }

        var recipients = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contractId in contractIds)
        {
            var userIds = await invoices.GetContractTenantUserIdsAsync(contractId, ct);
            foreach (var userId in userIds)
            {
                recipients.Add(userId);
                if (recipients.Count >= MaxRecipients)
                {
                    break;
                }
            }

            if (recipients.Count >= MaxRecipients)
            {
                break;
            }
        }

        if (recipients.Count == 0)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.ApplyFailed, "Các hợp đồng trong đề xuất không còn khách thuê đang hoạt động.");
        }

        var sent = 0;
        foreach (var userId in recipients)
        {
            try
            {
                await notifications.NotifyAsync(
                    userId, message.Title.Trim(), message.Body.Trim(), "/my/invoices");
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được nhắc thanh toán cho {UserId}", userId);
            }
        }

        if (sent == 0)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.ApplyFailed, "Không gửi được thông báo nào. Vui lòng thử lại.");
        }

        return AiApplyResult.Success(
            $"contracts:{string.Join(",", contractIds)};sent:{sent}");
    }

    private static IReadOnlyList<Guid> ReadContractIds(string inputRefsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputRefsJson);
            if (!document.RootElement.TryGetProperty("contractIds", out var array)
                || array.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var ids = new List<Guid>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && Guid.TryParse(item.GetString(), out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// U3 — tạo nhà và các phòng từ bản nháp đã duyệt, qua đúng <see cref="IPropertyService"/>
/// và <see cref="IRoomService"/> nên mọi kiểm tra nghiệp vụ vẫn chạy.
/// </summary>
public sealed class RoomDraftAiSuggestionApplier(
    ApplicationDbContext db,
    IPropertyService properties,
    IRoomService rooms) : IAiSuggestionApplier
{
    public AiUseCase UseCase => AiUseCase.RoomDraft;

    public async Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default)
    {
        AiRoomDraftOutput? draft;
        try
        {
            draft = JsonSerializer.Deserialize<AiRoomDraftOutput>(
                outputJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            draft = null;
        }

        if (draft is null || draft.Rooms.Count == 0)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.InvalidOutput, "Bản nháp không có phòng nào để tạo.");
        }

        int propertyId;
        if (suggestion.PropertyId is { } existingPropertyId)
        {
            var existing = await properties.GetAsync(existingPropertyId, approver, ct);
            if (!existing.Succeeded || existing.Property is null)
            {
                return AiApplyResult.Failure(
                    AiErrorCodes.ApplyFailed,
                    existing.ErrorMessage ?? "Không tìm thấy nhà trọ trong phạm vi của bạn.");
            }

            propertyId = existingPropertyId;
        }
        else
        {
            if (draft.Property is null
                || string.IsNullOrWhiteSpace(draft.Property.Name)
                || string.IsNullOrWhiteSpace(draft.Property.Address))
            {
                return AiApplyResult.Failure(
                    AiErrorCodes.InvalidOutput, "Bản nháp thiếu thông tin nhà trọ để tạo.");
            }

            var created = await properties.CreateAsync(new PropertySave
            {
                Name = draft.Property.Name.Trim(),
                Address = draft.Property.Address.Trim(),
                IsActive = true,
            }, approver, ct);

            if (!created.Succeeded || created.Property is null)
            {
                return AiApplyResult.Failure(
                    AiErrorCodes.ApplyFailed,
                    created.ErrorMessage ?? "Không tạo được nhà trọ từ bản nháp.");
            }

            propertyId = created.Property.Id;
        }

        var existingCodes = await db.Rooms
            .AsNoTracking()
            .Where(room => room.PropertyId == propertyId && !room.IsDeleted)
            .Select(room => room.Code)
            .ToListAsync(ct);
        var codeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var createdRooms = 0;
        foreach (var room in draft.Rooms)
        {
            var code = room.Code.Trim();
            if (!codeSet.Add(code))
            {
                return AiApplyResult.Failure(
                    AiErrorCodes.ApplyFailed,
                    $"Mã phòng '{code}' đã tồn tại trong nhà. Hãy sửa bản nháp rồi duyệt lại.");
            }

            var result = await rooms.CreateAsync(new RoomSave
            {
                PropertyId = propertyId,
                Code = code,
                Floor = room.Floor,
                AreaM2 = room.AreaM2,
                StandardPrice = room.StandardPrice,
                Status = RoomStatus.Available,
                IsActive = true,
            }, approver, ct);

            if (!result.Succeeded)
            {
                return AiApplyResult.Failure(
                    AiErrorCodes.ApplyFailed,
                    $"Đã tạo {createdRooms}/{draft.Rooms.Count} phòng. Phòng '{code}': "
                        + (result.ErrorMessage ?? "không tạo được."));
            }

            createdRooms++;
        }

        return AiApplyResult.Success($"property:{propertyId};rooms:{createdRooms}");
    }
}

/// <summary>
/// U6 — tạo hồ sơ khách thuê từ bản nháp đã duyệt, qua đúng <see cref="ITenantService"/>
/// nên vẫn kiểm trùng số điện thoại, chuẩn hoá dữ liệu và gắn hồ sơ với Chủ trọ.
/// AI không điền CCCD/số tài khoản; người duyệt bổ sung sau nếu cần.
/// </summary>
public sealed class TenantDraftAiSuggestionApplier(ITenantService tenants) : IAiSuggestionApplier
{
    public AiUseCase UseCase => AiUseCase.TenantDraft;

    public async Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default)
    {
        AiTenantDraftOutput? draft;
        try
        {
            draft = JsonSerializer.Deserialize<AiTenantDraftOutput>(
                outputJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            draft = null;
        }

        if (draft is null
            || string.IsNullOrWhiteSpace(draft.FullName)
            || string.IsNullOrWhiteSpace(draft.PhoneNumber))
        {
            return AiApplyResult.Failure(
                AiErrorCodes.InvalidOutput, "Bản nháp thiếu họ tên hoặc số điện thoại khách thuê.");
        }

        var request = new CreateTenantRequest
        {
            FullName = draft.FullName,
            PhoneNumber = draft.PhoneNumber,
            Email = draft.Email,
            DateOfBirth = ParseDate(draft.DateOfBirth),
            Gender = ParseGender(draft.Gender),
            PermanentAddress = draft.PermanentAddress,
            Note = draft.Note,
        };
        request.Normalize();

        var caller = new TenantCaller(approver.UserId, approver.Role);
        var result = await tenants.CreateAsync(request, caller, ct);
        if (!result.Succeeded || result.Tenant is null)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.ApplyFailed,
                result.ErrorMessage ?? "Không tạo được hồ sơ khách thuê từ bản nháp.");
        }

        return AiApplyResult.Success($"tenant:{result.Tenant.Id}");
    }

    private static DateOnly? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var date)
            ? date
            : null;

    private static Gender? ParseGender(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Enum.TryParse<Gender>(value, ignoreCase: true, out var gender)
            ? gender
            : null;
}

/// <summary>
/// U7 — tạo danh mục dịch vụ và phiên bản giá đầu tiên từ bản nháp đã duyệt,
/// qua <see cref="IServiceCatalogService"/> nên vẫn kiểm phạm vi nhà và dữ liệu giá.
/// </summary>
public sealed class ServiceDraftAiSuggestionApplier(IServiceCatalogService services) : IAiSuggestionApplier
{
    public AiUseCase UseCase => AiUseCase.ServiceDraft;

    public async Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default)
    {
        if (suggestion.PropertyId is not { } propertyId)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.ApplyFailed, "Bản nháp dịch vụ chưa gắn nhà trọ.");
        }

        AiServiceDraftOutput? draft;
        try
        {
            draft = JsonSerializer.Deserialize<AiServiceDraftOutput>(
                outputJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            draft = null;
        }

        if (draft is null
            || string.IsNullOrWhiteSpace(draft.Name)
            || string.IsNullOrWhiteSpace(draft.Unit)
            || !TryParseDate(draft.EffectiveDate, out var effectiveDate))
        {
            return AiApplyResult.Failure(
                AiErrorCodes.InvalidOutput, "Bản nháp dịch vụ thiếu tên, đơn vị hoặc ngày hiệu lực.");
        }

        DateOnly? effectiveTo = null;
        if (!string.IsNullOrWhiteSpace(draft.EffectiveTo))
        {
            if (!TryParseDate(draft.EffectiveTo, out var toDate))
            {
                return AiApplyResult.Failure(
                    AiErrorCodes.InvalidOutput, "Ngày kết thúc giá trong bản nháp không hợp lệ.");
            }

            effectiveTo = toDate;
        }

        try
        {
            var created = await services.CreateOrUpdateServicePriceAsync(new CreateServiceCatalogDto
            {
                PropertyId = propertyId,
                Name = draft.Name.Trim(),
                Unit = draft.Unit.Trim(),
                CalculationMethod = draft.CalculationMethod,
                UnitPrice = draft.UnitPrice,
                EffectiveDate = effectiveDate,
                EffectiveTo = effectiveTo,
                Note = draft.Note,
                Description = draft.Description,
            }, approver);

            return AiApplyResult.Success($"service:{created.Id}");
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or InvalidOperationException)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.ApplyFailed,
                ex.Message + " Dữ liệu chưa được ghi, vui lòng kiểm tra lại bản nháp.");
        }
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out date);
}

/// <summary>
/// U8 — lập hợp đồng thuê từ bản nháp đã duyệt, qua <see cref="IContractService"/>.
/// Luôn tạo ở trạng thái nháp (<c>AsDraft</c>) để đi tiếp luồng gửi/duyệt hợp đồng hiện có.
/// </summary>
public sealed class ContractDraftAiSuggestionApplier(IContractService contracts) : IAiSuggestionApplier
{
    public AiUseCase UseCase => AiUseCase.ContractDraft;

    public async Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default)
    {
        AiContractDraftOutput? draft;
        try
        {
            draft = JsonSerializer.Deserialize<AiContractDraftOutput>(
                outputJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            draft = null;
        }

        if (draft is null || draft.RoomId is not { } roomId || roomId <= 0
            || draft.TenantIds.Count == 0
            || !TryParseDate(draft.StartDate, out var start)
            || !TryParseDate(draft.EndDate, out var end))
        {
            return AiApplyResult.Failure(
                AiErrorCodes.InvalidOutput,
                "Bản nháp hợp đồng thiếu phòng, khách thuê hoặc thời hạn hợp lệ.");
        }

        try
        {
            var created = await contracts.CreateContractAsync(new CreateContractRequest(
                roomId,
                start.ToDateTime(TimeOnly.MinValue),
                end.ToDateTime(TimeOnly.MinValue),
                draft.TenantIds.ToList(),
                draft.DepositAmount,
                draft.MonthlyRent,
                draft.PaymentCycleMonths,
                draft.MeterReadingDay,
                draft.Terms,
                RepresentativeTenantId: draft.TenantIds[0],
                AsDraft: true), approver, ct);

            return AiApplyResult.Success($"contract:{created.ContractId}");
        }
        catch (Exception ex) when (ex is ContractValidationException or ContractNotFoundException
            or UnauthorizedAccessException or InvalidOperationException)
        {
            return AiApplyResult.Failure(
                AiErrorCodes.ApplyFailed,
                ex.Message + " Hợp đồng chưa được tạo, vui lòng kiểm tra lại bản nháp.");
        }
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out date);
}
