using System.ComponentModel.DataAnnotations;
using Web.Common.Models;
using Web.Features.Contracts.Enums;

namespace Web.Features.Reports.DTOs;

public sealed class ContractReportFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? PropertyId { get; set; }
    public ContractStatus? Status { get; set; }
    [MaxLength(50)] public string? Keyword { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public void Normalize() { if (Page < 1) Page = 1; if (PageSize < 1) PageSize = 20; else if (PageSize > 100) PageSize = 100; Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(); }
}
public sealed class ContractReportKpis { public int Total { get; init; } public int Approved { get; init; } public int Pending { get; init; } public int Rejected { get; init; } public int Liquidated { get; init; } }
public sealed class ContractReportItem { public required Guid ContractId { get; init; } public required string ContractNumber { get; init; } public required int RoomId { get; init; } public required string RoomCode { get; init; } public required int PropertyId { get; init; } public required string PropertyName { get; init; } public required DateOnly StartDate { get; init; } public required DateOnly EndDate { get; init; } public required ContractStatus Status { get; init; } public required decimal RentAmount { get; init; } public required decimal DepositAmount { get; init; } public required int MemberCount { get; init; } public required bool IsActiveInPeriod { get; init; } }
public sealed class ContractReportResult { public ContractReportKpis Kpis { get; init; } = new(); public PagedResult<ContractReportItem> Contracts { get; init; } = new(); }
