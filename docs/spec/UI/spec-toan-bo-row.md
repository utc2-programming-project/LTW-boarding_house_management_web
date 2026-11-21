# SPEC — Row giao diện NhaTro

> Đặc tả UI, chưa code · khảo sát 21/11/2026 · nguồn `NhaTro/Web/**/*.razor`.
> Mục đích: map mọi row → biến + điều kiện + cách apply. Không lộ trình/ước lượng.

## 0. Quy ước

| Kí hiệu | Nghĩa |
|---|---|
| `luôn` / vai trò / `có dữ liệu` | Cột hiện cho ai |
| `Template` / `Value` | Có popup/badge / text thuần (**không gắn popup được**) |
| `IsNumeric` · `Width` · `Sortable` | flag của `DataTableColumn`; có `Width` → `table-layout:fixed` |
| `che` | CCCD che theo D9 |
| `·` / `→` | tách ý / link, điều hướng |

Mỗi khổ = `###` route · `**Mẫu**` (mã trong [`template-row.md`](template-row.md)) · file/DTO/số cột/vai trò · bảng 4 cột · 1 dòng `**Áp dụng**`. Không viết thêm.

## 1. Template

Khối copy/paste: [`template-row.md`](template-row.md) — 15 mẫu `M00`–`M14`, không giải thích.

`M00` hạ tầng · `M01` tên+avatar · `M02` mã cắt sẵn · `M03` cắt 1 dòng + popup · `M04` popup 4 tầng · `M05` 2 dòng · `M06` tên nối · `M07` số/tiền · `M08` badge · `M09` che D9 · `M10` menu ⋯ · `M11` thùng rác · `M12` bảng dịch vụ · `M13` link chéo · `M14` thẻ/`<dl>`

Ràng buộc: full text luôn trong DOM (không cắt bằng JS) · `.hover-tip` phải là CSS global · popup có `role="tooltip"` + `aria-describedby` (hoặc `aria-hidden` nếu lặp nhãn).

**Hai loại tooltip, không trộn**: (1) **giải thích** — `<HelpTip>` nút `?`, nội dung không lặp lại chữ trên màn hình; (2) **đọc đầy đủ** — `.hover-tip` + `data-hover-tip`, nội dung là chính ô bị cắt. `title` native **không dùng trên ô dữ liệu** (sinh hộp đen, không theo theme, chồng popup) — chỉ còn cho nút/icon và chip có chú thích ngắn.

**Căn cột phải đều**: mọi ô trong một cột phải cùng trục với tiêu đề cột. Cột thao tác (`cell-actions`) căn giữa cho cả `th` lẫn `td`; `.p2-row-actions` nằm trong `RenderFragment` của trang nên **không nhận CSS scoped của `DataTable`** — phải căn bằng `::deep .table-card td.cell-actions .p2-row-actions { justify-content: center; }` trong CSS của trang.

---
## 2. Index toàn bộ row

> `Cột` = số cột; `*` = số cột đổi theo vai trò; `—` = không có `<table>` (ghi nhận để khỏi tìm lại).
> Mọi link file tương đối tính từ `docs/spec/UI/` → `../../NhaTro/Web/...`.

| Link trang | Tên | File | Cột | Ghi chú |
|---|---|---|---|---|
| `/tenants` | Khách thuê | `Features/Tenants/Pages/TenantList.razor` | 6/7* | D9 che CCCD; cột tài khoản chỉ Chủ trọ |
| `/tenants/trash`, `/trash` | Rác khách thuê | `.../TenantTrash.razor` | 6 | — |
| `/my/documents`, `/documents` | Tài liệu của tôi | `.../TenantDocumentsView.razor` | 5 | cả dòng là nút |
| `/my/tenant-profile` | Hồ sơ tôi | `.../MyTenantProfile.razor` | — | `<dl>` |
| `/my/room` | Phòng đang thuê | `.../MyLeasedRoomView.razor` | 3 | bảng dịch vụ |
| `/rooms` | Phòng | `Features/Rooms/Pages/RoomList.razor` | 8 | **có `data-hover-tip` mẫu** |
| `/rooms/trash` | Rác phòng | `.../RoomTrash.razor` | 7 | — |
| `/properties` | Nhà | `Features/Properties/Pages/PropertyList.razor` | 5 | số phòng là link lọc |
| `/properties/trash` | Rác nhà | `.../PropertyTrash.razor` | 5 | — |
| `/properties/new`, `/properties/{id}/setup` | Thiết lập nhà | `.../PropertySetup.razor` | — | card/grid |
| `/contracts` | Hợp đồng | `Features/Contracts/Pages/ContractListView.razor` | 6 | mobile thành card |
| `/contracts/renewals` | Gia hạn | `.../ContractRenewalQueueView.razor` | 9 | hạn đề xuất tính |
| `/contracts/pending-assignments` | Chờ lập HD | `.../PendingRoomAssignmentsView.razor` | 5 | 3 link chéo |
| `/contracts/{id}` | Chi tiết HD | `.../ContractDetailView.razor` | 4 | bảng dịch vụ |
| `/contracts/{id}/print` | Bản in | `.../ContractPrintView.razor` | 3 | chỉ dịch vụ đã áp dụng |
| `/contracts/create` | Tạo HD | `.../ContractCreateForm.razor` | 3 | bảng chỉ để xem |
| `/contracts/{id}/edit` | Sửa HD | `.../ContractEditForm.razor` | — | `<dl>` |
| `/my/contracts/{id}` | Khách xem/ký | `.../TenantContractReviewView.razor` | — | `<dl>/<ul>` |
| `/contracts/trash` | Rác hợp đồng | `.../ContractTrash.razor` | 6 | — |
| `/invoices`, `/my/invoices` | Hóa đơn | `Features/Invoices/Pages/InvoiceListView.razor` | 9 | `CanFollowLinks` |
| `/invoices/{id}` | Dòng hóa đơn | `.../InvoiceDetailView.razor` | 6 | + bảng lịch sử thanh toán 5+1 |
| `/invoices/trash` | Rác hóa đơn | `.../InvoiceTrash.razor` | 8 | — |
| `/payment-requests` | Duyệt thanh toán | `.../PaymentReviewListView.razor` | 9 | mặc định lọc Pending |
| `/payment-requests/{id}` | Chi tiết yêu cầu | `.../PaymentReviewDetailView.razor` | — | `detail-grid` |
| dialog | Sửa dòng HD | `.../Components/InvoiceRevisionDialog.razor` | 5+tfoot | ô mô tả là `<input>` |
| (component) | Thẻ tóm tắt thanh toán | `.../Components/PaymentSummaryCard.razor` | — | `<div>` row |
| `/services` | Dịch vụ | `Features/Utilities/Pages/ServiceCatalogView.razor` | 6 | bậc giá mở modal |
| `/services/prices` | Lịch sử giá | `.../ServicePricesView.razor` | 6 | — |
| `/service-registrations` | Đăng ký dịch vụ | `.../ServiceRegistrationsView.razor` | 7 | SQL lọc `?q=` |
| `/my/service-registrations` | Đăng ký của tôi | `.../MyServiceRegistrationsView.razor` | 7 | 3 ô văn bản tự do |
| `/services/readings/costs` | Tính tiền điện nước | `.../MonthlyUtilityCalculationView.razor` | 9 | +3 mini-table (4 cột) |
| `/services/trash` | Rác dịch vụ | `.../ServiceTrashView.razor` | 7 | — |
| `/services/registrations/trash` | Rác đăng ký | `.../ServiceRegistrationTrashView.razor` | 7 | — |
| (không active) | Preview chi phí / audit | `.../MeterReadingView.razor` | 5 / 5 | ⚠ không có `@page` |
| (component) | Bậc giá / tổng hợp / chi phí / audit | `Features/Utilities/Components/*.razor` | 3/8/4/4 | 4 bảng |
| `/services/readings`, `/meter-readings`, `/services/meter-reading` | Redirect | `.../MeterReadingForm.razor`, `.../MeterReadingRedirect.razor` | — | sang `/services/readings/costs` |
| `/incidents` | Sự cố | `Features/Maintenance/Components/IncidentListView.razor` | 7 | host `Pages/Incidents.razor` |
| `/incidents/trash` | Rác sự cố | `.../IncidentTrash.razor` | 7 | — |
| `/documents/trash` | Rác tài liệu | `Features/Storage/Pages/DocumentTrash.razor` | 6 | — |
| `/admin/audit-log` | Nhật ký | `Features/Audit/Pages/AuditLogView.razor` | 4/5* | ⚠ Metadata rất dài |
| `/admin/owners` | Chủ trọ | `Components/Pages/Admin/OwnerManagement.razor` | 7 | — |
| `/admin/users` | Tài khoản | `Components/Pages/Accounts/AccountManagement.razor` | 7 | menu theo `Id-Role` |
| dialog | Rác tài khoản | `.../AccountTrashDialog.razor` | 4 | giờ local |
| `/admin/manager-switches` | Quyền quản lý | `Components/Pages/ManagerSwitches.razor` | — | lưới thẻ |
| `/approvals` | Hàng đợi duyệt | `Components/Pages/ApprovalInbox.razor` → `Features/Approvals/Pages/ApprovalQueueView.razor` | — | `<ul>/<dl>` |
| `/my/requests` | Yêu cầu của tôi | `Components/Pages/MyApprovalRequests.razor` → `ApprovalRequests.razor` | — | thẻ |
| `/dev/components` | Demo DataTable | `Components/Pages/ComponentsDemo.razor` | 4 | **DEV-ONLY**, không sửa |
| `/home` | Tài khoản chờ duyệt | `Features/Home/Pages/HomeView.razor` | 2 | Admin |
| `/home` | Hóa đơn quá hạn | `.../HomeView.razor` | 3 | link theo role |
| `/home` | HĐ sắp hết hạn | `.../HomeView.razor` | 4 | `TenantNames` dài |
| `/dashboard` | KPI + biểu đồ | `Features/Dashboard/Components/*.razor`, `Features/Reports/Components/RevenueChart.razor` | — | `/dashboard` redirect `/home` |
| `/reports` | Công suất / phòng / khách / HĐ / chi phí / sự cố | `Features/Reports/Pages/BusinessReportsView.razor` | 5/4/6-7*/9/9/8 | 6 bảng |
| `/reports` | 4 báo cáo doanh thu | `Features/Reports/Components/RevenueReportView.razor` | 2/5/7/8 | — |
| `/Account/Manage/Passkeys` | Passkey | `Components/Account/Pages/Manage/Passkeys.razor` | 2 | — |
| `/Account/Manage/ExternalLogins` | Đăng nhập ngoài | `.../ExternalLogins.razor` | 2 | — |
| `/Account/Manage/Sessions` | Phiên đăng nhập | `.../Sessions.razor` | — | `<ul>`, **đã cắt 90 ký tự** |
| `/` | Landing mockup | `Features/Landing/Pages/LandingView.razor` | 3 | dữ liệu cứng, bỏ qua |

**Tổng**: ~60 bảng/row-set thật + ~12 trang không có bảng (đã ghi nhận). Một file duy nhất — không tách module.

## 3. Danh mục chi tiết

> **Chung cho mọi bảng dùng `DataTable<T>`**: `NhaTro/Web/Components/Shared/Tables/DataTable.razor` — sort qua `<button>` + `aria-sort` (`:26-44`), 5 hàng skeleton (`:54-65`), lỗi/rỗng → `colspan=Columns.Count` + `EmptyStateCard` (`:68-102`), giá trị null → `NoData` (`:117`), class `numeric` (`:276`), `AllowOverflow` cho menu `⋯` (`:166-167`). Không lặp lại ở từng khổ dưới đây.

### 3.1 Khách thuê · Phòng · Nhà

#### `/tenants` — Danh sách khách thuê
**Mẫu**: M10 · M09 · M08 · M01 · M05
[TenantList.razor](../../NhaTro/Web/Features/Tenants/Pages/TenantList.razor) · DTO: `TenantListItemDto` · Cột: 6 (7 với Chủ trọ) · Vai trò: Chủ trọ / Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Họ tên | `tenant.FullName` + avatar `Initial` + `<small>` mã + chip chờ duyệt | luôn | nút mở chi tiết; chip tone Warning (`:353-356`) |
| Điện thoại | `tenant.PhoneNumber` | luôn | — |
| Phòng | `tenant.RoomName` | luôn | link `AppLinks.RoomsByCode`, `title=CellLinkOpenRoom`; rỗng → `NoData` |
| CCCD | `tenant.IdNumberMasked` | luôn | che 4 số cuối (D9), `title=TenantIdMaskedHint` |
| Trạng thái | `tenant.IsActive` | luôn | `StatusBadge` Success/Neutral |
| Tài khoản | `tenant.HasAccount` | **Chủ trọ** | `StatusBadge` Info/Neutral; cột thêm khi `_canRevealIdNumber` (`:397-406`) |
| ⋯ | `tenant.Id` | luôn | `role=menu`, `aria-haspopup/expanded/controls`, `title`/`aria-label` kèm tên khách |

**Áp dụng**: Avatar chữ cái đầu + mã hồ sơ nằm cùng ô tên → có định danh mà không tốn cột (`:348-358`). · Chip "chờ phê duyệt" nhúng ngay cột tên, không tạo cột riêng (`:353-356`). · Cột tài khoản **biến mất với Quản lý**, bảng tự chia lại 6 cột (`:18-34` CSS, `:397-406`).

#### `/tenants/trash`, `/trash` — Thùng rác khách thuê
**Mẫu**: M11 · M10 · M08 · M07 · M03
[TenantTrash.razor](../../NhaTro/Web/Features/Tenants/Pages/TenantTrash.razor) · DTO: `TenantTrashItemDto` · Cột: 6 · Vai trò: Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên | `item.FullName` + avatar + `<small>` mã | luôn | — |
| Điện thoại | `item.PhoneNumber` | luôn | class `tenant-trash-mono` |
| Ngày xóa | `item.DeletedAt?.UtcDateTime` | luôn | `DateFormatter` |
| Tài khoản | `item.HasAccount` | luôn | `StatusBadge` Info/Neutral |
| Số tài liệu | `item.DocumentCount` | luôn | số thuần |
| Thao tác | `TrashRowMenu` | luôn | Restore / Purge |

**Áp dụng**: `<caption class="sr-only">` (`:78`) · điện thoại font mono dễ đối chiếu (`:102`) · UTC → local trước khi format (`:103`) · dùng chung `TrashRowMenu` với các thùng rác khác (`:110-111`). · cắt `…` ở `FullName`, `PhoneNumber`, ngày xóa. Chưa có `title` nào trên ô dữ liệu.

#### `/my/room`, `/my/room/{ContractId:guid}`, `/tenant/my-room` — Phòng đang thuê
**Mẫu**: M07 · M03 · M12
[MyLeasedRoomView.razor](../../NhaTro/Web/Features/Tenants/Pages/MyLeasedRoomView.razor) · DTO: `ContractServiceLineDto` · Cột: 3 · Vai trò: Khách thuê

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên dịch vụ | `service.Name` | luôn | link `/my/service-registrations` (`:350`) |
| Đơn vị | `service.Unit` | luôn | — |
| Đơn giá | `service.UnitPrice` | luôn | `MoneyFormatter`, `tabular-nums` |

**Áp dụng**: chỉ lấy dịch vụ `IsRegistered` (`:872-873`) · rỗng → thông báo thay vì bảng (`:331-333`) · lịch sử gia hạn là thẻ `renew-pending-card`, không phải bảng (`:562-596`). · cắt `…` ở `service.Name` (link); chưa có `title`/tip.

#### `/rooms` — Danh sách phòng
**Mẫu**: M10 · M08 · M07 · M13
[RoomList.razor](../../NhaTro/Web/Features/Rooms/Pages/RoomList.razor) · DTO: `RoomListItem` · Cột: 8 · Vai trò: Chủ trọ / Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Mã phòng | `item.Code` + chip phê duyệt | luôn | nút mở chi tiết; chip là link + `data-hover-tip` |
| Nhà | `item.PropertyName` | luôn | link `AppLinks.Property`, `title=CellLinkOpenProperty` |
| Tầng | `item.Floor` | luôn | `IsNumeric`; rỗng → `P2NotProvided` |
| Diện tích | `FormatArea(item)` | luôn | `IsNumeric` |
| Giá chuẩn | `MoneyFormatter.Format(StandardPrice)` | luôn | `IsNumeric` |
| Trạng thái | `item.Status` | luôn | `StatusBadge` (`StatusText/StatusTone`) |
| Hoạt động | `item.IsActive` | luôn | Success/Neutral |
| ⋯ | `item.Id` | luôn | mục xóa **chỉ Chủ trọ** (`:547-553`) |

**Áp dụng**: Chip "Chờ duyệt" là link tới đúng hàng đợi + `data-hover-tip`: tiêu đề, thao tác, mã phòng, người yêu cầu, thời gian, CTA **đổi theo vai trò** (`:503-521`). · Cột Nhà mang `PropertyId` → mở sẵn dialog chi tiết (`:527-528`).

#### `/rooms/trash` — Thùng rác phòng
**Mẫu**: M11 · M10 · M08 · M07 · M03
[RoomTrash.razor](../../NhaTro/Web/Features/Rooms/Pages/RoomTrash.razor) · DTO: `RoomTrashItem` · Cột: 7 · Vai trò: Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Mã phòng | `item.Code` | luôn | — |
| Nhà | `item.PropertyName` | luôn | — |
| Tầng | `item.Floor` | luôn | `IsNumeric`; rỗng → `P2NotProvided` |
| Giá chuẩn | `MoneyFormatter.Format(StandardPrice)` | luôn | `IsNumeric` |
| Trạng thái | `item.Status` | luôn | `StatusBadge`, dùng chung `StatusText/StatusTone` |
| Ngày xóa | `item.DeletedAt` | luôn | `DateFormatter.FormatDateTime` |
| Thao tác | `TrashRowMenu` | luôn | Restore / Purge |

**Áp dụng**: `AllowOverflow` chặn cắt menu (`:78-79`) · `_tableKey++` về trang 1 (`:129-142`) · xử lý lỗi theo mã cụ thể (trùng mã, nhà đã xóa, forbidden) (`:182-189`) · reload đúng trang hiện tại sau khôi phục (`:174-177`). · cắt `…` ở `PropertyName`, `Code`; chưa có `title`.

#### `/properties` — Danh sách nhà
**Mẫu**: M10 · M08 · M07 · M03
[PropertyList.razor](../../NhaTro/Web/Features/Properties/Pages/PropertyList.razor) · DTO: `PropertyListItem` · Cột: 5 · Vai trò: Chủ trọ / Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên nhà | `item.Name` | luôn | nút mở chi tiết, `Sortable=false` |
| Địa chỉ | `item.Address` | luôn | rỗng → `P2NotProvided` |
| Số phòng | `item.RoomCount` | luôn | link lọc phòng theo nhà, `title=CellLinkFilterRelated`, `IsNumeric` |
| Hoạt động | `item.IsActive` | luôn | `StatusBadge` |
| ⋯ | `item.Id` | luôn | Sửa/Xóa **chỉ Chủ trọ** (`:399-408`) |

**Áp dụng**: số phòng là link lọc sẵn (`:382-383`) · tắt sort trên cột tên vì đã có sort riêng (`:415`) · filter active/min/max lưu URL (`:423-440`). · cắt `…` ở `Name`, `Address` — **địa chỉ là ứng viên dài nhất**; popup nên giữ nguyên địa chỉ đầy đủ.

#### `/properties/trash` — Thùng rác nhà
**Mẫu**: M11 · M10 · M07 · M03
[PropertyTrash.razor](../../NhaTro/Web/Features/Properties/Pages/PropertyTrash.razor) · DTO: `PropertyTrashItem` · Cột: 5 · Vai trò: Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên nhà | `item.Name` | luôn | — |
| Địa chỉ | `item.Address` | luôn | rỗng → `P2NotProvided` |
| Số phòng đã xóa | `item.TrashedRoomCount` | luôn | `IsNumeric`, khác `RoomCount` active |
| Ngày xóa | `item.DeletedAt` | luôn | `DateFormatter.FormatDateTime` |
| Thao tác | `TrashRowMenu` | luôn | Restore / Purge |

**Áp dụng**: đếm riêng phòng đã xóa (`:105`) · reload đúng trang sau khôi phục (`:170-173`) · lỗi theo `PropertyErrorCodes` (`:178-184`). · cắt `…` ở `Name`, `Address`; chưa có `title`.

#### Không có bảng / chỉ skeleton
**Mẫu**: M14 · M03
- `/my/tenant-profile`, `/tenant/profile` — dùng `<dl class="tenant-profile-details">` 6 cặp (`MyTenantProfile.razor:50-56`). Ứng viên popup nếu chuyển bảng: `IdIssuedPlace`, địa chỉ.
- `/properties/new`, `/properties/{PropertyId:int}/setup` — card/grid, không `<table>`; nút bị khóa có `title=LockedReason(step)` (`PropertySetup.razor:64`).
- `DocumentTableSkeleton.razor:5` — 5 cột skeleton khớp bảng tài liệu (xem §3.5), `aria-busy=true`, không phải dữ liệu thật → **không áp dụng cắt/popup**.

### 3.2 Hợp đồng

#### `/contracts` — Danh sách hợp đồng
**Mẫu**: M10 · M08 · M07 · M06
[ContractListView.razor](../../NhaTro/Web/Features/Contracts/Pages/ContractListView.razor) · DTO: `ContractListItemDto` · Cột: 6 · Vai trò: Chủ trọ / Quản lý / Khách thuê

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Số hợp đồng | `item.ContractNumber` | luôn | link `/contracts/{id}`, `title=CellLinkOpenContract` dạng "Mở hợp đồng: {số}" (`:227`) |
| Khách thuê | `item.TenantNames` | có dữ liệu | rỗng → `—`; link `/tenants?q=` với Chủ trọ/Quản lý, text thuần với khách (`:231-246`) |
| Phòng / Tòa | `RoomCode`, `PropertyName` + `StatusBadge` | luôn | link `/rooms?code=`, `/properties?q=`; khách thuê → `<strong>/<span>` (`:255-269`) |
| Thời hạn | `StartDate` – `EndDate` | luôn | 2 dòng `DateFormatter`, `nowrap` + tabular-nums (`:273`) |
| Tài chính | `SnapshotPrice`, `DepositAmount` | luôn | `<dl>` Tiền thuê / Tiền cọc, `MoneyFormatter` (`:275-278`) |
| ⋯ | row menu | luôn | "Duyệt" chỉ Chủ trọ + `Status=Pending` (`:295`); "Xóa" chỉ Pending/Rejected (`:301`) |

**Áp dụng**: Trạng thái là **display status** theo ngày hiện tại (`ContractStatusPresentation.GetDisplayStatus(..., Clock.TodayVietnam())`) → 12 nhãn/5 tone (`:704-753`), không hiện enum thô. · 5 thẻ KPI lọc tại chỗ (giữ bảng cũ, `showLoading:false`) (`:43-95`); search debounce 300ms (`:456-473`).

#### `/contracts/renewals` — Hàng đợi gia hạn
**Mẫu**: M07 · M03
[ContractRenewalQueueView.razor](../../NhaTro/Web/Features/Contracts/Pages/ContractRenewalQueueView.razor) · DTO: `RenewalRequestItemDto` · Cột: 9 · Vai trò: Chủ trọ / Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Số hợp đồng | `item.ContractNumber` | luôn | link `/contracts/{id}` (`:99`) |
| Người yêu cầu | `item.RequesterName` | luôn | text |
| Phòng | `item.RoomCode` | luôn | text |
| Tòa nhà | `item.PropertyName` | luôn | text |
| Số tháng | `item.RequestedMonths` | luôn | `L["RenewalMonthsValue", n]`, tabular-nums |
| Hạn hiện tại | `item.CurrentEndDate` | luôn | `DateFormatter` |
| Hạn đề xuất | `CurrentEndDate.AddMonths(RequestedMonths)` | luôn | giá trị tính (DTO:55) |
| Ngày gửi | `item.RequestedAt` | luôn | `VietnamTime.ToVietnam` → `DateFormatter` |
| Thao tác | nút "Quyết định" | luôn | mở `ContractRenewalDecisionModal` |

**Áp dụng**: lọc + phân trang client (`:140-168`) · toast phân biệt "Quản lý duyệt xong"/"chờ Chủ trọ" theo `AwaitingOwnerApproval` (`:289-292`) · `min-width:1040px` cuộn ngang, `th` nowrap (css:3-5). · chưa có `title`/tip. Cắt `…` ở `RequesterName`, `PropertyName`, `ContractNumber`.

#### `/contracts/pending-assignments` — Phòng chờ lập hợp đồng
**Mẫu**: M03
[PendingRoomAssignmentsView.razor](../../NhaTro/Web/Features/Contracts/Pages/PendingRoomAssignmentsView.razor) · DTO: `PendingRoomAssignmentDto` · Cột: 5 · Vai trò: Chủ trọ / Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Khách thuê | `item.TenantName` | luôn | link `/tenants?tenantId=`, `min-width:180px` |
| Tòa nhà | `item.PropertyName` | luôn | link `/properties/{id}`, `min-width:180px` |
| Phòng | `item.RoomCode` | luôn | link `/rooms/{id}` |
| Ngày tạo | `item.CreatedAt` | luôn | `ToLocalTime().ToString("dd/MM/yyyy HH:mm")` **thủ công** |
| Thao tác | link "Lập hợp đồng" | luôn | prefill `/contracts/create?propertyId=…&roomId=…&tenantId=…` (`:49-51`) |

**Áp dụng**: cả 3 cột đầu là link chéo · nút thao tác prefill form tạo (form đọc query ở `ContractCreateForm.razor:408-472`) · rỗng → `EmptyStateCard`. · `TenantName`, `PropertyName` đã có `min-width` nhưng chưa ellipsis → cắt `…` + popup.

#### `/contracts/{ContractId}` — Bảng dịch vụ trong chi tiết
**Mẫu**: M08 · M07 · M12 · M14
[ContractDetailView.razor](../../NhaTro/Web/Features/Contracts/Pages/ContractDetailView.razor) · DTO: `ContractDetailDto` + `ContractServiceLineDto` · Cột: 4 · Vai trò: mọi vai trò thấy bảng; badge/ký/lịch sử chỉ Chủ trọ + Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên dịch vụ | `service.Name` | có dữ liệu | link `/services` (`:167`) |
| Đơn vị | `service.Unit` | có dữ liệu | — |
| Đơn giá | `service.UnitPrice` | có dữ liệu | `MoneyFormatter`, căn phải |
| Trạng thái | `service.IsRegistered` | có dữ liệu | `is-applied` / `is-muted` (`:170`) |

**Áp dụng**: đây là **bảng duy nhất** trong trang — tiền cọc/điều khoản/ngày hết hạn/lịch sử nằm trong `<dl>/<ul>`, không phải cột · `Services.Count==0` → không render bảng (`:155-158`) · badge trạng thái hợp đồng nằm ở `PageHeading`, tone tính riêng (`:646-655`).

#### `/contracts/{ContractId}/print` — Bản in (chỉ dịch vụ đã áp dụng)
**Mẫu**: M08 · M07 · M12
[ContractPrintView.razor](../../NhaTro/Web/Features/Contracts/Pages/ContractPrintView.razor) · DTO: `ContractServiceLineDto` · Cột: 3

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên dịch vụ | `service.Name` | có dữ liệu | text, không link |
| Đơn vị | `service.Unit` | có dữ liệu | — |
| Đơn giá | `service.UnitPrice` | có dữ liệu | `MoneyFormatter`, căn phải |

**Áp dụng**: chỉ in `IsRegistered == true` (`:272-273`), khác bảng chi tiết liệt kê tất cả · khổ A4 `max-width:210mm`, font 11.5px (css:24-38) · điều khoản là `<ol>`, không có badge. · bản in **không áp dụng hover** (không có tương tác); tên dịch vụ dài thì wrap tự nhiên.

#### `/contracts/create` — Bảng dịch vụ trong form tạo
**Mẫu**: M07 · M03 · M12
[ContractCreateForm.razor](../../NhaTro/Web/Features/Contracts/Pages/ContractCreateForm.razor) · DTO: `ContractServiceOptionDto` · Cột: 3 · Vai trò: Chủ trọ / Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên dịch vụ | `service.Name` | có dữ liệu | — |
| Đơn vị | `service.Unit` | có dữ liệu | — |
| Đơn giá | `service.UnitPrice` | có dữ liệu | `MoneyFormatter`, căn phải |

**Áp dụng**: 3 nhánh — chưa chọn tòa → hint (`:245-248`), rỗng → empty (`:249-252`), có → bảng · lọc dịch vụ theo `PropertyId` (`:348-350`) · bảng **chỉ để xem**, dịch vụ tự áp dụng theo giá hiện hành. · `service.Name` là ứng viên cắt; đây là bảng trong form.

#### `/contracts/trash` — Thùng rác hợp đồng
**Mẫu**: M11 · M10 · M08 · M07 · M03
[ContractTrash.razor](../../NhaTro/Web/Features/Contracts/Pages/ContractTrash.razor) · DTO: `ContractTrashItemDto` · Cột: 6 · Vai trò: **Chủ trọ**

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Số hợp đồng | `item.ContractNumber` | luôn | text, **không link** |
| Phòng / Tòa | `$"{PropertyName} / {RoomCode}"` | luôn | ghép chuỗi |
| Trạng thái | `L[StatusKey(item.Status)]` | luôn | text thuần, **không StatusBadge** |
| Thời hạn | `StartDate` – `EndDate` | luôn | `DateFormatter` |
| Xóa lúc | `item.DeletedAt` | luôn | `TrashColumns.DeletedAt`, `170px` |
| Thao tác | `TrashRowMenu` | luôn | `120px`, `cell-actions` |

**Áp dụng**: `DataTable<T>` với `ItemsProvider` · `AllowOverflow=true` (`:58`) · `_tableKey++` rebuild (`:127-142`) · purge bắt gõ đúng số hợp đồng (`:67-73`). · cắt `…` ở chuỗi ghép `PropertyName / RoomCode` (dễ dài nhất); `DataTable` render text thô, không có title/ellipsis.

#### Không có bảng
**Mẫu**: M14
`/contracts/{ContractId}/edit` dùng `dl.contract-edit-summary` (`:37-41`); `/my/contracts/{ContractId}` dùng `dl`/`ul` (`:49-134`); các modal `ContractApproveModal`, `ContractRenewalDecisionModal`, `ContractLifecycleModal` chỉ có form/nút.

### 3.3 Hóa đơn · Thanh toán

> Format chung: tiền `MoneyFormatter.Format` → `1.500.000 ₫`, ô tiền `text-right tabular-nums`; ngày thuần `dd/MM/yyyy`, thời điểm `dd/MM/yyyy HH:mm` (giờ VN) — `DateFormatter.cs:11-19`; kỳ = `Format(PeriodStart) – Format(PeriodEnd)`. Badge: Hóa đơn Draft=Neutral/Issued=Info/Paid=Success; Thanh toán Pending=Warning/Confirmed=Success/Rejected=Error.

#### `/invoices`, `/my/invoices`, `/my-invoices` — Danh sách hóa đơn
**Mẫu**: M10 · M08 · M07 · M06
[InvoiceListView.razor](../../NhaTro/Web/Features/Invoices/Pages/InvoiceListView.razor) · DTO: `InvoiceListItemDto` · Cột: 9 · Vai trò: Chủ trọ/Quản lý; Khách thuê `CanFollowLinks=false` (`:533`)

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Hóa đơn | `HD-@invoice.Id` | luôn | `cell-link`, `title=CellLinkOpenInvoice` (`:235`) |
| Kỳ | `PeriodStart`/`PeriodEnd` | luôn | `tabular-nums` |
| Hạn thanh toán | `invoice.DueDate` | Draft/null → `—` | `tabular-nums` |
| Hợp đồng | `invoice.ContractNumber` | luôn | link nếu `CanFollowLinks`, else `<span>` |
| Phòng / Nhà | `RoomCode` + badge + `PropertyName` | luôn | **badge trạng thái hóa đơn nằm trong ô này** (`:260`) |
| Khách thuê | `invoice.TenantNames` | `Count==0` → `NoTenants` | nối `", "`, link hoặc text |
| Tổng | `TotalAmount` | luôn | `text-right tabular-nums` |
| Còn lại | `OutstandingAmount` | Draft → `—`; `>0` → link wallet + arrow | — |
| ⋯ | row menu | luôn | Xóa chỉ `!IsMyInvoices` & (Draft hoặc Issued+còn nợ) (`:331-348`) |

**Áp dụng**: KPI "Còn phải thu" bấm lọc tại chỗ `?paymentState=unpaid`, không điều hướng lại (`:96-108,824-830`). · Search debounce 300ms **giữ bảng cũ** (`showLoading:false`) để không nháy overlay (`:854-876`). · URL state `q/page/year/month/status/paymentState` → link chia sẻ giữ bộ lọc (`:665-673`).

#### `/invoices/{Id}` — Bảng dòng hóa đơn
**Mẫu**: M07
[InvoiceDetailView.razor](../../NhaTro/Web/Features/Invoices/Pages/InvoiceDetailView.razor) · DTO: `InvoiceItemDto` · Cột: 6

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Loại | `ItemTypeText(item.ItemType)` | luôn | Rent/Electric/Water/Service/Discount |
| Diễn giải | `item.Description` | luôn | text thường, **không title** (`:219`) |
| Đơn vị | `item.Unit ?? "-"` | luôn | — |
| Số lượng | `Quantity.ToString("0.##")` | luôn | `text-right tabular-nums` |
| Đơn giá | `MoneyFormatter.Format(UnitPrice)` | luôn | `text-right tabular-nums` |
| Thành tiền | `MoneyFormatter.Format(Amount)` | luôn | `Amount<0` → class `discount` (`:223`) |

**Áp dụng**: dòng giảm giá tô class `discount` riêng (`:223`) · ngay dưới bảng là `summary-panel` 3 dòng, mỗi dòng có `HelpTip` (`:233-246`) · lịch sử chỉnh sửa là `<ul class="revision-list">`, không phải bảng, và là nơi duy nhất hiện `revision.Reason` (`:248-276`).

#### `/invoices/{Id}` — Bảng lịch sử thanh toán
**Mẫu**: M08 · M07 · M03
[InvoiceDetailView.razor](../../NhaTro/Web/Features/Invoices/Pages/InvoiceDetailView.razor) · DTO: `PaymentDto` · Cột: 5 · Vai trò: chỉ Chủ trọ/Quản lý (`!_caller.IsKhachThue`) và có dữ liệu (`:468,502`)

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Yêu cầu lúc | `RequestedAt.UtcDateTime` | luôn | `FormatDateTime`, tabular-nums |
| Số tiền | `payment.Amount` | luôn | `text-right tabular-nums` |
| Mã giao dịch | `TransactionCode ?? "-"` | luôn | class `break-anywhere` |
| Trạng thái | `StatusBadge(PaymentStatusText/Tone)` | luôn | Warning/Success/Error |
| Minh chứng | `ProofFileIds` → link `/api/files/{id:D}` | `Count==0` → `muted "-"` | `target=_blank rel=noopener` |

**Hàng phụ**: `Status==Rejected && RejectionReason` → `<tr class="history-reason"><td colspan="5">Lý do từ chối: …</td></tr>` (`:544-549`).

**Áp dụng**: `break-anywhere` cho mã giao dịch · lý do từ chối tách hàng riêng thay vì nhét vào cột · link minh chứng mở tab mới có `rel=noopener`. · `RejectionReason` (`:547`) cắt `…` + popup; `ProofName` (`:538`) ứng viên phụ.

#### `/invoices/trash` — Thùng rác hóa đơn
**Mẫu**: M11 · M07
[InvoiceTrash.razor](../../NhaTro/Web/Features/Invoices/Pages/InvoiceTrash.razor) · DTO: `InvoiceTrashItemDto` · Cột: 8 · Vai trò: Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Hóa đơn | `$"HD-{item.Id}"` | luôn | — |
| Hợp đồng | `item.ContractNumber` | luôn | — |
| Phòng/Nhà | `$"{PropertyName} / {RoomCode}"` | luôn | ghép chuỗi (`:106`) |
| Kỳ | `Format(PeriodStart) – Format(PeriodEnd)` | luôn | — |
| Tổng | `MoneyFormatter.Format(TotalAmount)` | luôn | `IsNumeric` |
| Trạng thái | `L[$"Status{item.Status}"]` | luôn | — |
| Xóa lúc | `TrashColumns.DeletedAt` | luôn | `170px` |
| Thao tác | `TrashColumns.Actions` | luôn | `120px`, `cell-actions` |

**Áp dụng**: `ItemsProvider` (`:115-119`) · `AllowOverflow=true` (`:53`) · `_tableKey++` (`:127-131`) · purge bắt gõ `HD-{id}` (`:62-68`), "Dọn sạch" bắt gõ cụm từ (`:70-76`).

#### `/payment-requests` — Hàng đợi duyệt thanh toán
**Mẫu**: M10 · M08 · M07 · M06
[PaymentReviewListView.razor](../../NhaTro/Web/Features/Invoices/Pages/PaymentReviewListView.razor) · DTO: `PaymentReviewItemDto` · Cột: 9 · Vai trò: Chủ trọ/Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Yêu cầu | `YC-@item.Payment.Id` | luôn | link, `title=CellLinkOpenPayment` |
| Hóa đơn | `HD-@item.InvoiceId` | luôn | link + title |
| Phòng/Nhà | `RoomCode` + `PropertyName` | luôn | 2 link + title |
| Khách thuê | `item.TenantNames` | `Count==0` → `NoTenants` | nối `", "` (`:163-178`) |
| Kỳ | `PeriodStart`/`PeriodEnd` | luôn | tabular-nums |
| Yêu cầu lúc | `RequestedAt.UtcDateTime` | luôn | `FormatDateTime` |
| Số tiền | `item.Payment.Amount` | luôn | `text-right tabular-nums` |
| Trạng thái | `StatusBadge` | luôn | Warning/Success/Error |
| ⋯ | row menu | luôn | chỉ "Xem chi tiết" (`:198-205`) |

**Áp dụng**: 4 thẻ KPI bấm lọc trực tiếp, "Tiền chờ duyệt" là tổng Pending chưa thu · mặc định chỉ hiện `Pending` (`:243`) → giảm nhiễu hàng đợi · debounce + giữ bảng cũ (`:441-463`) · URL state đầy đủ (`:363-372`).

#### Dialog `InvoiceRevisionDialog` — Sửa dòng hóa đơn
**Mẫu**: M07
[InvoiceRevisionDialog.razor](../../NhaTro/Web/Features/Invoices/Components/InvoiceRevisionDialog.razor) · DTO: `ReviseLine` (private) · Cột: 5 + 1 hàng tfoot · Vai trò: Chủ trọ, chỉ khi khách chưa gửi yêu cầu thanh toán

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Loại | `ItemTypeText(line.ItemType)` | luôn | read-only |
| Diễn giải | `<input maxlength="250">` `line.Description` | luôn | editable |
| Số lượng | `<input type=number step=0.01 min=0.01>` | luôn | `max-width:110px` |
| Đơn giá | `<input type=number step=1000 min=0>` | luôn | `max-width:140px` |
| Thành tiền | `MoneyFormatter.Format(LineAmount(line))` | luôn | `text-right tabular-nums`, read-only |
| tfoot | `colspan=4` "Tổng cộng" + `Format(NewTotal)` | luôn | — |

**Áp dụng**: `LineAmount` tự đảo dấu `Discount` (`:137-140`) · `ParseDecimal` dùng InvariantCulture (`:142-143`) · gửi `Amount = null` để server tự tính (`:183`) · bắt nhập lý do (textarea `maxlength=1000`, `:73-80`).

#### Không có bảng / component thẻ
**Mẫu**: M14 · M03
- `/payment-requests/{PaymentId}` — `detail-grid` + `summary-panel`, không `<table>`; `TenantNames` (`:143-148`) là ứng viên popup nếu đồng bộ.
- `PaymentSummaryCard.razor:3-56` — 5-6 `<div class="payment-summary-row">`, không bảng. Ẩn hẳn dòng mã giao dịch/duyệt lúc/lý do khi không có (`:16,23,31`) → thẻ luôn gọn. `RejectionReason` (`:35`) là ứng viên cắt + popup; fallback tên tệp `PaymentProofOpen` không lộ đường dẫn nội bộ.

### 3.4 Tiện ích · Điện nước · Dịch vụ

> Format chung: tiền `MoneyFormatter.Format` (`N0` + "." ngàn + " ₫", làm tròn VND — `MoneyFormatter.cs:10-23`); số đo `0.##` invariant qua `FormatNumber`/`FormatUsage(..., "kWh"|"m³")`; căn phải `text-right tabular-nums` hoặc class `numeric` (`DataTable.razor.css:71-76`).

#### `/services` — Danh mục dịch vụ
**Mẫu**: M10 · M08 · M07 · M12
[ServiceCatalogView.razor](../../NhaTro/Web/Features/Utilities/Pages/ServiceCatalogView.razor) · DTO: `ServiceItemDto` · Cột: 6 · Vai trò: lọc theo tòa/loại/trạng thái; sửa theo `UtilityManage`

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên dịch vụ | `service.Name`, `Description`, badge nguồn | link luôn; mô tả khi có; badge khi `_isSampleData` | link `title=CellLinkOpenService` (`:204`); badge tự wrap (`:211`) |
| Loại + Đơn vị | resource key theo enum | luôn | pill `meter`/`fixed` |
| Đơn giá | `MoneyFormatter.Format(CurrentRate)` hoặc nút "Xem bậc giá" | luôn | `text-right cell-rate tabular-nums` (`:229`) |
| Thời hạn | `EffectiveFrom` – `EffectiveTo ?? NoEndDate` | luôn | `tabular-nums`, `nowrap` |
| Kích hoạt | chip khoá / switch `role="switch"` / text tĩnh | readonly / `UtilityManage` / khôngAuthorized | switch có `title=` (`:273`) |
| ⋯ | menu: Xem, Đăng ký, Lịch sử, Sửa, Xoá | Đăng ký chỉ `KhachThue && !readonly && !Meter`; Sửa/Xoá chỉ `UtilityManage` | trigger có `title=` (`:290`) |

**Áp dụng**: biểu giá bậc thang không hiện số mà là nút mở `ServiceTierModal` (`:230-235`) · dịch vụ readonly dùng chip khoá thay vì switch disable (`:255-262`) · link `?q=điện` tự apply lại filter vì Blazor reuse trang (`:533-548`) · cho phép dịch vụ dùng chung `BuildingId=="all"` (`:731-734`).

#### `/services/prices` — Lịch sử bảng giá
**Mẫu**: M08 · M07
[ServicePricesView.razor](../../NhaTro/Web/Features/Utilities/Pages/ServicePricesView.razor) · DTO: `PriceVersionDto` · Cột: 6 · Vai trò: `UtilityBusiness`

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Hiệu lực từ | `DateFormatter.Format(EffectiveFrom)` + badge nguồn | luôn | `tabular-nums font-semibold` |
| Hiệu lực đến | `Format(EffectiveTo)` / `NoData` italic | `EffectiveTo.HasValue` | — |
| Đơn giá | `MoneyFormatter.Format(Rate)` + ` / ` + unit / nhãn bậc | luôn | `text-right tabular-nums font-bold` (`:200`) |
| Trạng thái | `status-badge-chip @GetStatusBadgeClass` | luôn | Active/Upcoming/Expired |
| Phạm vi | `BuildingScope=="all" ? AllBuildings : BuildingName` | luôn | — |
| Ghi chú | `v.Note` / `NoData` | luôn | italic, chưa có title |

**Áp dụng**: lọc ngày là lọc **giao nhau**, bản giá chưa có `EffectiveTo` vẫn không bị loại (`:442-455`) · `?serviceId=` chỉ áp dụng lần đầu (`:297-298`) · sau lưu khôi phục cả filter + page (`:542-546`) · 4 KPI thẻ bấm đổi filter trạng thái (`:64-98`).

#### `/service-registrations` — Quản lý yêu cầu đăng ký
**Mẫu**: M10 · M08 · M07 · M03 · M02
[ServiceRegistrationsView.razor](../../NhaTro/Web/Features/Utilities/Pages/ServiceRegistrationsView.razor) · DTO: `ServiceRegistrationDto` · Cột: 7 · Vai trò: Chủ trọ/Quản lý duyệt, từ chối, gửi hóa đơn

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Mã yêu cầu | `#@row.Id` | luôn | tabular-nums |
| Khách thuê | `TenantNames` link + `TenantPhones` + `ContractNumber` link | phones khi có | contract `is-code` **đã có ellipsis + title** (app.css:1054-1062) |
| Phòng / Dịch vụ | `ServiceName`, `RoomCode`, `PropertyName` link | luôn | 3 link đều có `title=` (`:93,96,99`) |
| Số lượng | `Quantity.ToString("0.##")` | luôn | căn giữa tabular-nums |
| Yêu cầu lúc | `FormatDateTime(RequestedAt.UtcDateTime)` | luôn | — |
| Trạng thái | `status-badge` + badge "chờ hủy" | khi `Approved && CancelStatus==Pending` | badge chồng lên badge trạng thái |
| ⋯ | menu "Chi tiết" | luôn | trigger có `title=` (`:115`) |

**Áp dụng**: `?q=` lọc ngay ở SQL (`:355-357`) · xoá/duyệt làm rỗng trang cuối → tự lùi trang (`:374-380`) · detail modal 4 nhánh footer theo trạng thái (`:255-297`) · preview hoá đơn + VietQR theo hợp đồng (`:493-507`). · `TenantNames` nối `", "` (`:76-80`) → cắt `…` + popup (nên xuống dòng từng tên).

#### `/my/service-registrations` — Lịch sử đăng ký của tôi
**Mẫu**: M08 · M07 · M03
[MyServiceRegistrationsView.razor](../../NhaTro/Web/Features/Utilities/Pages/MyServiceRegistrationsView.razor) · DTO: `ServiceRegistrationDto` · Cột: 7 · Vai trò: Khách thuê

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Phòng / Dịch vụ | `ServiceName`, `RoomCode · PropertyName`, kỳ, `Note` | Note khi có (`:63`) | 4 dòng xếp chồng |
| Số lượng | `Quantity.ToString("0.##")` | luôn | `text-right tabular-nums` |
| Yêu cầu lúc | `FormatDateTime(...)` | luôn | — |
| Trạng thái | `status-badge` | luôn | — |
| Thông tin duyệt | `RejectReason` / `FormatDateTime(ReviewedAt)` / `—` | Rejected / có ReviewedAt / khác | tái dùng 1 ô cho 2 mục đích (`:72-83`) |
| Hoá đơn | link `HD-@invoiceId` / `—` | `InvoiceId is { }` | — |
| Thao tác | Hủy / badge chờ hủy / Yêu cầu hủy / lý do từ chối | theo `Status` + `CancelStatus` | `text-right` |

**Áp dụng**: lùi trang khi hủy làm rỗng trang (`:191-196`) · `CancelStatus==Rejected` vừa hiện lý do vừa cho gửi lại (`:115-121`) · modal hủy bắt buộc lý do + confirm discard (`:135-165`).

#### `/services/readings/costs` — Bảng chi phí theo phòng (bảng chính)
**Mẫu**: M10 · M08 · M07 · M06
[MonthlyUtilityCalculationView.razor](../../NhaTro/Web/Features/Utilities/Pages/MonthlyUtilityCalculationView.razor) · DTO: `MonthlyUtilityRoomCostDto` · Cột: 9 · Vai trò: `UtilityBusiness`

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Phòng | `RoomCode`, `PropertyName`, `TenantNames` + badge | TenantNames khi có | mọi `<td>` có `data-label=` cho mobile |
| Điện tiêu thụ | `FormatUsage(ElectricUsage,"kWh")` / `NoData` | `HasReadings` | `text-right tabular-nums` |
| Tiền điện | `MoneyFormatter.Format(ElectricCost)` | `HasReadings` | căn phải + `amount` |
| Nước tiêu thụ | `FormatUsage(WaterUsage,"m³")` | `HasReadings` | căn phải |
| Tiền nước | `MoneyFormatter.Format(WaterCost)` | `HasReadings` | căn phải + `amount` |
| Dịch vụ | `Format(ServiceCost)` + `service-count` | `ServiceLines.Count>0`, else `NoData` | căn phải |
| Tổng cộng | `MoneyFormatter.Format(TotalAmount)` | luôn | căn phải + `total-cell` |
| Trạng thái HD | link "Đã lập" / pill "Chưa lập" | `InvoiceId` | — |
| ⋯ | Chi tiết / Sửa chỉ số / Lập hoá đơn | Sửa khi `!IsInvoiced && (CanAdjust \|\| !HasReadings)`; lập HD khi `InvoiceId is null` | nút lập HD có `title=` khi thiếu chỉ số (`:280`) |

**Áp dụng**: mỗi dòng là **một hợp đồng** — một phòng có thể có nhiều hợp đồng trong kỳ → `@key="room.ContractId"` (`:165`) · "Chưa có phiếu" tách khỏi "Đang mở" (`:1402-1410`) · KPI tổng tiền lấy trên danh sách **đã lọc** (`:81`).

#### `/services/readings/costs` › 3 mini-table trong modal chi tiết
**Mẫu**: M03
[MonthlyUtilityCalculationView.razor](../../NhaTro/Web/Features/Utilities/Pages/MonthlyUtilityCalculationView.razor) · mỗi bảng 4 cột · DTO: `MonthlyUtilityTierDto`, `MonthlyUtilityCorrectionDto`

| Bảng | File:line | Cột | Ghi chú |
|---|---|---|---|
| Bậc giá điện | `:351` | Bậc / Số lượng (kWh) / Đơn giá / Thành tiền | chỉ render khi `ElectricTiers.Count>0` (`:349`) |
| Bậc giá nước | `:386` | Bậc / Số lượng (m³) / Đơn giá / Thành tiền | cùng grid, khác đơn vị (`:400`) |
| Điều chỉnh / hiệu đính | `:468` | Trường / Chênh lệch (`old → new`) / Lý do / Thời điểm | không có hiệu đính → câu `CorrectionsEmpty` (`:461-463`) |

**Áp dụng**: chip đếm phân biệt số ít/nhiều (`:459`) · `FormatCorrection` dùng chung `FormatNumber 0.##` (`:1433-1438`) · nút "Hiệu đính kỳ trước" chỉ hiện khi `CanCorrectPrevious && PreviousReading != null` (`:500-505`).

#### Bảng preview chi phí & audit chỉ số (`MeterReadingView`)
**Mẫu**: M03 · M01
[MeterReadingView.razor](../../NhaTro/Web/Features/Utilities/Pages/MeterReadingView.razor) · ⚠ **không có `@page`**, không bị file nào tham chiếu — dữ liệu mẫu cũ, không có route active

| Bảng | File:line | Cột | Ghi chú |
|---|---|---|---|
| Tính chi phí xem trước | `:275` | Loại chi phí / Công thức / Số lượng / Đơn giá / Thành tiền | 6 dòng (`CostLineDto`); tổng nằm ngoài bảng ở `cost-total-banner` (`:310-319`) |
| Log kiểm toán | `:344` | Người sửa / Thời gian / Trường / Thay đổi / Lý do | `<AuthorizeView Policy="UtilityRecord">` (`:328-329`); rỗng → `NoAuditLogs` (`:383-387`) |

**Áp dụng**: `GetCostFormula()` nối các bậc bằng `" + "` (`:669-677`) → ô công thức dài nhất · đơn giá điện in chữ ghi chú thay vì số (`:665-667`) · audit avatar 1 ký tự, khác `RoomReadingCard` (2 ký tự).

#### `/services/trash` — Thùng rác dịch vụ
**Mẫu**: M11 · M10 · M07 · M03
[ServiceTrashView.razor](../../NhaTro/Web/Features/Utilities/Pages/ServiceTrashView.razor) · DTO: `ServiceCatalogTrashItemDto` · Cột: 7 · Vai trò: Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên dịch vụ | `item.Name` | luôn | `col.Value` thuần |
| Tòa nhà | `item.PropertyName` | luôn | `col.Value` thuần |
| Đơn vị | `item.Unit` | luôn | — |
| Đơn giá | `UnitPrice.ToString("N0", CurrentCulture)` | luôn | `IsNumeric` → class `numeric` |
| Số phiên bản giá | `PriceVersionCount.ToString(...)` | luôn | `IsNumeric` |
| Xóa lúc | `TrashColumns.DeletedAt` | luôn | `170px` |
| Thao tác | `TrashRowMenu` | luôn | `120px`, `AllowOverflow=true` (`:49`) |

**Áp dụng**: cột build trong C# qua `BuildColumns()` (`:93-108`) · `@key="_tableKey"` rebuild khi đổi search/size (`:122-136`) · purge bắt gõ đúng tên dịch vụ (`:58-64`). · `Name`, `PropertyName` qua `col.Value` → **không có title, không cắt**; cần thêm `CellClass`/`Template` rồi áp popup.

#### `/services/registrations/trash` — Thùng rác yêu cầu đăng ký
**Mẫu**: M11 · M10 · M07 · M03
[ServiceRegistrationTrashView.razor](../../NhaTro/Web/Features/Utilities/Pages/ServiceRegistrationTrashView.razor) · DTO: `ServiceRegistrationTrashItemDto` · Cột: 7 · Vai trò: Chủ trọ/Quản lý

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Hợp đồng | `item.ContractNumber` | luôn | — |
| Phòng | `item.RoomCode` | luôn | — |
| Tòa nhà | `item.PropertyName` | luôn | — |
| Dịch vụ | `item.ServiceName` | luôn | — |
| Tháng | `DateFormatter.FormatMonth(MonthStart)` | luôn | format sẵn ở `Value` |
| Xóa lúc | `TrashColumns.DeletedAt` | luôn | — |
| Thao tác | `TrashRowMenu` | luôn | — |

**Áp dụng**: khác rác dịch vụ ở chỗ **không có cột `IsNumeric`** · `ErrorText` map 4 mã lỗi (`:256-263`) · restore/purge tách `ServiceRegistrationException` và `UnauthorizedAccessException` (`:182-186,210-214`). · `ContractNumber`, `PropertyName`, `ServiceName` đều text thuần → cắt `…` + popup.

#### Component tiện ích có bảng
**Mẫu**: M08 · M03 · M01

| Bảng | File:line | Cột | Ghi chú |
|---|---|---|---|
| Bậc giá dịch vụ (modal) | `ServiceTierModal.razor:27` | 3 | `To=null` → "từ X trở lên", không in `∞` (`:53-56`); empty `colspan=3` (`:36-41`) |
| Tổng hợp chỉ số phòng | `RoomSummaryTable.razor:18` | 8 | badge bất thường **đã có `title=`** (`:53,77`); `DraftReadings` đè chỉ số đã lưu (`:124-131`) |
| Chi phí 1 phòng | `MonthlyUtilityTable.razor:25` | 4 | 3 nhánh điện + 3 nhánh nước; dịch vụ chỉ render khi `Cost>0` |
| Audit 1 phòng | `RoomReadingCard.razor:287` | 4 | ẩn với `KhachThue` và khi `Count==0` (`:276`); avatar 2 ký tự; `FieldLabel` + `DetailDiff` gộp **1 cột** |

**Áp dụng**: `TenantName` (`RoomSummaryTable:38`), `log.Reason` (`RoomReadingCard:314`), cột gộp `FieldLabel: DetailDiff` (`:311-312`) → cắt `…` + popup.

#### Không có bảng / redirect
**Mẫu**: M14
- `MeterReadingForm.razor:1-10` (`/services/readings`, `/meter-readings`) và `MeterReadingRedirect.razor:1-9` (`/services/meter-reading`): chỉ redirect sang `/services/readings/costs`.
- `ServiceCreateModal.razor`, `ServicePriceModal.razor`: form + `<div>` summary, không `<table>`.

### 3.5 Sự cố · Tài liệu · Kiểm toán · Admin

#### `/incidents` — Danh sách sự cố (`IncidentListView`)
**Mẫu**: M08 · M07 · M03
[IncidentListView.razor](../../NhaTro/Web/Features/Maintenance/Components/IncidentListView.razor) · DTO: `IncidentItem` · Cột: 7 · Vai trò: Chủ trọ / Quản lý / Khách thuê (`CanFollowLinks=false` với khách, `:130`)

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tiêu đề | `Text(i, i.Title)` | luôn | `Sortable` |
| Phòng | `RoomCell` | luôn | `Sortable`; `<a>` nếu `CanFollowLinks`, else `<span>` (`:259-268`) |
| Danh mục | `L[$"Category{i.Category}"]` | luôn | — |
| Mức khẩn | `L[$"Urgency{i.Urgency}"]` | luôn | — |
| Trạng thái | `StatusBadge` + chip `PendingApproval` | chip khi `i.Pending.Count > 0` (`:175-178`) | — |
| Ngày tạo | `DateFormatter.FormatDateTime(i.CreatedAtUtc)` | luôn | `Sortable` |
| Xem chi tiết | nút `@ref=_detailButtons[i.Id]` | luôn | `aria-label=ViewDetailNamed` (`:181-183`) |

**Áp dụng**: Filter lưu lên URL `q/room/category/urgency/status/created` (`:189-232`) + debounce (`:288-306`) → chia sẻ link giữ ngữ cảnh. · `RestoreDetailFocusAsync` trả focus đúng nút "Xem chi tiết" sau mỗi reload DOM (`:236-250`). · Cắt `…`: `Title` (dài). Popup: tiêu đề đầy đủ + mô tả ngắn.

#### `/incidents/trash` — Thùng rác sự cố
**Mẫu**: M11 · M10 · M07 · M03
[IncidentTrash.razor](../../NhaTro/Web/Features/Maintenance/Pages/IncidentTrash.razor) · DTO: `IncidentTrashItemDto` · Cột: 7 · Vai trò: Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tiêu đề | `item.Title` | luôn | — |
| Phòng | `$"{PropertyName} / {RoomCode}"` | luôn | ghép chuỗi |
| Danh mục | `L[$"Category{item.Category}"].Value` | luôn | — |
| Trạng thái | `L[$"Status{item.Status}"].Value` | luôn | — |
| Ngày tạo | `DateFormatter.FormatDateTime(CreatedAt)` | luôn | — |
| Xóa lúc | `TrashColumns.DeletedAt(DeletedAt)` | luôn | `Width=170px` (`TrashColumns.cs:10-17`) |
| Thao tác | `TrashRowMenu` | luôn | `cell-actions`, `Width=120px` (`:19-27`) |

**Áp dụng**: `@key="_tableKey"` reset phân trang khi đổi tìm kiếm/số dòng (`:47,118-131`). · Xóa vĩnh viễn bắt **gõ đúng tiêu đề sự cố** (`:57-63`). · `AllowOverflow=true` (`:48`) để menu `⋯` không bị khung cắt. · Tách lỗi `IncidentPageException` / `UnauthorizedAccessException`, log riêng (`:178-188`).

#### `/documents/trash` — Thùng rác tài liệu
**Mẫu**: M11 · M10 · M07
[DocumentTrash.razor](../../NhaTro/Web/Features/Storage/Pages/DocumentTrash.razor) · DTO: `StoredFileTrashItemDto` · Cột: 6 · Vai trò: Chủ trọ / Quản lý / Khách thuê

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tệp | `item.FileName` | luôn | — |
| Danh mục | `item.FileCategory.ToString()` | luôn | **chưa localize** |
| Liên quan | `$"{RelatedEntityType} #{RelatedEntityId}"` | luôn | — |
| Tải lên | `DateFormatter.FormatDateTime(item.UploadedAt.UtcDateTime)` | luôn | — |
| Xóa lúc | `TrashColumns.DeletedAt` | luôn | `170px` |
| Thao tác | `TrashRowMenu` | luôn | `cell-actions`, `120px` |

**Áp dụng**: Pagination kiểu client `Skip/Take` (`:129-139`) — khác `IncidentTrash` đẩy cho server. · Xác nhận xóa bắt gõ đúng `FileName` (`:75-81`); `AllowOverflow=true` (`:66`). · `ErrorText` map 5 `StorageErrorCodes` (`:257-265`).

#### `/my/documents`, `/documents` — Tài liệu của tôi
**Mẫu**: M07
[TenantDocumentsView.razor](../../NhaTro/Web/Features/Tenants/Pages/TenantDocumentsView.razor) · DTO: `TenantDocumentMetadataDto` · Cột: 5 · Vai trò: Khách thuê

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Loại | `doc-type-pill` + `GetCategoryLabel` | luôn | `:258-262` |
| Tên tệp | `doc.FileName` + subtitle Ảnh/PDF | luôn | `:265-286` |
| Kích thước | `NumberFormatter.FileSize(FileSizeBytes)` | luôn | `text-right tabular-nums` |
| Tải lên | `DateFormatter.FormatDateTime(item.UploadedAt.UtcDateTime)` | luôn | `text-right tabular-nums` |
| Thao tác | eye / download / trash | download khi `CanDownload`, delete khi `CanDelete` (`:312,324`) | `stopPropagation` (`:299`) |

**Áp dụng**: Cả hàng là nút (`tabindex`, `role=button`, Enter/Space) (`:256,612-621`); ô thao tác chặn sự kiện để không mở preview (`:299`). · 3 nút đều có `title=` + `aria-label` (`:305-330`). · Export CSV chặn formula injection `=+-@` (`:750-763`).

#### `/admin/audit-log` — Nhật ký kiểm toán
**Mẫu**: M07 · M03 · M13
[AuditLogView.razor](../../NhaTro/Web/Features/Audit/Pages/AuditLogView.razor) · DTO: `AuditLogEntryDto` · Cột: 4 (Admin) / 5 (Chủ trọ) · Vai trò: Admin / Chủ trọ

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Thời gian | `DateFormatter.FormatDateTime(entry.OccurredAt.UtcDateTime)` | luôn | — |
| Người tạo | `ActorDisplayName ?? "—"` | luôn | — |
| Hành động | `ActionLabel(entry.Action)` | luôn | fallback trả key nếu thiếu resource (`:221-225`) |
| Tài sản | link `AppLinks.Property(propertyId)` | **chỉ Chủ trọ** (`:192-209`) | thiếu → `—` |
| Metadata | `entry.Metadata ?? "—"` | luôn | **JSON thô, rất dài** (`:211-216`) |

**Áp dụng**: Cột Metadata trả nguyên payload qua `Value`, không format/parse/cắt (`:211-216`) — nơi cần cắt + popup nhất toàn dự án. · Số cột đổi theo vai trò, tự rebuild khi `_isOwner` lệch sau lần load (`:145-148`).

#### `/admin/owners` — Quản lý Chủ trọ
**Mẫu**: M08 · M07
[OwnerManagement.razor](../../NhaTro/Web/Components/Pages/Admin/OwnerManagement.razor) · DTO: `OwnerListItemDto` · Cột: 7 · Vai trò: Admin

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên | `FullName ?? L["NoData"]` | luôn | — |
| Email | `Email ?? L["NoData"]` | luôn | — |
| Số tài sản | `PropertyCount.ToString` | luôn | `IsNumeric` |
| Số quản lý | `ManagerCount.ToString` | luôn | `IsNumeric` |
| Số khách thuê | `TenantCount.ToString` | luôn | `IsNumeric` |
| Trạng thái | `StatusBadge` (Locked/Active) | luôn | `:108-109` |
| Thao tác | nút lock/unlock | luôn | icon đổi theo `IsLocked` (`:116-119`) |

**Áp dụng**: debounce 300ms + hủy `CancellationTokenSource` khi dispose (`:138-158`) · `_tableKey++` đưa tìm kiếm về trang 1 (`:131-136`) · 3 cột số `IsNumeric` căn phải (`:87,94,101`) · `ConfirmDialog` đổi tone `Danger` theo chiều khóa/mở (`:46-53`).

#### `/admin/users` — Quản lý tài khoản
**Mẫu**: M10 · M08 · M05
[AccountManagement.razor](../../NhaTro/Web/Components/Pages/Accounts/AccountManagement.razor) · DTO: `AccountListItemDto` · Cột: 7 · Vai trò: `[Authorize]`; quyền do `AccountPermissionRules` chốt server (`:269-274`)

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tài khoản | `FullName ?? UserName` + `<small>UserName` | luôn | `Sortable` |
| Email | `user.Email` | luôn | — |
| Điện thoại | `PhoneNumber ?? NoData` | luôn | — |
| Vai trò | `StatusBadge` + `RoleTone` | luôn | `Sortable` |
| Chủ trọ/TS | template theo role | Quản lý: badge `unassigned` hoặc Owner + TS; Chủ trọ: `OwnerSelf` + TS sở hữu; role khác: `NotApplicable` (`:314-340`) | — |
| Trạng thái | `StatusBadge` + `StatusTone` | luôn | `:346-347` |
| Thao tác | menu `⋯` | ẩn với chính mình (`:354-359`); mục theo quyền (`:377-421`) | `title=` + `aria-haspopup/expanded/controls` (`:366-371`) |

**Áp dụng**: `_openMenuId` theo `RowKey = "{Id}-{Role}"` → một user nhiều vai trò không mở menu cùng lúc (`:195-197,453-458`). · "Chủ trọ tự gán mình" hiện nhãn thay vì menu (`:354-359`); ô Chủ trọ/TS đổi hẳn theo role (`:314-340`). · Từ chối/khóa bắt nhập lý do `maxlength=500` (`:108-119,147-161`).

#### Dialog — Thùng rác tài khoản (không có route riêng)
**Mẫu**: M11 · M10 · M03
[AccountTrashDialog.razor](../../NhaTro/Web/Components/Pages/Accounts/AccountTrashDialog.razor) · DTO: `DeletedAccountDto` · Cột: 4 · Vai trò: mở từ Admin

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Email | `item.OriginalEmail` | luôn | — |
| Họ tên | `FullName ?? NoData` | luôn | — |
| Ngày xóa | `DeletedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "-"` | luôn | giờ local, khác chỗ khác dùng UTC (`:44`) |
| Thao tác | `TrashRowMenu` | luôn | khôi phục / xóa vĩnh viễn |

**Áp dụng**: chỉ tải khi `Visible` chuyển `false→true` (`:87-95`) · xóa bắt gõ đúng email gốc (`:58-64`). · cắt `…` ở `OriginalEmail`, `FullName`.

#### `/dev/components` — DataTable demo (DEV-ONLY)
**Mẫu**: M08 · M07 · M03
[ComponentsDemo.razor](../../NhaTro/Web/Components/Pages/ComponentsDemo.razor) · DTO: `DemoRoom` · Cột: 4 · Vai trò: chỉ `Env.IsDevelopment()` (`:20`), không auth

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Mã phòng | `r.Code` | luôn | `Sortable` |
| Trạng thái | `_statusCell` → `StatusBadge` | luôn | `Sortable` |
| Giá thuê | `MoneyFormatter.Format(r.Price)` | luôn | `IsNumeric`, `Sortable` |
| Cập nhật | `DateFormatter.Format(r.Updated)` | luôn | `Sortable` |

**Áp dụng**: giả độ trễ 400ms để thấy skeleton (`:184-187`) · rule "trùng mã" đưa vào `ValidationMessageStore` (`:171-173,217-225`) · `_rooms` là field instance, không static (`:145-146`). · không có cột văn bản dài; dev-only → không áp dụng template cắt/popup.

#### Không có bảng (đã rà, ghi nhận để khỏi tìm lại)
**Mẫu**: M14

| Route | Giao diện thật |
|---|---|
| `/incidents` (`Incidents.razor`) | host: heading + KPI + modal; bảng nằm ở `IncidentListView` |
| `/admin/manager-switches` | lưới thẻ `sw-grid` radio + nút "Giải thích" (`aria-expanded`), không `<table>` |
| `/approvals` | `<ul class="approval-list">` + thẻ `<li>`, `<dl>` metadata (`ApprovalQueueView.razor:108-200`) |
| `/my/requests` | `<article class="approval-card">` (`ApprovalRequests.razor:45-111`) |

Nếu sau này chuyển các danh sách thẻ sang bảng, ứng viên popup: `TargetLabel`, `RequesterText`, `DecisionReason`, `PayloadSummary`, `SwDesc-*`.

### 3.6 Trang chủ · Dashboard · Báo cáo · Tài khoản

> `/dashboard` (`DashboardView.razor:1-7`) chỉ `NavigateTo("/home", replace: true)` — không có bảng. Các khối KPI/biểu đồ nằm ở component con; 3 bảng của `HomeView` được nhét vào `HeaderContent` khi `IsManagement` (`HomeView.razor:37-49`).

#### `/home` — Tài khoản chờ duyệt
**Mẫu**: M03
[HomeView.razor](../../NhaTro/Web/Features/Home/Pages/HomeView.razor) · DTO: `HomeAccountItem` · Cột: 2 · Vai trò: **Admin** (`:90`)

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tài khoản | `account.DisplayName` | `PendingAccounts.Items.Count > 0` | text thuần, không link |
| Vai trò | `RoleLabel(account.Role)` | như trên | map 4 role, fallback trả nguyên chuỗi (`:224-231`) |

**Áp dụng**: DTO chỉ 2 field, cố tình không lộ email (`HomeDtos.cs:45`) · panel có `Href="/admin/users"` + `State` riêng nên bảng lỗi vẫn giữ nút retry (`:92-94`) · rỗng → `EmptyStateCard`, không render bảng 0 dòng (`:95-97`). · `DisplayName` ứng viên `…` + hover; chưa có `title`.

#### `/home` — Hóa đơn quá hạn / chưa thu
**Mẫu**: M07
[HomeView.razor](../../NhaTro/Web/Features/Home/Pages/HomeView.razor) · DTO: `HomeInvoiceItem` · Cột: 3 · Vai trò: mọi role không phải Admin

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Phòng | `RoomCode` + `PropertyName` (`span.muted`) | `OpenInvoices.Items.Count > 0` | 2 dòng trong 1 ô |
| Kỳ | `PeriodStart – PeriodEnd` | như trên | `DateFormatter` |
| Còn nợ | `MoneyFormatter.Format(Outstanding)` | như trên | link `home-due-link` + mũi tên |

**Áp dụng**: link thông minh theo role — khách → `/my/invoices`, còn lại → `/invoices`, rồi gắn `?paymentState=unpaid` (`:217-222`) · `[PersistentState]` giữ snapshot lúc prerender, không nháy "Đang tải" (`:206`). · ô nợ đã có `title="@L["OpenInvoiceTitle"]"` (`:152`); `PropertyName` chưa có.

#### `/home` — Hợp đồng sắp hết hạn
**Mẫu**: M07 · M03 · M06
[HomeView.razor](../../NhaTro/Web/Features/Home/Pages/HomeView.razor) · DTO: `HomeContractItem` · Cột: 4 · Vai trò: mọi role không phải Admin

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Phòng | `RoomCode` + `PropertyName` | `ExpiringContracts.Items.Count > 0` | 2 dòng |
| Khách thuê | `contract.TenantNames` | như trên | chuỗi có thể rất dài |
| Ngày hết hạn | `DateFormatter.Format(EndDate)` | như trên | — |
| Số ngày còn | `L["DaysLeftValue", DaysLeft]` | như trên | resource có `{0}` |

**Áp dụng**: `DaysLeft` là `int` tính sẵn ở service, view không tự tính ngày (`HomeDtos.cs:30-35`) · panel `Href="/contracts"` dùng chung retry (`:165-166`). · `TenantNames` (`:191`) ứng viên mạnh nhất; chưa có `title`.

#### `/dashboard` — Khối KPI & liệt kê (component, không phải bảng thuần)
**Mẫu**: M14 · M07
[KPICardGrid.razor](../../NhaTro/Web/Features/Dashboard/Components/KPICardGrid.razor) · DTO: `RoomReportKpis`, `RevenueReportDto`, `IncidentReportKpis` · 3–6 thẻ

| Thành phần | File | Hàng | Ghi chú |
|---|---|---|---|
| KPI card | `KPICardGrid.razor:2` | Nhãn / Giá trị / Ghi chú | Loading → skeleton, Error → `—` + retry, **không hiện số 0 giả** (`:7-16`); thẻ doanh thu chỉ khi `IsOwner` (`:74-82`) |
| KPI khách thuê | `TenantOverviewCard.razor:8` | 4 (`Total/Active/Renting/LinkedAccount`) | `Total:0` → empty; cột liên kết chỉ khi owner có dữ liệu (`:12-15`) |
| KPI sự cố | `IncidentOverviewCard.razor:8` | 5 | thay panel doanh thu cho Quản lý; link `/reports` (`:17`) |
| Chú giải cơ cấu phòng | `RoomStructureChart.razor:39` | 4 (màu/label, số, %) | nhãn % trong SVG chỉ vẽ khi `sweep>=10`, legend vẽ hết; **đã có tooltip SVG** (`:30-35`) |
| Bảng dữ liệu doanh thu | `RevenueChart.razor:116` | 3 (Kỳ, Doanh thu, Công nợ) | bảng song song SVG; `PeriodKey` rỗng → `L["UnknownPeriod"]` (`:129`) |

**Áp dụng**: `RevenueChart.razor:253-270` chỉ đăng ký `ResizeObserver` khi có data, tránh `ElementReference` mặc định làm sập circuit · trục Y làm tròn 1/2/2.5/5/10 × 10ⁿ (`:230-237`). · nhãn thẻ ngắn, không cần ellipsis; `PeriodKey` (`2024-01`) đã có tooltip SVG cover.

#### `/reports` — Các báo cáo
**Mẫu**: M07 · M08 · M03
[BusinessReportsView.razor](../../NhaTro/Web/Features/Reports/Pages/BusinessReportsView.razor) · DTO: các `*ReportResult` · Vai trò: Chủ trọ / Quản lý

| Bảng | File:line | Cột | Ghi chú |
|---|---|---|---|
| Công suất theo tháng | `:156` | 5 | Tháng / Tổng phòng / Đã thuê / Trống / Tỷ lệ |
| Báo cáo phòng | `:192` | 4 | Mã phòng / `PropertyName` / trạng thái / giá chuẩn |
| Báo cáo khách thuê | `:286` | 6 hoặc 7 | cột 7 "Liên kết tài khoản" **chỉ `IsOwner`** (`:296-299`) |
| Báo cáo hợp đồng | `:375` | 9 | 5 KPI trạng thái đứng trước bảng (`:359-365`) |
| Báo cáo chi phí điện nước | `:441` | 9 | KPI `MissingSnapshotCount` ngay trên bảng (`:425-430`) |
| Báo cáo sự cố | `:544` | 8 | 3 filter enum sinh từ `Enum.GetValues<>` (`:487-523`) |

**Áp dụng**: Rỗng → `EmptyStateCard`, không render bảng (`:149-152`). · `PropertyName`/`RoomCode` null → `—`, không để trống (`:310-314`). · Filter `IsActive`/`HasActiveContract` là `bool?`, chọn "Tất cả" → `null` (`.cs:535-546`).

#### `/reports` — 4 bảng doanh thu (component `RevenueReportView`)
**Mẫu**: M07 · M03
[RevenueReportView.razor](../../NhaTro/Web/Features/Reports/Components/RevenueReportView.razor) · DTO: `RevenueFeeTypeDto`, `RevenuePropertyGroupDto`, `RevenueReportLineDto` · Vai trò: Chủ trọ / Quản lý

| # | Tên | File:line | Cột |
|---|---|---|---|
| 1 | Tiền theo loại phí | `:68` | 2 |
| 2 | Theo nhà trọ | `:99` | 5 |
| 3 | Dòng hóa đơn | `:141` | 7 |
| 4 | Hóa đơn công nợ | `:185` | 8 |

**Áp dụng**: đổi cơ sở ghi nhận giữa **kỳ hóa đơn** và **ngày thanh toán** (`:15-18`) · mỗi bảng có empty state riêng · `DueDate==null` → "Không có hạn", `OverdueDays>0` mới hiện số (`:206-207`)

#### `/Account/Manage/Passkeys` — Danh sách passkey
**Mẫu**: M03
[Passkeys.razor](../../NhaTro/Web/Components/Account/Pages/Manage/Passkeys.razor) · DTO: `UserPasskeyInfo` · Cột: 2 · Vai trò: user đã đăng nhập

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Tên passkey | `passkey.Name ?? L["UnnamedPasskey"]` | `Count > 0` | do người dùng đặt |
| Thao tác | form rename/delete | như trên | cả 2 nút đã có `title=` (`:41-42`) |

**Áp dụng**: `CredentialId` mã hoá Base64Url rồi nhét vào `@formname` + hidden input làm khóa form (`:35-39`) · chạm `MaxPasskeyCount=100` thì ẩn nút thêm (`:59-62`). · `passkey.Name` ứng viên `…` + hover (tên do user đặt, có thể rất dài).

#### `/Account/Manage/ExternalLogins` — Đăng nhập ngoài
**Mẫu**: M03
[ExternalLogins.razor](../../NhaTro/Web/Components/Account/Pages/Manage/ExternalLogins.razor) · DTO: `UserLoginInfo` / `AuthenticationScheme` · Cột: 2

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Nhà cung cấp | `login.ProviderDisplayName` | `Count > 0` | — |
| Thao tác | nút Remove | `showRemoveButton` | ẩn nếu chỉ còn 1 login và không có password (`:108`) |

**Áp dụng**: `showRemoveButton` chặn trường hợp mất truy cập (`:108`) · nút Remove có `title` chứa tên provider (`:41`) · `otherLogins` render thành nút bên dưới bảng (`:98-100`). · ô text chưa có `title`; ứng viên thấp (tên provider thường ngắn).

#### `/Account/Manage/Sessions` — Phiên đăng nhập (danh sách `<ul>`, không phải `<table>`)
**Mẫu**: M08 · M03
[Sessions.razor](../../NhaTro/Web/Components/Account/Pages/Manage/Sessions.razor) · DTO: `UserSessionInfo` · 3–4 mục/dòng

| Mục | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Thiết bị | `DeviceLabel(row.UserAgent)` | `Count > 0` | **đã cắt 90 ký tự + `…`** (`:133`) |
| Badge | `L["SessionsCurrent"]` | phiên hiện tại | điều kiện |
| Meta | `IpAddress · LastSeenAt` | luôn | giờ local |
| Thu hồi | nút Revoke | `!row.IsCurrent(...)` | chặn cả client gửi trùng session (`:101-107`) |

**Áp dụng**: nút "Thu hồi tất cả" chỉ hiện khi `Count > 1` (`:17-25`). · ⚠ **trường hợp duy nhất đã cắt nhưng chưa có hover** — thêm `title="@row.UserAgent"` (hoặc `data-hover-tip`) vào `<strong>` là khớp nhất.

#### `/` — Bảng mockup landing (dữ liệu cứng)
**Mẫu**: M08
[LandingView.razor](../../NhaTro/Web/Features/Landing/Pages/LandingView.razor) · DTO: **không có** · Cột: 3 · Vai trò: `AllowAnonymous` (`:3`)

| Cột | Biến | Hiện khi | Ghi chú |
|---|---|---|---|
| Phòng | `L["MockupRoom1..3"]` | luôn | mockup |
| Khách thuê | `L["MockupTenant1..3"]` | luôn | mockup |
| Trạng thái | `mockup-status status-paid/unpaid/empty` | luôn | 3 badge màu |

**Áp dụng**: toàn bộ mockup bọc `<figure role="img" aria-label="@L["MockupTitle"]">` (`:23`) → trình đọc màn hình xem như 1 ảnh; thanh biểu đồ giả là 8 `<span>` `aria-hidden` (`:42-44`). · tên mockup ngắn, `.mockup-table` đã `white-space:nowrap` (css:101) → **không áp dụng**.