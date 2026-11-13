using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Services;

namespace Web.Features.Contracts.Rules;

/// <summary>
/// Dựng snapshot nội dung hợp đồng cố định thứ tự + băm SHA-256 để làm bằng chứng
/// cho từng phiên bản gửi khách.
/// </summary>
public static class ContractSigningWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public sealed record SnapshotResult(string Json, string Hash);

    public static async Task<SnapshotResult> BuildSnapshotAsync(
        ApplicationDbContext db,
        Contract contract,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.AsNoTracking()
            .Where(item => item.Id == contract.RoomId)
            .Select(item => new { item.Code, item.PropertyId, item.Floor, item.AreaM2 })
            .SingleAsync(cancellationToken);

        var property = await db.Properties.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == room.PropertyId)
            .Select(item => new { item.Name, item.Address, item.OwnerId })
            .SingleAsync(cancellationToken);

        var ownerName = await db.Users.AsNoTracking()
            .Where(user => user.Id == property.OwnerId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var members = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on member.TenantId equals tenant.Id
            where member.ContractId == contract.ContractId
            orderby member.IsRepresentative descending, member.TenantId
            select new SnapshotMember(
                member.TenantId,
                tenant.FullName,
                tenant.Phone,
                member.Relationship,
                member.IsRepresentative))
            .ToListAsync(cancellationToken);

        var services = await ContractServiceLineLoader.LoadAsync(
            db,
            room.PropertyId,
            contract.ContractId,
            DateOnly.FromDateTime(contract.StartDate),
            cancellationToken);

        var snapshot = new Snapshot(
            ContractNumber: contract.ContractNumber,
            PropertyName: property.Name,
            PropertyAddress: property.Address,
            RoomCode: room.Code,
            Floor: room.Floor,
            AreaM2: room.AreaM2,
            StartDate: contract.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate: contract.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            MonthlyRent: contract.SnapshotPrice,
            DepositAmount: contract.DepositAmount,
            PaymentCycleMonths: contract.PaymentCycleMonths,
            MeterReadingDay: contract.MeterReadingDay,
            Terms: contract.Terms,
            OwnerName: ownerName,
            Members: members,
            Services: services
                .OrderBy(service => service.ServiceCatalogId)
                .Select(service => new SnapshotService(
                    service.ServiceCatalogId,
                    service.Name,
                    service.Unit,
                    service.CalculationMethod,
                    service.UnitPrice))
                .ToList());

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        return new SnapshotResult(json, ComputeHash(json));
    }

    public static string ComputeHash(string content) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static async Task<ContractVersion> CreateVersionAsync(
        ApplicationDbContext db,
        Contract contract,
        string createdByUserId,
        DateTimeOffset now,
        bool sent,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildSnapshotAsync(db, contract, cancellationToken);
        var nextVersion = await db.ContractVersions
            .Where(version => version.ContractId == contract.ContractId)
            .Select(version => (int?)version.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

        var version = new ContractVersion
        {
            ContractId = contract.ContractId,
            VersionNo = nextVersion + 1,
            SnapshotJson = snapshot.Json,
            ContentHash = snapshot.Hash,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            SentAt = sent ? now : null,
        };
        db.ContractVersions.Add(version);
        return version;
    }

    public sealed record Snapshot(
        string ContractNumber,
        string PropertyName,
        string? PropertyAddress,
        string RoomCode,
        int? Floor,
        decimal? AreaM2,
        string StartDate,
        string EndDate,
        decimal MonthlyRent,
        decimal DepositAmount,
        int PaymentCycleMonths,
        int? MeterReadingDay,
        string? Terms,
        string OwnerName,
        IReadOnlyList<SnapshotMember> Members,
        IReadOnlyList<SnapshotService> Services);

    public sealed record SnapshotMember(
        int TenantId,
        string FullName,
        string Phone,
        string? Relationship,
        bool IsRepresentative);

    public sealed record SnapshotService(
        int ServiceCatalogId,
        string Name,
        string Unit,
        string CalculationMethod,
        decimal UnitPrice);
}
