using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using NhaTro.IntegrationTests.TestSupport;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Infrastructure.Storage;

namespace NhaTro.IntegrationTests.Features.Storage;

/// <summary>
/// VHT-26: tải giấy tờ đúng hồ sơ, chặn ngoài phạm vi dù biết id, và audit không chứa nội dung.
/// Chạy trên SQL Server thật; tệp dùng bộ nhớ để không phụ thuộc ổ đĩa.
/// </summary>
public sealed class FileServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "chu-tro-1", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "ql-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "khach-2", Roles.KhachThue);

        var owner = new TenantCaller("chu-tro-1", Roles.ChuTro);
        var tenantService = new TenantService(
            fixture, TimeProvider.System, new AllowAllManagerTogglePolicy());
        var created = await tenantService.CreateAsync(new CreateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0901234567",
        }, owner);
        Assert.True(created.Succeeded, created.ErrorMessage);
        var tenantId = created.Tenant!.Id;

        var linked = await tenantService.LinkAccountAsync(
            tenantId, "khach-1", created.Tenant.RowVersion, owner);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        var factory = new TestDbContextFactory(fixture);
        var storage = new MemoryFileStorage();
        var options = Options.Create(new StorageOptions { RootPath = "memory" });
        var audit = new FileAccessAuditWriter(
            factory, TimeProvider.System, NullLogger<FileAccessAuditWriter>.Instance);

        var scope = new TenantDocumentScopeService(db, new NoManagerTenantScope());
        var service = NewFileService(factory, storage, options, scope, audit);

        var manager = new StorageCaller("chu-tro-1", Roles.ChuTro);
        var ownerTenant = new StorageCaller("khach-1", Roles.KhachThue);
        var stranger = new StorageCaller("khach-2", Roles.KhachThue);
        var quanLy = new StorageCaller("ql-1", Roles.QuanLy);
        var admin = new StorageCaller("admin-1", Roles.Admin);

        var upload = await service.UploadAsync(NewRequest(tenantId), manager);
        Assert.True(upload.Succeeded, upload.ErrorMessage);
        var fileId = upload.File!.Id;

        // Chủ trọ và chính khách thuê tải được.
        await AssertDownloadedAsync(service, fileId, manager);
        await AssertDownloadedAsync(service, fileId, ownerTenant);

        // Khách khác, Quản lý chưa được giao tòa nhà: 404 dù biết id.
        Assert.Null(await service.DownloadAsync(fileId, stranger));
        Assert.Null(await service.DownloadAsync(fileId, quanLy));

        // Admin bị chặn ở service (endpoint còn chặn ở policy).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DownloadAsync(fileId, admin));

        // Khi VHT-24 giao tòa nhà, Quản lý tải được; danh sách cũng đúng phạm vi.
        var managerScope = new TenantDocumentScopeService(db, new AllowManagerScope());
        var managerService = NewFileService(factory, storage, options, managerScope, audit);
        await AssertDownloadedAsync(managerService, fileId, quanLy);

        Assert.Single(await service.ListAsync(
            RelatedEntityType.Tenant, tenantId.ToString(), ownerTenant));
        Assert.Empty(await service.ListAsync(
            RelatedEntityType.Tenant, tenantId.ToString(), stranger));
        Assert.Empty(await service.ListAsync(
            RelatedEntityType.Tenant, tenantId.ToString(), quanLy));

        // Khách thuê không được xóa; Chủ trọ xóa mềm được.
        Assert.False(await service.DeleteAsync(fileId, ownerTenant));
        Assert.True(await service.DeleteAsync(fileId, manager));
        Assert.Null(await service.DownloadAsync(fileId, manager));

        // Audit ghi cả thành công và bị chặn; không có trường nội dung/tên tệp.
        await using var checkDb = fixture.CreateContext();
        var logs = await checkDb.FileAccessAuditLogs.AsNoTracking().ToListAsync();

        Assert.Contains(logs, l => l.Action == FileAccessAction.Upload && l.Succeeded);
        Assert.Contains(logs, l => l.Action == FileAccessAction.Download
            && l.Succeeded && l.ActorUserId == "khach-1");
        Assert.Contains(logs, l => l.Action == FileAccessAction.Download
            && !l.Succeeded && l.ActorUserId == "khach-2");
        Assert.Contains(logs, l => l.Action == FileAccessAction.Download
            && !l.Succeeded && l.ActorUserId == "ql-1");
        Assert.Contains(logs, l => l.Action == FileAccessAction.Download
            && l.Succeeded && l.ActorUserId == "ql-1");
        Assert.Contains(logs, l => l.Action == FileAccessAction.Delete
            && l.Succeeded && l.ActorUserId == "chu-tro-1");

        // Không dòng audit nào chứa tên tệp hoặc đường dẫn nội bộ.
        Assert.DoesNotContain(logs, l =>
            (l.RelatedEntityId ?? string.Empty).Contains("cccd", StringComparison.OrdinalIgnoreCase)
            || (l.ErrorCode ?? string.Empty).Contains("cccd", StringComparison.OrdinalIgnoreCase));
    }

    [SqlServerFact]
    public async Task Service_P3_gan_metadata_thay_va_xoa_dung_ho_so()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "chu-tro-1", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "ql-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "khach-2", Roles.KhachThue);

        var owner = new TenantCaller("chu-tro-1", Roles.ChuTro);
        var tenantService = new TenantService(
            fixture, TimeProvider.System, new AllowAllManagerTogglePolicy());
        var created = await tenantService.CreateAsync(new CreateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0901234567",
        }, owner);
        Assert.True(created.Succeeded, created.ErrorMessage);
        var tenantId = created.Tenant!.Id;
        var linked = await tenantService.LinkAccountAsync(
            tenantId, "khach-1", created.Tenant.RowVersion, owner);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        var factory = new TestDbContextFactory(fixture);
        var storage = new MemoryFileStorage();
        var options = Options.Create(new StorageOptions { RootPath = "memory" });
        var audit = new FileAccessAuditWriter(
            factory, TimeProvider.System, NullLogger<FileAccessAuditWriter>.Instance);
        var scope = new TenantDocumentScopeService(db, new NoManagerTenantScope());
        var fileService = NewFileService(factory, storage, options, scope, audit);
        var service = new TenantDocumentService(
            db, fileService, scope, options, audit, NullLogger<TenantDocumentService>.Instance);

        // Không cho client đổi relatedEntityId sang hồ sơ khác.
        var crossProfile = await service.UploadAsync(
            tenantId,
            NewRequest(tenantId + 1),
            new TenantCaller("khach-1", Roles.KhachThue));
        Assert.False(crossProfile.Succeeded);
        Assert.Equal(StorageErrorCodes.InvalidRequest, crossProfile.ErrorCode);

        // relatedEntityId rỗng được service P3 tự gắn đúng hồ sơ.
        var request = NewRequest(tenantId);
        request = new UploadFileRequest
        {
            File = request.File,
            FileCategory = request.FileCategory,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = string.Empty,
            Description = request.Description,
        };
        var uploaded = await service.UploadAsync(
            tenantId, request, new TenantCaller("khach-1", Roles.KhachThue));
        Assert.True(uploaded.Succeeded, uploaded.ErrorMessage);
        Assert.Equal(tenantId.ToString(), uploaded.File!.RelatedEntityId);

        // Danh sách metadata đúng phạm vi và cờ D9.
        var ownList = await service.ListAsync(tenantId, new TenantCaller("khach-1", Roles.KhachThue));
        Assert.NotNull(ownList);
        Assert.Single(ownList.Items);
        Assert.True(ownList.CanExport);
        Assert.False(ownList.CanDelete);

        Assert.Null(await service.ListAsync(tenantId, new TenantCaller("khach-2", Roles.KhachThue)));
        Assert.Null(await service.ListAsync(tenantId, new TenantCaller("ql-1", Roles.QuanLy)));

        var managerScope = new TenantDocumentScopeService(db, new AllowManagerScope());
        var managerFileService = NewFileService(factory, storage, options, managerScope, audit);
        var managerService = new TenantDocumentService(
            db, managerFileService, managerScope, options, audit, NullLogger<TenantDocumentService>.Instance);
        var managerList = await managerService.ListAsync(tenantId, new TenantCaller("ql-1", Roles.QuanLy));
        Assert.NotNull(managerList);
        Assert.Single(managerList.Items);
        Assert.False(managerList.CanExport);

        // Thay tệp: tệp cũ bị xóa mềm, tệp mới còn hiện hành.
        var oldId = uploaded.File.Id;
        var replaced = await managerService.ReplaceAsync(
            tenantId, oldId, NewRequest(tenantId), new TenantCaller("ql-1", Roles.QuanLy));
        Assert.True(replaced.Succeeded, replaced.ErrorMessage);
        Assert.NotEqual(oldId, replaced.File!.Id);
        Assert.Null(await managerService.DownloadAsync(tenantId, oldId, new TenantCaller("ql-1", Roles.QuanLy)));

        var afterReplace = await service.ListAsync(tenantId, new TenantCaller("khach-1", Roles.KhachThue));
        Assert.NotNull(afterReplace);
        Assert.Single(afterReplace.Items);
        Assert.Equal(replaced.File.Id, afterReplace.Items[0].Id);

        // Xóa mềm: danh sách rỗng, tệp không tải được nữa.
        Assert.True(await service.DeleteAsync(
            tenantId, replaced.File.Id, new TenantCaller("chu-tro-1", Roles.ChuTro)));
        var afterDelete = await service.ListAsync(tenantId, new TenantCaller("chu-tro-1", Roles.ChuTro));
        Assert.NotNull(afterDelete);
        Assert.Empty(afterDelete.Items);
    }

    private static FileService NewFileService(
        IDbContextFactory<ApplicationDbContext> factory,
        IFileStorage storage,
        IOptions<StorageOptions> options,
        ITenantDocumentScopeService scope,
        IFileAccessAuditWriter audit) =>
        new(
            factory,
            storage,
            options,
            TimeProvider.System,
            scope,
            audit,
            NullLogger<FileService>.Instance);

    private static UploadFileRequest NewRequest(int tenantId) => new()
    {
        File = new FormFile(
            new MemoryStream("%PDF-1.4\nVHT-26 test"u8.ToArray()),
            0,
            "%PDF-1.4\nVHT-26 test"u8.Length,
            "file",
            "cccd.pdf"),
        FileCategory = FileCategory.IdentityDocument,
        RelatedEntityType = RelatedEntityType.Tenant,
        RelatedEntityId = tenantId.ToString(),
        Description = "CCCD",
    };

    private static async Task AssertDownloadedAsync(
        FileService service,
        Guid fileId,
        StorageCaller caller)
    {
        var response = await service.DownloadAsync(fileId, caller);
        Assert.NotNull(response);
        await using var stream = response.FileStream;
        Assert.True(stream.CanRead);
    }

    private sealed class TestDbContextFactory(SqlServerFixture fixture)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => fixture.CreateContext();

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
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

        public Stream? TryOpenRead(string relativePath) =>
            _files.TryGetValue(relativePath, out var content)
                ? new MemoryStream(content, writable: false)
                : null;

        public bool Exists(string relativePath) => _files.ContainsKey(relativePath);

        public void Delete(string relativePath) => _files.Remove(relativePath);
    }

    private sealed class AllowManagerScope : IManagerTenantScope
    {
        public Task<bool> IsTenantInScopeAsync(
            string managerUserId,
            int tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
