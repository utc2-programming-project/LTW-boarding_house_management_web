using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;
using Web.Features.Contracts.Enums;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Adapter cho UI Blazor Server của phân hệ hồ sơ giấy tờ (VHT-26/2B) và phòng đang thuê (VHT-25/2D).
/// </summary>
public sealed class TenantDocumentPageService(
    AuthenticationStateProvider authenticationState,
    ITenantDocumentService documentService,
    ApplicationDbContext db,
    TenantPageService? tenantPageService = null,
    IOptions<StorageOptions>? storageOptions = null,
    TenantContactPageService? contactPageService = null)
{
    public async Task<string> GetCurrentRoleAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        return Roles.ResolvePrimaryRole(user);
    }

    public async Task<TenantCaller> GetCallerAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = Roles.ResolvePrimaryRole(user);
        return new TenantCaller(userId, role);
    }

    public async Task<Tenant?> GetCurrentTenantAsync(CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        if (!caller.IsTenant || string.IsNullOrWhiteSpace(caller.UserId))
        {
            return null;
        }

        return await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == caller.UserId && !t.IsDeleted, ct);
    }

    public async Task<TenantDocumentListDto?> GetDocumentsAsync(int tenantId, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await documentService.ListAsync(tenantId, caller, ct);
    }

    public async Task<TenantDocumentPageResult> GetMyDocumentPageAsync(CancellationToken ct = default)
    {
        var tenant = await GetCurrentTenantAsync(ct);
        if (tenant is null)
        {
            return new TenantDocumentPageResult(null, null, NoTenantLinked: true);
        }

        var documents = await GetDocumentsAsync(tenant.Id, ct);
        return new TenantDocumentPageResult(tenant, documents, NoTenantLinked: false);
    }

    public async Task<UploadResult> UploadDocumentAsync(
        int tenantId,
        IBrowserFile browserFile,
        string documentType,
        string? description,
        CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();

        // Giới hạn thật lấy từ cấu hình Storage (cùng nguồn với FileService). Nếu không có
        // options (unit test dựng tay) thì dùng mức mặc định của FileTypeRules.
        var maxAllowedBytes = storageOptions is null
            ? FileTypeRules.MaxDefaultBytes()
            : StorageLimits.MaxBytes(storageOptions.Value, FileCategory.IdentityDocument);

        // Chặn sớm trước khi mở stream: không đọc tệp quá lớn vào RAM.
        if (browserFile.Size > maxAllowedBytes)
        {
            return UploadResult.Failure(
                StorageErrorCodes.FileSizeExceeded,
                $"Kích thước tệp vượt quá giới hạn {FileTypeRules.DescribeMegabytes(maxAllowedBytes)} MB.");
        }

        // IFormFile bọc trực tiếp IBrowserFile: FileService chỉ đọc stream một lần, có giới hạn
        // maxAllowedBytes. Không tạo MemoryStream chứa toàn bộ tệp trong page service nữa.
        var formFile = new BrowserFileFormFile(browserFile, maxAllowedBytes);

        var request = new UploadFileRequest
        {
            File = formFile,
            FileCategory = FileCategory.IdentityDocument,
            RelatedEntityType = RelatedEntityType.Tenant,
            RelatedEntityId = tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DocumentType = documentType,
            Description = description,
        };

        return await documentService.UploadAsync(tenantId, request, caller, ct);
    }

    public async Task<bool> DeleteDocumentAsync(int tenantId, Guid fileId, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await documentService.DeleteAsync(tenantId, fileId, caller, ct);
    }

    public async Task<FileDownloadResponse?> DownloadDocumentAsync(int tenantId, Guid fileId, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await documentService.DownloadAsync(tenantId, fileId, caller, ct);
    }

    /// <summary>Danh sách hợp đồng/phòng của khách hiện tại để dựng tabs chuyển phòng.</summary>
    public async Task<IReadOnlyList<LeasedRoomSummaryDto>> GetMyRentalsAsync(CancellationToken ct = default)
    {
        var overview = await LoadMyOverviewAsync(ct);
        if (overview is null)
        {
            return [];
        }

        return overview.Rentals
            .Select(rental => new LeasedRoomSummaryDto(
                rental.ContractId,
                rental.ContractNumber,
                rental.RoomCode,
                rental.PropertyName,
                rental.StartDate,
                rental.EndDate,
                rental.IsCurrent,
                rental.Status))
            .ToList();
    }

    /// <summary>
    /// Lấy phòng đang thuê qua liên kết hợp đồng thật của khách thuê hiện tại (không suy từ Room.Status).
    /// Khi truyền contractId, chỉ trả hợp đồng đó nếu thuộc chính khách.
    /// </summary>
    public async Task<LeasedRoomDetailDto?> GetMyLeasedRoomAsync(Guid? contractId = null, CancellationToken ct = default)
    {
        var overview = await LoadMyOverviewAsync(ct);
        if (overview is null)
        {
            return null;
        }

        var rental = contractId is Guid selectedId
            ? overview.Rentals.FirstOrDefault(item => item.ContractId == selectedId)
            : overview.CurrentRental ?? overview.Rentals.FirstOrDefault();
        if (rental is null)
        {
            return null;
        }

        return await MapRentalAsync(overview, rental, ct);
    }

    private async Task<TenantRentalOverviewDto?> LoadMyOverviewAsync(CancellationToken ct)
    {
        if (tenantPageService is null)
        {
            return null;
        }

        var result = await tenantPageService.GetOwnRentalOverviewAsync(ct);
        if (!result.Succeeded)
        {
            // Tài khoản chưa được gắn hồ sơ khách thuê là trạng thái rỗng hợp lệ, không phải
            // sự cố tải: page cần empty state thay vì màn hình lỗi kèm nút thử lại vô ích.
            if (result.ErrorCode == TenantErrorCodes.TenantNotFound)
            {
                return null;
            }

            // Các lỗi khác không được nuốt thành "chưa có phòng": page cần phân biệt
            // lỗi backend với trạng thái khách thật sự chưa có hợp đồng hiệu lực.
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Unable to load the current rental."
                    : result.ErrorMessage);
        }

        return result.Rental;
    }

    private async Task<LeasedRoomDetailDto> MapRentalAsync(
        TenantRentalOverviewDto overview,
        TenantRentalContractDto rental,
        CancellationToken ct)
    {
        var floor = await db.Rooms.AsNoTracking()
            .Where(r => r.Id == rental.RoomId)
            .Select(r => r.Floor)
            .FirstOrDefaultAsync(ct);

        // Liên hệ Chủ trọ/Quản lý của đúng tòa nhà đang thuê. Lỗi tải liên hệ không được
        // làm hỏng trang phòng đang thuê, nên chỉ bỏ trống phần liên hệ.
        PropertyContactDto? contact = null;
        if (contactPageService is not null)
        {
            try
            {
                contact = await contactPageService.GetForPropertyAsync(rental.PropertyId, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Liên hệ là thông tin phụ: lỗi tải không được làm hỏng trang phòng đang thuê.
                contact = null;
            }
        }

        var services = await Web.Features.Contracts.Services.ContractServiceLineLoader.LoadAsync(
            db, rental.PropertyId, rental.ContractId, rental.StartDate, ct);

        return new LeasedRoomDetailDto
        {
            ContractId = rental.ContractId,
            ContractNumber = rental.ContractNumber,
            RoomId = rental.RoomId,
            RoomCode = rental.RoomCode,
            Floor = floor,
            AreaM2 = rental.AreaM2,
            PropertyName = rental.PropertyName,
            PropertyAddress = rental.PropertyAddress,
            StartDate = rental.StartDate.ToDateTime(TimeOnly.MinValue),
            EndDate = rental.EndDate.ToDateTime(TimeOnly.MinValue),
            MonthlyRent = rental.SnapshotPrice,
            DepositAmount = rental.DepositAmount,
            PaymentCycleMonths = rental.PaymentCycleMonths,
            MeterReadingDay = rental.MeterReadingDay,
            Terms = rental.Terms,
            Services = services,
            // Dữ liệu tiền cọc đã đóng thực tế từ hợp đồng/phiếu thu; null khi chưa có chứng từ ghi nhận.
            PaidDepositAmount = rental.PaidDepositAmount,
            DepositPaidDate = rental.DepositPaidDate?.ToDateTime(TimeOnly.MinValue),
            DepositDueDate = rental.DepositDueDate?.ToDateTime(TimeOnly.MinValue),
            StatusLabel = rental.Status.ToString(),
            IsActive = rental.IsCurrent,
            Roommates = rental.Roommates.Select(roommate => new RoommateDto
            {
                TenantId = roommate.TenantId,
                FullName = roommate.FullName,
                PhoneNumber = roommate.PhoneNumber,
                IsCurrentUser = roommate.TenantId == overview!.TenantId,
            }).ToList(),
            Contact = contact,
        };
    }

    /// <summary>
    /// Bọc <see cref="IBrowserFile"/> thành <see cref="IFormFile"/> để chuyển cho tầng storage
    /// mà không phải copy toàn bộ tệp vào bộ nhớ. Giới hạn đọc được áp ngay trong OpenReadStream.
    /// </summary>
    private sealed class BrowserFileFormFile(IBrowserFile file, long maxAllowedBytes) : IFormFile
    {
        public string ContentType { get; } =
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{file.Name}\"";

        public IHeaderDictionary Headers { get; } = new HeaderDictionary();

        public long Length => file.Size;

        public string Name => "file";

        public string FileName => file.Name;

        public void CopyTo(Stream target)
        {
            using var source = OpenReadStream();
            source.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            await using var source = OpenReadStream();
            await source.CopyToAsync(target, cancellationToken);
        }

        public Stream OpenReadStream() => file.OpenReadStream(maxAllowedBytes);
    }
}

public sealed record TenantDocumentPageResult(
    Tenant? Tenant,
    TenantDocumentListDto? Documents,
    bool NoTenantLinked);
