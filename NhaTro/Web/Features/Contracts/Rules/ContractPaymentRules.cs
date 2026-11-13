namespace Web.Features.Contracts.Rules;

/// <summary>Chính sách thanh toán cọc giữ căn sau khi khách ký hợp đồng điện tử.</summary>
public static class ContractPaymentRules
{
    /// <summary>Thời hạn thanh toán cọc tính từ lúc khách xác nhận hợp đồng.</summary>
    public static readonly TimeSpan DepositPaymentWindow = TimeSpan.FromHours(48);
}
