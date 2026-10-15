namespace Web.Components.Layout;

public static class AppNavigation
{
    // Chỉ gắn route khi module có trang thật và đã bảo vệ quyền.
    public static readonly NavigationItem[] Items =
    [
        new("Accounts", "users", "Admin,ChuTro"),
        new("Permissions", "shield", "Admin"),
        new("Properties", "building", "ChuTro,QuanLy"),
        new("Rooms", "door", "ChuTro,QuanLy"),
        new("Tenants", "users", "ChuTro,QuanLy"),
        new("Contracts", "file", "ChuTro,QuanLy"),
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
        new("ReportIncident", "wrench", "KhachThue")
    ];
}

public sealed record NavigationItem(string Key, string Icon, string Roles, string? Href = null);
