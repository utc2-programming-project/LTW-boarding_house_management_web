namespace Web.Components.Layout;

public static class AppNavigation
{
    // Chỉ gắn route khi module có trang thật và đã bảo vệ quyền.
    public static readonly NavigationItem[] Items =
    [
        new("Dashboard", "chart", "ChuTro,QuanLy"),
        // VHT-95: /admin/users cho Admin, Chủ trọ, Quản lý; phạm vi dữ liệu do service lọc.
        new("Accounts", "users", "Admin,ChuTro,QuanLy", "/admin/users"),
        new("Permissions", "shield", "Admin"),
        new("Properties", "building", "ChuTro,QuanLy", "/properties"),
        new("Rooms", "door", "ChuTro,QuanLy", "/rooms"),
        new("Tenants", "users", "ChuTro,QuanLy", "/tenants"),
        new("Contracts", "file", "ChuTro,QuanLy", "/contracts/create"),
        new("Services", "settings", "ChuTro,QuanLy,KhachThue", "/services"),
        new("Pricing", "wallet", "ChuTro,QuanLy,KhachThue", "/services/prices"),
        new("Invoices", "receipt", "ChuTro,QuanLy"),
        new("Incidents", "wrench", "ChuTro,QuanLy", "/incidents"),
        new("Reports", "chart", "ChuTro"),
        new("ManagerSwitches", "settings", "ChuTro"),
        new("Approvals", "check", "ChuTro"),
        new("Trash", "trash", "ChuTro"),
        new("MyRequests", "file", "QuanLy"),
        new("MyRoom", "door", "KhachThue"),
        new("MyInvoices", "receipt", "KhachThue"),
        new("ReportIncident", "wrench", "KhachThue", "/incidents"),
        // Hồ sơ khách thuê dùng service P3; các vai trò còn lại dùng hồ sơ tài khoản P1.
        new("MyAccount", "user", "Admin,ChuTro,QuanLy", "/profile"),
        new("MyTenantProfile", "user", "KhachThue", "/tenant/profile")
    ];
}

public sealed record NavigationItem(string Key, string Icon, string Roles, string? Href = null);
