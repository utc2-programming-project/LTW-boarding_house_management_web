# Checklist review P4 — Hợp đồng (Sprint 2C–2D)

Tài liệu review do agent phân tích lập. Không phải đặc tả mới, không thay đổi code.
Dùng để đối chiếu từng diff/commit của Codex.

- Repo chuẩn: `LTW-boarding_house_management_web` / project `NhaTro`
- Baseline lập checklist: nhánh `VHT-31-ghep-tao-va-duyet-hop-dong-p4` @ `d9c421b` (bằng `dev`, cây sạch)
- Build baseline: PASS, 0 warning / 0 error
- Test SQL: CHƯA XÁC MINH (không có SQL Server trong phiên khảo sát)

## 0. Phạm vi và quyết định đã chốt

| Mã | Quyết định |
|---|---|
| S-1 | Phạm vi 2C–2D: **list / detail / create / approve**, ghép UI với service SQL, phân quyền, VI/EN |
| S-2 | Gia hạn / thanh lý theo phạm vi **VHT-50**, không mở rộng trong 2C–2D |
| S-3 | **Giữ nguyên giá trị enum DB** (`Pending=0, Approved=1, Rejected=2`) khi chưa có quyết định nghiệp vụ + migration được duyệt |
| S-4 | Phân biệt **trạng thái lưu DB** và **trạng thái hiển thị suy ra từ ngày**; không thêm enum/cột chỉ để có badge |
| S-5 | "Sắp trống" phân tích từ dữ liệu hợp đồng + khoảng thời gian; **không thêm enum/cột/migration** nếu chưa chứng minh cần |
| S-6 | Không tự sửa code thay Codex; chỉ báo lỗi + cách sửa |

### Thang mức độ

| Mức | Ý nghĩa |
|---|---|
| P0 | Sai nghiệp vụ/phân quyền, mất dữ liệu, hoặc chặn nghiệm thu sprint |
| P1 | Sai logic/biên, thiếu trường bắt buộc, lỗi hiển thị song ngữ |
| P2 | Chất lượng code, nhất quán, hiệu năng, khả năng bảo trì |
| NIT | Góp ý nhỏ, không bắt buộc sửa |

### Trạng thái đánh giá

`PASS` / `FAIL` / `SKIP` (ngoài phạm vi) / `CHƯA XÁC MINH` (cần chạy DB hoặc cần người quyết định)

---

## A. Phân quyền

Nguồn chuẩn: `docs/task/thao_tac_va_quyen.xlsx` dòng #22–#27, D1–D17.

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| A-01 | Xem danh sách HĐ — Chủ trọ | Thấy toàn bộ HĐ thuộc sở hữu | P0 | CHƯA XÁC MINH (chưa có query) |
| A-02 | Xem danh sách HĐ — Quản lý | Chỉ HĐ thuộc toà được gán | P0 | CHƯA XÁC MINH |
| A-03 | Xem danh sách HĐ — Khách thuê | Chỉ HĐ có mình là member + thông tin cọc | P0 | CHƯA XÁC MINH |
| A-04 | Xem danh sách HĐ — Admin | Không có quyền nghiệp vụ | P0 | CHƯA XÁC MINH |
| A-05 | Xem chi tiết HĐ — mọi vai trò | Cùng quy tắc như A-01..A-04; HĐ ngoài phạm vi trả 403/404, không lộ dữ liệu | P0 | CHƯA XÁC MINH |
| A-06 | Tạo HĐ — Chủ trọ | Được tạo, kết quả `Pending` | P0 | FAIL — `ContractService.CreateContractAsync` không nhận caller |
| A-07 | Tạo HĐ — Quản lý | Được tạo trong toà được gán, kết quả `Pending` | P0 | FAIL — không kiểm scope toà |
| A-08 | Tạo HĐ — Khách thuê / Admin | Bị chặn ở cả route lẫn service | P0 | CHƯA XÁC MINH (service không chặn) |
| A-09 | Duyệt / từ chối — Chủ trọ | Chỉ Chủ trọ; được tự tạo rồi tự duyệt (D4) | P0 | PASS (đã có test) |
| A-10 | Duyệt / từ chối — Quản lý | Luôn bị chặn, kể cả HĐ mình tạo | P0 | PASS — `ContractService.cs:29-34` |
| A-11 | Duyệt HĐ ngoài sở hữu | `ContractAuthorizationException` | P0 | PASS — `ContractService.cs:111-116` |
| A-12 | Ẩn/hiện menu, nút theo vai trò | `AuthorizeView` khớp ma trận quyền | P1 | CHƯA XÁC MINH |
| A-13 | Không tin dữ liệu client | `RoomId`/`TenantIds` phải được kiểm lại ở service | P0 | FAIL — create chỉ kiểm tồn tại, không kiểm quan hệ toà–phòng–khách |

Ghi chú A-13: hiện `CreateContractAsync` cho phép ghép khách thuộc toà khác vào HĐ của phòng, nếu không kiểm `Tenant`–`Property`. Cần xác nhận có ràng buộc nghiệp vụ này không trước khi bắt lỗi.

## B. Truy vấn list / detail

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| B-01 | Tồn tại method list + detail trong `IContractService` | Có, nhận `PropertyCaller` + `CancellationToken` | P0 | FAIL — chỉ có create/approve |
| B-02 | DTO list đủ trường hiển thị | Mã HĐ, phòng, toà, khách đại diện, số member, start/end, giá, cọc, trạng thái | P1 | FAIL — `ContractResponseDto` thiếu tên phòng/toà/khách |
| B-03 | DTO detail đủ trường | Thêm danh sách thành viên, lý do từ chối, người duyệt/ngày duyệt (nếu có dữ liệu), trạng thái cọc | P1 | FAIL |
| B-04 | Lọc theo trạng thái + toà + từ khoá | Tham số hoá, không nối chuỗi SQL | P1 | CHƯA XÁC MINH |
| B-05 | Phân trang/sắp xếp ổn định | Có `OrderBy` xác định (ví dụ `StartDate desc, ContractId`) | P2 | CHƯA XÁC MINH |
| B-06 | Không N+1 | Dùng `Select`/`Include` một lượt cho room/property/members | P2 | CHƯA XÁC MINH |
| B-07 | Tôn trọng global query filter (soft delete) | Không dùng `IgnoreQueryFilters` cho list/detail nghiệp vụ | P2 | CHƯA XÁC MINH |
| B-08 | `AsNoTracking` cho truy vấn chỉ đọc | Có | P2 | CHƯA XÁC MINH |
| B-09 | Query khách thuê hiện có | `TenantService` (~647-738) trả mọi trạng thái, `ScopeQuery` không giới hạn toà cho QL | P1 | FAIL — cần lọc/nhất quán khi ghép |
| B-10 | Đếm/tổng hợp cho badge | Không tính bằng nhiều query rời nếu list lớn | P2 | CHƯA XÁC MINH |

## C. Tạo hợp đồng

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| C-01 | Caller + scope | Bắt buộc, kiểm theo A-06/A-07 | P0 | FAIL — `ContractService.cs:208` |
| C-02 | `StartDate < EndDate` | Chặn, lỗi typed | P1 | PASS một phần — dùng `ArgumentException` |
| C-03 | Có ≥ 1 khách thuê | Chặn, lỗi typed | P1 | PASS một phần |
| C-04 | Khách thuê tồn tại + đang hoạt động | Chặn | P1 | PASS |
| C-05 | Trùng khách trong cùng request | `Distinct()` trước khi lưu | P1 | PASS — `ContractService.cs:242` |
| C-06 | Phòng tồn tại, active, không xoá | Chặn | P1 | PASS |
| C-07 | Điều kiện phòng khi tạo | Theo S-5: xác định rõ "Trống" hoặc "Sắp trống" bằng dữ liệu HĐ, không chỉ `RoomStatus.Available` | P0 | FAIL — `ContractService.cs:231` chỉ nhận `Available` |
| C-08 | Không tạo HĐ trùng khoảng với HĐ `Approved`/`Pending` khác | Chặn sớm hoặc để trigger bắt, trả lỗi nghiệp vụ | P0 | CHƯA XÁC MINH (chỉ có trigger ở bước duyệt) |
| C-09 | Lưu `CreatedBy` + thời điểm | Phục vụ audit/detail | P1 | FAIL — entity không có trường |
| C-10 | Snapshot giá/cọc lúc tạo | Lấy từ dữ liệu phòng tại thời điểm tạo | P1 | PASS — `ContractService.cs:258-267` |
| C-11 | Transaction + rollback | Có, giữ exception gốc | PASS | PASS — `ContractService.cs:273-297` |
| C-12 | Mã hợp đồng | Không trùng, không quá dài | P2 | PASS (không trùng) / NIT (35 ký tự) |
| C-13 | Trả DTO đủ cho UI điều hướng sang detail | Có `ContractId` | P2 | PASS |

## D. Duyệt / từ chối

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| D-01 | Chỉ duyệt HĐ `Pending` | Sai trạng thái → `InvalidStatus` | P0 | PASS — `ContractService.cs:131-136` |
| D-02 | Khoá HĐ khi quyết định | `UPDLOCK, HOLDLOCK` + reload | P0 | PASS — `ContractService.cs:119-130` |
| D-03 | Khoá phòng khi duyệt | Có; reject không khoá phòng | P1 | PASS — `ContractService.cs:78-90` |
| D-04 | Phòng bị xoá / ngưng hoạt động | Chặn | P0 | PASS — `ContractService.cs:152-157` |
| D-05 | Toà bị xoá | Chặn | P0 | PASS — `ContractService.cs:145-150` |
| D-06 | Phòng `Maintenance` | Chặn | P0 | PASS — `ContractService.cs:159-164` |
| D-07 | Phòng đang `Rented` | Chặn (spec: chỉ Trống hoặc Sắp trống) | P0 | **FAIL** — không kiểm, chỉ chặn Maintenance |
| D-08 | Trùng khoảng thời gian với HĐ `Approved` | Chặn | P0 | PASS — `ContractService.cs:166-178` |
| D-09 | Trigger SQL trùng khoảng (51030) | Map thành lỗi nghiệp vụ, rollback sạch | P1 | PASS — `ContractService.cs:192-199` |
| D-10 | Set `RoomStatus.Rented` theo thời điểm | Chỉ khi HĐ đã/đang hiệu lực; HĐ tương lai không được chiếm phòng ngay | P0 | **FAIL** — `ContractService.cs:184` set vô điều kiện |
| D-11 | Duyệt làm ghi đè giá/cọc | Theo `docs/VHT-9…` là đúng thiết kế snapshot | P2 | PASS (đúng thiết kế) — ghi chú rủi ro nếu sau này cho nhập giá/cọc riêng |
| D-12 | Từ chối: bắt buộc lý do, giới hạn độ dài, trim | Có | P1 | PASS — `ContractService.cs:38-58` |
| D-13 | Từ chối: xoá lý do cũ khi duyệt lại | Có | P2 | PASS — `ContractService.cs:180-181` |
| D-14 | Rollback + reload khi lỗi | Không để entity/room bẩn trong context | P1 | PASS — `ContractService.cs:322-358` |
| D-15 | Sau duyệt, phòng/hợp đồng hiển thị đúng ở list/detail | Cần test tích hợp | P1 | CHƯA XÁC MINH |

## E. Trạng thái phòng và "Sắp trống" (theo S-4, S-5)

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| E-01 | Không thêm enum/cột khi chưa chứng minh | Không có `SapTrong`, không `AvailableFrom` nếu chưa duyệt | P0 (phạm vi) | PASS — chưa thêm |
| E-02 | Định nghĩa "Sắp trống" bằng dữ liệu HĐ | Có quy tắc viết rõ + test | P0 | **FAIL** — chưa có định nghĩa trong code |
| E-03 | Phòng đang có HĐ hiệu lực | Không cho duyệt HĐ khác chồng lấn (D-08 đã chặn theo ngày) | P0 | PASS theo ngày / FAIL theo trạng thái phòng (D-07) |
| E-04 | Phòng có HĐ `Approved` tương lai | Được xem là "sắp trống"; tạo/duyệt HĐ mới cho khoảng không chồng lấn | P0 | **FAIL** — phòng bị set `Rented`, create chỉ nhận `Available` |
| E-05 | HĐ hết hạn nhưng phòng còn `Rented` | Có cách nhận biết "đã trống trên thực tế" mà không cần migration | P1 | CHƯA XÁC MINH |
| E-06 | Trạng thái hiển thị suy ra từ ngày | `Pending`→Chờ duyệt; `Rejected`→Từ chối; `Approved` + `today < StartDate`→Sắp hiệu lực; trong hạn→Hiệu lực; `EndDate - today ≤ ngưỡng`→Sắp hết hạn; `today > EndDate`→Hết hạn (cần chốt ngưỡng) | P1 | CHƯA XÁC MINH |
| E-07 | Badge `Liquidated` / `Cancelled` | Không có dữ liệu DB → không được bịa; ẩn hoặc chờ VHT-50 | P1 | SKIP (ngoài phạm vi) |
| E-08 | Biên múi giờ/ngày | So sánh theo ngày nghiệp vụ, không lẫn `DateTime.Now` UTC/local | P1 | CHƯA XÁC MINH |

Đề xuất quy tắc "Sắp trống" để review (chưa phải quyết định):

```
Phòng P được coi là "sắp trống" tại thời điểm T nếu:
  - không có HĐ Approved nào của P phủ T (StartDate <= T <= EndDate), VÀ
  - tồn tại HĐ Approved của P có StartDate > T (đã có người thuê tương lai), HOẶC
  - HĐ Approved gần nhất của P có EndDate trong khoảng (T, T + N ngày].

Hệ quả cần xử lý:
  - "Trống" = không có HĐ Approved nào phủ T và không có HĐ tương lai.
  - Khi duyệt HĐ có StartDate > hôm nay: KHÔNG set Rented ngay (D-10).
  - Khi tạo HĐ: chấp nhận phòng Trống hoặc Sắp trống, miễn khoảng ngày không chồng lấn.
```

Cần chốt: giá trị `N` (ví dụ 30 ngày) và việc HĐ `Pending` có tính vào "sắp trống" hay không.

## F. Validation và xử lý lỗi

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| F-01 | Lỗi nghiệp vụ dùng exception typed + mã lỗi | `ContractValidationException` / `ContractOperationException` | P1 | FAIL — create ném `ArgumentException` + chuỗi tiếng Việt |
| F-02 | Thông báo lỗi lấy từ resource, không hard-code | Không chuỗi tiếng Việt trong service | P1 | FAIL — `ContractService.cs:214-256` |
| F-03 | Map lỗi về đúng field trên form | `ContractErrorCodes` → field | P1 | CHƯA XÁC MINH |
| F-04 | Giá thuê / cọc không âm | Chặn ở model + service; hiện chưa có input giá/cọc | P1 | CHƯA XÁC MINH |
| F-05 | Ngày bắt đầu/kết thúc hợp lệ | Có, thông báo rõ | P1 | PASS một phần |
| F-06 | Không nuốt exception | Chỉ bắt để rollback rồi ném lại | P1 | PASS — `ContractService.cs:286-297` |
| F-07 | Trạng thái UI loading/empty/error/success | Có đủ 4 trạng thái | P1 | FAIL — chưa có màn |

## G. VI/EN và định dạng

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| G-01 | Key list | Đủ cột, tiêu đề, bộ lọc, empty state | P1 | FAIL |
| G-02 | Key detail | Đủ nhãn, nút, lý do từ chối, trạng thái cọc | P1 | FAIL |
| G-03 | Key approve modal | Đủ tiêu đề, nút, cảnh báo, lý do bắt buộc | P1 | FAIL |
| G-04 | Key trạng thái hiển thị | Đủ 5–6 nhãn theo E-06 | P1 | FAIL |
| G-05 | Số key VI = EN | Cân bằng, không key mồ côi | P1 | FAIL — 104 key chỉ cho form; sẽ lệch khi thêm |
| G-06 | Không hard-code chuỗi trong `.razor` | Dùng `L[...]` | P1 | CHƯA XÁC MINH |
| G-07 | Ngày `dd/MM/yyyy` qua helper chung | Không tự `ToString` | P1 | CHƯA XÁC MINH |
| G-08 | Tiền `1.500.000 ₫` qua `MoneyFormatter` | Không tự format | P1 | CHƯA XÁC MINH |
| G-09 | `tabular-nums`, nút 40/44px, badge đúng component | Theo `docs/design/phong_cach_giao_dien.md` | P2 | CHƯA XÁC MINH |
| G-10 | Breadcrumb + `MainLayout` | Đúng spec | P2 | CHƯA XÁC MINH |

## H. SQL, hiệu năng, đồng thời

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| H-01 | Tham số hoá truy vấn | Không nối chuỗi | P0 | PASS — dùng interpolated/EF |
| H-02 | `FromSqlInterpolated` + `IgnoreQueryFilters` chỉ dùng nơi cần khoá | Có comment giải thích | P2 | PASS |
| H-03 | Transaction bao đúng phạm vi | Không mở transaction quá sớm, không commit một phần | P1 | PASS |
| H-04 | Chống race duyệt đồng thời | Khoá HĐ + reload trạng thái | P0 | PASS |
| H-05 | Chống race duyệt cùng phòng | Khoá phòng | P0 | PASS |
| H-06 | Trigger trùng khoảng hoạt động trên DB thật | Cần chạy integration test | P0 | CHƯA XÁC MINH |
| H-07 | Index cho list (theo toà/trạng thái/ngày) | Có hoặc chấp nhận được với dữ liệu nhỏ | P2 | CHƯA XÁC MINH |
| H-08 | Không `SELECT *` ngoài câu khoá | Chọn cột cần thiết | P2 | PASS (ngoài câu khoá) |

## I. Test và nghiệm thu

| ID | Nội dung kiểm | Kỳ vọng | Mức nếu sai | Hiện trạng |
|---|---|---|---|---|
| I-01 | Unit test ma trận quyền list/detail | Có, đủ 4 vai trò | P0 | FAIL |
| I-02 | Unit test create thiếu caller / sai toà | Có | P0 | FAIL |
| I-03 | Unit test approve phòng `Rented` | Có | P0 | FAIL |
| I-04 | Unit test approve start tương lai không chiếm phòng | Có | P0 | FAIL |
| I-05 | Unit test "sắp trống" theo quy tắc E-02 | Có bảng case biên | P0 | FAIL |
| I-06 | Test song ngữ cân key | Có | P1 | FAIL |
| I-07 | Integration test trên SQL thật (trigger, khoá) | Chạy được, ghi lại kết quả | P0 | CHƯA XÁC MINH |
| I-08 | `dotnet build` 0 warning | Bắt buộc | P1 | PASS (baseline) |
| I-09 | Không phá test cũ của VHT-29/VHT-30 | Toàn bộ xanh | P0 | CHƯA XÁC MINH |

---

## J. Đối chiếu baseline — lỗi đã biết (dùng để so diff)

| ID checklist | File / dòng | Mô tả |
|---|---|---|
| A-06, A-07, A-08, A-13 | `Web/Features/Contracts/Services/ContractService.cs:208` | Create không nhận caller, không kiểm scope |
| A-01..A-05 | `Web/Features/Contracts/Services/IContractService.cs:6-15` | Không có method list/detail |
| B-02, B-03 | `Web/Features/Contracts/DTOs/ContractResponseDto.cs` | Thiếu tên phòng/toà/khách, người duyệt, ngày duyệt |
| C-07 | `ContractService.cs:226-238` | Chỉ nhận phòng `Available` |
| C-09 | `Web/Features/Contracts/Entities/Contract.cs:6-26` | Thiếu `CreatedBy`, `ApprovedBy`, `ApprovedAt` |
| D-07 | `ContractService.cs:159-164` | Chỉ chặn `Maintenance`, cho qua `Rented` |
| D-10 | `ContractService.cs:184` | Set `Rented` ngay cả khi HĐ tương lai |
| E-02, E-04 | `ContractService.cs:166-184, 226-231` | Không có định nghĩa "sắp trống"; phòng bị chiếm sớm |
| F-01, F-02 | `ContractService.cs:212-256` | `ArgumentException` + chuỗi tiếng Việt hard-code |
| G-01..G-05 | `Web/Resources/Features/Contracts/ContractResources.{vi-VN,en-US}.resx` | 104 key, chỉ phục vụ form tạo |
| P0 (UI) | `Web/Features/Contracts/Pages/` | Thiếu `ContractListView.razor`, `ContractDetailView.razor`, `ContractApproveModal.razor` |
| P0 (UI) | `Web/Features/Contracts/Pages/ContractCreateForm.razor:563-595` | Form mock, không gọi service |
| P0 (route) | `Web/Components/Layout/AppNavigation.cs:15` | Menu trỏ `/contracts/create`, không có `/contracts` |
| B-09 | `Web/Features/Contracts/Services/TenantService.cs` (~647-738) | Trả mọi trạng thái, không giới hạn toà cho QL |
| P2 (dọn dẹp) | `Web/Features/Contracts/ ContractDtos.cs` (có dấu cách đầu tên) | DTO mẫu VHT-9 cũ, namespace khác, trùng `ContractStatus` 6 giá trị, vẫn compile |

---

## K. Quy trình review một diff/commit

1. Xác nhận phạm vi: `git log --oneline dev..HEAD`, `git diff --stat dev...HEAD`.
2. Build: `dotnet build` tại `NhaTro` — phải 0 warning / 0 error.
3. Đọc diff theo thứ tự: entity/migration → DTO → service → DI → page/component → resx → test.
4. Đối chiếu từng mục A–I, ghi PASS/FAIL/CHƯA XÁC MINH kèm `file:dòng`.
5. Kiểm tra riêng: không có migration/enum mới ngoài S-3/S-5; không có chuỗi hard-code; không đổi hành vi VHT-29/VHT-30.
6. Chạy test khả dụng; nếu cần SQL, ghi rõ "chưa chạy" thay vì suy đoán.
7. Kết luận: danh sách lỗi theo mức P0/P1/P2 + hướng sửa cụ thể, không tự sửa code.

## L. Câu hỏi còn mở (cần người quyết)

| Mã | Câu hỏi | Ảnh hưởng |
|---|---|---|
| Q-1 | Ngưỡng "Sắp hết hạn" (ví dụ 30 ngày) và "sắp trống" dùng cùng ngưỡng? | E-06, E-02 |
| Q-2 | HĐ `Pending` có được tính khi xác định phòng "sắp trống"/trùng lịch? | C-08, E-02 |
| Q-3 | Có ràng buộc khách thuê phải thuộc cùng toà với phòng? | A-13 |
| Q-4 | Có cho nhập giá thuê/cọc riêng khi tạo, hay luôn snapshot 20% giá chuẩn? | F-04, D-11 |
| Q-5 | Có cần hiển thị "Hết hạn" cho HĐ `Approved` đã qua `EndDate` (ngoài 6 badge spec)? | E-06 |
