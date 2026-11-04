namespace Web.Features.Tenants.Documents;

/// <summary>
/// Danh sách giấy tờ của một hồ sơ kèm khả năng thao tác theo phạm vi người gọi.
/// UI dùng các cờ <c>Can*</c> để ẩn nút thay vì tự đoán quyền; API vẫn kiểm lại khi thao tác.
/// </summary>
public sealed class TenantDocumentListDto
{
    public required int TenantId { get; init; }

    public required IReadOnlyList<TenantDocumentMetadataDto> Items { get; init; }

    public required bool CanUpload { get; init; }

    public required bool CanDownload { get; init; }

    public required bool CanDelete { get; init; }

    public required bool CanReplace { get; init; }

    /// <summary>D9: Quản lý xem được nhưng không xuất giấy tờ.</summary>
    public required bool CanExport { get; init; }

    public required long MaxFileSizeBytes { get; init; }

    public required IReadOnlyList<string> AllowedExtensions { get; init; }

    /// <summary>
    /// Số tệp tối đa cho mỗi hồ sơ theo cấu hình storage; <c>null</c> = chưa chốt/không giới hạn.
    /// Dùng để UI chỉ hiển thị hạn mức khi thực sự có cấu hình, không tự nhân số loại giấy tờ.
    /// </summary>
    public int? MaxFilesPerEntity { get; init; }
}
