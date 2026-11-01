# VHT-120 — Ghép liên kết thuê thật P3 (đợt 2C)

- **Nhánh:** `VHT-120-ghep-lien-ket-thue-that-p3` (tạo từ `dev`, tiếp nối VHT-25 đợt 2B)
- **Phạm vi:** Sprint 2C, subsystem P3: đưa hợp đồng đang hiệu lực thật vào danh sách + picker khách,
  lọc danh sách theo tình trạng thuê, và hiển thị phòng đang thuê ở UI quản lý khách.
- **Nguồn quyết định:** `docs/task/cong_viec_theo_tuan.md` (VHT-120), `docs/task/timeline_song_song.md` (2C),
  `docs/Imp/VHT-25-gan-tai-khoan-voi-ho-so.md` §4.

## 1. Kết quả bàn giao 2C

| Hạng mục | Trạng thái | Vị trí |
|---|---|---|
| `HasActiveContract` thật trong danh sách khách | Xong | `TenantService.ListAsync` + `LoadActiveContractsAsync` |
| `RoomName` thật trong danh sách khách | Xong | `TenantMapping.ToListItem`, cột "Phòng" của `TenantList.razor` |
| `HasActiveContract` thật trong picker khách | Xong | `TenantService.SearchForPickerAsync` |
| Áp dụng `TenantFilterRequest.HasActiveContract` | Xong | lọc trong SQL trước phân trang/tính tổng |
| Lọc tình trạng thuê trên UI danh sách | Xong | `TenantList.razor` + resource VI/EN |
| Test hồi quy | Xong | `TenantRentalLinkIntegrationTests` (2 test mới) |

## 2. Quy tắc đã chốt

- **Nguồn dữ liệu:** `Tenant -> ContractMember -> Contract -> Room`; **không** suy từ `Room.Status`.
- **Đang thuê:** `Status == Approved && StartDate <= hôm nay <= EndDate`, dùng chung định nghĩa với
  `TenantRentalOverviewDto.CurrentRental` của đợt 2B.
- **Khách chưa thuê:** danh sách/picker trả `HasActiveContract = false`, `RoomName = null`.
  Không phải lỗi và không làm hỏng phân trang.
- **Khách nhiều hợp đồng:**
  - Chỉ hợp đồng đang hiệu lực mới được tính; hợp đồng đã hết hạn/chưa tới ngày/chờ duyệt không tính.
  - Nhiều hợp đồng cùng hiệu lực: chọn xác định theo `StartDate` mới nhất, sau đó `ContractId`,
    để phòng hiển thị không đổi theo thứ tự query.
- **Hiệu năng:** mỗi trang danh sách/picker nạp hợp đồng hiện tại **một lần** theo lô `TenantId`
  (tránh N+1). Bộ lọc `HasActiveContract` được dịch thành truy vấn con trong SQL trước `COUNT`/`Skip`/`Take`.
- **Quyền (giữ nguyên D9):**
  - Chủ trọ và Quản lý đều thấy cột phòng + trạng thái thuê.
  - Quản lý vẫn không thấy trạng thái tài khoản (`HasAccount = null`).
  - Khách thuê/Admin không gọi được danh sách/picker quản lý.
  - `MyLeasedRoomView` tiếp tục đọc `GetOwnRentalOverviewAsync` → `CurrentRental`, chỉ trả hồ sơ
    gắn đúng tài khoản đang đăng nhập.

## 3. Thay đổi chính

- `TenantService`: thêm `ActiveTenantIds` (lọc SQL), `ActiveContractRows` (nạp theo lô, kèm phòng),
  `LoadActiveContractsAsync` (chọn hợp đồng hiện tại xác định); ghép kết quả vào `ListAsync` và
  `SearchForPickerAsync`.
- `TenantMapping.ToListItem`: nhận thêm `roomName`.
- `TenantListItemDto` / `TenantPickerItemDto` / `TenantFilterRequest`: cập nhật tài liệu từ "placeholder/chờ hợp đồng"
  sang mô tả liên kết thật.
- `TenantList.razor`: thêm bộ lọc "Tình trạng thuê" (tất cả / đang thuê / chưa thuê), áp dụng cho
  tải danh sách và xuất CSV; reset/clear filter bao gồm bộ lọc mới.
- `Shared.vi-VN.resx` / `Shared.en-US.resx`: thêm `TenantRentalFilter`, `TenantRentalAll`,
  `TenantRentalActive`, `TenantRentalNone`.

## 4. Kiểm chứng

```powershell
dotnet build NhaTro/NhaTro.slnx
dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj --filter "FullyQualifiedName~Tenants"
```

Kết quả đã chạy trên nhánh này: build 0 error; unit test 568/568 pass; integration test nhóm Tenants
23/23 pass. Hai test mới của VHT-120:

- `Danh_sach_va_picker_dung_hop_dong_dang_hieu_luc_va_loc_theo_tinh_trang_thue`
  — khách đang thuê, khách chỉ có hợp đồng lịch sử (phòng đang `Rented` nhưng không tính),
  khách chưa thuê; lọc `HasActiveContract` đúng tổng số; Quản lý thấy phòng nhưng không thấy tài khoản.
- `Nhieu_hop_dong_cung_hieu_luc_chon_hop_dong_moi_nhat`
  — hai hợp đồng cùng hiệu lực chọn phòng của hợp đồng `StartDate` mới nhất.

## 5. Bàn giao

- Service `TenantService.ListAsync` / `SearchForPickerAsync` trả dữ liệu thuê thật; UI danh sách khách
  dùng được ngay, không cần tính lại ở client.
- Trang khách `MyLeasedRoomView` đã nối từ đợt 2B, không đổi hành vi: chưa thuê → trạng thái rỗng rõ ràng;
  nhiều hợp đồng lịch sử → chỉ hiện hợp đồng đang hiệu lực.
- Phần còn lại của VHT-36 (nếu có) chỉ cần dùng `RoomName`/`HasActiveContract` sẵn có, không thêm truy vấn riêng.
