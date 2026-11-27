namespace Web.Features.Dashboard.Models;

/// <summary>Ba nhóm tuổi công nợ dùng cho biểu đồ cột trên trang chủ.</summary>
public sealed record DebtAgingDto(decimal Under30, decimal Days30To60, decimal Over60);

/// <summary>Số hợp đồng hết hạn trong từng tháng tới. SortKey = năm * 12 + tháng.</summary>
public sealed record ContractExpiryMonthDto(string MonthKey, int Count, int SortKey);
