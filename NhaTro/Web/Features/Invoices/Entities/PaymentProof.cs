using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;

namespace Web.Features.Invoices.Entities;

/// <summary>
/// Liên kết một yêu cầu thanh toán với các tệp minh chứng đã upload qua storage.
/// Tệp vẫn gắn với hóa đơn (<see cref="RelatedEntityType.Payment"/> + id hóa đơn) để kiểm phạm vi;
/// bảng này xác định minh chứng thuộc lần gửi nào và chặn dùng lại cùng tệp cho lần gửi khác.
/// </summary>
public sealed class PaymentProof
{
    public int PaymentId { get; set; }

    public Guid FileId { get; set; }

    public Payment Payment { get; set; } = null!;

    public StoredFile File { get; set; } = null!;
}
