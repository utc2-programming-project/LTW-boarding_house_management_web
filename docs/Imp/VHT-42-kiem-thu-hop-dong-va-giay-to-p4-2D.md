# VHT-42 — Kiểm thử hợp đồng/phòng và giấy tờ P4 (đợt 2D)

> **Phạm vi:** đợt **2D** — kiểm luồng đã ghép cho hợp đồng/phòng (P4) và giấy tờ/hồ sơ (P3–P4):
> tạo/duyệt trùng, duyệt đồng thời, rollback, snapshot giá, truy cập hồ sơ/tệp sai quyền, và
> **Admin bị chặn trên P2–P4** theo bổ sung D1–D17 (12/10/2026).
> **Đã sửa sau kiểm thử:** BUG-2D-01 → BUG-2D-05 (dịch vụ hợp đồng + phạm vi giấy tờ Quản lý +
> ghép UI hợp đồng P4 tương đương VHT-31/`b7734e5` và VHT-124).
> **Đợt 2E:** **chưa thực hiện** — cần commit bản sửa và retest trên commit ghép tuần 2 (xem mục 8).
> **Không làm:** không tạo migration, không mở rộng ngoài D1–D17.
> **Nhánh:** `VHT-42-kiểm-thử-hợp-dồng-va-giấy-tờ-p-4`.
> **Commit nền kiểm thử:** `049c22d` (merge PR #56, mốc `dev` hiện tại).
> **Ngày kiểm thử:** 02/11/2026, giờ Việt Nam.
> **Nguồn đối chiếu:** `docs/task/cong_viec_theo_tuan.md` (VHT-42, VHT-124, VHT-129),
> `docs/task/timeline_song_song.md`, `docs/task/thao_tac_va_quyen.xlsx` (D1–D17),
> `docs/VHT-9_trang_thai_ky_du_lieu_P4_P5.md`, `docs/Imp/VHT-30-2B-ra-soat-loi.md`,
> `docs/archive/imp/VHT-22-kiem-thu-p-1-p-3-va-ghi-tai-lieu.md`.

## 0. Kết luận nhanh

- **Đã sửa 5 lỗi trong 2D:** tạo hợp đồng nay kiểm role + phạm vi nhà, chặn hợp đồng chờ duyệt
  trùng (kể cả khi tạo đồng thời), chặn duyệt hợp đồng cho phòng không còn trống, Quản lý được phân
  công đã xem được giấy tờ qua implementation thật của `IManagerTenantScope`, và UI hợp đồng P4 đã
  ghép thật (list/create/detail/duyệt) thay cho mock.
- **Luồng lõi đã ghép của hợp đồng đạt:** tạo hợp đồng có snapshot, chặn phòng không khả dụng,
  rollback khi insert/update lỗi, duyệt tuần tự và đồng thời chỉ một hợp đồng hiệu lực, chặn duyệt
  trùng, chặn duyệt chéo chủ trọ, chặn Quản lý/Admin duyệt, snapshot được chốt lại đúng tại thời
  điểm duyệt và **không đổi khi giá phòng thay đổi sau đó**.
- **Giấy tờ/hồ sơ đạt case quyền:** khách thuê chỉ thấy/tải hồ sơ của mình; người khác bị 404; Admin
  bị chặn ở storage; Chủ trọ đúng phạm vi; Quản lý đã phân công xem được nhưng không xuất; thay/xóa
  mềm đúng và audit không chứa nội dung.
- **UI hợp đồng P4 đã PASS trên app thật:** tạo → danh sách → chi tiết → duyệt chạy end-to-end; Admin
  bị `AccessDenied`; Quản lý và Khách thuê vào list đúng phạm vi, không thấy nút duyệt/tạo trái quyền.
- **Kết luận 2D:** phần **dịch vụ P3–P4, UI hợp đồng P4 và UI giấy tờ `/documents` đã đạt**. Không còn
  lỗi mở trong phạm vi hợp đồng P4/giấy tờ.
- **Mức đáp ứng yêu cầu chung (2D + 2E):** **2D đạt**, nhưng **chưa đủ để đóng yêu cầu** vì
  (1) **2E chưa chạy** trên commit ghép tuần 2 và (2) bản sửa **chưa commit** nên bàn giao chưa có
  hash "theo commit". Chi tiết ở mục 8.

## 1. Môi trường và bằng chứng chạy thật

| Thành phần | Giá trị |
| --- | --- |
| OS / shell | Windows, PowerShell |
| .NET SDK | 10.0.x |
| Database test | SQL Server LocalDB, database riêng `NhaTro_It_<guid>` cho mỗi fixture |
| Cấu hình test | `EnsureCreated` theo model hiện tại; trigger chống trùng được tạo/xóa trong test |
| Commit nền | `049c22d` |
| Ứng dụng UI | Chạy local trên database tạm `NhaTroContractSmoke` (hợp đồng) và `NhaTroDocsSmoke` (giấy tờ), đều là LocalDB và đã xóa sau smoke; đăng nhập 4 vai trò bằng quick-login dev; `NhaTroDocsSmoke` có thêm hồ sơ `Tenant` gắn với `khachthue@nhatro.local` |
| Công cụ UI | Browser automation (Playwright MCP) trên `http://localhost:5177` |

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build NhaTro/NhaTro.slnx --nologo` | **0 error, 0 warning** sau khi bỏ key resx trùng |
| `dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj` | **608 pass, 0 fail** |
| `dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj` | **177 pass, 0 fail** |
| `dotnet test ... --filter "FullyQualifiedName~ContractServiceIntegrationTests\|FullyQualifiedName~TenantDocumentScopeIntegrationTests"` | **26/26 pass** sau khi sửa lỗi |
| `dotnet test ... --filter "FullyQualifiedName~ContractServiceIntegrationTests"` | **25/25 pass** (gồm 7 test mới/đã chỉnh của VHT-42) |
| `dotnet test NhaTro/NhaTro.E2ETests` (app thật + database tạm) | **6/6 pass** — 3 Rooms UI regression + 2 auth smoke + 1 placeholder (2 test auth đã sửa selector/kỳ vọng ngôn ngữ trong VHT-42) |
| Smoke UI 4 vai trò trên app thật | **PASS** — chi tiết ở mục 2.4 |
| Test tạm tái hiện lỗi trước khi sửa (đã xóa) | **3/3 pass** — chứng minh 3 hành vi sai đã được sửa và thay bằng test âm |

> 3 test tạm chỉ dùng để xác nhận lỗi, không commit vì chúng khẳng định hành vi **sai**. Bước tái hiện
> chính thức nằm ở mục 3; khi sửa xong phải chuyển thành test âm (kỳ vọng bị chặn) ở bản retest.

## 2. Bảng case và kết quả

Ký hiệu: **PASS** = chạy đúng kỳ vọng; **FAIL** = tái hiện sai hành vi; **BLOCKED** = thiếu bản ghép/
UI/tài khoản nên chưa kết luận.

### 2.1. Hợp đồng và phòng (P4)

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| C-01 | Tạo hợp đồng hợp lệ cho phòng `Available` | Lưu hợp đồng + thành viên + snapshot giá/cọc | **PASS** | `CreateContractAsync_PersistsContractAndMembersWithPriceSnapshots` |
| C-02 | Tạo hợp đồng cho phòng không `Available` | Bị từ chối | **PASS** | `CreateContractAsync_RejectsRoomThatIsNotAvailable` |
| C-03 | Tạo hợp đồng với khách thuê không tồn tại/ngừng hoạt động | Bị từ chối | **PASS** | `CreateContractAsync_RejectsTenantIdThatDoesNotExist` |
| C-04 | Insert thành viên lỗi giữa transaction | Rollback cả hợp đồng, không ghi dở | **PASS** | `CreateContractAsync_RollsBackContractWhenMemberInsertFails` |
| C-05 | Tạo 2 hợp đồng `Pending` cùng phòng, cùng khoảng ngày | Chỉ một hợp đồng chờ duyệt hoặc bị chặn | **PASS** | `CreateContractAsync_RejectsDuplicatePendingContractForSameRoomAndPeriod`; `CreateContractAsync_ConcurrentDuplicatesOnlyPersistOne` (2 luồng song song) — một thắng, một `PendingContractExists`, chỉ 1 hợp đồng + 1 thành viên được ghi |
| C-05b | Tạo tuần tự hai hợp đồng cùng phòng nhưng **không chồng lấn** | Cả hai được tạo | **PASS** | `CreateContractAsync_AllowsNonOverlappingPendingContracts` |
| C-06 | Admin/Khách thuê gọi tạo hợp đồng | Bị từ chối | **PASS** | `CreateContractAsync_RejectsRolesOtherThanChuTroOrQuanLy` (Admin + Khách thuê) |
| C-06b | Chủ trọ A tạo hợp đồng cho phòng của Chủ trọ B | Bị từ chối, không ghi dữ liệu | **PASS** | `CreateContractAsync_RejectsRoomOwnedByAnotherOwner` |
| C-07 | Duyệt hợp đồng `Pending` | `Approved`, phòng sang `Rented` | **PASS** | `ApproveContractAsync_ApprovesContractAndRentsRoom` |
| C-08 | Duyệt lại/từ chối lại hợp đồng đã quyết định | `InvalidStatus` | **PASS** | `ApproveContractAsync_DuplicateApprovalAndRejectionAreBlocked` |
| C-09 | Duyệt hợp đồng khi đã có hợp đồng hiệu lực chồng lấn | `ContractOverlap`, không đổi phòng | **PASS** | `ApproveContractAsync_RejectsOverlappingApprovedContract` |
| C-10 | Hai luồng duyệt chồng lấn chạy đồng thời | Chỉ một luồng thắng | **PASS** | `ApproveContractAsync_ConcurrentOverlappingRequestsOnlyApproveOne` |
| C-11 | Ghi trực tiếp trạng thái `Approved` bỏ qua service | Trigger DB chặn (SQL 51030) | **PASS** | `ContractsTable_RejectsOverlappingApprovedContractOutsideService` |
| C-12 | Chủ trọ A duyệt hợp đồng thuộc nhà Chủ trọ B | Bị từ chối, không đổi dữ liệu | **PASS** | `ApproveContractAsync_RejectsContractOwnedByAnotherOwner` |
| C-13 | Quản lý duyệt hợp đồng | Bị từ chối | **PASS** | `ApproveContractAsync_BlocksManagerForContractTheyCreated` |
| C-14 | Admin/Khách thuê duyệt hợp đồng | Bị từ chối | **PASS** | Unit test `ApproveContractAsync_RejectsRolesOtherThanChuTro` |
| C-15 | Duyệt hợp đồng của nhà đã xóa mềm / phòng bảo trì | Bị chặn, giữ nguyên trạng thái | **PASS** | `ApproveContractAsync_RejectsContractForSoftDeletedProperty`, `ApproveContractAsync_RejectsRoomUnderMaintenanceWithoutChangingIt` |
| C-16 | Duyệt hợp đồng của phòng đang `Rented` (không có hợp đồng hiệu lực chồng lấn) | Bị chặn vì phòng không còn `Available` | **PASS** | `ApproveContractAsync_RejectsRentedRoomWithoutChangingIt` — `RoomNotVacant`, giữ nguyên hợp đồng/phòng |
| C-17 | Đổi giá phòng giữa lúc tạo và lúc duyệt | Snapshot/cọc chốt lại theo giá tại thời điểm duyệt | **PASS** | `ApproveContractAsync_SnapshotsCurrentRoomPriceAndDeposit` |
| C-18 | Đổi giá phòng **sau khi đã duyệt** | Snapshot đã chốt không đổi | **PASS** | `ApproveContractAsync_KeepsSnapshotWhenRoomPriceChangesAfterApproval` (**test mới VHT-42**) |
| C-19 | Cập nhật phòng lỗi khi duyệt | Rollback cả hợp đồng và phòng | **PASS** | `ApproveContractAsync_RollsBackContractWhenRoomUpdateFails` |
| C-20 | Hợp đồng không tồn tại | `ContractNotFound` | **PASS** | `ApproveContractAsync_ThrowsNotFoundForMissingContract` |
| C-21 | Từ chối hợp đồng thiếu/ quá dài lý do | `RejectReasonRequired` / `RejectReasonTooLong` | **PASS** | Unit tests `ApproveContractAsync_RequiresReasonWhenRejecting`, `...RejectsReasonLongerThan500Characters` |

### 2.2. Giấy tờ và hồ sơ (P3–P4)

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| D-01 | Khách thuê A xem/tải giấy tờ hồ sơ A | Thành công | **PASS** | `Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung`, `Service_P3_gan_metadata_thay_va_xoa_dung_ho_so` |
| D-02 | Khách thuê B biết `fileId` của A và tải | `null` (404), không lộ tồn tại | **PASS** | cùng test |
| D-03 | Khách thuê B gọi list hồ sơ A | `null` (404) | **PASS** | `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` |
| D-04 | Chủ trọ xem/tải giấy tờ khách thuộc nhà mình | Thành công | **PASS** | `Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung` |
| D-05 | Chủ trọ khác truy cập hồ sơ không thuộc nhà mình | Không có quyền (`None`/404) | **PASS** | `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` — thêm assertion Chủ trọ `chu-tro-2` nhận `None`; service join `Property.OwnerId` |
| D-06 | Admin list/upload/download/delete giấy tờ | Bị chặn ở service/policy | **PASS** | `Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung`, `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` |
| D-07 | Quản lý chưa được giao tòa nhà | Bị từ chối | **PASS** | `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` |
| D-08 | Quản lý **đã được giao** tòa nhà (theo VHT-24) | Xem được, **không xuất** (D9) | **PASS với scope giả** | `AllowManagerScope`: `Manager`, `CanView = true`, `CanExport = false` |
| D-09 | Quản lý đã được giao trên **đăng ký production hiện tại** | Xem được giấy tờ đúng tòa nhà | **PASS** | `Program.cs` đăng ký `PropertyManagerTenantScope`; test thật trong `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` (gán → thấy, bỏ gán → mất quyền) |
| D-10 | Upload giấy tờ vào hồ sơ khách khác | `Forbidden` | **PASS** | `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` |
| D-11 | Gửi `RelatedEntityId`/loại tệp khác hồ sơ đích | `InvalidRequest` | **PASS** | `Service_P3_gan_metadata_thay_va_xoa_dung_ho_so` (`crossProfile`) |
| D-12 | Thay tệp giấy tờ | Tệp mới là bản hiện hành duy nhất; tệp cũ không tải được | **PASS** | `Service_P3_gan_metadata_thay_va_xoa_dung_ho_so` |
| D-13 | Xóa giấy tờ | Xóa mềm; khách thuê không tự xóa, Chủ trọ xóa được | **PASS** | `Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung` |
| D-14 | Audit truy cập tệp | Ghi actor/hành động/kết quả; không chứa tên tệp/nội dung | **PASS** | cùng test |
| D-15 | Quản lý xuất danh sách giấy tờ | Bị chặn bởi `CanExport` | **PASS** | `CanExport` trả `false`; UI ẩn nút |

### 2.3. Admin bị chặn trên P2–P4 (bổ sung D1–D17, 12/10/2026)

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| A-01 | Admin gọi P2: list/get/create/update nhà, list/get/create/update/delete/restore phòng | Bị `UnauthorizedAccessException` | **PASS** | `Admin_bi_chan_o_moi_service_nghiep_vu_P2_P3` |
| A-02 | Admin gọi P3: list/get/get-own/create/update/link/unlink khách thuê | Bị `UnauthorizedAccessException` | **PASS** | cùng test |
| A-03 | Admin gọi storage: upload/download/list/delete tệp | Bị chặn | **PASS** | `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh`; smoke UI: Admin vào `/documents` bị chuyển `/Account/AccessDenied?ReturnUrl=%2Fdocuments` (trang chỉ cho `KhachThue`) |
| A-04 | Admin duyệt hợp đồng P4 | Bị chặn | **PASS** | Unit test `ApproveContractAsync_RejectsRolesOtherThanChuTro` |
| A-05 | Admin tạo hợp đồng P4 ở tầng service | Bị chặn | **PASS** | `CreateContractAsync` nhận `PropertyCaller`, chặn Admin/Khách thuê; test `CreateContractAsync_RejectsRolesOtherThanChuTroOrQuanLy` |
| A-06 | Admin vào trang tạo hợp đồng `/contracts/create` | Bị chặn | **PASS (UI)** | `ContractCreateForm.razor:3` dùng policy `ContractManage` (chặn Admin); smoke: Admin vào `/contracts` bị chuyển `/Account/AccessDenied` |
| A-07 | Admin vào list/detail/duyệt hợp đồng P4 trên UI | Bị chặn | **PASS (UI + service)** | Policy `ContractBusiness` chặn Admin ở `ContractListView`/`ContractDetailView`; smoke xác nhận `AccessDenied?ReturnUrl=%2Fcontracts`. Tầng service: `EnsureCanView` trong `ListAsync`/`GetAsync` ném `ContractAuthorizationException` cho Admin; `GetCreateOptionsAsync` chặn qua `EnsureCanCreate` — kiểm trong `ListAndGetAsync_ApplyOwnerManagerTenantAndAdminScopes` |

### 2.4. Case UI 2D

| Mã | Case | Kỳ vọng | Kết quả | Ghi chú |
| --- | --- | --- | --- | --- |
| U-01 | Tạo hợp đồng trên `/contracts/create` | Lưu thật vào DB | **PASS** | Smoke Chủ trọ: chọn nhà/phòng/khách → gửi duyệt → chuyển `/contracts/{id}`, DB có hợp đồng `Pending` với snapshot 2.000.000 ₫ / cọc 400.000 ₫ |
| U-02 | Danh sách/chi tiết/duyệt hợp đồng | Đúng quyền, đúng trạng thái | **PASS** | Smoke: list hiển thị hợp đồng; Chủ trọ duyệt qua modal → trạng thái `Hiệu lực`, DB `Contracts.Status = Approved`, `Rooms.Status = Rented`; Admin bị `AccessDenied`; Quản lý/Khách thuê không thấy nút duyệt |
| U-03 | Khách thuê upload/tải/xóa giấy tờ trên `/documents` | Đúng quyền | **PASS** | Smoke `khachthue@nhatro.local` trên `NhaTroDocsSmoke`: trang nhận đúng hồ sơ (1 tệp), upload PDF 218 B thành công, danh sách hiển thị tệp, tải xuống thành công; audit ghi `Upload`/`Download` `Succeeded = 1`. Không có nút xóa cho Khách thuê (đúng D13). Admin vào `/documents` bị `AccessDenied` |

## 3. Danh sách lỗi và người sửa

| Mã | Mức | Mô tả | Người nhận | Trạng thái |
| --- | --- | --- | --- | --- |
| BUG-2D-01 | **Cao** | `CreateContractAsync` không nhận caller, không kiểm role/phạm vi chủ sở hữu; Admin (hoặc bất kỳ role gọi được service) tạo được hợp đồng. | Tính (VHT-29/VHT-31) | **Đã sửa** — caller + role + `PropertyScope`; retest `CreateContractAsync_RejectsRolesOtherThanChuTroOrQuanLy`, `...RejectsRoomOwnedByAnotherOwner` |
| BUG-2D-02 | **Cao** | Cho tạo nhiều hợp đồng `Pending` cùng phòng, cùng khoảng ngày; không chặn trùng ở bước tạo. | Tính (VHT-29/VHT-31) | **Đã sửa** — khóa phòng + kiểm chồng lấn trong transaction; retest `...RejectsDuplicatePendingContractForSameRoomAndPeriod` |
| BUG-2D-03 | Trung bình | Duyệt được hợp đồng cho phòng đang `Rented` (chỉ chặn `Maintenance`), rồi ghi đè `Rented`. | Tính (VHT-30) | **Đã sửa** — thêm `RoomNotVacant`; retest `ApproveContractAsync_RejectsRentedRoomWithoutChangingIt` |
| BUG-2D-04 | **Cao** | Production đăng ký `NoManagerTenantScope`, nên Quản lý đã được phân công vẫn bị deny-all giấy tờ, trái D9/VHT-24. | Tính (VHT-24) + Phúc (VHT-26) | **Đã sửa** — thêm `PropertyManagerTenantScope`, đổi đăng ký DI; retest trong `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` |
| BUG-2D-05 | **Blocker** | UI hợp đồng P4 chưa ghép: `/contracts/create` chỉ mock; không có list/detail/duyệt trên `dev`. | Phú (ghép VHT-31) + Đào (VHT-124) | **Đã sửa** — ghép UI thật + policy `ContractBusiness`/`ContractManage`; smoke 4 vai trò **PASS** |
| BUG-2D-06 | Thấp (test) | Trước 2D chưa có test khẳng định “đổi giá sau khi duyệt không đổi snapshot”. | Phát (VHT-42) | **Đã bổ sung test và PASS** |

## 4. Chi tiết lỗi, bước tái hiện và hướng sửa

### BUG-2D-01 — [Cao] Tạo hợp đồng không kiểm quyền/phạm vi

**Vị trí:** `NhaTro/Web/Features/Contracts/Services/ContractService.cs:208-309`;
`NhaTro/Web/Features/Contracts/Services/IContractService.cs:8`.

**Nguyên nhân:** `CreateContractAsync(CreateContractRequest request)` không có `PropertyCaller`; method
truy vấn phòng theo `RoomId` mà không join `Properties`/`PropertyManagers`, không kiểm role. Vì vậy
không chỉ Chủ trọ/Quản lý đúng phạm vi mới tạo được, mà Admin và mọi caller nội bộ đều tạo được.

**Hệ quả:** Admin có thể tạo hợp đồng P4 ở tầng nghiệp vụ, trái yêu cầu “Admin bị chặn mọi thao tác
nghiệp vụ”. Chủ trọ A cũng có thể tạo hợp đồng cho phòng của Chủ trọ B nếu gọi service trực tiếp.

**Cách tái hiện (đã chạy):**
1. Seed `admin-x` role `Admin`, `owner-x` role `ChuTro`, một `Property` của `owner-x`, một `Room`
   `Available`, một `Tenant`.
2. Gọi `new ContractService(db).CreateContractAsync(new CreateContractRequest(room.Id, 01/11/2026,
   01/12/2026, [tenant.Id]))` **không truyền caller**.
3. Kết quả: hợp đồng được tạo thành công (test tạm pass; đây là hành vi sai).

**Đã sửa:** `IContractService`/`ContractService` nhận `PropertyCaller caller`; chỉ `ChuTro`/`QuanLy`
được tạo; phòng được lọc qua `PropertyScope.Apply(...)` nên Chủ trọ chỉ thấy nhà mình, Quản lý chỉ
thấy nhà được phân công. Trả `ContractAuthorizationException` khi sai role; phòng ngoài phạm vi bị
coi như không tồn tại (không lộ dữ liệu).

**Retest:** `CreateContractAsync_RejectsRolesOtherThanChuTroOrQuanLy` (Admin, Khách thuê) và
`CreateContractAsync_RejectsRoomOwnedByAnotherOwner` — **PASS**.

---

### BUG-2D-02 — [Cao] Tạo trùng hợp đồng chờ duyệt cùng phòng/khoảng ngày

**Vị trí:** `ContractService.cs:208-309` (không có kiểm chồng lấn ở bước tạo).

**Nguyên nhân:** kiểm chồng lấn chỉ có ở `ApproveContractAsync` và trigger DB chỉ chặn hai hợp đồng
**đã duyệt** chồng lấn. Nhiều hợp đồng `Pending` cho cùng phòng/khoảng ngày vẫn được ghi.

**Hệ quả:** người dùng bấm tạo hai lần hoặc hai Chủ trọ/Quản lý cùng tạo sẽ sinh dữ liệu rác; khi duyệt
chỉ một hợp đồng thắng, các hợp đồng còn lại treo `Pending` và gây nhầm lẫn hàng đợi duyệt.

**Cách tái hiện (đã chạy):**
1. Seed phòng `Available` và một khách thuê.
2. Gọi `CreateContractAsync` hai lần với **cùng** `RoomId`, `StartDate`, `EndDate`, `TenantIds`.
3. Kết quả: `first.ContractId != second.ContractId` và `db.Contracts.Count() == 2`.

**Đã sửa:** trong transaction tạo, khóa dòng phòng bằng `UPDLOCK, HOLDLOCK` rồi kiểm hợp đồng
`Pending`/`Approved` chồng lấn; trả `ContractErrorCodes.PendingContractExists`. Cách này chặn cả hai
yêu cầu tạo đồng thời cho cùng phòng, không chỉ kiểm tra tuần tự.

**Retest:** `CreateContractAsync_RejectsDuplicatePendingContractForSameRoomAndPeriod` — **PASS**;
`CreateContractAsync_ConcurrentDuplicatesOnlyPersistOne` — **PASS** (2 transaction song song, một
thắng một bị `PendingContractExists`, DB chỉ có 1 hợp đồng); `CreateContractAsync_AllowsNonOverlappingPendingContracts`
bảo đảm không chặn nhầm hợp đồng nối tiếp.

---

### BUG-2D-03 — [Trung bình] Duyệt hợp đồng cho phòng đang `Rented`

**Vị trí:** `ContractService.cs:159-164` (chỉ chặn `Maintenance`) và `:180-184` (gán `Approved` +
`Rented`).

**Nguyên nhân:** điều kiện duyệt không yêu cầu phòng phải `Available`; chỉ kiểm `IsDeleted`,
`IsActive` và `Maintenance`. Nếu phòng đã `Rented` nhưng chưa có hợp đồng hiệu lực chồng lấn (dữ liệu
lệch, hợp đồng cũ đã thanh lý nhưng quên trả trạng thái), hợp đồng mới vẫn được duyệt.

**Hệ quả:** hai nguồn cùng cho rằng phòng đã thuộc về hai hợp đồng; snapshot/cọc mới ghi đè trạng thái
phòng mà không có cảnh báo.

**Cách tái hiện (đã chạy):**
1. Seed phòng `Status = Rented`, không có hợp đồng `Approved` chồng lấn.
2. Thêm hợp đồng `Pending` cho phòng, gọi `ApproveContractAsync(..., IsApproved = true, OwnerCaller)`.
3. Kết quả: `response.Status == Approved` (test tạm pass; hành vi sai).

**Đã sửa:** sau khi kiểm chồng lấn, chỉ cho duyệt khi `room.Status == RoomStatus.Available`; nếu
không trả `ContractErrorCodes.RoomNotVacant` trước khi ghi bất kỳ thay đổi nào.

**Retest:** `ApproveContractAsync_RejectsRentedRoomWithoutChangingIt` — **PASS**; hợp đồng vẫn
`Pending`, phòng vẫn `Rented`.

---

### BUG-2D-04 — [Cao] Quản lý được phân công vẫn bị deny-all giấy tờ

**Vị trí:** `NhaTro/Web/Program.cs:319`:
`builder.Services.AddScoped<IManagerTenantScope, NoManagerTenantScope>();`

**Nguyên nhân:** VHT-24 đã có bảng `PropertyManagers` và `PropertyScope`, nhưng chưa có
implementation `IManagerTenantScope` đọc bảng phân công. `NoManagerTenantScope` luôn trả `false`,
nên `TenantDocumentScopeService` luôn trả `None` cho Quản lý trong production, dù test với
`AllowManagerScope` chứng minh logic giấy tờ đã sẵn sàng.

**Hệ quả:** Quản lý được giao tòa nhà không xem/tải được giấy tờ hồ sơ khách thuộc tòa nhà mình —
trái D9 và mục tiêu “Quản lý thấy P3 theo tòa nhà được phân công”. Đây là lỗi tích hợp, không phải
lỗi của `TenantDocumentScopeService`.

**Cách tái hiện:** phân công `manager-1` vào `Property` A (VHT-24), tạo khách + hợp đồng trong A,
đăng nhập Quản lý trên app production → `/documents`/API giấy tờ trả không có quyền; trong khi test
dùng `AllowManagerScope` trả `Manager`.

**Đã sửa:** thêm `PropertyManagerTenantScope` đọc `PropertyManagers` đang hiệu lực
(`RevokedAt == null`), join `ContractMembers` → `Contracts` → `Rooms` → `Properties`; đổi
`Program.cs` sang implementation này. Chưa phân công hoặc đã bỏ gán vẫn deny-by-default.

**Retest:** trong `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so`, gán Quản lý vào nhà → `CanView`,
`CanExport = false`; bỏ gán → `None` ngay — **PASS**.

---

### BUG-2D-05 — [Blocker] UI hợp đồng P4 chưa ghép — ĐÃ SỬA

**Vị trí cũ:** `NhaTro/Web/Features/Contracts/Pages/ContractCreateForm.razor:1-3` (chỉ mock, không gọi
service); `dev` không có `ContractListView`/`ContractDetailView`.

**Nguyên nhân:** VHT-31 nằm trên `origin/VHT-31-ghep-tao-va-duyet-hop-dong-p4` (commit `a97ffe7`,
`b7734e5`) và chưa merge vào `dev`; VHT-124 (sửa UI hợp đồng sau ghép) chưa có trên nhánh nào được
kiểm. Trang tạo hợp đồng chỉ dùng `MockData`, không gọi `IContractService`.

**Đã sửa:**
- Ghép `ContractPageService` + DTO + `ContractListView` (`/contracts`), `ContractDetailView`
  (`/contracts/{id}`), `ContractApproveModal` và các component hiển thị trạng thái từ `b7734e5`.
- Thay form mock bằng form thật: lấy option qua `GetCreateOptionsAsync`, submit qua
  `CreateContractAsync`, hiển thị lỗi typed `IContractServiceError`, chuyển tới trang chi tiết sau
  khi tạo, và báo "không có phòng trống" khi nhà đã chọn không còn phòng `Available`.
- Thêm policy `ContractBusiness` (ChuTro/QuanLy/KhachThue, chặn Admin) cho list/detail và
  `ContractManage` (ChuTro/QuanLy, chặn Admin) cho trang tạo; service vẫn kiểm role/phạm vi lần 2.
- Giữ nguyên các fix 2D ở `ContractService`: khóa phòng + chặn `Pending` trùng khi tạo, chặn duyệt
  phòng `Rented`/bảo trì/nhà đã xóa, snapshot tại thời điểm duyệt. Bỏ ràng buộc "không được bắt đầu
  trong tương lai" của VHT-31 để không phá case C-05 (hợp đồng nối tiếp).
- Cập nhật menu: mục Hợp đồng hiển thị cho `ChuTro,QuanLy,KhachThue`.

**Retest (app thật, database tạm):**
1. Chưa đăng nhập vào `/contracts` → chuyển `/Account/Login?ReturnUrl=%2Fcontracts`.
2. Chủ trọ: tạo hợp đồng cho phòng `Available` + khách thuê → chuyển `/contracts/{id}`, trạng thái
   `Chờ duyệt`, snapshot/cọc đúng; duyệt qua modal → `Hiệu lực`; DB ghi `Status = Approved` và phòng
   sang `Rented`.
3. Danh sách hiển thị hợp đồng vừa tạo với đúng mã phòng/nhà/khách/trạng thái.
4. Quản lý: vào được list và trang tạo trong phạm vi; không thấy nút duyệt (chỉ Chủ trọ).
5. Khách thuê: vào được list nhưng không có nút tạo/duyệt.
6. Admin: vào `/contracts` bị `/Account/AccessDenied`.

**Kết quả:** **PASS** — U-01, U-02, A-06, A-07 chuyển từ BLOCKED sang PASS. Database tạm đã được
xóa sau smoke, không đụng dữ liệu dev hiện có. U-03 được smoke riêng trên `NhaTroDocsSmoke` (mục 2.4).

## 5. Phần chưa kiểm được và điều kiện retest

| Hạng mục | Lý do | Điều kiện retest |
| --- | --- | --- |
| UI tạo/list/detail/duyệt hợp đồng | Đã ghép UI thật + policy + smoke 4 vai trò | **Đã retest PASS** — U-01/U-02/A-06/A-07 |
| Quản lý xem giấy tờ đã phân công | Đã có `PropertyManagerTenantScope` + test tích hợp | **Đã retest PASS** — D-09 |
| Smoke UI giấy tờ `/documents` | Đã gieo `Tenant` cho tài khoản khách thuê dev trên database tạm | **Đã retest PASS** — U-03 (upload/download/Admin bị chặn) |
| BUG-2D-01/02/03 | Đã sửa dịch vụ và thêm test âm | **Đã retest PASS** toàn bộ `ContractServiceIntegrationTests` 25/25 |
| Duyệt đồng thời trên SQL production | Đã test LocalDB với `UPDLOCK/HOLDLOCK` + trigger; chưa chạy trên SQL Server/IIS thật | Retest trên bản publish 2E/VHT-44 nếu môi trường khác biệt |
| **Đợt 2E** | Chưa có commit bản sửa và chưa có commit ghép tuần 2 để retest | **BLOCKED** — commit branch 42, merge bản ghép tuần 2, chạy lại toàn bộ case + regression, Phát điều phối xác nhận |
| **Bàn giao "theo commit"** | Các fix đang ở working tree branch 42, chưa commit | Cần commit hash của bản sửa; ghi hash đó vào bảng case và mục 7 trước khi bàn giao |
| **Case UI P3 (Phúc)** | Đã smoke được bằng tài khoản dev + `Tenant` seed trên DB tạm | **Đã retest PASS** — U-03; Phúc vẫn nên xác nhận chéo khi chạy VHT-36 |

**Điều kiện nghiệm thu lại:** không còn hợp đồng `Pending` trùng; Admin và caller sai phạm vi bị chặn
ở cả service lẫn UI; phòng `Rented`/`Maintenance` không bị duyệt đè; snapshot sau duyệt bất biến;
Quản lý đã phân công xem được giấy tờ nhưng không xuất; unit + integration + E2E **xanh toàn bộ**
(6/6 E2E sau khi sửa selector auth).

## 6. File thay đổi trong nhánh

| File | Việc |
| --- | --- |
| `NhaTro/Web/Features/Contracts/Services/IContractService.cs` | Đổi chữ ký `CreateContractAsync` nhận `PropertyCaller` + `CancellationToken`; bổ sung list/get/create-options/approve |
| `NhaTro/Web/Features/Contracts/Services/ContractService.cs` | Kiểm role/phạm vi khi tạo; khóa phòng + chặn trùng `Pending`; chặn duyệt phòng không trống (`RoomNotVacant`) |
| `NhaTro/Web/Features/Contracts/Services/ContractPageService.cs` | Resolve caller qua `P2CallerProvider` cho mọi thao tác UI |
| `NhaTro/Web/Features/Contracts/DTOs/ContractCreateOptionsDto.cs` | DTO option tạo hợp đồng (nhà/phòng/khách) |
| `NhaTro/Web/Features/Contracts/DTOs/ContractListItemDto.cs` | DTO list/detail/member hợp đồng |
| `NhaTro/Web/Features/Contracts/Rules/ContractStatusPresentation.cs` | Trạng thái hiển thị dẫn xuất (Pending/Upcoming/Active/Expiring/Expired) |
| `NhaTro/Web/Features/Contracts/Pages/ContractListView.razor(.css)` | Danh sách + tìm kiếm + duyệt hợp đồng, route `/contracts` |
| `NhaTro/Web/Features/Contracts/Pages/ContractDetailView.razor(.css)` | Chi tiết + duyệt/từ chối, route `/contracts/{id}` |
| `NhaTro/Web/Features/Contracts/Components/ContractApproveModal.razor(.css)` | Modal duyệt/từ chối kèm lý do |
| `NhaTro/Web/Features/Contracts/Pages/ContractCreateForm.razor(.css)` | Thay mock bằng form thật gọi `IContractService` |
| `NhaTro/Web/Features/Contracts/ContractServiceErrors.cs` | Thêm mã/thông điệp `RoomNotVacant`, `PendingContractExists`; sửa thông điệp `Forbidden` dùng chung cho tạo/duyệt |
| `NhaTro/Web/Features/Tenants/Documents/PropertyManagerTenantScope.cs` | Implementation thật của `IManagerTenantScope` theo `PropertyManagers` đang hiệu lực |
| `NhaTro/Web/Program.cs` | Đăng ký `PropertyManagerTenantScope`; thêm policy `ContractBusiness`/`ContractManage`; đăng ký `ContractPageService` |
| `NhaTro/Web/Components/Layout/AppNavigation.cs` | Hiện mục Hợp đồng cho `ChuTro,QuanLy,KhachThue` |
| `NhaTro/Web/Components/Shared/Feedback/ToastHost.razor` | Nút đóng toast có nhãn i18n |
| `NhaTro/Web/Resources/Features/Contracts/ContractResources.*.resx` | Bổ sung key UI/danh sách/duyệt và mã lỗi typed |
| `NhaTro/Web/Resources/Shared.*.resx` | Thêm `CloseNotification` |
| `NhaTro/NhaTro.UnitTests/Features/Contracts/ContractServiceTests.cs` | Cập nhật caller; thêm test Admin/Khách thuê không được tạo hợp đồng |
| `NhaTro/NhaTro.UnitTests/Features/Contracts/ContractStatusPresentationTests.cs` | Test trạng thái hiển thị dẫn xuất |
| `NhaTro/NhaTro.IntegrationTests/Features/Contracts/ContractServiceIntegrationTests.cs` | Cập nhật caller; thêm test chéo chủ trọ, trùng `Pending`, không chồng lấn, phòng `Rented`, snapshot sau duyệt |
| `NhaTro/NhaTro.IntegrationTests/Features/Tenants/TenantDocumentScopeIntegrationTests.cs` | Thêm assertion Chủ trọ khác nhận `None`; test `PropertyManagerTenantScope` gán/bỏ gán |
| `NhaTro/NhaTro.E2ETests/Features/Auth/LoginSmokeTests.cs` | Sửa selector theo markup thật (`#login-email`, `#login-password`) và bỏ kỳ vọng tiếng Anh cứng |
| `docs/Imp/VHT-42-kiem-thu-hop-dong-va-giay-to-p4-2D.md` | Tài liệu case/kết quả/lỗi/người sửa/retest của đợt 2D |

Không tạo migration; không commit test tạm tái hiện lỗi; UI được ghép trực tiếp từ `b7734e5` (VHT-31 +
VHT-124) vào nhánh này thay vì merge nguyên nhánh.

## 7. Xác nhận chạy lại

- Trước khi thêm test VHT-42: `049c22d` — 594 unit, 170 integration, 3 E2E pass + 3 skip.
- Sau khi thêm test 2D (trước sửa lỗi): 594 unit + 171 integration + 3 E2E pass.
- Sau khi sửa BUG-2D-01 → 04 và bổ sung test âm: **596 unit + 175 integration**, **0 fail**.
- Sau khi ghép UI P4 (BUG-2D-05) và bổ sung test snapshot/UI:
  - `dotnet build NhaTro/NhaTro.slnx` → **0 error, 0 warning**.
  - `NhaTro.UnitTests` → **608 pass**.
  - `NhaTro.IntegrationTests` → **177 pass**.
  - `ContractServiceIntegrationTests` → **25/25 pass**; `TenantDocumentScopeIntegrationTests` → **1/1 pass**.
  - `NhaTro.E2ETests` (app thật + database tạm) → **6/6 pass** sau khi sửa selector/kỳ vọng ngôn ngữ
    của 2 test auth (`#login-email`, `#login-password`, `#login-title`).
- Smoke UI trên app thật + database tạm: **PASS** cho tạo/list/detail/duyệt hợp đồng, phân quyền 4 vai
  trò và giấy tờ `/documents` (upload/download/Admin bị chặn) — chi tiết mục 2.4 và BUG-2D-05.
- Không còn case **BLOCKED** trong phạm vi 2D; phần còn lại là 2E trên bản chung (mục 8).

## 8. Đợt 2E và phần chưa đáp ứng yêu cầu

### 8.1. Trạng thái đợt 2E — CHƯA THỰC HIỆN

Yêu cầu 2E là **chạy lại từng lỗi và regression trên commit ghép tuần 2**, xác nhận sau sửa. Hiện
chưa thể thực hiện vì:

1. Bản sửa 2D đang nằm ở working tree branch 42, **chưa commit** → chưa có hash để retest "theo commit".
2. Branch 42 **chưa merge** vào bản chung/commit ghép tuần 2 → chưa có "bản chung" để retest.
3. Theo yêu cầu chỉ sửa trong branch 42, không merge/không tác động branch khác trong đợt này.

**Điều kiện để chạy 2E:**
1. Commit toàn bộ bản sửa lên `VHT-42-kiểm-thử-hợp-dồng-va-giấy-tờ-p-4`, lấy hash.
2. Merge vào commit ghép tuần 2 (hoặc bản `dev` chung theo kế hoạch).
3. Chạy lại `ContractServiceIntegrationTests`, `TenantDocumentScopeIntegrationTests`, unit tests,
   smoke UI 4 vai trò và E2E trên bản đã merge.
4. Ghi hash commit + kết quả từng case vào tài liệu; **Phát điều phối xác nhận retest**.

### 8.2. Các điểm chưa đáp ứng đầy đủ (tính đến 02/11/2026)

| Yêu cầu | Trạng thái | Ghi chú |
| --- | --- | --- |
| Test tạo/duyệt trùng, đồng thời | **ĐẠT** | Tạo: tuần tự + song song; duyệt: tuần tự + song song + trigger DB |
| Test rollback | **ĐẠT** | Rollback khi insert thành viên lỗi và khi update phòng lỗi |
| Đổi giá chuẩn không đổi snapshot | **ĐẠT** | Snapshot chốt tại duyệt; đổi giá sau duyệt không ảnh hưởng |
| Truy cập hồ sơ/tệp sai quyền | **ĐẠT ở service/integration + UI** | Khách thuê/chủ trọ khác/Admin/Quản lý đúng phạm vi; U-03 smoke upload/download và Admin bị `AccessDenied` trên `/documents` |
| Ghi lỗi/người sửa/bước tái hiện | **ĐẠT** | Mục 3 và 4 |
| Admin bị chặn P2–P4 (D1–D17) | **ĐẠT** | A-01 → A-07, gồm UI hợp đồng |
| Không ghi dở dang / truy cập chéo | **ĐẠT** | C-04, C-06b, C-12, C-19, D-02/D-03/D-05/D-10 |
| Test lại trên bản chung (2D) | **CHƯA** | Mới retest trên branch 42 |
| Retest 2E trên commit ghép tuần 2 | **CHƯA** | Chưa có commit/merge |
| Bàn giao case/kết quả theo commit | **CHƯA** | Cần hash commit của bản sửa |
| UI P3 của Phúc (U-03) | **ĐẠT** | Smoke upload/download bằng `khachthue@nhatro.local`, Admin bị chặn |

**Kết luận:** tài liệu này **đáp ứng phần 2D về nội dung kiểm thử và sửa lỗi** (gồm UI hợp đồng và
UI giấy tờ), nhưng **chưa đáp ứng đầy đủ yêu cầu 2D–2E** ở khâu bàn giao theo commit và test lại trên
bản chung/2E.
