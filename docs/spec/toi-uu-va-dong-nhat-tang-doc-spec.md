# SPEC — Tối ưu hiệu năng & đồng nhất tầng đọc danh sách

> Trạng thái: **đề xuất kiến trúc & chuẩn kỹ thuật**, chưa sửa code.
> Ngày: 2026-11-20. Nền: Blazor Server .NET 10, EF Core 10, SQL Server, chạy trên **local server** (web + DB cùng máy/VPC).
> Liên quan: [Tải danh sách & bộ lọc](tai-danh-sach-va-loc-spec.md) — spec hiện trạng và các finding F-01…F-23.

## 0. Phạm vi, định nghĩa & nguyên tắc

### 0.1 Phạm vi

- Áp dụng cho mọi danh sách, báo cáo, option/picker và nhật ký trong `NhaTro/Web`.
- Chỉ thay **cách đọc dữ liệu**; không đổi nghiệp vụ, quyền, schema nghiệp vụ.
- Không gồm: đổi DB engine, microservice, migration dữ liệu lớn.

### 0.2 Định nghĩa

| Thuật ngữ | Nghĩa |
|---|---|
| Round-trip | Một lệnh gửi tới SQL Server |
| Page | Một trang kết quả có `Page/PageSize/TotalCount` |
| Bounded list | Danh sách có trần cứng, không cần pager |
| Cache hit | Key có trong bộ nhớ, không gọi DB |
| Dataset M | Tập đo cỡ trung: 500 phòng, 2.000 khách, 50.000 hóa đơn, 300.000 payment |
| SLO | Ngưỡng p95 mục tiêu, đo trên dataset M |

### 0.3 Nguyên tắc

1. **Local-first, nhưng không local-first bừa.** Giả định chưa đo: SQL và web cùng máy ⇒ 1 round-trip ~1–3 ms; quét 300k dòng ~100–500 ms. Các số này chỉ để hình dung, **không dùng làm cam kết**; phải thay bằng số đo thực tế. Chi phí cần giảm theo thứ tự: **bytes scan → RAM/GC → round-trip → render**.
2. **Không nạp tập không giới hạn.** Mọi danh sách phải có page hoặc trần cứng; filter/sort/page/aggregate chạy ở SQL.
3. **Không tối ưu khi chưa có số đo.** Bật EF command log + interceptor đếm query; đo trên dataset M; không suy đoán.
4. **Tách logic, không tạo framework.** Một bộ primitive nhỏ dùng chung; query EF vẫn nằm trong module, không nhét vào generic repository.
5. **Cache ở local memory, không đẩy tính toán nặng về browser.** Blazor Server là client mỏng; browser chỉ giữ UI state và lọc tập nhỏ.

## 1. Kết luận từ tài liệu hiện hành

| Nguồn | Kết luận áp dụng |
|---|---|
| EF Core — Efficient Querying | Index đúng, chỉ `Select` cột cần, giới hạn result set, phân trang, tránh cartesian explosion, tránh lazy loading, dùng no-tracking cho read-only, stream thay vì buffer |
| EF Core — Advanced Performance | `DbContext` pooling/factory, compiled query cho hot path, parameter hóa query, query cache; không dùng Expression API nếu không cần |
| EF Core — Pagination | Offset phù hợp khi cần nhảy trang; keyset khi chỉ next/prev; **luôn sort có cột unique** (đã có `Id` tie-breaker) |
| SQL Server — Index Design Guide | Composite index đúng thứ tự cột; `INCLUDE` để covering; filtered index cho tập con; không over-index; index chỉ hữu ích khi predicate **SARGable** |
| Blazor — Rendering Performance | `ShouldRender`, parameter immutable, tránh hàng nghìn component, tránh delegate trong vòng lặp, không bắn event quá nhanh |
| Blazor — Virtualization | `Virtualize` chỉ render item trong viewport; dùng `ItemsProvider` khi không muốn nạp hết list |
| ASP.NET Core — HybridCache | L1 in-memory + L2 tùy chọn, chống stampede, tag invalidation, giới hạn payload/key; single server chỉ cần L1 |
| ASP.NET Core — Hosted services | `BackgroundService` + `PeriodicTimer` cho precompute/cleanup; dùng scope + `IDbContextFactory`, không chặn request |

### 1.1 Hiện trạng → mục tiêu

| Hiện trạng (finding) | Mục tiêu |
|---|---|
| F-01 list/option không giới hạn | Page ở DB hoặc trần cứng ≤200 |
| F-02 báo cáo materialize cả tập khớp | Aggregate SQL + page DB; summary chỉ khi số đo vượt SLO |
| F-03 audit đọc file 2 lần/trang | Một snapshot/request |
| F-04 N+1 OwnerAdmin/ảnh/file | Batch enricher hoặc aggregate query |
| F-05 thiếu index | Index theo execution plan, không theo cảm giác |
| F-06/07/08 page size, sort, search lệch chuẩn | `ListQueryBase` + `SortMap` + `SearchTerm` |
| F-09/10 biên ngày và timezone lệch | `VietnamDateRange` |
| F-13 phân trang client trên list không giới hạn | Pipeline server-side |
| F-15 tracking/materialize entity | Projection + `AsNoTracking` |
| F-20 không có số đo | EF command log + interceptor + SLO |

## 2. Phân tầng xử lý: cái gì ở đâu

| Việc | Nơi đúng | Ghi chú |
|---|---|---|
| Filter, sort, page, count | **SQL Server** | SARGable, index; page size ≤100 |
| Aggregate tiền/kỳ | **SQL Server** hoặc bảng summary | Không tính ở browser; không tính trên list đã page |
| Authorization/scope | **SQL/server** | Không bao giờ để client quyết định |
| Reference data (nhà, dịch vụ, tariff, quyền) | **Local memory cache** | TTL 5–30 phút, tag theo module |
| Dashboard/Home snapshot | **Local memory cache** | TTL 30–60 giây, tag theo owner/nhà |
| Báo cáo tháng/năm | **Aggregate SQL trước**; bảng summary + precompute chỉ khi số đo vượt SLO | Request chỉ đọc page; không mặc định thêm bảng summary |
| Audit đã parse | **Local memory cache** | Key theo `path + mtime + length`; 1 lần đọc/request |
| Filter danh sách nhỏ (<200 dòng) | Browser | Chỉ khi list đã có trần cứng |
| Render danh sách dài | Browser `Virtualize` | Không thay thế phân trang server |
| Export Excel/PDF | Server stream | `AsAsyncEnumerable()`/`IAsyncEnumerable`, không `ToList` cả bảng |
| Ghi audit/notification | Server; batch/queue chỉ khi cần | Không ghi đồng bộ từng dòng nếu không cần |

### 2.1 “Quăng về local” đúng nghĩa

- **Local server memory**: cache reference data, filter options, dashboard, audit snapshot. Đây là nơi giảm tải DB rẻ nhất.
- **Local background worker** (chỉ khi số đo chứng minh cần): precompute summary, warm cache, dọn audit, gửi notification. Chạy **1–2 job song song**, ưu tiên thấp, có cancellation. Không thuộc phạm vi mặc định.
- **Browser**: chỉ UI state, sort/filter tập nhỏ, virtualization. **Không** gửi 50k dòng sang browser để lọc/sort/tính tổng — Blazor Server sẽ lag vì payload + circuit memory.
- **Không đưa về local**: kiểm tra quyền, tổng tiền, `TotalCount`, quyết định trạng thái. Đây là logic server.

### 2.2 Giới hạn để local không lag

| Tài nguyên | Giới hạn đề xuất |
|---|---|
| Cache entry | ≤ 1 MB/entry, ≤ 20–50 MB tổng cho cache nghiệp vụ |
| Cache key | Không chứa input thô; user id/nhà id nội bộ |
| Background job | Nếu có: tối đa 1–2 job; `PeriodicTimer`; tránh giờ cao điểm |
| Memory cache per user | **Không cache list nghiệp vụ per-user.** Snapshot dashboard/audit theo owner là ngoại lệ nhỏ, TTL ngắn, có giới hạn kích thước |
| Circuit memory | Chỉ giữ DTO trang hiện tại; xóa list khi rời trang; `PersistentState` chỉ snapshot nhỏ |
| Connection pool | Đặt `Max Pool Size` hợp lý; không mở context dài hạn |

## 3. Đồng nhất logic

### 3.1 Bộ primitive dùng chung (tối thiểu)

Đề xuất **4 primitive bắt buộc + 4 helper tùy chọn**. Không bắt buộc mỗi module phải tách 5 file; module nhỏ có thể gộp.

| Thành phần | Loại | Trách nhiệm | Không làm gì |
|---|---|---|---|
| `PagedResult<T>` | Bắt buộc | Envelope duy nhất: Items/TotalCount/Page/PageSize | Không chứa logic |
| `ListQueryBase` | Bắt buộc | Page, PageSize, SortKey, SortDesc, Keyword + `Normalize()` | Không biết EF, không biết quyền |
| `PagingExtensions` | Bắt buộc | `ToPagedResultAsync(query, page, size, ct)` | Không tự ý `ToList` |
| `SortMap<T>` | Helper | Whitelist sort key → `IOrderedQueryable<T>` | Không nhận sort string thô |
| `VietnamDateRange` | Bắt buộc | Chuyển DateOnly/ngày VN → `[startUtc, endUtcExclusive)` | Không dùng `TimeZoneInfo.Local` |
| `SearchTerm` | Helper | Trim, min length, chuẩn hóa bỏ dấu (nếu cần) | Không tự quyết định collation |
| `ListCache` | Helper | Bọc `IMemoryCache` trước; `HybridCache` chỉ khi cần stampede/tag | Không cache list lớn/per-user |
| `LookupQuery` | Helper | Option/picker có `Take(≤200)` + keyword | Không trả toàn bảng |

Không tạo `IRepository<T>` chung: EF query vẫn viết trong module để giữ projection/index rõ ràng.

### 3.2 Tách vai trò (khái niệm, không bắt buộc 5 file)

```
Features/<Module>/Queries/
  <Module>ListFilter.cs      // chỉ normalize tham số
  <Module>Scope.cs           // chỉ quyền/scope
  <Module>ListQuery.cs       // IQueryable + Where/OrderBy/Projection
  <Module>ListEnricher.cs    // batch Contains(ids) + dictionary
  <Module>ListService.cs     // ghép: filter → scope → count → page → enrich → map
```

Module nhỏ có thể gộp `Filter/Scope/Query` vào một file; điều quan trọng là **không trộn** quyền, filter, EF query, mapping và cache trong cùng một method.

Quy tắc:

- `PageService` chỉ lấy caller/session, **không** viết EF.
- `Filter` **không** chứa quyền; `Scope` **không** chứa keyword.
- `ListQuery` **không** gọi service khác, không cache, không map sang DTO phức tạp.
- `Enricher` chỉ chạy trên ID của trang hiện tại, tối đa 1 query/loại quan hệ.
- `ListService` là nơi duy nhất quyết định cache (nếu có).
- Mapper thuần, không query.

### 3.3 Bản đồ gộp logic trùng

| Hiện trạng | Đích |
|---|---|
| 2 `PagedResult<T>` (Common + Tenants) | 1 type duy nhất |
| 6 kiểu `Normalize` page size (reset 20 / clamp 100 / 200 cứng) | `ListQueryBase` clamp 1..100, mặc định 20 |
| 3 kiểu sort (cứng, string không whitelist, enum) | `SortMap<T>` whitelist cho tất cả |
| 4 kiểu search (Contains, Collate CI_AI, OrdinalIgnoreCase, TextSearchNormalizer) | `SearchTerm` + khai báo mode từng cột |
| Nhiều helper ngày (VN, UTC, server-local, đóng/nửa mở) | `VietnamDateRange` + quy ước khoảng |
| 8 trang tự viết `<table>` | `DataTable` + `TablePager` |
| 6 màn “nạp hết → lọc/sort/page RAM” | cùng pipeline server-side |
| N+1 owner/ảnh/file/count phòng | batch enricher/aggregate query |
| Audit đọc file 2 lần/trang | `AuditSnapshot` đọc 1 lần + cache |
| Options/picker không giới hạn | `LookupQuery` cap 200 + keyword |
| Cache rải rác (`IMemoryCache` dashboard, `ManagerToggle` khai báo nhưng không dùng) | `ListCache` bọc `IMemoryCache` trước; `HybridCache` chỉ khi cần |
| Filter state mỗi trang một kiểu | `FilterState` + URL sync dùng chung |

### 3.4 Quy tắc bắt buộc

- **BẮT BUỘC:** filter, sort, page, count chạy trước khi materialize; `CountAsync` dùng đúng predicate của page.
- **BẮT BUỘC:** `PageSize` clamp 1..100, mặc định 20; sort phải có whitelist và tie-breaker `Id`.
- **BẮT BUỘC:** enricher tối đa 1 query/loại quan hệ, chỉ trên ID của trang hiện tại.
- **BẮT BUỘC:** option/picker có `Take(≤200)`; list không giới hạn không được lọc/sort/page ở browser.
- **BẮT BUỘC:** kiểm tra quyền, tổng tiền và `TotalCount` ở server.
- **NÊN:** `AsNoTracking` + projection; `TagWith`; compiled query cho hot path; index covering; cache reference/snapshot.
- **CẤM:** generic repository; cache list nghiệp vụ; `ToList` trước filter trên bảng lớn; đưa aggregate tài chính xuống client.

## 4. Tối ưu theo tầng

### 4.1 SQL Server

1. **Index theo truy vấn thật**, không theo cảm giác:
   - Composite: cột equality trước, cột range/sort sau; thêm `Id` để tie-break.
   - `INCLUDE` các cột projection để thành covering index, tránh key lookup.
   - Filtered index cho `IsDeleted = 0`, `Status` tập con.
   - Không over-index bảng ghi nhiều (`Payments`, `Invoices`, `StoredFiles`).
2. **SARGable**:
   - Ngày: `>= start AND < endExclusive` thay vì `Date = ...`/`AddDays`.
   - Không bọc cột trong hàm (`YEAR(PeriodStart) = @y` → dùng range).
   - `LIKE '%kw%'` không dùng index; nếu cần search lớn, dùng cột chuẩn hóa/full-text.
3. **Count**:
   - `CountAsync` trên cùng predicate; không count sau khi `ToList`.
   - Nếu UI không cần tổng chính xác, có thể bỏ count ở một số màn (đánh đổi UX).
4. **Pagination**:
   - Giữ `OFFSET/FETCH` vì pager cần số trang; đảm bảo sort unique (`... , Id`).
   - Đo page sâu; nếu chậm, keyset cho invoice/payment (next/prev), vẫn giữ count riêng.
5. **Vận hành**: Query Store, execution plan, statistics; cân nhắc RCSI nếu bị block; không partition ở quy mô này.

### 4.2 EF Core

| Kỹ thuật | Áp dụng vào |
|---|---|
| `AsNoTracking` + `Select` projection | Mọi list/report read-only; sửa query hóa đơn đang track entity, tenant list đang materialize entity |
| `AsSplitQuery` | Chỉ khi `Include` nhiều collection (ví dụ Payment + Proofs) và thấy cartesian; projection thường tốt hơn |
| Compiled query | Hot path cố định: notification, KPI, lookup theo id; không dùng cho filter động phức tạp |
| Parameter hóa | Query động phải dùng biến/parameter; tránh constant trong expression |
| `ExecuteUpdate/Delete` | Bulk mark-read notification, purge, sweep; thay vòng lặp từng dòng |
| `TagWith` | Gắn `list:invoices`, `report:revenue` để đọc log/Query Store |
| Streaming | Export Excel/PDF dùng `AsAsyncEnumerable`, không `ToList` cả bảng |
| Pooled factory | Cân nhắc `AddPooledDbContextFactory` cho factory path; giữ `AddDbContext` cho Identity nếu cần |
| Query cache | Theo dõi hit rate; tránh query shape thay đổi do constant |

### 4.3 Local cache (IMemoryCache trước, HybridCache tùy chọn)

- **Single local server: `IMemoryCache` hiện có gần như đủ.** Chỉ thêm `HybridCache` khi cần chống stampede/tag hoặc chạy nhiều instance; nếu thêm, pin package 10.x và viết test cho invalidation.
- Tag invalidation là **logical invalidation** (đánh dấu cũ, không xóa vật lý ngay); không dựa vào nó để giải phóng RAM tức thì.
- Cache reference data dùng chung; **không cache list nghiệp vụ per-user**. Dashboard/audit snapshot theo owner là ngoại lệ nhỏ, TTL ngắn, có size limit/LRU.
- Tag gợi ý: `ref:properties`, `ref:services`, `ref:tariffs`, `ref:managers`, `ref:permissions`, `dash:{ownerId}`, `audit:{ownerId}`.
- TTL gợi ý:

| Dữ liệu | TTL | Invalidate |
|---|---|---|
| Danh mục dịch vụ/tariff | 5–30 phút | Khi sửa giá/danh mục |
| Quyền/toggle/vai trò | 5 phút | Khi đổi quyền |
| Dashboard/Home snapshot | 30–60 giây | Nút Làm mới hoặc ghi hóa đơn/payment |
| Filter options | 5–15 phút | Khi thêm/xóa reference |
| Audit snapshot | Theo `mtime + length` | File đổi; không cần TTL riêng |
| List nghiệp vụ | **Không cache** | Dữ liệu thay đổi liên tục |

- Dùng `GetOrCreateAsync` để chống stampede; không đưa keyword thô vào key.
- **Không coi `MaximumPayloadBytes` là cơ chế bảo vệ RAM duy nhất**: tự giới hạn kích thước entry/TTL; kiểm tra hành vi L1 theo version package.
- Cache **không** thay thế index: cache hit tốt, cache miss vẫn phải có query tối ưu.

### 4.4 Background worker (chỉ khi số đo chứng minh cần)

| Job | Việc | Khi nào |
|---|---|---|
| Report summary | Tính lại doanh thu/chi phí/công suất vào bảng summary | Chỉ khi aggregate SQL vẫn vượt SLO |
| Audit compaction | Parse/compact file hoặc chuyển sang bảng | Chỉ khi file lớn và đo thấy chậm |
| Cleanup | Xóa draft/trash quá hạn, purge theo lô | Giữ ở mức hiện có, không mở rộng |

**Ngoài phạm vi mặc định:** cache warm, notification queue, mark-all-read nền. Chỉ xem xét khi có nhu cầu đo được.

Quy tắc nếu làm: `BackgroundService` + `IServiceScopeFactory`/`IDbContextFactory`; `PeriodicTimer`; cancellation; idempotent; watermark; **không** chạy song song vô hạn; **không** `Task.Run` trong request.

### 4.5 Blazor UI

- Server-side paging là chính; `Virtualize` chỉ cho list client-side có trần (documents, catalog nếu vẫn giữ client).
- `@key` cho row; `ShouldRender` cho component nặng; parameter immutable (`int/string/Guid`) để giảm rerender.
- Debounce 300 ms dùng chung; `CancellationTokenSource` hủy request cũ khi filter đổi.
- Filter ghi lên URL (`replaceState`) để F5/back giữ trạng thái.
- Không giữ list lớn trong circuit; không `PersistentState` cho list.
- Tránh lambda trong vòng lặp lớn; tránh `CaptureUnmatchedValues` ở row.
- **Bỏ `[StreamRendering]` khỏi phạm vi**: các trang đang `@rendermode InteractiveServer`; thuộc tính này chủ yếu có nghĩa ở SSR/prerender, lợi ích thấp khi đã interactive.

## 5. Quyết định theo module & điểm nóng

### 5.1 Quyết định theo module

| Module | Pattern đọc | Cache | Ghi chú |
|---|---|---|---|
| Rooms / Properties / Tenants | Server page | Không | Sort whitelist; lookup options bounded |
| Invoices | Server page + enrich batch | Không | Contract options bounded/cache reference |
| Contracts | Server page + stats SQL | Không | Property names cache reference |
| Payments | Server page + projection proof | Không | Không `Include` toàn bộ proof nếu chỉ cần metadata |
| Approvals | Server page inbox | Lookup room/property/user | Enrich 9 batch chỉ cho trang hiện tại |
| Incidents / Documents | Server filter + page | Không | Bỏ lọc/sort/page client |
| Service catalog / prices | Server page | Reference cache | Tách lookup khỏi list |
| Accounts / Owners | Server page + aggregate | Không | Owner impact 3 query/trang |
| Audit | Một snapshot/request + page RAM | Snapshot theo file | Không đọc file 2 lần |
| Reports | Aggregate SQL + page DB | Summary chỉ khi cần | Không materialize cả bảng |
| Notifications | `Take(30)` + unread count | Unread ngắn hạn | Index theo recipient |

### 5.2 Điểm nóng

| Điểm nóng | Thiết kế đích | Độ phức tạp sau |
|---|---|---|
| Cost report | Trước hết aggregate SQL + page DB; chỉ precompute summary nếu số đo vượt SLO; bắt buộc kỳ hoặc 12 tháng | `O(page)` |
| Revenue report | Aggregate SQL + page DB; summary table chỉ khi cần; export stream | `O(page)` |
| Occupancy | Tính SQL theo tháng; bảng `MonthlyOccupancy` chỉ khi đo thấy chậm | `O(số tháng × số nhà)` nhỏ |
| Audit | Đọc file 1 lần/request + cache snapshot; dài hạn chuyển `AuditLogEntries` (đã có bảng) | `O(page)` |
| OwnerAdmin | 3 query aggregate theo `ownerIds` của trang | `O(1)` query/trang |
| Incidents/Documents | Filter/page server; bỏ lọc client | `O(page)` |
| Approvals | Page inbox ở DB; enrich 9 batch chỉ cho trang hiện tại; cache lookup reference | `O(page)` + 1–2 query |
| Home | Đọc cache/summary nếu có; chạy song song các khối độc lập; không gọi report nặng | ~6–10 query ấm |

## 6. Ngoài phạm vi

- Redis/distributed cache (single local server chưa cần).
- Generic repository/CQRS/microservice.
- Cache mọi list nghiệp vụ.
- Đưa filter/aggregate nặng xuống browser.
- Over-index toàn bộ cột.
- Background cache warm/notification queue/audit compaction khi chưa có nhu cầu đo được.

## 7. Nghiệm thu

> SLO dưới đây là **mục tiêu kỹ thuật**, chưa phải cam kết. Đo bằng p95 trên dataset M; số query lấy từ EF command log/interceptor, không dùng ước lượng thay cho số đo.

| Tiêu chí | Mục tiêu |
|---|---|
| List CRUD p95 (dataset M) | ≤ 300 ms |
| List có enrich p95 | ≤ 500 ms |
| `/home` cache nguội p95 | ≤ 800 ms |
| Report tab p95 | ≤ 1,5 s |
| Audit p95 | ≤ 1 s |
| Query/trang list thường | ≤ 5; **ngoại lệ ghi rõ**: Approvals ≤ 10 (9 batch + inbox), Home ấm ≤ 10 |
| Query/trang OwnerAdmin | ≤ 4 |
| Index | Seek cho **filter/sort chính**; keyword `LIKE '%...%'` được phép scan có trần hoặc dùng cột chuẩn hóa/full-text — không hứa seek |
| Cache | Reference data hit rate > 80% khi có traffic lặp; dashboard đo bằng **số query DB giảm**, không đặt hit rate; tự giới hạn entry size |
| Bộ nhớ | Cache nghiệp vụ ≤ 50 MB; circuit không giữ list > 1 trang |
| Không còn | `ToListAsync` trên bảng lớn không có page/trần; filter client trên list không giới hạn |

## 8. Nguồn tham khảo

- [EF Core — Efficient Querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- [EF Core — Advanced Performance Topics](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics)
- [EF Core — Pagination (offset vs keyset)](https://learn.microsoft.com/en-us/ef/core/querying/pagination)
- [SQL Server — Index Architecture and Design Guide](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-index-design-guide?view=sql-server-ver17)
- [ASP.NET Core Blazor — Rendering performance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0)
- [ASP.NET Core Blazor — Component virtualization](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization?view=aspnetcore-10.0)
- [ASP.NET Core — HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0)
- [ASP.NET Core — Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Milan Jovanović — EF Core Performance Guide](https://milanjovanovic.tech/blog/ef-core-performance-guide)
- [Milan Jovanović — Pagination in ASP.NET Core](https://milanjovanovic.tech/blog/pagination-aspnetcore)
