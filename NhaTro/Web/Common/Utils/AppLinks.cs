using System.Globalization;

namespace Web.Common.Utils;

/// <summary>
/// URL cho các cột định danh trong bảng danh sách, để bấm một phát là tới đúng chỗ
/// thay vì phải gõ lại ô tìm kiếm. Quy ước chung của app:
/// <c>?q=</c> điền sẵn ô tìm kiếm của trang đích, <c>?&lt;entity&gt;Id=</c> mở dialog chi tiết.
/// </summary>
public static class AppLinks
{
    // --- Phòng: mở chi tiết theo id, hoặc lọc danh sách theo mã phòng ---
    public static string Room(int roomId) => WithQuery("/rooms", "roomId", Id(roomId));

    public static string RoomsOfProperty(int propertyId) => WithQuery("/rooms", "propertyId", Id(propertyId));

    public static string RoomsByCode(string? roomCode) => WithQuery("/rooms", "q", roomCode);

    // --- Nhà: mở chi tiết theo id, hoặc lọc danh sách theo từ khóa ---
    public static string Property(int propertyId) => WithQuery("/properties", "propertyId", Id(propertyId));

    public static string PropertiesByKeyword(string? keyword) => WithQuery("/properties", "q", keyword);

    // --- Khách thuê: mở hồ sơ theo id, hoặc lọc danh sách theo tên ---
    public static string Tenant(int tenantId) => WithQuery("/tenants", "tenantId", Id(tenantId));

    public static string TenantsByKeyword(string? keyword) => WithQuery("/tenants", "q", keyword);

    // --- Hợp đồng, hóa đơn, phiếu thanh toán: đều đã có trang chi tiết ---
    public static string Contract(Guid contractId) => $"/contracts/{contractId:D}";

    public static string ContractsByKeyword(string? keyword) => WithQuery("/contracts", "q", keyword);

    public static string Invoice(int invoiceId, string basePath = "/invoices") => $"{basePath}/{Id(invoiceId)}";

    public static string InvoicesByKeyword(string? keyword) => WithQuery("/invoices", "q", keyword);

    public static string PaymentRequest(int paymentId) => $"/payment-requests/{Id(paymentId)}";

    public static string PaymentRequestsByKeyword(string? keyword) => WithQuery("/payment-requests", "q", keyword);

    // --- Sự cố: mở modal chi tiết theo id ---
    public static string Incident(int incidentId) => WithQuery("/incidents", "incidentId", Id(incidentId));

    // --- Dịch vụ: bảng giá của một dịch vụ, hoặc lọc danh mục dịch vụ ---
    public static string ServicePrices(string? serviceId) => WithQuery("/services/prices", "serviceId", serviceId);

    public static string ServicePrices(int serviceId) => ServicePrices(Id(serviceId));

    public static string ServicesByKeyword(string? keyword) => WithQuery("/services", "q", keyword);

    public static string ServiceRegistrationsByKeyword(string? keyword) => WithQuery("/service-registrations", "q", keyword);

    // Không có giá trị (bản ghi thiếu mã/tên) thì trả path trần để link vẫn dùng được.
    private static string WithQuery(string path, string key, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? path
            : $"{path}?{key}={Uri.EscapeDataString(value.Trim())}";

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
