using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Services;

namespace NhaTro.UnitTests.Features.Tenants;

public class TenantDocumentPageServiceTests
{
    [Theory]
    [InlineData("empty.pdf", 0L, StorageErrorCodes.EmptyFile)]
    [InlineData("document.docx", 1024L, StorageErrorCodes.InvalidFileType)]
    public async Task Upload_De_chuyen_validation_cho_service_va_tra_ErrorCode(
        string fileName, long size, string errorCode)
    {
        var backendResult = UploadResult.Failure(errorCode, "backend-error-message");
        var documentService = new FakeTenantDocumentService(backendResult);
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var service = new TenantDocumentPageService(authStateProvider, documentService, null!);
        var fakeFile = new FakeBrowserFile(fileName, size);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal("backend-error-message", result.ErrorMessage);
    }

    [Fact]
    public async Task Upload_Tu_choi_som_khi_tep_vuot_gioi_han_doc_cho_phep()
    {
        var documentService = new FakeTenantDocumentService(
            UploadResult.Failure(StorageErrorCodes.UploadFailed, "khong-duoc-goi"));
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var service = new TenantDocumentPageService(authStateProvider, documentService, null!);
        var fakeFile = new FakeBrowserFile("large.pdf", 10L * 1024 * 1024 + 1);

        var result = await service.UploadDocumentAsync(1, fakeFile, "CCCD", null);

        Assert.False(result.Succeeded);
        Assert.Equal(StorageErrorCodes.FileSizeExceeded, result.ErrorCode);
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
    public async Task GetMyLeasedRoom_Tra_ve_null_khi_TenantPageService_null()
    {
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), null!, null);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.Null(room);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Tra_ve_null_khi_khach_khong_co_hop_dong_hieu_luc()
    {
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [],
            CurrentRental = null
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), null!, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.Null(room);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Tra_ve_null_khi_tai_khoan_chua_duoc_gan_ho_so_khach_thue()
    {
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var fakeTenantService = new FakeRentalTenantService(null, TenantErrorCodes.TenantNotFound);
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        await using var db = CreateInMemoryDbContext();
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), db, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.Null(room);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Nem_loi_khi_backend_tra_failure_thay_vi_hien_thi_empty_state()
    {
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var fakeTenantService = new FakeRentalTenantService(null, TenantErrorCodes.TenantOperationFailed);
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), null!, pageService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetMyLeasedRoomAsync());
    }

    [Fact]
    public async Task GetMyLeasedRoom_Map_du_lieu_tien_coc_da_dong_thuc_te()
    {
        var contractId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 12, 31);
        var paidDate = new DateOnly(2026, 1, 2);
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");

        var currentRental = new TenantRentalContractDto
        {
            ContractId = contractId,
            ContractNumber = "HD-2026-001",
            RoomId = 101,
            RoomCode = "P.101",
            PropertyId = 1,
            PropertyName = "Nhà trọ Bình An",
            PropertyAddress = "123 Đường Số 1, Phường Thủ Đức, Thành phố Hồ Chí Minh",
            AreaM2 = 25.5m,
            StartDate = startDate,
            EndDate = endDate,
            SnapshotPrice = 3_500_000m,
            DepositAmount = 3_500_000m,
            PaidDepositAmount = 3_500_000m,
            DepositPaidDate = paidDate,
            Status = ContractStatus.Approved,
            IsCurrent = true,
            Roommates =
            [
                new TenantRoommateDto { TenantId = 1, FullName = "Nguyen Van A", PhoneNumber = "0901234567" },
                new TenantRoommateDto { TenantId = 2, FullName = "Tran Van B", PhoneNumber = "0909876543" }
            ]
        };

        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [currentRental],
            CurrentRental = currentRental
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        await using var db = CreateInMemoryDbContext();
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), db, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.NotNull(room);
        Assert.Equal(contractId, room.ContractId);
        Assert.Equal(3_500_000m, room.DepositAmount);
        Assert.Equal(3_500_000m, room.PaidDepositAmount);
        Assert.Equal(paidDate.ToDateTime(TimeOnly.MinValue), room.DepositPaidDate);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Tra_ve_null_cho_tien_coc_khi_chua_co_du_lieu_thanh_toan()
    {
        var contractId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 12, 31);
        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");

        var currentRental = new TenantRentalContractDto
        {
            ContractId = contractId,
            ContractNumber = "HD-2026-001",
            RoomId = 101,
            RoomCode = "P.101",
            PropertyId = 1,
            PropertyName = "Nhà trọ Bình An",
            PropertyAddress = "123 Đường Số 1, Phường Thủ Đức, Thành phố Hồ Chí Minh",
            AreaM2 = 25.5m,
            StartDate = startDate,
            EndDate = endDate,
            SnapshotPrice = 3_500_000m,
            DepositAmount = 3_500_000m,
            PaidDepositAmount = null,
            DepositPaidDate = null,
            Status = ContractStatus.Approved,
            IsCurrent = true,
            Roommates = []
        };

        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [currentRental],
            CurrentRental = currentRental
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        await using var db = CreateInMemoryDbContext();
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), db, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.NotNull(room);
        Assert.Null(room.PaidDepositAmount);
        Assert.Null(room.DepositPaidDate);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Doc_duoc_Floor_tu_Room_trong_database()
    {
        await using var db = CreateInMemoryDbContext();
        db.Rooms.Add(new Web.Features.Rooms.Entities.Room
        {
            Id = 101,
            PropertyId = 1,
            Code = "P.101",
            Floor = 3,
            AreaM2 = 25m,
            StandardPrice = 3_500_000m,
            Status = Web.Features.Rooms.Enums.RoomStatus.Rented
        });
        await db.SaveChangesAsync();

        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var currentRental = CreateSampleRental(101);
        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [currentRental],
            CurrentRental = currentRental
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), db, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.NotNull(room);
        Assert.Equal(3, room.Floor);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Tra_ve_Floor_null_khi_Room_co_Floor_null()
    {
        await using var db = CreateInMemoryDbContext();
        db.Rooms.Add(new Web.Features.Rooms.Entities.Room
        {
            Id = 101,
            PropertyId = 1,
            Code = "P.101",
            Floor = null,
            AreaM2 = 25m,
            StandardPrice = 3_500_000m,
            Status = Web.Features.Rooms.Enums.RoomStatus.Rented
        });
        await db.SaveChangesAsync();

        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var currentRental = CreateSampleRental(101);
        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [currentRental],
            CurrentRental = currentRental
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), db, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.NotNull(room);
        Assert.Null(room.Floor);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Tra_ve_Floor_null_khi_Room_khong_ton_tai()
    {
        await using var db = CreateInMemoryDbContext();

        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var currentRental = CreateSampleRental(999);
        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [currentRental],
            CurrentRental = currentRental
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        var service = new TenantDocumentPageService(authStateProvider, new FakeTenantDocumentService(), db, pageService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.NotNull(room);
        Assert.Null(room.Floor);
    }

    [Fact]
    public async Task GetMyLeasedRoom_Map_thong_tin_lien_he_chu_tro_va_quan_ly()
    {
        await using var db = CreateInMemoryDbContext();
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner-1",
                UserName = "owner",
                FullName = "Chu Tro A",
                PhoneNumber = "0901111111",
            },
            new ApplicationUser
            {
                Id = "manager-1",
                UserName = "manager",
                FullName = "Quan Ly B",
                PhoneNumber = "0902222222",
            });
        db.Properties.Add(new Property { Id = 1, Name = "Nhà trọ Bình An", OwnerId = "owner-1" });
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = 1,
            ManagerUserId = "manager-1",
            AssignedByUserId = "owner-1",
        });
        await db.SaveChangesAsync();

        var authStateProvider = new FakeAuthStateProvider(Roles.KhachThue, "tenant-1");
        var currentRental = CreateSampleRental(101);
        var fakeTenantService = new FakeRentalTenantService(new TenantRentalOverviewDto
        {
            TenantId = 1,
            FullName = "Nguyen Van A",
            Rentals = [currentRental],
            CurrentRental = currentRental
        });
        var pageService = new TenantPageService(authStateProvider, fakeTenantService, null!, null!);
        var contactService = new TenantContactPageService(
            authStateProvider, new TestDbContextFactory(db), TimeProvider.System);
        var service = new TenantDocumentPageService(
            authStateProvider, new FakeTenantDocumentService(), db, pageService, null, contactService);

        var room = await service.GetMyLeasedRoomAsync();

        Assert.NotNull(room);
        Assert.NotNull(room.Contact);
        Assert.Equal("Chu Tro A", room.Contact.Owner!.Name);
        Assert.Equal("0901111111", room.Contact.Owner.PhoneNumber);
        Assert.Single(room.Contact.Managers);
        Assert.Equal("Quan Ly B", room.Contact.Managers[0].Name);
        Assert.Equal("0902222222", room.Contact.Managers[0].PhoneNumber);
    }

    [Fact]
    public async Task GetMyContact_Tra_ve_null_khi_khong_phai_khach_thue()
    {
        await using var db = CreateInMemoryDbContext();
        var service = new TenantContactPageService(
            new FakeAuthStateProvider(Roles.QuanLy, "manager-1"),
            new TestDbContextFactory(db),
            TimeProvider.System);

        var contact = await service.GetMyContactAsync();

        Assert.Null(contact);
    }

    [Fact]
    public async Task GetMyContact_Tim_dung_toa_nha_va_chi_lay_quan_ly_dang_hieu_luc()
    {
        await using var db = CreateInMemoryDbContext();
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner-1",
                UserName = "owner",
                FullName = "Chu Tro A",
                PhoneNumber = "0901111111",
            },
            new ApplicationUser
            {
                Id = "manager-1",
                UserName = "manager1",
                FullName = "Quan Ly B",
                PhoneNumber = "0902222222",
            },
            new ApplicationUser
            {
                Id = "manager-2",
                UserName = "manager2",
                FullName = "Quan Ly C",
                PhoneNumber = "0903333333",
            });
        db.Properties.Add(new Property { Id = 7, Name = "Nhà trọ Bình An", OwnerId = "owner-1" });
        db.PropertyManagers.AddRange(
            new PropertyManager
            {
                PropertyId = 7,
                ManagerUserId = "manager-1",
                AssignedByUserId = "owner-1",
            },
            new PropertyManager
            {
                PropertyId = 7,
                ManagerUserId = "manager-2",
                AssignedByUserId = "owner-1",
                RevokedAt = DateTime.UtcNow,
            });
        db.Tenants.Add(new Web.Features.Tenants.Entities.Tenant
        {
            Id = 5,
            FullName = "Khach Thue",
            Phone = "0900000000",
            UserId = "tenant-1",
        });
        db.Rooms.Add(new Room { Id = 99, PropertyId = 7, Code = "P.99" });

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-2026-099",
            RoomId = 99,
            StartDate = DateTime.UtcNow.Date.AddDays(-5),
            EndDate = DateTime.UtcNow.Date.AddDays(5),
            SnapshotPrice = 3_500_000m,
            DepositAmount = 3_500_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = 5 });
        await db.SaveChangesAsync();

        var service = new TenantContactPageService(
            new FakeAuthStateProvider(Roles.KhachThue, "tenant-1"),
            new TestDbContextFactory(db),
            TimeProvider.System);

        var contact = await service.GetMyContactAsync();

        Assert.NotNull(contact);
        Assert.Equal("Chu Tro A", contact.Owner!.Name);
        Assert.Equal("0901111111", contact.Owner.PhoneNumber);
        Assert.Single(contact.Managers);
        Assert.Equal("Quan Ly B", contact.Managers[0].Name);
    }

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TenantRentalContractDto CreateSampleRental(int roomId) => new()
    {
        ContractId = Guid.NewGuid(),
        ContractNumber = "HD-2026-001",
        RoomId = roomId,
        RoomCode = $"P.{roomId}",
        PropertyId = 1,
        PropertyName = "Nhà trọ Bình An",
        PropertyAddress = "123 Đường Số 1",
        AreaM2 = 25.5m,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 12, 31),
        SnapshotPrice = 3_500_000m,
        DepositAmount = 3_500_000m,
        PaidDepositAmount = null,
        DepositPaidDate = null,
        Status = ContractStatus.Approved,
        IsCurrent = true,
        Roommates = []
    };

    private static TenantDocumentPageService CreateServiceWithRole(string role, string userId)
    {
        var authStateProvider = new FakeAuthStateProvider(role, userId);
        return new TenantDocumentPageService(authStateProvider, new PermissionAwareDocumentService(), null!);
    }

    private sealed class FakeRentalTenantService(TenantRentalOverviewDto? overview, string? failureCode = null) : ITenantService
    {
        public Task<TenantRentalResult> GetOwnRentalOverviewAsync(TenantCaller caller, CancellationToken ct = default) =>
            Task.FromResult(failureCode is not null
                ? TenantRentalResult.Failure(failureCode, "Backend failure")
                : overview is null
                    ? TenantRentalResult.Failure(TenantErrorCodes.TenantNotFound, TenantErrorMessages.TenantNotFound)
                    : TenantRentalResult.Success(overview));

        public Task<TenantRentalResult> GetRentalOverviewAsync(int id, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantListSummaryDto> GetSummaryAsync(TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Web.Features.Tenants.DTOs.PagedResult<TenantListItemDto>> ListAsync(
            TenantFilterRequest filter, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<List<TenantAccountOptionDto>> SearchUnlinkedAccountsAsync(
            string? keyword, int limit, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> GetAsync(int id, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> GetOwnAsync(TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> CreateAsync(CreateTenantRequest request, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> UpdateAsync(int id, UpdateTenantRequest request, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> UpdateOwnAsync(UpdateOwnProfileRequest request, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> SetActiveAsync(int id, bool isActive, string rowVersion, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> DeleteAsync(int id, string rowVersion, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> RestoreAsync(int id, string rowVersion, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Web.Features.Tenants.DTOs.PagedResult<TenantTrashItemDto>> ListTrashAsync(
            TenantTrashFilterRequest filter, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> PurgeAsync(
            int id, string rowVersion, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TrashPurgeSummary> PurgeAllAsync(
            TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> LinkAccountAsync(int id, string accountUserId, string rowVersion, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TenantResult> UnlinkAccountAsync(int id, string rowVersion, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<List<TenantPickerItemDto>> SearchForPickerAsync(TenantPickerRequest request, TenantCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();
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
        private readonly UploadResult? _uploadResult;

        public FakeTenantDocumentService(UploadResult? uploadResult = null)
        {
            _uploadResult = uploadResult;
        }

        public Task<TenantDocumentListDto?> ListAsync(int tenantId, TenantCaller caller, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantDocumentListDto?>(null);

        public Task<UploadResult> UploadAsync(int tenantId, UploadFileRequest request, TenantCaller caller, CancellationToken cancellationToken = default) =>
            Task.FromResult(_uploadResult ?? UploadResult.Success(new FileMetadataDto
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

    private sealed class TestDbContextFactory(ApplicationDbContext db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db;

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(db);
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
