namespace Web.Features.Assistant.Entities;

/// <summary>
/// Cài đặt trợ lý AI theo Chủ trọ: bật/tắt toàn bộ cho nhà mình và hạn mức gọi/ngày.
/// Chưa có dòng nghĩa là dùng mặc định (bật, hạn mức trong appsettings).
/// </summary>
public sealed class AiAssistantSettings
{
    public string OwnerUserId { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int DailyRequestLimit { get; set; } = 50;

    public DateTimeOffset UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
