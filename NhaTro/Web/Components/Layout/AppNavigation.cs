namespace Web.Components.Layout;

public static class AppNavigation
{
    // Nhóm theo nghiệp vụ để sidebar dễ quét; mỗi nhóm chỉ hiện khi người dùng có
    // ít nhất một vai trò trong các mục con.
    public static readonly NavigationGroup[] Groups =
    [
        new("NavGroupOverview", "Admin,ChuTro,QuanLy,KhachThue",
        [
            new("Home", "home", "Admin,ChuTro,QuanLy,KhachThue", "/"),
            new("Dashboard", "chart", "ChuTro,QuanLy", "/dashboard"),
        ]),
        new("NavGroupProperty", "ChuTro,QuanLy,KhachThue",
        [
            new("Properties", "building", "ChuTro,QuanLy", "/properties"),
            new("Rooms", "door", "ChuTro,QuanLy", "/rooms"),
            new("Tenants", "users", "ChuTro,QuanLy", "/tenants"),
            new("Contracts", "file", "ChuTro,QuanLy,KhachThue", "/contracts"),
            new("MyRoom", "door", "KhachThue", "/tenant/my-room"),
        ]),
        new("NavGroupFinance", "ChuTro,QuanLy,KhachThue",
        [
            new("Services", "settings", "ChuTro,QuanLy,KhachThue", "/services"),
            new("Pricing", "wallet", "ChuTro,QuanLy,KhachThue", "/services/prices"),
            new("MeterReadings", "zap", "ChuTro,QuanLy,KhachThue", "/meter-readings"),
            new("Invoices", "receipt", "ChuTro,QuanLy", "/invoices"),
            new("MyInvoices", "receipt", "KhachThue", "/my-invoices"),
        ]),
        new("NavGroupOperations", "ChuTro,QuanLy,KhachThue",
        [
            new("Incidents", "wrench", "ChuTro,QuanLy", "/incidents"),
            new("ReportIncident", "wrench", "KhachThue", "/incidents"),
            new("Reports", "chart", "ChuTro,QuanLy", "/reports"),
            new("Approvals", "check", "ChuTro", "/approvals"),
            new("MyRequests", "file", "QuanLy", "/my-requests"),
        ]),
        new("NavGroupAdmin", "Admin,ChuTro,QuanLy",
        [
            // VHT-95: /admin/users cho Admin, Chủ trọ, Quản lý; phạm vi dữ liệu do service lọc.
            new("Accounts", "users", "Admin,ChuTro,QuanLy", "/admin/users"),
            new("Permissions", "shield", "Admin"),
            new("ManagerSwitches", "settings", "ChuTro", "/manager-switches"),
            // VHT-99: thùng rác hồ sơ khách thuê.
            new("Trash", "trash", "ChuTro", "/trash"),
            // VHT-98: thùng rác phòng tách route để không trùng /trash.
            new("RoomTrash", "trash", "ChuTro", "/rooms/trash"),
        ]),
        new("NavGroupPersonal", "Admin,ChuTro,QuanLy,KhachThue",
        [
            new("MyDocuments", "file-text", "KhachThue", "/documents"),
            // Hồ sơ khách thuê dùng service P3; các vai trò còn lại dùng hồ sơ tài khoản P1.
            new("MyTenantProfile", "user", "KhachThue", "/tenant/profile"),
            new("MyAccount", "user", "Admin,ChuTro,QuanLy", "/profile"),
        ]),
    ];

    public static readonly NavigationItem[] Items = Groups.SelectMany(group => group.Items).ToArray();
}

public sealed record NavigationItem(string Key, string Icon, string Roles, string? Href = null);

public sealed record NavigationGroup(string Key, string Roles, NavigationItem[] Items);
