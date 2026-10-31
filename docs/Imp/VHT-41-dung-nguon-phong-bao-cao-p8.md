# VHT-41 — Dựng nguồn phòng báo cáo P8

> **Nhánh:** `VHT-41-dung-nguon-phong-bao-cao-p-8`, tạo từ `origin/dev` tại `143e8a7`.
> **Đợt/phân hệ:** Đợt 2B, P8 — Báo cáo và thống kê.
> **Mục tiêu:** DTO/count/filter phòng theo phạm vi P2, bàn giao Phú ghép dashboard; nguồn khách do Phát làm riêng (VHT-46).
> **Không làm:** UI dashboard (VHT-40), nguồn khách (VHT-46), hợp đồng/chi phí (VHT-52), doanh thu/công nợ (VHT-57), xuất Excel (VHT-103), migration/entity mới.
> **Nguồn:** `docs/task/cong_viec_theo_tuan.md` · `docs/task/timeline_song_song.md` · `docs/task/thao_tac_va_quyen.xlsx` (mục P8, SW-15a) · VHT-40, VHT-94.

## 1. Chốt trước khi code

| Nội dung | Quyết định đề xuất | Cần xác nhận |
| --- | --- | --- |
| Nguồn đếm | Chỉ bảng `Rooms`; **không join** `Contracts`. Trạng thái phòng là nguồn chuẩn cho "đang thuê" | Phúc (đối chiếu phòng) |
| Tổng | Phòng chưa xóa mềm **và** `IsActive` (đang niêm yết). Mặc định `IsActive = true` trong filter | Phú, Phúc |
| Trống | `IsActive` và `Status = Available` | Phú, Phúc |
| Đang thuê | `IsActive` và `Status = Rented` | Phú, Phúc |
| Bảo trì | `IsActive` và `Status = Maintenance` | Phú, Phúc |
| Quan hệ 4 số | Trống + Đang thuê + Bảo trì = Tổng (vì report luôn lọc `IsActive` xác định) | Phú |
| Phòng tạm ngưng | Không vào KPI mặc định; chỉ thấy khi lọc `IsActive = false` | Phú |
| Kỳ báo cáo | Báo cáo phòng là ảnh chụp hiện tại, **không** lọc theo kỳ | Phú |
| Công tắc | Quản lý phải qua **SW-15** (xem báo cáo) **và SW-15a** (mục Phòng); Chủ trọ luôn xem (D4) | Tính/Phú |
| Nhà trọ trống | Lọc đúng nhà chưa có phòng → KPI toàn 0, danh sách rỗng, không lỗi, không ẩn mục | Phúc |
| Dòng danh sách | Dùng lại `RoomListItem` + `PagedResult<RoomListItem>` của P2 để UI Phú hiển thị giống danh sách phòng | Phú |

> Lưu ý lệch chuẩn: `RoomSummary` của P2 đang cộng `Total` gồm cả phòng `IsActive = false`, còn 3 số trạng thái chỉ đếm phòng active. P8 **không** copy cách này; P8 lấy cả 4 số trên cùng một query đã lọc. Nếu nhóm muốn P2 đổi theo, tách task riêng.

> **Đã chốt 30/10/2026:** giữ đúng các quyết định trên — Tổng chỉ tính phòng đang niêm yết; Quản lý bị chặn khi **một trong hai** công tắc SW-15/SW-15a tắt; chưa làm bảng `ByProperty` (để lượt ghép VHT-40).

## 2. Thiết kế

### File

| File | Nội dung |
| --- | --- |
| `NhaTro/Web/Features/Reports/DTOs/RoomReportFilter.cs` | `PropertyId`, `Status`, `IsActive` (mặc định `true`), `Keyword`, `Page`, `PageSize`; `Normalize()` giới hạn trang 1–100, trim keyword |
| `NhaTro/Web/Features/Reports/DTOs/RoomReportKpis.cs` | `Total`, `Available`, `Rented`, `Maintenance` |
| `NhaTro/Web/Features/Reports/DTOs/RoomReportResult.cs` | `Kpis` + `PagedResult<RoomListItem> Rooms` |
| `NhaTro/Web/Features/Reports/IRoomReportService.cs` | Một hàm `GetAsync(filter, caller, ct)` |
| `NhaTro/Web/Features/Reports/RoomReportService.cs` | Quyền → công tắc → phạm vi → một predicate dùng chung cho count và list |
| `NhaTro/Web/Program.cs` | Đăng ký scoped service cạnh `IRoomService` |
| `NhaTro.UnitTests/Features/Reports/RoomReportFilterTests.cs` | Chuẩn hóa filter, biên trang, từ khóa rỗng |
| `NhaTro.IntegrationTests/Features/Reports/RoomReportServiceIntegrationTests.cs` | SQL thật: phạm vi, KPI khớp list, nhà trống, công tắc, phòng xóa mềm |

### Luồng xử lý

1. Kiểm vai trò: chỉ Chủ trọ/Quản lý; Admin và Khách thuê bị từ chối (D1, D15).
2. Nếu là Quản lý: hỏi policy lần lượt `SW-15` rồi `SW-15a`; bất kỳ mã nào `Denied` → từ chối toàn bộ. Chủ trọ bỏ qua bước này (D4).
3. Dựng **một** query phòng: lọc xóa mềm (query filter sẵn có) → phạm vi `RoomScope` → điều kiện filter.
4. Đếm KPI trên chính query đó, gom nhóm theo trạng thái (một lượt SQL).
5. Lấy trang danh sách trên cùng query, sắp theo mã phòng rồi Id; điền tên nhà như P2.
6. Trả `Kpis` + trang danh sách.

Không đọc bảng hợp đồng ở bất kỳ bước nào. Nếu sau này cần kiểm chéo trạng thái, chỉ dùng `EXISTS`, không join.

### Hợp đồng dữ liệu

| DTO | Trường | Ghi chú |
| --- | --- | --- |
| `RoomReportFilter` | `PropertyId`, `Status`, `IsActive`, `Keyword`, `Page`, `PageSize` | `IsActive` kiểu `bool`, mặc định `true`; không có chế độ "tất cả" để 4 số luôn cộng khớp |
| `RoomReportKpis` | `Total`, `Available`, `Rented`, `Maintenance` | Cùng định nghĩa ở mục 1 |
| `RoomReportResult` | `Kpis`, `Rooms` | `Rooms` là `PagedResult<RoomListItem>` dùng lại P2 |

### Ghi chú phạm vi

- `RoomScope` hiện cho Quản lý xem **toàn hệ thống** vì chưa có bảng phân công tòa nhà (VHT-24). P8 kế thừa đúng hành vi này; khi VHT-24 xong, sửa một chỗ là cả P2 lẫn P8 đổi theo.
- Nếu VHT-40 cần bảng "số phòng theo từng nhà" (kể cả nhà 0 phòng), bổ sung `ByProperty` ở lượt ghép; chưa đưa vào lõi VHT-41 để tránh chốt DTO khi Phú chưa có khung.

## 3. Quyền và công tắc

| Vai trò | Hành vi |
| --- | --- |
| Chủ trọ | Xem phòng thuộc nhà mình; không phụ thuộc công tắc (D4) |
| Quản lý | Xem theo phạm vi P2; chỉ khi **SW-15** và **SW-15a** đều Cho phép; Không thể → từ chối, không trả số 0 |
| Admin | Từ chối (D1) |
| Khách thuê | Từ chối; trang tổng quan cá nhân là việc khác (D15) |

Cách từ chối dùng chung với P2 (`UnauthorizedAccessException`) để endpoint map 403, không trả DTO rỗng gây hiểu nhầm "không có phòng".

## 4. Kiểm thử và đối chiếu

### Case bắt buộc

| # | Case | Dữ liệu | Kỳ vọng |
| --- | --- | --- | --- |
| 1 | KPI khớp danh sách | Nhà A: 3 phòng (1 trống, 1 thuê, 1 bảo trì) | `Total = 3`, `Available = 1`, `Rented = 1`, `Maintenance = 1`; `Rooms.TotalCount = 3` |
| 2 | Lọc theo trạng thái | Cùng dữ liệu, `Status = Rented` | `Total = Rented = 1`; danh sách 1 dòng |
| 3 | Nhà trọ trống | Nhà B không có phòng | `Total = 0`, 4 số = 0, danh sách rỗng, HTTP 200 |
| 4 | Phạm vi Chủ trọ | Chủ trọ A gọi báo cáo | Chỉ thấy phòng nhà A; không thấy nhà B |
| 5 | Phòng xóa mềm | Xóa mềm 1 phòng | Không vào KPI, không vào danh sách |
| 6 | Phòng tạm ngưng | `IsActive = false` | Mặc định không vào KPI; lọc `IsActive = false` mới thấy |
| 7 | Không đếm trùng hợp đồng | 1 phòng có 2 hợp đồng | KPI và danh sách vẫn đếm 1 |
| 8 | SW-15a Không thể | Quản lý, tắt mục Phòng | Bị từ chối; không trả DTO |
| 9 | SW-15 Không thể | Quản lý, tắt xem báo cáo | Bị từ chối |
| 10 | Vai trò cấm | Admin, Khách thuê | Bị từ chối |
| 11 | Chuẩn hóa filter | Trang 0, PageSize 500, keyword toàn khoảng trắng | Về trang 1, 100, keyword null |

### Đối chiếu với P2 (bàn giao Phúc)

| Bộ lọc | P2 `ListAsync` TotalCount | P8 `Kpis.Total` | P8 `Rooms.TotalCount` |
| --- | --- | --- | --- |
| Nhà A, mặc định (`IsActive = true`) | 3 | 3 | 3 |
| Nhà A, `Status = Available` | 1 | 1 | 1 |
| Nhà C (0 phòng), mặc định | 0 | 0 | 0 |
| Quản lý, mặc định (nhà A + B) | 4 | 4 | 4 |

Dữ liệu seed của integration test: Nhà A có 3 phòng active (1 trống, 1 thuê, 1 bảo trì) + 1 phòng tạm ngưng + 1 phòng xóa mềm; Nhà B có 1 phòng trống; Nhà C không có phòng. Kết quả chạy 30/10/2026 khớp bảng trên.

### Kết quả chạy

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build NhaTro/NhaTro.slnx` | 0 lỗi; 1 warning có sẵn ở `IncidentDemoSessionTests` |
| `dotnet test NhaTro/NhaTro.UnitTests --filter FullyQualifiedName~Reports` | **6/6 đạt** |
| `dotnet test NhaTro/NhaTro.IntegrationTests --filter FullyQualifiedName~Reports` | **11/11 đạt** (SQL LocalDB thật) |
| `dotnet test NhaTro/NhaTro.IntegrationTests` (toàn bộ) | **102/102 đạt**, không hồi quy |
| `dotnet test NhaTro/NhaTro.UnitTests` (toàn bộ) | 494/497; 3 test P6 hóa đơn đỏ do lỗi culture parse decimal có sẵn, không liên quan VHT-41 |

### Lệnh kiểm

```text
dotnet build NhaTro/NhaTro.slnx
dotnet test NhaTro/NhaTro.UnitTests
dotnet test NhaTro/NhaTro.IntegrationTests
```

## 5. Thứ tự thực hiện

1. **Chốt shape với Phú** (15 phút): tên DTO, có cần `ByProperty` ngay không, `IsActive` mặc định. Chặn rủi ro VHT-40 chưa có trên `dev`.
2. **Tạo DTO + interface** trong `Features/Reports`.
3. **Viết service**: quyền → công tắc → predicate chung → KPI → trang danh sách.
4. **Đăng ký DI** trong `Program.cs`.
5. **Unit test** filter; **integration test** 11 case trên.
6. **Đối chiếu P2**, điền bảng số vào tài liệu này.
7. **Báo cáo bàn giao** cho Phú/Phúc; chỉ push và mở PR về `dev` khi được đồng ý (AGENTS.md).

## 6. Bàn giao

- DTO/filter/count/list phòng P8, tái dùng `RoomListItem`.
- Bảng đối chiếu P2 theo từng bộ lọc, gồm nhà trọ 0 phòng.
- Ghi chú cho Phú: gọi service, không tự đếm ở UI; ẩn/không gọi mục khi công tắc tắt.
- Ghi chú cho Phúc: đối chiếu danh sách P2 cùng filter; xác nhận định nghĩa 4 số.

## 7. Rủi ro

| Rủi ro | Ảnh hưởng | Cách xử lý |
| --- | --- | --- |
| VHT-40 chưa có, DTO có thể phải đổi | Phú sửa UI hai lần | Chốt shape trước khi code; giữ DTO nhỏ, tái dùng DTO P2 |
| Quản lý đang xem toàn hệ thống (VHT-24 chưa xong) | Báo cáo Quản lý rộng hơn mong đợi | Ghi rõ là hành vi tạm; không tự thêm bảng phân công |
| `Rooms.Status` lệch hợp đồng thật | "Đang thuê" sai | Không join; ghi nhận và kiểm dữ liệu ở case 7; nếu lệch nhiều, mở task riêng |
| SW-15 vs SW-15a hiểu khác nhau | Quản lý bị chặn nhầm hoặc lọt | Kiểm cả hai; ghi rõ trong tài liệu bàn giao |
| Đếm và list chạy hai query | Chênh số khi dữ liệu đổi giữa hai lượt | Chấp nhận (báo cáo không giao dịch); cùng predicate, cùng context; test so khớp |
| Dữ liệu lớn | Chậm | Count và phân trang ở SQL, không tải hết vào bộ nhớ |
