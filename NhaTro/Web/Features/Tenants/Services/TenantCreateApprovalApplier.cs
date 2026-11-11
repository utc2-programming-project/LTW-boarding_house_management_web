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
using Web.Features.Tenants.Entities;
using Web.Infrastructure.Time;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Áp dụng yêu cầu SW-04 (tạo hồ sơ khách thuê) khi Chủ trọ duyệt: kiểm lại dữ liệu
/// tại thời điểm duyệt rồi mới tạo hồ sơ + liên kết chủ sở hữu trong cùng transaction.
/// </summary>
public sealed class TenantCreateApprovalApplier(
    TimeProvider clock,
    IDataProtectionProvider? dataProtection = null) : IApprovalApplier, IApprovalSaveExceptionMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-04";

    public async Task ApplyAsync(
        ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Tenant
            || request.PropertyId is not null && !await PropertyExistsAsync(db, request.PropertyId.Value, ct))
        {
            throw Invalid(TenantErrorCodes.InvalidRequest);
        }

        var payload = Deserialize(request);
        if (payload.Action != TenantApprovalActions.Create
            || string.IsNullOrWhiteSpace(payload.FullName)
            || string.IsNullOrWhiteSpace(payload.PhoneNumber))
        {
            throw Invalid(TenantErrorCodes.InvalidRequest);
        }

        var fullName = TenantNormalizer.NormalizeFullName(payload.FullName);
        var phone = TenantNormalizer.NormalizePhone(payload.PhoneNumber);
        var idNumber = NullIfEmpty(TenantNormalizer.NormalizeIdNumber(
            TenantApprovalPayloadCodec.Unprotect(dataProtection, payload.IdNumber)));
        var email = TenantNormalizer.NormalizeOptional(payload.Email);
        var address = TenantNormalizer.NormalizeOptional(payload.PermanentAddress);
        var issuedPlace = TenantNormalizer.NormalizeOptional(payload.IdIssuedPlace);
        var note = TenantNormalizer.NormalizeOptional(payload.Note);

        if (!IsValidProfile(fullName, phone, email, idNumber, address, issuedPlace, note,
                payload.DateOfBirth, payload.IdIssuedDate, clock.TodayVietnam()))
        {
            throw Invalid(TenantErrorCodes.InvalidRequest);
        }

        if (idNumber is not null
            && await db.Tenants.AsNoTracking()
                .AnyAsync(t => t.IsActive && t.CitizenId == idNumber, ct))
        {
            throw new ApprovalApplyException(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
        }

        var now = clock.UtcNow();
        var tenant = new Tenant
        {
            FullName = fullName,
            Phone = phone,
            Email = email,
            CitizenId = idNumber,
            DateOfBirth = payload.DateOfBirth,
            Gender = payload.Gender,
            PermanentAddress = address,
            IdIssuedDate = payload.IdIssuedDate,
            IdIssuedPlace = issuedPlace,
            Note = note,
            IsActive = true,
            CreatedAt = now,
        };
        db.Tenants.Add(tenant);

        if (!string.IsNullOrWhiteSpace(payload.OwnerId))
        {
            db.TenantOwnerLinks.Add(new TenantOwnerLink
            {
                Tenant = tenant,
                OwnerId = payload.OwnerId,
                CreatedAt = now,
            });
        }
    }

    public ApprovalApplyException? MapSaveException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return new ApprovalApplyException(TenantErrorCodes.DuplicateTenant, TenantErrorMessages.DuplicateTenant);
            }
        }

        return null;
    }

    private static TenantApprovalPayload Deserialize(ManagerApprovalRequest request)
    {
        try
        {
            return JsonSerializer.Deserialize<TenantApprovalPayload>(request.PayloadJson, PayloadJsonOptions)
                ?? throw Invalid(TenantErrorCodes.InvalidRequest);
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

    private static Task<bool> PropertyExistsAsync(ApplicationDbContext db, int propertyId, CancellationToken ct) =>
        db.Properties.AnyAsync(property => property.Id == propertyId && !property.IsDeleted, ct);

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static ApprovalApplyException Invalid(string errorCode) =>
        new(errorCode, TenantErrorMessages.InvalidRequest);
}
