namespace Web.Features.Tenants.DTOs;

/// <summary>Số liệu tổng quan dùng trên màn hình danh sách khách thuê P3.</summary>
public sealed class TenantListSummaryDto
{
    public required int TotalCount { get; init; }

    public required int ActiveCount { get; init; }

    public required int LinkedAccountCount { get; init; }

    public required int WithIdNumberCount { get; init; }
}
