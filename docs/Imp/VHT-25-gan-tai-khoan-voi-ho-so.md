# VHT-25 — Gắn tài khoản với hồ sơ khách và liên kết thuê (đợt 2B)

- **Nhánh:** `VHT-25-gắn-tai-khoản-với-hồ-sơ-va-lien-kết-thue` (tạo từ `dev`)
- **Phạm vi:** Sprint 2B, subsystem P3: gắn user ↔ hồ sơ khách + truy vấn hồ sơ/phòng/hợp đồng
  đang thuê **của một khách** từ liên kết thật.
- **Không làm (đợt 2C):** ghép `HasActiveContract`/`RoomName` vào danh sách + picker khách,
  lọc danh sách theo hợp đồng, và nối UI trang khách (VHT-36). Phần này phụ thuộc VHT-30
  (duyệt hợp đồng) và được giữ ở nhánh `VHT-120-ghep-lien-ket-thue-that-p3`.
- **Nguồn quyết định:** `docs/task/de_xuat_hop_dong_khach_thue.md` mục 1/4, `docs/task/cong_viec_theo_tuan.md` (VHT-25),
  `docs/task/timeline_song_song.md` (2B).

## 1. Kết quả bàn giao 2B

| Hạng mục | Trạng thái | Vị trí |
|---|---|---|
| Gắn tài khoản vào hồ sơ, kiểm vai trò Khách thuê | Xong | `TenantService.LinkAccountAsync` |
| Gỡ liên kết, giữ nguyên hồ sơ | Xong | `TenantService.UnlinkAccountAsync` |
| Danh sách tài khoản chưa gắn để UI chọn | Xong | `TenantService.SearchUnlinkedAccountsAsync` |
| DTO cho UI (trạng thái đã/chưa liên kết) | Xong | `TenantDetailDto.HasAccount`, `TenantAccountLinkDialog.razor` |
| Chặn trùng liên kết ở service + DB | Xong | kiểm trước trong service; unique index có lọc `Tenant.UserId` trong `TenantConfiguration` |
| **Truy vấn hồ sơ + phòng/hợp đồng đang thuê theo liên kết thật** | **Xong** | `GetRentalOverviewAsync`, `GetOwnRentalOverviewAsync`, `BuildRentalOverviewAsync` |
| **DTO hợp đồng/phòng cho UI** | **Xong** | `TenantRentalOverviewDto`, `TenantRentalContractDto`, `TenantRentalResult` |
| Chốt quy tắc gắn/gỡ và cách thay liên kết | Xong | `docs/task/de_xuat_hop_dong_khach_thue.md` §1 + tài liệu này |
| Test hồi quy | Xong | `TenantRentalLinkIntegrationTests`, `TenantServiceIntegrationTests`, `P1P3RoleAccessTests` |

## 2. Quy tắc đã chốt

### 2.1. Gắn/gỡ tài khoản

1. **Ai gắn/gỡ:** chỉ **Chủ trọ**. Quản lý và Admin bị chặn (`UnauthorizedAccessException` → 403).
2. **Một tài khoản tối đa một hồ sơ**, một hồ sơ tối đa một tài khoản.
   - Tài khoản đã gắn hồ sơ khác → `AccountAlreadyLinked`.
   - Hồ sơ đã có tài khoản, gọi gắn lại → `AccountAlreadyLinked` (không ghi đè).
   - Tài khoản không tồn tại / sai vai trò → `InvalidRequest`.
3. **Cách thay liên kết:** gỡ liên kết cũ → gắn tài khoản mới. Không có thao tác "replace" ghi đè.
4. **Đồng thời:** unique index có lọc `[UserId] IS NOT NULL AND [IsDeleted] = 0` là lớp chặn cuối;
   service bắt `DbUpdateException` unique và trả `AccountAlreadyLinked`.
5. **Không lộ dữ liệu tài khoản:** DTO chỉ trả `HasAccount` (bool?), không trả `UserId` thô;
   Quản lý nhận `null` (D9).

### 2.2. Truy vấn phòng/hợp đồng đang thuê

- Nguồn: `Tenant -> ContractMember -> Contract -> Room -> Property`. **Không** suy từ `Room.Status`.
- `IsCurrent = Status == Approved && StartDate <= hôm nay <= EndDate`.
  Hợp đồng `Pending`, đã hết hạn hoặc chưa tới ngày đều không phải "đang thuê".
- `CurrentRental` là hợp đồng đang hiệu lực; `Rentals` giữ toàn bộ hợp đồng của khách
  (đang thuê trước, rồi lịch sử mới nhất trước) để hiển thị cả quá khứ.
- Khách chưa thuê: trả thành công với `Rentals` rỗng và `CurrentRental = null` (không phải lỗi).
- Quyền:
  - Chủ trọ/Quản lý: xem theo `ScopeQuery` của service.
  - Khách thuê: `GetOwnRentalOverviewAsync` chỉ trả hồ sơ có `Tenant.UserId` trùng tài khoản;
    gọi hồ sơ người khác qua `GetRentalOverviewAsync` trả `TenantNotFound` (không lộ dữ liệu).
  - Admin: chặn toàn bộ.

## 3. Kiểm chứng

```powershell
dotnet build NhaTro/NhaTro.slnx
dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter "FullyQualifiedName~Tenants"
```

Kết quả đã chạy: build 0 error; unit test 492/492 pass; integration test nhóm Tenants 21/21 pass,
trong đó 3 test mới của VHT-25:
- `Ho_so_chua_thue_tra_ket_qua_rong_ro_rang`
- `Chi_tra_dung_hop_dong_cua_khach_va_tach_hien_tai_voi_lich_su`
- `Khong_xem_duoc_phong_cua_khach_khac`

## 4. Chuyển giao sang 2C (VHT-120)

Nhánh `VHT-120-ghep-lien-ket-thue-that-p3` giữ phần chưa làm: đưa `HasActiveContract`/`RoomName`
vào danh sách + picker, lọc `TenantFilterRequest.HasActiveContract`, và nối UI trang khách (VHT-36).
Khi VHT-30 (duyệt hợp đồng) xong thì mở lại, bỏ comment, bật test — **không** merge bản nháp dở vào `dev`.
