// VHT-9 — DTO mẫu P5 (Đơn giá điện/nước và dịch vụ)
namespace Web.Features.Utilities
{
    public enum TariffType { Electric, Water }

    public record TariffTierDto(decimal From, decimal? To, decimal Price);

    /// <summary>Biểu giá điện/nước: chỉ đọc, cấu hình sẵn (D7). Không có request tạo/sửa.</summary>
    public record TariffDto(
        TariffType Type,
        DateOnly EffectiveFrom,
        IReadOnlyList<TariffTierDto> Tiers);

    /// <summary>Dòng dịch vụ snapshot trong hợp đồng; thêm dịch vụ bằng dòng mới, không thêm cột phí cố định.</summary>
    public record ServiceLineDto(int ServiceId, string Name, decimal UnitPrice, string? Unit);

    /// <summary>Đơn giá dịch vụ khác, Chủ trọ thiết lập, hiệu lực theo ngày.</summary>
    public record ServicePriceDto(
        int ServiceId,
        string Name,
        decimal UnitPrice,
        DateOnly EffectiveFrom);
}

namespace BoardingHouse.Web.Features.Utilities
{
    public enum TariffType { Electric, Water }
    public record TariffTierDto(decimal From, decimal? To, decimal Price);
    public record TariffDto(TariffType Type, DateOnly EffectiveFrom, IReadOnlyList<TariffTierDto> Tiers);
    public record ServiceLineDto(int ServiceId, string Name, decimal UnitPrice, string? Unit);
    public record ServicePriceDto(int ServiceId, string Name, decimal UnitPrice, DateOnly EffectiveFrom);
}
