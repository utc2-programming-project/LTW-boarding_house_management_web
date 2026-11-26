using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Web.Data;
using Web.Features.Assistant.DTOs;
using Web.Features.Assistant.Entities;
using Web.Features.Assistant.Enums;
using Web.Features.Assistant.Rules;
using Web.Features.Audit.Entities;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.Assistant.Services;

/// <summary>
/// Vòng đời đề xuất AI: tạo nháp (chỉ ghi AiSuggestion, không đụng dữ liệu nghiệp vụ),
/// Chủ trọ duyệt/từ chối, applier của use case mới gọi service nghiệp vụ. Mọi lần gọi model
/// đều ghi AiUsageLog để tính hạn mức ngày; U1/U5 dùng lại kết quả trong ngày theo ngữ cảnh.
/// </summary>
public sealed class AiSuggestionService(
    ApplicationDbContext db,
    TimeProvider clock,
    IAiChatClient client,
    AiContextBuilder contextBuilder,
    AiSettingsService settings,
    IEnumerable<IAiSuggestionApplier> appliers,
    INotificationService notifications,
    IMemoryCache cache,
    ILogger<AiSuggestionService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxReasonLength = 500;
    private const int MaxListItems = 100;

    private readonly Dictionary<AiUseCase, IAiSuggestionApplier> _appliers =
        appliers.ToDictionary(applier => applier.UseCase);

    public Task<AiAccess> ResolveAccessAsync(
        PropertyCaller caller, int? propertyId, CancellationToken ct = default) =>
        settings.ResolveAccessAsync(caller, propertyId, ct);

    public async Task<AiSuggestionResult> GenerateAsync(
        AiGenerateRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        var built = await contextBuilder.BuildAsync(request, caller, ct);
        if (!built.Succeeded)
        {
            return AiSuggestionResult.Failure(
                built.ErrorCode ?? AiErrorCodes.InvalidRequest,
                built.ErrorMessage ?? "Không dựng được ngữ cảnh cho trợ lý.");
        }

        var access = await settings.ResolveAccessAsync(caller, built.PropertyId, ct);
        if (!access.Allowed)
        {
            return AiSuggestionResult.Failure(
                access.ErrorCode ?? AiErrorCodes.Forbidden,
                access.ErrorMessage ?? "Bạn không có quyền dùng trợ lý AI.");
        }

        if (!access.Enabled)
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.AiDisabled, "Chủ trọ đã tắt trợ lý AI cho tòa nhà này.");
        }

        var usedToday = await settings.CountTodayAsync(caller.UserId, ct);
        if (usedToday >= access.DailyLimit)
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.QuotaExceeded,
                $"Đã dùng {usedToday}/{access.DailyLimit} lượt AI hôm nay. Vui lòng thử lại sau.");
        }

        var cacheKey = CacheKey(request, built.ContextJson, caller.UserId);
        string? cachedJson = null;
        var fromCache = cacheKey is not null
            && cache.TryGetValue(cacheKey, out cachedJson)
            && !string.IsNullOrWhiteSpace(cachedJson);

        AiChatResult result;
        if (fromCache && !string.IsNullOrWhiteSpace(cachedJson))
        {
            result = AiChatResult.Success(cachedJson!, "cache", fromCache: true);
        }
        else
        {
            result = await client.CompleteAsync(new AiChatRequest(
                request.UseCase,
                AiOutputSchemas.SystemPrompt(request.UseCase),
                built.UserPrompt,
                built.ContextJson,
                request.Instruction), ct);
        }

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Json))
        {
            var failureCode = result.ErrorCode ?? AiErrorCodes.ProviderFailed;
            RecordUsage(caller.UserId, request.UseCase, result.Model, result,
                succeeded: false, failureCode);
            await db.SaveChangesAsync(ct);

            return AiSuggestionResult.Failure(
                failureCode,
                result.ErrorMessage ?? "Trợ lý AI tạm thời không phản hồi. Không có dữ liệu nào bị thay đổi.");
        }

        var validation = AiOutputValidator.Validate(request.UseCase, result.Json);
        if (!validation.Succeeded || validation.NormalizedJson is null)
        {
            RecordUsage(caller.UserId, request.UseCase, result.Model, result,
                succeeded: false, AiErrorCodes.InvalidOutput);
            await db.SaveChangesAsync(ct);

            return AiSuggestionResult.Failure(
                AiErrorCodes.InvalidOutput,
                validation.ErrorMessage ?? "Trợ lý AI trả về nội dung không hợp lệ. Vui lòng thử lại.");
        }

        var suggestion = new AiSuggestion
        {
            Id = Guid.NewGuid(),
            UseCase = request.UseCase,
            PropertyId = built.PropertyId,
            RequestedByUserId = caller.UserId,
            ScopeJson = built.ScopeJson,
            InputRefsJson = built.InputRefsJson,
            OutputJson = validation.NormalizedJson,
            Model = result.Model,
            Status = AiSuggestionStatus.Pending,
            CreatedAt = clock.GetUtcNow(),
        };

        db.AiSuggestions.Add(suggestion);
        AddAudit("AiSuggestionRequested", caller.UserId, built.PropertyId, new
        {
            useCase = request.UseCase.ToString(),
            model = result.Model,
            suggestionId = suggestion.Id,
        });

        RecordUsage(caller.UserId, request.UseCase, result.Model, result, succeeded: true, null);
        await db.SaveChangesAsync(ct);

        if (!fromCache && cacheKey is not null && result.Json is not null)
        {
            cache.Set(cacheKey, validation.NormalizedJson, CacheEntryOptions());
        }

        return AiSuggestionResult.Success(await ToDtoAsync(suggestion, ct));
    }

    public async Task<AiSuggestionListResult> ListMineAsync(
        PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager)
        {
            return AiSuggestionListResult.Failure(
                AiErrorCodes.Forbidden, "Vai trò hiện tại không dùng được trợ lý AI.");
        }

        var rows = await db.AiSuggestions
            .AsNoTracking()
            .Where(item => item.RequestedByUserId == caller.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(MaxListItems)
            .ToListAsync(ct);

        return AiSuggestionListResult.Success(await ToDtosAsync(rows, ct));
    }

    public async Task<AiSuggestionListResult> ListInboxAsync(
        PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return AiSuggestionListResult.Failure(
                AiErrorCodes.Forbidden, "Chỉ Chủ trọ duyệt đề xuất AI.");
        }

        var ownedPropertyIds = db.Properties
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        var scopedManagerIds = db.PropertyManagers
            .Where(assignment => assignment.RevokedAt == null
                && ownedPropertyIds.Contains(assignment.PropertyId))
            .Select(assignment => assignment.ManagerUserId);

        var rows = await db.AiSuggestions
            .AsNoTracking()
            .Where(item =>
                (item.PropertyId != null && ownedPropertyIds.Contains(item.PropertyId.Value))
                || (item.PropertyId == null
                    && (item.RequestedByUserId == caller.UserId
                        || scopedManagerIds.Contains(item.RequestedByUserId))))
            .OrderByDescending(item => item.CreatedAt)
            .Take(MaxListItems)
            .ToListAsync(ct);

        return AiSuggestionListResult.Success(await ToDtosAsync(rows, ct));
    }

    /// <summary>Chủ trọ duyệt; tùy chọn nhận nội dung đã sửa (U2/U3) và kiểm lại trước khi áp dụng.</summary>
    public async Task<AiSuggestionResult> ApproveAsync(
        Guid id,
        string? editedOutputJson,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return AiSuggestionResult.Failure(AiErrorCodes.Forbidden, "Chỉ Chủ trọ duyệt đề xuất AI.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var suggestion = await LockAsync(id, ct);
        if (suggestion is null)
        {
            return AiSuggestionResult.Failure(AiErrorCodes.NotFound, "Không tìm thấy đề xuất.");
        }

        if (!await IsInOwnerScopeAsync(suggestion, caller.UserId, ct))
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.Forbidden, "Đề xuất không thuộc tòa nhà bạn sở hữu.");
        }

        if (suggestion.Status != AiSuggestionStatus.Pending)
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.AlreadyDecided, "Đề xuất đã được quyết định trước đó.");
        }

        var outputJson = suggestion.OutputJson;
        if (!string.IsNullOrWhiteSpace(editedOutputJson))
        {
            if (!CanEditOutput(suggestion.UseCase))
            {
                return AiSuggestionResult.Failure(
                    AiErrorCodes.InvalidRequest, "Loại đề xuất này không cho sửa nội dung.");
            }

            var edited = AiOutputValidator.Validate(suggestion.UseCase, editedOutputJson);
            if (!edited.Succeeded || edited.NormalizedJson is null)
            {
                return AiSuggestionResult.Failure(
                    AiErrorCodes.InvalidOutput,
                    edited.ErrorMessage ?? "Nội dung sửa không hợp lệ.");
            }

            outputJson = edited.NormalizedJson;
            suggestion.OutputJson = outputJson;
        }
        else
        {
            var current = AiOutputValidator.Validate(suggestion.UseCase, outputJson);
            if (!current.Succeeded || current.NormalizedJson is null)
            {
                return AiSuggestionResult.Failure(
                    AiErrorCodes.InvalidOutput,
                    current.ErrorMessage ?? "Nội dung đề xuất không còn hợp lệ.");
            }

            outputJson = current.NormalizedJson;
        }

        if (!_appliers.TryGetValue(suggestion.UseCase, out var applier))
        {
            logger.LogError("Chưa đăng ký applier cho use case {UseCase}", suggestion.UseCase);
            return AiSuggestionResult.Failure(
                AiErrorCodes.ApplyFailed, "Loại đề xuất này chưa hỗ trợ áp dụng.");
        }

        suggestion.Status = AiSuggestionStatus.Approved;
        suggestion.DecidedByUserId = caller.UserId;
        suggestion.DecidedAt = clock.GetUtcNow();
        suggestion.DecisionReason = null;

        AiApplyResult applied;
        try
        {
            applied = await applier.ApplyAsync(suggestion, outputJson, caller, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Áp dụng đề xuất {SuggestionId} thất bại", suggestion.Id);
            db.ChangeTracker.Clear();
            return AiSuggestionResult.Failure(
                AiErrorCodes.ApplyFailed,
                "Không áp dụng được đề xuất. Dữ liệu chưa được ghi thêm, vui lòng kiểm tra lại.");
        }

        if (!applied.Succeeded)
        {
            db.ChangeTracker.Clear();
            return AiSuggestionResult.Failure(
                applied.ErrorCode ?? AiErrorCodes.ApplyFailed,
                applied.ErrorMessage ?? "Không áp dụng được đề xuất.");
        }

        suggestion.Status = AiSuggestionStatus.Applied;
        suggestion.AppliedRefId = applied.AppliedRefId;
        AddAudit("AiSuggestionApproved", caller.UserId, suggestion.PropertyId, new
        {
            useCase = suggestion.UseCase.ToString(),
            suggestionId = suggestion.Id,
            appliedRefId = applied.AppliedRefId,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return AiSuggestionResult.Failure(
                AiErrorCodes.Conflict, "Đề xuất đã bị thay đổi ở nơi khác. Vui lòng tải lại.");
        }

        await transaction.CommitAsync(ct);
        await NotifyRequesterAsync(
            suggestion, "Đề xuất AI đã được duyệt",
            $"Chủ trọ đã duyệt đề xuất {UseCaseName(suggestion.UseCase)} của bạn.", ct);

        return AiSuggestionResult.Success(await ToDtoAsync(suggestion, ct));
    }

    public async Task<AiSuggestionResult> RejectAsync(
        Guid id, string? reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return AiSuggestionResult.Failure(AiErrorCodes.Forbidden, "Chỉ Chủ trọ từ chối đề xuất AI.");
        }

        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.ReasonRequired, "Vui lòng nhập lý do từ chối.");
        }

        if (trimmed.Length > MaxReasonLength)
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.ReasonTooLong, $"Lý do tối đa {MaxReasonLength} ký tự.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var suggestion = await LockAsync(id, ct);
        if (suggestion is null)
        {
            return AiSuggestionResult.Failure(AiErrorCodes.NotFound, "Không tìm thấy đề xuất.");
        }

        if (!await IsInOwnerScopeAsync(suggestion, caller.UserId, ct))
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.Forbidden, "Đề xuất không thuộc tòa nhà bạn sở hữu.");
        }

        if (suggestion.Status != AiSuggestionStatus.Pending)
        {
            return AiSuggestionResult.Failure(
                AiErrorCodes.AlreadyDecided, "Đề xuất đã được quyết định trước đó.");
        }

        suggestion.Status = AiSuggestionStatus.Rejected;
        suggestion.DecidedByUserId = caller.UserId;
        suggestion.DecidedAt = clock.GetUtcNow();
        suggestion.DecisionReason = trimmed;
        AddAudit("AiSuggestionRejected", caller.UserId, suggestion.PropertyId, new
        {
            useCase = suggestion.UseCase.ToString(),
            suggestionId = suggestion.Id,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return AiSuggestionResult.Failure(
                AiErrorCodes.Conflict, "Đề xuất đã bị thay đổi ở nơi khác. Vui lòng tải lại.");
        }

        await transaction.CommitAsync(ct);
        await NotifyRequesterAsync(
            suggestion, "Đề xuất AI bị từ chối",
            $"Chủ trọ đã từ chối đề xuất {UseCaseName(suggestion.UseCase)}. Lý do: {trimmed}", ct);

        return AiSuggestionResult.Success(await ToDtoAsync(suggestion, ct));
    }

    private void RecordUsage(
        string userId,
        AiUseCase useCase,
        string model,
        AiChatResult result,
        bool succeeded,
        string? errorCode)
    {
        db.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = userId,
            UseCase = useCase,
            Model = model,
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            Cost = result.Cost,
            Succeeded = succeeded,
            ErrorCode = errorCode,
            CreatedAt = clock.GetUtcNow(),
        });
    }

    private Task<AiSuggestion?> LockAsync(Guid id, CancellationToken ct) =>
        db.AiSuggestions
            .FromSqlInterpolated(
                $"SELECT * FROM [AiSuggestions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync(ct);

    /// <summary>Chủ trọ chỉ quyết định đề xuất thuộc tòa mình, hoặc do Quản lý tòa mình gửi.</summary>
    private async Task<bool> IsInOwnerScopeAsync(
        AiSuggestion suggestion, string ownerId, CancellationToken ct)
    {
        if (suggestion.PropertyId is { } propertyId)
        {
            return await db.Properties.AnyAsync(
                property => property.Id == propertyId && property.OwnerId == ownerId, ct);
        }

        if (suggestion.RequestedByUserId == ownerId)
        {
            return true;
        }

        return await db.PropertyManagers.AnyAsync(
            assignment => assignment.ManagerUserId == suggestion.RequestedByUserId
                && assignment.RevokedAt == null
                && db.Properties.Any(
                    property => property.Id == assignment.PropertyId && property.OwnerId == ownerId),
            ct);
    }

    private async Task NotifyRequesterAsync(
        AiSuggestion suggestion, string title, string body, CancellationToken ct)
    {
        if (suggestion.RequestedByUserId == suggestion.DecidedByUserId)
        {
            return;
        }

        try
        {
            await notifications.NotifyAsync(
                suggestion.RequestedByUserId, title, body, "/assistant");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được thông báo quyết định AI {Id}", suggestion.Id);
        }
    }

    private void AddAudit(string action, string actorUserId, int? propertyId, object metadata)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Category = AuditLogCategory.Business,
            Action = action,
            ActorUserId = actorUserId,
            PropertyId = propertyId,
            Metadata = JsonSerializer.Serialize(metadata, JsonOptions),
            OccurredAt = clock.GetUtcNow(),
        });
    }

    private async Task<AiSuggestionDto> ToDtoAsync(AiSuggestion suggestion, CancellationToken ct) =>
        (await ToDtosAsync([suggestion], ct))[0];

    private async Task<IReadOnlyList<AiSuggestionDto>> ToDtosAsync(
        IReadOnlyList<AiSuggestion> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var requesterIds = rows
            .Select(row => row.RequestedByUserId)
            .Distinct()
            .ToList();

        var users = await db.Users
            .AsNoTracking()
            .Where(user => requesterIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName, user.UserName })
            .ToListAsync(ct);
        var names = users.ToDictionary(
            user => user.Id,
            user => string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName);

        var dtos = rows.Select(row => new AiSuggestionDto(
            row.Id,
            row.UseCase,
            row.PropertyId,
            row.RequestedByUserId,
            names.TryGetValue(row.RequestedByUserId, out var name) ? name ?? row.RequestedByUserId : row.RequestedByUserId,
            row.CreatedAt,
            row.Status,
            row.Model,
            row.OutputJson,
            ParseDraft(row.UseCase, row.OutputJson),
            row.DecisionReason,
            row.DecidedAt,
            row.AppliedRefId)).ToList();

        var contractTenantIds = dtos
            .Where(item => item.Draft.ContractDraft is not null)
            .SelectMany(item => item.Draft.ContractDraft!.TenantIds)
            .Distinct()
            .ToList();
        if (contractTenantIds.Count > 0)
        {
            var tenantRows = await db.Tenants
                .AsNoTracking()
                .Where(tenant => contractTenantIds.Contains(tenant.Id))
                .Select(tenant => new { tenant.Id, tenant.FullName })
                .ToListAsync(ct);
            var tenantNames = tenantRows.ToDictionary(tenant => tenant.Id, tenant => tenant.FullName);

            dtos = dtos.Select(item => item.Draft.ContractDraft is { } contract
                ? item with
                {
                    Draft = item.Draft with
                    {
                        ContractDraft = contract with
                        {
                            TenantNames = contract.TenantIds
                                .Select(id => tenantNames.GetValueOrDefault(id))
                                .Where(fullName => !string.IsNullOrWhiteSpace(fullName))
                                .Select(fullName => fullName!)
                                .ToList(),
                        },
                    },
                }
                : item).ToList();
        }

        return dtos;
    }

    private static AiDraftView ParseDraft(AiUseCase useCase, string outputJson)
    {
        try
        {
            return useCase switch
            {
                AiUseCase.ReportSummary => new AiDraftView(
                    ReportSummary: JsonSerializer.Deserialize<AiReportSummaryOutput>(outputJson, JsonOptions)),
                AiUseCase.ReminderMessage => new AiDraftView(
                    Notification: JsonSerializer.Deserialize<AiNotificationOutput>(outputJson, JsonOptions)),
                AiUseCase.RoomDraft => new AiDraftView(
                    RoomDraft: JsonSerializer.Deserialize<AiRoomDraftOutput>(outputJson, JsonOptions)),
                AiUseCase.IncidentTriage => new AiDraftView(
                    Incident: JsonSerializer.Deserialize<AiIncidentSuggestionOutput>(outputJson, JsonOptions)),
                AiUseCase.DailySummary => new AiDraftView(
                    WorkSummary: JsonSerializer.Deserialize<AiWorkSummaryOutput>(outputJson, JsonOptions)),
                AiUseCase.TenantDraft => new AiDraftView(
                    TenantDraft: JsonSerializer.Deserialize<AiTenantDraftOutput>(outputJson, JsonOptions)),
                AiUseCase.ServiceDraft => new AiDraftView(
                    ServiceDraft: JsonSerializer.Deserialize<AiServiceDraftOutput>(outputJson, JsonOptions)),
                AiUseCase.ContractDraft => new AiDraftView(
                    ContractDraft: JsonSerializer.Deserialize<AiContractDraftOutput>(outputJson, JsonOptions)),
                _ => new AiDraftView(),
            };
        }
        catch (JsonException)
        {
            return new AiDraftView();
        }
    }

    private static bool CanEditOutput(AiUseCase useCase) =>
        useCase is AiUseCase.ReminderMessage or AiUseCase.RoomDraft;

    /// <summary>Chỉ U1/U5 được cache trong ngày; khóa gồm người gọi, use case, ngày và ngữ cảnh.</summary>
    private string? CacheKey(AiGenerateRequest request, string contextJson, string userId)
    {
        if (request.UseCase is not (AiUseCase.ReportSummary or AiUseCase.DailySummary))
        {
            return null;
        }

        var today = DateOnly.FromDateTime(VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contextJson)));
        return $"ai.suggestion:{request.UseCase}:{userId}:{today:yyyyMMdd}:{hash}";
    }

    private static MemoryCacheEntryOptions CacheEntryOptions() => new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        Size = 1,
    };

    private static string UseCaseName(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => "tóm tắt báo cáo",
        AiUseCase.ReminderMessage => "nhắc thanh toán",
        AiUseCase.RoomDraft => "nháp nhà/phòng",
        AiUseCase.IncidentTriage => "phân loại sự cố",
        AiUseCase.DailySummary => "tóm tắt việc trong ngày",
        AiUseCase.TenantDraft => "nháp hồ sơ khách thuê",
        AiUseCase.ServiceDraft => "nháp dịch vụ",
        AiUseCase.ContractDraft => "nháp hợp đồng thuê",
        _ => "trợ lý AI",
    };
}
