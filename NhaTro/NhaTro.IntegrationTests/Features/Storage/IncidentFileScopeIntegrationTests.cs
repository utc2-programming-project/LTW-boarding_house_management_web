using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using NhaTro.IntegrationTests.TestSupport;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Services;
using Web.Features.Tenants.Documents;
using Web.Infrastructure.Storage;

namespace NhaTro.IntegrationTests.Features.Storage;

public sealed class IncidentFileScopeIntegrationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task IncidentPhotosAreScopedCanonicalizedLimitedAndDeletableByOwner()
    {
        await fixture.ResetAsync();
        const string ownerId = "incident-file-owner";
        const string otherOwnerId = "incident-file-other-owner";
        const string managerId = "incident-file-manager";
        const string otherManagerId = "incident-file-other-manager";
        const string tenantId = "incident-file-tenant";
        const string strangerId = "incident-file-stranger";
        int incidentId;

        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, ownerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, otherOwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, managerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, otherManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, tenantId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, strangerId, Roles.KhachThue);

        var ownerProperty = new Property { Name = "Owner property", OwnerId = ownerId };
        var otherProperty = new Property { Name = "Other property", OwnerId = otherOwnerId };
        db.Properties.AddRange(ownerProperty, otherProperty);
        await db.SaveChangesAsync();
        db.PropertyManagers.AddRange(
            new PropertyManager { PropertyId = ownerProperty.Id, ManagerUserId = managerId, AssignedByUserId = ownerId },
            new PropertyManager { PropertyId = otherProperty.Id, ManagerUserId = otherManagerId, AssignedByUserId = otherOwnerId });
        var ownerRoom = new Room { PropertyId = ownerProperty.Id, Code = "101", StandardPrice = 1_000_000m };
        var otherRoom = new Room { PropertyId = otherProperty.Id, Code = "201", StandardPrice = 1_000_000m };
        db.Rooms.AddRange(ownerRoom, otherRoom);
        await db.SaveChangesAsync();

        var tenant = new Web.Features.Tenants.Entities.Tenant
        {
            FullName = "Tenant One", Phone = "0901111111", UserId = tenantId,
        };
        var stranger = new Web.Features.Tenants.Entities.Tenant
        {
            FullName = "Tenant Two", Phone = "0902222222", UserId = strangerId,
        };
        db.Tenants.AddRange(tenant, stranger);
        await db.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;
        var tenantContract = NewApprovedContract(ownerRoom.Id, "incident-file-tenant-contract", today);
        var strangerContract = NewApprovedContract(otherRoom.Id, "incident-file-stranger-contract", today);
        db.Contracts.AddRange(tenantContract, strangerContract);
        db.ContractMembers.AddRange(
            new Web.Features.Contracts.Entities.ContractMember { ContractId = tenantContract.ContractId, TenantId = tenant.Id },
            new Web.Features.Contracts.Entities.ContractMember { ContractId = strangerContract.ContractId, TenantId = stranger.Id });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('MaintenanceIncidents', RESEED, 7)");
        db.MaintenanceIncidents.Add(new MaintenanceIncident
        {
            RoomId = ownerRoom.Id,
            ReportedByUserId = ownerId,
            Title = "Broken socket",
            Description = "The socket is damaged and needs repair.",
            Category = IncidentCategory.Electrical,
            Urgency = IncidentUrgency.Normal,
            Status = IncidentStatus.New,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        incidentId = await db.MaintenanceIncidents.Select(item => item.Id).SingleAsync();
        Assert.Equal(7, incidentId);

        var factory = new TestDbContextFactory(fixture);
        var memoryStorage = new MemoryFileStorage();
        var options = Options.Create(new StorageOptions
        {
            RootPath = "memory",
            MaxFilesPerEntity = new Dictionary<string, int?> { [nameof(FileCategory.IncidentPhoto)] = 3 },
        });
        var audit = new FileAccessAuditWriter(factory, TimeProvider.System,
            NullLogger<FileAccessAuditWriter>.Instance);
        var tenantScope = new TenantDocumentScopeService(db, new NoManagerTenantScope());
        var service = new FileService(factory, memoryStorage, options, TimeProvider.System, tenantScope,
            new IncidentFileScope(factory, TimeProvider.System), audit, NullLogger<FileService>.Instance);
        var owner = new StorageCaller(ownerId, Roles.ChuTro);
        var manager = new StorageCaller(managerId, Roles.QuanLy);
        var outOfScopeManager = new StorageCaller(otherManagerId, Roles.QuanLy);
        var tenantCaller = new StorageCaller(tenantId, Roles.KhachThue);
        var strangerCaller = new StorageCaller(strangerId, Roles.KhachThue);

        var first = await service.UploadAsync(PhotoRequest("007"), owner);
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal("7", first.File!.RelatedEntityId);
        var tenantUpload = await service.UploadAsync(PhotoRequest("7"), tenantCaller);
        Assert.True(tenantUpload.Succeeded, tenantUpload.ErrorMessage);
        Assert.Equal("7", tenantUpload.File!.RelatedEntityId);
        var third = await service.UploadAsync(PhotoRequest("007"), owner);
        Assert.True(third.Succeeded, third.ErrorMessage);
        var overLimit = await service.UploadAsync(PhotoRequest("7"), owner);
        Assert.Equal(StorageErrorCodes.FileCountExceeded, overLimit.ErrorCode);
        var deniedUpload = await service.UploadAsync(PhotoRequest("007"), strangerCaller);
        Assert.Equal(StorageErrorCodes.Forbidden, deniedUpload.ErrorCode);

        Assert.Equal(3, (await service.ListAsync(RelatedEntityType.MaintenanceIncident, "007", owner)).Count);
        Assert.Equal(3, (await service.ListAsync(RelatedEntityType.MaintenanceIncident, "7", tenantCaller)).Count);
        Assert.Empty(await service.ListAsync(RelatedEntityType.MaintenanceIncident, "7", strangerCaller));
        Assert.Empty(await service.ListAsync(RelatedEntityType.MaintenanceIncident, "7", outOfScopeManager));

        Assert.NotNull(await service.DownloadAsync(first.File.Id, owner));
        Assert.NotNull(await service.DownloadAsync(first.File.Id, manager));
        Assert.NotNull(await service.DownloadAsync(first.File.Id, tenantCaller));
        Assert.NotNull(await service.DownloadAsync(tenantUpload.File!.Id, tenantCaller));
        Assert.Null(await service.DownloadAsync(first.File.Id, strangerCaller));
        Assert.Null(await service.DownloadAsync(first.File.Id, outOfScopeManager));
        Assert.False(await service.DeleteAsync(first.File.Id, tenantCaller));
        Assert.False(await service.DeleteAsync(first.File.Id, strangerCaller));
        Assert.False(await service.DeleteAsync(first.File.Id, outOfScopeManager));
        Assert.True(await service.DeleteAsync(first.File.Id, owner));
        Assert.Null(await service.DownloadAsync(first.File.Id, owner));
        Assert.Equal(2, (await service.ListAsync(RelatedEntityType.MaintenanceIncident, "7", owner)).Count);
    }

    private static Contract NewApprovedContract(int roomId, string number, DateTime today) => new()
    {
        ContractNumber = number,
        RoomId = roomId,
        StartDate = today.AddDays(-1),
        EndDate = today.AddDays(1),
        SnapshotPrice = 1_000_000m,
        DepositAmount = 200_000m,
        Status = ContractStatus.Approved,
    };

    private static UploadFileRequest PhotoRequest(string relatedId)
    {
        var bytes = Convert.FromHexString("89504E470D0A1A0A00000000");
        return new UploadFileRequest
        {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "incident.png"),
            FileCategory = FileCategory.IncidentPhoto,
            RelatedEntityType = RelatedEntityType.MaintenanceIncident,
            RelatedEntityId = relatedId,
        };
    }

    private sealed class TestDbContextFactory(SqlServerFixture fixture)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => fixture.CreateContext();

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class MemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public Task<StoredFileWriteResult> SaveAsync(
            ReadOnlyMemory<byte> content,
            string extension,
            DateTimeOffset uploadedAt,
            CancellationToken cancellationToken = default)
        {
            var name = Guid.NewGuid().ToString("N") + extension;
            var path = $"memory/{name}";
            _files[path] = content.ToArray();
            return Task.FromResult(new StoredFileWriteResult(name, path, content.Length));
        }

        public Stream? TryOpenRead(string relativePath) => _files.TryGetValue(relativePath, out var content)
            ? new MemoryStream(content, writable: false)
            : null;

        public bool Exists(string relativePath) => _files.ContainsKey(relativePath);
        public void Delete(string relativePath) => _files.Remove(relativePath);
    }
}
