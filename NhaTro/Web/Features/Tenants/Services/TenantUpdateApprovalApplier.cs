using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Tenants.DTOs;
using Web.Infrastructure.Time;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Áp dụng yêu cầu SW-05 (sửa hồ sơ khách thuê) khi Chủ trọ duyệt. Số giấy tờ chỉ được
/// ghi khi Quản lý thực sự đổi; nếu không, giữ nguyên bản gốc (D9).
/// </summary>
public sealed class TenantUpdateApprovalApplier(
    TimeProvider clock,
    IDataProtectionProvider? dataProtection = null) : IApprovalApplier, IApprovalSaveExceptionMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-05";

    public async Task ApplyAsync(
        ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Tenant
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId)
            || tenantId <= 0
            || request.ExpectedToken is not { Length: > 0 })
        {
            throw Invalid();
        }

        var payload = Deserialize(request);
        if (payload.Action != TenantApprovalActions.Update || payload.TenantId != tenantId)
        {
            throw Invalid();
        }

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
        if (tenant is null)
        {
            throw new ApprovalApplyException(TenantErrorCodes.TenantNotFound, TenantErrorMessages.TenantNotFound);
        }

        var fullName = TenantNormalizer.NormalizeFullName(payload.FullName ?? string.Empty);
        var phone = TenantNormalizer.NormalizePhone(payload.PhoneNumber ?? string.Empty);
        var email = TenantNormalizer.NormalizeOptional(payload.Email);
        var idNumber = payload.IdNumberUnchanged
            ? tenant.CitizenId
            : NullIfEmpty(TenantNormalizer.NormalizeIdNumber(
                TenantApprovalPayloadCodec.Unprotect(dataProtection, payload.IdNumber)));
        var address = TenantNormalizer.NormalizeOptional(payload.PermanentAddress);
        var issuedPlace = TenantNormalizer.NormalizeOptional(payload.IdIssuedPlace);
        var note = TenantNormalizer.NormalizeOptional(payload.Note);
        var today = clock.TodayVietnam();

        if (!IsValidProfile(fullName, phone, email, idNumber, address, issuedPlace, note,
                payload.DateOfBirth, payload.IdIssuedDate, today))
        {
            throw Invalid();
        }

        if (idNumber is not null && idNumber != tenant.CitizenId
            && await db.Tenants.AsNoTracking()
                .AnyAsync(t => t.IsActive && t.Id != tenantId && t.CitizenId == idNumber, ct))
        {
            throw new ApprovalApplyException(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
        }

        db.Entry(tenant).Property(t => t.RowVersion).OriginalValue = request.ExpectedToken;
        tenant.FullName = fullName;
        tenant.Phone = phone;
        tenant.Email = email;
        tenant.CitizenId = idNumber;
        tenant.DateOfBirth = payload.DateOfBirth;
        tenant.Gender = payload.Gender;
        tenant.PermanentAddress = address;
        tenant.IdIssuedDate = payload.IdIssuedDate;
        tenant.IdIssuedPlace = issuedPlace;
        tenant.Note = note;
        tenant.UpdatedAt = clock.UtcNow();
    }

    public ApprovalApplyException? MapSaveException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return new ApprovalApplyException(
                    TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
            }
        }

        return null;
    }

    private static TenantApprovalPayload Deserialize(ManagerApprovalRequest request)
    {
        try
        {
            return JsonSerializer.Deserialize<TenantApprovalPayload>(request.PayloadJson, PayloadJsonOptions)
                ?? throw Invalid();
        }
        catch (JsonException ex)
        {
            throw new ApprovalApplyException(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest, ex);
        }
    }

    private static bool IsValidProfile(
        string fullName, string phone, string? email, string? idNumber,
        string? address, string? issuedPlace, string? note,
        DateOnly? dateOfBirth, DateOnly? idIssuedDate, DateOnly today) =>
        fullName.Length is > 0 and <= 200
        && phone.Length <= 20
        && TenantNormalizer.IsValidVietnamesePhone(phone)
        && (email?.Length ?? 0) <= 256
        && (idNumber?.Length ?? 0) <= 20
        && (idNumber is null || idNumber.Length >= 8)
        && (address?.Length ?? 0) <= 300
        && (issuedPlace?.Length ?? 0) <= 200
        && (note?.Length ?? 0) <= 500
        && (dateOfBirth is null || dateOfBirth <= today)
        && (idIssuedDate is null || idIssuedDate <= today)
        && (idIssuedDate is null || dateOfBirth is null || idIssuedDate >= dateOfBirth);

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static ApprovalApplyException Invalid() =>
        new(TenantErrorCodes.InvalidRequest, TenantErrorMessages.InvalidRequest);
}
