using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Web.Features.Accounts;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Services;

namespace NhaTro.UnitTests.Features.Tenants;

public class TenantDocumentPageServiceTests
{
    [Fact]
    public void MaxUploadFileSizeBytes_Chuan_10MB()
    {
        Assert.Equal(10L * 1024 * 1024, TenantDocumentPageService.MaxUploadFileSizeBytes);
    }

    [Fact]
    public void AllowedExtensions_Chi_nhan_pdf_jpg_jpeg_png()
    {
        var expected = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        Assert.Equal(expected, TenantDocumentPageService.AllowedExtensions);
    }

    [Theory]
    [InlineData(".docx", false)]
    [InlineData(".exe", false)]
    [InlineData(".zip", false)]
    [InlineData(".pdf", true)]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".png", true)]
    public void Kiem_tra_dinh_dang_duoi_tep(string ext, bool expected)
    {
        var isAllowed = TenantDocumentPageService.AllowedExtensions.Contains(ext.ToLowerInvariant());
        Assert.Equal(expected, isAllowed);
    }

    [Fact]
    public async Task Upload_Tu_choi_khi_dung_luong_vuot_10MB()
    {
        var service = CreateServiceWithRole(Roles.KhachThue, "tenant-1");
        var fakeFile = new FakeBrowserFile("test.pdf", TenantDocumentPageService.MaxUploadFileSizeBytes + 1);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.False(result.Succeeded);
        Assert.Equal(StorageErrorCodes.FileSizeExceeded, result.ErrorCode);
    }

    [Fact]
    public async Task Upload_Tu_choi_khi_file_rong()
    {
        var service = CreateServiceWithRole(Roles.KhachThue, "tenant-1");
        var fakeFile = new FakeBrowserFile("test.pdf", 0);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.False(result.Succeeded);
        Assert.Equal(StorageErrorCodes.EmptyFile, result.ErrorCode);
    }

    [Fact]
    public async Task Upload_Bi_chan_khi_nguoi_dung_la_QuanLy()
    {
        var service = CreateServiceWithRole(Roles.QuanLy, "manager-1");
        var fakeFile = new FakeBrowserFile("test.pdf", 1024 * 1024);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.False(result.Succeeded);
        Assert.Equal(StorageErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task Upload_Thanh_cong_khi_hop_le_va_dung_quyen()
    {
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), null!);
        var fakeFile = new FakeBrowserFile("test.pdf", 1024);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Upload_Tu_choi_khi_dinh_dang_khong_hop_le()
    {
        var service = CreateServiceWithRole(Roles.KhachThue, "tenant-1");
        var fakeFile = new FakeBrowserFile("document.docx", 1024);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.False(result.Succeeded);
        Assert.Equal(StorageErrorCodes.InvalidFileType, result.ErrorCode);
    }

    private static TenantDocumentPageService CreateServiceWithRole(string role, string userId)
    {
        var authStateProvider = new FakeAuthStateProvider(role, userId);
        return new TenantDocumentPageService(authStateProvider, new PermissionAwareDocumentService(), null!);
    }

    private sealed class PermissionAwareDocumentService : ITenantDocumentService
    {
        public Task<TenantDocumentListDto?> ListAsync(int tenantId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantDocumentListDto?>(null);

        public Task<UploadResult> UploadAsync(int tenantId, UploadFileRequest request, TenantCaller caller, CancellationToken cancellationToken = default)
        {
            var access = caller.Role switch
            {
                Roles.KhachThue => TenantDocumentAccessLevel.Own,
                Roles.ChuTro => TenantDocumentAccessLevel.Owner,
                Roles.QuanLy => TenantDocumentAccessLevel.Manager,
                _ => TenantDocumentAccessLevel.None
            };

            var result = TenantDocumentPermissionRules.CanUpload(access)
                ? UploadResult.Failure(StorageErrorCodes.UploadFailed, "Fake service does not persist files.")
                : UploadResult.Failure(StorageErrorCodes.Forbidden, "Upload is not permitted for this role.");

            return Task.FromResult(result);
        }

        public Task<UploadResult> ReplaceAsync(int tenantId, Guid fileId, UploadFileRequest request, TenantCaller caller, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileDownloadResponse?> DownloadAsync(int tenantId, Guid fileId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(int tenantId, Guid fileId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTenantDocumentService : ITenantDocumentService
    {
        public Task<TenantDocumentListDto?> ListAsync(int tenantId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantDocumentListDto?>(null);

        public Task<UploadResult> UploadAsync(int tenantId, UploadFileRequest request, TenantCaller caller, CancellationToken cancellationToken = default) =>
            Task.FromResult(UploadResult.Success(new FileMetadataDto
            {
                Id = Guid.NewGuid(),
                FileName = request.File.FileName,
                ContentType = request.File.ContentType,
                FileSizeBytes = request.File.Length,
                FileCategory = request.FileCategory,
                UploadedByUserId = caller.UserId,
                UploadedAt = DateTimeOffset.UtcNow,
                RelatedEntityId = request.RelatedEntityId,
                RelatedEntityType = request.RelatedEntityType
            }));

        public Task<UploadResult> ReplaceAsync(int tenantId, Guid fileId, UploadFileRequest request, TenantCaller caller, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileDownloadResponse?> DownloadAsync(int tenantId, Guid fileId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(int tenantId, Guid fileId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public FakeAuthStateProvider(string role, string userId)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            ], "TestAuth");

            _state = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }

    private sealed class FakeBrowserFile : IBrowserFile
    {
        public FakeBrowserFile(string name, long size)
        {
            Name = name;
            Size = size;
        }

        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size { get; }
        public string ContentType => "application/octet-stream";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream();
    }
}
