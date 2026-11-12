namespace Web.Common.Models;

/// <summary>Kết quả dọn sạch thùng rác: số mục đã xóa và số mục bị giữ vì còn dữ liệu liên quan.</summary>
public sealed record TrashPurgeSummary(int Purged, int Skipped);
