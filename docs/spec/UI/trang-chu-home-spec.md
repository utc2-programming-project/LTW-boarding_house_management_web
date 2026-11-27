# SPEC — Trang chủ `/home` (hiện trạng, theo 4 vai trò)

> Trạng thái: **đặc tả hiện trạng (as-is)** — mô tả đúng code đang chạy; không phải bản thiết kế v2, không chứa kế hoạch triển khai.
> Nền: NhaTro, Blazor Web App .NET 10; `@rendermode InteractiveServer` + prerender; 4 vai trò `ChuTro`, `QuanLy`, `KhachThue`, `Admin`.
> Nguồn sự thật: `NhaTro/Web/Features/Home/**`, `NhaTro/Web/Features/Dashboard/Components/DashboardOverview.razor`, `Shared.*.resx`, `DashboardResource.*.resx`.
> Bản mô tả giao diện dạng txt: [Chủ trọ](giao-dien-home-chu-tro-hien-tai.txt) · [Quản lý](giao-dien-home-quan-ly-hien-tai.txt) · [Khách thuê](giao-dien-home-khach-thue-hien-tai.txt) · [Admin](giao-dien-home-admin-hien-tai.txt) — cùng thứ tự render với spec này.
> Liên quan: [Trang theo vai trò](../trang-theo-vai-tro-spec.md) · [Tối ưu/đồng nhất tầng đọc](../toi-uu-va-dong-nhat-tang-doc-spec.md). Landing công khai `/` và đăng nhập **không nằm trong spec này**.

## 0. Quy ước chung

| Mục | Quy ước |
|---|---|
| Route | `@page "/home"`, `@attribute [Authorize]`; cookie Identity chuyển về `/Account/Login`; `/dashboard` redirect `/home`; landing `/` là trang khác |
| Vai trò | `Roles.ResolvePrimaryRole`: claim `nhatro.active_role` trước, fallback `Admin → ChuTro → QuanLy → KhachThue`; không có vai trò → `UnauthorizedAccessException` |
| Hai nhánh render | `IsManagement = role is ChuTro or QuanLy` → nhúng `DashboardOverview` thay cho header + KPI riêng của `HomeView`; `Admin`/`KhachThue` dùng `PageHeading` + `HomeKpiGrid` |
| Nguồn dữ liệu | `HomePageService.LoadAsync` trả `HomeSnapshot`; mỗi khối tự bắt lỗi (xem §8) |
| Tham số cố định | `HomeView` luôn gọi `LoadAsync(includeManagementKpis: false)` → service **không tính KPI quản lý**; KPI của Chủ trọ/Quản lý do `DashboardOverview` tự tải và cache 45 giây |
| Định dạng | Chữ qua `IStringLocalizer<HomeResource>` (VI/EN); phần dashboard qua `DashboardResource`; tiền `MoneyFormatter`, số `NumberFormatter`, ngày `DateFormatter` |

### 0.1 Bảng so sánh nhanh 4 trang

| | Chủ trọ | Quản lý | Khách thuê | Admin |
|---|---|---|---|---|
| Header của HomeView | Ẩn | Ẩn | Hiện (huy hiệu vai trò, ngày, giờ cập nhật, Làm mới) | Hiện (giống Khách thuê) |
| KPI hiển thị | 6 thẻ dashboard (có doanh thu/công nợ) | 3 thẻ dashboard (không doanh thu/công nợ) | 4 thẻ "Tổng quan hôm nay" | 4 thẻ "Tổng quan hôm nay" |
| Bộ lọc nhà/kỳ | Có, trong `DashboardOverview` | Có, trong `DashboardOverview` | Không | Không |
| Việc cần làm | 4 mục | 4 mục (duyệt của mình) | 3 mục | 2 mục (tài khoản) |
| Panel dưới việc cần làm | Hoá đơn chưa thu + Hợp đồng hết hạn | Giống Chủ trọ | Giống Chủ trọ nhưng link `/my/...` | Tài khoản chờ duyệt |
| Lối tắt | 4 | 5 (thêm "Yêu cầu của tôi") | 3 | 2 (1 disabled) |
| Panel cuối dashboard | Doanh thu theo kỳ + biểu đồ | Sự cố + biểu đồ sự cố | Không có dashboard | Không có dashboard |

## 1. Bản đồ file & luồng chung

| Thành phần | Vị trí | Trách nhiệm |
|---|---|---|
| Trang | `Features/Home/Pages/HomeView.razor`, `.razor.css` | Chọn nhánh theo vai trò, render lối tắt + 3 panel, điều hướng hoá đơn |
| Nguồn dữ liệu | `Features/Home/Services/HomePageService.cs` | 4 hàm tải theo vai trò + cô lập lỗi từng nguồn |
| Hợp đồng dữ liệu | `Features/Home/DTOs/HomeDtos.cs` | `HomeSnapshot`, `HomeSection<T>`, `HomeKpi`, `HomeAction`, `HomeLoadState` |
| Khối UI | `Features/Home/Components/{HomeKpiGrid,HomeActionList,HomePanel,HomeQuickActions}.razor(.css)` | 4 template dùng lại (§7) |
| Dashboard nhúng | `Features/Dashboard/Components/DashboardOverview.razor` + `KPICardGrid`, `RoomStructureChart`, `TenantOverviewCard`, `RevenueChart`, `IncidentOverviewCard` | Chỉ cho Chủ trọ/Quản lý |
| Shell | `Components/Layout/AppTopbar.razor`, `AppSidebar.razor`, `AppNavigation.cs` | Thanh trên, menu trái theo vai trò |
| Resource | `Resources/Shared.*.resx`, `Resources/Features/Home/HomeResource.*.resx`, `Resources/Features/Dashboard/DashboardResource.*.resx` | Toàn bộ nhãn VI/EN |
| Test | `NhaTro.IntegrationTests/Features/Home/HomePageTests.cs` | 6 ca trên HTML prerender (§10) |

Luồng chung: `OnInitializedAsync` đọc `AuthenticationState` → resolve role → đặt `IsManagement` → nếu `[PersistentState] Snapshot` đã có từ prerender thì bỏ qua tải lại → ngược lại `LoadAsync()`: `Busy` guard, gọi service, `UnauthorizedAccessException` → `Denied`, exception khác → `Error`, `LastRefreshed = VietnamTime.ToVietnam(Time.UtcNow())`. Nút Làm mới: nhánh thường ở `PageHeading`; nhánh quản lý ở `DashboardOverview` (`InvalidateCache()` rồi gọi lại `HomeView.LoadAsync`).

## 2. Trang chủ — Chủ trọ (`ChuTro`)

File txt: `giao-dien-home-chu-tro-hien-tai.txt`.

**Cấu trúc render (đúng thứ tự):**

1. `DashboardOverview` thay cho header của `HomeView`: `PageHeading` "Trang chủ" / "Tổng quan vận hành nhà trọ" + nút Làm mới.
2. Slot `HeaderContent` của `HomeView`: lối tắt **Ghi chỉ số / Lập hoá đơn / Tạo hợp đồng / Thêm khách thuê**; câu chú thích `WorkScopeNote` (việc cần làm áp dụng toàn bộ phạm vi, không theo bộ lọc); 3 panel: **Việc cần làm**, **Hoá đơn chưa thu**, **Hợp đồng sắp hết hạn**.
3. Thanh lọc của `DashboardOverview`: `Nhà trọ [Tất cả nhà ▼]`, `Kỳ cho báo cáo theo thời gian [Tháng này|Quý này|Năm nay ▼]`, `[⊗ Xóa bộ lọc]`; 2 dòng ghi chú `RoomScopeNote`, `CurrentPeriodNote`; dòng `Cập nhật lúc …`.
4. KPI card grid của `DashboardOverview` (`IsOwner = true`): Tổng phòng đang hoạt động (`/rooms`), Tỷ lệ lấp đầy (`/rooms`), Doanh thu ghi nhận (`/reports`), Tổng doanh thu (`/reports`), Công nợ còn phải thu (`/invoices?paymentState=unpaid`), Sự cố trong kỳ (`/reports`). Ghi chú thẻ: "Số liệu hiện tại" hoặc "Số liệu trong kỳ báo cáo đã chọn" / "Số liệu luỹ kế toàn bộ".
5. Link "Xem báo cáo phòng và khách thuê".
6. Panel: **Cơ cấu phòng** (donut Còn trống / Đang thuê / Bảo trì), **Khách thuê** (Tổng hồ sơ / Đang hoạt động / Khách đang thuê / Có tài khoản + "Xem khách thuê"), **Doanh thu theo kỳ** (full-width, chú thích "Số liệu luỹ kế toàn bộ").
7. Sidebar: Trang chủ · Nhà trọ & hợp đồng (Nhà trọ, Phòng, Khách thuê, Hợp đồng) · Tài chính (Dịch vụ, Hóa đơn, Xác nhận thanh toán) · Vận hành (Sự cố, Ra vào cổng, Kiosk cổng, Nhật ký cổng, Cấu hình cổng, Báo cáo, Trợ lý AI, Hàng đợi duyệt, Công tắc Quản lý).

**Dữ liệu:** `LoadOwnerAsync` (với `includeKpis = false`) lấy tuần tự: hoá đơn (`PageSize=20`), đếm duyệt (`ApprovalCaller(UserId, ChuTro)`), đếm sự cố mở, snapshot hợp đồng, hồ sơ tên. Panel việc cần làm: hoá đơn chưa thu `/invoices`, yêu cầu chờ duyệt `/approvals`, hợp đồng hết hạn `/contracts`, sự cố `/incidents`. Panel hoá đơn giữ `Status = Issued`, tối đa 5 dòng; panel hợp đồng lấy `EndDate ∈ [today, today+30]`, tối đa 5 dòng.

**Khác bản txt đính kèm (bản v2):** hiện tại không có nút `[+ Tạo nhanh]` trên thanh trên; lối tắt nằm ngay đầu nội dung. Không có các nhóm "SỐ LIỆU HIỆN TRẠNG / TRONG KỲ / LŨY KẾ" riêng, không có 4 biểu đồ bổ sung, không có cột đôi thu/nợ trong biểu đồ doanh thu. KPI "Doanh thu tháng" của `HomePageService` chỉ được tính khi `includeManagementKpis = true` — `HomeView` không dùng nên **không hiển thị**.

## 3. Trang chủ — Quản lý (`QuanLy`)

File txt: `giao-dien-home-quan-ly-hien-tai.txt`.

**Cấu trúc render:** giống Chủ trọ ở các mục 1–3 (cùng `DashboardOverview`, cùng thanh lọc, cùng 3 panel trong `HeaderContent`), nhưng:

- Lối tắt có 5 mục: Ghi chỉ số, Lập hoá đơn, Tạo hợp đồng, Thêm khách thuê, **Yêu cầu của tôi** (`/my/requests`).
- `DashboardOverview` đặt `IsOwner = false` → KPI grid chỉ có 3 thẻ: Tổng phòng đang hoạt động, Tỷ lệ lấp đầy, Sự cố trong kỳ. **Không có** Doanh thu ghi nhận / Tổng doanh thu / Công nợ; nếu nguồn bị từ chối, thẻ hiện "—" + "Bạn không có quyền xem báo cáo sự cố" + Retry.
- Panel cuối thay bằng **Sự cố** (full-width): Tổng sự cố / Mới / Đang xử lý / Hoàn tất / Khẩn cấp + "Số liệu trong kỳ báo cáo đã chọn" + link "Xem báo cáo phòng và khách thuê". Không có panel Doanh thu theo kỳ.
- Sidebar: Trang chủ · Nhà trọ & hợp đồng (Nhà trọ, Phòng, Khách thuê, Hợp đồng) · Tài chính (Dịch vụ, Hóa đơn, Xác nhận thanh toán) · Vận hành (Sự cố, Ra vào cổng, Kiosk cổng, Nhật ký cổng, Cấu hình cổng, Báo cáo, Trợ lý AI, **Yêu cầu của tôi**). **Không có** "Hàng đợi duyệt" và "Công tắc Quản lý" (chỉ Chủ trọ).

**Dữ liệu:** `LoadManagerAsync` (với `includeKpis = false`) lấy: hoá đơn, đếm duyệt của **chính mình** (`ApprovalCaller(UserId, QuanLy)`), đếm sự cố mở, snapshot hợp đồng, hồ sơ tên. Việc cần làm link `/invoices`, `/approvals`, `/contracts`, `/incidents`. `HomeKpiExpiring` được service tính khi bật KPI quản lý nhưng **không dùng** ở giao diện hiện tại.

## 4. Trang chủ — Khách thuê (`KhachThue`)

File txt: `giao-dien-home-khach-thue-hien-tai.txt`.

**Cấu trúc render (nhánh không quản lý):**

1. `PageHeading`: "Trang chủ" + mô tả "Tổng quan nhanh tình hình nhà trọ, phòng, hợp đồng và hóa đơn trong phạm vi tài khoản của bạn"; meta: huy hiệu **Khách thuê**, ngày Việt Nam, "Cập nhật lúc HH:mm"; nút Làm mới.
2. `<h2>Tổng quan hôm nay</h2>` + `HomeKpiGrid` 4 thẻ:
   - Hoá đơn chưa thanh toán = `Summary.IssuedCount` → `/my/invoices?paymentState=unpaid`.
   - Số tiền còn nợ = `Summary.OutstandingAmount` (định dạng ₫) → cùng link.
   - Sự cố đang xử lý = số đếm sự cố mở → `/incidents`.
   - Hợp đồng còn = số ngày đến `EndDate` của hợp đồng active sớm nhất, gợi ý "Ngày kết thúc: dd/MM/yyyy" → `/contracts`; nếu không có hợp đồng → "—" + "Chưa có hợp đồng đang hiệu lực".
3. Lối tắt: **Phòng của tôi** (`/my/room`), **Xem hoá đơn** (`/my/invoices`), **Gửi sự cố** (`/incidents`).
4. 3 panel: Việc cần làm (chỉ mục có số > 0), Hoá đơn chưa thu (link `/my/invoices?paymentState=unpaid`, mỗi dòng tiền bấm mở `/my/invoices/{id}`), Hợp đồng sắp hết hạn (link `/contracts`). Panel rỗng → `EmptyStateCard` tương ứng.
5. Sidebar: Trang chủ · Nhà trọ & hợp đồng (Hợp đồng, Phòng của tôi) · Tài chính (Dịch vụ, Hóa đơn của tôi) · Vận hành (Báo sự cố, Ra vào của tôi) · Cá nhân (Hồ sơ giấy tờ).

**Dữ liệu:** `LoadTenantAsync` với `includeKpis: false` — lấy hoá đơn, đếm sự cố, snapshot hợp đồng; `activeContracts` sắp theo `EndDate`, panel cắt tối đa 5 dòng có `EndDate ≤ today+30`. Không gọi nguồn doanh thu/phòng/khách thuê toàn hệ thống.

## 5. Trang chủ — Quản trị viên (`Admin`)

File txt: `giao-dien-home-admin-hien-tai.txt`.

**Cấu trúc render (nhánh không quản lý):**

1. `PageHeading`: "Trang chủ" + mô tả chung; meta: huy hiệu **Quản trị viên**, ngày Việt Nam, "Cập nhật lúc HH:mm"; nút Làm mới.
2. `<h2>Tổng quan hôm nay</h2>` + 4 thẻ: Tài khoản hoạt động, Chờ duyệt, Bị khoá, Tổng tài khoản — tất cả link `/admin/users`, không có gợi ý phụ.
3. Lối tắt: **Quản lý tài khoản** (`/admin/users`) và **Phân quyền** (`Href = null` → ô disabled, nhãn "Đang hoàn thiện").
4. Panel: Việc cần làm (Tài khoản chờ duyệt, Tài khoản bị khoá → `/admin/users`) và **Tài khoản chờ duyệt** (bảng 2 cột Tài khoản / Vai trò, "Xem tất cả" → `/admin/users`; rỗng → "Không có tài khoản chờ duyệt."). Không có panel hoá đơn/hợp đồng.
5. Sidebar: Trang chủ · Quản trị (Tài khoản, Chủ trọ, Nhật ký thao tác).

**Dữ liệu:** `LoadAdminAsync` chỉ gọi `IAccountService.GetSummaryAsync` + hồ sơ tên. Khi nguồn lỗi/denied, 4 KPI, danh sách việc và bảng tài khoản đều rỗng và khối hiện thông báo lỗi/quyền kèm Retry.

## 6. Trạng thái & hành vi dùng chung

| Trạng thái | Hiển thị |
|---|---|
| Trang `Loading` | `<p class="page-feedback" role="status">Đang tải</p>` |
| Trang `Error` / `Denied` | `role="alert"`: "Không tải được dữ liệu. Vui lòng thử lại." / "Bạn không có quyền xem mục này." + Retry |
| `HomeKpiGrid` `Loading` | 4 thẻ skeleton `aria-busy` |
| `HomeKpiGrid` `Error`/`Denied` + thẻ `Unavailable` | Thông báo + Retry; thẻ riêng hiện "—" + "Không tải được số liệu" + Retry (không hiện số 0 giả) |
| `HomePanel` `Loading`/`Error`/`Denied` | Skeleton / thông báo + Retry; link "Xem tất cả" chỉ khi `Loaded` |
| Danh sách rỗng | `EmptyStateCard` với tiêu đề riêng từng panel; `HomeActionList` ẩn mục `Count = 0` |
| Dashboard cards `Error`/`Denied` | "—" + thông báo (có key riêng: RoomsDenied / RevenueDenied / IncidentsDenied) + Retry |

## 7. Template khối UI (dùng chung trong spec)

| ID | Template | Cấu trúc / mẫu | Rule |
|---|---|---|---|
| T01 | Thẻ KPI | `HomeKpiGrid` ← `HomeSection<HomeKpi>`; `<a class="kpi-card kpi-link" href="@kpi.Href">` hoặc `<article class="kpi-card">` | `Hint = "{HintKey}: {Hint}"`; `Href=null` → thẻ không link; `Unavailable` → "—" + Retry |
| T02 | Việc cần làm | `HomeActionList` ← `HomeSection<HomeAction>`; nhãn + số `N0` + link | Chỉ `Count > 0`; không đọc `State` (xem §11) |
| T03 | Panel + bảng | `HomePanel` bọc `.table-scroll > table.home-table` hoặc `EmptyStateCard` | Link "Xem tất cả" chỉ khi `State=Loaded`; bảng cuộn ngang; cột tiền `.num.tabular-nums` |
| T04 | Lối tắt | `HomeQuickActions` ← `Role` (switch cứng) với icon Lucide | `Href=null` → ô disabled + "Đang hoàn thiện" |
| T05 | Thông báo trang | `.page-feedback` (`role=status` / `role=alert`) | Lỗi toàn trang do `PageState`; lỗi khối do T01/T03 |
| T06 | Vỏ dashboard | `<DashboardOverview RefreshAdditionalContent="LoadAsync" AdditionalContentBusy="Busy"><HeaderContent>…</HeaderContent></DashboardOverview>` | Bộ lọc nhà/kỳ + 6 nguồn `Task.WhenAll` + cache 45s; `IsOwner` quyết định biểu đồ doanh thu hay sự cố |

Mẫu khởi tạo KPI (trích `HomePageService`):

```csharp
kpis.Add(new("HomeKpiRented", Count(roomKpis.Rented),
    "HomeKpiTotalRooms", Count(roomKpis.Total), "/rooms"));
kpis.Add(new("HomeKpiRevenue", MoneyFormatter.Format(revenueData.TotalRevenue),
    "HomeKpiOutstanding", MoneyFormatter.Format(revenueData.Outstanding)));
```

Mẫu dòng tiền trong panel hoá đơn (trích `HomeView`):

```razor
<a class="home-due-link" href="@InvoiceHref(invoice.InvoiceId)" title="@L["OpenInvoiceTitle"]">
    <span class="tabular-nums">@MoneyFormatter.Format(invoice.Outstanding)</span>
    <LucideIcon Name="arrow-right" />
</a>
```

### 7.1 Layout CSS hiện trạng

| Lớp | Quy tắc |
|---|---|
| `.home-page` | `flex column`, gap `24px` |
| `.home-grid` | `repeat(auto-fit, minmax(320px,1fr))`, gap `24px`; `≤1199px` → 1 cột; `≤767px` → gap `16px` |
| `.home-table` + `.table-scroll` | Bảng full-width, cuộn ngang, font `0.875rem` |
| `.home-due-link` | `--warning-text`, underline khi hover, focus ring `--focus-ring` |
| `.role-badge` / `.home-meta` | Huy hiệu bo tròn, meta ngày/giờ màu `--text-secondary` |
| `.filter-bar--panel` | Card lọc có viền/bo góc/shadow; **không** sticky |

## 8. Hợp đồng dữ liệu & quy tắc nghiệp vụ

| Kiểu / quy tắc | Nội dung |
|---|---|
| `HomeLoadState` | `Loading`, `Loaded`, `Error`, `Denied` — trạng thái riêng từng khối |
| `HomeSection<T>` | `Items` + `State`; `HomeSection<T>.Loading()` = rỗng + `Loading` |
| `HomeKpi` | `Key` (resource key), `Value` (đã format), `HintKey?`, `Hint?`, `Href?`, `Unavailable` |
| `HomeAction` | `Key`, `Count`, `Href` — `Count ≤ 0` bị lọc ở UI |
| `HomeSnapshot` | `Role`, `Kpis`, `Actions`, `ExpiringContracts`, `OpenInvoices`, `PendingAccounts`, `DisplayName?` |
| State của KPI/Actions | Có item → `Loaded`; rỗng → `Combine(states)` với `Error` > `Denied` > `Loaded` |
| Hợp đồng hết hạn | `Status=Approved`, `EndDate ∈ [today, today+30]`, sort tăng, `Take(5)`; khách thuê dùng `ActiveForTenant`, `EndDate ≤ today+30` |
| Hoá đơn chưa thu | Trang 1 `PageSize=20` → giữ `Status=Issued` → `Take(5)`; số KPI dùng `Summary.IssuedCount` của toàn bộ kết quả lọc |
| Định dạng | `Count()` = `N0` với `NumberFormatter.Display`; tiền `MoneyFormatter.Format` (₫, làm tròn `AwayFromZero`); ngày `dd/MM/yyyy`; giờ VN |

## 9. i18n

| Nhóm | Key (`HomeResource`, trừ khi ghi rõ) |
|---|---|
| Trang | `Title`, `HomeDescription`, `Refresh`, `Updated`, `Loading`, `Error`, `Denied`, `Retry`, `WorkScopeNote` |
| Tiêu đề/đếm | `KpiTitle`, `KpiUnavailable`, `ActionsTitle`, `ActionsEmpty`, `QuickActionsTitle`, `OpenInvoicesTitle`, `OpenInvoicesEmpty`, `OpenInvoiceTitle`, `ExpiringTitle`, `ExpiringEmpty`, `PendingAccountsTitle`, `PendingAccountsEmpty`, `ViewAll`, `Room`, `Tenants`, `EndDate`, `DaysLeft`, `DaysLeftValue`, `Period`, `Outstanding`, `Account`, `Role` |
| KPI | `HomeKpiRented`, `HomeKpiTotalRooms`, `HomeKpiAvailable`, `HomeKpiMaintenance`, `HomeKpiTenants`, `HomeKpiTenantTotal`, `HomeKpiRevenue`, `HomeKpiOutstanding`, `HomeKpiExpiring`, `HomeKpiExpiringHint`, `HomeKpiAccountActive`, `HomeKpiAccountPending`, `HomeKpiAccountLocked`, `HomeKpiAccountTotal`, `HomeKpiUnpaidInvoices`, `HomeKpiOutstandingAmount`, `HomeKpiOpenIncidents`, `HomeKpiContractDaysLeft`, `HomeKpiContractEnd`, `HomeKpiNoContract` |
| Hành động/lối tắt | `HomeActionUnpaidInvoices`, `HomeActionPendingApprovals`, `HomeActionExpiringContracts`, `HomeActionOpenIncidents`, `HomeActionPendingAccounts`, `HomeActionLockedAccounts`, `QuickRecordReading`, `QuickCreateInvoice`, `QuickNewContract`, `QuickAddTenant`, `QuickMyRequests`, `QuickManageAccounts`, `QuickPermissions`, `QuickViewRoom`, `QuickViewInvoices`, `QuickReportIncident`, `ComingSoon` |
| Dashboard | `HomeTitle`, `Subtitle`, `Property`, `AllProperties`, `Inactive`, `PendingPeriod`, `Month`, `Quarter`, `Year`, `RoomScopeNote`, `CurrentPeriodNote`, `TotalRooms`, `Occupancy`, `Revenue`, `TotalRevenue`, `Debt`, `Incidents`, `TotalRevenue`, `AllTimeData`, `RoomStructure`, `Available`, `Rented`, `Maintenance`, `TenantTotal`, `TenantActive`, `TenantRenting`, `TenantLinked`, `ViewTenants`, `RevenueChart`, `RevenueDenied`, `IncidentsDenied`, `IncidentTotal`, `IncidentNew`, `IncidentInProgress`, `IncidentCompleted`, `IncidentUrgent`, `ViewReports` |
| Shell | `AppName`, `GlobalSearch`, `GlobalSearchPlaceholder`, `Notifications`, `Home`, `NavGroupProperty`, `NavGroupFinance`, `NavGroupOperations`, `NavGroupAdmin`, `NavGroupPersonal`, `RoleAdmin`, `RoleChuTro`, `RoleQuanLy`, `RoleKhachThue` |

Key có trong `HomeResource` nhưng **không dùng** ở `/home` hiện tại: `Empty`, `Property`, `QuickOpenDashboard`.

## 10. Thuật toán & độ phức tạp

| Khía cạnh | Hiện trạng |
|---|---|
| `HomePageService` | Các nguồn được `await` tuần tự. Chủ trọ/Quản lý với `includeKpis=false`: 5 nguồn (invoices, approvals, incidents, contracts, profile); Admin: 2; Khách thuê: 4. Nếu bật KPI quản lý thì +3 (rooms, tenants, revenue), riêng Chủ trọ lấy doanh thu tháng theo `DashboardPeriodRange` |
| `DashboardOverview` | 6 nguồn `Task.WhenAll`; `DashboardPageService` cache `IMemoryCache` 45s, key `userId:role:propertyId/kỳ`, chống stampede bằng `ListCache`; `InvalidateCache()` tăng `_cacheVersion` khi bấm Làm mới |
| Giới hạn dữ liệu | Hoá đơn `PageSize=20` rồi `Take(5)`; hợp đồng `Take(5)`; cửa sổ 30 ngày; không phân trang trên trang chủ |
| Render | O(k) với k ≤ 5 dòng/panel + số thẻ cố định; không query theo từng dòng |
| Cô lập lỗi | `TryAsync`/`TryCountAsync`: `UnauthorizedAccessException` → `Denied`, exception khác → `Error`; fallback `null`/`0`/`AccountSummaryDto.Empty` |

## 11. Điểm dễ hiểu nhầm khi đối chiếu code

- `HomePageService` có sinh KPI cho Chủ trọ/Quản lý (`HomeKpiRevenue`, `HomeKpiExpiring`…), nhưng `HomeView` gọi `includeManagementKpis: false` và ẩn khối đó — giao diện hiện tại lấy KPI từ `DashboardOverview`.
- `HomeActionList` không đọc `Section.State`: nếu nguồn lỗi nhưng danh sách rỗng, panel vẫn hiện "Không có việc nào cần xử lý." (lỗi chỉ hiện ở panel/KPI khác).
- `HomeSnapshot.DisplayName` được nạp nhưng không render trong `HomeView`; tên người dùng hiển thị ở sidebar (claim `GivenName`).
- `title="@L["OpenInvoiceTitle"]"` trên link tiền là thuộc tính `title` của trình duyệt, không phải tooltip `.hover-tip` của dự án.

## 12. Tiêu chí nghiệm thu (đối chiếu test có sẵn)

| Ca trong `HomePageTests.cs` | Kỳ vọng hiện trạng |
|---|---|
| `Khach_chua_dang_nhap_thay_landing_page` | `GET /` → 200, có nội dung landing, không có form `Input.Email` |
| `Da_dang_nhap_vao_goc_duoc_chuyen_ve_trang_chu` | Sau login, `GET /` → Redirect `/home` |
| `Chu_tro_thay_thong_ke_va_viec_can_lam_khong_trung_kpi` | Có "Cơ cấu phòng", "Việc cần làm", "Hoá đơn chưa thu", "Hợp đồng sắp hết hạn"; **không** có "Tổng quan hôm nay", "Doanh thu tháng"; chỉ 1 `.kpi-grid` |
| `Quan_ly_khong_thay_doanh_thu_va_cong_no_tong_hop` | Có "Cơ cấu phòng", "Việc cần làm"; không có "Doanh thu tháng", "Công nợ" |
| `Dashboard_cu_chuyen_ve_trang_chu` | `GET /dashboard` → Redirect `/home` |
| `Admin_thay_tai_khoan_cho_duyet_va_loi_tat_quan_ly_tai_khoan` | Có "Tài khoản chờ duyệt", "Quản lý tài khoản", "Phân quyền", "Đang hoàn thiện"; không có "Doanh thu tháng" |
| `Khach_thue_thay_hoa_don_va_loi_tat_phong_cua_minh` | Có "Hoá đơn chưa thanh toán", "Phòng của tôi"; không có "Doanh thu tháng", "Tài khoản chờ duyệt" |

Cách kiểm chứng tài liệu: đối chiếu source + file test. Theo `AGENTS.md` của repo, không tự build/chạy app — việc chạy test do người dùng thực hiện.

## 13. Ngoài phạm vi

- Landing `/`, đăng nhập/chọn ngữ cảnh (`/login`, `/Account/Login`, `/select-role`), redirect `/dashboard`.
- Layout chi tiết của topbar/sidebar, tìm kiếm toàn cục, thông báo, trợ lý AI, cài đặt tài khoản.
- Bản thiết kế v2 trong file txt đính kèm (`+ Tạo nhanh`, bộ lọc sticky, 3 nhóm số liệu, 4 biểu đồ bổ sung) — **không phải hiện trạng**.
- Bất kỳ thay đổi/tối ưu/tính năng mới — đây là bản mô tả hiện trạng.
