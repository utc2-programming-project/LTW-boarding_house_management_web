using System.ComponentModel.DataAnnotations;
using Web.Common.Models;

namespace Web.Features.Reports.DTOs;

public sealed class CostReportFilter { public DateOnly? From { get; set; } public DateOnly? To { get; set; } public int? PropertyId { get; set; } public int? RoomId { get; set; } [Range(1, int.MaxValue)] public int Page { get; set; } = 1; [Range(1, 100)] public int PageSize { get; set; } = 20; public void Normalize() { if (Page < 1) Page = 1; if (PageSize < 1) PageSize = 20; else if (PageSize > 100) PageSize = 100; } }
public sealed class CostReportKpis { public int ReadingCount { get; init; } public decimal ElectricUsage { get; init; } public decimal ElectricAmount { get; init; } public decimal WaterUsage { get; init; } public decimal WaterAmount { get; init; } public decimal TotalAmount { get; init; } public int MissingSnapshotCount { get; init; } }
public sealed class CostReportItem { public required int ReadingId { get; init; } public required int RoomId { get; init; } public required string RoomCode { get; init; } public required int PropertyId { get; init; } public required string PropertyName { get; init; } public Guid? ContractId { get; init; } public required DateOnly PeriodStart { get; init; } public required DateOnly PeriodEnd { get; init; } public required decimal ElectricUsage { get; init; } public required decimal ElectricAmount { get; init; } public required decimal WaterUsage { get; init; } public required decimal WaterAmount { get; init; } public required decimal TotalAmount { get; init; } public required bool HasSnapshot { get; init; } }
public sealed class CostReportResult { public CostReportKpis Kpis { get; init; } = new(); public PagedResult<CostReportItem> Readings { get; init; } = new(); }
