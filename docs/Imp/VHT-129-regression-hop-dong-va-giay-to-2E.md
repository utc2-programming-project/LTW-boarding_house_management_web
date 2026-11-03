# VHT-129 — Regression hợp đồng/phòng và giấy tờ trên bản ghép tuần 2 (đợt 2E)

> **Phạm vi:** đợt **2E** — chạy lại toàn bộ lỗi của đợt 2D và regression trên **commit ghép tuần 2**,
> xác nhận kết quả sau sửa cho hợp đồng/phòng (P4) và giấy tờ/hồ sơ (P3–P4).
> **Nội dung trọng tâm:** tạo/duyệt trùng, tạo/duyệt đồng thời, rollback, snapshot giá không đổi,
> truy cập hồ sơ/tệp sai quyền, ghi lỗi/người sửa, **Admin bị chặn trên P2–P4** theo bổ sung D1–D17 (12/10/2026).
> **Không làm:** không tạo migration, không mở rộng ngoài D1–D17, không merge nhánh ngoài phạm vi kiểm thử.
> **Nhánh:** `VHT-129-regression-tuần-2`.
> **Commit ghép tuần 2 (commit nền kiểm thử):** `951ab06` — merge PR #69 (`VHT-128`), mốc `dev` hiện tại.
> **Ngày kiểm thử:** 03/11/2026, giờ Việt Nam.
> **Nguồn đối chiếu:** `docs/task/cong_viec_theo_tuan.md` (VHT-42, VHT-129),
> `docs/task/timeline_song_song.md` (2D/2E), `docs/task/thao_tac_va_quyen.xlsx` (D1–D17),
> `docs/Imp/VHT-42-kiem-thu-hop-dong-va-giay-to-p4-2D.md`,
> `docs/VHT-9_trang_thai_ky_du_lieu_P4_P5.md`.

## 0. Kết luận nhanh

- **Regression 2E ĐẠT trên commit `951ab06`.** Toàn bộ lỗi 2D (BUG-2D-01 → BUG-2D-06) được chạy lại
  và **PASS**; không phát hiện lỗi mới trong phạm vi hợp đồng/phòng và giấy tờ.
- **Tạo trùng và tạo đồng thời:** chỉ một hợp đồng `Pending` được ghi cho cùng phòng/khoảng ngày;
  hai luồng song song chỉ một luồng thắng.
- **Duyệt trùng và duyệt đồng thời:** duyệt lại bị `InvalidStatus`; hai luồng duyệt chồng lấn chỉ một
  hợp đồng hiệu lực; trigger DB vẫn chặn ghi trực tiếp ngoài service.
- **Rollback:** insert thành viên lỗi và update phòng lỗi đều rollback cả transaction, không ghi dở.
- **Snapshot giá:** snapshot/cọc chốt tại thời điểm duyệt và **không đổi khi giá phòng đổi sau duyệt**.
- **Giấy tờ sai quyền:** khách thuê khác nhận 404; Chủ trọ khác và Quản lý chưa phân công bị chặn;
  Quản lý đã phân công xem được nhưng không xuất; Admin bị chặn ở cả service lẫn endpoint tệp.
- **Admin bị chặn P2–P4:** kiểm ở tầng service (P2/P3/P4) **và** tầng HTTP/UI thật trên bản ghép:
  Admin vào `/properties`, `/rooms`, `/tenants`, `/contracts`, `/contracts/create`, `/documents`
  đều bị chuyển `/Account/AccessDenied`.
- **Không còn case BLOCKED trong phạm vi 2D–2E.** Hạng mục còn lại chỉ là ghi nhận phối hợp
  (nhánh UI `VHT-124` chưa merge — không ảnh hưởng kết quả chức năng, xem mục 7).

## 1. Môi trường và bằng chứng chạy thật

| Thành phần | Giá trị |
| --- | --- |
| OS / shell | Windows, PowerShell |
| .NET SDK | 10.0.401 |
| Database test | SQL Server LocalDB; mỗi fixture dùng database riêng `NhaTro_It_<guid>` |
| Cấu hình test | `EnsureCreated` theo model hiện tại; trigger chống trùng được tạo/xóa trong test |
| Commit nền | `951ab06` (`dev`, merge PR #69) |
| Nhánh kiểm thử | `VHT-129-regression-tuần-2` |
| UI/HTTP | `WebAppFixture` + cookie đăng nhập thật, kiểm route qua HTTP (không dùng mock policy) |

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build NhaTro/NhaTro.slnx --nologo` | **0 error**, 2 warning (xUnit2031 có sẵn ở test P7, ngoài phạm vi P4) |
| `dotnet test NhaTro/NhaTro.UnitTests/NhaTro.UnitTests.csproj` | **648 pass, 0 fail, 0 skip** |
| `dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj` | **210 pass, 0 fail, 0 skip** |
| Lọc `ContractServiceIntegrationTests \| TenantDocumentScopeIntegrationTests \| ContractUiAccessTests` | **29/29 pass** |
| `dotnet test NhaTro/NhaTro.E2ETests/NhaTro.E2ETests.csproj` | **3 pass, 3 skip** — nhưng **không có case UI thật nào chạy**: 1 placeholder + 2 smoke auth trả về sớm vì app không chạy; 3 case UI phòng bị skip do thiếu `E2E_BASE_URL`/`E2E_PASSWORD`. Quyền/route được kiểm bù bằng HTTP integration bên dưới. |
| Test HTTP/UI mới của 2E | **2/2 pass** — `ContractUiAccessTests` |

> 2 test mới `ContractUiAccessTests` được bổ sung trong đợt 2E để kiểm **cổng route thật** của
> Admin trên P2–P4 và giấy tờ, thay vì chỉ dựa vào unit test policy. Không thay đổi code nghiệp vụ.

## 2. Chạy lại lỗi đợt 2D

| Mã lỗi | Nội dung | Test chạy lại trên `951ab06` | Kết quả | Người sửa |
| --- | --- | --- | --- | --- |
| BUG-2D-01 | Tạo hợp đồng không kiểm role/phạm vi | `CreateContractAsync_RejectsRolesOtherThanChuTroOrQuanLy` (Admin + Khách thuê), `CreateContractAsync_RejectsRoomOwnedByAnotherOwner` | **PASS** | Tính (VHT-29/VHT-31) |
| BUG-2D-02 | Tạo trùng hợp đồng `Pending` cùng phòng/khoảng ngày | `CreateContractAsync_RejectsDuplicatePendingContractForSameRoomAndPeriod`, `CreateContractAsync_ConcurrentDuplicatesOnlyPersistOne` | **PASS** | Tính (VHT-29/VHT-31) |
| BUG-2D-03 | Duyệt hợp đồng cho phòng đang `Rented` | `ApproveContractAsync_RejectsRentedRoomWithoutChangingIt` | **PASS** | Tính (VHT-30) |
| BUG-2D-04 | Quản lý được phân công vẫn bị deny-all giấy tờ | `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` (gán → xem được, bỏ gán → mất quyền) | **PASS** | Tính (VHT-24) + Phúc (VHT-26) |
| BUG-2D-05 | UI hợp đồng P4 chưa ghép | `ContractUiAccessTests` (route thật) + build Razor; kế thừa smoke UI 2D | **PASS** | Phú (VHT-31) + Đào (VHT-124) |
| BUG-2D-06 | Thiếu test “đổi giá sau duyệt không đổi snapshot” | `ApproveContractAsync_KeepsSnapshotWhenRoomPriceChangesAfterApproval` | **PASS** | Phát (VHT-42) |

## 3. Case trọng tâm đợt 2E

### 3.1. Tạo và duyệt trùng/đồng thời

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| R-01 | Tạo tuần tự hai hợp đồng `Pending` cùng phòng, cùng khoảng ngày | Chặn hợp đồng thứ hai | **PASS** | `CreateContractAsync_RejectsDuplicatePendingContractForSameRoomAndPeriod` |
| R-02 | Tạo đồng thời hai hợp đồng cùng phòng/khoảng ngày | Chỉ một hợp đồng + một thành viên được ghi | **PASS** | `CreateContractAsync_ConcurrentDuplicatesOnlyPersistOne` |
| R-03 | Tạo hai hợp đồng cùng phòng nhưng không chồng lấn | Cả hai được tạo (không chặn nhầm) | **PASS** | `CreateContractAsync_AllowsNonOverlappingPendingContracts` |
| R-04 | Duyệt lại/từ chối lại hợp đồng đã quyết định | `InvalidStatus`, không đổi dữ liệu | **PASS** | `ApproveContractAsync_DuplicateApprovalAndRejectionAreBlocked` |
| R-05 | Duyệt tuần tự hợp đồng chồng lấn | `ContractOverlap`, không đổi phòng | **PASS** | `ApproveContractAsync_RejectsOverlappingApprovedContract` |
| R-06 | Hai luồng duyệt chồng lấn đồng thời | Chỉ một luồng thắng | **PASS** | `ApproveContractAsync_ConcurrentOverlappingRequestsOnlyApproveOne` |
| R-07 | Ghi trực tiếp `Approved` bỏ qua service | Trigger DB chặn (SQL 51030) | **PASS** | `ContractsTable_RejectsOverlappingApprovedContractOutsideService` |

### 3.2. Rollback và snapshot giá

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| R-08 | Insert thành viên lỗi giữa transaction tạo hợp đồng | Rollback cả hợp đồng, không ghi dở | **PASS** | `CreateContractAsync_RollsBackContractWhenMemberInsertFails` |
| R-09 | Update phòng lỗi khi duyệt | Rollback cả hợp đồng và phòng | **PASS** | `ApproveContractAsync_RollsBackContractWhenRoomUpdateFails` |
| R-10 | Đổi giá phòng giữa lúc tạo và lúc duyệt | Snapshot/cọc chốt lại theo giá tại thời điểm duyệt | **PASS** | `ApproveContractAsync_SnapshotsCurrentRoomPriceAndDeposit` |
| R-11 | Đổi giá phòng **sau khi đã duyệt** | Snapshot đã chốt không đổi | **PASS** | `ApproveContractAsync_KeepsSnapshotWhenRoomPriceChangesAfterApproval` |
| R-12 | Duyệt hợp đồng của nhà đã xóa mềm / phòng bảo trì / phòng `Rented` | Bị chặn, giữ nguyên trạng thái | **PASS** | `ApproveContractAsync_RejectsContractForSoftDeletedProperty`, `...RejectsRoomUnderMaintenanceWithoutChangingIt`, `...RejectsRentedRoomWithoutChangingIt` |

### 3.3. Truy cập hồ sơ/tệp sai quyền

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| R-13 | Khách thuê A xem/tải giấy tờ hồ sơ A | Thành công | **PASS** | `Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung` |
| R-14 | Khách thuê B biết `fileId` của A | 404 (`null`), không lộ tồn tại | **PASS** | cùng test; `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` |
| R-15 | Chủ trọ xem/tải giấy tờ khách thuộc nhà mình | Thành công | **PASS** | `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` |
| R-16 | Chủ trọ khác truy cập hồ sơ ngoài nhà mình | `None`/404 | **PASS** | cùng test (Chủ trọ `chu-tro-2` nhận `None`) |
| R-17 | Quản lý chưa phân công / đã bỏ phân công | Bị từ chối ngay | **PASS** | `Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so` |
| R-18 | Quản lý đã phân công tòa nhà | Xem được, `CanExport = false` | **PASS** | cùng test, dùng `ContractManagerTenantScope` của production |
| R-19 | Upload/xóa tệp vào hồ sơ khách khác | `Forbidden` | **PASS** | `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` |
| R-20 | Thay/xóa mềm giấy tờ và audit truy cập | Chỉ bản mới tải được; audit không chứa tên/nội dung tệp | **PASS** | `Service_P3_gan_metadata_thay_va_xoa_dung_ho_so`, `Tai_giay_to_dung_pham_vi_va_ghi_audit_khong_noi_dung` |

### 3.4. Admin bị chặn P2–P4 (bổ sung D1–D17)

| Mã | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| R-21 | Admin gọi service P2 (nhà/phòng) | `UnauthorizedAccessException` | **PASS** | `Admin_bi_chan_o_moi_service_nghiep_vu_P2_P3` |
| R-22 | Admin gọi service P3 (khách thuê, hồ sơ) | `UnauthorizedAccessException` | **PASS** | cùng test |
| R-23 | Admin gọi storage (upload/download/list/delete) | Bị chặn | **PASS** | `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` |
| R-24 | Admin tạo/duyệt/list/get hợp đồng P4 ở service | Bị chặn | **PASS** | `CreateContractAsync_RejectsRolesOtherThanChuTroOrQuanLy`, `ApproveContractAsync_RejectsRolesOtherThanChuTro`, `ListAndGetAsync_ApplyOwnerManagerTenantAndAdminScopes` |
| R-25 | Admin vào `/properties`, `/rooms`, `/tenants`, `/contracts`, `/contracts/create`, `/documents` | Chuyển `/Account/AccessDenied` | **PASS (HTTP/UI)** | `ContractUiAccessTests.Admin_bi_chan_UI_P2_P4_tren_ban_ghep` |
| R-26 | Chủ trọ vào `/contracts`, `/contracts/create`; không vào `/documents` | Đúng quyền | **PASS (HTTP/UI)** | `ContractUiAccessTests.ChuTro_vao_hop_dong_nhung_khong_vao_giay_to` |

### 3.5. Regression chung trên bản ghép

| Hạng mục | Kết quả |
| --- | --- |
| Build toàn solution | **PASS** — 0 error |
| Unit tests | **648/648 pass** |
| Integration tests | **210/210 pass** |
| Nhóm hợp đồng + giấy tờ + UI access | **29/29 pass** |
| E2E hiện có | **3 pass, 3 skip** — 3 pass không phải kiểm UI thật (1 placeholder + 2 smoke auth trả về sớm do app chưa chạy); 3 case UI phòng skip vì thiếu env. Không phải fail, nhưng **không dùng làm bằng chứng UI trong 2E**. |
| Hợp đồng `Pending` trùng còn trong DB sau test | **Không** — test khẳng định chỉ một bản ghi |
| Dữ liệu ghi dở sau rollback | **Không** — test khẳng định cả hợp đồng/phòng giữ nguyên |

## 4. Lỗi và người sửa

**Không phát hiện lỗi mới trong 2E.** Toàn bộ lỗi 2D đã được xác nhận sửa trên commit ghép:

| Mã | Mức | Trạng thái trên `951ab06` | Người sửa | Xác nhận |
| --- | --- | --- | --- | --- |
| BUG-2D-01 | Cao | Đã sửa | Tính | Test âm PASS |
| BUG-2D-02 | Cao | Đã sửa | Tính | Test tuần tự + đồng thời PASS |
| BUG-2D-03 | Trung bình | Đã sửa | Tính | Test phòng `Rented` PASS |
| BUG-2D-04 | Cao | Đã sửa (thay bằng `ContractManagerTenantScope` sau VHT-119) | Tính + Phúc | Test phân công/bỏ phân công PASS |
| BUG-2D-05 | Blocker | Đã sửa | Phú + Đào | Route HTTP + build UI PASS |
| BUG-2D-06 | Thấp (test) | Đã bổ sung | Phát | Test snapshot sau duyệt PASS |

### Ghi nhận phối hợp (không phải lỗi chức năng)

| Mã | Nội dung | Người phụ trách | Trạng thái |
| --- | --- | --- | --- |
| OBS-2E-01 | Nhánh `VHT-124` (dialog duyệt hợp đồng dùng `<dialog>`, focus trap/Esc) **chưa có trong `dev` tại `951ab06`**; UI hiện tại vẫn là modal `div role="dialog"` đã kiểm ở 2D. Không ảnh hưởng luồng tạo/duyệt. | Đào (VHT-124), Phú (ghép) | Theo dõi trước release; không chặn 2E |
| OBS-2E-02 | 2D dùng `PropertyManagerTenantScope`, bản ghép tuần 2 dùng `ContractManagerTenantScope` (VHT-119). Hành vi D9 vẫn đạt; tài liệu 2D đã được cập nhật lại theo bản ghép. | Tính (VHT-119) | Đã đối chiếu, PASS |
| OBS-2E-03 | E2E UI phòng tự skip khi app không chạy; 2E dùng HTTP integration + smoke UI 2D thay thế. | Phát / Phú | Ghi nhận, không chặn |

## 5. Bàn giao đợt 2E

| Hạng mục | Nội dung |
| --- | --- |
| Commit kiểm thử | `951ab06` — merge PR #69, nhánh `dev` |
| Nhánh ghi kết quả | `VHT-129-regression-tuần-2` |
| Case/kết quả | Mục 2 và mục 3 (R-01 → R-26) |
| Lỗi | Không có lỗi mới; BUG-2D-01 → 06 đã xác nhận sửa |
| Người sửa | Tính (P2/P4), Phúc (P3 UI), Phú (ghép), Đào (UI hợp đồng), Phát (test) |
| Điều phối test lại | Phát — tổng hợp kết quả, ngày 03/11/2026 |
| Test bổ sung trong 2E | `NhaTro/NhaTro.IntegrationTests/Features/Contracts/ContractUiAccessTests.cs` (2 case HTTP/UI) |

## 6. Tiêu chí hoàn thành

| Tiêu chí | Kết quả | Bằng chứng |
| --- | --- | --- |
| [2D] Test lại lỗi trên bản chung | **ĐẠT** | BUG-2D-01 → 06 chạy lại trên `951ab06` |
| [2D] Không ghi dở dang | **ĐẠT** | R-08, R-09 |
| [2D] Không truy cập chéo | **ĐẠT** | R-14 → R-19, R-21 → R-25 |
| [2E] Test tạo/duyệt trùng, đồng thời | **ĐẠT** | R-01 → R-07 |
| [2E] Test rollback | **ĐẠT** | R-08, R-09 |
| [2E] Đổi giá chuẩn không đổi snapshot | **ĐẠT** | R-10, R-11 |
| [2E] Giấy tờ sai quyền | **ĐẠT** | R-13 → R-20 |
| [2E] Ghi lỗi/người sửa/bước tái hiện | **ĐẠT** | Mục 2, 4; chi tiết tái hiện ở tài liệu 2D |
| [2E] Admin bị chặn P2–P4 | **ĐẠT** | R-21 → R-25 |
| [2E] Regression trên commit ghép tuần 2 | **ĐẠT** | Build + 648 unit + 210 integration + 29 targeted |

## 7. Giới hạn và điều kiện còn lại

| Hạng mục | Trạng thái | Ghi chú |
| --- | --- | --- |
| Chạy trên SQL Server/IIS thật thay LocalDB | Chưa | Nằm trong phạm vi VHT-130/VHT-44 (publish tuần 2). LocalDB dùng cùng provider SQL Server, trigger và `UPDLOCK/HOLDLOCK` đã chạy thật trong test |
| Smoke UI 4 vai trò bằng trình duyệt trên `951ab06` | Chưa chạy lại | Đã có smoke UI 2D; 2E bổ sung HTTP/UI gate cho 4 route. Đề nghị VHT-130 smoke lại trên bản publish |
| VHT-124 (dialog hợp đồng) | Chưa merge vào `dev` | OBS-2E-01; không ảnh hưởng kết quả chức năng 2E |
| E2E 3 case UI phòng | Skip | Thiếu `E2E_BASE_URL`/`E2E_PASSWORD` và app chưa chạy. 2 smoke auth cũng trả về sớm, nên **không coi E2E là bằng chứng UI của 2E**; thay bằng HTTP integration + smoke UI 2D. |
| Rủi ro còn lại | Thấp | Không có lỗi mở trong phạm vi P2–P4/giấy tờ trên commit kiểm thử |

## 8. File thay đổi trong nhánh

| File | Việc |
| --- | --- |
| `NhaTro/NhaTro.IntegrationTests/Features/Contracts/ContractUiAccessTests.cs` | Thêm 2 test HTTP/UI: Admin bị chặn P2–P4 + giấy tờ; Chủ trọ đúng quyền hợp đồng/giấy tờ |
| `docs/Imp/VHT-129-regression-hop-dong-va-giay-to-2E.md` | Tài liệu case/kết quả/người sửa/xác nhận của đợt 2E |
| `docs/Imp/VHT-42-kiem-thu-hop-dong-va-giay-to-p4-2D.md` | Cập nhật trạng thái 2D: fixes đã merge, 2E đã chạy trên `951ab06` |

Không sửa code nghiệp vụ, không tạo migration, không đổi schema.
