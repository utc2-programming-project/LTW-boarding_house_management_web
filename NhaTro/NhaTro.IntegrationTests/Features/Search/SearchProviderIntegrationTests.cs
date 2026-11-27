using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Common.Utils;
using Web.Data;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Providers;
using Web.Features.Search.Services;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.Entities;

namespace NhaTro.IntegrationTests.Features.Search;

/// <summary>
/// Chạy provider trên SQL Server thật để bắt lỗi dịch LINQ và kiểm phạm vi quyền (A01–A15).
/// </summary>
public sealed class SearchProviderIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private sealed record Seed(
        Guid ContractA,
        Guid ContractB,
        Guid DraftContract,
        int InvoiceA,
        int InvoiceB);

    [SqlServerFact]
    public async Task Admin_thay_phong_va_go_khong_dau_khop_phong_so()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var admin = new PropertyCaller("admin-a", Roles.Admin);
        var result = await SearchAsync(new RoomSearchProvider(fixture), admin, "phong a101");

        Assert.Contains(result.Hits, hit => hit.Title == "A101");
        Assert.Contains(result.Hits, hit => hit.Href.Contains("rooms", StringComparison.Ordinal));
    }

    [SqlServerFact]
    public async Task KhachThue_chit_thay_phong_cua_minh_va_link_dung_trang()
    {
        await fixture.ResetAsync();
        var seed = await SeedAsync();

        var tenant = new PropertyCaller("tenant-a", Roles.KhachThue);
        var result = await SearchAsync(new RoomSearchProvider(fixture), tenant, "a101");

        var hit = Assert.Single(result.Hits);
        Assert.Equal($"/my/room/{seed.ContractA:D}", hit.Href);

        // Hợp đồng nháp không được lộ ra.
        var draft = await SearchAsync(new RoomSearchProvider(fixture), tenant, "a102");
        Assert.Empty(draft.Hits);
    }

    [SqlServerFact]
    public async Task QuanLy_tim_hop_dong_theo_ten_khach_nhung_khong_thay_nha_bi_khoa()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var manager = LockedOwnerManager();
        var byName = await SearchAsync(new ContractSearchProvider(fixture), manager, "nguyen van an");
        var hit = Assert.Single(byName.Hits, item => item.Title == "HD-2026-001");
        Assert.Contains("A101", hit.Subtitle, StringComparison.Ordinal);
        Assert.DoesNotContain(byName.Hits, item => item.Subtitle!.Contains("B201", StringComparison.Ordinal));

        var ofLockedOwner = await SearchAsync(new ContractSearchProvider(fixture), manager, "B201");
        Assert.Empty(ofLockedOwner.Hits);
    }

    [SqlServerFact]
    public async Task HoaDon_tim_theo_ma_co_gach_ten_khach_phong_va_loai_nha_khoa()
    {
        await fixture.ResetAsync();
        var seed = await SeedAsync();
        var manager = LockedOwnerManager();

        var byUiCode = await SearchAsync(new InvoiceSearchProvider(fixture), manager, $"HD-{seed.InvoiceA}");
        var hit = Assert.Single(byUiCode.Hits);
        Assert.Equal($"HD-{seed.InvoiceA}", hit.Title);
        Assert.Contains("A101", hit.Subtitle, StringComparison.Ordinal);

        Assert.NotEmpty((await SearchAsync(new InvoiceSearchProvider(fixture), manager, "nguyen van an")).Hits);
        Assert.NotEmpty((await SearchAsync(new InvoiceSearchProvider(fixture), manager, "a101")).Hits);
        Assert.Empty((await SearchAsync(new InvoiceSearchProvider(fixture), manager, "b201")).Hits);
    }

    [SqlServerFact]
    public async Task Su_co_chi_tra_hop_dong_dang_hieu_luc_cho_khach_thue()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var tenant = new PropertyCaller("tenant-a", Roles.KhachThue);
        var active = await SearchAsync(new IncidentSearchProvider(fixture, TimeProvider.System), tenant, "voi nuoc");
        Assert.Single(active.Hits);

        var draft = await SearchAsync(new IncidentSearchProvider(fixture, TimeProvider.System), tenant, "su co nhap");
        Assert.Empty(draft.Hits);
    }

    [SqlServerFact]
    public async Task Phieu_ra_vao_va_tai_lieu_co_nhom_rieng()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var manager = LockedOwnerManager();
        var gate = await SearchAsync(new GateSearchProvider(fixture), manager, "tran binh");
        var gateHit = Assert.Single(gate.Hits);
        Assert.DoesNotContain("0912", gateHit.Subtitle, StringComparison.Ordinal);

        var tenant = new PropertyCaller("tenant-a", Roles.KhachThue);
        var documents = await SearchAsync(new DocumentSearchProvider(fixture), tenant, "cccd");
        var documentHit = Assert.Single(documents.Hits);
        Assert.Equal("/my/documents", documentHit.Href);
    }

    [SqlServerFact]
    public async Task Khach_thue_khong_lo_sdt_trong_phu_de()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var manager = LockedOwnerManager();
        var result = await SearchAsync(new TenantSearchProvider(fixture), manager, "nguyen van an");
        var hit = Assert.Single(result.Hits);
        Assert.Null(hit.Subtitle);
    }

    [SqlServerFact]
    public async Task ChuTro_chi_thay_phong_va_nha_cua_minh()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var owner = new PropertyCaller("owner-a", Roles.ChuTro);
        var rooms = await SearchAsync(new RoomSearchProvider(fixture), owner, "a1");
        Assert.Contains(rooms.Hits, hit => hit.Title == "A101");
        Assert.Contains(rooms.Hits, hit => hit.Title == "A102");
        Assert.DoesNotContain(rooms.Hits, hit => hit.Title == "B201");

        var properties = await SearchAsync(new PropertySearchProvider(fixture), owner, "nha tro");
        var property = Assert.Single(properties.Hits);
        Assert.Equal("Nha Tro Anh Duong", property.Title);
    }

    [SqlServerFact]
    public async Task Khach_thue_chi_thay_hop_dong_da_hieu_luc_cua_minh()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var tenant = new PropertyCaller("tenant-a", Roles.KhachThue);
        var active = await SearchAsync(new ContractSearchProvider(fixture), tenant, "HD-2026-001");
        Assert.Single(active.Hits);

        Assert.Empty((await SearchAsync(new ContractSearchProvider(fixture), tenant, "HD-2026-002")).Hits);
        // Hợp đồng nháp của chính mình cũng không được lộ.
        Assert.Empty((await SearchAsync(new ContractSearchProvider(fixture), tenant, "DRAFT")).Hits);
    }

    [SqlServerFact]
    public async Task Khach_thue_chi_tra_ho_so_chinh_minh()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var tenant = new PropertyCaller("tenant-a", Roles.KhachThue);
        var self = await SearchAsync(new TenantSearchProvider(fixture), tenant, "nguyen");
        var hit = Assert.Single(self.Hits);
        Assert.Equal("/tenant/profile", hit.Href);
        Assert.Null(hit.Subtitle);

        var admin = new PropertyCaller("admin-a", Roles.Admin);
        Assert.NotEmpty((await SearchAsync(new TenantSearchProvider(fixture), admin, "0901234567")).Hits);
    }

    [SqlServerFact]
    public async Task Dich_vu_loc_theo_nha_va_khop_ten_trong_lich_su_gia()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var owner = new PropertyCaller("owner-a", Roles.ChuTro);
        var hit = Assert.Single((await SearchAsync(new ServiceSearchProvider(fixture), owner, "dien")).Hits);
        Assert.Equal("Dien sinh hoat", hit.Title);

        var otherOwner = new PropertyCaller("owner-b", Roles.ChuTro);
        Assert.Empty((await SearchAsync(new ServiceSearchProvider(fixture), otherOwner, "dien")).Hits);
    }

    [SqlServerFact]
    public async Task Go_khong_dau_khop_duoc_ten_co_dau()
    {
        // Prefilter DB dùng collation Latin1_General_100_CI_AI: "duc anh" phải khớp "Đức Anh".
        await fixture.ResetAsync();
        await SeedAsync();

        var owner = new PropertyCaller("owner-a", Roles.ChuTro);
        var hit = Assert.Single((await SearchAsync(new TenantSearchProvider(fixture), owner, "duc anh")).Hits);
        Assert.Equal("Đức Anh", hit.Title);
    }

    [SqlServerFact]
    public async Task Nhom_chi_danh_cho_mot_vai_tro_tra_rong_o_vai_tro_khac()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var tenant = new PropertyCaller("tenant-a", Roles.KhachThue);
        Assert.Empty((await SearchAsync(new GateSearchProvider(fixture), tenant, "tran binh")).Hits);

        var owner = new PropertyCaller("owner-a", Roles.ChuTro);
        Assert.Empty((await SearchAsync(new DocumentSearchProvider(fixture), owner, "cccd")).Hits);
    }

    [SqlServerFact]
    public async Task HoaDon_nhan_kieu_go_cu_hd_va_tim_theo_ky()
    {
        await fixture.ResetAsync();
        var seed = await SeedAsync();

        var owner = new PropertyCaller("owner-a", Roles.ChuTro);
        var legacy = await SearchAsync(new InvoiceSearchProvider(fixture), owner, $"HD{seed.InvoiceA}");
        Assert.Single(legacy.Hits, hit => hit.Title == $"HD-{seed.InvoiceA}");

        Assert.NotEmpty((await SearchAsync(new InvoiceSearchProvider(fixture), owner, "11/2026")).Hits);
        Assert.NotEmpty((await SearchAsync(new InvoiceSearchProvider(fixture), owner, "2026-11")).Hits);
    }

    [SqlServerFact]
    public async Task Global_search_chay_song_song_va_loc_theo_nhom()
    {
        await fixture.ResetAsync();
        await SeedAsync();

        var admin = new PropertyCaller("admin-a", Roles.Admin);
        var service = new GlobalSearchService(
            AllProviders(), new FixedCallers(admin), NullLogger<GlobalSearchService>.Instance);

        var all = await service.SearchAsync(new SearchRequest("a101"), CancellationToken.None);
        Assert.Contains(all.Groups, group => group.GroupKey == "rooms");
        Assert.Contains(all.Groups, group => group.GroupKey == "contracts");
        Assert.Empty(all.FailedGroups ?? []);
        // Admin chạy mọi nhóm trừ "Tài liệu" (chỉ Khách thuê).
        Assert.Equal(8, all.ProviderCount);

        var roomsOnly = await service.SearchAsync(
            new SearchRequest("a101", Group: "rooms"), CancellationToken.None);
        var group = Assert.Single(roomsOnly.Groups);
        Assert.Equal("rooms", group.GroupKey);
        Assert.Equal("SearchGroupRooms", group.GroupLabelKey);
    }

    private sealed class FixedCallers(PropertyCaller caller) : ISearchCallerProvider
    {
        public Task<PropertyCaller> GetCallerAsync() => Task.FromResult(caller);
    }

    private ISearchProvider[] AllProviders() =>
    [
        new RoomSearchProvider(fixture),
        new PropertySearchProvider(fixture),
        new TenantSearchProvider(fixture),
        new ContractSearchProvider(fixture),
        new InvoiceSearchProvider(fixture),
        new IncidentSearchProvider(fixture, TimeProvider.System),
        new ServiceSearchProvider(fixture),
        new GateSearchProvider(fixture),
        new DocumentSearchProvider(fixture),
    ];

    private static PropertyCaller LockedOwnerManager() =>
        new("manager-a", Roles.QuanLy) { BlockedOwnerIds = ["owner-b"] };

    private static async Task<SearchGroupResult> SearchAsync(ISearchProvider provider, PropertyCaller caller, string query)
    {
        var context = new SearchContext(caller, query, TextSearchNormalizer.Normalize(query), 10, 0);
        return await provider.SearchAsync(context, CancellationToken.None);
    }

    private async Task<Seed> SeedAsync()
    {
        var today = DateTime.UtcNow.Date;
        var contractA = Guid.NewGuid();
        var contractB = Guid.NewGuid();
        var draftContract = Guid.NewGuid();

        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "owner-b", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-a", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "tenant-a", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "admin-a", Roles.Admin);

        var propertyA = new Property { Name = "Nha Tro Anh Duong", OwnerId = "owner-a" };
        var propertyB = new Property { Name = "Nha Tro Bi Khoa", OwnerId = "owner-b" };
        db.Properties.AddRange(propertyA, propertyB);
        await db.SaveChangesAsync();

        db.PropertyManagers.AddRange(
            new PropertyManager { PropertyId = propertyA.Id, ManagerUserId = "manager-a", AssignedByUserId = "owner-a" },
            new PropertyManager { PropertyId = propertyB.Id, ManagerUserId = "manager-a", AssignedByUserId = "owner-b" });

        var roomA101 = new Room { PropertyId = propertyA.Id, Code = "A101", StandardPrice = 1000 };
        var roomA102 = new Room { PropertyId = propertyA.Id, Code = "A102", StandardPrice = 1000 };
        var roomB201 = new Room { PropertyId = propertyB.Id, Code = "B201", StandardPrice = 1000 };
        db.Rooms.AddRange(roomA101, roomA102, roomB201);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Nguyen Van An", Phone = "0901234567", UserId = "tenant-a" };
        // Tên có dấu: chứng minh prefilter Latin1_General_100_CI_AI khớp "duc anh" ↔ "Đức Anh".
        var accentTenant = new Tenant { FullName = "Đức Anh", Phone = "0907654321" };
        db.Tenants.AddRange(tenant, accentTenant);
        await db.SaveChangesAsync();
        db.TenantOwnerLinks.AddRange(
            new TenantOwnerLink { TenantId = tenant.Id, OwnerId = "owner-a" },
            new TenantOwnerLink { TenantId = accentTenant.Id, OwnerId = "owner-a" });

        db.Contracts.AddRange(
            new Contract
            {
                ContractId = contractA,
                ContractNumber = "HD-2026-001",
                RoomId = roomA101.Id,
                StartDate = today.AddDays(-5),
                EndDate = today.AddDays(25),
                Status = ContractStatus.Approved,
            },
            new Contract
            {
                ContractId = contractB,
                ContractNumber = "HD-2026-002",
                RoomId = roomB201.Id,
                StartDate = today.AddDays(-5),
                EndDate = today.AddDays(25),
                Status = ContractStatus.Approved,
            },
            new Contract
            {
                ContractId = draftContract,
                ContractNumber = "HD-2026-DRAFT",
                RoomId = roomA102.Id,
                StartDate = today.AddDays(-5),
                EndDate = today.AddDays(25),
                Status = ContractStatus.Draft,
            });

        db.ContractMembers.AddRange(
            new ContractMember { ContractId = contractA, TenantId = tenant.Id, IsRepresentative = true },
            new ContractMember { ContractId = draftContract, TenantId = tenant.Id, IsRepresentative = true });
        await db.SaveChangesAsync();

        var invoiceA = new Invoice
        {
            ContractId = contractA,
            PeriodStart = new DateOnly(2026, 11, 1),
            PeriodEnd = new DateOnly(2026, 11, 30),
            Status = InvoiceStatus.Issued,
            TotalAmount = 1000,
        };
        var invoiceB = new Invoice
        {
            ContractId = contractB,
            PeriodStart = new DateOnly(2026, 11, 1),
            PeriodEnd = new DateOnly(2026, 11, 30),
            Status = InvoiceStatus.Issued,
            TotalAmount = 1000,
        };
        db.Invoices.AddRange(invoiceA, invoiceB);

        db.MaintenanceIncidents.AddRange(
            new MaintenanceIncident
            {
                RoomId = roomA101.Id,
                ReportedByUserId = "tenant-a",
                Title = "Voi nuoc hong",
                Description = "Voi nuoc hong",
                Category = IncidentCategory.Water,
                Urgency = IncidentUrgency.Normal,
                CreatedAt = DateTime.UtcNow,
            },
            new MaintenanceIncident
            {
                RoomId = roomA102.Id,
                ReportedByUserId = "tenant-a",
                Title = "Su co nhap",
                Description = "Su co nhap",
                Category = IncidentCategory.Water,
                Urgency = IncidentUrgency.Normal,
                CreatedAt = DateTime.UtcNow,
            });

        db.GatePasses.Add(new GatePass
        {
            PropertyId = propertyA.Id,
            RoomId = roomA101.Id,
            TokenHash = new string('a', 64),
            VisitorName = "Tran Binh",
            VisitorPhone = "0912345678",
            PassType = GatePassType.Guest,
            StayType = GateStayType.Visit,
            Status = GatePassStatus.Approved,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            IssuedByUserId = "manager-a",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        db.StoredFiles.Add(new StoredFile
        {
            FileName = "cccd.pdf",
            StoredFileName = "cccd.pdf",
            RelativePath = "2026/11/cccd.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 10,
            FileCategory = FileCategory.IdentityDocument,
            UploadedByUserId = "tenant-a",
            UploadedAt = DateTimeOffset.UtcNow,
            RelatedEntityId = tenant.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RelatedEntityType = RelatedEntityType.Tenant,
        });

        var catalog = new ServiceCatalog { PropertyId = propertyA.Id };
        db.ServiceCatalogs.Add(catalog);
        await db.SaveChangesAsync();
        db.ServicePriceHistories.Add(new ServicePriceHistory
        {
            ServiceCatalogId = catalog.Id,
            Name = "Dien sinh hoat",
            Unit = "kWh",
            CalculationMethod = "PerUnit",
            UnitPrice = 3500,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return new Seed(contractA, contractB, draftContract, invoiceA.Id, invoiceB.Id);
    }
}
