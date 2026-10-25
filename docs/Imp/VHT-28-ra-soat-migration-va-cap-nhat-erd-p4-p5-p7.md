# VHT-28 — Rà soát migration và cập nhật ERD P4/P5/P7

> **Đợt gốc:** 2A (Sprint 2A) · **Các đợt có việc:** 2A, 2C, 2D, 2E (2B không phải đợt riêng của task; migration gửi sớm trong 2B vẫn review ngay bằng cùng checklist).
> **Chính:** Tính · **Phối hợp:** Đào (entity/EF/migration P4/P5), Phát (metadata tệp, schema P3/P6, kiểm DB), Phúc (nhận ERD ghép Word).
> **Task tách đợt:** VHT-121 (2C — rà soát migration hóa đơn theo lượt), VHT-122 (2D — cập nhật ERD theo migration đã ghép), VHT-123 (2E — kiểm migration và ERD trên bản chung).
> **Nguồn:** Jira VHT-28 · `docs/task/cong_viec_theo_tuan.md` (dòng VHT-28) · `docs/task/timeline_song_song.md` (nhịp 2A–2E) · `docs/task/quy_uoc_jira.md` · `docs/Imp/VHT-117-review-va-thu-tu-merge-tuan-1.md` (quy trình migration đã chốt tuần 1) · `docs/task/de_xuat_hop_dong_nha_tro_phong.md` · `docs/task/de_xuat_hop_dong_storage.md` · `docs/VHT-9_trang_thai_ky_du_lieu_P4_P5.md` · `docs/design/database/so_do_quan_he_vht5_schema.sql`.

---

## 1. Mục tiêu

Review/merge migration theo lượt; chạy trên DB sạch và DB có dữ liệu, kiểm FK/unique; cập nhật ERD gửi Phúc.

Đo được:

- Không PR migration nào được merge khi chưa có kết quả chạy trên **DB sạch** và **DB có dữ liệu mẫu không nhạy cảm**.
- Mỗi migration P4/P5/P7 có: entity + configuration + migration + snapshot đồng bộ trong **cùng một PR**, không sửa migration đã merge.
- Nguồn ERD (`docs/design/database/so_do_quan_he_vht5_schema.sql`) khớp 100% schema đã merge (cột, kiểu, null, PK/FK/unique/check) và SVG/Word tương ứng được cập nhật ở 2D.
- Không có FK nghiệp vụ `CASCADE`, không có bảng thiếu PK, không có unique thiếu bộ lọc xóa mềm khi bảng có `IsDeleted`.

## 2. Phạm vi

**Làm**

- Rà entity/configuration/migration/snapshot của P4 (hợp đồng, thành viên, cọc), P5 (dịch vụ, đơn giá, biểu giá, chỉ số, chi phí), P7 (sự cố, bảo trì), metadata tệp dùng chung.
- Kiểm FK, unique, check constraint, kiểu tiền/chỉ số, hành vi xóa và index lọc.
- Điều phối thứ tự ghép migration giữa Đào và Phát trên nền `dev` mới nhất.
- Cập nhật nguồn ERD (SQL + SVG) theo migration đã merge; bàn giao Phúc cho Word.
- Cung cấp script kiểm tra lặp lại được cho mọi PR migration.

**Không làm**

- Không viết entity/migration nghiệp vụ thay Đào/Phát; không tự đặt tên cột/bảng khi chưa có entity tương ứng.
- Không làm việc ngoài các đợt và ngoài P4/P5/P7 + metadata tệp; không đụng nghiệp vụ ngoài quyết định D1–D17.
- Không sửa migration đã merge, không reset/drop DB dùng chung, không tạo lại migration INIT.
- Không merge PR, không push khi chưa hỏi.
- Không chốt thay nhóm các điểm còn mở ở mục 8 (cọc, hủy hợp đồng, kỳ lẻ, làm tròn…).

## 3. Hiện trạng đã kiểm (nền `dev` = `781c268`, đã merge vào nhánh VHT-28 ngày 25/10/2026)

### 3.1 Migration đang có

| Thứ tự | Migration | Nội dung | Trạng thái ERD |
| --- | --- | --- | --- |
| 1 | `00000000000000_CreateIdentitySchema` | Identity + `AspNetUsers` | có |
| 2 | `20261008142750_CoreSchema` | `Properties`, `Rooms`, `Tenants` | có |
| 3 | `20261016142707_AddStorageStoredFiles` | `StoredFiles` + FK user + 2 index | **thiếu** → đã bổ sung ở 2A |
| 4 | `20261017005536_ExtendTenantProfile` | 8 cột hồ sơ khách + đổi lọc unique | **thiếu** → đã bổ sung ở 2A |
| 5 | `20261019101408_AddUserFullName` | `AspNetUsers.FullName` | **thiếu** → đã bổ sung ở 2A |
| 6 | `20261020080707_AddManagerToggles` | `ManagerToggles` + 21 dòng seed + 3 check | **thiếu** → đã bổ sung ở 2A |
| 7 | `20261021014020_AddFileAccessAuditLog` | `FileAccessAuditLogs` (append-only, không FK) | **thiếu** → đã bổ sung ở 2A |

Chưa có migration nào cho P4/P5/P7 trên `dev`. Entity hiện có: `Properties`, `Rooms`, `Tenants`, `StoredFile`, `FileAccessAuditLog`, `ManagerToggle`.

- **P4** có trên nhánh `VHT-29-tao-hop-dong` (chưa merge): `Contract`, `ContractMember`, migration `20261024084644_AddContracts`.
- **P5** mới có DTO (`Features/Utilities/*Dtos.cs`), chưa có entity/migration.
- **P7** mới có thư mục rỗng `Features/Maintenance/`, chưa có entity/migration (nhánh `VHT-39-...` chỉ là UI).
- **Metadata tệp** đã xong trên `dev`: `StoredFiles` + `FileAccessAuditLogs` + quy tắc loại tệp.

### 3.2 Delta ERD đã phát hiện và xử lý ở 2A

| Khu vực | ERD cũ (VHT-5) | Schema thật sau migration | Xử lý |
| --- | --- | --- | --- |
| `AspNetUsers` | không có `FullName` | `FullName nvarchar(200) NULL` | đã thêm |
| `Tenants` | 9 cột | + `IsActive`, `RowVersion`, `Gender`, `DateOfBirth`, `IdIssuedDate`, `IdIssuedPlace`, `PermanentAddress`, `Note` | đã thêm |
| `IX_Tenants_CitizenId` | `CitizenId IS NOT NULL AND IsDeleted = 0` | thêm `AND IsActive = 1` | đã sửa |
| `IX_Tenants_UserId` | index thường | **unique** filtered `UserId IS NOT NULL AND IsDeleted = 0` | đã sửa |
| `IX_Tenants_FullName`, `IX_Tenants_Phone` | không có | có | đã thêm |
| `StoredFiles` | không có | 14 cột, FK user Restrict, 2 index | đã thêm |
| `ManagerToggles` | không có | 4 cột, PK `Code`, FK user, 3 check, seed 21 dòng | đã thêm |
| `FileAccessAuditLogs` | không có | 11 cột, PK `bigint identity`, 3 index, **không FK** (log phải sống sau khi user/tệp bị xóa) | đã thêm |

> Hai thay đổi **đổi ngữ nghĩa** phải ghi rõ trong ERD/Word: (1) `UserId` nay là 1–1 (một tài khoản tối đa một hồ sơ khách), khác kết luận VHT-5; (2) CCCD chỉ chặn trùng khi hồ sơ `IsActive = 1`. Cả hai đến từ VHT-23 và cần Đào/Phát xác nhận lại khi dùng cho P4 (hợp đồng tham chiếu hồ sơ khách).

### 3.3 Kết quả chạy thật ngày 25/10/2026

| Nội dung | Lệnh | Kết quả |
| --- | --- | --- |
| Danh sách migration | `dotnet ef migrations list --project NhaTro/Web` | 7 migration, không có pending |
| DB sạch | `scripts/database/kiem_tra_migration.ps1` | Đạt: tạo DB mới, áp đủ 7 migration |
| DB có dữ liệu | cùng script (seed Users/Properties/Rooms/Tenants rồi update lại) | Đạt: dữ liệu còn nguyên `1\|1\|1` |
| PK/FK/unique/check | cùng script | Đủ PK; FK nghiệp vụ `NO_ACTION`; 3 check của `ManagerToggles` |
| ERD SQL vs schema EF | so `sys.columns`/`sys.indexes`/`sys.foreign_keys` giữa 2 DB | 0 khác biệt cột, 0 khác biệt index trên 7 bảng nghiệp vụ; FK khác chỉ thuộc bảng Identity phụ (ERD không vẽ, đúng thiết kế) |

> **Bài học khi làm script:** lần đầu chạy với `--no-build`, EF dùng bản build cũ nên **bỏ sót migration mới `AddFileAccessAuditLog`** mà vẫn báo "đạt". Script đã bỏ `--no-build` và kiểm lại pending **theo DB kiểm tra** (không theo DB mặc định trong `appsettings`). Từ nay mọi lần kiểm phải để EF build lại.

### 3.4 Tình trạng 2A sau khi pull `dev` — **chưa thể đóng**

| Hạng mục của 2A | Trạng thái | Bằng chứng |
| --- | --- | --- |
| Metadata tệp dùng chung | **Xong** | `StoredFiles` + `FileAccessAuditLogs` đã merge `dev`, ERD đã cập nhật |
| Schema P4 (hợp đồng) | **Chưa merge, chưa đạt** | Có trên nhánh `VHT-29-tao-hop-dong`; xem lỗi ở mục 3.4.1 |
| Schema P5 (dịch vụ/kỳ/chi phí) | **Chưa có** | Chỉ có DTO, không có entity/migration ở bất kỳ nhánh nào |
| Schema P7 (sự cố) | **Chưa có** | `Features/Maintenance/` chỉ có `.gitkeep` |
| ERD khớp phần đã merge | **Xong** | Mục 3.2–3.3 |
| Người thứ hai chạy script | **Chưa** | Cần Đào hoặc Phát |

#### 3.4.1 Rà schema P4 trên nhánh `VHT-29-tao-hop-dong` (ngày 25/10/2026)

Đây là lỗi **chặn merge** theo checklist mục 5, không phải góp ý phong cách:

| # | Vấn đề | Hệ quả |
| --- | --- | --- |
| 1 | `Contract.ContractId` là `Guid`, trong khi DTO `ContractDto` và mọi bảng khác dùng `int` | Lệch kiểu xuyên DTO/service/UI; `StoredFiles.RelatedEntityId` là chuỗi nên phải quy ước riêng |
| 2 | Không có cột đại diện trong `ContractMembers` (DTO có `IsRepresentative`) | Không thể bảo đảm "đúng một người đại diện" |
| 3 | Không có unique/ràng buộc chống **2 hợp đồng hiệu lực cùng một phòng**; `IX_Contracts_RoomId` là index thường | Vi phạm quy tắc VHT-9 mục 2; đua dữ liệu sẽ lọt |
| 4 | Thiếu `CHECK StartDate < EndDate` | Lưu được hợp đồng vô nghĩa |
| 5 | Không có `IsDeleted`; `IX_Contracts_ContractNumber` unique không lọc | Không có thùng rác hợp đồng; xóa mềm sau này sẽ vướng unique |
| 6 | Thiếu các cột DTO cần: `CreatedBy`, `ApprovedBy`, `ApprovedAt`, `RejectReason`, `CancelReason`, `InitialElectricIndex`, `InitialWaterIndex`, `CreatedAt/UpdatedAt` | Service không trả đủ `ContractDto`; không có dấu vết duyệt/từ chối |
| 7 | Không có bảng snapshot dòng dịch vụ (`ServiceLines`) | Hợp đồng không giữ được dịch vụ đã chốt, P5/P6 mất nguồn |
| 8 | Không có bảng lịch sử trạng thái | Không đáp ứng "giữ lịch sử" của VHT-9 |

Phần **đạt**: FK `RoomId → Rooms` Restrict, khóa chính ghép `(ContractId, TenantId)`, FK thành viên Restrict, tiền `decimal(18,2)`, trạng thái lưu `int`.

## 4. Việc cần làm theo đợt

### Đợt 2A — Chốt nền trước khi có SQL P4/P5/P7

Mục tiêu đợt: Đào/Phát viết entity/EF/migration độc lập; Tính chốt **hợp đồng schema** và bộ kiểm để review ngay khi PR mở, tránh ghép xong mới phát hiện thiếu FK/unique.

| # | Việc | Ai | Đầu ra |
| --- | --- | --- | --- |
| 2A.1 | Đồng bộ nguồn ERD với 6 migration đã merge (mục 3.2) | Tính | `so_do_quan_he_vht5_schema.sql` chạy được, khớp schema EF (đã xong) |
| 2A.2 | Viết script kiểm migration dùng lại cho mọi PR | Tính | `scripts/database/kiem_tra_migration.ps1` (đã xong, đã chạy) |
| 2A.3 | Gửi Đào/Phát checklist ràng buộc bắt buộc (mục 5) trước khi họ tạo migration | Tính | Checklist trong tài liệu này + tin nhắn Jira |
| 2A.4 | Chốt với Đào/Phát danh sách bảng dự kiến và tên entity cho P4/P5/P7 (mục 6) | Tính + Đào + Phát | Bảng "điểm phải chốt" đã trả lời, ghi lại trong PR hoặc comment Jira |
| 2A.5 | Chốt thứ tự ghép: migration nào trước, migration nào phải chờ | Tính | Mục 7.1 |
| 2A.6 | Chốt `RelatedEntityType` cho P7 trước khi Phát/Phú dựng ảnh sự cố | Tính + Phát | Enum không đổi sau khi có dữ liệu |

**Điều kiện xong 2A:** ERD SQL khớp schema đã merge; script chạy đạt trên máy ít nhất 1 người ngoài Tính; Đào/Phát đã nhận checklist và xác nhận tên bảng/entity.

> **Trạng thái 25/10/2026: CHƯA ĐẠT.** Đã xong ERD + script phía Tính; còn thiếu: P4 đang ở nhánh `VHT-29` và chưa đạt checklist (mục 3.4.1), P5/P7 chưa có schema, chưa có người thứ hai chạy script, chưa gửi checklist cho Đào/Phát.

### Đợt 2C — Review theo từng PR (VHT-121)

| # | Việc | Ai | Đầu ra |
| --- | --- | --- | --- |
| 2C.1 | Review entity + configuration + migration + snapshot của P4 (hợp đồng/thành viên/cọc) | Tính | Nhận xét chặn/duyệt trên PR, ghi rõ mục nào thiếu |
| 2C.2 | Review P5 (dịch vụ, đơn giá, biểu giá, kỳ, chỉ số, chi phí) | Tính | như trên |
| 2C.3 | Review P7 (sự cố, bảo trì) + ràng buộc với `StoredFiles` | Tính | như trên |
| 2C.4 | Review migration hóa đơn do Phát chuẩn bị (chưa phát hành hóa đơn thật) | Tính | Chỉ chặn lỗi schema; không đánh giá nghiệp vụ tiền |
| 2C.5 | Chạy `kiem_tra_migration.ps1` trên nhánh PR (sau khi rebase `dev`) | Tác giả + Tính xác nhận | Log/kết quả dán vào PR |
| 2C.6 | Điều phối thứ tự merge: PR nào lấy snapshot trước, PR nào rebase | Tính + Phú | Comment thứ tự trên PR |

**Điều kiện xong 2C:** mọi PR migration P4/P5/P7 đã merge đều có kết quả script đạt; không còn migration pending; không có xung đột snapshot.

### Đợt 2D — Cập nhật ERD phần đã merge (VHT-122)

| # | Việc | Ai | Đầu ra |
| --- | --- | --- | --- |
| 2D.1 | Bổ sung bảng P4/P5/P7 + quan hệ vào `so_do_quan_he_vht5_schema.sql` | Tính | SQL chạy lại được trên DB sạch, khớp EF |
| 2D.2 | Vẽ lại SVG ERD (bố cục mới, không nhồi vào khung cũ) | Tính | `docs/design/database/so_do_quan_he_vht5.svg` |
| 2D.3 | Ghi chú các điểm khác DTO/đề xuất (nếu có) ngay trên ERD/Word | Tính | Danh sách lệch kèm lý do |
| 2D.4 | Gửi Phúc bản ERD + ghi chú để ghép Word P1–P4 | Tính → Phúc | Tin nhắn kèm file, ngày gửi |
| 2D.5 | Đối chiếu ERD với migration **đã merge**, không lấy nhánh chưa merge | Tính | Không vẽ bảng "dự kiến" |

**Điều kiện xong 2D:** ERD có đủ bảng đã merge; mỗi quan hệ ghi đúng `PK/FK/nullable/unique/Restrict`; Phúc xác nhận đã nhận.

### Đợt 2E — Kiểm trên bản chung (VHT-123)

| # | Việc | Ai | Đầu ra |
| --- | --- | --- | --- |
| 2E.1 | Chạy script trên DB sạch và DB có dữ liệu của bản `dev` chung | Tính | Kết quả đạt/không, kèm SHA |
| 2E.2 | So ERD với schema chung lần cuối; sửa phần lệch | Tính | ERD chốt |
| 2E.3 | Kiểm migration có phá dữ liệu cũ không (thêm cột NOT NULL, đổi unique, đổi kiểu) | Tính + Phát | Danh sách rủi ro + cách xử lý |
| 2E.4 | Ghi chú rollback cho từng migration mới (điều kiện, mất gì) | Tính | Mục 7.3 |
| 2E.5 | Bàn giao Phúc bản ERD cuối cho Word/slide | Tính → Phúc | Xác nhận |

**Điều kiện xong 2E:** script đạt trên `dev` chung; ERD khớp schema; có ghi chú rollback; Phúc nhận bản cuối.

## 5. Checklist review migration (bắt buộc, dùng cho mọi PR)

### 5.1 Cấu trúc PR

- [ ] Entity + `IEntityTypeConfiguration` + migration + `ApplicationDbContextModelSnapshot` trong **cùng PR**, cùng một tác giả cho một lượt.
- [ ] Không sửa migration đã merge; thay đổi schema mới = migration mới.
- [ ] Tên migration mô tả việc (`AddContracts`, `AddBillingPeriods`…), không đặt tên chung.
- [ ] PR nêu: bảng/cột thêm, dữ liệu seed (nếu có), ảnh hưởng dữ liệu cũ, cách rollback.
- [ ] Không có `DropTable`/`DropColumn`/đổi kiểu trên bảng đã có dữ liệu nếu chưa có kế hoạch chuyển dữ liệu.

### 5.2 Ràng buộc schema

- [ ] Mọi bảng có PK; PK kiểu `int identity` trừ khi có lý do (bảng metadata tệp dùng `Guid`).
- [ ] FK nghiệp vụ `Restrict` (`NO_ACTION`); chỉ FK của bảng Identity phụ được `Cascade`.
- [ ] FK có index (EF tự tạo `IX_<Bảng>_<Cột>`; nếu thiếu phải thêm).
- [ ] Bảng có `IsDeleted` thì mọi unique phải có filter `[IsDeleted] = 0`; query filter `!IsDeleted` trong configuration.
- [ ] Cột tiền `decimal(18,2)`; diện tích `decimal(10,2)`; chỉ số điện/nước `decimal(18,2)` trừ khi nhóm chốt khác — **một kiểu duy nhất cho một khái niệm**.
- [ ] Cột ngày nghiệp vụ dùng `DateOnly` (`date`), thời điểm dùng `DateTime`/`DateTimeOffset` theo mẫu hiện có (`CreatedAt` UTC `datetime2`).
- [ ] Enum lưu `int` (mặc định) hoặc chuỗi — phải thống nhất trong một bảng và ghi rõ; không đổi kiểu sau khi có dữ liệu.
- [ ] Trạng thái có giới hạn thì thêm `CHECK` khi tập giá trị đã chốt (như `ManagerToggles`).
- [ ] Bảng append-only (lịch sử sự cố, lịch sử sửa chỉ số) không có cột sửa/xóa và không có `Cascade` từ cha.

### 5.3 Kiểm chạy

- [ ] `dotnet ef migrations list` không có `(Pending)` **khi truyền `--connection` của DB kiểm tra** (pending so với DB mặc định trong `appsettings` là bình thường).
- [ ] Không dùng `--no-build`: EF phải build lại, nếu không sẽ bỏ sót migration mới (đã xảy ra với `AddFileAccessAuditLog`).
- [ ] `scripts/database/kiem_tra_migration.ps1` đạt trên nhánh PR (DB sạch + DB có dữ liệu).
- [ ] Snapshot không chứa thay đổi ngoài phạm vi PR (diff snapshot chỉ đúng bảng/cột của task).
- [ ] Không đổi `ConnectionStrings`, không commit DB/log/tệp.

### 5.4 Tiêu chí chặn (không merge)

- Snapshot/migration xung đột hoặc mất dữ liệu chưa xử lý.
- FK nghiệp vụ `Cascade`, bảng thiếu PK, unique thiếu filter xóa mềm.
- Migration chứa seed dữ liệu nghiệp vụ nhạy cảm hoặc dữ liệu mẫu dùng khóa thật.
- PR không có kết quả chạy trên DB sạch và DB có dữ liệu.
- Đổi migration đã merge.

## 6. Ràng buộc schema phải có cho P4/P5/P7 (điểm phải chốt với Đào/Phát)

> Đây là **ràng buộc nghiệp vụ/kỹ thuật bắt buộc**, không phải tên cột cuối cùng. Tên bảng/cột do tác giả entity đặt, Tính review theo bảng này. Chỗ ghi *chờ chốt* thì chưa được tự quyết.

### 6.1 P4 — Hợp đồng (Đào)

| Ràng buộc | Vì sao | Cách kiểm |
| --- | --- | --- |
| Một phòng chỉ có **một** hợp đồng hiệu lực/sắp hết hạn | Quy tắc nghiệp vụ VHT-9 mục 2 | Unique filtered trên `RoomId` theo trạng thái hiệu lực, hoặc khóa nghiệp vụ tương đương + test đua |
| `(ContractId, TenantId)` duy nhất trong bảng thành viên | Không lặp một khách hai lần | Unique |
| Đúng **một** người đại diện mỗi hợp đồng | DTO có `IsRepresentative` | Unique filtered `WHERE IsRepresentative = 1` |
| `StartDate < EndDate` | Tránh hợp đồng vô nghĩa | CHECK |
| Mã hợp đồng duy nhất (nếu có `Code`) | DTO có `Code` | Unique filtered `IsDeleted = 0` |
| Giá thuê/cọc là **snapshot**, không tham chiếu giá phòng | Đổi giá chuẩn không đổi hợp đồng đã duyệt | 2 cột `decimal(18,2)` trên hợp đồng, không join giá phòng khi đọc |
| `RoomId`, `TenantId`, người tạo/người duyệt → FK `Restrict` | Không xóa thật cha khi còn hợp đồng | FK + index |
| Trạng thái lịch sử là bảng riêng, append-only | Cần dấu vết duyệt/từ chối/hủy | Không cột sửa; FK `Restrict` |
| Tiền cọc đã thu | VHT-9: ghi nhận khi thu, không nằm trong hóa đơn tháng | **Chờ chốt**: cọc nằm trên hợp đồng hay bảng thu cọc riêng (VHT-50). Chưa chốt thì chưa thêm bảng |

### 6.2 P5 — Dịch vụ, đơn giá, kỳ, chi phí (Đào)

| Ràng buộc | Vì sao | Cách kiểm |
| --- | --- | --- |
| Biểu giá điện/nước **chỉ đọc**, hiệu lực theo ngày; không có API sửa | D7 | Bảng biểu giá + bậc; `EffectiveFrom` unique theo loại |
| Bậc giá không chồng nhau, `From < To` khi có `To` | Tránh tính sai bậc | CHECK `From >= 0`, `To IS NULL OR To > From`; chồng lấn kiểm ở service + test |
| Đơn giá dịch vụ khác unique `(ServiceId, EffectiveFrom)` | Một mức giá tại một ngày | Unique |
| Kỳ unique theo hợp đồng + khoảng kỳ | Không tạo 2 kỳ trùng | Unique `(ContractId, PeriodStart)` (hoặc thêm `PeriodEnd`) |
| `PeriodStart < PeriodEnd`; trạng thái kỳ có CHECK | 0 Mở / 1 Đã chốt / 2 Đã phát hành | CHECK |
| Chi phí lưu **giá snapshot theo dòng** (điện/nước/dịch vụ) | Đổi biểu giá sau không đổi kỳ đã tính | Cột `UnitPrice`/`Amount` trên dòng chi phí |
| Lịch sử sửa chỉ số: ai, khi nào, cũ → mới, lý do | D10 | Bảng riêng, append-only, `Reason` bắt buộc |
| Chỉ số đầu/cuối kỳ `decimal(18,2)`, không âm | Tránh chỉ số âm | CHECK hoặc validate service + test |
| `InvoiceId` trên kỳ là FK **nullable** sang hóa đơn | P6 ghép sau | FK `Restrict`, không unique toàn bảng (chờ chốt 1–1 hay 1–N) |

### 6.3 P7 — Sự cố, bảo trì (Đào)

| Ràng buộc | Vì sao | Cách kiểm |
| --- | --- | --- |
| Sự cố thuộc **một phòng** (và có thể thuộc hợp đồng/khách báo) | P7 lấy phòng từ D2 | FK `Restrict` + index |
| Trạng thái xử lý có CHECK và chỉ chuyển theo bước đã chốt | VHT-33 | CHECK + service |
| Lịch sử cập nhật trạng thái append-only | Dấu vết xử lý | Bảng riêng, `Restrict` |
| Ảnh sự cố dùng `StoredFiles`, **không thêm cột ảnh** trong bảng sự cố | VHT-12/VHT-13 đã có storage | `RelatedEntityType` phải có giá trị tương ứng |
| `RelatedEntityType`/`FileCategory` không đổi giá trị sau khi có dữ liệu | Enum lưu `int`; đổi thứ tự làm sai dữ liệu | Chốt enum ở 2A.6 |

### 6.4 Metadata tệp (Phát)

| Ràng buộc | Vì sao |
| --- | --- |
| Không tạo bảng tệp thứ hai; P4/P5/P7 dùng `StoredFiles` | Tránh trùng lặp metadata |
| `RelatedEntityId` là chuỗi 450 nên **không có FK** tới bảng nghiệp vụ | Phải ghi rõ trong ERD/Word: liên kết mềm, kiểm khi upload/xóa |
| `RelatedEntityType` + `FileCategory` là enum số; thêm giá trị = migration mới + cập nhật `FileTypeRules` | Enum sai làm hỏng dữ liệu cũ |
| Tải tệp luôn qua API kiểm quyền, không đặt trong `wwwroot` | Bảo mật |

## 7. Quy trình ghép và rollback

### 7.1 Thứ tự ghép migration (2A chốt, 2C điều chỉnh)

1. Migration của **tác giả đang giữ file chung** (`ApplicationDbContext`, snapshot) ghép trước; PR sau rebase `dev` rồi tái tạo migration **chưa merge** của mình.
2. Ưu tiên migration **nền** trước: P4 hợp đồng → P5 dịch vụ/kỳ → P7 sự cố → hóa đơn P6 (2C) → thùng rác P2/P3 (2E).
3. Không ghép hai PR cùng chạm snapshot trong cùng một lượt; nếu buộc phải ghép, một người lấy snapshot cuối và người kia tái tạo migration.
4. Migration **chưa merge** được phép tái tạo; migration **đã merge** tuyệt đối không sửa.

### 7.2 Kiểm trên DB có dữ liệu

- Dùng DB riêng, không dùng DB chung. Seed tối thiểu: 1 user/chủ trọ, 1 nhà, 1 phòng, 1 khách — như script hiện có.
- Với migration thêm cột `NOT NULL`: phải có default hoặc backfill trong migration; nếu không, ghi rõ là chặn.
- Với migration đổi unique/index: kiểm dữ liệu hiện có có vi phạm không trước khi merge.
- Với migration seed: chạy lại lần 2 phải không nhân đôi (như `ManagerToggles` dùng `NOT EXISTS`/`InsertData` có khóa).

### 7.3 Ghi chú rollback (2E)

Mỗi migration mới ghi: `Down` làm gì, mất dữ liệu gì, có cần backup không. Không tự chạy `database update 0` trên DB thật.

## 8. Điểm chưa thống nhất (không tự quyết)

| # | Điểm | Ảnh hưởng schema | Ai chốt |
| --- | --- | --- | --- |
| 1 | Cọc nằm trên hợp đồng hay bảng thu cọc riêng | Bảng `Contracts` hoặc bảng mới | Đào + nhóm |
| 2 | Hủy hợp đồng: ai, trạng thái nào, xử lý cọc | CHECK trạng thái + bảng lịch sử | Nhóm (VHT-9 mục 7) |
| 3 | Ngưỡng "Sắp hết hạn" (30 ngày?) | Không ảnh hưởng schema, ảnh hưởng unique theo trạng thái | Nhóm |
| 4 | Kỳ lẻ tính theo ngày hay cả tháng | Chỉ ảnh hưởng công thức, không ảnh hưởng cột | Đào + Phát |
| 5 | Làm tròn từng dòng hay tổng | Kiểu `decimal`, cách lưu `Amount` | Phát (VHT-34) |
| 6 | Kỳ 1–1 hay 1–N với hóa đơn | Unique/FK `InvoiceId` | Phát + Tính |
| 7 | `RelatedEntityId` chuỗi hay đổi sang FK thật | Nếu đổi phải migration + sửa service storage | Tính + Phát (chốt 2A.6) |
| 8 | Tên bảng P7 (`MaintenanceIncidents`/`Incidents`) khớp enum `MaintenanceIncident` | Tên bảng/enum | Đào + Phát |

## 9. Bàn giao

| Việc | Người nhận | Thời điểm |
| --- | --- | --- |
| Checklist ràng buộc + script kiểm | Đào, Phát | Đầu 2A |
| Kết quả review từng PR migration | Tác giả PR | Trong 2C |
| ERD SQL + SVG + ghi chú lệch | Phúc (ghép Word), cả nhóm (review) | 2D/2E |
| Kết quả chạy trên `dev` chung + SHA | Phú (bản tích hợp) | 2E |

## 10. Checklist hoàn thành task

- [ ] ERD SQL khớp schema đã merge; chạy lại được trên DB sạch (đã đạt ở 2A, lặp lại sau mỗi đợt).
- [ ] Script `kiem_tra_migration.ps1` chạy đạt trên máy ít nhất 2 người.
- [ ] Mọi PR migration P4/P5/P7 có kết quả DB sạch + DB có dữ liệu.
- [ ] Không có FK nghiệp vụ `Cascade`, không bảng thiếu PK, unique có filter xóa mềm.
- [ ] Snapshot không lệch phạm vi PR.
- [ ] ERD SVG vẽ lại theo bảng đã merge và đã gửi Phúc.
- [ ] Ghi chú rollback cho từng migration mới.
- [ ] Các điểm mục 8 có quyết định hoặc được ghi rõ "chưa chốt, chưa làm".
- [ ] Không push/merge khi chưa hỏi.
