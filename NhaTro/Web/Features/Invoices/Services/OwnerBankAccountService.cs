using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Features.Invoices.Services;

/// <summary>
/// PLAN-028: tài khoản nhận tiền của chủ trọ, dùng để sinh QR VietQR.
/// </summary>
public sealed record OwnerBankAccount(
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountName)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BankCode)
        && !string.IsNullOrWhiteSpace(AccountNumber)
        && !string.IsNullOrWhiteSpace(AccountName);

    public string DisplayAccount => $"{BankName} · {AccountNumber} · {AccountName}";
}

public interface IOwnerBankAccountService
{
    /// <summary>Tài khoản nhận tiền của một người dùng (thường là chủ trọ).</summary>
    Task<OwnerBankAccount?> GetForUserAsync(string userId, CancellationToken ct = default);

    /// <summary>Tài khoản của chủ trọ sở hữu phòng của hợp đồng.</summary>
    Task<OwnerBankAccount?> GetForContractAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Tài khoản của chủ trọ sở hữu phòng của hóa đơn.</summary>
    Task<OwnerBankAccount?> GetForInvoiceAsync(int invoiceId, CancellationToken ct = default);
}

public sealed class OwnerBankAccountService(
    IDbContextFactory<ApplicationDbContext> dbFactory) : IOwnerBankAccountService
{
    public async Task<OwnerBankAccount?> GetForUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.BankCode, user.BankAccountNumber, user.BankAccountName })
            .FirstOrDefaultAsync(ct);

        return ToAccount(row?.BankCode, row?.BankAccountNumber, row?.BankAccountName);
    }

    public async Task<OwnerBankAccount?> GetForContractAsync(Guid contractId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ownerId = await (
            from contract in db.Contracts
            join room in db.Rooms on contract.RoomId equals room.Id
            join property in db.Properties on room.PropertyId equals property.Id
            where contract.ContractId == contractId
            select property.OwnerId).FirstOrDefaultAsync(ct);

        return ownerId is null ? null : await GetForUserAsync(ownerId, ct);
    }

    public async Task<OwnerBankAccount?> GetForInvoiceAsync(int invoiceId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var contractId = await db.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Id == invoiceId)
            .Select(invoice => (Guid?)invoice.ContractId)
            .FirstOrDefaultAsync(ct);

        return contractId is null ? null : await GetForContractAsync(contractId.Value, ct);
    }

    private static OwnerBankAccount? ToAccount(string? code, string? number, string? name)
    {
        if (string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(number)
            || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var account = new OwnerBankAccount(code, VietQrBankCatalog.GetName(code), number, name);
        return account.IsConfigured ? account : null;
    }
}
