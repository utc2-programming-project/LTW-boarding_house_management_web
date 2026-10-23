namespace Web.Features.Tenants.Documents;

/// <summary>
/// Xác định phạm vi giấy tờ của người gọi với một hồ sơ khách thuê. Dùng chung cho service P3 và
/// storage dùng chung để đường tải trực tiếp <c>/api/files/{id}</c> cũng bị chặn ngoài phạm vi.
/// </summary>
public interface ITenantDocumentScopeService
{
    Task<TenantDocumentAccessLevel> ResolveAsync(
        int tenantId,
        TenantCaller caller,
        CancellationToken cancellationToken = default);
}
