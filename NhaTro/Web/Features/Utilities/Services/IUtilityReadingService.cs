using Web.Features.Invoices.DTOs;
using Web.Features.Properties;
using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Luồng ghi chỉ số và chốt chi phí kỳ thật của P5 (VHT-51):
/// mở kỳ từ hợp đồng + chỉ số kỳ trước, lưu chỉ số/chi phí snapshot, khóa kỳ đã phát hành (D10)
/// và cung cấp nguồn chi phí hợp lệ cho hóa đơn P6.
/// </summary>
public interface IUtilityReadingService
{
    /// <summary>
    /// Mở kỳ cho hợp đồng. Chỉ số đầu kỳ lấy từ kỳ liền trước đã ghi chỉ số (0 nếu là kỳ đầu).
    /// Gọi lặp với cùng ngày bắt đầu trả lại kỳ đã có, không ghi trùng.
    /// </summary>
    Task<UtilityReadingPeriodDto> OpenPeriodAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi chỉ số cuối kỳ lần đầu và chốt snapshot điện/nước/dịch vụ trong cùng transaction.
    /// Chặn khi kỳ đã phát hành hóa đơn (D10) hoặc đã ghi chỉ số (điều chỉnh thuộc VHT-102).
    /// </summary>
    Task<UtilityReadingPeriodDto> SaveReadingsAsync(
        int periodId,
        decimal electricEnd,
        decimal waterEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<UtilityReadingPeriodDto?> GetPeriodAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<UtilityReadingPeriodDto?> GetPeriodByContractAsync(
        Guid contractId,
        DateOnly periodStart,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Chụp tường minh dịch vụ cho kỳ legacy đã ghi chỉ số, chưa có hóa đơn/snapshot.
    /// Gọi lại sau khi chụp trả 0; giá dùng tại thời điểm gọi, không suy diễn lịch sử.</summary>
    Task<int> BackfillServiceSnapshotsAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dựng nguồn chi phí đã chốt (thuê + điện + nước + dịch vụ) để P6 lập hóa đơn.
    /// Không tự tính tiền hóa đơn: mọi phép tính vẫn đi qua rule của P6.
    /// </summary>
    Task<CreateInvoiceRequest> BuildInvoiceRequestAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
