using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Features.Accounts;
using Web.Features.Invoices.DTOs;
using Web.Features.Properties;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Storage.Services;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Adapter UI P6 (VHT-55): giữ caller đáng tin cậy từ principal server, gom luồng tải minh chứng
/// và gửi yêu cầu thanh toán. Không chứa state theo phiên; trang Blazor tự giữ trạng thái form.
/// </summary>
public sealed class PaymentPageService(
    AuthenticationStateProvider authentication,
    IPaymentService payments,
    FileService files,
    IOptions<StorageOptions> storageOptions)
{
    private PaymentProofPolicy? _proofPolicy;

    /// <summary>Ngữ cảnh nghiệp vụ của người dùng hiện tại; Admin/không đăng nhập bị từ chối.</summary>
    public async Task<PropertyCaller> CallerAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = Roles.ResolvePrimaryRole(user);

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role) || role == Roles.Admin)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác thanh toán này.");
        }

        return new PropertyCaller(userId, role);
    }

    /// <summary>Giới hạn minh chứng lấy cùng nguồn với storage để UI báo trước khi upload.</summary>
    public PaymentProofPolicy ProofPolicy => _proofPolicy ??= new PaymentProofPolicy(
        StorageLimits.MaxBytes(storageOptions.Value, FileCategory.PaymentProof),
        FileTypeRules.AllowedExtensions(FileCategory.PaymentProof));

    /// <summary>Lịch sử các lần gửi của hóa đơn, mới nhất trước.</summary>
    public async Task<IReadOnlyList<PaymentDto>> HistoryAsync(
        int invoiceId,
        CancellationToken cancellationToken = default) =>
        await payments.GetPaymentsAsync(invoiceId, await CallerAsync(), cancellationToken);

    /// <summary>Tra kết quả mới nhất; dùng sau khi mất kết nối trước khi cho phép thử lại.</summary>
    public async Task<PaymentDto?> LatestAsync(
        int invoiceId,
        CancellationToken cancellationToken = default) =>
        await payments.GetLatestPaymentAsync(invoiceId, await CallerAsync(), cancellationToken);

    /// <summary>Danh sách metadata minh chứng đã upload cho hóa đơn để hiển thị tên/tải tệp.</summary>
    public async Task<IReadOnlyList<FileMetadataDto>> ProofFilesAsync(
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        var caller = await CallerAsync();
        return await files.ListAsync(
            RelatedEntityType.Payment,
            invoiceId.ToString(CultureInfo.InvariantCulture),
            new StorageCaller(caller.UserId, caller.Role),
            cancellationToken);
    }

    /// <summary>
    /// Tải một minh chứng lên storage. Chặn sớm tệp quá lớn và sai đuôi trước khi đọc stream;
    /// FileService vẫn kiểm loại nội dung, quyền và phạm vi hóa đơn lần hai.
    /// </summary>
    public async Task<UploadResult> UploadProofAsync(
        int invoiceId,
        IBrowserFile browserFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(browserFile);

        var policy = ProofPolicy;
        if (browserFile.Size > policy.MaxBytes)
        {
            return UploadResult.Failure(
                StorageErrorCodes.FileSizeExceeded,
                $"Kích thước tệp vượt quá giới hạn {FileTypeRules.DescribeMegabytes(policy.MaxBytes)} MB.");
        }

        var extension = FileTypeRules.GetExtension(browserFile.Name);
        if (!policy.AllowedExtensions.Contains(extension, StringComparer.Ordinal))
        {
            return UploadResult.Failure(
                StorageErrorCodes.InvalidFileType,
                $"Minh chứng thanh toán chỉ chấp nhận file {FileTypeRules.DescribeAllowedExtensions(FileCategory.PaymentProof)}");
        }

        var caller = await CallerAsync();
        var request = new UploadFileRequest
        {
            File = new BrowserFileFormFile(browserFile, policy.MaxBytes),
            FileCategory = FileCategory.PaymentProof,
            RelatedEntityType = RelatedEntityType.Payment,
            RelatedEntityId = invoiceId.ToString(CultureInfo.InvariantCulture),
        };

        return await files.UploadAsync(
            request,
            new StorageCaller(caller.UserId, caller.Role),
            cancellationToken);
    }

    /// <summary>Gửi yêu cầu thanh toán; service tự chặn lặp và kiểm số tiền/phạm vi.</summary>
    public async Task<PaymentDto> SubmitAsync(
        int invoiceId,
        decimal amount,
        DateTimeOffset transferredAt,
        string? transactionCode,
        IReadOnlyList<Guid> proofFileIds,
        CancellationToken cancellationToken = default)
    {
        var request = new SubmitPaymentRequest
        {
            Amount = amount,
            TransferredAt = transferredAt,
            TransactionCode = transactionCode,
            ProofFileIds = proofFileIds.ToList(),
        };

        return await payments.SubmitAsync(invoiceId, request, await CallerAsync(), cancellationToken);
    }

    /// <summary>
    /// Bọc <see cref="IBrowserFile"/> thành <see cref="IFormFile"/> để storage đọc trực tiếp,
    /// không copy toàn bộ tệp vào RAM; giới hạn đọc áp ngay trong <c>OpenReadStream</c>.
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

/// <summary>Giới hạn hiển thị trước cho minh chứng thanh toán (cùng nguồn với storage).</summary>
public sealed record PaymentProofPolicy(long MaxBytes, IReadOnlyList<string> AllowedExtensions);
