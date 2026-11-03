using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using NhaTro.IntegrationTests.TestSupport;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.Services;
using Web.Features.Maintenance.DTOs;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Settings.Enums;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Maintenance.Models;
using Web.Features.Maintenance;
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
        var incidentScope = new IncidentFileScope(factory, TimeProvider.System);
        var toggle = new PhotoTogglePolicy();
        var approvals = new ManagerApprovalService(db, TimeProvider.System,
            [new IncidentCreateApprovalApplier(TimeProvider.System), new IncidentResultPhotoApprovalApplier(incidentScope)],
            NullLogger<ManagerApprovalService>.Instance);
        var photoService = new IncidentPhotoService(factory, memoryStorage, incidentScope, toggle, approvals, options,
            TimeProvider.System, audit, NullLogger<IncidentPhotoService>.Instance);
        var service = new FileService(factory, memoryStorage, options, TimeProvider.System, tenantScope,
            incidentScope, audit, NullLogger<FileService>.Instance, photoService);
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

        // Gọi thẳng storage không vượt được SW-14 hoặc giả nhóm Report của phiếu người khác.
        toggle.Decision = RoomChangeDecision.Denied;
        Assert.Equal(StorageErrorCodes.Forbidden, (await service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result), manager)).ErrorCode);
        Assert.Equal(StorageErrorCodes.Forbidden, (await service.UploadAsync(PhotoRequest("7"), manager)).ErrorCode);
        Assert.Equal(StorageErrorCodes.Forbidden, (await service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result), tenantCaller)).ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(PhotoRequest("7"), new StorageCaller(ownerId, Roles.Admin)));

        // Hai ảnh cùng một đề xuất: không thể duyệt giữa chừng hoặc lộ ảnh chờ cho khách.
        toggle.Decision = RoomChangeDecision.RequiresApproval;
        var pending = await service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result, expected: 2), manager);
        Assert.True(pending.Succeeded, pending.ErrorMessage);
        var pendingId = pending.ApprovalRequestId!.Value;
        Assert.Null(await service.DownloadAsync(pending.File!.Id, tenantCaller));
        Assert.NotNull(await service.DownloadAsync(pending.File.Id, owner));
        Assert.False(await service.DeleteAsync(pending.File.Id, owner));
        var incomplete = await approvals.ApproveAsync(pendingId, new(ownerId, Roles.ChuTro));
        Assert.Equal("ApprovalPhotosIncomplete", incomplete.ErrorCode);
        var secondResult = await service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result, pendingId, 2), manager);
        Assert.True(secondResult.Succeeded, secondResult.ErrorMessage);
        Assert.True((await approvals.ApproveAsync(pendingId, new(ownerId, Roles.ChuTro))).Succeeded);
        Assert.NotNull(await service.DownloadAsync(pending.File.Id, tenantCaller));
        Assert.False((await approvals.ApproveAsync(pendingId, new(ownerId, Roles.ChuTro))).Succeeded);

        // Ảnh bị từ chối/hủy không còn tải được và không chiếm quota.
        var rejected = await service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result), manager);
        Assert.True(rejected.Succeeded, rejected.ErrorMessage);
        Assert.True((await approvals.RejectAsync(rejected.ApprovalRequestId!.Value, new() { Reason = "Ảnh không phù hợp" }, new(ownerId, Roles.ChuTro))).Succeeded);
        Assert.Null(await service.DownloadAsync(rejected.File!.Id, manager));
        var cancelled = await service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result), manager);
        Assert.True(cancelled.Succeeded, cancelled.ErrorMessage);
        Assert.True((await approvals.CancelAsync(cancelled.ApprovalRequestId!.Value, new(managerId, Roles.QuanLy))).Succeeded);
        Assert.Null(await service.DownloadAsync(cancelled.File!.Id, owner));

        // Còn đúng một slot kết quả: upload đồng thời chỉ một lần thắng; nhóm báo độc lập.
        var racing = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => service.UploadAsync(PhotoRequest("7", IncidentPhotoKind.Result), owner)));
        Assert.Single(racing.Where(r => r.Succeeded));
        Assert.All(racing.Where(r => !r.Succeeded), r => Assert.Equal(StorageErrorCodes.FileCountExceeded, r.ErrorCode));
        Assert.True((await service.UploadAsync(PhotoRequest("7"), owner)).Succeeded);
        Assert.Equal(6, (await service.ListAsync(RelatedEntityType.MaintenanceIncident, "7", tenantCaller)).Count);

        // SW-12 giữ ảnh ngoài phiếu cho tới khi đủ ảnh và duyệt; sau duyệt ảnh nằm đúng phiếu SQL.
        var incidents = new IncidentService(factory, TimeProvider.System, toggle, approvals);
        var creation = await incidents.CreateAsync(new IncidentCreateRequest
        {
            RoomId = ownerRoom.Id, Title = "Sự cố cần kiểm tra", Description = "Thông tin sự cố và ảnh để kiểm tra.",
            ExpectedPhotoCount = 1
        }, new PropertyCaller(managerId, Roles.QuanLy));
        Assert.True(creation.Succeeded);
        Assert.Null(creation.Incident);
        var creationId = creation.ApprovalRequest!.Id;
        Assert.Equal("ApprovalPhotosIncomplete", (await approvals.ApproveAsync(creationId, new(ownerId, Roles.ChuTro))).ErrorCode);
        var creationPhoto = await service.UploadAsync(PhotoRequest(creationId.ToString("D"), related: RelatedEntityType.IncidentApproval), manager);
        Assert.True(creationPhoto.Succeeded, creationPhoto.ErrorMessage);
        Assert.Null(await service.DownloadAsync(creationPhoto.File!.Id, tenantCaller));
        Assert.NotNull(await service.DownloadAsync(creationPhoto.File.Id, owner));
        Assert.True((await approvals.ApproveAsync(creationId, new(ownerId, Roles.ChuTro))).Succeeded);
        Assert.NotNull(await service.DownloadAsync(creationPhoto.File.Id, tenantCaller));
        await using var verified = fixture.CreateContext();
        var createdIncidentId = await verified.IncidentPhotoAttachments.Where(p => p.FileId == creationPhoto.File.Id).Select(p => p.IncidentId).SingleAsync();
        Assert.NotNull(createdIncidentId);
        Assert.Contains(await service.ListAsync(RelatedEntityType.MaintenanceIncident, createdIncidentId.Value.ToString(), tenantCaller), f => f.Id == creationPhoto.File.Id);
        Assert.True((await incidents.GetAsync(createdIncidentId.Value, new(tenantId, Roles.KhachThue))).Succeeded);

        // Lỗi ghi private storage giữ request ID để retry, không tạo đề xuất trùng.
        memoryStorage.FailNextWrite = true;
        var failedPhoto = await service.UploadAsync(PhotoRequest(createdIncidentId.Value.ToString(), IncidentPhotoKind.Result), manager);
        Assert.Equal(StorageErrorCodes.UploadFailed, failedPhoto.ErrorCode);
        Assert.NotNull(failedPhoto.ApprovalRequestId);
        var retriedPhoto = await service.UploadAsync(PhotoRequest(createdIncidentId.Value.ToString(), IncidentPhotoKind.Result, failedPhoto.ApprovalRequestId), manager);
        Assert.True(retriedPhoto.Succeeded, retriedPhoto.ErrorMessage);
        Assert.Equal(failedPhoto.ApprovalRequestId, retriedPhoto.ApprovalRequestId);
        Assert.True((await approvals.ApproveAsync(retriedPhoto.ApprovalRequestId!.Value, new(ownerId, Roles.ChuTro))).Succeeded);

        toggle.Decision = RoomChangeDecision.Allowed;
        var allowedPhoto = await service.UploadAsync(PhotoRequest(createdIncidentId.Value.ToString(), IncidentPhotoKind.Result), manager);
        Assert.True(allowedPhoto.Succeeded, allowedPhoto.ErrorMessage);
        Assert.Null(allowedPhoto.ApprovalRequestId);
        Assert.NotNull(await service.DownloadAsync(allowedPhoto.File!.Id, tenantCaller));

        // Adapter form retry ảnh sau khi đã lưu phiếu: chỉ có một phiếu SQL cho cả hai lần gửi.
        var page = new IncidentPageService(new TestAuthentication(tenantId, Roles.KhachThue), incidents, photoService, toggle, approvals);
        var form = new IncidentForm
        {
            PropertyId = ownerProperty.Id, RoomId = ownerRoom.Id, Title = "Vòi nước cần thay",
            Description = "Vòi nước bị rò rỉ và cần được thay thế.",
            Photos = [new("report.png", Convert.FromHexString("89504E470D0A1A0A00000000"))]
        };
        var countBefore = await verified.MaintenanceIncidents.CountAsync();
        memoryStorage.FailNextWrite = true;
        await Assert.ThrowsAsync<IncidentPageException>(() => page.CreateAsync(form));
        Assert.NotNull(form.SavedIncidentId);
        Assert.Single(form.Photos);
        Assert.False(await page.CreateAsync(form));
        Assert.Empty(form.Photos);
        Assert.Equal(countBefore + 1, await verified.MaintenanceIncidents.CountAsync());
        Assert.Single(await page.PhotosAsync(form.SavedIncidentId, null));
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

    private static UploadFileRequest PhotoRequest(string relatedId, IncidentPhotoKind kind = IncidentPhotoKind.Report,
        Guid? approvalId = null, int expected = 1, RelatedEntityType related = RelatedEntityType.MaintenanceIncident)
    {
        var bytes = Convert.FromHexString("89504E470D0A1A0A00000000");
        return new UploadFileRequest
        {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "incident.png"),
            FileCategory = FileCategory.IncidentPhoto,
            RelatedEntityType = related,
            RelatedEntityId = relatedId,
            IncidentPhotoKind = kind, ApprovalRequestId = approvalId, ExpectedPhotoCount = expected,
        };
    }

    private sealed class TestDbContextFactory(SqlServerFixture fixture)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => fixture.CreateContext();

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class PhotoTogglePolicy : IManagerTogglePolicy
    {
        public RoomChangeDecision Decision { get; set; } = RoomChangeDecision.Allowed;
        public Task<RoomChangeDecision> EvaluateAsync(ManagerAction action, string role, CancellationToken ct = default) => Task.FromResult(Decision);
        public Task<RoomChangeDecision> EvaluateAsync(string code, string role, CancellationToken ct = default) => Task.FromResult(Decision);
    }

    private sealed class TestAuthentication(string userId, string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Role, role)], "test"))));
    }

    private sealed class MemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        public bool FailNextWrite { get; set; }

        public Task<StoredFileWriteResult> SaveAsync(
            ReadOnlyMemory<byte> content,
            string extension,
            DateTimeOffset uploadedAt,
            CancellationToken cancellationToken = default)
        {
            if (FailNextWrite) { FailNextWrite = false; throw new IOException("Simulated storage failure"); }
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
