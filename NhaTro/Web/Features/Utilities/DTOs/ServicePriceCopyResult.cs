namespace Web.Features.Utilities.DTOs;

/// <summary>PLAN-012: kết quả sao chép danh mục/giá từ nhà trọ nguồn sang nhà trọ đích.</summary>
public sealed record ServicePriceCopyResult(int Copied, int Skipped);
