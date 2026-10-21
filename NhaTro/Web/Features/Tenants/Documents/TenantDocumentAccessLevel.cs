namespace Web.Features.Tenants.Documents;

/// <summary>
/// Mức phạm vi của người gọi với giấy tờ của một hồ sơ khách thuê.
/// D9: Admin không xem; Quản lý chỉ xem trong tòa nhà được giao; khách thuê chỉ hồ sơ của mình.
/// </summary>
public enum TenantDocumentAccessLevel
{
    None = 0,

    /// <summary>Chính khách thuê sở hữu hồ sơ.</summary>
    Own = 1,

    /// <summary>Chủ trọ (phạm vi toàn hệ thống của chủ trọ; chờ VHT-25 khóa theo chủ sở hữu).</summary>
    Owner = 2,

    /// <summary>Quản lý được giao tòa nhà có hồ sơ khách.</summary>
    Manager = 3,
}
