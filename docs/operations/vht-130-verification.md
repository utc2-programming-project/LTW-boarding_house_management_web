# VHT-130 — Biên bản kiểm chứng local bản tích hợp tuần 2

Kiểm chứng ba nhóm trước khi đóng VHT-130: ghi/sửa dữ liệu rồi mở lại, tệp đúng/thiếu quyền, và Blazor reconnect.

Đây là kiểm **local** trên LocalDB, không phải smoke 2E/IIS. Không thay thế bảng trong [VHT-44 release record](vht-44-release-record.md).

## Môi trường

| Mục | Giá trị |
| --- | --- |
| Nhánh / SHA | `VHT-130-ban-giao-db-2e` tại `9d77364` |
| Thời điểm | 05/11/2026, khoảng 20:10–20:50 (+07:00) |
| Ứng dụng | `NhaTro/Web`, chạy Debug, `http://localhost:5199` |
| Database | LocalDB `NhaTro_VHT130_Manual`, 29 migration, mới nhất `20261104033809_AddContractRenewalRequests` |
| Tệp | thư mục storage tạm ngoài thư mục publish; metadata trong `StoredFiles`, audit trong `FileAccessAuditLogs` |
| Tài khoản | 4 tài khoản seed dev (Admin, Chủ trọ, Quản lý, Khách thuê); mật khẩu đọc từ user secrets, không ghi trong tài liệu |
| Dữ liệu mẫu | nhà trọ `Nhà trọ kiểm thử 130`, phòng `P101`, khách `Khách kiểm thử 130` (`0905556666`), hợp đồng `HD-KT130-01` trạng thái Pending |

## Tổng hợp

| Nhóm | Kết quả |
| --- | --- |
| 1. Tạo/sửa dữ liệu rồi mở lại, đối chiếu SQL | Đạt |
| 2. Upload/đọc tệp bằng tài khoản có quyền | Đạt |
| 2b. Tài khoản thiếu quyền | Đạt — bị chặn và có audit `Forbidden` |
| 3. Ngắt mạng ngắn rồi kết nối lại (Blazor reconnect) | Đạt — modal hiện và tự phục hồi |
| Kiểm tự động trên nhánh 130 | 295/295 integration đạt, trong đó 6/6 storage |

## 1. Tạo/sửa dữ liệu rồi mở lại

Thao tác: đăng nhập Chủ trọ, tạo mới một khách thuê và sửa số điện thoại của `Khách kiểm thử 130`, sau đó tải lại trang và mở lại hồ sơ.

Đối chiếu SQL:

```sql
SELECT Id, FullName, Phone, CreatedAt FROM dbo.Tenants ORDER BY Id DESC;
```

Kết quả (giờ UTC):

```text
Id|FullName             |Phone       |CreatedAt
2 |sdfs                 |03423423434 |2026-11-05 13:17:05
1 |Khách kiểm thử 130   |0905556667  |2026-11-05 13:12:36
```

- Bản ghi mới có trong SQL sau khi tạo.
- Số điện thoại của khách mẫu đổi từ `0905556666` sang `0905556667` và giữ nguyên sau khi tải lại trang.
- Tên tiếng Việt lưu đúng dấu: `SELECT CASE WHEN FullName = N'Khách kiểm thử 130' THEN 'OK' ELSE 'SAI' END` trả `OK`.

## 2. Tệp có quyền và thiếu quyền

### 2a. Tài khoản có quyền

Thao tác: Chủ trọ báo sự cố cho phòng `P101`, đính kèm 1 ảnh JPG và 1 ảnh PNG, gửi phiếu, rồi mở lại phiếu và tải cả hai ảnh.

Bằng chứng:

```sql
SELECT FileName, ContentType, FileSizeBytes, RelatedEntityType, RelatedEntityId, IsDeleted
FROM dbo.StoredFiles ORDER BY UploadedAt DESC;

SELECT OccurredAt, ActorRole, Action, Succeeded, ErrorCode
FROM dbo.FileAccessAuditLogs ORDER BY OccurredAt DESC;
```

```text
FileName                    |ContentType|FileSizeBytes|RelatedEntityType|RelatedEntityId|IsDeleted
4b_Hiến máu tình nguyện.jpg |image/jpeg |195974       |4                |1              |0
câu2.png                    |image/png  |240907       |4                |1              |0

OccurredAt (UTC)          |ActorRole|Action  |Succeeded|ErrorCode
2026-11-05 13:22:15       |ChuTro   |Download|1        |NULL
2026-11-05 13:22:15       |ChuTro   |Download|1        |NULL
2026-11-05 13:22:13       |ChuTro   |Upload  |1        |NULL
2026-11-05 13:22:13       |ChuTro   |Upload  |1        |NULL
```

Hai tệp vật lý tồn tại dưới thư mục storage, đúng đường dẫn tương đối trong `StoredFiles.RelativePath`.

### 2b. Tài khoản thiếu quyền

Thao tác: đăng nhập Khách thuê, mở trực tiếp `GET /api/files/{id}` của ảnh thuộc sự cố trên.

Kết quả: HTTP 404 với thân `{"succeeded":false,"errorCode":"FileNotFound"}` — cố ý trả 404 để không lộ tệp có tồn tại; lý do thật nằm ở audit:

```text
OccurredAt (UTC)    |ActorRole|Action  |Succeeded|ErrorCode
2026-11-05 13:36:02 |KhachThue|Download|0        |Forbidden
```

Lý do bị chặn: hợp đồng `HD-KT130-01` đang `Status = 0` (Pending), không nằm trong phạm vi hợp đồng Approved còn hiệu lực của khách thuê (`IncidentFileScope`).

Đối chứng dự kiến cho Quản lý: `PropertyManagers` đang 0 dòng nên tài khoản Quản lý không được giao tòa nhà nào và cũng bị chặn với cùng mã `Forbidden`. Admin bị chặn ở policy `Business` (403) trước khi vào service nên không sinh dòng audit.

## 3. Blazor reconnect

Thao tác: đang mở trang Bảng đơn giá, chuyển DevTools → Network → Offline trong khoảng 48 giây, rồi chuyển lại No throttling. Không tải lại trang trong lúc mất mạng.

Bằng chứng phía server:

```text
[2026-11-05 20:41:02 +07:00] HTTP GET /_blazor → 101 kết thúc sau 139736 ms   (socket đóng khi Offline)
[2026-11-05 20:41:50 +07:00] HTTP POST /_blazor/negotiate → 200                (reconnect, 48 giây sau)
```

- Modal `components-reconnect-modal` hiện đúng trạng thái: "Rejoining the server..." rồi "Rejoin failed... trying again in 3 seconds.".
- Sau khi Online, chỉ có đúng một lần `negotiate` rồi im lặng — không lặp lại, không tải lại css/js/`_blazor/initializers`, tức là reconnect chứ không phải reload.
- Số liệu trước và sau reconnect không đổi:

```sql
SELECT (SELECT COUNT(*) FROM dbo.Tenants) AS Tenants,
       (SELECT COUNT(*) FROM dbo.MaintenanceIncidents) AS Incidents,
       (SELECT COUNT(*) FROM dbo.StoredFiles) AS Files;
-- 2 | 1 | 2 trước và sau reconnect
```

## Khoảng trống và ghi chú

- Chưa chạy trên IIS/HTTPS, ACL app pool và migration trên SQL server thật; các mục này vẫn thuộc 2E.
- `scripts/database/chuan_bi_db_vht130.ps1` ghim 29 migration và cấm `AddPayments` theo bản 130. Nhánh `dev` hiện có 30 migration (`20261105044209_AddAuditUtilityPayment`), nên phải cập nhật script trước khi bàn giao bản tích hợp từ `dev`.
- Lỗi đã ghi nhận trong script DB: `Invoke-VerifyMode` định dùng kỳ vọng mặc định khi thiếu artifact, nhưng `Resolve-Artifact` gọi `Stop-With` (có `exit`), nên `try/catch` không bắt được và script thoát với mã 2 thay vì chạy tiếp. Chưa sửa.
