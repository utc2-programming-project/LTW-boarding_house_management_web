# VHT-94 — Nền công tắc Quản lý (Cho phép / Duyệt / Không thể)

> **Nhánh:** `VHT-94-nen-cong-tac-quan-ly` (từ `dev`).
> **Đợt:** 1D. **Phụ thuộc:** VHT-4 (ma trận chốt), VHT-6 (policy 4 vai trò).
> **Nguồn:** `docs/task/thao_tac_va_quyen.xlsx` sheet "Công tắc Quản lý"; D12, D1, D4.
> **Không làm ở đây:** hàng đợi duyệt + màn hình duyệt/từ chối (NEW-03/VHT-96), UI cấu hình (NEW-04).

## 1. Đã làm

| Lớp | File | Việc |
|---|---|---|
| Danh mục | `Web/Features/Settings/ManagerToggleCatalog.cs` | 21 công tắc: SW-01…SW-16 + SW-15a…e; tên, phân hệ, chế độ mặc định, chế độ được phép; chuẩn hóa mã |
| Enum | `Web/Features/Settings/Enums/ManagerToggleMode.cs`, `Enums/ManagerAction.cs` | 3 chế độ (0/1/2 — giá trị lưu SQL, không đổi thứ tự); 16 thao tác |
| Entity | `Web/Features/Settings/Entities/ManagerToggle.cs` | `Code` (PK), `Mode`, `UpdatedAt`, `UpdatedByUserId` |
| Dữ liệu | `Web/Data/Configurations/Settings/ManagerToggleConfiguration.cs`, migration `AddManagerToggles` | Bảng `ManagerToggles`, 3 check constraint, FK `AspNetUsers`, seed 21 dòng |
| Service | `Web/Features/Settings/Services/ManagerToggleService.cs` | Đọc/ghi SQL + kiểm quyền; upsert khi thiếu dòng seed |
| Policy | `Web/Features/Settings/Services/ManagerTogglePolicy.cs` | Mode → `Allowed` / `RequiresApproval` / `Denied`; Chủ trọ luôn `Allowed`; mã lạ fail closed |
| Điểm gọi cũ | `ManagerTogglePolicy` cài `IRoomChangePolicy` | `RoomService` sửa phòng đi qua SW-01/02/03, không đổi chữ ký P2 |
| Điểm gọi P3 | `Web/Features/Tenants/TenantService.cs` | Quản lý tạo/sửa/ngừng hồ sơ khách thuê đi qua SW-04/05/06, kiểm trước mọi truy vấn dữ liệu; thêm `TenantErrorCodes.ApprovalRequired` |
| Test P3 | `NhaTro.IntegrationTests/Features/Tenants/TenantServiceIntegrationTests.cs` | Quản lý bị chặn (403) / Chờ duyệt (không ghi) / Cho phép, và Chủ trọ không bị chặn (D4) |
| DI | `Web/Program.cs` | Đăng ký service + reader + policy; **không** bind `IOptions` cho công tắc |
| Test | `NhaTro.UnitTests/Features/Settings/ManagerTogglePolicyTests.cs` | 3 chế độ × 16 thao tác (48 ca) + danh mục + SW-15 + Chủ trọ + mã lạ |
| Test | `NhaTro.IntegrationTests/Features/Settings/ManagerToggleServiceIntegrationTests.cs` | SQL thật: seed, ghi rồi đọc lại, SW-15 chặn Duyệt, Quản lý/Admin bị chặn, policy đọc DB |

## 2. Quy tắc đã cài

- **Chỉ Chủ trọ** đọc/ghi công tắc (ma trận dòng 8). Admin không có quyền nghiệp vụ (D1). Quản lý không tự đổi công tắc của mình.
- **Mặc định Duyệt** cho 16 công tắc; riêng **SW-15 + SW-15a…e** mặc định **Cho phép** và chỉ nhận Cho phép/Không thể (D12) — chặn cả ở service và ở check constraint SQL.
- **Chủ trọ luôn thực thi ngay** (D4): policy trả `Allowed` trước khi đọc công tắc, nên công tắc "Không thể" cũng không chặn Chủ trọ.
- **Duyệt** trả `RequiresApproval` — chưa tạo yêu cầu; hàng đợi thuộc VHT-96.
- **Mã công tắc lạ** → `Denied` (fail closed), có log lỗi. Thiếu dòng trong DB → dùng chế độ mặc định của danh mục; ghi thì upsert.
- **Không lưu công tắc trong appsettings**; không cache để đổi có hiệu lực ngay.
- **Thao tác ghi của Quản lý trên hồ sơ khách thuê (P3) đi qua policy** trước khi đọc dữ liệu: bị chặn thì 403, Chờ duyệt thì trả `ApprovalRequired` và không ghi DB. Chủ trọ vẫn đi qua policy nhưng luôn `Allowed` (D4).

## 3. Lệnh kiểm chứng

```powershell
dotnet test NhaTro\NhaTro.UnitTests --filter "FullyQualifiedName~Settings"
dotnet test NhaTro\NhaTro.IntegrationTests --filter "FullyQualifiedName~Settings"
dotnet test NhaTro\NhaTro.IntegrationTests --filter "FullyQualifiedName~Tenants"
dotnet ef migrations has-pending-model-changes --project NhaTro\Web --startup-project NhaTro\Web
```

## 4. Còn để ngỏ (task sau, không tự quyết)

- Ánh xạ 5 công tắc phụ SW-15a…e vào từng mục báo cáo cụ thể (P8 — VHT-103/104); hiện chỉ lưu và trả về UI.
- Quản lý có được **đọc** công tắc để ẩn nút trên UI không: ma trận chỉ cho Chủ trọ cấu hình, nên service đang chặn cả đọc. Nếu NEW-04 cần hiển thị cho Quản lý thì mở một DTO chỉ-đọc riêng.
- Ghi nhật ký thao tác đổi công tắc vào sổ audit nghiệp vụ (VHT-104, chờ D16).
- **`SetActiveAsync` dùng SW-06 cho cả hai chiều** (ngừng và kích hoạt lại hồ sơ khách thuê). Ma trận dòng #21 ghi Quản lý ❌ với khôi phục hồ sơ từ thùng rác, nhưng thùng rác khách thuê thuộc VHT-99 nên chưa chặn riêng; cần chốt khi làm thùng rác.
- **Gắn/gỡ tài khoản đăng nhập** (`LinkAccountAsync`/`UnlinkAccountAsync`) chưa nối công tắc vì 16 mã ⚙️ không có mã nào phủ và ma trận cũng không xếp vào nhóm ngoài công tắc. Chờ nhóm chốt (quy về SW-05 hay giữ ngoài công tắc).
- **SW-07…SW-16 chưa có service** (P4–P8): khi làm từng phân hệ phải nối vào policy ngay từ đầu, không để lặp lại lỗ hổng này.
