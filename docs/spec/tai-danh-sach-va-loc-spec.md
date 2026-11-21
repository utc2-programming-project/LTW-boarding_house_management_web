# SPEC — Tải danh sách & bộ lọc (toàn hệ thống)

> Trạng thái: **đặc tả hiện trạng + chuẩn hóa đề xuất** — chưa sửa code.
> Ngày: 2026-11-20. Phạm vi: mọi trang danh sách trong `NhaTro/Web` (Blazor Web App .NET 10, EF Core, SQL Server).
> Liên quan: [Tối ưu & đồng nhất tầng đọc](toi-uu-va-dong-nhat-tang-doc-spec.md), `docs/spec/chuc-nang-tim-kiem-toan-cuc-spec.md`, `docs/architecture/cau_truc_thu_muc.md`.

## 0. Quy ước đọc tài liệu

| Mục | Quy ước |
|---|---|
| Round-trip | 1 lần gửi câu lệnh tới SQL Server (kể cả COUNT, query enrich). |
| Ký hiệu | `N` tổng số dòng bảng · `P` page size · `R` số nhà · `I` số hóa đơn · `C` số hợp đồng · `L` số dòng nhật ký · `A` số yêu cầu duyệt · `U` số tài khoản |
| Thời gian | **Ước lượng bậc độ lớn**, không phải benchmark. Chưa có số đo thực tế trong repo. |
| Đường dẫn code | Tính từ `NhaTro/Web/` trừ khi ghi khác. |
| Mức độ | Cao = chặn/không đáp ứng dữ liệu lớn · TB = lệch chuẩn, ảnh hưởng UX/chi phí · Thấp = dọn dẹp/đồng bộ |

Quy mô giả định để ước lượng:

| Mức | Phòng | Khách | Hóa đơn | Payment | Nhật ký / 3 ngày |
|---|---|---|---|---|---|
| S — demo | vài chục | vài chục | vài trăm | vài trăm | vài nghìn dòng |
| M — một chủ trọ lớn | 500 | 2.000 | 50.000 | 300.000 | 200.000 dòng |
| L — nhiều chủ trọ | 5.000 | 20.000 | 500.000 | 3 triệu | 1 triệu dòng |

Giả định hạ tầng: SQL Server cùng máy/VPC, 1 round-trip đơn giản 1–3 ms; truy vấn có index 1–10 ms; COUNT/LIKE quét bảng 20–300 ms; đọc + parse 100k dòng JSONL 100–300 ms; 1 triệu object trong RAM ~100–200 MB.

## 1. Tóm tắt điều hành

1. Toàn dự án dùng **5 kiểu tải dữ liệu**: (A) lọc/sort/phân trang ở DB, (B) nạp hết rồi xử lý trong RAM, (C) một trang + nhiều query enrich, (D) đọc cả file JSONL, (E) nhiều aggregate song song. Không có chuẩn chung bắt buộc — mỗi module chọn một kiểu.
2. **Chi phí round-trip cao**: `/home` ~22–25 query khi cache nguội; `/contracts` 8–9; `/payment-requests` 8; `/approvals` 9–10; `/admin/owners` `3P+2` (N+1).
3. **Hai báo cáo nạp cả bảng vào RAM**: Chi phí (`CostReportService`) và Doanh thu (`RevenueReportService.GetAsync`). Không giới hạn kỳ mặc định ⇒ tăng tuyến tính tới toàn bộ lịch sử.
4. **Nhật ký là file JSONL**: mỗi lần mở trang đọc **2 lần toàn bộ file** (dữ liệu + filter options), giữ lock trong lúc đọc; chi phí tăng theo số dòng tích lũy trong 3 ngày.
5. **Filter không đồng bộ**: page size có nơi reset về 20, có nơi clamp 100; search tên khách bỏ dấu/không phân biệt hoa thường, các module khác phụ thuộc collation; biên ngày đóng/nửa mở lẫn lộn; có chỗ dùng UTC thay vì giờ Việt Nam.
6. **Không có đo lường query**: chỉ có Serilog request logging; không Stopwatch, không đếm số round-trip, không SLO.
7. Ưu tiên sửa: (1) chặn mọi endpoint không phân trang, (2) đưa 2 báo cáo nặng về SQL/có kỳ bắt buộc, (3) bỏ N+1 OwnerAdmin, (4) cache 1 lần đọc file audit, (5) thống nhất filter contract + index.

## 2. Kiến trúc và hợp đồng chung

Luồng chuẩn:

```
Razor Page (.razor)
  → PageService (gắn caller/quyền, gọi scope)
    → Domain Service (EF Core DbContext / FileStore / in-memory)
      → PagedResult<T> hoặc IReadOnlyList<T>
```

| Thành phần dùng chung | Vai trò | Vị trí |
|---|---|---|
| `PagedResult<T>` | `Items / TotalCount / Page / PageSize` (mặc định 20) | `Common/Models/PagedResult.cs:5` |
| `PagedResult<T>` (bản sao) | Bản riêng của Tenants, `class` không `sealed` | `Features/Tenants/DTOs/PagedResult.cs:7` |
| `DataTable<TItem>` | Bảng chuẩn: skeleton/empty/error/retry + sort server | `Components/Shared/Tables/DataTable.razor` |
| `TablePager` | First/Prev/5 trang/Next/Last + "x–y / n" | `Components/Shared/Tables/TablePager.razor` |
| `PaginationHelper` | `TotalPages/ClampPage/PageWindow` | `Common/Utils/PaginationHelper.cs:7` |
| `DataTableRequest` | Chỉ `Page/PageSize/SortKey/SortDescending` — **không chứa filter** | `Components/Shared/Tables/DataTableRequest.cs:7` |
| `FilterAdvancedToggle` / `FilterResetButton` | UI bộ lọc nâng cao | `Components/Shared/FilterAdvancedToggle.razor`, `FilterResetButton.razor` |

Hiện trạng UI: `DataTable` xuất hiện trong **17 file `.razor`**, `TablePager` trong **13 file** (một số trang tự viết `<table>`), **0 chỗ dùng `Virtualize`**. Filter luôn nằm trong state component; URL chỉ được **đọc** cho deep-link (`q`, `propertyId`, `roomId`, `tenantId`, `contractId`, `paymentState`, `incidentId`, `serviceId`…), không trang nào ghi filter ngược lên URL.

## 3. Danh mục tải danh sách (inventory)

### 3.1 Phòng / Nhà / Khách thuê

| Method | UI | Nguồn | Lọc | Sort | Phân trang | RT |
|---|---|---|---|---|---|---|
| `RoomService.ListAsync` (`Features/Rooms/RoomService.cs:75`) | `/rooms` | EF first, `AsNoTracking`, subquery tên nhà mỗi dòng | DB đầy đủ | Cứng `Code, Id` | Count + Skip/Take, **reset 20** | 3–4 |
| `RoomService.ListDeletedAsync` (`:365`) | `/rooms/trash` | EF + `IgnoreQueryFilters` | Dùng lại toàn bộ `RoomFilter` | `DeletedAt desc, Id desc` | Có | 2 |
| `RoomService.SearchForPickerAsync` (`:546`) | chưa UI nào gọi | EF | `IsActive`, `Available`, `Code` | — | `Take(20/50)` | 1 |
| `PropertyService.ListAsync` (`Features/Properties/PropertyService.cs:20`) | `/properties` | EF, subquery `RoomCount` mỗi dòng | DB đầy đủ | 5 kiểu (`name/newest/...`) | Count + Skip/Take, **reset 20** | 2 |
| `PropertyService.ListOptionsAsync` (`:106`) | dropdown nhiều trang | EF | scope | `Name` | **Không giới hạn** | 1 |
| `PropertyService.ListDeletedAsync` (`:223`) | `/properties/trash` | EF + `IgnoreQueryFilters` | Chỉ `Keyword` (bỏ `IsActive`, room count, sort) | `DeletedAt desc` | Có | 2 |
| `TenantService.ListAsync` (`Features/Tenants/TenantService.cs:72`) | `/tenants` | EF, **materialize entity `Tenant`** | DB đầy đủ | Enum + direction | Count + Skip/Take, **clamp 1..100** | 4 |
| `TenantService.ListTrashAsync` (`:754`) | `/tenants/trash` | EF + `IgnoreQueryFilters` | Chỉ `Keyword` | `DeletedAt desc` | Có | 3 |
| `TenantService.SearchForPickerAsync` (`:1024`) | chưa UI nào gọi | EF | keyword | — | `Take(≤50)` | 2 |
| `TenantService.SearchUnlinkedAccountsAsync` (`:137`) | dialog gắn tài khoản | EF projection | keyword | `FullName` | `Take(≤50)` | 1 |
| `PropertyManagerService.List*` (`Features/Properties/Services/PropertyManagerService.cs`) | dialog phân công | EF join | join | — | Không | 2–4 |

### 3.2 Hóa đơn / Thanh toán / Hợp đồng / Duyệt

| Method | UI | Nguồn | Lọc | Sort | Phân trang | RT |
|---|---|---|---|---|---|---|
| `InvoiceQueryService.GetInvoicesAsync` (`Features/Invoices/Services/InvoiceQueryService.cs:19`) | `/invoices`, `/my/invoices` | EF first; **query chính track entity `Invoice`** | DB đầy đủ + KPI riêng | Cứng `PeriodStart desc, Id desc` | Count + Skip/Take, **clamp 1..100** | 5 (UI +1 options = 6) |
| `GetContractOptionsAsync` (`:245`) | cùng trang | EF projection | scope | `ContractNumber` | **Không giới hạn** | 1 |
| `InvoiceService.ListDeletedAsync` (`Services/InvoiceService.cs:624`) | `/invoices/trash` | EF projection | — | `DeletedAt desc` | UI tự cắt | 1 |
| `PaymentReviewQueryService.GetRequestsAsync` (`Services/PaymentReviewQueryService.cs:22`) | `/payment-requests` | EF + `Include(Proofs)` | DB đầy đủ, mặc định `Pending` | Cứng `RequestedAt desc` | Count + Skip/Take, clamp | 8 |
| `PaymentService.GetPaymentsAsync` (`Services/PaymentService.cs:509`) | chi tiết hóa đơn | EF | `InvoiceId` | `RequestedAt desc` | Không | 1 |
| `ContractService.GetListPageAsync` (`Services/ContractService.cs:309`) | `/contracts` | EF + 5 COUNT thống kê | DB đầy đủ, 12 nhánh status | 3 kiểu | Count + Skip/Take, clamp | 8–9 |
| `ContractService.ListAsync` (`:230`, legacy) | — | EF | keyword | Cứng | **Không** | 2 |
| `ContractService.ListDeletedAsync` (`:1469`) | `/contracts/trash` | EF projection | — | `DeletedAt desc` | UI tự cắt | 1 |
| `ContractRenewalService.ListPendingAsync` (`Services/ContractRenewalService.cs:77`) | `/contracts/renewals` | EF | — | `RequestedAt` | UI tự cắt | 1 |
| `ContractService.GetCreateOptionsAsync` (`:51`) | form tạo HĐ | EF | — | — | **Không giới hạn** (phòng trống + khách active) | 3–4 |
| `ContractService.GetHomeSnapshotAsync` (`:430`) | `/home` | EF | kỳ | `EndDate` | `Take(5/50)` | 2–3 |
| `ManagerApprovalService.ListInboxAsync` (`Features/Approvals/Services/ManagerApprovalService.cs:122`) | `/approvals` | EF | `Pending` + scope | `RequestedAt` | **Không** | 1 |
| `ApprovalQueuePageService.LoadAsync` (`:36` → `EnrichAsync`) | `/approvals` | EF + 8–9 lookup map | Filter/sort/đếm **trong RAM** | RAM | **Không** | 9–10 |

### 3.3 Home / Dashboard / Báo cáo

| Method | UI | Nguồn | Lọc | Phân trang | Chi phí |
|---|---|---|---|---|---|
| `HomePageService.LoadAsync` (`Features/Home/Services/HomePageService.cs:51`) | `/home` | Ghép 6 khối, mỗi khối `try/catch` | Role + kỳ | Không (trần 5 dòng/khối) | **~22–25 query khi cache nguội**, ~11 khi ấm |
| `DashboardPageService.ListPropertiesAsync` (`Services/DashboardPageService.cs:27`) | `/home` | Vòng lặp page 100 | scope | — | `O(P/100)` query |
| Dashboard cache | — | `IMemoryCache` 45 giây, key có version | — | — | Làm mới = tăng version (`:19`, `:92`) |
| `RevenueReportService.GetSummaryAsync` (`Reports/Services/RevenueReportService.cs:41`) | dashboard | 3 GroupBy SQL rồi trộn RAM | kỳ | — | 3 query, O(M×G) RAM nhỏ |
| `RoomReportService.GetAsync` (`Reports/RoomReportService.cs:31`) | `/reports` tab Phòng | EF GroupBy + page | DB | DB 20 | 3 |
| `RoomReportService.GetOccupancyAsync` (`:88`) | `/reports` | `ToListAsync` toàn bộ phòng + hợp đồng ⇒ vòng theo tháng | DB theo khoảng tháng | Không | **O(M×(R+C)) RAM**, 2 query |
| `TenantReportService.GetAsync` (`Reports/TenantReportService.cs:36`) | tab Khách | EF + enrich ≤100 dòng | DB | DB 20 | 4 |
| `ContractReportService` (`Reports/Services/ContractReportService.cs:20`) | tab Hợp đồng | EF GroupBy + page | DB | DB 20 | 3 |
| `CostReportService` (`Reports/Services/CostReportService.cs:15`) | tab Chi phí | **`ToListAsync` toàn bộ UtilityReadings** + toàn bộ Contract của các phòng | DB chỉ lọc kỳ/nhà/phòng | **Skip/Take trong RAM** | **O(U + U×C) RAM**, 3 query |
| `RevenueReportService.GetAsync` (`:154`) | tab Doanh thu + Excel | **`ToListAsync` toàn bộ hóa đơn + payment trong kỳ** | DB chỉ lọc kỳ | **RAM** | **O(I+P) RAM**, 4–6 query |
| `IncidentReportService` (`:15`) | tab Sự cố | EF GroupBy + page | DB | DB 20 | 2 — mẫu tốt nhất |

> Ghi chú Home: `HomeView` gọi `LoadAsync(includeManagementKpis:false)`; KPI/dashboard do `DashboardOverview` nạp riêng qua cache 45 s. Nhánh `revenue.GetAsync` (`HomePageService.cs:100`) chỉ chạy nếu caller dùng mặc định `includeManagementKpis=true` — khi đó nạp cả báo cáo tháng vào RAM; UI hiện không đi vào nhánh này.

### 3.4 Tài khoản / Nhật ký / Vận hành / Tiện ích

| Method | UI | Nguồn | Lọc | Phân trang | RT |
|---|---|---|---|---|---|
| `AccountService.ListAsync` (`Features/Accounts/AccountService.cs:64`) | `/admin/users` | EF projection + scope subquery | DB đầy đủ | Count + Skip/Take, clamp 20/100 | 4 |
| `AccountService.GetSummaryAsync` (`:258`) | dashboard admin | **EF nạp toàn bộ Users + roles vào RAM** | — | `Take(5)` RAM | 1 + O(U) RAM |
| `AccountService.ListDeletedAsync` (`:700`) | admin | EF | `IsDeleted` | `Take(200)` cứng | 1 |
| `OwnerAdminService.ListAsync` (`Features/Accounts/Owners/OwnerAdminService.cs:17`) | `/admin/owners` | EF + **`GetImpactAsync` 3 query/chủ trọ** | keyword | 20 | **3P+2 (N+1)** |
| `UserSessionStore.ListAsync` (`Sessions/UserSessionStore.cs:72`) | trang tài khoản | **File JSON/user**, deserialize toàn bộ | — | prune ≤20 | 1 I/O |
| `AuditLogService.QueryAsync` (`Features/Audit/Services/AuditLogService.cs:31`) | `/admin/audit-log` | **Đọc toàn bộ file JSONL** | From/To/Actor/Action/Property **RAM** | Skip/Take RAM, clamp 20/200 | **1 lần đọc cả file** |
| `AuditLogService.GetFilterOptionsAsync` (`:114`) | cùng trang | **Đọc lại toàn bộ file lần 2** | — | — | **+1 lần đọc cả file** |
| `AuditLogPageService.LoadAsync` (`Services/AuditLogPageService.cs:41`) | `/admin/audit-log` | Gọi 2 method trên + `PropertyFilter{PageSize=200}` | — | — | 2 file + 4 query |
| `IncidentService.ListAsync` (`Features/Maintenance/Services/IncidentService.cs:163`) | `/incidents` | EF **toàn bộ sự cố trong scope** | Không filter | UI lọc/sort/page 5 | 1 |
| `IncidentService.ListDeletedAsync` (`:406`) | `/incidents/trash` | EF + `IgnoreQueryFilters` | — | UI | 1 |
| `ServiceCatalogService.GetServiceCatalogsAsync` (`Features/Utilities/Services/ServiceCatalogService.cs:28`) | `/services` | EF + Include PriceHistory, `PriceHistory.Count > 0` lọc RAM | scope | UI 20 | 2 |
| `ServiceCatalogPageService.GetServicesForCallerAsync` (`:126`) | `/services` | Catalog + **toàn bộ UtilityTariffs + Tiers** rồi lọc RAM | building/type/status/search RAM | UI 20 | 3–4 |
| `GetPriceVersionsForCallerAsync` (`:258`) | `/services/prices` | **Gọi lại `GetServicesForCallerAsync`** + nạp lại tariffs | RAM | UI 10 | 5–6 |
| `ServiceCatalogService.ListDeletedAsync` (`:297`) | `/services/trash` | Nạp hết catalog xóa | keyword RAM | Skip/Take RAM | 2 |
| `ContractServiceRegistrationService.ListDeletedAsync` (`:138`) | `/services/registrations/trash` | Nạp hết đăng ký xóa | keyword RAM | Skip/Take RAM | 2 |
| `ServiceRegistrationService.ListMineAsync/ListForLandlordAsync` (`:112`) | `/service-registrations` | EF 4 query batch | property | UI 20 | 4 |
| `FileService.ListAsync` (`Features/Storage/Services/FileService.cs:382`) | `/documents` | EF, scope | scope | Không | 1 |
| `FileService.ListDeletedAsync` (`:548`) | `/documents/trash` | EF nạp hết file xóa + **check quyền từng file** | type/id | Không | 1+N |
| `IncidentPhotoService.ListAsync` (`Services/IncidentPhotoService.cs:235`) | chi tiết sự cố | EF Include + **`CanReadAsync` từng ảnh** | — | Không | 1+N |
| `NotificationService.ListAsync` (`Features/Notifications/NotificationService.cs:35`) | topbar mọi trang | EF | user | `Take(30)` + `UnreadCountAsync` riêng | 2 |
| `ManagerToggleService.GetAllAsync` (`Settings/Services/ManagerToggleService.cs:22`) | `/settings` | EF nạp cả bảng (danh mục ~20) | — | Không | 1, **không cache** dù có khai báo |

## 4. Thuật toán và độ phức tạp

### 4.1 Năm pattern

**A. Query-first (DB làm hết)**

```
WHERE (index) → COUNT → ORDER BY → OFFSET/FETCH
```

- Thời gian: COUNT `O(N)` nếu không có index phù hợp, `O(log N + P)` khi có; sort `O(N log N)` nếu thiếu index sort; lấy trang `O(P)`.
- Không gian: `O(P)` phía server.
- Round-trip: 2 (Count + Page). Enrich thêm 1 query/loại quan hệ.
- Điểm chết: `Contains('%kw%')` không dùng được B-tree index ⇒ `O(N)` toàn bảng dù chỉ lấy 20 dòng.

**B. Load-all rồi xử lý RAM**

```
ToListAsync() → Where → OrderBy → Skip/Take
```

- Thời gian: `O(N)` truyền dữ liệu + `O(N)` lọc + `O(N log N)` sort + `O(P)` cắt trang.
- Không gian: `O(N)` (entity + DTO). Với M/L dễ gây GC pressure, thậm chí OOM.
- Trang 1 vẫn trả giá bằng toàn bộ dữ liệu. `TotalCount` không cần COUNT nhưng đã trả giá `O(N)`.

**C. Fan-out enrich**

```
page (1 query) → ids → IN (...) cho từng quan hệ → dictionary
```

- Thời gian RAM: `O(P + K)`. Độ trễ: `K × round-trip`; K = 5–10 làm trang chậm dù dữ liệu nhỏ.
- Chấp nhận được nếu K ≤ 3; trên 5 nên gộp projection/join.

**D. File store (nhật ký, session)**

```
read all lines → parse JSON → filter → sort → Skip/Take
```

- Thời gian: `O(L)` I/O + `O(L)` parse + `O(L log L)` sort; không gian `O(L)`.
- Lock `SemaphoreSlim` giữ trong suốt lần đọc ⇒ request khác phải chờ. Trang audit gọi 2 lần ⇒ nhân đôi và tăng thời gian giữ lock.

**E. Aggregate song song (Home/Dashboard)**

```
Task.WhenAll(K aggregate)
```

- Wall-clock ≈ `max(tᵢ)` nhưng mở nhiều kết nối/round-trip; nghẽn connection pool khi nhiều người dùng.
- Aggregate SQL `GroupBy` là `O(N)` hash; aggregate RAM sau `ToListAsync` là `O(N)` bộ nhớ.

**F. N+1 (anti-pattern)**

```
for row in page: query(row)   → O(P × q) round-trip
```

Xuất hiện ở OwnerAdmin, ảnh sự cố, file trash, count phòng theo tòa, sweep nền.

### 4.2 Điểm nóng và công thức chi phí

| Điểm | Công thức | Mặc định | Rủi ro |
|---|---|---|---|
| `CostReportService.GetAsync` | `O(U + U×C_room)` RAM | Không lọc kỳ = mọi kỳ | M: vài trăm nghìn phép so sánh/phòng; L: treo/OOM |
| `RevenueReportService.GetAsync` | `O(I + P)` RAM, sort RAM | Không lọc kỳ = toàn bộ lịch sử | M: 50k HĐ + 300k payment; L: 500k + 3M |
| `GetOccupancyAsync` | `O(M×(R+C))`, M = số tháng | 6 tháng, **không chặn max** | Chọn 5–10 năm = 60–120 vòng |
| `AuditLogService.QueryAsync` | `O(L)` đọc + parse + `O(L log L)` sort, **×2 mỗi trang** | Giữ 3 ngày | M: 200k dòng; L: 1M dòng |
| `HomePageService.LoadAsync` | `~22–25` round-trip | cache dashboard 45 s | M: 250–600 ms; L: 0,6–1,5 s |
| `OwnerAdminService.ListAsync` | `3P+2` round-trip | P = 20 | 62 query/trang |
| `ContractService.GetListPageAsync` | 8–9 round-trip (5 COUNT) | P = 20 | M: 80–250 ms |
| `PaymentReviewQueryService.GetRequestsAsync` | 8 round-trip + `Include(Proofs)` | P = 20 | Proofs nhiều ảnh làm phình payload |
| `InvoiceQueryService.GetInvoicesAsync` | 5–6 round-trip + 2 SUM tương quan khi `Partial` | P = 20 | M: 50–150 ms; L: 150–500 ms |
| `ManagerApprovalService` + queue | 9–10 round-trip, `O(A)` RAM | Không phân trang | A tăng là chậm tuyến tính |
| `PropertyService.ListOptionsAsync` | `O(R)` không giới hạn | Mọi nhà | Dropdown 5k nhà = payload lớn |

### 4.3 Sort và index (tác động lên độ phức tạp)

| Bảng | Sort/filter thường dùng | Index hiện có | Hệ quả |
|---|---|---|---|
| Rooms | `Code` (sort), `Status`, `IsActive`, `StandardPrice`, `Floor`, `AreaM2` | `(PropertyId, Code)` unique filtered `[IsDeleted]=0` | Sort toàn cục theo `Code` không dùng được index; filter còn lại quét |
| Properties | `Name` (mặc định), `CreatedAt`, `IsActive` | `OwnerId` | Sort `Name`/`CreatedAt` không index |
| Tenants | `FullName`, `Phone`, `CitizenId`, `CreatedAt` | `FullName`, `Phone`, `CitizenId` unique filtered | Tốt |
| Invoices | `PeriodStart desc`, `Status`, `DueDate`, min/max tiền | `(ContractId, PeriodStart)` unique filtered, `(PeriodStart, Status)` | Thiếu `DueDate`, `TotalAmount`, `CreatedAt` |
| Payments | `RequestedAt`, `Status`, tổng theo hóa đơn | `(InvoiceId, Status)`, `(InvoiceId, Status, RequestedAt)`, `(Status, ReviewedAt)` | `RequestedAt` toàn cục không index |
| Contracts | `CreatedAt desc`, `Status`, `EndDate`, giá thuê | `(RoomId, Status, StartDate, EndDate)`, `(Status, DepositDueAt)` | Thiếu `CreatedAt`; lọc chủ trọ phải join Room→Property |
| Incidents | `CreatedAt desc`, `Status`, `Category`, `Urgency` | `RoomId` | Hầu hết filter/sort quét |
| AppNotifications | `RecipientUserId`, `IsRead`, `CreatedAt` | **Không có index**; `RecipientUserId` là `nvarchar(max)` | Không thể tạo index key; topbar quét mỗi trang |
| ServiceCatalogs | `IsActive`, `Name/Code` | `PropertyId` (FK) | Search LIKE quét |

Ghi chú: 17 chỗ có `HasQueryFilter(!IsDeleted)` (`Data/Configurations/*`, `Data/ApplicationDbContext.cs:145`). Một số bảng không có filter xóa mềm (`AppNotification`, `AuditLogEntry`, `UtilityReading*`, `ServiceRegistrationRequest`, `ManagerApprovalRequest`, `TenantInvitation`, `PendingRoomAssignment`…) nên phải tự lọc. Bảng `AuditLogEntries` trong DB có index nhưng **đường đọc nhật ký hiện tại dùng file JSONL**, không query bảng này.

## 5. Thời gian: hiện trạng, cách đo, SLO đề xuất

### 5.1 Hiện trạng

| Có | Không có |
|---|---|
| Serilog request logging (`Program.cs:586`) — log thời lượng từng HTTP request | Không `Stopwatch`/metric trong service (grep toàn `Web` = 0) |
| Dashboard cache 45 s (`DashboardPageService.cs:19`) | Không đếm round-trip/query mỗi trang |
| UI debounce 300 ms ở Rooms/Tenants/Owners | Không benchmark dataset, không SLO |
| `IMemoryCache` chỉ ở dashboard | EF command log đang bị tắt: Serilog override `Microsoft` → Warning (`Program.cs:83`) và `appsettings.Development.json` đặt `Microsoft.EntityFrameworkCore.Database.Command` = Warning; không có duration/query count |

### 5.2 Ước lượng độ trễ (bậc độ lớn, chưa đo)

| Trang / thao tác | S | M | L |
|---|---|---|---|
| `/rooms`, `/properties`, `/tenants` | 10–40 ms | 20–80 ms | 50–200 ms |
| `/invoices` (kèm `paymentState=Partial`) | 20–50 ms | 50–150 ms | 150–500 ms |
| `/payment-requests` | 30–80 ms | 80–250 ms | 250–800 ms |
| `/contracts` | 30–80 ms | 80–250 ms | 250–800 ms |
| `/approvals` | 20–50 ms | 50–200 ms | 0,2–1 s |
| `/home` cache nguội | 100–250 ms | 250–600 ms | 0,6–1,5 s |
| `/reports` tab Doanh thu | 20–50 ms | 0,2–0,8 s | 2–10 s + RAM |
| `/reports` tab Chi phí | 10–30 ms | 1–5 s | > 20 s / OOM |
| `/reports` tab Công suất | < 50 ms | 0,1–0,4 s | 1–3 s (nhân theo số tháng) |
| `/admin/audit-log` | 20–80 ms | 0,2–0,8 s | 1–4 s, giữ lock |
| `/admin/owners` | 50–150 ms | 150–500 ms | 0,5–2 s |

UI cộng thêm: debounce 300 ms (nếu có); trang không debounce gọi lại service mỗi lần đổi filter; `IncidentListView` còn gọi lại **toàn bộ** list mỗi lần đổi filter dù lọc client-side; `BusinessReportsView` chạy lại 3–4 service mỗi lần đổi ngày/nhà.

### 5.3 Cách đo chuẩn (đề xuất, chưa làm)

1. Dùng Serilog request log sẵn có: lọc `RequestPath` + `Elapsed`, lấy p50/p95 theo route.
2. Bật log `Microsoft.EntityFrameworkCore.Database.Command` ở mức Information để thấy `Executed DbCommand ... in X ms`; đếm số lệnh/trang.
3. Tạo dataset M (script `NhaTro/tools/demo-seed/generate-demo-data.mjs` mở rộng quy mô) và chạy lại các route trên.
4. Với báo cáo nặng: lấy execution plan (SSMS/XEvents), xác nhận scan vs seek.
5. Sau khi chuẩn hóa: thêm interceptor đếm query + duration, gắn vào log theo `TraceId` và hiển thị ở trang debug (không bật production).

### 5.4 SLO đề xuất

| Hạng mục | Mục tiêu (server-side, p95, dữ liệu M) |
|---|---|
| Trang danh sách CRUD | ≤ 300 ms |
| Trang danh sách có enrich (invoices/payments/contracts) | ≤ 500 ms |
| `/home` cache nguội | ≤ 800 ms |
| Tab báo cáo có kỳ bắt buộc | ≤ 1,5 s |
| Nhật ký (3 ngày lưu) | ≤ 1 s |
| Trang admin owners | ≤ 500 ms |
| Số round-trip/trang danh sách thường | ≤ 5 |

## 6. Bộ lọc (filter)

### 6.1 Ngữ nghĩa tham số dùng chung

| Tham số | Kiểu | Hành vi hiện tại | Ghi chú |
|---|---|---|---|
| `Keyword` | string | `Contains` → `LIKE '%x%'`; trim; `MaxLength` 50–200 | Không index; hoa/thường tùy collation |
| `PropertyId` | int? | `==` | Scope vẫn áp thêm |
| `Status` | enum? | `==` | Riêng Payments mặc định `Pending` |
| `Year`/`Month` | int? | Hóa đơn: giao khoảng kỳ, biên **đóng** | Tháng bắt buộc có năm trên UI |
| `From`/`To` | DateOnly/DateTime? | Báo cáo: đóng hoặc nửa mở tùy module | Xem 6.5 |
| `Min/MaxAmount` | decimal? | `>=`/`<=`; Invoice và Property tự đảo nếu min>max | Module khác không đảo |
| `HasAccount`, `HasActiveContract` | bool? | 3 trạng thái: null = tất cả | Quản lý bị chặn `HasAccount` |
| `Page`/`PageSize` | int | Page < 1 → 1; PageSize sai → **reset 20 hoặc clamp 100** | Không đồng bộ (F-06) |
| `SortBy`/`SortDirection` | string/enum | Rooms cố định; Properties string không whitelist; Tenants enum; Contracts string | Xem F-07 |

### 6.2 Filter theo module

| Module | Tham số | Tầng | Ghi chú |
|---|---|---|---|
| Rooms | propertyId, status, onlyAvailable, isActive, keyword(Code), min/maxPrice, floor, min/maxArea | DB | `onlyAvailable` = `IsActive && Available`; không sort tùy chọn |
| Properties | keyword(Name/Address), isActive, min/maxRoomCount, sortBy | DB | `roomCount` là COUNT tương quan trong WHERE và trong SELECT |
| Tenants | keyword(FullName/SĐT/CCCD), isActive, hasAccount, hasActiveContract, sortBy, direction | DB | Tên bỏ dấu + CI_AI; SĐT/CCCD yêu cầu ≥ 3 ký tự sau chuẩn hóa |
| Invoices | keyword(HĐ/mã phòng/tên nhà/tên khách/`HD-n`), contractId, status, year+month, min/maxAmount, paymentState | DB | `paymentState` 6 nhánh; `Partial` sinh 2 SUM tương quan |
| Payments | status (mặc định Pending), year+month, keyword (`YC-n`), min/maxAmount | DB | `Include(Proofs)` |
| Contracts | keyword, propertyName (**so khớp tuyệt đối**), termFrom/To, min/maxRent, statusKey (12 nhánh), sort | DB | `active` dùng biên nửa mở; `expiring` đóng |
| Approvals | group, keyword, sort | RAM | Load hết inbox rồi lọc/đếm/sort |
| Incidents | title, room, category, urgency, status, created date (text) | RAM | UI tự lọc/sort/page 5; mỗi thay đổi gọi lại `ListAsync()` |
| Audit | actor, action, property, from/to | RAM (file) | Actor/action so khớp `Ordinal`; ngày dùng offset server-local |
| Accounts | keyword, role, status | DB | Scope subquery 4–5 join |
| Owners | keyword(FullName/Email/Phone) | DB | Mỗi dòng 3 query impact |
| Service catalog | search, building, type, status | RAM | Nạp cả catalog + tariffs trước khi lọc |
| Prices | keyword, status, scope, effective date | RAM | Gọi lại catalog + tariffs |
| Documents | search, category, sort | RAM | Không phân trang |
| Reports | property + from/to + filter riêng từng tab | Hỗn hợp | Costs/Revenue lọc DB nhưng xử lý RAM |

### 6.3 Phạm vi quyền (scope) áp ở đâu

| Module | Cơ chế | Tầng |
|---|---|---|
| Rooms | `RoomScope.Apply`: Chủ trọ theo `Property.OwnerId`; Quản lý theo `PropertyManagers` + loại owner bị khóa; vai trò khác `Where(false)` | DB |
| Properties | `PropertyScope.Apply`; `BlockedOwnerIds` là danh sách **in-memory** truyền từ `PropertyCaller` | DB + RAM |
| Tenants | `TenantVisibility`: link chủ trọ hoặc hợp đồng còn hiệu lực; Quản lý theo phân công + blocked owners | DB |
| Invoices/Payments/Contracts | Join Room→Property / `ContractMembers`; `OwnerLockQuery.BlockedOwnerIds` subquery | DB |
| Approvals | Inbox theo property sở hữu hoặc người gửi được phân công | DB |
| Accounts | `ScopedUserIds` subquery | DB |
| Audit | Admin đọc sổ Account; Chủ trọ đọc sổ Business của mình | File |
| Reports | `PropertyScope` + kiểm tra toggle với Quản lý | DB |

Lưu ý: Admin **không** thuộc diện Chủ trọ/Quản lý nên `RoomScope` trả rỗng và `ScopeContracts` ném `UnauthorizedAccessException`; trong khi Accounts/Audit lại cho Admin xem rộng. Cần xác nhận đây là chủ ý nghiệp vụ.

### 6.4 UI: state, URL, debounce, page size

| Trang | Filter giữ ở | Ghi URL? | Debounce | Page size |
|---|---|---|---|---|
| `/rooms` | State + đọc `propertyId/q/roomId` | Không | 300 ms | 10/20/50 |
| `/properties` | State + đọc `q/propertyId` | Không | Không | 10/20/50 |
| `/tenants` | State + đọc `q/tenantId` | Không | 300 ms | 10/20/50 |
| `/invoices` | State + đọc `contractId/periodStart/q/paymentState` | Không | Không | Cố định 20 |
| `/contracts` | State + đọc `q` | Không | Không | 10/20/50 |
| `/payment-requests` | State + đọc `q` | Không | Không | Cố định 20 |
| `/incidents` | State + đọc `incidentId` | Không | Không | Cố định 5, lọc client-side |
| `/admin/audit-log` | State | Không | Không | Cố định 20 |
| `/admin/users` | State | Không | Không | Cố định 10 |
| `/admin/owners` | State | Không | 300 ms | Cố định 20 |
| `/services` | State + đọc `q` | Không | Không | Cố định 20, client-side |
| `/services/prices` | State + đọc `serviceId` | Không | Không | 10/20/50, mặc định 10 |
| `/approvals` | State | Không | Không | **Không phân trang** |
| `/documents` | State | Không | Không | Không phân trang |

Hệ quả: F5 mất filter (chỉ còn deep-link); không chia sẻ được URL đã lọc; back/forward không khôi phục bộ lọc.

### 6.5 Biên thời gian (dễ sai lệch)

| Ngữ cảnh | Biểu thức | Biên |
|---|---|---|
| Kỳ hóa đơn (Year/Month) | `PeriodStart <= end && PeriodEnd >= start`, `end = start.AddMonths(1).AddDays(-1)` | Đóng cả hai đầu (giao khoảng) |
| Thời hạn hợp đồng | `EndDate >= TermFrom && StartDate <= TermTo` | Đóng |
| Hợp đồng `active` | `StartDate <= todayStart && EndDate > expiringEnd` | Nửa mở |
| Hợp đồng `expiring` | `EndDate >= todayStart && EndDate <= expiringEnd` | Đóng |
| Sự cố (báo cáo) | `CreatedAt >= StartUtc && CreatedAt < EndUtc` | Nửa mở, VN → UTC |
| Chi phí (báo cáo) | `PeriodEnd >= From && PeriodStart <= To` (DateOnly) | Đóng |
| Công suất | `StartDate < rangeEndExclusive` | Nửa mở |
| Dashboard | Start..End, End = start.AddMonths(n).AddDays(-1) | Đóng, `TodayVietnam()` |
| Doanh thu theo ngày thanh toán | `ReviewedAt` trong `[fromUtc, toUtcExclusive)` | Nửa mở, VN day range |
| Báo cáo khách thuê | `clock.GetUtcNow().UtcDateTime.Date` | **UTC — lệch quy ước** (`Reports/TenantReportService.cs:50`) |
| Nhật ký From/To | `TimeZoneInfo.Local` (start/end of day) | Phụ thuộc TZ server, không phải VN cố định (`AuditLogPageService.cs:112`) |

### 6.6 So khớp chuỗi tìm kiếm

| Module | Cách so khớp | Bỏ dấu | Hoa/thường |
|---|---|---|---|
| Tenants | `Collate(FullName, SQL_Latin1_General_CP1_CI_AI).Contains`, SĐT/CCCD `Contains` sau chuẩn hóa | Có (tên) | Không phân biệt |
| Rooms | `Code.Contains` | Không | Tùy collation DB |
| Properties | `Name/Address.Contains` | Không | Tùy collation |
| Invoices/Contracts | `Contains` trên nhiều cột + parse `HD-n/YC-n` | Không | Tùy collation |
| Accounts/Owners | `Contains` | Không | Tùy collation |
| Catalog/Registration trash | `Contains(..., OrdinalIgnoreCase)` | Không | Không phân biệt |
| Incidents | `Contains(..., CurrentCultureIgnoreCase)` | Không | Không phân biệt |
| Documents | `TextSearchNormalizer.Contains` | Có | Không phân biệt |

Không nơi nào dùng full-text index; mọi `LIKE '%...%'` đều quét bảng. `TextSearchNormalizer` chỉ được dùng ở tài liệu khách thuê, chưa dùng cho danh sách chính.

## 7. Chỗ chưa đồng bộ / khác biệt

| ID | Mức | Vấn đề | Bằng chứng | Ảnh hưởng |
|---|---|---|---|---|
| F-01 | Cao | Endpoint trả **không giới hạn**: property options, contract options, create-options, approvals inbox, incident list, catalog, documents, report lines | `PropertyService.cs:106`, `InvoiceQueryService.cs:245`, `ContractService.cs:51`, `ManagerApprovalService.cs:132`, `IncidentService.cs:163`, `ServiceCatalogService.cs:28`, `FileService.cs:382` | Payload/RAM tăng tuyến tính; dropdown 5k nhà |
| F-02 | Cao | Báo cáo Chi phí/Doanh thu **lọc kỳ/nhà ở DB nhưng materialize toàn bộ tập khớp**, rồi sort/aggregate/phân trang trong RAM; không truyền kỳ = quét cả lịch sử | `CostReportService.cs:26-35`, `RevenueReportService.cs:212-232` | L: treo/OOM; M: chậm 1–5 s |
| F-03 | Cao | Nhật ký đọc toàn bộ file JSONL **2 lần/trang**, giữ lock khi đọc, không cache | `AuditLogService.cs:44,57,123,127`, `AuditLogFileStore.cs:130` | Chi phí tăng mỗi ngày; request khác bị chặn |
| F-04 | Cao | N+1: OwnerAdmin 3 query/chủ trọ; đếm phòng/tòa; quyền từng ảnh/file | `OwnerAdminService.cs:47-60`, `ServiceCatalogPageService.cs:404`, `IncidentPhotoService.cs:245`, `FileService.cs:580` | Trang admin chậm; list ảnh/tệp lớn |
| F-05 | Cao | Thiếu index cho cột filter/sort chính; `AppNotifications.RecipientUserId` là `nvarchar(max)` nên **không thể index**, lại không có index nào | Mục 4.3; migration `AddAppNotifications` | Sort/filter quét bảng; topbar mọi trang |
| F-06 | TB | PageSize: Rooms/Properties reset 20, Tenants/Invoices/Contracts/Payments clamp 1..100, Owners reset 20 | `RoomFilter.cs:43`, `PropertyFilter.cs:31`, `TenantFilterRequest.cs:37`, `InvoiceListDtos.cs:108` | Cùng tham số `pageSize=0/999` cho kết quả khác nhau |
| F-07 | TB | Sort không đồng bộ: Rooms cố định; Properties nhận string **không whitelist**; Tenants enum; Invoices/Payments cố định; UI Accounts bật sort nhưng service bỏ qua `SortKey` | `PropertyService.cs:51`, `AccountManagement.razor:282,307`, `AccountService.cs:137` | Bấm sort không có tác dụng; giá trị lạ rơi về mặc định |
| F-08 | TB | Search: chỉ Tenants bỏ dấu/CI_AI; nơi khác phụ thuộc collation; min length chỉ áp cho SĐT/CCCD | `TenantService.cs:1403`, `RoomService.cs:620` | Cùng từ khóa cho kết quả khác nhau giữa module |
| F-09 | Cao | Biên ngày lẫn đóng/nửa mở; Dashboard đóng, Incidents nửa mở, Contracts `active` nửa mở nhưng `expiring` đóng | Mục 6.5 | Lệch 1 ngày ở ranh giới tháng/quý; báo cáo không khớp |
| F-10 | Cao | Báo cáo khách thuê dùng **ngày UTC** thay vì giờ VN; audit dùng `TimeZoneInfo.Local` của server | `TenantReportService.cs:50`, `AuditLogPageService.cs:112` | Sai "đang thuê" vào 0h–7h VN; lọc ngày audit lệch nếu server UTC |
| F-11 | TB | Filter không ghi lên URL ⇒ F5/back mất bộ lọc; chỉ deep-link đọc một phần | `RoomList.razor:250`, `TenantList.razor:223` | UX kém, không chia sẻ được màn hình đã lọc |
| F-12 | TB | Debounce không đồng bộ: 300 ms chỉ ở Rooms/Tenants/Owners; Incidents gọi lại toàn bộ list mỗi lần đổi filter | `IncidentListView.razor:253-296` | Tải lại liên tục, tốn query |
| F-13 | TB | 6 màn nạp hết rồi phân trang client: contracts trash/renewals, invoice trash, approvals, catalog, incidents, documents | Mục 3.2–3.4 | Số dòng tăng là UI nặng; `TotalCount` ảo |
| F-14 | TB | Truy vấn lặp: `paymentState=Partial` 2 SUM tương quan; tenant active subquery xuất hiện 2–3 lần; prices gọi lại catalog + tariffs | `InvoiceQueryService.cs:401`, `TenantReportService.cs:71`, `ServiceCatalogPageService.cs:262` | Tăng round-trip/scan không cần thiết |
| F-15 | TB | Projection/tracking không đồng bộ: query hóa đơn track entity `Invoice`; tenant list materialize full entity; payments `Include(Proofs)` | `InvoiceQueryService.cs:57`, `TenantService.cs:110` | RAM/GC cao hơn cần thiết |
| F-16 | Thấp | Hai `PagedResult<T>` và nhiều DTO trang riêng (`InvoiceListResult`, `ContractListPageDto`, `AuditLogPageDto`) | `Common/Models/PagedResult.cs:5`, `Tenants/DTOs/PagedResult.cs:7` | Khó chuẩn hóa UI/service |
| F-17 | Thấp | Admin không xem được list nghiệp vụ (Room rỗng, Invoice ném lỗi) nhưng xem rộng Accounts/Audit | `RoomScope.cs:14-39`, `InvoiceQueryService.cs:303-341` | Cần xác nhận chủ ý |
| F-18 | TB | `IgnoreQueryFilters` ở join phòng/nhà để đọc hóa đơn lịch sử; bảng không có soft-delete filter phải tự lọc | `InvoiceQueryService.cs:59`, ApplicationDbContext | Dễ lộ tên bản ghi đã xóa nếu quên |
| F-19 | TB | Vòng lặp nền N+1: sweep hợp đồng, purge từng dòng, purge tài khoản | `ContractPaymentDeadlineService.cs:58`, `InvoiceService.cs:749`, `AccountService.cs:794` | Tác vụ nền kéo dài, khóa DB |
| F-20 | TB | Không có instrumentation/SLO/benchmark; chỉ Serilog request log | `Program.cs:586` | Không biết trang nào chậm thật, không đo được cải thiện |
| F-21 | Thấp | Bảng UI không đồng nhất: 17 file dùng `DataTable`, 13 dùng `TablePager`, một số trang tự viết `<table>`; không `Virtualize` | Mục 2 | Trải nghiệm/khả năng bảo trì khác nhau |
| F-22 | Thấp | Bảng `AuditLogEntries` + index trong DB không được đường đọc nhật ký dùng | `ApplicationDbContext.cs:90`, `AuditLogFileStore` | Code chết, dễ gây hiểu nhầm |
| F-23 | TB | Approvals dùng 9 query **batch** (không phải N+1) nhưng không phân trang; group/keyword/sort lọc RAM | `ApprovalQueuePageService.cs:89-173`, `ApprovalQueueView.razor:320-394` | `A` tăng tuyến tính; gộp ≤5 query khó vì target đa hình, cần projection/cache thay vì "sửa N+1" |

## 8. Chuẩn hóa đề xuất

### 8.1 Hợp đồng chung cho mọi danh sách

> Bất biến cần giữ là **không nạp tập không giới hạn vào RAM**, không phải "mọi danh sách đều phải có pager". Option/picker/notification dùng trần cứng; filter dựa trên dữ liệu suy diễn chỉ được chạy trên tập đã giới hạn.

1. **Một** `PagedResult<T>` duy nhất trong `Web.Common.Models`; bỏ bản sao ở Tenants.
2. Mọi method list nhận một filter kế thừa `ListQueryBase`:
   - `Page` (≥1), `PageSize` (**clamp 1..100**, mặc định 20 — không reset),
   - `SortKey` (string) + `SortDesc` (bool), whitelist bằng `switch`, giá trị lạ → mặc định,
   - `Keyword` (trim, tối thiểu 2 ký tự khi tìm).
3. Luôn: lọc/sort/page **ở DB**, `AsNoTracking`, `Select` projection, `CountAsync` trước page, `Skip/Take` một lần — trừ danh sách đã có trần cứng ở mục 5.
4. Enrich tối đa **1 query/loại quan hệ** bằng `Contains(ids)` + dictionary; cấm query trong vòng lặp.
5. Endpoint options/picker: `Take(≤200)` + keyword server-side; vượt ngưỡng thì dùng picker thay vì dropdown.
6. Báo cáo: bắt buộc kỳ hoặc mặc định 12 tháng gần nhất; trả page ở DB; không nạp cả bảng.
7. Nhật ký: đọc file **1 lần/request** (dùng chung dữ liệu cho cả danh sách và filter options); cache theo `LastWriteTime`; tiếp tục giữ 3 ngày.

> Về keyset: giữ `OFFSET/FETCH` cho UI admin vì `TablePager` cần số trang (First/Prev/5 số/Next/Last). Chỉ cân nhắc keyset cho hóa đơn/payment khi đo thấy trang sâu chậm; khi đó vẫn cần một `COUNT` riêng cho tổng số trang.

### 8.2 Quy tắc thời gian

- Một helper duy nhất cho biên ngày: `VietnamTime` (đã có) — cấm `UtcDateTime.Date`, cấm `TimeZoneInfo.Local`.
- Ngày/giờ: dùng khoảng **nửa mở** `[start, endExclusive)`; với `DateOnly` (kỳ hóa đơn, chi phí) ghi rõ "giao khoảng đóng" trong tài liệu và test.
- Trạng thái hợp đồng: `active` nửa mở, `expiring` đóng — giữ nhất quán và có test biên 00:00 ngày cuối.
- Dashboard/Chart: dùng `TodayVietnam()`; báo cáo khách thuê sửa về VN.
- Audit: thay `TimeZoneInfo.Local` bằng `VietnamTime.DayRangeUtc`.

### 8.3 Index đề xuất

| Bảng | Index đề xuất | Phục vụ |
|---|---|---|
| Rooms | `(PropertyId, Status, IsActive)`; `(IsActive, Status, Code)` | filter + sort mặc định |
| Properties | `(OwnerId, IsDeleted, Name)`; `(IsActive, Name)`; `(CreatedAt)` | list/options/sort |
| Invoices | Đã có `(PeriodStart, Status)`, `(ContractId, PeriodStart)`; thêm `(Status, DueDate)`/`(DueDate)`, include `TotalAmount` nếu đo thấy thiếu | trang hóa đơn + KPI + quá hạn |
| Payments | Đã có `(InvoiceId, Status)` filtered, `(InvoiceId, Status, RequestedAt)`, `(Status, ReviewedAt)`; thêm `(Status, RequestedAt)`/include `Amount` nếu thiếu | tổng đã thu + review |
| Contracts | Đã có `(RoomId, Status, StartDate, EndDate)`, `(Status, DepositDueAt)`; thêm `(Status, EndDate)`, `(CreatedAt)` | expiring + list |
| Incidents | `(Status, CreatedAt)`; `(RoomId, CreatedAt)` | list/report |
| AppNotifications | Đổi `RecipientUserId` sang `nvarchar(450)` + index `(RecipientUserId, IsRead, CreatedAt)` | topbar mọi trang |
| ServiceCatalogs | `(PropertyId, IsActive)` | list dịch vụ |
| Tenants | Giữ hiện có; cân nhắc cột search chuẩn hóa nếu cần bỏ dấu toàn cục | keyword |
| Audit file | Không index được — cache + giới hạn retention | nhật ký |

Index chỉ có tác dụng khi **cột đầu** khớp predicate hoặc sort; index một cột ít chọn lọc (`IsActive`) thường không đủ, phải là composite đúng thứ tự. `LIKE '%kw%'` vẫn quét dù có index; nếu số dòng > ~100k và search là tính năng chính, cân nhắc full-text index hoặc cột `SearchText` chuẩn hóa. Các index đề xuất cần đo write-cost trước khi thêm hàng loạt.

### 8.4 UI

- Ghi filter lên query string bằng `replaceState` (debounce 300 ms cho text), đọc lại khi vào trang ⇒ F5/back giữ bộ lọc.
- Một component search chuẩn dùng debounce 300 ms; bỏ gọi lại toàn bộ list khi filter client-side.
- Dùng `DataTable` + `TablePager` cho mọi danh sách; page size 10/20/50 (trừ khi có lý do ghi rõ).
- Khi đổi filter làm giảm số trang: clamp page ngay trên client, tránh vòng `do/while` gọi lại service.
- Hiển thị tổng, khoảng đang xem, trạng thái loading/empty/error thống nhất.

### 8.5 Đo lường

- Thêm `DbContext` interceptor đếm `queries`, `durationMs`; gắn vào log request.
- Cảnh báo khi trang vượt SLO (mục 5.4) hoặc > 5 round-trip.
- Giữ một dataset M cố định để đo p50/p95.

## 9. Tiêu chí nghiệm thu

Một danh sách được coi là đạt chuẩn khi:

- [ ] Trả `PagedResult<T>` chung; `PageSize` clamp 1..100, mặc định 20.
- [ ] Filter/sort/page chạy ở DB; `AsNoTracking` + projection; không materialize entity đầy đủ nếu chỉ cần vài cột.
- [ ] `CountAsync` + một query page; tổng round-trip ≤ 5 (hoặc có ngoại lệ ghi rõ).
- [ ] Không có query trong vòng lặp; enrich bằng `Contains(ids)` + dictionary.
- [ ] Keyword trim; tối thiểu 2 ký tự; ngữ nghĩa bỏ dấu/hoa-thường được ghi rõ và nhất quán.
- [ ] Biên ngày dùng `VietnamTime`, kiểu khoảng được ghi rõ; có test 00:00 và ngày cuối tháng.
- [ ] Sort whitelist; giá trị lạ không làm đổi kết quả ngoài dự kiến.
- [ ] Options/picker có giới hạn; không dropdown toàn bộ dữ liệu.
- [ ] Báo cáo có kỳ bắt buộc hoặc mặc định ≤ 12 tháng; không có đường nào nạp toàn bộ lịch sử khi filter null.
- [ ] Giữ `Skip/Take` cho UI có số trang; chỉ dùng keyset khi đã đo thấy OFFSET sâu chậm.
- [ ] Index mới được xác nhận bằng execution plan (seek thay vì scan) và đánh giá write-cost.
- [ ] Filter được giữ trên URL (hoặc quyết định bỏ có ghi chú).
- [ ] Có số đo p50/p95 và số query cho trang ở dataset M.

## 10. Phụ lục — map nhanh route ↔ service

| Route | Service chính |
|---|---|
| `/rooms`, `/rooms/trash` | `RoomPageService` → `RoomService` |
| `/properties`, `/properties/trash` | `PropertyPageService` → `PropertyService` |
| `/tenants`, `/tenants/trash` | `TenantPageService` → `TenantService` |
| `/invoices`, `/my/invoices`, `/invoices/trash` | `InvoiceQueryService`, `InvoiceService` |
| `/payment-requests` | `PaymentReviewQueryService` |
| `/contracts`, `/contracts/trash`, `/contracts/renewals` | `ContractService`, `ContractRenewalService` |
| `/approvals` | `ApprovalQueuePageService`, `ManagerApprovalService` |
| `/home`, `/dashboard` | `HomePageService` (+ redirect), `DashboardPageService` |
| `/reports` | `ReportsPageService` → 6 report service |
| `/admin/users`, `/admin/owners` | `AccountService`, `OwnerAdminService` |
| `/admin/audit-log` | `AuditLogPageService` → `AuditLogService` → `AuditLogFileStore` |
| `/incidents`, `/incidents/trash` | `IncidentPageService` → `IncidentService` |
| `/services`, `/services/prices`, `/services/trash`, `/services/registrations/trash` | `ServiceCatalogService`, `ServiceCatalogPageService`, `ServiceRegistrationService` |
| `/service-registrations` | `ServiceRegistrationService` |
| `/documents`, `/documents/trash` | `FileService` |
| Topbar | `NotificationService` |
| `/settings` | `ManagerToggleService` |
