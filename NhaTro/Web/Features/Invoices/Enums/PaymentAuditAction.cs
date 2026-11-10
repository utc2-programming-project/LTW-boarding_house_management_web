namespace Web.Features.Invoices.Enums;

/// <summary>Hành động nghiệp vụ P6 được ghi audit cùng transaction với thay đổi tiền/hóa đơn.</summary>
public enum PaymentAuditAction
{
    Submitted = 0,
    Confirmed = 1,
    Rejected = 2,
}
