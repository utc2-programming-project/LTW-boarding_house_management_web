# VHT-22 — Kiểm thử P1–P3 và ghi tài liệu

> **Nhánh:** `VHT-22-kiem-thu-p-1-p-3-va-ghi-tai-lieu`, tạo từ `origin/dev`.
> **Commit nền kiểm thử:** `69e0cd9` — merge PR #24, mốc `dev` trước khi thêm test VHT-22.
> **Ngày kiểm thử:** 19/10/2026, giờ Việt Nam.
> **Phạm vi:** Sprint 1D — test 4 vai trò, Admin bị chặn nghiệp vụ, tòa nhà khác, validation và tải tệp trái quyền; ghi kết quả, bước tái hiện và người sửa.
> **Không làm:** không sửa nghiệp vụ P1–P3, không tạo migration, không thay DTO/service, không merge/push.

Tài liệu này kế thừa [kế hoạch tuần](../task/cong_viec_theo_tuan.md), [timeline](../task/timeline_song_song.md) và [ma trận quyền](../task/thao_tac_va_quyen.xlsx). Mã nguồn P1–P3 dưới test **không đổi**; phần thêm mới chỉ là test scaffold và tài liệu.

## 1. Môi trường và cách chạy

| Thành phần | Giá trị |
| --- | --- |
| OS | Windows, PowerShell |
| .NET SDK | 10.0.401 |
| Database test | SQL Server LocalDB, database riêng `NhaTro_It_<guid>` cho mỗi lần chạy |
| Cấu hình test | `EnsureCreated` theo model hiện tại, không dùng database demo |
| Trình duyệt/UI | P1 đã smoke trên worktree tạm ghép VHT-8 + VHT-14; UI P2/P3 chưa có trên bất kỳ nhánh remote nào; xem mục 5 |

```powershell
# Toàn bộ test
dotnet test NhaTro/NhaTro.slnx

# Chỉ case VHT-22
dotnet test NhaTro/NhaTro.IntegrationTests/NhaTro.IntegrationTests.csproj `
  --filter "FullyQualifiedName~P1P3RoleAccessTests"
```

Nếu máy không có LocalDB, các test SQL được đánh dấu `Skip` qua `[SqlServerFact]`; kết quả xanh khi đó **không** chứng minh P1–P3 đã chạy.

## 2. Bảng case và kết quả

Ký hiệu: **PASS** = service thật trả đúng kết quả; **BLOCKED** = thiếu đầu vào/UI/service nên chưa kết luận được; **CONDITIONAL** = code hiện tại chạy đúng nhưng chưa đáp ứng yêu cầu phạm vi tòa nhà.

| Mã | Vai trò | Case | Kỳ vọng | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- | --- |
| P1-01 | Admin | Admin gọi P2 (nhà/phòng) | Bị từ chối tại service | **PASS** | `Admin_bi_chan_o_moi_service_nghiep_vu_P2_P3` |
| P1-02 | Admin | Admin gọi P3 (khách thuê) | Bị từ chối tại service | **PASS** | cùng test trên |
| P1-03 | Admin | Admin gọi storage nghiệp vụ | Bị từ chối tại `BusinessGuard` | **PASS** | `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` — đã kiểm `Upload`, `Download`, `List`, `Delete` |
| P1-04 | Quản lý | Quản lý đổi vai trò tài khoản | Bị từ chối | **PASS** | `P1_chi_admin_doi_vai_tro_va_quan_ly_khong_tu_nang_quyen` |
| P1-05 | Admin | Admin đổi vai trò người khác | Thành công | **PASS** | cùng test trên |
| P1-06 | Admin | Admin tự đổi vai trò mình | Bị từ chối | **PASS** | cùng test trên |
| P1-07 | Quản lý | Quản lý tạo tài khoản ngoài tòa nhà được giao | Bị từ chối | **BLOCKED** | Đã quét mọi nhánh remote và PR ref: VHT-8 có `AccountService` nhưng không có hàm tạo tài khoản; VHT-95 mới bổ sung; xem B-22-01 |
| P2-01 | Chủ trọ A | Xem nhà/phòng của mình | Chỉ thấy dữ liệu của A | **PASS** | `Bon_vai_tro_chi_thay_du_lieu_dung_pham_vi_P1_P3` |
| P2-02 | Chủ trọ A | Đọc nhà/phòng của chủ trọ B | `PropertyNotFound` / `RoomNotFound` | **PASS** | cùng test trên |
| P2-02b | Chủ trọ A | Sửa/xóa/khôi phục ID nhà/phòng của chủ trọ B | `PropertyNotFound` / `RoomNotFound` | **PASS** | cùng test trên — đã kiểm `UpdateAsync` nhà, `Update/Delete/RestoreAsync` phòng |
| P2-03 | Quản lý | Xem P2 | Chỉ thấy dữ liệu P2 thuộc tòa nhà được giao | **CONDITIONAL** | Test xác nhận Quản lý thấy cả 2 tòa nhà; chờ VHT-24, xem B-22-03 |
| P2-04 | Khách thuê | Xem danh sách P2 | Bị từ chối | **PASS** | cùng test trên |
| P2-05 | Chủ trọ | Tạo nhà thiếu tên | `InvalidRequest` | **PASS** | `Validation_P2_P3_chan_du_lieu_thieu_va_trung` |
| P2-06 | Chủ trọ | Tạo phòng giá âm | `InvalidRequest` | **PASS** | cùng test trên |
| P2-07 | Chủ trọ | Tạo phòng trùng mã trong cùng nhà | `DuplicateRoomCode` | **PASS** | cùng test trên |
| P3-01 | Admin | Danh sách khách thuê | Bị từ chối | **PASS** | `Admin_bi_chan_o_moi_service_nghiep_vu_P2_P3` |
| P3-02 | Khách thuê | Danh sách khách thuê | Bị từ chối | **PASS** | cùng test trên |
| P3-03 | Khách thuê A | Xem hồ sơ của mình | Thành công | **PASS** | cùng test trên |
| P3-04 | Khách thuê A | Xem hồ sơ khách B | `TenantNotFound` | **PASS** | cùng test trên |
| P3-05 | Quản lý | Tạo khách thiếu/sai số điện thoại | `InvalidRequest` | **PASS** | `Validation_P2_P3_chan_du_lieu_thieu_va_trung` |
| P3-06 | Quản lý | Tạo khách trùng CCCD | `DuplicateTenant` | **PASS** | cùng test trên |
| P3-07 | Quản lý | Xem khách của tòa nhà khác | Chỉ thấy khách thuộc tòa nhà được giao | **CONDITIONAL** | Code hiện tại trả toàn bộ; chờ VHT-24, xem B-22-03 |
| ST-01 | Khách thuê | Upload ảnh phòng | `Forbidden` | **PASS** | `Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh` |
| ST-02 | Khách thuê A | Upload giấy tờ vào hồ sơ khách B | `Forbidden` | **PASS** | cùng test trên |
| ST-03 | Khách thuê A | Upload giấy tờ vào hồ sơ của mình | Thành công | **PASS** | cùng test trên |
| ST-04 | Khách thuê A | Tải tệp của mình | Thành công | **PASS** | cùng test trên |
| ST-05 | Khách thuê B | Tải tệp của khách A | `null` (endpoint trả 404) | **PASS** | cùng test trên |
| UI-01 | 4 vai trò | Đăng nhập và thao tác P1–P3 trên UI | Đúng quyền, đúng phạm vi | **BLOCKED** | P1 login/profile đã smoke đạt trên worktree ghép VHT-8 + VHT-14; UI P2/P3 chưa có trên remote; xem B-22-02, B-22-06 |

### Smoke P1 UI trên worktree ghép (không thuộc nhánh VHT-22)

Worktree tạm local merge `origin/VHT-8` (`8b43f22`) với `origin/VHT-14` (`9dd3aca`); chỉ dùng để kiểm P1, không đưa vào nhánh VHT-22 và không push.

| Case | Kết quả | Ghi chú |
| --- | --- | --- |
| Trang login trả 200, có form và antiforgery token | **PASS** | Chạy HTTP thật qua `WebAppFixture` |
| Admin đăng nhập đúng, thấy avatar/đăng xuất | **PASS** | Cookie Identity hợp lệ |
| Sai mật khẩu hiện “Email hoặc mật khẩu không đúng.” | **PASS** | Phải `WebUtility.HtmlDecode` HTML trước khi so chuỗi; Razor mã hóa tiếng Việt thành numeric entity |
| 4 vai trò vào `/profile` | **PASS** | 4/4 vai trò trả 200 |
| Admin vào `/admin/users` | **PASS** | Trang quản trị tài khoản trả 200 |
| Quản lý/Khách thuê vào `/admin/users` | **PASS** | Bị chuyển `/Account/AccessDenied` |
| Chủ trọ vào `/admin/users` | **FAIL (B-22-06)** | Cũng bị chuyển `/Account/AccessDenied`, trái D1 và `AccountPermissionRules.CanManageAccounts` |
| Khách vãng lai vào `/admin/users` | **PASS** | Bị chuyển `/Account/Login?ReturnUrl=...` |

Tổng: 10/10 kịch bản chạy xong; 9 đạt kỳ vọng, 1 phát hiện sai phạm vi (Chủ trọ).

### Case bổ sung D1–D17 (12/10/2026)

| Case | Kết quả | Ghi chú |
| --- | --- | --- |
| Admin gọi mọi service nghiệp vụ P2–P3 phải bị từ chối | **PASS** | Đã kiểm đủ 21/21 method P2/P3, gồm `TenantService.UpdateOwnAsync`; storage đã kiểm `Upload`, `Download`, `List`, `Delete`. |
| Quản lý tạo tài khoản ngoài tòa nhà được giao bị từ chối | **BLOCKED** | Đã kiểm VHT-8: `IAccountService` không có hàm tạo tài khoản và UI không có form tạo; VHT-95 mới bổ sung. Xem B-22-01. |

## 3. Danh sách lỗi, thiếu đầu vào và người sửa

| Mã | Mức | Mô tả | Bước tái hiện | Người nhận | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| B-22-01 | Blocker | Chưa có service tạo tài khoản P1 và phân công tài khoản theo tòa nhà, nên case “Quản lý tạo tài khoản ngoài tòa nhà bị từ chối” chưa kiểm được. | Đã quét mọi remote head và PR ref: `origin/VHT-8` (`8b43f22`) có `Features/Accounts/AccountService.cs` nhưng `IAccountService` chỉ có List/GetMyProfile/UpdateMyProfile/ChangeRole/Lock/Unlock, **không có Create**; UI chỉ có login, profile, danh sách tài khoản, không có form tạo. `git grep -i CreateAccount` và `CreateUserAsync` trên tất cả ref không có kết quả. VHT-95 mới là task vòng đời tài khoản. | Tính (VHT-95 backend), Phú (UI) | Chờ VHT-95; **merge VHT-8 một mình không đủ** để retest |
| B-22-02 | Blocker | UI P2/P3 chưa có trên `dev` hay bất kỳ nhánh remote nào, nên chưa chạy được case UI 4 vai trò, tòa nhà khác và tải tệp trên trình duyệt. | Đã quét `refs/remotes/origin/*` và 24 PR ref (`refs/pull/*/head`): chỉ VHT-8 có trang P1 (`Accounts/AccountManagement`, `Accounts/UserProfile`); VHT-7/VHT-23 chỉ có service P2/P3; VHT-115 là component demo. Không ref nào có `Components/Pages/Properties`, `Rooms` hoặc `Tenants`. | Phúc (UI P2/P3), Phú (ghép) | Chờ VHT-16/VHT-17; test lại trên bản ghép 1D |
| B-22-03 | High (chờ quyết định) | Phạm vi Quản lý theo tòa nhà chưa được triển khai: `TenantService.ScopeQuery` trả toàn bộ khách; `PropertyService.ScopeQuery` cho Quản lý thấy toàn bộ nhà. Đây là hành vi có chủ đích đang chờ VHT-24, không phải lỗi mới của VHT-22. | Đăng nhập Quản lý, gọi `PropertyService.ListAsync`/`TenantService.ListAsync`: thấy dữ liệu của cả hai tòa nhà. | Tính (VHT-24) | Chưa sửa; test âm tính sẽ bổ sung sau VHT-24 |
| B-22-04 | Medium (test scaffold) | `SqlServerFixture.ResetAsync` xóa `AspNetUsers` trước `StoredFiles`, gây lỗi FK khi test upload tệp. | Chạy test upload tệp lần thứ hai trên cùng fixture: `DELETE` vi phạm `FK_StoredFiles_AspNetUsers_UploadedByUserId`. | Phát (test scaffold) | **Đã sửa** trong nhánh: xóa `StoredFiles` trước `Users`; integration test chạy lại 35/35 đạt |
| B-22-05 | Low (test scaffold) | `SeedAccountAsync` tạo role trực tiếp nhưng không điền `NormalizedName`, làm `UserManager.GetUsersInRoleAsync` không thấy Admin. | Seed role rồi gọi `RoleService.ChangeRoleAsync` với Admin: guard “Admin cuối cùng” không hoạt động đúng. | Phát (test scaffold) | **Đã sửa** trong nhánh: điền `NormalizedName`; test P1 đạt |
| B-22-06 | High (phát hiện trên nhánh chưa ghép) | Trên VHT-8, `/admin/users` có `[Authorize(Roles = Roles.Admin)]` và menu chỉ hiện cho Admin, nhưng D1 và `AccountPermissionRules.CanManageAccounts` cho Chủ trọ quản lý tài khoản Quản lý/Khách thuê. Chủ trọ bị chuyển sang AccessDenied. | Worktree ghép VHT-8 `8b43f22` + VHT-14 `9dd3aca`: đăng nhập `chutro.a@test.local`, gọi `/admin/users` → 302 `/Account/AccessDenied`; `CanManageAccounts(Roles.ChuTro)` trả `true`. | Tính (VHT-8/VHT-95), Phú (UI) | Chờ xác nhận: nếu VHT-95 tách màn hình riêng cho Chủ trọ thì ghi rõ; nếu không, sửa quyền/menu trước khi merge VHT-8 |

> B-22-01/B-22-02/B-22-03 là **thiếu đầu vào/thiếu phạm vi**, chưa phải lỗi nghiệp vụ đã tái hiện. B-22-06 là lỗi phạm vi quyền đã tái hiện trên nhánh VHT-8 chưa ghép. Không đánh dấu các case này là PASS.

## 4. Kết quả chạy và xác nhận test lại

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build NhaTro/NhaTro.slnx` | 0 warning, 0 error |
| `dotnet test NhaTro/NhaTro.IntegrationTests` | **35/35 đạt**, 0 fail, 0 skip (30 test cũ + 5 test VHT-22) |
| `dotnet test NhaTro/NhaTro.UnitTests` | **191/191 đạt** (không đổi so với nền `dev`) |
| `dotnet test NhaTro/NhaTro.E2ETests` | **1/1 đạt** — chỉ là scaffold rỗng, không tính nghiệm thu UI |
| `dotnet ef migrations has-pending-model-changes` | Không có thay đổi model |

Tổng toàn solution: **227/227 đạt** (191 unit + 35 integration + 1 E2E scaffold).

Smoke P1 UI trên worktree tạm (VHT-8 `8b43f22` + VHT-14 `9dd3aca`): **10/10 kịch bản chạy xong**, trong đó 9 đạt kỳ vọng và 1 phát hiện sai phạm vi (Chủ trọ bị chặn khỏi `/admin/users`, B-22-06). Kết quả này **không** nằm trong 227 test của nhánh VHT-22 vì VHT-8/VHT-14 chưa ghép.

Xác nhận sau khi sửa B-22-04/B-22-05 và bổ sung 19/10 (`UpdateOwnAsync`, storage `Download/List/Delete`, ghi chéo P2): chạy lại riêng `P1P3RoleAccessTests` **5/5 đạt**, sau đó chạy toàn bộ integration **35/35 đạt**. Các case **BLOCKED** chưa có kết quả retest; sẽ cập nhật sau khi VHT-95/VHT-16/VHT-17/VHT-24 được ghép.

## 5. Phần chưa kiểm được và điều kiện kiểm lại

- **Rà soát nhánh remote (19/10/2026):** đã fetch `refs/remotes/origin/*` và toàn bộ `refs/pull/*/head` (24 PR). Chỉ VHT-8 có UI P1; VHT-7/VHT-23 là service P2/P3; VHT-115 là component demo. Không nhánh/PR nào có UI P2/P3 hoặc hàm tạo tài khoản P1.
- **UI P1–P3:** P1 đã smoke trên worktree tạm VHT-8 + VHT-14: login 4 vai trò, profile 4 vai trò, Admin `/admin/users`, chặn Quản lý/Khách thuê/anonymous đều đạt; Chủ trọ bị chặn trái D1 (B-22-06). P2/P3 chưa có UI nên UI-01 vẫn **BLOCKED**. VHT-14 `AuthFlowTests` cần `WebUtility.HtmlDecode` trước khi so chuỗi tiếng Việt (Razor mã hóa numeric entity), nếu không sẽ false-fail.
- **Tạo tài khoản ngoài tòa nhà:** VHT-8 chỉ có quản trị tài khoản (list/profile/role/lock), **không có Create**. Không thể retest chỉ bằng cách merge VHT-8; cần VHT-95 bổ sung service + UI tạo tài khoản. Khi có, kiểm: Quản lý tạo tài khoản cho tòa nhà không được giao phải bị từ chối; Admin tạo được; kiểm audit/không lộ tài khoản.
- **Phạm vi Quản lý theo tòa nhà:** chờ VHT-24. Khi có bảng phân công, bổ sung case Quản lý A không thấy nhà/khách của Quản lý B và không sửa được dữ liệu ngoài phạm vi.
- **Tải tệp:** đã kiểm service với storage giả lập trong bộ nhớ; chưa kiểm endpoint HTTP/`multipart` thật và chưa kiểm dung lượng/loại tệp trên UI. Các phần này thuộc VHT-13/VHT-14 và test UI sau ghép.
- **Công tắc Quản lý (SW-01…SW-16):** VHT-22 không kiểm; `RoomChangePolicyStub` hiện dùng cho P2. Cần VHT-94 và test 3 chế độ trước khi kết luận P2/P3 đã đủ quyền.

## 6. File thay đổi trong nhánh

| File | Việc |
| --- | --- |
| `NhaTro/NhaTro.IntegrationTests/Features/TestReadiness/P1P3RoleAccessTests.cs` | 5 test P1–P3: ma trận 4 vai trò, Admin chặn toàn bộ P2/P3, validation, đổi vai trò, tải tệp trái quyền |
| `NhaTro/NhaTro.IntegrationTests/Infrastructure/SqlServerFixture.cs` | Xóa `StoredFiles` trước `Users`; điền `NormalizedName` khi seed role |
| `docs/Imp/VHT-22-kiem-thu-p-1-p-3-va-ghi-tai-lieu.md` | Tài liệu case/kết quả/lỗi/người sửa/retest |

## 7. Kết luận mức đạt

- **Đạt:** case cấm của Admin, Khách thuê, Chủ trọ khác tòa nhà, validation P2/P3 và tải tệp trái quyền đã bị **service thật** chặn; có test tự động và kết quả chạy lại. P1 UI smoke đạt 9/10 kỳ vọng trên worktree ghép VHT-8 + VHT-14.
- **Chưa đạt / còn chờ:** case Quản lý tạo tài khoản ngoài tòa nhà chờ VHT-95 (VHT-8 không có hàm tạo); UI P2/P3 chờ VHT-16/VHT-17; Chủ trọ bị chặn khỏi `/admin/users` trên VHT-8 (B-22-06); phạm vi Quản lý theo tòa nhà chờ VHT-24. Đã giao người nhận và điều kiện retest tại mục 3 và 5.
- **Kết luận:** VHT-22 **chưa thể đóng hoàn toàn** cho tới khi các mục BLOCKED có bản ghép và được chạy lại; phần service P1–P3 trong phạm vi hiện có đã đạt.
