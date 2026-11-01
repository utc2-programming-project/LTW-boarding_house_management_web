using Web.Features.Invoices.DTOs;
using Web.Features.Properties;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Query hóa đơn P6. Mọi hàm nhận <see cref="PropertyCaller"/> và tự giới hạn phạm vi:
/// Chủ trọ theo nhà sở hữu, Quản lý theo nhà được phân công, Khách thuê theo hợp đồng có tên mình.
/// </summary>
public interface IInvoiceQueryService
{
    Task<InvoiceListResult> GetInvoicesAsync(
        InvoiceFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto> GetInvoiceAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceContractOptionDto>> GetContractOptionsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
