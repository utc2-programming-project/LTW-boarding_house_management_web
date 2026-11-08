# Kiểm thử: tài khoản tự đăng ký và mời khách thuê (PLAN-004…009)

Tài liệu bổ sung cho `docs/kiem_thu_toan_bo_chuc_nang.md`.

## 1. Chủ trọ tự đăng ký

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| R-01 | — | Mở `/Account/RegisterOwner`, nhập họ tên, email mới, SĐT, mật khẩu | Thấy "đã gửi yêu cầu đăng ký" |
| R-02 | — | Đăng nhập ngay sau khi đăng ký | Bị chặn: tài khoản chờ duyệt (`ApprovedAt = NULL`) |
| R-03 | Admin | `/admin/users` → duyệt tài khoản vừa đăng ký | Đăng nhập được, tạo được nhà trọ |
| R-04 | — | Đăng ký trùng email | Báo "Email này đã có tài khoản" |
| R-05 | — | Gửi đăng ký 6 lần trong 1 giờ từ cùng IP | Bị chặn tần suất (5 lần/giờ) |

SQL đối chiếu:

```sql
SELECT Email, ApprovedAt FROM AspNetUsers WHERE Email = '...';
SELECT r.Name
FROM AspNetUserRoles ur
JOIN AspNetRoles r ON r.Id = ur.RoleId
JOIN AspNetUsers u ON u.Id = ur.UserId
WHERE u.Email = '...';
```

## 2. Mời khách thuê và phạm vi nhiều chủ trọ

| Mã | Vai trò | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| I-01 | CT | `/tenants` → "Mời khách thuê" → email thật | Có bản ghi `TenantInvitations` + email chứa link `/invite/{token}` |
| I-02 | — | Mở link, đặt mật khẩu | Tạo tài khoản Khách thuê + hồ sơ; `TenantOwnerLinks` có 1 dòng |
| I-03 | — | Mở lại link cũ | Báo "lời mời đã được sử dụng" |
| I-04 | CT khác | Đăng nhập, mở `/tenants` | **Không** thấy hồ sơ của I-02 |
| I-05 | CT khác | Tạo hợp đồng cho khách I-02 tại tòa của mình | CT đó **thấy** hồ sơ; `TenantOwnerLinks` có thêm 1 dòng |
| I-06 | — | Link quá 72 giờ | Báo "lời mời đã hết hạn" |

SQL đối chiếu:

```sql
SELECT Id, Email, ExpiresAt, AcceptedAt, RevokedAt FROM TenantInvitations;
SELECT TenantId, OwnerId FROM TenantOwnerLinks WHERE TenantId = ...;
```

## 3. Ghi chú

- Nếu chưa cấu hình SMTP, link mời xuất hiện trong log terminal với tiền tố `[MAIL][NoOp]`.
- Hồ sơ khách ↔ chủ trọ là **nhiều-nhiều**: một khách có thể thuê của nhiều chủ trọ, mỗi chủ trọ thấy hồ sơ mà mình có liên kết.
- Xác nhận email khi đăng ký chủ trọ là tuỳ chọn; điều kiện bắt buộc để đăng nhập là **Admin duyệt**.
