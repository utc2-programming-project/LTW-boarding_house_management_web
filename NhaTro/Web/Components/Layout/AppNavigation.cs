// Cây điều hướng duy nhất cho sidebar + breadcrumb + active.
// Mỗi section = một nhóm trên sidebar (TitleKey null: không hiện tiêu đề).
// Item có Href là đích; Children/Aliases là route con/URL cũ không hiện trên sidebar.
namespace Web.Components.Layout;

public static class AppNavigation
{
    public static readonly NavigationSection[] Sections =
    [
        new(null, "Admin,ChuTro,QuanLy,KhachThue",
        [
            new("Home", "home", "Admin,ChuTro,QuanLy,KhachThue", "/", Aliases: ["/dashboard"]),
        ]),
        new("NavGroupProperty", "ChuTro,QuanLy,KhachThue",
        [
            new("Properties", "building", "ChuTro,QuanLy", "/properties",
                Children: [new("P2PropertyTrashTitle", "", "ChuTro", "/properties/trash", ShowInSidebar: false)]),
            new("Rooms", "door", "ChuTro,QuanLy", "/rooms",
                Children: [new("P2TrashTitle", "", "ChuTro", "/rooms/trash", ShowInSidebar: false)]),
            new("Tenants", "users", "ChuTro,QuanLy", "/tenants",
                Children: [new("TenantTrashTitle", "", "ChuTro", "/tenants/trash", ShowInSidebar: false)],
                Aliases: ["/trash"]),
            new("Contracts", "file", "ChuTro,QuanLy,KhachThue", "/contracts",
                Children:
                [
                    new("ContractTrashTitle", "", "ChuTro", "/contracts/trash", ShowInSidebar: false),
                    new("ContractCreateNav", "", "ChuTro,QuanLy", "/contracts/create", ShowInSidebar: false),
                    new("ContractRenewalsNav", "", "ChuTro,QuanLy", "/contracts/renewals", ShowInSidebar: false),
                ]),
            new("MyRoom", "door", "KhachThue", "/my/room", Aliases: ["/tenant/my-room"]),
        ]),
        new("NavGroupFinance", "ChuTro,QuanLy,KhachThue",
        [
            new("Services", "settings", "ChuTro,QuanLy,KhachThue", "/services",
                Children:
                [
                    new("ServiceTrashTitle", "", "ChuTro", "/services/trash", ShowInSidebar: false),
                    new("ServiceRegistrationTrashTitle", "", "ChuTro", "/services/registrations/trash", ShowInSidebar: false),
                    new("PricingNav", "", "ChuTro,QuanLy,KhachThue", "/services/prices", ShowInSidebar: false),
                    new("ServiceRegistrationsNav", "", "ChuTro,QuanLy", "/service-registrations", ShowInSidebar: false),
                    new("RegHistoryNav", "", "KhachThue", "/my/service-registrations", ShowInSidebar: false),
                    new("MeterReadingNavReadings", "", "ChuTro,QuanLy,KhachThue", "/services/readings",
                        ShowInSidebar: false, Aliases: ["/meter-readings", "/services/meter-reading"],
                        Children:
                        [
                            new("P5CostNav", "", "ChuTro,QuanLy,KhachThue", "/services/readings/costs",
                                ShowInSidebar: false),
                        ]),
                ]),
            new("Invoices", "receipt", "ChuTro,QuanLy", "/invoices",
                Children: [new("InvoiceTrashTitle", "", "ChuTro", "/invoices/trash", ShowInSidebar: false)]),
            new("PaymentRequests", "check", "ChuTro,QuanLy", "/payment-requests"),
            new("MyInvoices", "receipt", "KhachThue", "/my/invoices", Aliases: ["/my-invoices"]),
        ]),
        new("NavGroupOperations", "ChuTro,QuanLy,KhachThue",
        [
            new("Incidents", "wrench", "ChuTro,QuanLy", "/incidents",
                Children: [new("TrashTitle", "", "ChuTro", "/incidents/trash", ShowInSidebar: false)]),
            new("ReportIncident", "wrench", "KhachThue", "/incidents"),
            new("Reports", "chart", "ChuTro,QuanLy", "/reports"),
            new("Approvals", "check", "ChuTro", "/approvals"),
            new("MyRequests", "file", "QuanLy", "/my/requests", Aliases: ["/my-requests"]),
        ]),
        new("NavGroupAdmin", "Admin,ChuTro,QuanLy",
        [
            // VHT-95: /admin/users cho Admin, Chủ trọ, Quản lý; phạm vi dữ liệu do service lọc.
            new("Accounts", "users", "Admin,ChuTro,QuanLy", "/admin/users"),
            new("OwnerAdmin", "building", "Admin", "/admin/owners"),
            new("DocumentTrashTitle", "", "ChuTro,QuanLy", "/documents/trash", ShowInSidebar: false),
            new("ManagerSwitches", "settings", "ChuTro", "/admin/manager-switches", Aliases: ["/manager-switches"]),
        ]),
        new("NavGroupPersonal", "KhachThue",
        [
            new("MyDocuments", "file-text", "KhachThue", "/my/documents", Aliases: ["/documents"],
                Children: [new("DocumentTrashTitle", "", "KhachThue", "/documents/trash", ShowInSidebar: false)]),
        ]),
    ];

    /// <summary>Toàn bộ item (kể cả route con ẩn sidebar), dùng cho test/resolver.</summary>
    public static readonly NavigationItem[] Items = Sections.SelectMany(section => Flatten(section.Items)).ToArray();

    private static IEnumerable<NavigationItem> Flatten(IEnumerable<NavigationItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            if (item.Children is null) continue;
            foreach (var child in Flatten(item.Children)) yield return child;
        }
    }
}

public sealed record NavigationItem(
    string Key,
    string Icon,
    string Roles,
    string Href,
    bool ShowInSidebar = true,
    string[]? Aliases = null,
    NavigationItem[]? Children = null);

public sealed record NavigationSection(string? TitleKey, string Roles, NavigationItem[] Items);
