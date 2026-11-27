using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Rules;

public static class GatePassRules
{
    public const int MinTtlMinutes = 5;
    public const int MaxTtlMinutes = 24 * 60;

    /// <summary>Chỉ khách đến chơi trong ngày mới được tự duyệt; qua đêm luôn chờ duyệt.</summary>
    public static bool CanAutoApprove(GatePolicyDto policy, GateStayType stayType) => stayType switch
    {
        GateStayType.Visit => policy.AutoApproveDayVisit,
        GateStayType.Overnight => false,
        _ => false,
    };

    /// <summary>Quản lý chỉ duyệt phiếu khi chủ trọ bật <c>AllowManagerApprove</c>.</summary>
    public static bool CanApprove(bool isChuTro, bool isQuanLy, bool allowManagerApprove) =>
        isChuTro || (isQuanLy && allowManagerApprove);

    public static bool TenantQuotaExceeded(int issuedToday, int maxPerDay) =>
        maxPerDay <= 0 || issuedToday >= maxPerDay;

    /// <summary>Giới hạn số phiếu tự điền/ngày theo số điện thoại; 0 nghĩa là tắt.</summary>
    public static bool SelfFillQuotaExceeded(int submittedToday, int maxPerDay) =>
        maxPerDay <= 0 || submittedToday >= maxPerDay;

    public static DateTimeOffset ComputeExpiry(DateTimeOffset now, int ttlMinutes) =>
        now.AddMinutes(Math.Clamp(ttlMinutes, MinTtlMinutes, MaxTtlMinutes));

    public static string? ValidateCreate(GatePassCreateRequest request, bool isTenant, DateOnly? todayVietnam = null)
    {
        if (string.IsNullOrWhiteSpace(request.VisitorName) || request.VisitorName.Trim().Length > 200)
        {
            return GateErrorCodes.MissingGuestName;
        }

        if (string.IsNullOrWhiteSpace(request.VisitorPhone) || request.VisitorPhone.Trim().Length > 30)
        {
            return GateErrorCodes.MissingGuestPhone;
        }

        // Giới hạn theo cột DB để không lỗi truncate khi dữ liệu gửi thẳng lên service.
        if (request.VisitorIdNumber is { } idNumber && idNumber.Trim().Length > 50)
        {
            return GateErrorCodes.InvalidRequest;
        }

        if (request.StayReason is { } stayReason && stayReason.Trim().Length > 500)
        {
            return GateErrorCodes.InvalidRequest;
        }

        if (request.Note is { } note && note.Trim().Length > 1000)
        {
            return GateErrorCodes.InvalidRequest;
        }

        if (todayVietnam is { } today && request.VisitorDob is { } dob && dob > today)
        {
            return GateErrorCodes.InvalidRequest;
        }

        if (request.StayType == GateStayType.Overnight)
        {
            if (request.VisitorDob is null || string.IsNullOrWhiteSpace(request.VisitorIdNumber) ||
                string.IsNullOrWhiteSpace(request.StayReason))
            {
                return GateErrorCodes.MissingLawfulFields;
            }
        }

        if (isTenant && request.RoomId is null)
        {
            return GateErrorCodes.RoomNotFound;
        }

        if (request.TtlMinutes is < MinTtlMinutes or > MaxTtlMinutes)
        {
            return GateErrorCodes.InvalidRequest;
        }

        if (request.MaxUses is < 1 or > 10)
        {
            return GateErrorCodes.InvalidRequest;
        }

        return null;
    }

    private static readonly string[] GuestFieldNames = ["FullName", "Phone", "Dob", "IdNumber", "StayReason"];

    /// <summary>Đọc cấu hình RequiredGuestFields, bỏ qua tên trường không nhận diện được.</summary>
    public static IReadOnlySet<string> ParseRequiredGuestFields(string? value)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (GuestFieldNames.Contains(part, StringComparer.OrdinalIgnoreCase))
            {
                fields.Add(part);
            }
        }

        return fields;
    }

    /// <summary>Bỏ khoảng trắng/ký tự ngăn cách và đổi +84 thành 0 để so trùng ổn định.</summary>
    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var cleaned = new string(phone.Where(ch => !char.IsWhiteSpace(ch) && ch is not ('-' or '.' or '(' or ')')).ToArray());
        return cleaned.StartsWith("+84", StringComparison.Ordinal) ? $"0{cleaned[3..]}" : cleaned;
    }

    public static bool IsVietnamMobile(string? phone) =>
        phone is { Length: 10 } && phone[0] == '0' && phone.All(char.IsAsciiDigit);

    /// <summary>Chỉ áp dụng trong khung giờ khách đến chơi (theo giờ Việt Nam).</summary>
    public static bool IsInsideDayVisitWindow(GatePolicyDto policy, DateTimeOffset at)
    {
        var local = at.ToOffset(TimeSpan.FromHours(7));
        return local.Hour >= policy.DayVisitStartHour && local.Hour < policy.DayVisitEndHour;
    }

    public static GateMethod DefaultMethods(GateStayType stayType) =>
        stayType == GateStayType.Overnight
            ? GateMethod.Qr | GateMethod.Code | GateMethod.Face
            : GateMethod.Qr | GateMethod.Code;
}

public static class StayReportRules
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>
    /// Hạn thông báo lưu trú: trước 23:00 ngày khách đến; nếu đến từ 23:00 trở đi
    /// thì trước 08:00 sáng hôm sau (giờ Việt Nam).
    /// </summary>
    public static DateTimeOffset DueAt(DateTimeOffset arrival)
    {
        var local = arrival.ToOffset(VietnamOffset);
        var dueToday = new DateTimeOffset(local.Year, local.Month, local.Day, 23, 0, 0, VietnamOffset);
        if (local >= dueToday)
        {
            return new DateTimeOffset(local.Year, local.Month, local.Day, 8, 0, 0, VietnamOffset).AddDays(1);
        }

        return dueToday;
    }

    public static DateTimeOffset DefaultStayTo(DateTimeOffset arrival) =>
        arrival.ToOffset(VietnamOffset).AddHours(12);
}
