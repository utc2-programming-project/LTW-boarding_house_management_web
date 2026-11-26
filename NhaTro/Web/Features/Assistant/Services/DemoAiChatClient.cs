using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Web.Configuration.Options;
using Web.Features.Assistant.Enums;
using Microsoft.Extensions.Options;

namespace Web.Features.Assistant.Services;

/// <summary>
/// Provider "Demo": sinh đề xuất theo mẫu từ chính ngữ cảnh server đã gom (không gọi mạng).
/// Dùng để chạy trọn luồng tạo → duyệt → áp dụng khi chưa cấu hình model, và trong test.
/// Không thay thế model thật: nội dung chỉ là câu mẫu dựa trên số liệu có thật.
/// </summary>
public sealed class DemoAiChatClient(IOptions<AiOptions> options)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public AiChatResult Complete(AiChatRequest request)
    {
        using var document = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(request.ContextJson) ? "{}" : request.ContextJson);
        var context = document.RootElement;

        var json = request.UseCase switch
        {
            AiUseCase.ReportSummary => ReportSummary(context),
            AiUseCase.ReminderMessage => Reminder(context),
            AiUseCase.RoomDraft => RoomDraft(context, request.Instruction),
            AiUseCase.IncidentTriage => IncidentTriage(context, request.Instruction),
            AiUseCase.DailySummary => DailySummary(context),
            AiUseCase.TenantDraft => TenantDraft(context, request.Instruction),
            AiUseCase.ServiceDraft => ServiceDraft(context, request.Instruction),
            AiUseCase.ContractDraft => ContractDraft(context, request.Instruction),
            _ => "{}",
        };

        return AiChatResult.Success(json, $"demo:{options.Value.Model}");
    }

    private static string ReportSummary(JsonElement context)
    {
        var metrics = new List<object>();
        var insights = new List<object>();
        var actions = new List<object>();

        var revenue = Prop(context, "revenue");
        var rooms = Prop(context, "rooms");
        var incidents = Prop(context, "incidents");
        var contracts = Prop(context, "contracts");
        var costs = Prop(context, "costs");

        if (revenue is { } rev)
        {
            metrics.Add(Metric("Doanh thu đã thu", Money(GetDecimal(rev, "totalRevenue"))));
            metrics.Add(Metric("Công nợ còn lại", Money(GetDecimal(rev, "outstanding"))));

            var overdue = GetDecimal(rev, "overdueOutstanding");
            var overdueCount = GetInt(rev, "overdueInvoiceCount");
            if (overdueCount > 0)
            {
                insights.Add(Insight(
                    $"{overdueCount} hóa đơn quá hạn",
                    $"Còn {Money(overdue)} chưa thu trên hóa đơn đã quá hạn.",
                    "critical"));
                actions.Add(Action(
                    "Đối soát công nợ quá hạn",
                    "Mở danh sách hóa đơn và gửi nhắc thanh toán cho các phòng còn nợ.",
                    "/invoices"));
            }
        }

        if (rooms is { } room)
        {
            metrics.Add(Metric("Phòng đang thuê", $"{GetInt(room, "rented")}/{GetInt(room, "total")}"));
            if (GetInt(room, "available") > 0)
            {
                insights.Add(Insight(
                    "Còn phòng trống",
                    $"{GetInt(room, "available")} phòng đang trống, có thể đẩy tin cho thuê.",
                    "info"));
            }
        }

        if (incidents is { } incident)
        {
            metrics.Add(Metric("Sự cố đang mở", (GetInt(incident, "new") + GetInt(incident, "inProgress")).ToString(Vietnamese)));
            if (GetInt(incident, "urgent") > 0)
            {
                insights.Add(Insight(
                    "Sự cố khẩn cấp",
                    $"{GetInt(incident, "urgent")} sự cố mức khẩn cần xử lý trong ngày.",
                    "critical"));
                actions.Add(Action("Xử lý sự cố khẩn", "Ưu tiên các sự cố mức Khẩn/Cấp bách.", "/incidents"));
            }
        }

        if (contracts is { } contract)
        {
            var expiring = GetInt(contract, "expiring30");
            if (expiring > 0)
            {
                insights.Add(Insight(
                    "Hợp đồng sắp hết hạn",
                    $"{expiring} hợp đồng hết hạn trong 30 ngày tới.",
                    "warning"));
                actions.Add(Action("Gia hạn hợp đồng", "Liên hệ khách trước khi hợp đồng hết hạn.", "/contracts"));
            }
        }

        if (costs is { } cost && GetInt(cost, "missingSnapshots") > 0)
        {
            insights.Add(Insight(
                "Thiếu snapshot chi phí",
                $"{GetInt(cost, "missingSnapshots")} kỳ ghi chỉ số chưa chốt giá điện/nước.",
                "warning"));
        }

        if (insights.Count == 0)
        {
            insights.Add(Insight("Vận hành ổn định", "Không có điểm bất thường trong kỳ đã chọn.", "info"));
        }

        if (actions.Count == 0)
        {
            actions.Add(Action("Duy trì theo dõi", "Xem báo cáo định kỳ để bắt biến động sớm.", "/reports"));
        }

        var period = GetString(Prop(context, "common"), "periodLabel") ?? "kỳ hiện tại";
        var summary =
            $"Tổng hợp {period}: " +
            $"{GetInt(rooms, "rented")} phòng đang thuê trên {GetInt(rooms, "total")} phòng; " +
            $"còn {Money(GetDecimal(revenue, "outstanding"))} công nợ; " +
            $"{GetInt(incidents, "new") + GetInt(incidents, "inProgress")} sự cố đang mở.";

        return JsonSerializer.Serialize(new
        {
            headline = $"Tóm tắt vận hành {period}",
            summary,
            insights,
            actions,
            metrics,
        });
    }

    private static string Reminder(JsonElement context)
    {
        var debtors = Prop(context, "debtors");
        var lines = new List<string>();
        var total = 0m;
        if (debtors is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var debtor in array.EnumerateArray().Take(10))
            {
                total += GetDecimal(debtor, "amount");
                var names = GetStringArray(debtor, "tenantNames");
                var who = names.Count > 0 ? string.Join(", ", names) : "Quý khách";
                var due = GetString(debtor, "dueDate");
                lines.Add(
                    $"- Phòng {GetString(debtor, "roomCode")} ({who}): {Money(GetDecimal(debtor, "amount"))}" +
                    (string.IsNullOrWhiteSpace(due) ? "." : $", hạn {FormatDate(due)}."));
            }
        }

        var tone = GetString(context, "tone") ?? GetString(Prop(context, "common"), "instruction");
        var intro = string.IsNullOrWhiteSpace(tone)
            ? "Ban quản lý nhà trọ thông báo tới Quý khách về các khoản thanh toán còn lại:"
            : $"Ban quản lý nhà trọ gửi lời nhắc: {tone}";

        var body = lines.Count == 0
            ? $"{intro} Hiện chưa ghi nhận khoản nào còn nợ. Nếu Quý khách đã thanh toán, vui lòng bỏ qua tin nhắn này."
            : $"{intro}\n{string.Join("\n", lines)}\nTổng cộng: {Money(total)}.\nVui lòng thanh toán trước hạn. Xin cảm ơn Quý khách.";

        return JsonSerializer.Serialize(new
        {
            title = "Nhắc thanh toán tiền phòng",
            body,
            notes = lines.Count == 0
                ? "Không có hóa đơn còn nợ trong phạm vi đã chọn."
                : $"Gửi tới {lines.Count} phòng còn nợ.",
        });
    }

    private static string RoomDraft(JsonElement context, string? instruction)
    {
        var text = instruction ?? string.Empty;
        var propertyId = GetNullableInt(Prop(context, "common"), "propertyId");

        var name = MatchName(text) ?? "Nhà trọ mới";
        var address = MatchAddress(text) ?? "Chưa cập nhật";
        var roomCount = MatchInt(@"(\d+)\s*(?:phòng|can|p)\b", text) ?? 5;
        roomCount = Math.Clamp(roomCount, 1, 100);
        var floors = MatchInt(@"(\d+)\s*tầng", text);
        var price = MatchPrice(text) ?? 2_500_000m;
        var area = MatchDecimal(@"(\d+(?:[.,]\d+)?)\s*m2", text);

        var existing = GetStringArray(Prop(context, "existingRoomCodes"), null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var perFloor = floors is { } f and > 0
            ? Math.Max(1, (int)Math.Ceiling(roomCount / (double)f))
            : roomCount;
        var floorCount = floors ?? Math.Max(1, (int)Math.Ceiling(roomCount / 20.0));

        var rooms = new List<object>();
        for (var index = 0; index < roomCount; index++)
        {
            var floor = floorCount <= 1 ? 1 : index / perFloor + 1;
            var number = index % perFloor + 1;
            var code = $"P{floor}{number:00}";
            while (existing.Contains(code))
            {
                number++;
                code = $"P{floor}{number:00}";
            }

            existing.Add(code);
            rooms.Add(new
            {
                code,
                floor,
                areaM2 = area,
                standardPrice = price,
            });
        }

        var summary = roomCount == 1
            ? "1 phòng"
            : $"{roomCount} phòng";
        var notes =
            $"Bản nháp gồm {summary}, giá thuê {Money(price)}/phòng" +
            (area is { } a ? $", diện tích {a.ToString("0.##", Vietnamese)} m²" : string.Empty) +
            ". Hãy kiểm tra mã phòng và giá trước khi duyệt.";

        return JsonSerializer.Serialize(new
        {
            property = propertyId is null
                ? new { name, address }
                : null,
            rooms,
            notes,
        });
    }

    private static string IncidentTriage(JsonElement context, string? instruction)
    {
        var text = (instruction ?? GetString(context, "incidentText") ?? string.Empty).ToLower(Vietnamese);

        var (category, keywords) = MatchCategory(text);
        var urgency = MatchUrgency(text);
        var reason = urgency switch
        {
            "Emergency" =>
                $"Nội dung có dấu hiệu nguy hiểm ({keywords}), cần xử lý ngay để tránh mất an toàn.",
            "Urgent" =>
                $"Sự cố ảnh hưởng sinh hoạt ({keywords}), nên xử lý trong ngày.",
            _ =>
                $"Sự cố thuộc nhóm {category}, chưa thấy dấu hiệu khẩn cấp.",
        };

        var title = instruction is { Length: > 5 and <= 200 } ? instruction.Trim() : null;
        return JsonSerializer.Serialize(new
        {
            category,
            urgency,
            assignee = (string?)null,
            reason,
            suggestedTitle = title,
            suggestedDescription = instruction is { Length: > 10 } ? instruction.Trim() : null,
        });
    }

    private static string DailySummary(JsonElement context)
    {
        var invoices = Prop(context, "invoiceDue");
        var incidents = Prop(context, "incidents");
        var approvals = Prop(context, "approvals");
        var aiPending = GetInt(context, "aiPending");
        var expiring = Prop(context, "expiringContracts");

        var items = new List<object>();
        var overdueCount = GetInt(invoices, "overdueCount");
        var dueCount = GetInt(invoices, "dueSoonCount");
        if (overdueCount > 0)
        {
            items.Add(WorkItem(
                "Hóa đơn quá hạn",
                overdueCount,
                "/invoices",
                "critical",
                $"Tổng {Money(GetDecimal(invoices, "overdueAmount"))} cần thu."));
        }

        if (dueCount > 0)
        {
            items.Add(WorkItem("Hóa đơn sắp đến hạn", dueCount, "/invoices", "warning", null));
        }

        var openIncidents = GetInt(incidents, "open");
        if (openIncidents > 0)
        {
            items.Add(WorkItem(
                "Sự cố đang mở",
                openIncidents,
                "/incidents",
                GetInt(incidents, "urgent") > 0 ? "critical" : "warning",
                GetInt(incidents, "urgent") > 0 ? $"{GetInt(incidents, "urgent")} sự cố khẩn." : null));
        }

        var pendingApprovals = GetInt(approvals, "pending");
        if (pendingApprovals > 0)
        {
            items.Add(WorkItem("Yêu cầu chờ duyệt", pendingApprovals, "/approvals", "warning", null));
        }

        if (aiPending > 0)
        {
            items.Add(WorkItem("Đề xuất AI chờ duyệt", aiPending, "/approvals?tab=ai", "info", null));
        }

        if (expiring is { } exp && GetInt(exp, "count") > 0)
        {
            items.Add(WorkItem("Hợp đồng sắp hết hạn", GetInt(exp, "count"), "/contracts", "warning", null));
        }

        var summary = items.Count == 0
            ? "Hôm nay không có việc nào đến hạn trong phạm vi của bạn."
            : $"Hôm nay có {overdueCount} hóa đơn quá hạn, {openIncidents} sự cố đang mở và {pendingApprovals + aiPending} đề xuất cần duyệt.";

        return JsonSerializer.Serialize(new
        {
            headline = "Việc cần làm hôm nay",
            summary,
            items,
        });
    }

    private static string TenantDraft(JsonElement context, string? instruction)
    {
        var text = instruction ?? string.Empty;
        var name = MatchTenantName(text) ?? string.Empty;
        var phone = MatchPhone(text) ?? string.Empty;
        var email = MatchEmail(text);
        var address = MatchAddress(text);
        var birth = MatchBirthDate(text);
        var gender = MatchGender(text);

        return JsonSerializer.Serialize(new
        {
            fullName = name,
            phoneNumber = phone,
            email,
            dateOfBirth = birth,
            gender,
            permanentAddress = address,
            note = (string?)null,
        });
    }

    private static string ServiceDraft(JsonElement context, string? instruction)
    {
        var text = instruction ?? string.Empty;
        var name = MatchServiceName(text) ?? "Dịch vụ mới";
        var (unit, method) = MatchServiceUnit(text);
        var price = MatchPrice(text) ?? 50_000m;
        var today = GetString(Prop(context, "common"), "today")
            ?? GetString(context, "today")
            ?? "2026-01-01";
        var effectiveDate = MatchDateAfter(text, @"(?:từ|áp dụng từ|hiệu lực từ)\s*(\d{1,2}/\d{1,2}/\d{4})")?.ToString("yyyy-MM-dd") ?? today;

        return JsonSerializer.Serialize(new
        {
            name,
            unit,
            calculationMethod = method,
            unitPrice = price,
            effectiveDate,
            effectiveTo = (string?)null,
            note = (string?)null,
            description = (string?)null,
        });
    }

    private static string ContractDraft(JsonElement context, string? instruction)
    {
        var text = instruction ?? string.Empty;
        var today = ParseIso(GetString(context, "today")
            ?? GetString(Prop(context, "common"), "today"))
            ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var room = FindRoom(context, text);
        var tenant = FindTenant(context, text);
        var roomId = room is { } r ? GetNullableInt(r, "id") : null;
        var roomCode = room is { } r2 ? GetString(r2, "code") : null;

        var start = MatchDateAfter(text, @"từ\s*(\d{1,2}/\d{1,2}/\d{4})") ?? today;
        // Hợp đồng chỉ được bắt đầu từ hôm nay trở về trước (rule FutureStartNotAllowed).
        if (start > today)
        {
            start = today;
        }

        var end = MatchDateAfter(text, @"(?:đến|tới)\s*(\d{1,2}/\d{1,2}/\d{4})")
            ?? start.AddMonths(12).AddDays(-1);
        if (end < start.AddMonths(1))
        {
            end = start.AddMonths(1);
        }

        var rent = MatchPrice(text) ?? (room is { } r3 ? GetDecimal(r3, "standardPrice") : 0m);
        var depositMatch = Regex.Match(text, @"cọc[^.\n]{0,40}", RegexOptions.IgnoreCase);
        decimal? deposit = depositMatch.Success ? MatchPrice(depositMatch.Value) : null;
        if (deposit is null && depositMatch.Success)
        {
            var depositMonths = Regex.Match(depositMatch.Value, @"(\d{1,2})\s*tháng");
            if (depositMonths.Success
                && int.TryParse(depositMonths.Groups[1].Value, out var months)
                && months > 0)
            {
                deposit = rent * months;
            }
        }

        var cycle = 1;
        if (ContainsAny(text, "6 tháng", "nửa năm"))
        {
            cycle = 6;
        }
        else if (ContainsAny(text, "3 tháng", "quý", "một quý"))
        {
            cycle = 3;
        }
        else if (ContainsAny(text, "2 tháng", "hai tháng"))
        {
            cycle = 2;
        }

        return JsonSerializer.Serialize(new
        {
            roomId,
            roomCode,
            tenantIds = tenant is { } t && GetNullableInt(t, "id") is { } tenantId
                ? new[] { tenantId }
                : Array.Empty<int>(),
            startDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            endDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            depositAmount = deposit,
            monthlyRent = rent > 0 ? rent : (decimal?)null,
            paymentCycleMonths = cycle,
            meterReadingDay = (int?)null,
            terms = (string?)null,
        });
    }

    private static string? MatchTenantName(string text)
    {
        var match = Regex.Match(text,
            @"(?:khách thuê|khách hàng|khách|hồ sơ|tên)\s+(?:mới\s+)?(?:là\s+)?([^,.;\n]+)",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        value = Regex.Replace(value, @"\s+(sđt|sdt|điện thoại|email|sinh ngày).*$", string.Empty,
            RegexOptions.IgnoreCase).Trim();
        return value.Length is >= 2 and <= 200 ? value : null;
    }

    private static string? MatchPhone(string text)
    {
        var match = Regex.Match(text, @"(?<!\d)(?:\+?84|0)([\d\s.\-]{7,13}\d)(?!\d)");
        if (!match.Success)
        {
            return null;
        }

        var digits = new string(match.Value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11)
        {
            digits = "0" + digits[2..];
        }

        return digits.Length is >= 9 and <= 12 ? digits : null;
    }

    private static string? MatchEmail(string text)
    {
        var match = Regex.Match(text, @"[\w.+\-]+@[\w\-]+(?:\.[\w\-]+)+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static string? MatchBirthDate(string text)
    {
        var match = Regex.Match(text, @"(?:sinh|sinh ngày|ngày sinh)[^\d]{0,10}(\d{1,2}/\d{1,2}/\d{4})",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return DateOnly.TryParseExact(match.Groups[1].Value, "d/M/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static string? MatchGender(string text)
    {
        var match = Regex.Match(text, @"giới tính\s*(nam|nữ|khác)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.ToLower(Vietnamese) switch
        {
            "nam" => "Male",
            "nữ" => "Female",
            _ => "Other",
        };
    }

    private static string? MatchServiceName(string text)
    {
        var match = Regex.Match(text, @"dịch vụ\s+(?:mới\s+)?([^,.;\n]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var value = match.Groups[1].Value.Trim();
            value = Regex.Replace(value, @"\s+(giá|với|theo)\b.*$", string.Empty,
                RegexOptions.IgnoreCase).Trim();
            if (value.Length is > 0 and <= 200)
            {
                return char.ToUpper(value[0], Vietnamese) + value[1..];
            }
        }

        if (ContainsAny(text, "wifi", "internet"))
        {
            return "Internet";
        }

        if (ContainsAny(text, "rác"))
        {
            return "Phí rác";
        }

        if (ContainsAny(text, "giữ xe", "gửi xe", "trông xe"))
        {
            return "Phí gửi xe";
        }

        if (ContainsAny(text, "điện"))
        {
            return "Điện";
        }

        if (ContainsAny(text, "nước"))
        {
            return "Nước";
        }

        return null;
    }

    private static (string Unit, string Method) MatchServiceUnit(string text)
    {
        if (ContainsAny(text, "kwh", "số điện", "chỉ số điện", "điện"))
        {
            return ("kWh", "meter");
        }

        if (ContainsAny(text, "m3", "m³", "khối", "nước"))
        {
            return ("m³", "meter");
        }

        if (ContainsAny(text, "người"))
        {
            return ("người", "fixed-monthly");
        }

        if (ContainsAny(text, "xe"))
        {
            return ("xe", "fixed-monthly");
        }

        if (ContainsAny(text, "lượt"))
        {
            return ("lượt", "custom");
        }

        return ("phòng", ContainsAny(text, "bậc thang") ? "tiered" : "fixed-monthly");
    }

    private static DateOnly? ParseIso(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var date)
            ? date
            : null;

    private static DateOnly? MatchDateAfter(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return DateOnly.TryParseExact(match.Groups[1].Value, "d/M/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static JsonElement? FindRoom(JsonElement context, string text)
    {
        var rooms = Items(Prop(context, "rooms")).ToList();
        if (rooms.Count == 0)
        {
            return null;
        }

        var byCode = rooms.FirstOrDefault(room =>
        {
            var code = GetString(room, "code");
            return !string.IsNullOrWhiteSpace(code)
                && Regex.IsMatch(text, @"(?:^|[^A-Za-z0-9])" + Regex.Escape(code) + @"(?:$|[^A-Za-z0-9])",
                    RegexOptions.IgnoreCase);
        });
        if (byCode.ValueKind == JsonValueKind.Object)
        {
            return byCode;
        }

        // Câu có nêu mã phòng nhưng không khớp phòng trống nào: trả null để báo lỗi rõ
        // thay vì tự chọn một phòng khác.
        if (Regex.IsMatch(text, @"ph[oò]ng\s+[A-Za-z0-9]", RegexOptions.IgnoreCase))
        {
            return null;
        }

        var available = rooms.FirstOrDefault(room => GetBool(room, "isAvailable"));
        return available.ValueKind == JsonValueKind.Object ? available : rooms[0];
    }

    private static JsonElement? FindTenant(JsonElement context, string text)
    {
        var tenants = Items(Prop(context, "tenants")).ToList();
        if (tenants.Count == 0)
        {
            return null;
        }

        var byName = tenants.Where(tenant =>
        {
            var fullName = GetString(tenant, "fullName");
            return !string.IsNullOrWhiteSpace(fullName)
                && text.Contains(fullName, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        if (byName.Count == 1)
        {
            return byName[0];
        }

        // Dò theo từ cuối của họ tên (tên gọi) nếu không khớp đầy đủ.
        var byShortName = tenants.Where(tenant =>
        {
            var fullName = GetString(tenant, "fullName");
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return false;
            }

            var given = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
            return given.Length >= 2
                && Regex.IsMatch(text, @"(?:^|\s)" + Regex.Escape(given) + @"(?:$|\s|,|\.)",
                    RegexOptions.IgnoreCase);
        }).ToList();
        if (byShortName.Count == 1)
        {
            return byShortName[0];
        }

        // Chỉ dùng số điện thoại khi nó xác định duy nhất một khách.
        var byPhone = tenants.Where(tenant =>
        {
            var phone = GetString(tenant, "phone");
            return !string.IsNullOrWhiteSpace(phone) && text.Contains(phone, StringComparison.Ordinal);
        }).ToList();
        if (byPhone.Count == 1)
        {
            return byPhone[0];
        }

        // Không khớp hoặc khớp nhiều khách (vd. chỉ ghi "Lan"): trả null để hợp đồng
        // không chọn sai người và người dùng bổ sung tên đầy đủ.
        return null;
    }

    private static IEnumerable<JsonElement> Items(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray()
            : [];

    private static bool GetBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static (string Category, string Keywords) MatchCategory(string text)
    {
        if (ContainsAny(text, "điện", "chập", "đèn", "ổ cắm", "cầu dao", "phích"))
        {
            return ("Electrical", "điện");
        }

        if (ContainsAny(text, "nước", "rò", "vòi", "ống", "bồn", "thấm", "dột"))
        {
            return ("Water", "nước");
        }

        if (ContainsAny(text, "cửa", "giường", "tủ", "bàn", "ghế", "khoá", "khóa"))
        {
            return ("Furniture", "nội thất");
        }

        if (ContainsAny(text, "điều hoà", "điều hòa", "máy lạnh", "quạt", "máy giặt", "bình nóng"))
        {
            return ("Equipment", "thiết bị");
        }

        return ("Other", "khác");
    }

    private static string MatchUrgency(string text) =>
        ContainsAny(text, "cháy", "khói", "điện giật", "ngập", "sập", "nguy hiểm", "cấp bách")
            ? "Emergency"
            : ContainsAny(text, "gấp", "khẩn", "ngay", "hỏng hoàn toàn", "không dùng được", "rò", "chập")
                ? "Urgent"
                : "Normal";

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));

    private static object Metric(string label, string value) => new { label, value };

    private static object Insight(string title, string detail, string severity) =>
        new { title, detail, severity };

    private static object Action(string title, string detail, string? link) =>
        new { title, detail, link };

    private static object WorkItem(string label, int count, string? link, string severity, string? detail) =>
        new { label, count, link, severity, detail };

    private static string? MatchName(string text)
    {
        var match = Regex.Match(text, @"(?:nhà|tòa|toà|khu)\s+(?:trọ\s+)?([^,.;\n]+)",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        return value.Length is 0 or > 200 ? null : char.ToUpper(value[0], Vietnamese) + value[1..];
    }

    private static string? MatchAddress(string text)
    {
        var match = Regex.Match(text, @"(?:địa chỉ|ở|tại)\s+([^.;\n]+)", RegexOptions.IgnoreCase);
        var value = match.Success ? match.Groups[1].Value.Trim() : null;
        return value is { Length: > 0 and <= 500 } ? value : null;
    }

    private static int? MatchInt(string pattern, string text)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static decimal? MatchDecimal(string pattern, string text)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups[1].Value.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static decimal? MatchPrice(string text)
    {
        var million = MatchDecimal(@"(\d+(?:[.,]\d+)?)\s*(?:tr|triệu|m\b)", text);
        if (million is { } value && value is > 0 and <= 1000)
        {
            return Math.Round(value * 1_000_000m, 0);
        }

        var grouped = Regex.Match(text, @"(\d{1,3}(?:\.\d{3})+)\s*(?:đ|vnd|đồng|dong)",
            RegexOptions.IgnoreCase);
        if (grouped.Success
            && decimal.TryParse(grouped.Groups[1].Value.Replace(".", string.Empty),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var groupedValue))
        {
            return groupedValue;
        }

        return MatchDecimal(@"(\d{4,12})\s*(?:đ|vnd|đồng|dong)", text)
            ?? MatchDecimal(@"(\d{6,12})\s*(?:đ|vnd|đồng|dong)?", text);
    }

    private static string Money(decimal amount) =>
        $"{amount.ToString("N0", Vietnamese)} đ";

    private static string FormatDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date)
            ? date.ToString("dd/MM/yyyy", Vietnamese)
            : value;

    private static JsonElement? Prop(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    private static string? GetString(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static int? GetNullableInt(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;

    private static decimal GetDecimal(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var number)
            ? number
            : 0m;

    private static IReadOnlyList<string> GetStringArray(JsonElement? element, string? name)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .ToList();
    }
}
