using Web.Features.Properties;

namespace Web.Features.Search.Abstractions;

/// <summary>
/// Nguồn danh tính cho tìm kiếm. Tách giao diện khỏi <c>P2CallerProvider</c> để test đơn vị
/// dựng được caller thật mà không phải giả lập HttpContext/OwnerAccessGuard (E03).
/// </summary>
public interface ISearchCallerProvider
{
    Task<PropertyCaller> GetCallerAsync();
}
