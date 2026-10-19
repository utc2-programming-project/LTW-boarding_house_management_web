# Đề xuất DTO nhà trọ/phòng và service P2 (VHT-7)

> **Trạng thái:** DTO chốt ở đợt 1B (VHT-112, đã merge `dev`); service SQL ở đợt 1C (VHT-7, đã có code + test).
> **Phạm vi:** phân hệ P2 (nhà trọ + phòng), đợt 1B/1C. Không làm việc ngoài hai đợt này và ngoài D1–D17.
> **Phối hợp:** Phúc (trường form/UI VHT-16), Đào (dữ liệu chọn phòng cho hợp đồng VHT-11/VHT-29), Tính (quyền VHT-6, schema VHT-5).
> **Nguồn chuẩn:** `docs/task/thao_tac_va_quyen.xlsx` (dòng 11–16), `docs/design/database/so_do_quan_he_vht5_schema.sql` (bảng `Properties`, `Rooms`).

---

## 0. Chốt đối chiếu DTO với schema

DTO 1B đã khớp ERD VHT-5 nên 1C **không phải đổi kiểu**: `Id` là `int` identity, `PropertyId` là `int`,
`IsDeleted` là xóa mềm, `IsActive` là ngừng hoạt động. Chỉ bổ sung phần service cần:

| Điểm | 1B | 1C áp dụng |
|---|---|---|
| Khóa chính | `int` | Giữ `int` (khớp `IDENTITY(1,1)`) |
| Mã phòng | `Code` chuỗi | Chuẩn hóa `Trim()`; duy nhất theo `(PropertyId, Code)` **khi chưa xóa mềm** |
| Giá | `decimal StandardPrice` | Chặn âm và vượt `decimal(18,2)`; làm tròn 2 chữ số |
| Diện tích | `decimal? AreaM2` | Chặn âm/vượt `decimal(10,2)`; làm tròn 2 chữ số |
| Trạng thái | `RoomStatus` enum | Chặn giá trị ngoài enum (client có thể gửi số lạ) |
| Phân trang | `Page`/`PageSize` | `PageSize` > 100 → 20; `Skip` tính bằng `long` |
| Xóa | (chưa có ở 1B) | **Xóa mềm** `IsDeleted` + `RestoreAsync`, chặn khi còn hợp đồng hiệu lực |
| Quản lý đổi giá/trạng thái | (chưa có ở 1B) | **Điểm gọi policy công tắc** (VHT-94), chưa làm hàng đợi |

**Vị trí code 1C**

| Thành phần | File |
|---|---|
| DTO | `NhaTro/Web/Features/Properties/DTOs/`, `NhaTro/Web/Features/Rooms/DTOs/` |
| Service | `Features/Properties/PropertyService.cs`, `Features/Rooms/RoomService.cs` |
| Ngữ cảnh/quyền | `Features/Properties/PropertyCaller.cs` |
| Mã lỗi | `PropertyErrorCodes.cs`, `RoomErrorCodes.cs` |
| Kết quả | `PropertyResult.cs`, `RoomResult.cs` |
| Điểm gọi VHT-94 / VHT-98 | `Rooms/IRoomChangePolicy.cs`, `Rooms/IRoomUsageChecker.cs` |
| DI | `Program.cs` |

---

## 1. Quyền — đối chiếu ma trận

| Thao tác (dòng ma trận) | Chủ trọ | Quản lý | Khách thuê | Admin | Cách service chặn |
|---|---|---|---|---|---|
| Xem nhà trọ/phòng (#11) | Có | Có | ❌ (chỉ phòng đang thuê qua hợp đồng) | ❌ | `EnsureCanRead` |
| Sửa thông tin phòng (#12) | Có | ⚙️ SW-01 | ❌ | ❌ | `UpdateAsync` + policy |
| Đổi giá phòng (#13) | Có | ⚙️ SW-02 | ❌ | ❌ | `UpdateAsync` + policy |
| Đổi trạng thái phòng (#14) | Có | ⚙️ SW-03 | ❌ | ❌ | `UpdateAsync` + policy |
| Xóa phòng → thùng rác (#15) | Có | ❌ | ❌ | ❌ | `EnsureCanManage` |
| Khôi phục phòng (#16) | Có | ❌ | ❌ | ❌ | `EnsureCanManage` |

- **Admin** bị chặn mọi thao tác P2 (D1): `PropertyCaller.IsManager` sai → ném `UnauthorizedAccessException`.
- **Quản lý** chưa có bảng phân công tòa nhà nên hiện đọc **toàn hệ thống**; khi VHT-24 chốt phạm vi, chỉ sửa `ScopeRooms`/`ScopeQuery` (một chỗ).
- **Chủ trọ** luôn bị lọc theo `Property.OwnerId` trong query, không chỉ ẩn nút.

### Điểm gọi policy công tắc (VHT-94)

`UpdateAsync` khi người gọi **không phải Chủ trọ** sẽ tính các loại thay đổi rồi hỏi `IRoomChangePolicy`:

| Thay đổi | `RoomChangeKind` | Công tắc |
|---|---|---|
| `Code`, `PropertyId`, `Floor`, `AreaM2`, `IsActive` | `EditInfo` | SW-01 |
| `StandardPrice` | `ChangePrice` | SW-02 |
| `Status` | `ChangeStatus` | SW-03 |

Kết quả: `Allowed` → ghi; `Denied` → 403; `RequiresApproval` → `ApprovalRequired` (chưa ghi).
`RoomChangePolicyStub` hiện trả đúng **mặc định Duyệt** của ma trận. VHT-94 thay bằng policy thật, VHT-96 nối hàng đợi duyệt.

### Điểm gọi kiểm hợp đồng (VHT-98)

`DeleteAsync` gọi `IRoomUsageChecker.HasActiveContractAsync`. `NoActiveContractRoomUsageChecker` hiện trả `false`
vì bảng `Contracts` thuộc VHT-29/VHT-30; VHT-98 thay bằng truy vấn thật. Nhờ vậy quy tắc "không xóa phòng còn hợp đồng hiệu lực" **đã có chỗ cắm**, chưa bị bỏ quên.

---

## 2. DTO (đã chốt 1B, dùng nguyên ở 1C)

### 2.1 Nhà trọ

| DTO | Trường |
|---|---|
| `PropertyFilter` | `Keyword?`, `IsActive?`, `Page`, `PageSize` |
| `PropertyListItem` | `Id`, `Name`, `Address?`, `IsActive`, `RoomCount` |
| `PropertyDetail` | `Id`, `Name`, `Address?`, `OwnerId`, `IsActive`, `CreatedAt`, `UpdatedAt?` |
| `PropertySave` | `Name` (bắt buộc, ≤200), `Address?` (≤500), `IsActive` |

`OwnerId` **không nhận từ client** — service lấy từ người gọi. `RoomCount` đếm phòng chưa xóa mềm.

### 2.2 Phòng

| DTO | Trường |
|---|---|
| `RoomFilter` | `PropertyId?`, `Status?`, `Keyword?`, `OnlyAvailable?`, `IsActive?`, `MinPrice?`, `MaxPrice?`, `Page`, `PageSize` |
| `RoomListItem` | `Id`, `PropertyId`, `PropertyName`, `Code`, `Floor?`, `AreaM2?`, `StandardPrice`, `Status`, `IsActive` |
| `RoomDetail` | như list + `CreatedAt`, `UpdatedAt?` |
| `RoomSave` | `PropertyId`, `Code` (bắt buộc, ≤50), `Floor?` (0–1000), `AreaM2?` (0–100 000), `StandardPrice` (≥0), `Status`, `IsActive` |
| `RoomOption` | `Id`, `PropertyId`, `PropertyName`, `Code`, `StandardPrice`, `Status` |
| `RoomOptionRequest` | `Keyword?`, `PropertyId?`, `IncludeUnavailable`, `ExcludeRoomIds?`, `Limit` (mặc định 20, trần 50) |

### 2.3 Kết quả / lỗi

`PropertyResult`, `RoomResult`: `Succeeded`, `Property`/`Room?`, `ErrorCode?`, `ErrorMessage?` — cùng khuôn `TenantResult`.

| Mã lỗi | HTTP gợi ý | Khi nào |
|---|---|---|
| `InvalidRequest` | 400 | Thiếu mã/tên, giá âm, diện tích/tầng sai, trạng thái lạ, vượt độ dài |
| `Forbidden` | 403 | Sai vai trò (Admin, Khách thuê) |
| `PropertyNotFound` / `RoomNotFound` | 404 | Không tồn tại **hoặc ngoài phạm vi** (không lộ sự tồn tại) |
| `DuplicateRoomCode` | 409 | Mã phòng trùng trong cùng nhà (kể cả khi khôi phục) |
| `RoomInUse` | 409 | Phòng còn hợp đồng hiệu lực |
| `ApprovalRequired` | 202/409 | Quản lý thao tác khi công tắc = Duyệt |

---

## 3. Service (đợt 1C)

### 3.1 `IPropertyService`

| Hàm | Mô tả |
|---|---|
| `ListAsync(filter, caller)` | `PagedResult<PropertyListItem>` trong phạm vi, có `RoomCount` |
| `GetAsync(id, caller)` | `PropertyResult` |
| `CreateAsync(request, caller)` | Chỉ Chủ trọ; `OwnerId` = người gọi |
| `UpdateAsync(id, request, caller)` | Chỉ Chủ trọ; không đổi chủ sở hữu |

### 3.2 `IRoomService`

| Hàm | Mô tả |
|---|---|
| `ListAsync(filter, caller)` | `PagedResult<RoomListItem>`, lọc + phân trang |
| `GetAsync(id, caller)` | `RoomResult` kèm `PropertyName` |
| `CreateAsync(request, caller)` | Chỉ Chủ trọ; kiểm nhà trong phạm vi + mã chưa trùng |
| `UpdateAsync(id, request, caller)` | Kiểm mã, kiểm phạm vi nhà đích, **đi qua policy công tắc** cho Quản lý |
| `DeleteAsync(id, caller)` | Chỉ Chủ trọ; xóa mềm; chặn `RoomInUse` |
| `RestoreAsync(id, caller)` | Chỉ Chủ trọ; chặn khi mã đã bị dùng lại |
| `SearchForPickerAsync(request, caller)` | Dữ liệu chọn phòng cho Đào |

**Chống đua:** trùng mã phòng do hai request đồng thời được bắt bằng `DbUpdateException` (SQL 2601/2627) và trả `DuplicateRoomCode` thay vì 500; unique filtered index `IX_Rooms_PropertyId_Code ... WHERE IsDeleted = 0` là lớp chặn cuối.

---

## 4. Mẫu input/output (gửi Phúc)

### 4.1 Tạo nhà trọ

```json
{ "name": "Trọ An Bình", "address": "12 Nguyễn Trãi, Quận 5", "isActive": true }
```

```json
{
  "succeeded": true,
  "property": {
    "id": 1,
    "name": "Trọ An Bình",
    "address": "12 Nguyễn Trãi, Quận 5",
    "ownerId": "chu-tro-1",
    "isActive": true,
    "createdAt": "2026-10-19T03:20:00Z",
    "updatedAt": null
  },
  "errorCode": null,
  "errorMessage": null
}
```

### 4.2 Danh sách nhà trọ

`GET /api/properties?keyword=tro&page=1&pageSize=20`

```json
{
  "items": [
    { "id": 1, "name": "Trọ An Bình", "address": "12 Nguyễn Trãi, Quận 5", "isActive": true, "roomCount": 3 }
  ],
  "page": 1, "pageSize": 20, "totalCount": 1
}
```

### 4.3 Tạo phòng

```json
{
  "propertyId": 1, "code": "P101", "floor": 1,
  "areaM2": 20.5, "standardPrice": 2500000, "status": 0, "isActive": true
}
```

```json
{
  "succeeded": true,
  "room": {
    "id": 7, "propertyId": 1, "propertyName": "Trọ An Bình", "code": "P101",
    "floor": 1, "areaM2": 20.5, "standardPrice": 2500000, "status": 0,
    "isActive": true, "createdAt": "2026-10-19T03:22:00Z", "updatedAt": null
  },
  "errorCode": null, "errorMessage": null
}
```

### 4.4 Trùng mã phòng trong cùng nhà — 409

```json
{
  "succeeded": false, "room": null,
  "errorCode": "DuplicateRoomCode",
  "errorMessage": "Mã phòng đã tồn tại trong nhà trọ này"
}
```

### 4.5 Giá âm — 400

```json
{ "succeeded": false, "room": null, "errorCode": "InvalidRequest", "errorMessage": "Giá phòng không hợp lệ" }
```

### 4.6 Quản lý đổi giá khi công tắc = Duyệt

```json
{
  "succeeded": false, "room": null,
  "errorCode": "ApprovalRequired",
  "errorMessage": "Thay đổi đang chờ Chủ trọ duyệt"
}
```

### 4.7 Dữ liệu chọn phòng (Đào — `GET /api/rooms/picker?limit=20`)

```json
[
  { "id": 7, "propertyId": 1, "propertyName": "Trọ An Bình", "code": "P101", "standardPrice": 2500000, "status": 0 }
]
```

Mặc định chỉ trả `status = Available` và `isActive = true` trong phạm vi người gọi; `includeUnavailable=true` để lấy cả phòng đang thuê/bảo trì.

---

## 5. Tự kiểm tra (kết quả thật)

| Nội dung | Cách kiểm | Kết quả |
|---|---|---|
| Lưu rồi tải lại đúng | `Luu_nha_tro_roi_tai_lai_dung`, `Luu_phong_roi_tai_lai_dung_moi_truong`, `Sua_gia_va_trang_thai_roi_doc_lai_dung` | Pass |
| Chặn mã trùng theo nhà | `Ma_phong_trung_trong_cung_nha_bi_chan`, `Cung_ma_phong_o_nha_khac_duoc_phep`, `Sua_ma_phong_trung_phong_khac_bi_chan` | Pass |
| Chặn giá sai | `Gia_am_bi_chan_va_khong_luu`, `Gia_qua_lon_bi_chan`, `Gia_nhieu_hon_2_so_le_duoc_lam_tron` | Pass |
| Chặn truy cập ngoài phạm vi | `ChuTro_khac_khong_doc_duoc_nha_va_phong`, `Khong_tao_duoc_phong_trong_nha_nguoi_khac`, `Admin_va_khach_thue_bi_chan_khoi_nghiep_vu_p2` | Pass |
| Công tắc Quản lý (điểm gọi) | `Quan_ly_tao_nha_bi_chan_va_sua_phong_qua_cong_tac`, `Quan_ly_sua_phong_khi_cong_tac_cho_phep` | Pass |
| Xóa mềm + khôi phục | `Xoa_mem_an_khoi_query_va_khoi_phuc_duoc`, `Xoa_phong_con_hop_dong_hieu_luc_bi_chan` | Pass |
| Dữ liệu chọn phòng | `Du_lieu_chon_phong_chi_tra_phong_trong_trong_pham_vi`, `Phan_trang_va_loc_gia_tra_dung` | Pass |

Chạy: `dotnet test NhaTro/NhaTro.slnx` → Unit 50 pass, Integration 19 pass (SQL Server LocalDB thật), E2E 1 pass.

---

## 6. Còn cần nhóm chốt / follow-up

- **Tạo nhà trọ/phòng cho Quản lý:** ma trận chỉ có sửa/xóa, không có dòng "tạo", nên 1C **chặn** Quản lý tạo. Nếu nhóm muốn Quản lý tạo phòng thì phải thêm dòng ma trận + công tắc.
- **Phạm vi Quản lý:** hiện đọc toàn hệ thống (chưa có bảng phân công). VHT-24 chỉ sửa `ScopeRooms`/`ScopeQuery`.
- **Hàng đợi duyệt:** `RequiresApproval` hiện trả lỗi, chưa tạo yêu cầu — thuộc VHT-96.
- **Xóa phòng còn hợp đồng:** `IRoomUsageChecker` đang trả `false`; VHT-98 nối bảng `Contracts`.
- **Xóa nhà trọ:** chưa có trong task; nếu thêm thì phải chặn khi còn phòng (FK `Restrict` đã chặn xóa thật, xóa mềm cần quy tắc riêng).
- **`RowVersion`:** bảng `Properties`/`Rooms` chưa có cột rowversion, nên chống ghi đè đồng thời hiện chỉ dựa trên unique index; nếu cần như P3 thì phải thêm migration (Tính review).
- **Endpoint HTTP:** 1C mới có service; controller/endpoint do VHT-16 nối. Khi dựng endpoint, cần bật `UnmappedMemberHandling = Disallow` (như VHT-23) để chặn client gửi `OwnerId`/`IsDeleted` ngoài DTO.
