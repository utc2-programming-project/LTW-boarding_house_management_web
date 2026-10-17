# Đề xuất hợp đồng DTO khách thuê và service P3

> **Trạng thái:** Bản đề xuất, **chưa có giá trị nào được nhóm xác nhận**. Mọi trường, quy tắc và quyền dưới đây là *đề xuất* để nhóm chốt. Chỗ ghi *"Cần chốt"* là câu hỏi mở. Không đưa vào code hoặc schema cho đến khi mục tương ứng được nhóm chốt.
>
> **Phạm vi:** phân hệ P3 (khách thuê), đợt 1B (chốt DTO) và đợt 1C (viết service trên SQL). Không làm việc ngoài hai đợt này và ngoài các quyết định D1–D17.
> **Phối hợp:** Phúc (trường form UI), Đào (DTO/service, quy tắc chọn khách cho hợp đồng), Tính (quyền VHT-6, schema Tenant VHT-5).
> **Nguồn đối chiếu:** `docs/task/thao_tac_va_quyen.xlsx`, quyết định D1–D17, schema Tenant INIT hiện có (Đào đối chiếu với bảng thật), `de_xuat_hop_dong_storage.md` (tệp giấy tờ của khách thuê).

---

## 0.1 Chốt đối chiếu DTO với schema (đợt 1C)

> Ghi lúc viết service 1C. Đây là các điểm DTO 1B đề xuất khác ERD VHT-5, đã thống nhất hướng áp dụng trong code. Chỗ nào còn **cần nhóm chốt** thì ghi rõ.

| Điểm | DTO 1B đề xuất | ERD VHT-5 / thực tế | Đã áp dụng ở 1C |
|---|---|---|---|
| Kiểu `Id` | `Guid` | `int identity` (PK `Tenants`) | **`int`**: sửa `TenantDetailDto`, `TenantListItemDto`, `TenantPickerItemDto`, `TenantPickerRequest.ExcludeTenantIds` sang `int` |
| Xóa mềm | cột `IsActive` | cột `IsDeleted` (thùng rác) | Thêm **`IsActive`** (hoạt động) và **giữ `IsDeleted`** (thùng rác VHT-99), theo quy ước Property/Room |
| Tên trường | `PhoneNumber`, `IdNumber` | cột `Phone`, `CitizenId` | Giữ tên cột DB; DTO dùng `PhoneNumber`/`IdNumber`, đổi chiếu tại `TenantMapping` |
| `RowVersion` | `byte[]` | chưa có | Thêm cột `rowversion` để chống ghi đè |
| `Gender` | enum | chưa có | Thêm cột, lưu **chuỗi** (`HasConversion<string>()`) cho dễ đọc/ổn định |
| Chỉ mục `CitizenId` | duy nhất khi khác null | `UNIQUE ... IsDeleted = 0` | Đổi thành `... AND [IsActive] = 1` cho **khớp logic service** (chỉ kiểm trùng hồ sơ đang sử dụng) |
| Chỉ mục `UserId` | duy nhất khi khác null | index thường, chờ chốt | Đặt **unique filtered** (`UserId IS NOT NULL AND IsDeleted = 0`); cần Đào/Phát xác nhận 1 tài khoản ↔ 1 hồ sơ |

> Lý do: ERD VHT-5 là nguồn chuẩn schema (AGENTS yêu cầu không tự suy luận schema), và P2 (Property/Room) đã dùng `int Id` + tách `IsActive`/`IsDeleted`. DTO 1B là *đề xuất*, chưa được chốt với schema nên phải đổi theo.

**Vị trí code 1C:** `NhaTro/Web/Features/Tenants/TenantService.cs` (+ `ITenantService.cs`, `TenantNormalizer.cs`, `TenantErrorCodes.cs`, `Mapping/TenantMapping.cs`), migration mới `20261017005536_ExtendTenantProfile`, đăng ký DI ở `Program.cs`.

**Đã xử lý theo rà soát:**
- Race trùng khi tạo/sửa/kích hoạt: bắt `DbUpdateException` vi phạm unique (SQL 2601/2627) → trả `DuplicateTenant` thay vì 500.
- Lộ thông tin: chỉ kèm `PossibleDuplicates` khi hồ sơ trùng **nằm trong phạm vi** người gọi (mục 3).
- `ScopeQuery` giữ tracking cho hàm ghi; query đọc thêm `.AsNoTracking()`; thao tác sửa/ngừng sử dụng đi qua `ScopeQuery` để sau này VHT-25 thu hẹp phạm vi là có hiệu lực.
- `SetActiveAsync` nhận `RowVersion`, bắt `ConcurrencyConflict`, và không ghi `UpdatedAt` khi trạng thái không đổi.
- `(Page-1)*PageSize` tính bằng `long`, chặn tràn `int`.
- `Normalize()` là nguồn duy nhất kẹp giá trị (bỏ `[Range]` ở `Page/PageSize/Limit` để không 400 trước khi kẹp): `PageSize` >100 → 100, `Limit` >50 → 50; `ExcludeTenantIds` cắt còn 100.
- Kiểm định dạng sau chuẩn hóa: SĐT 10–11 số bắt đầu 0; `DateOfBirth` không ở tương lai; `IdIssuedDate` không trước `DateOfBirth`.
- `NormalizeIdNumber` dùng ASCII + in hoa; `NormalizePhone` nhận `+84`/`0084`/`84…`; tìm kiếm bỏ qua từ khóa SĐT/giấy tờ < 3 ký tự và chuẩn hóa tên trước khi so.
- Kiểm **độ dài sau `Normalize`** (khớp cột DB: 200/20/256/20/300/200/500) → `InvalidRequest` + thông báo `InvalidLength`. `[MaxLength]` của DTO chỉ chạy trên chuỗi thô, không thay thế được bước này (chống `SqlException` truncation → 500).
- Chặn client gửi **trường lạ** ngoài DTO: `ConfigureHttpJsonOptions(... UnmappedMemberHandling = Disallow)` ở `Program.cs` (áp cho JSON của HTTP endpoint; Blazor Server gọi service nội bộ nên không qua JSON).
- `Gender` lưu chuỗi; `Normalize()` chỉ gọi ở service (không gọi lại trong mapping).
- Liên kết tài khoản: thêm `LinkAccountAsync`/`UnlinkAccountAsync` — chặn liên kết nhầm (tài khoản phải tồn tại, đúng vai trò `KhachThue`, chưa gắn hồ sơ khác); FK `UserId → AspNetUsers` (Restrict) + unique filtered index là lớp chặn cuối; vi phạm unique khi đua → `AccountAlreadyLinked` thay vì 500.
- Integration test SQL Server thật (`NhaTro/NhaTro.IntegrationTests/Features/Tenants/TenantServiceIntegrationTests.cs`): lưu rồi tải lại đúng; RowVersion cũ → `ConcurrencyConflict`; trùng giấy tờ → `DuplicateTenant` kèm hồ sơ trùng; liên kết/gỡ tài khoản (sai vai trò, không tồn tại, đã gắn hồ sơ khác); khách chỉ sửa 3 trường; **kiểm quyền** (Admin và Khách thuê bị chặn khỏi nghiệp vụ quản lý; khách chỉ xem hồ sơ của mình, không thấy ghi chú); **độ dài sau chuẩn hóa**. Chạy trên LocalDB, tự `Skip` nếu máy/CI không có.

**Còn cần nhóm chốt / follow-up:**
- Trùng `PhoneNumber`: tạm dùng mã `DuplicatePhone` (ngoài 9 mã ở mục 9) để UI hỏi "vẫn lưu".
- `IdNumber`: bắt buộc khi tạo hay chỉ trước khi ký hợp đồng; có nhận hộ chiếu không; có mã hóa cột không.
- Phạm vi Quản lý: hiện **toàn hệ thống**, chờ VHT-24/VHT-25.
- `UserId` **đang áp theo hướng 1-1** (unique filtered index + `LinkAccountAsync` chặn): cần Đào/Phát xác nhận; nếu cho 1 tài khoản nhiều hồ sơ thì bỏ unique, nới `LinkAccountAsync`, và sửa `GetAsync`/`GetOwnAsync`/`UpdateOwnAsync`.
- `HasActiveContract` (filter) hiện **bỏ qua im lặng**, và giá trị trong list/picker luôn `false`; `TenantInUse` chưa kiểm — chờ bảng hợp đồng.
- Ngữ nghĩa PUT của `UpdateTenantRequest`: form sửa phải gửi đủ trường; xác nhận có cho xóa `IdNumber` khi hồ sơ đã gắn hợp đồng không.
- Khách đã `IsActive = false` hiện vẫn tự sửa được — cần xác nhận có chặn không.
- Email/`PhoneNumber` của hồ sơ tách khỏi tài khoản đăng nhập — chốt có đồng bộ không.
- Khôi phục hồ sơ thùng rác (VHT-98/99) sẽ cần `IgnoreQueryFilters()` + kiểm trùng `CitizenId` trước khi khôi phục.
- Testcontainers **không dùng** (tech-stack cấm Docker); integration test chạy LocalDB và tạo schema bằng `EnsureCreated` (xem chú thích `SqlServerFixture`). CI (`.github/workflows/ci.yml`) đã thêm `dotnet tool restore`, `dotnet ef migrations has-pending-model-changes` (exit code 1 khi thiếu migration), bước **chạy toàn bộ migration lên một DB trống** (`dotnet ef database update --connection`, bắt lỗi migration/SQL — bổ sung cho `EnsureCreated`), và bước khởi động LocalDB trước `dotnet test`.
- `TenantScope.EnsureCanAccess` (VHT-6) hiện chưa dùng ở production: **giữ**, khớp pattern `PropertyScope`/`RoomScope` (cả bộ 3 chỉ được `GuardTests` dùng, sẽ nối khi có controller/UI ở VHT-17). Cần chốt phạm vi Quản lý (VHT-24/VHT-25) trước khi dùng.
- Collation/tìm tiếng Việt không dấu (`nguyen` → `Nguyễn`) phụ thuộc collation DB; cần chốt.

---

## 0. Phạm vi và vị trí code

**Phạm vi hợp đồng này:**

| Nội dung | Có trong hợp đồng |
|---|---|
| Trường hồ sơ khách thuê và trường nào được sửa | Có (mục 2, 5) |
| Phân biệt khách thuê (hồ sơ) và tài khoản đăng nhập | Có (mục 1) |
| Phát hiện hồ sơ trùng | Có (mục 3) |
| DTO list / detail / filter / cập nhật / chọn khách | Có (mục 6) |
| Quyền và phạm vi dữ liệu | Có (mục 4) |
| Thay đổi schema Tenant gửi Tính | Có (mục 8) |
| Tệp giấy tờ của khách thuê (CCCD…) | **Không** — thuộc hợp đồng storage (`RelatedEntityType = Tenant`) |
| Nghiệp vụ hợp đồng thuê, thanh toán, sự cố | **Không** — ngoài P3 |

**Vị trí code (đề xuất, theo cấu trúc feature-oriented, Đào xác nhận):**

| Thành phần | Vị trí |
|---|---|
| DTO | `NhaTro/Web/Features/Tenants/DTOs/` (namespace `Web.Features.Tenants.DTOs`) |
| Service / Controller | `NhaTro/Web/Features/Tenants/` |

**Nguồn chuẩn:** tài liệu này là hợp đồng; DTO và service phải khớp mục 6 và 7.

---

## 1. Khách thuê và tài khoản đăng nhập

Hai khái niệm tách biệt, không dùng chung bảng:

| | Khách thuê (`Tenant`) | Tài khoản đăng nhập (`User`) |
|---|---|---|
| Bản chất | Hồ sơ người thuê: họ tên, giấy tờ, liên hệ | Danh tính đăng nhập: tên đăng nhập, mật khẩu, vai trò |
| Ai tạo | Chủ trọ / Quản lý | Theo luồng tài khoản (VHT-6, Tính) |
| Có thể tồn tại độc lập | Có — hồ sơ có thể tạo trước, chưa có tài khoản | Có — tài khoản Chủ trọ/Quản lý/Admin không có hồ sơ khách thuê |
| Xóa | Chỉ ngừng sử dụng (xóa mềm); không xóa nếu còn hợp đồng liên quan | Theo luồng tài khoản |

**Liên kết:** `Tenant.UserId` (nullable) trỏ tới tài khoản của khách. Quy tắc đề xuất:
- Một tài khoản vai trò Khách thuê gắn với **tối đa một** hồ sơ (`UserId` duy nhất khi khác null).
- Khóa/xóa tài khoản **không** xóa hồ sơ; ngừng sử dụng hồ sơ **không** tự khóa tài khoản.
- Khách thuê chỉ thấy và sửa hồ sơ có `UserId` trùng với tài khoản đang đăng nhập.
- Ai thực hiện việc gắn tài khoản vào hồ sơ và ở feature nào: *Cần chốt* (Tính / Đào).

---

## 2. Trường hồ sơ khách thuê

| Trường | Kiểu | Bắt buộc khi tạo | Duy nhất | Ghi chú |
|---|---|---|---|---|
| `Id` | Guid | Tự sinh | Có | Khóa chính. Kiểu phải khớp quy ước PK của schema (Tính xác nhận) |
| `FullName` | string | Có | Không | Họ tên theo giấy tờ |
| `PhoneNumber` | string | Có | Không (cảnh báo trùng, mục 3) | Chuẩn hóa trước khi lưu |
| `Email` | string | Không | Không | |
| `IdNumber` | string | *Cần chốt* (bắt buộc khi tạo hay chỉ bắt buộc trước khi ký hợp đồng) | Có, khi khác null và chưa ngừng sử dụng | Số CCCD/CMND, dữ liệu nhạy cảm |
| `DateOfBirth` | date | Không | Không | |
| `Gender` | enum | Không | Không | Giá trị enum *cần chốt* |
| `PermanentAddress` | string | Không | Không | Địa chỉ thường trú |
| `IdIssuedDate` | date | Không | Không | *Cần hỏi Phúc/Đào: mẫu hợp đồng có cần không* |
| `IdIssuedPlace` | string | Không | Không | *Như trên* |
| `Note` | string | Không | Không | Ghi chú nội bộ của Chủ trọ/Quản lý, **khách thuê không thấy** |
| `IsActive` | bool | Tự đặt `true` | Không | Ngừng sử dụng = `false` (xóa mềm) |
| `UserId` | string? | Không | Có, khi khác null | Liên kết tài khoản (mục 1) |
| `CreatedAt` / `UpdatedAt` | datetime | Tự sinh | Không | |
| `RowVersion` | byte[] | Tự sinh | Không | Chống ghi đè khi hai người cùng sửa (mục 5) |

**Đề xuất:** chỉ giữ các trường cần cho hồ sơ và hợp đồng ở phiên bản đầu. Không thêm trường khác khi Phúc chưa xác nhận trên UI.

**Chuẩn hóa dữ liệu (đề xuất):**
- `FullName`: cắt khoảng trắng đầu/cuối và gộp khoảng trắng thừa.
- `PhoneNumber`: bỏ khoảng trắng, dấu chấm, gạch; đổi tiền tố `+84` thành `0`.
- `IdNumber`: chỉ giữ chữ số (và chữ cái nếu hộ chiếu — *cần chốt có nhận hộ chiếu không*).
- `Email`: cắt khoảng trắng, so sánh không phân biệt hoa thường.

---

## 3. Phát hiện hồ sơ trùng

Thực hiện ở service khi **tạo** và khi **sửa `IdNumber` / `PhoneNumber`** (trừ khi khách thuê tự sửa, xem 6.7). Đối tượng so sánh là các hồ sơ còn sử dụng (`IsActive = true`) **trên toàn hệ thống**, không giới hạn theo phạm vi của người gọi (riêng trùng `PhoneNumber` chỉ xét trong phạm vi của người gọi, xem bảng). Nếu hồ sơ trùng nằm ngoài phạm vi, vẫn trả `DuplicateTenant` nhưng không kèm thông tin hồ sơ đó (`PossibleDuplicates` để trống).

| Tiêu chí | Mức | Xử lý đề xuất |
|---|---|---|
| Trùng `IdNumber` (sau chuẩn hóa) | **Trùng cứng** | Chặn, trả `DuplicateTenant` (HTTP 409), kèm `Id` và `FullName` của hồ sơ đang có để người dùng mở hồ sơ đó (chỉ khi hồ sơ nằm trong phạm vi của người gọi) |
| Trùng `PhoneNumber` (sau chuẩn hóa) | **Nghi trùng** | Không chặn. Trả danh sách `PossibleDuplicates` để UI hỏi xác nhận; gửi lại với `ConfirmDuplicatePhone = true` thì tạo. Với trùng `PhoneNumber`, chỉ kiểm tra và liệt kê các hồ sơ **trong phạm vi của người gọi**; cảnh báo trùng số điện thoại chỉ để hỗ trợ nhập liệu, không có ràng buộc ở DB nên không cần quét toàn hệ thống |
| Trùng `FullName` + `DateOfBirth` | **Nghi trùng** | *Cần chốt có kiểm tra không* |
| Trùng `Email` | Không kiểm tra | Một email có thể dùng chung (gia đình) |

**Ràng buộc ở DB (Tính thực hiện):** chỉ mục duy nhất có lọc trên `IdNumber` (khi khác null và `IsActive = 1`) và trên `UserId` (khi khác null). Service vẫn kiểm trước để trả lỗi rõ ràng, DB là lớp chặn cuối khi có hai request đồng thời.

**Hồ sơ đã ngừng sử dụng có trùng không:** đề xuất không tính là trùng, và khi tạo mới trùng với hồ sơ ngừng sử dụng thì gợi ý kích hoạt lại. *Cần chốt.*

---

## 4. Kiểm quyền — Ai được làm gì

> **Tính cần đối chiếu bảng này với `docs/task/thao_tac_va_quyen.xlsx` và VHT-6** trước khi chốt. "Khách thuê" luôn bị giới hạn trong hồ sơ của chính mình. **Admin không có quyền nghiệp vụ** nên bị chặn toàn bộ API khách thuê (trả `403`).

| Thao tác | Chủ trọ | Quản lý | Khách thuê | Admin |
|---|---|---|---|---|
| Xem danh sách / lọc | Có | Có *(phạm vi: cần chốt)* | **Không** | **Không** |
| Xem chi tiết | Có | Có *(phạm vi: cần chốt)* | Chỉ hồ sơ của mình | **Không** |
| Tạo hồ sơ | Có | Có | **Không** | **Không** |
| Sửa hồ sơ (mọi trường) | Có | Có | **Không** | **Không** |
| Tự sửa hồ sơ của mình | — | — | Có, **chỉ các trường ở mục 5.2** | **Không** |
| Ngừng / kích hoạt lại hồ sơ | Có | Cần chốt | **Không** | **Không** |
| Chọn khách (cho hợp đồng) | Có | Có | **Không** | **Không** |
| Gắn / gỡ tài khoản | Cần chốt | Cần chốt | **Không** | **Không** |

**Phạm vi dữ liệu của Quản lý:** toàn bộ nhà trọ hay chỉ nhà được phân công. *Cần chốt* (cùng câu hỏi ở hợp đồng storage, nên chốt một lần cho cả hệ thống). Dù chốt thế nào, **việc lọc theo phạm vi nằm trong service**, không chỉ ở controller, để mọi nơi gọi service (kể cả hàm chọn khách) đều bị giới hạn như nhau.

**Đánh đổi cần nhắc khi chốt phạm vi (mục 3):** vì kiểm trùng chạy **trên toàn hệ thống**, Quản lý có thể dò xem một số CCCD đã tồn tại trong hệ thống hay chưa qua lỗi `DuplicateTenant`, kể cả với hồ sơ ngoài phạm vi. Cân nhắc điểm này khi chốt phạm vi của Quản lý, nhất là nếu chọn "chỉ nhà được phân công".

**Che dữ liệu nhạy cảm:**
- Danh sách và hàm chọn khách chỉ trả `IdNumberMasked` (ví dụ `********1234`), không trả số đầy đủ.
- `IdNumber` đầy đủ chỉ có trong chi tiết cho Chủ trọ, Quản lý (trong phạm vi) và chính khách thuê đó.
- `Note` không bao giờ trả cho khách thuê.
- Không ghi `IdNumber` đầy đủ vào log.

---

## 5. Trường nào được sửa

### 5.1 Chủ trọ / Quản lý

Sửa được mọi trường ở mục 2, trừ `Id`, `CreatedAt`, `UpdatedAt`, `RowVersion` và `UserId` (đổi liên kết tài khoản đi qua thao tác riêng, mục 1).

### 5.2 Khách thuê tự sửa (đề xuất)

| Trường | Tự sửa | Lý do |
|---|---|---|
| `PhoneNumber` | Có | Thông tin liên hệ thay đổi thường xuyên |
| `Email` | Có | Như trên |
| `PermanentAddress` | Có | Do khách tự khai |
| `FullName`, `IdNumber`, `DateOfBirth`, `Gender`, `IdIssuedDate`, `IdIssuedPlace` | **Không** | Gắn với giấy tờ đã xác minh; đổi qua Chủ trọ/Quản lý |
| `Note`, `IsActive`, `UserId` | **Không** | Quản lý nội bộ |

*Cần Phúc xác nhận trường nào khách thấy và sửa trên UI; Tính xác nhận quyền.*

### 5.3 Chống ghi đè đồng thời

Mọi thao tác sửa gửi kèm `RowVersion` đã đọc. Nếu bản ghi đã đổi, trả `ConcurrencyConflict` (HTTP 409) và UI tải lại dữ liệu.

---

## 6. DTO

> **Vị trí code:** `NhaTro/Web/Features/Tenants/DTOs/` — namespace `Web.Features.Tenants.DTOs`
>
> **Kiểu `Id`:** theo mục 0.1, `Id` là `int` (không phải `Guid` như bản đề xuất đầu).

### 6.1 `TenantFilterRequest` — lọc danh sách

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `Keyword` | string? | Không | Tìm theo họ tên, số điện thoại, số giấy tờ (sau chuẩn hóa); khớp một phần |
| `IsActive` | bool? | Không | `null` = tất cả; mặc định đề xuất `true` |
| `HasAccount` | bool? | Không | Lọc theo đã/chưa có tài khoản |
| `HasActiveContract` | bool? | Không | *Cần chốt: phụ thuộc dữ liệu hợp đồng, Đào xác nhận* |
| `Page` | int | Không | Bắt đầu từ 1; mặc định đề xuất 1 |
| `PageSize` | int | Không | Mặc định và tối đa: *cần chốt* |
| `SortBy` | enum | Không | `FullName` / `CreatedAt`; mặc định đề xuất `FullName` |
| `SortDirection` | enum | Không | `Asc` / `Desc` |

### 6.2 `PagedResult<T>` — kết quả phân trang dùng chung

| Trường | Kiểu |
|---|---|
| `Items` | `List<T>` |
| `Page` | int |
| `PageSize` | int |
| `TotalCount` | int |

### 6.3 `TenantListItemDto` — một dòng trong danh sách

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Guid | |
| `FullName` | string | |
| `PhoneNumber` | string | |
| `IdNumberMasked` | string? | Chỉ 4 số cuối |
| `IsActive` | bool | |
| `HasAccount` | bool | `UserId != null` |
| `HasActiveContract` | bool | *Cần chốt như 6.1* |

### 6.4 `TenantDetailDto` — chi tiết

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Guid | |
| `FullName` | string | |
| `PhoneNumber` | string | |
| `Email` | string? | |
| `IdNumber` | string? | Đầy đủ, theo quy tắc che ở mục 4 |
| `DateOfBirth` | date? | |
| `Gender` | enum? | |
| `PermanentAddress` | string? | |
| `IdIssuedDate` / `IdIssuedPlace` | date? / string? | Nếu chốt giữ |
| `Note` | string? | **Chỉ trả cho Chủ trọ/Quản lý** |
| `IsActive` | bool | |
| `HasAccount` | bool | Không trả `UserId` thô cho khách thuê |
| `CreatedAt` / `UpdatedAt` | DateTimeOffset / DateTimeOffset? | `UpdatedAt` null khi chưa sửa lần nào |
| `RowVersion` | string | Base64, gửi lại khi sửa |

### 6.5 `CreateTenantRequest` — tạo hồ sơ

Các trường nhập ở mục 2 (`FullName`, `PhoneNumber`, `Email`, `IdNumber`, `DateOfBirth`, `Gender`, `PermanentAddress`, `IdIssuedDate`, `IdIssuedPlace`, `Note`), cộng:

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `ConfirmDuplicatePhone` | bool | Mặc định `false`; `true` để tạo dù nghi trùng số điện thoại (mục 3) |

### 6.6 `UpdateTenantRequest` — Chủ trọ / Quản lý sửa

Các trường ở mục 5.1 và bắt buộc có `RowVersion`. Có thêm `ConfirmDuplicatePhone` như 6.5.

### 6.7 `UpdateOwnProfileRequest` — khách thuê tự sửa

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `PhoneNumber` | string | |
| `Email` | string? | |
| `PermanentAddress` | string? | |
| `RowVersion` | string | Bắt buộc |

Chỉ có đúng các trường ở mục 5.2; không có trường nào khác để tránh khách gửi thừa dữ liệu rồi bị gán nhầm.

Khi khách thuê tự sửa (`UpdateOwnAsync`), service **không** kiểm trùng số điện thoại và **không** trả `PossibleDuplicates`, để khách không thấy được hồ sơ của người khác.

### 6.8 Chọn khách (cho hợp đồng, Đào dùng)

`TenantPickerRequest`:

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Keyword` | string? | Họ tên / số điện thoại / số giấy tờ |
| `Limit` | int | Số kết quả tối đa; mặc định và trần *cần chốt* |
| `ExcludeTenantIds` | List<Guid>? | Loại trừ khách đã chọn |

`TenantPickerItemDto`:

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Guid | |
| `FullName` | string | |
| `PhoneNumber` | string | |
| `IdNumberMasked` | string? | |
| `HasActiveContract` | bool | *Cần chốt như 6.1* |

**Quy tắc chọn khách:** chỉ trả hồ sơ `IsActive = true` trong phạm vi quyền của người gọi. Có cho chọn khách đang có hợp đồng hiệu lực để lập hợp đồng khác hay không là *quy tắc của Đào, cần chốt*; hợp đồng này chỉ cung cấp cờ `HasActiveContract` để Đào áp dụng.

### 6.9 `TenantResult` — kết quả thao tác tạo / sửa / ngừng sử dụng

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Succeeded` | bool | |
| `Tenant` | `TenantDetailDto?` | `null` khi thất bại |
| `ErrorCode` | string? | Mã ở mục 9, `null` khi thành công |
| `ErrorMessage` | string? | `null` khi thành công |
| `PossibleDuplicates` | `List<TenantListItemDto>?` | Có khi nghi trùng hoặc trùng cứng; còn lại `null` |

Dùng cùng khuôn `Succeeded` / `ErrorCode` / `ErrorMessage` như `UploadResult` của hợp đồng storage để frontend xử lý lỗi thống nhất.

---

## 7. Service (đợt 1C)

`ITenantService` (tên và chữ ký là đề xuất; Đào chốt khi viết code). Mọi hàm nhận **ngữ cảnh người gọi** (mã người dùng, vai trò, phạm vi dữ liệu) và kiểm quyền bên trong service.

| Hàm | Mô tả | Trả về |
|---|---|---|
| `ListAsync(filter, caller)` | Danh sách có lọc, phân trang, theo phạm vi | `PagedResult<TenantListItemDto>` |
| `GetAsync(id, caller)` | Chi tiết một hồ sơ | `TenantResult` |
| `GetOwnAsync(caller)` | Hồ sơ của khách thuê đang đăng nhập | `TenantResult` |
| `CreateAsync(request, caller)` | Tạo hồ sơ, kiểm trùng | `TenantResult` |
| `UpdateAsync(id, request, caller)` | Chủ trọ/Quản lý sửa | `TenantResult` |
| `UpdateOwnAsync(request, caller)` | Khách thuê tự sửa | `TenantResult` |
| `SetActiveAsync(id, isActive, caller)` | Ngừng / kích hoạt lại | `TenantResult` |
| `SearchForPickerAsync(request, caller)` | Chọn khách theo quyền | `List<TenantPickerItemDto>` |

**Endpoint đề xuất:**

| Thao tác | Endpoint | Thành công |
|---|---|---|
| Danh sách | `GET /api/tenants` | 200 + `PagedResult<TenantListItemDto>` |
| Chi tiết | `GET /api/tenants/{id}` | 200 + `TenantResult` |
| Hồ sơ của tôi | `GET /api/tenants/me` | 200 + `TenantResult` |
| Tạo | `POST /api/tenants` | 201 + `TenantResult` |
| Sửa | `PUT /api/tenants/{id}` | 200 + `TenantResult` |
| Tự sửa | `PUT /api/tenants/me` | 200 + `TenantResult` |
| Ngừng / kích hoạt lại | `PATCH /api/tenants/{id}/active` | 200 + `TenantResult` |
| Chọn khách | `GET /api/tenants/picker` | 200 + danh sách `TenantPickerItemDto` |

**Ràng buộc route:** dùng `{id:guid}` cho `GET /api/tenants/{id}` (và các endpoint `{id}` khác) để không nhầm với `/me` và `/picker`.

**Ngừng sử dụng:** không cho ngừng sử dụng hồ sơ đang có hợp đồng hiệu lực (trả `TenantInUse`, 409). Điều kiện kiểm tra dựa vào dữ liệu hợp đồng, Đào xác nhận cách truy vấn.

**Kích hoạt lại:** khi `SetActiveAsync(isActive = true)`, phải kiểm trùng `IdNumber` với các hồ sơ đang sử dụng và trả `DuplicateTenant` nếu trùng; nếu không sẽ vi phạm chỉ mục duy nhất (mục 3, mục 8).

**Khi chưa có hợp đồng trong schema:** các cờ `HasActiveContract` và kiểm tra `TenantInUse` để chờ, không làm ở đợt 1C nếu bảng hợp đồng chưa sẵn sàng.

---

## 8. Thay đổi schema Tenant gửi Tính (VHT-5)

So với Tenant INIT hiện có (Đào đối chiếu và đánh dấu cột nào đã có):

| Việc | Chi tiết |
|---|---|
| Thêm / đổi cột | Các trường ở mục 2 chưa có trong bảng; kiểu và độ dài chuỗi do Tính đề xuất, nhóm chốt |
| Cột `UserId` | Nullable, khóa ngoại tới bảng tài khoản, chỉ mục duy nhất có lọc khi khác null |
| Chỉ mục `IdNumber` | Duy nhất có lọc: `IdNumber IS NOT NULL AND IsActive = 1` |
| Chỉ mục tìm kiếm | Trên `FullName` và `PhoneNumber` để lọc theo `Keyword` |
| `IsActive` | Cột bool, mặc định `1` |
| `RowVersion` | Cột rowversion/timestamp để chống ghi đè |
| `CreatedAt`, `UpdatedAt` | Tự cập nhật |
| Mã hóa / bảo vệ `IdNumber` | *Cần chốt có mã hóa cột hay chỉ hạn chế truy cập và che hiển thị* |

Tính thực hiện thay đổi schema; Đào làm DTO/service sau khi nhận schema (đợt 1C).

---

## 9. Lỗi trả về

| Tình huống | HTTP | Mã lỗi | Thông báo hiển thị |
|---|---|---|---|
| Thiếu trường bắt buộc / dữ liệu sai định dạng | 400 | `InvalidRequest` | "Dữ liệu gửi lên không hợp lệ" |
| Chưa đăng nhập | 401 | `Unauthorized` | "Vui lòng đăng nhập để tiếp tục" |
| Không đủ quyền (gồm Admin) | 403 | `Forbidden` | "Bạn không có quyền thực hiện thao tác này" |
| Không tìm thấy hồ sơ (hoặc ngoài phạm vi) | 404 | `TenantNotFound` | "Không tìm thấy khách thuê" |
| Trùng số giấy tờ | 409 | `DuplicateTenant` | "Đã có khách thuê với số giấy tờ này" |
| Hồ sơ đã bị người khác sửa | 409 | `ConcurrencyConflict` | "Hồ sơ vừa được cập nhật, vui lòng tải lại" |
| Không thể ngừng sử dụng vì còn hợp đồng hiệu lực | 409 | `TenantInUse` | "Khách thuê đang có hợp đồng, không thể ngừng sử dụng" |
| Tài khoản đã gắn với hồ sơ khác | 409 | `AccountAlreadyLinked` | "Tài khoản này đã được gắn với một hồ sơ khác" |
| Lỗi hệ thống | 500 | `TenantOperationFailed` | "Không thể thực hiện, vui lòng thử lại" |

**Đề xuất:** với hồ sơ nằm ngoài phạm vi của người gọi, trả `TenantNotFound` thay vì `Forbidden` để không lộ việc hồ sơ có tồn tại. Nghi trùng số điện thoại **không** là lỗi; trả thành công kèm `PossibleDuplicates` hoặc yêu cầu xác nhận như mục 3. *Cần chốt.*

---

## 10. Ví dụ — mẫu kết quả cho Phúc

> Dữ liệu minh họa. Ở đợt 1C, Đào gửi Phúc **kết quả thật** từ SQL theo đúng cấu trúc này.
> Lưu ý mục 0.1: `id` là số nguyên (`int`), không phải chuỗi Guid như ví dụ dưới; các ví dụ giữ nguyên để dễ đối chiếu cấu trúc trường.

### 10.1 Danh sách — `GET /api/tenants?keyword=nguyen&page=1&pageSize=20`

**Response 200:**

```json
{
  "items": [
    {
      "id": "3f1c9a52-7b0e-4d0a-9c1e-5d2f6a8b1c01",
      "fullName": "Nguyễn Văn An",
      "phoneNumber": "0901234567",
      "idNumberMasked": "********4821",
      "isActive": true,
      "hasAccount": true,
      "hasActiveContract": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

### 10.2 Chi tiết — `GET /api/tenants/{id}`

**Response 200:**

```json
{
  "succeeded": true,
  "tenant": {
    "id": "3f1c9a52-7b0e-4d0a-9c1e-5d2f6a8b1c01",
    "fullName": "Nguyễn Văn An",
    "phoneNumber": "0901234567",
    "email": "an.nguyen@example.com",
    "idNumber": "079201004821",
    "dateOfBirth": "2001-05-14",
    "gender": "Male",
    "permanentAddress": "12 Lê Lợi, Quận 1, TP.HCM",
    "note": null,
    "isActive": true,
    "hasAccount": true,
    "createdAt": "2026-10-01T09:00:00+07:00",
    "updatedAt": "2026-10-10T15:20:00+07:00",
    "rowVersion": "AAAAAAAAB9E="
  },
  "errorCode": null,
  "errorMessage": null,
  "possibleDuplicates": null
}
```

### 10.3 Tạo thất bại — Trùng số giấy tờ

**Response 409:**

```json
{
  "succeeded": false,
  "tenant": null,
  "errorCode": "DuplicateTenant",
  "errorMessage": "Đã có khách thuê với số giấy tờ này",
  "possibleDuplicates": [
    {
      "id": "3f1c9a52-7b0e-4d0a-9c1e-5d2f6a8b1c01",
      "fullName": "Nguyễn Văn An",
      "phoneNumber": "0901234567",
      "idNumberMasked": "********4821",
      "isActive": true,
      "hasAccount": true,
      "hasActiveContract": true
    }
  ]
}
```

> Nếu hồ sơ trùng nằm ngoài phạm vi của người gọi, `possibleDuplicates` là `null` (xem mục 3).

### 10.4 Sửa thất bại — Hồ sơ vừa bị người khác cập nhật

**Response 409:**

```json
{
  "succeeded": false,
  "tenant": null,
  "errorCode": "ConcurrencyConflict",
  "errorMessage": "Hồ sơ vừa được cập nhật, vui lòng tải lại",
  "possibleDuplicates": null
}
```

### 10.5 Không đủ quyền (ví dụ Admin hoặc Khách thuê gọi danh sách)

**Response 403:**

```json
{
  "succeeded": false,
  "tenant": null,
  "errorCode": "Forbidden",
  "errorMessage": "Bạn không có quyền thực hiện thao tác này",
  "possibleDuplicates": null
}
```

> Lỗi của mọi endpoint dùng chung khung `TenantResult`, kể cả endpoint danh sách; danh sách thành công thì trả `PagedResult<TenantListItemDto>`.

### 10.6 Mẫu kết quả theo DTO 1C (gửi Phúc)

> Cấu trúc đúng như service `TenantService` trả về (camelCase mặc định của ASP.NET Core). `id` là **số nguyên**. Đây là mẫu theo DTO đã viết; bản chụp từ SQL thật bổ sung khi DB demo sẵn sàng (chưa chạy được SQL trong phiên này).

Danh sách (`GET /api/tenants?keyword=nguyen&page=1&pageSize=20`):

```json
{
  "items": [
    {
      "id": 1,
      "fullName": "Nguyễn Văn An",
      "phoneNumber": "0901234567",
      "idNumberMasked": "********4821",
      "isActive": true,
      "hasAccount": true,
      "hasActiveContract": false
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Chi tiết (`GET /api/tenants/{id}`):

```json
{
  "succeeded": true,
  "tenant": {
    "id": 1,
    "fullName": "Nguyễn Văn An",
    "phoneNumber": "0901234567",
    "email": "an.nguyen@example.com",
    "idNumber": "079201004821",
    "dateOfBirth": "2001-05-14",
    "gender": "Male",
    "permanentAddress": "12 Lê Lợi, Quận 1, TP.HCM",
    "idIssuedDate": null,
    "idIssuedPlace": null,
    "note": null,
    "isActive": true,
    "hasAccount": true,
    "createdAt": "2026-10-01T09:00:00+00:00",
    "updatedAt": null,
    "rowVersion": "AAAAAAAAB9E="
  },
  "errorCode": null,
  "errorMessage": null,
  "possibleDuplicates": null
}
```

Chọn khách (`GET /api/tenants/picker`):

```json
[
  {
    "id": 1,
    "fullName": "Nguyễn Văn An",
    "phoneNumber": "0901234567",
    "idNumberMasked": "********4821",
    "hasActiveContract": false
  }
]
```

---

## Tóm tắt đề xuất

| Mục | Đề xuất |
|---|---|
| Khách thuê và tài khoản | Hai thực thể tách biệt; `Tenant.UserId` nullable, duy nhất khi khác null |
| Trường hồ sơ | `FullName`, `PhoneNumber` (bắt buộc), `Email`, `IdNumber`, `DateOfBirth`, `Gender`, `PermanentAddress`, `Note`, `IsActive`, `UserId`, thời gian, `RowVersion` (`IdIssuedDate`/`IdIssuedPlace` chờ xác nhận) |
| Trường khách tự sửa | `PhoneNumber`, `Email`, `PermanentAddress` |
| Phát hiện trùng | Trùng `IdNumber` → chặn (409); trùng `PhoneNumber` → cảnh báo, cho xác nhận |
| Quyền | Chủ trọ + Quản lý: xem/tạo/sửa/chọn khách; Khách thuê: chỉ hồ sơ của mình; Admin: bị chặn |
| Che dữ liệu | Danh sách/chọn khách chỉ trả `IdNumberMasked`; `Note` không trả cho khách |
| DTO | `TenantFilterRequest`, `PagedResult<T>`, `TenantListItemDto`, `TenantDetailDto`, `CreateTenantRequest`, `UpdateTenantRequest`, `UpdateOwnProfileRequest`, `TenantPickerRequest`, `TenantPickerItemDto`, `TenantResult` |
| Service | `ITenantService` với 8 hàm, kiểm quyền trong service |
| Lỗi | 9 mã (`InvalidRequest`, `Unauthorized`, `Forbidden`, `TenantNotFound`, `DuplicateTenant`, `ConcurrencyConflict`, `TenantInUse`, `AccountAlreadyLinked`, `TenantOperationFailed`) |
| Schema (gửi Tính) | Cột mới theo mục 2, chỉ mục duy nhất có lọc, `RowVersion`, chỉ mục tìm kiếm |
| Vị trí code | `NhaTro/Web/Features/Tenants/` |