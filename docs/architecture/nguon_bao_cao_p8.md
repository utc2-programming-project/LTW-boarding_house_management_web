# Nguon bao cao P8

| Nguon | Interface | Service | DTO |
| --- | --- | --- | --- |
| Hop dong | `IContractReportService` | `ContractReportService` | `ContractReportDtos.cs` |
| Chi phi dien nuoc | `ICostReportService` | `CostReportService` | `CostReportDtos.cs` |
| Su co | `IIncidentReportService` | `IncidentReportService` | `IncidentReportDtos.cs` |

## Ky va KPI

`From`/`To` la khoang ngay dong hai dau. `From > To` tra ket qua rong.
KPI luon tinh tren toan bo predicate, khong chi tren trang hien tai.

- Hop dong: `Kpis.Total == Contracts.TotalCount == Approved + Pending + Rejected + Liquidated`.
- Chi phi: `Kpis.ReadingCount == Readings.TotalCount`; chi phi la snapshot dien/nuoc P5.
- Su co: `Kpis.New + InProgress + Completed == Kpis.Total`.

## Cach goi

```csharp
var contracts = await contractReport.GetAsync(
    new ContractReportFilter { From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30) },
    caller, ct);
```

## Canh bao ky thuat

- `UtilityBillingPeriodSnapshots.BillingPeriodId` phai bang `UtilityReading.Id`, khong phai `UtilityReadingPeriod.Id`.
- Khong doc `UtilityReadingPeriods` hay `UtilityServicePeriodSnapshots` cho nguon nay.
- Snapshot chi ton tai khi usage lon hon 0; thieu snapshot la chua co so lieu, khong phai 0 dong.
- Dich vu rac, internet, xe khong nam trong snapshot dien/nuoc va khong co trong bao cao nay.
- Quan ly khong duoc xem doanh thu hay cong no.

## SQL doi chieu

```sql
SELECT COUNT(*) FROM Contracts WHERE EndDate >= @from AND StartDate <= @to;
SELECT COUNT(*) FROM UtilityReadings WHERE PeriodEnd >= @from AND PeriodStart <= @to;
SELECT COUNT(*) FROM MaintenanceIncidents WHERE CreatedAt >= @fromUtc AND CreatedAt < @toUtc;
```

`@fromUtc`/`@toUtc` phai duoc doi bang `VietnamTime.DayRangeUtc`.
