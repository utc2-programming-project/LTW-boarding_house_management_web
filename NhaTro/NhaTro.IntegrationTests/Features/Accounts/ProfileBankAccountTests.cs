// PLAN-028: lưu tài khoản nhận tiền theo chủ trọ và dùng nó cho QR hóa đơn.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;

namespace NhaTro.IntegrationTests.Features.Accounts;

public sealed class ProfileBankAccountTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [SqlServerFact]
    public async Task ChuTro_luu_tai_khoan_nhan_tien_va_doc_lai_duoc()
    {
        await fixture.ResetAsync();
        await using (var seed = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(seed, "owner-a", Roles.ChuTro);
        }

        var service = NewAccountService();
        var result = await service.UpdateMyProfileAsync(new UpdateMyProfileRequest
        {
            BankCode = "vietinbank",
            BankAccountNumber = "1190 0295 3584",
            BankAccountName = "Nguyen  Van A",
        }, "owner-a");

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal("VIETINBANK", result.Value!.BankCode);
        Assert.Equal("VietinBank", result.Value.BankName);
        Assert.Equal("119002953584", result.Value.BankAccountNumber);
        Assert.Equal("NGUYEN VAN A", result.Value.BankAccountName);
        Assert.True(result.Value.HasBankAccount);

        var ownerBank = new OwnerBankAccountService(fixture);
        var account = await ownerBank.GetForUserAsync("owner-a");
        Assert.NotNull(account);
        Assert.Equal("119002953584", account!.AccountNumber);
        Assert.Equal("VietinBank · 119002953584 · NGUYEN VAN A", account.DisplayAccount);
    }

    [SqlServerFact]
    public async Task KhachThue_khong_duoc_luu_tai_khoan_nhan_tien()
    {
        await fixture.ResetAsync();
        await using (var seed = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(seed, "khach-a", Roles.KhachThue);
        }

        var result = await NewAccountService().UpdateMyProfileAsync(new UpdateMyProfileRequest
        {
            BankCode = "VIETINBANK",
            BankAccountNumber = "119002953584",
            BankAccountName = "NGUYEN VAN A",
        }, "khach-a");

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.Forbidden, result.ErrorCode);
    }

    [SqlServerFact]
    public async Task So_tai_khoan_sai_dinh_dang_bi_tu_choi()
    {
        await fixture.ResetAsync();
        await using (var seed = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(seed, "owner-a", Roles.ChuTro);
        }

        var result = await NewAccountService().UpdateMyProfileAsync(new UpdateMyProfileRequest
        {
            BankCode = "VIETINBANK",
            BankAccountNumber = "12ab",
            BankAccountName = "NGUYEN VAN A",
        }, "owner-a");

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.InvalidBankAccount, result.ErrorCode);
    }

    [SqlServerFact]
    public async Task Qr_hoa_don_lay_tai_khoan_cua_chu_tro_so_huu_phong()
    {
        await fixture.ResetAsync();
        int invoiceId;
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(db, "owner-b", Roles.ChuTro);

            var owner = await db.Users.SingleAsync(u => u.Id == "owner-a");
            owner.BankCode = "VIETINBANK";
            owner.BankAccountNumber = "119002953584";
            owner.BankAccountName = "NGUYEN VAN A";
            await db.SaveChangesAsync();

            var property = new Property { Name = "Nhà A", OwnerId = "owner-a" };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            var room = new Room
            {
                PropertyId = property.Id,
                Code = "A-101",
                StandardPrice = 1_000_000m,
                Status = RoomStatus.Available,
            };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();

            var contract = new Contract
            {
                ContractNumber = "HD-A",
                RoomId = room.Id,
                StartDate = Today.AddDays(-10),
                EndDate = Today.AddDays(20),
                SnapshotPrice = 1_000_000m,
                DepositAmount = 200_000m,
                Status = ContractStatus.Approved,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();

            var invoice = new Invoice
            {
                ContractId = contract.ContractId,
                PeriodStart = DateOnly.FromDateTime(Today.AddDays(-10)),
                PeriodEnd = DateOnly.FromDateTime(Today.AddDays(20)),
                Status = InvoiceStatus.Issued,
                TotalAmount = 1_000_000m,
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var service = new OwnerBankAccountService(fixture);
        var account = await service.GetForInvoiceAsync(invoiceId);

        Assert.NotNull(account);
        Assert.Equal("VIETINBANK", account!.BankCode);
        Assert.Equal("119002953584", account.AccountNumber);
        Assert.Equal("NGUYEN VAN A", account.AccountName);
    }

    private AccountService NewAccountService()
    {
        var db = fixture.CreateContext();
        var identityOptions = new IdentityOptions
        {
            Password =
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = false,
            },
            User = { RequireUniqueEmail = true },
        };

        var userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(db),
            Options.Create(identityOptions),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

        return new AccountService(userManager, fixture, NullLogger<AccountService>.Instance);
    }
}
