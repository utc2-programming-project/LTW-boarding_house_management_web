# VHT-25 — Gắn tài khoản với hồ sơ khách (đợt 2B)

- **Nhánh:** `VHT-25-gắn-tai-khoản-với-hồ-sơ-va-lien-kết-thue` (tạo từ `dev`)
- **Phạm vi:** Sprint 2B, subsystem P3, **chỉ phần gắn user ↔ hồ sơ khách**.
- **Không làm (đợt 2C):** truy vấn phòng/hợp đồng đang thuê, lọc `HasActiveContract`, cột `RoomName`.
  Phần này cần VHT-30 (duyệt hợp đồng) nên được **tách sang nhánh `VHT-120-ghep-lien-ket-thue-that-p3`**,
  không đưa vào 2B để tránh nhảy bước.
- **Nguồn quyết định:** `docs/task/de_xuat_hop_dong_khach_thue.md` mục 1/4, `docs/task/timeline_song_song.md`
  (2B = "gắn user/hồ sơ, chuẩn bị liên kết thuê", "chưa ghép liên kết thuê đến khi VHT-30 chạy được").

## 1. Kết quả bàn giao 2B

| Hạng mục | Trạng thái | Vị trí |
|---|---|---|
| Gắn tài khoản vào hồ sơ, kiểm vai trò Khách thuê | Xong (có từ VHT-23, giữ nguyên) | `TenantService.LinkAccountAsync` |
| Gỡ liên kết, giữ nguyên hồ sơ | Xong | `TenantService.UnlinkAccountAsync` |
| Danh sách tài khoản chưa gắn để UI chọn | Xong | `TenantService.SearchUnlinkedAccountsAsync` |
| DTO cho UI (trạng thái đã/chưa liên kết) | Xong | `TenantDetailDto.HasAccount`, `TenantAccountLinkDialog.razor` |
| Chặn trùng liên kết ở service + DB | Xong | kiểm trước trong service; unique index có lọc `Tenant.UserId` trong `TenantConfiguration` |
| Chốt quy tắc gắn/gỡ và cách thay liên kết | Xong (tài liệu này + `de_xuat_hop_dong_khach_thue.md` §1) | — |
| Test hồi quy 2B | Xong | `TenantServiceIntegrationTests.Khong_lien_ket_nham_tai_khoan`, `Summary_va_tai_khoan_chua_lien_ket_theo_dung_quyen`, `P1P3RoleAccessTests` |

## 2. Quy tắc đã chốt

1. **Ai gắn/gỡ:** chỉ **Chủ trọ**. Quản lý và Admin bị chặn (`UnauthorizedAccessException` → 403).
2. **Một tài khoản tối đa một hồ sơ**, một hồ sơ tối đa một tài khoản.
   - Tài khoản đã gắn hồ sơ khác → `AccountAlreadyLinked`.
   - Hồ sơ đã có tài khoản, gọi gắn lại → `AccountAlreadyLinked` (không ghi đè).
   - Tài khoản không tồn tại / sai vai trò → `InvalidRequest` với thông báo tương ứng.
3. **Cách thay liên kết:** gỡ liên kết cũ → gắn tài khoản mới. Không có thao tác "replace" ghi đè.
4. **Đồng thời:** unique index có lọc `[UserId] IS NOT NULL AND [IsDeleted] = 0` là lớp chặn cuối;
   service bắt `DbUpdateException` unique và trả `AccountAlreadyLinked`.
5. **Không lộ dữ liệu tài khoản:** DTO chỉ trả `HasAccount` (bool?), không trả `UserId` thô;
   Quản lý nhận `null` (D9).

## 3. Kiểm chứng

```powershell
dotnet build NhaTro/NhaTro.slnx
dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter "FullyQualifiedName~Tenants"
```

Kỳ vọng: build 0 error; unit test pass toàn bộ; integration test nhóm Tenants pass, gồm các case
gắn/gỡ/trùng quyền và "khách chỉ đọc hồ sơ của mình".

## 4. Chuyển giao sang 2C (VHT-120)

Nhánh `VHT-120-ghep-lien-ket-thue-that-p3` giữ bản nháp đã tách: DTO `TenantRental*`,
`GetRentalOverviewAsync`/`GetOwnRentalOverviewAsync`, query `ContractMember → Contract → Room → Property`,
lọc `HasActiveContract` + `RoomName`, và integration test tương ứng. Khi VHT-30 xong thì mở lại,
bỏ comment, bật test — **không** merge bản nháp dở vào `dev`.
