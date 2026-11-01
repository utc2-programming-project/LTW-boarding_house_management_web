# VHT-30 — Rà soát lỗi đợt 2B (Duyệt hợp đồng và cập nhật phòng)

> **Phạm vi rà soát:** chỉ đợt **2B** — duyệt/từ chối hợp đồng, chống trùng hiệu lực, cập nhật hợp đồng + phòng cùng transaction.
> Không rà các đợt sau: ghép UI (VHT-31), gia hạn/thanh lý/hủy hợp đồng, trạng thái "Sắp hết hạn", chỉ số đầu kỳ (P5), báo cáo.
> **Nhánh rà:** `origin/VHT-30-duyet-hop-dong-va-cap-nhat-phong`, commit `1702ca9` (nền `dev` = `143e8a7`, không lệch).
> **Ngày rà:** 30/10/2026
> **Nguồn đối chiếu:** `docs/task/cong_viec_theo_tuan.md` (VHT-30), `docs/task/timeline_song_song.md`, `docs/VHT-9_trang_thai_ky_du_lieu_P4_P5.md`, `docs/thiet_ke_he_thong.md`, `docs/tech-stack/cong_nghe_su_dung.md`.

## 0. Bằng chứng chạy thật

| Hạng mục | Kết quả |
|---|---|
| `dotnet build NhaTro/NhaTro.slnx` | 0 error, 1 warning (xUnit2031 có sẵn, ngoài phạm vi) |
| `NhaTro.UnitTests` (toàn bộ) | 500/500 pass |
| `NhaTro.IntegrationTests` (SQL Server LocalDB thật) | 98/98 pass |
| Test Contracts (unit + integration) | 26 + 11 pass |
| `dotnet ef migrations has-pending-model-changes` | No changes (snapshot + migration khớp model) |

Kết luận nhanh (bản rà commit `1702ca9`): **code chạy được, đủ tiêu chí lõi 2B**, nhưng còn 11 lỗi logic/thiết kế dưới đây. Hai lỗi BUG-01, BUG-02 nên sửa **trước khi merge**.

> **Cập nhật 31/10/2026:** nhánh đã có commit `2f09e11` sửa BUG-01 → BUG-11. Xem **mục 7** để biết cái nào đã đạt và 6 lỗi mới phát sinh (trong đó 1 lỗi chặn merge).

## 1. Bảng tổng hợp lỗi

| Mã | Mức | Tóm tắt | Vị trí |
|---|---|---|---|
| BUG-01 | **Cao** | Không kiểm hợp đồng/phòng thuộc nhà trọ của người duyệt; `userId` sai kiểu (`int`) và không dùng | `ContractService.cs:25-34`, `IContractService.cs:9-13` |
| BUG-02 | **Cao** | Chấp nhận role `"Landlord"` không tồn tại trong `Roles` | `ContractService.cs:31` + test dùng `"Landlord"` |
| BUG-03 | Trung bình | Duyệt được hợp đồng cho phòng `Maintenance`, ghi đè thành `Rented` | `ContractService.cs:88-90, 107` |
| BUG-04 | Trung bình | Không chốt lại snapshot giá/cọc tại thời điểm duyệt (lệch VHT-9) | `ContractService.cs:105-107` |
| BUG-05 | Trung bình | `catch` rollback/reload có thể ném lỗi mới, che lỗi gốc | `ContractService.cs:115-124` |
| BUG-06 | Trung bình | Không có ràng buộc DB "1 phòng chỉ 1 hợp đồng hiệu lực", chỉ chặn ở service | `ContractService.cs:93-103` |
| BUG-07 | Thấp | Thiếu test bắt buộc theo tiêu chí 2B | `ContractServiceTests.cs`, `ContractServiceIntegrationTests.cs` |
| BUG-08 | Thấp | Test đồng thời kỳ vọng đúng 1 loại exception → dễ flaky | `ContractServiceIntegrationTests.cs:303-304` |
| BUG-09 | Thấp | `RejectReason` là `nvarchar(max)`, không giới hạn/không validate độ dài | `20261030085133_AddContractRejectReason.cs:13-17` |
| BUG-10 | Thấp | Thông báo lỗi tiếng Anh, không có mã lỗi/outcome để map 403/409 | `ContractService.cs:33, 41` |
| BUG-11 | Thấp | `RoomService` inject nhưng không dùng; khoá phòng cả khi từ chối | `ContractService.cs:17, 22, 63-66` |

## 2. Chi tiết từng lỗi

### BUG-01 — [Cao] Không kiểm phạm vi nhà trọ; `userId` sai kiểu và không được dùng

**Vị trí:** `NhaTro/Web/Features/Contracts/Services/ContractService.cs:25-34`; `IContractService.cs:9-13`

```csharp
public async Task<ContractResponseDto> ApproveContractAsync(
    Guid contractId,
    ApproveContractRequestDto request,
    int userId,          // không dùng ở bất kỳ đâu
    string userRole)
{
    if (userRole is not (Roles.ChuTro or "Landlord")) { throw ... }
```

**Nguyên nhân / sai ở đâu**
- ID tài khoản của project là `string` (`ApplicationUser.Id` = `nvarchar(450)`; test seed `Id = "contract-test-owner"`, `ContractServiceIntegrationTests.cs:317`). Tham số `int userId` không thể nhận ID thật → VHT-31 không dùng được, sẽ phải đổi chữ ký lần nữa.
- Không truy vết `Contract.RoomId → Room.PropertyId → Property.OwnerId` để xác nhận người duyệt là chủ nhà trọ chứa hợp đồng. Project đã có chuẩn `PropertyCaller(string UserId, string Role)` + `PropertyScope.EnsureOwnerCanAccess(ownerId, userId)` (VHT-6/VHT-24).

**Hệ quả:** Chủ trọ A duyệt được hợp đồng thuộc nhà trọ của Chủ trọ B; phòng của B bị đổi sang `Rented`. Đây là lỗi phân quyền, không phải chỉ là "thiếu test".

**Cách tái hiện:** tạo 2 Chủ trọ A/B, mỗi người 1 `Property`/`Room`; tạo hợp đồng `Pending` cho phòng của B; gọi `ApproveContractAsync(contractId, ..., userId: 1, Roles.ChuTro)` → duyệt thành công.

**Hướng sửa:** đổi tham số sang `PropertyCaller caller`, join `Properties` theo `contract.RoomId`, gọi `PropertyScope.EnsureOwnerCanAccess(property.OwnerId, caller.UserId)` trước khi mở transaction; bỏ `userId`/`userRole` rời.

**Test cần thêm:** `ApproveContractAsync_RejectsContractOfAnotherOwner`.

---

### BUG-02 — [Cao] Chấp nhận role `"Landlord"` ngoài nguồn chuẩn `Roles`

**Vị trí:** `ContractService.cs:31`; test phụ thuộc: `ContractServiceTests.cs:73-75, 99`; `ContractServiceIntegrationTests.cs:119`

```csharp
if (userRole is not (Roles.ChuTro or "Landlord"))
```

**Nguyên nhân:** `Roles.All` chỉ có `Admin`, `ChuTro`, `QuanLy`, `KhachThue` (`NhaTro/Web/Features/Accounts/Roles.cs`). `"Landlord"` không tồn tại trong Identity nhưng vẫn qua vòng kiểm quyền, và test đang "xác nhận" hành vi bằng role bịa.

**Hệ quả:** quy tắc "ai được duyệt" không khớp nguồn chuẩn; nếu sau này có nguồn role khác (SSO/seed) dùng `"Landlord"` thì mặc nhiên được duyệt; test không chứng minh đúng quy tắc.

**Cách tái hiện:** gọi `ApproveContractAsync(id, ..., 1, "Landlord")` → qua kiểm quyền, đi tiếp vào DB.

**Hướng sửa:** chỉ dùng `Roles.ChuTro`; sửa mọi `"Landlord"` trong test thành `Roles.ChuTro`.

---

### BUG-03 — [Trung bình] Duyệt được hợp đồng cho phòng đang `Maintenance` và ghi đè thành `Rented`

**Vị trí:** `ContractService.cs:88-90` (kiểm phòng) và `:107` (gán trạng thái)

```csharp
if (room.IsDeleted || !room.IsActive) { throw ... }  // không kiểm room.Status
...
room.Status = RoomStatus.Rented;
```

**Nguyên nhân:** `RoomStatus` có `Available = 0`, `Rented = 1`, `Maintenance = 2` (`NhaTro/Web/Features/Rooms/Enums/RoomStatus.cs`). Phòng đang bảo trì vẫn qua được điều kiện và bị đổi sang `Rented`, mất trạng thái bảo trì mà không cảnh báo.

**Cách tái hiện:** tạo phòng `Status = Maintenance` → tạo hợp đồng `Pending` cho phòng đó (nếu dữ liệu đã có sẵn, không qua `CreateContractAsync`) → duyệt → phòng thành `Rented`.

**Hướng sửa:** chặn khi `room.Status == RoomStatus.Maintenance` (hoặc chỉ cho duyệt khi `Available`); thêm test.

---

### BUG-04 — [Trung bình] Không chốt lại snapshot giá/cọc tại thời điểm duyệt

**Vị trí:** `ContractService.cs:105-107` (nhánh duyệt chỉ set `Status`, xoá `RejectReason`, set phòng)

**Nguyên nhân:** `docs/VHT-9_trang_thai_ky_du_lieu_P4_P5.md:34` quy định *"Snapshot hợp đồng: khi duyệt hoặc gia hạn, sao chép giá thuê, cọc ... vào hợp đồng"*. Hiện snapshot chỉ lấy lúc **tạo** (VHT-29). Nếu giá phòng đổi giữa lúc tạo và lúc duyệt, hợp đồng đã duyệt vẫn giữ giá cũ.

**Cách tái hiện:** tạo hợp đồng với `StandardPrice = 1.000.000` → sửa giá phòng thành `1.500.000` → duyệt → `SnapshotPrice` vẫn `1.000.000`, cọc vẫn tính theo giá cũ.

**Hướng sửa:** trong nhánh duyệt, đọc lại `room.StandardPrice`, tính lại cọc (`MoneyFormatter.RoundVnd(price * 0.20m)`) và ghi đè `SnapshotPrice`/`DepositAmount` **trong cùng transaction**; hoặc nếu nhóm chốt giữ snapshot lúc tạo thì sửa lại câu chữ VHT-9 và ghi rõ ở đây. Cần chốt trước khi sửa.

---

### BUG-05 — [Trung bình] `catch` có thể ném lỗi mới và che lỗi gốc

**Vị trí:** `ContractService.cs:115-124`

```csharp
catch
{
    await transaction.RollbackAsync();             // có thể ném
    await _context.Entry(contract).ReloadAsync();  // có thể ném
    if (lockedRoom is not null) { await _context.Entry(lockedRoom).ReloadAsync(); }
    throw;                                          // không tới được nếu dòng trên ném
}
```

**Nguyên nhân:** khi lỗi gốc là mất kết nối/timeout/deadlock (SQL 1205), `RollbackAsync`/`ReloadAsync` có thể ném `SqlException`/`InvalidOperationException` mới → caller nhận sai loại lỗi, mất thông điệp nghiệp vụ ("Phòng đã có hợp đồng hiệu lực...", "Chỉ hợp đồng đang chờ duyệt...").

**Hướng sửa:** bọc rollback/reload trong `try { } catch { }` (best-effort), hoặc dùng `try/finally` + chỉ reload khi kết nối còn mở; luôn giữ exception gốc.

---

### BUG-06 — [Trung bình] Không có ràng buộc DB cho "1 phòng chỉ 1 hợp đồng hiệu lực"

**Vị trí:** `ContractService.cs:93-103`

**Nguyên nhân:** quy tắc VHT-9 dòng 27 (*"Một phòng chỉ có một hợp đồng Hiệu lực hoặc Sắp hết hạn"*) hiện chỉ được kiểm ở service. Ghi trực tiếp DB, seed, script, hoặc một service khác không qua `ApproveContractAsync` vẫn tạo được 2 hợp đồng `Approved` trùng thời gian.

**Hệ quả:** dữ liệu sai không bị DB chặn; khi có thêm trạng thái "Sắp hết hạn" mà quên thêm vào điều kiện `other.Status == Approved` thì lỗ hổng trùng hiệu lực quay lại.

**Hướng sửa:** thêm filtered unique index/constraint ở DB (SQL Server) hoặc ít nhất ghi chú rõ + mở rộng điều kiện khi thêm trạng thái mới. (Ràng buộc "khoảng thời gian không chồng" không biểu diễn được bằng unique index đơn giản; có thể dùng trigger hoặc giữ service + test.)

---

### BUG-07 — [Thấp] Thiếu test bắt buộc theo tiêu chí 2B

**Vị trí:** `NhaTro/NhaTro.UnitTests/Features/Contracts/ContractServiceTests.cs`; `NhaTro/NhaTro.IntegrationTests/Features/Contracts/ContractServiceIntegrationTests.cs`

Các ca chưa có:
1. **Duyệt lặp cùng một hợp đồng**: `Pending → Approved` rồi gọi duyệt lần 2 (tuần tự). Hiện chỉ test hợp đồng đã `Approved` từ đầu (`RejectsContractThatIsNotPending`).
2. **Từ chối lần 2** hoặc **duyệt sau khi đã từ chối**.
3. **"Quản lý tự duyệt hợp đồng mình tạo bị chặn"** đúng ngữ cảnh: hiện chỉ có theory role `QuanLy` với `Guid.NewGuid()`; `Contract` chưa có `CreatedByUserId` nên không kiểm được "hợp đồng mình tạo".
4. **Duyệt hợp đồng của Chủ trọ khác** (BUG-01).
5. **Duyệt khi phòng `Maintenance`** (BUG-03) / phòng `IsActive = false` / phòng đã xoá mềm.
6. **Hai request đồng thời duyệt cùng một hợp đồng** (hiện chỉ test 2 hợp đồng khác nhau cùng phòng).

---

### BUG-08 — [Thấp] Test đồng thời kỳ vọng đúng một loại exception → dễ flaky

**Vị trí:** `ContractServiceIntegrationTests.cs:303-304`

```csharp
Assert.Single(results, result => result is null);
Assert.Single(results, result => result is InvalidOperationException);
```

**Nguyên nhân:** trên SQL Server thật, request thua có thể nhận `DbUpdateException` hoặc deadlock (1205) thay vì `InvalidOperationException`, dù hành vi nghiệp vụ vẫn đúng (chỉ 1 hợp đồng được duyệt). Test sẽ đỏ ngẫu nhiên trên CI.

**Hướng sửa:** chấp nhận cả `InvalidOperationException` và `DbUpdateException`/`SqlException` deadlock, hoặc map deadlock sang lỗi nghiệp vụ trong service.

---

### BUG-09 — [Thấp] `RejectReason` không giới hạn độ dài

**Vị trí:** `NhaTro/Web/Data/Migrations/20261030085133_AddContractRejectReason.cs:13-17`; `ApproveContractRequestDto.cs:7`

```csharp
migrationBuilder.AddColumn<string>(
    name: "RejectReason",
    table: "Contracts",
    type: "nvarchar(max)",
    nullable: true);
```

**Hệ quả:** lý do từ chối có thể dài tuỳ ý, không validate; UI hiển thị dễ vỡ layout; không thống nhất quy ước độ dài text nghiệp vụ của project.

**Hướng sửa:** đặt `nvarchar(500)` (hoặc mức nhóm chốt) + validate trong DTO/service, thêm test biên.

---

### BUG-10 — [Thấp] Thông báo lỗi tiếng Anh, không có mã lỗi/outcome

**Vị trí:** `ContractService.cs:33, 41`

```csharp
throw new UnauthorizedAccessException("Only the landlord can approve or reject a contract.");
throw new ArgumentException("A rejection reason is required when rejecting a contract.", nameof(request));
```

**Nguyên nhân:** các service P2/P3 dùng `RoomResult`/`TenantResult` với outcome `Blocked`/mã lỗi để controller map 403/409 và dịch VI/EN (`ITenantService`, `IRoomService`, `BusinessGuard`). Service này ném exception thô + message tiếng Anh, lẫn tiếng Việt ở các chỗ khác.

**Hệ quả (trong 2B):** không có hợp đồng lỗi rõ ràng cho tầng gọi; khi ghép VHT-31 sẽ phải đoán exception → map sai HTTP status.

**Hướng sửa:** trả kết quả có mã lỗi (`Forbidden`, `InvalidStatus`, `Overlap`, `ReasonRequired`...) hoặc ít nhất đặt mã lỗi + message tiếng Việt thống nhất; phần dịch VI/EN để VHT-31.

---

### BUG-11 — [Thấp] `RoomService` inject nhưng không dùng; khoá phòng cả khi từ chối

**Vị trí:** `ContractService.cs:17, 22` (dependency không dùng) và `:63-66` (khoá chạy trước khi biết duyệt/từ chối)

**Nguyên nhân / hệ quả**
- `IRoomService RoomService` được inject nhưng không tham chiếu; đợt này "Tính: query phòng" lại dùng raw SQL thay vì service. Gây hiểu nhầm về luồng truy vấn và giữ dependency chết.
- Nhánh từ chối không cần khoá phòng nhưng vẫn lấy `UPDLOCK, HOLDLOCK`, làm chậm/block các thao tác khác trên cùng phòng không cần thiết.

**Hướng sửa:** bỏ dependency nếu không dùng (hoặc chuyển sang `RoomScope` đúng chuẩn), và chỉ khoá khi `request.IsApproved`.

## 3. Tiêu chí 2B đã đạt (đã kiểm, không tính là lỗi)

| Tiêu chí | Trạng thái | Căn cứ |
|---|---|---|
| Duyệt đúng quyền thành công | Đạt | `ApproveContractAsync_ApprovesContractAndRentsRoom` |
| Duyệt lặp / sai trạng thái bị chặn | Đạt (logic) | kiểm `Pending` 2 lần: trước transaction và sau reload (`:53-56`, `:76-79`) |
| Trùng hiệu lực bị chặn | Đạt | overlap inclusive (`:93-98`), test `RejectsOverlappingApprovedContract` |
| Hai request đồng thời | Đạt | `UPDLOCK, HOLDLOCK` (`:63-66`) + test `ConcurrentOverlappingRequestsOnlyApproveOne` |
| Hợp đồng + phòng cùng transaction | Đạt | `BeginTransactionAsync` (`:58`), rollback + reload; test `RollsBackContractWhenRoomUpdateFails` |
| "Duyệt" gồm "Từ chối" có lý do | Đạt | bắt buộc `RejectReason` (`:38-43`), trim (`:84`), lưu DB; test `RejectionPersistsReasonAndStatus` |
| Chỉ Chủ trọ; Quản lý và Admin bị chặn | Đạt | theory `RejectsNonLandlordRoles` |
| Chủ trọ tự duyệt (D4) | Đạt | không cấm tự duyệt |

## 4. Cần chốt nghiệp vụ (chưa xếp lỗi)

1. **Hợp đồng bắt đầu trong tương lai:** hiện duyệt là phòng chuyển `Rented` ngay, trong khi màn "phòng đang thuê" (VHT-25/VHT-36) chỉ tính khi `StartDate <= hôm nay`. Cần chốt: giữ `Rented` ngay hay chỉ đổi khi tới `StartDate`.
2. **Hợp đồng `Pending` khác cùng phòng:** hiện không bị chặn lúc duyệt (chỉ chặn `Approved`). Cần chốt có auto-hủy/từ chối các `Pending` còn lại hay không.
3. **Cọc và giá khi duyệt:** xem BUG-04, cần chốt theo VHT-9 hay giữ snapshot lúc tạo.

## 5. Ngoài phạm vi 2B (không rà, không tính lỗi ở đây)

- Ghép UI list/detail/tạo/duyệt, dịch VI/EN, thông báo lỗi ra người dùng → **VHT-31**.
- Trạng thái "Sắp hết hạn", gia hạn, thanh lý, hủy hợp đồng → đợt sau (P4).
- Chốt chỉ số điện/nước đầu kỳ khi duyệt → P5 (`VHT-9` mục 3; VHT-51/VHT-100).
- Chặn xoá phòng/khách khi còn hợp đồng hiệu lực → VHT-98/VHT-99.
- Phân quyền Quản lý theo tòa nhà (VHT-24) chỉ là **phụ thuộc kỹ thuật** cho cách sửa BUG-01: khi VHT-24 vào `dev`, `PropertyScope.Apply`/`RoomScope.Apply` có thêm tham số `IQueryable<PropertyManager>`; nếu chuyển `ContractService` sang dùng các hàm này thì phải truyền `db.PropertyManagers`.

## 6. Thứ tự xử lý đề xuất (cho commit `1702ca9`)

1. BUG-01 + BUG-02 (bắt buộc trước merge, đổi chữ ký service một lần cho VHT-31 dùng luôn).
2. BUG-03 + BUG-04 (cần chốt nghiệp vụ mục 4).
3. BUG-05, BUG-06.
4. BUG-07 (bổ sung test cho các case ở trên), BUG-08, BUG-09, BUG-10, BUG-11.

---

# 7. Rà lại ngày 31/10/2026 — commit `2f09e11`

> Tiêu đề commit: *"Fix trọn gói BUG-01 đến BUG-11..."*. Nhánh vẫn nền `dev` = `143e8a7`, không lệch.
> Phạm vi vẫn **chỉ 2B**.

## 7.1. Kết quả chạy thật

| Hạng mục | Kết quả |
|---|---|
| `dotnet build NhaTro/NhaTro.slnx` | 0 error (1 warning xUnit2031 có sẵn) |
| `NhaTro.UnitTests` (toàn bộ) | **501/501 pass** |
| `NhaTro.IntegrationTests` (toàn bộ, SQL Server LocalDB) | **100/104 pass — 4 fail** |
| Riêng test Contracts **khi không có trigger sót lại** | 16/16 pass |
| Riêng test `ContractsTable_RejectsOverlappingApprovedContractOutsideService` | 1/1 pass |
| `dotnet ef migrations has-pending-model-changes` | No changes |
| Áp migration lên DB mới (`dotnet ef database update`) | Done; trigger `TR_Contracts_PreventApprovedOverlap` được tạo, `RejectReason` = `nvarchar(500)` |

**Vì sao 4 test fail:** test `ContractsTable_...` tự tạo trigger trên DB dùng chung nhưng **không drop**, trigger còn lại làm các test sau chết — xem NEW-02.

## 7.2. Trạng thái 11 lỗi cũ

| Mã | Nội dung | Trạng thái | Bằng chứng |
|---|---|---|---|
| BUG-01 | Thiếu kiểm phạm vi nhà trọ; `userId` sai kiểu | **Đã sửa** | `ApproveContractAsync` nhận `PropertyCaller` (`ContractService.cs:22-26`), so `PropertyScope.IsOwner(propertyOwnerId, caller.UserId)` (`:99-117`); test `RejectsContractOwnedByAnotherOwner` |
| BUG-02 | Role `"Landlord"` ngoài `Roles` | **Đã sửa** | chỉ `caller.IsChuTro` (`:29`); `git grep "Landlord"` không còn trong code nghiệp vụ |
| BUG-03 | Duyệt phòng `Maintenance` | **Đã sửa** | chặn `RoomStatus.Maintenance` (`:153-158`); test `RejectsRoomUnderMaintenanceWithoutChangingIt` |
| BUG-04 | Không re-snapshot giá/cọc khi duyệt | **Đã sửa** | `SnapshotPrice = room.StandardPrice`, cọc tính lại (`:176-177`); test `SnapshotsCurrentRoomPriceAndDeposit` |
| BUG-05 | `catch` che lỗi gốc | **Đã sửa** | `TryRollbackAndReloadAsync` bọc từng bước, giữ exception gốc |
| BUG-06 | Thiếu ràng buộc DB 1 phòng/1 HĐ hiệu lực | **Đã sửa (nhưng đang lỗi — xem NEW-01)** | migration thêm trigger `TR_Contracts_PreventApprovedOverlap` + map lỗi 51030 |
| BUG-07 | Thiếu test 2B | **Đã sửa** | thêm `RejectsContractOwnedByAnotherOwner`, `BlocksManagerForContractTheyCreated`, `RejectsRoomUnderMaintenanceWithoutChangingIt`, `SnapshotsCurrentRoomPriceAndDeposit`, `DuplicateApprovalAndRejectionAreBlocked`, `ContractsTable_...` |
| BUG-08 | Test đồng thời dễ flaky | **Đã sửa một phần** | `IsExpectedConcurrentApprovalLoss` chấp nhận `ContractOperationException`/`DbUpdateException`/deadlock 1205 (xem NEW-04) |
| BUG-09 | `RejectReason` không giới hạn | **Đã sửa** | DTO `[MaxLength(500)]`, service chặn > 500, config `HasMaxLength(500)`, migration `nvarchar(500)`; 3 test biên |
| BUG-10 | Message tiếng Anh, không mã lỗi | **Đã sửa** | `ContractServiceErrors.cs` có `ContractErrorCodes` + message tiếng Việt, exception mang `ErrorCode` |
| BUG-11 | `RoomService` thừa; khoá phòng cả khi từ chối | **Đã sửa** | bỏ `IRoomService` khỏi constructor (`:20`); chỉ khoá phòng khi duyệt (`:76-90`) |

## 7.3. Lỗi phát sinh trong commit `2f09e11`

### NEW-01 — [CHẶN MERGE] Trigger DB xung đột với `OUTPUT` của EF Core → sau migration, duyệt/từ chối hợp đồng sẽ lỗi

**Vị trí:** `NhaTro/Web/Data/Migrations/20261030192845_LimitContractRejectReasonLength.cs:38-67` (tạo trigger) và `NhaTro/Web/Data/Configurations/Contracts/ContractConfiguration.cs` (thiếu `HasTrigger`).

**Hiện tượng:** EF Core cập nhật `Contracts` bằng câu lệnh có `OUTPUT` không kèm `INTO`; SQL Server chặn khi bảng có trigger đang bật:

```
UPDATE [Contracts] SET [Status] = @p0
OUTPUT 1
WHERE [ContractId] = @p1;

Microsoft.EntityFrameworkCore.DbUpdateException: Could not save changes because the target table
has database triggers. Please configure your table accordingly...
 ---> Microsoft.Data.SqlClient.SqlException: The target table 'Contracts' of the DML statement
      cannot have any enabled triggers if the statement contains an OUTPUT clause without INTO clause.
```

Tôi đã áp migration lên DB mới rồi chạy thử trực tiếp:

```
UPDATE [Contracts] SET [Status] = 1 OUTPUT INSERTED.[ContractId] WHERE 1 = 0;
-- Msg 334: The target table 'Contracts' ... cannot have any enabled triggers
--          if the statement contains an OUTPUT clause without INTO clause.
```

**Hệ quả:** Đây là lỗi **production**, không chỉ lỗi test. Sau khi chạy migration:
- Duyệt/từ chối hợp đồng (`SaveChangesAsync`) → `DbUpdateException` 334, thao tác không hoàn tất.
- Nhánh `catch` map lỗi 51030 không bao giờ chạy (SQL Server chặn bằng 334 trước khi trigger kịp `THROW 51030`), nên "chốt DB" của BUG-06 vô hiệu.
- Test hiện không bắt được vì `SqlServerFixture` dùng `EnsureCreated` (không tạo trigger từ migration); chỉ test tự tạo trigger mới lộ ra.

**Cách sửa (chọn 1):**
1. Khai báo trigger trong model để EF dùng `OUTPUT ... INTO` (khuyến nghị, giữ được chốt DB):
   ```csharp
   // NhaTro/Web/Data/Configurations/Contracts/ContractConfiguration.cs
   builder.ToTable(table => table.HasTrigger("TR_Contracts_PreventApprovedOverlap"));
   ```
   Sau đó chạy lại `dotnet ef migrations has-pending-model-changes` + toàn bộ test; cập nhật fixture để test chạy trên schema có trigger.
2. Bỏ trigger khỏi migration, chỉ giữ kiểm tra trong service (quay lại BUG-06 mức "chỉ service", phải ghi rõ trong tài liệu).

**Test cần có:** duyệt/từ chối thành công trên DB **đã áp migration** (có trigger); hai request đồng thời trên DB có trigger.

---

### NEW-02 — [CAO] Test tạo trigger nhưng không drop → làm hỏng 4–5 test khác (CI đỏ)

**Vị trí:** `ContractServiceIntegrationTests.cs:379-402` (`CREATE TRIGGER` trong test `ContractsTable_RejectsOverlappingApprovedContractOutsideService`), không có `DROP TRIGGER` ở `finally`.

**Hệ quả:** trigger nằm lại trong DB của fixture dùng chung. Các test chạy sau đó cập nhật `Contracts` đều chết với lỗi NEW-01. Kết quả thực tế:
- Chạy cả bộ: **4 fail** (`ApprovesContractAndRentsRoom`, `RejectionPersistsReasonAndStatus`, `SnapshotsCurrentRoomPriceAndDeposit`, `DuplicateApprovalAndRejectionAreBlocked`); có lần **5 fail** do thứ tự test khác.
- Chạy riêng nhóm Contracts **không có** test trigger: 16/16 pass.
- Chạy riêng test trigger: 1/1 pass.

⇒ Test suite **phụ thuộc thứ tự**, không thể dùng làm CI.

**Cách sửa:** `try { ... } finally { DROP TRIGGER IF EXISTS ... }`, hoặc tạo trigger một lần ở fixture và reset giữa các test; tốt nhất là cho fixture áp migration thật (NEW-01) thay vì `EnsureCreated`.

---

### NEW-03 — [TRUNG BÌNH] Test "chốt DB" không kiểm trigger của migration

**Vị trí:** `ContractServiceIntegrationTests.cs:379-402`

Test **tự viết lại** câu `CREATE TRIGGER` thay vì dùng trigger do migration tạo. Vì vậy:
- Nếu migration tạo trigger sai/thiếu, test vẫn xanh.
- Không phát hiện được xung đột NEW-01 ở môi trường thật.

**Cách sửa:** dùng chung một nguồn DDL (fixture tạo trigger giống migration, hoặc áp migration cho DB test), và thêm test hành vi duyệt khi trigger tồn tại.

---

### NEW-04 — [THẤP] Test đồng thời chấp nhận mọi `DbUpdateException`

**Vị trí:** `ContractServiceIntegrationTests.cs:481-483`

```csharp
private static bool IsExpectedConcurrentApprovalLoss(Exception? exception) => exception is
    ContractOperationException or DbUpdateException ||
    exception is SqlException { Number: 1205 };
```

Chấp nhận **mọi** `DbUpdateException`, kể cả lỗi trigger 334 của NEW-01 → test đồng thời có thể "xanh giả" trong khi chức năng đang hỏng. Nên chỉ chấp nhận đúng mã lỗi overlap/ deadlock, còn lại phải fail.

---

### NEW-05 — [THẤP] `IgnoreQueryFilters` khi kiểm chủ sở hữu cho phép duyệt nhà trọ đã xoá mềm

**Vị trí:** `ContractService.cs:99-103`

`Properties` có query filter `!IsDeleted` (`PropertyConfiguration.cs:16`), nhưng service dùng `.IgnoreQueryFilters()` nên vẫn đọc được `OwnerId` của nhà trọ đã xoá mềm; nếu phòng chưa bị xoá thì hợp đồng vẫn duyệt được. Cần chốt: chặn khi `Property.IsDeleted` (thêm điều kiện) hay chấp nhận. Ngoài ra có thể bỏ `IgnoreQueryFilters()` ở đây (không cần thiết như với `Rooms`).

---

### NEW-06 — [THẤP] Chất lượng migration/cấu hình

- Migration tên `LimitContractRejectReasonLength` nhưng lại tạo **trigger chống trùng** (`:38-67`) → tên không phản ánh nội dung, khó tra khi rollback.
- `ContractConfiguration` (tầng `Web.Data`) phụ thuộc hằng số của DTO (`ApproveContractRequestDto.MaxRejectReasonLength`) → coupling tầng Data → Features.
- Trigger dùng `UPDLOCK, HOLDLOCK` quét theo `RoomId`; nên kiểm tra index hỗ trợ `(RoomId, Status, StartDate, EndDate)` để tránh lock escalation khi nhiều hợp đồng.

## 7.4. Kết luận đợt rà 31/10

- **Tiêu chí chức năng 2B: ĐẠT.** Toàn bộ BUG-01 → BUG-11 đã được sửa đúng hướng, có test tương ứng; logic duyệt/từ chối/transaction/đồng thời tốt hơn bản trước.
- **Chưa đạt để merge:** NEW-01 là lỗi production (duyệt hợp đồng sẽ lỗi sau khi áp migration) và NEW-02 làm CI đỏ. Bắt buộc sửa NEW-01 + NEW-02 trước khi mở PR về `dev`.
- Việc còn lại: NEW-03 (test đúng schema thật), NEW-04 (siết assertion), NEW-05/NEW-06 (chốt nghiệp vụ + dọn dẹp).

**Thứ tự sửa đề xuất:** NEW-01 → NEW-02 → chạy lại toàn bộ unit + integration (phải 501 + 104 xanh, không phụ thuộc thứ tự) → NEW-03/NEW-04 → NEW-05/NEW-06.
