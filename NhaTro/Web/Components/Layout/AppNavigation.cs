namespace Web.Components.Layout;

public static class AppNavigation
{
    // Chỉ gắn route khi module có trang thật và đã bảo vệ quyền.
    public static readonly NavigationItem[] Items =
    [
        new("Dashboard", "chart", "ChuTro,QuanLy"),
        // VHT-8: /admin/users chỉ dành cho Admin (D1). Màn hình trạng thái tài khoản của
        // Chủ trọ/Quản lý thuộc VHT-95 nên "Accounts" tạm chỉ mở cho Admin.
        new("Accounts", "users", "Admin", "/admin/users"),
        new("Permissions", "shield", "Admin"),
        new("Properties", "building", "ChuTro,QuanLy"),
        new("Rooms", "door", "ChuTro,QuanLy"),
        new("Tenants", "users", "ChuTro,QuanLy", "/tenants"),
        new("Contracts", "file", "ChuTro,QuanLy", "/contracts/create"),
        new("Services", "settings", "ChuTro,QuanLy"),
        new("Pricing", "wallet", "ChuTro"),
        new("Invoices", "receipt", "ChuTro,QuanLy"),
        new("Incidents", "wrench", "ChuTro,QuanLy"),
        new("Reports", "chart", "ChuTro"),
        new("ManagerSwitches", "settings", "ChuTro"),
        new("Approvals", "check", "ChuTro"),
        new("Trash", "trash", "ChuTro"),
        new("MyRequests", "file", "QuanLy"),
        new("MyRoom", "door", "KhachThue"),
        new("MyInvoices", "receipt", "KhachThue"),
        new("ReportIncident", "wrench", "KhachThue"),
        // Hồ sơ khách thuê dùng service P3; các vai trò còn lại dùng hồ sơ tài khoản P1.
        new("MyAccount", "user", "Admin,ChuTro,QuanLy", "/profile"),
        new("MyTenantProfile", "user", "KhachThue", "/tenant/profile")
    ];
}

public sealed record NavigationItem(string Key, string Icon, string Roles, string? Href = null);
