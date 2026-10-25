# Secrets và biến môi trường

Không commit secret. Dev dùng User Secrets, server dùng biến môi trường.

## Xem đang có gì

```bash
dotnet user-secrets list --project NhaTro/Web
```

Trống là chưa có gì.

## Admin đầu tiên (VHT-6 3b)

```bash
dotnet user-secrets set "AdminSeed:Email" "admin-cua-ban@example.com" --project NhaTro/Web
dotnet user-secrets set "AdminSeed:Password" "mat-khau-manh" --project NhaTro/Web
```

F5 chạy app 1 lần, thấy log báo tạo Admin đầu tiên là xong.
Kiểm database: bảng `AspNetUsers` có mail đó, bảng `AspNetUserRoles` gắn role `Admin`.

## User mẫu 4 vai trò (dev)

```bash
dotnet user-secrets set "DevAccounts:Password" "mat-khau-manh" --project NhaTro/Web
```

Chỉ chạy ở Development. Thiếu là bỏ qua, chỉ gieo role.

## Khoá Data Protection (VHT-27)

Khoá dùng để mã hoá cookie đăng nhập và token CAPTCHA. Nếu mất khoá, mọi người dùng bị đăng xuất và
token CAPTCHA cũ không giải mã được.

- Đường dẫn lấy từ cấu hình `Security:DataProtectionKeysPath` (không hard-code trong code), mặc định
  `%LOCALAPPDATA%\NhaTro\keys` trên máy dev.
- Trên server phải trỏ tới thư mục **ngoài thư mục publish** và **ổn định qua các lần deploy**, ví dụ:

  ```bash
  # Windows / IIS: đặt biến môi trường
  setx Security__DataProtectionKeysPath "D:\App_Data\NhaTro\keys"
  ```

  hoặc trong `appsettings.Production.json`:
  ```json
  { "Security": { "DataProtectionKeysPath": "D:\\App_Data\\NhaTro\\keys" } }
  ```
- Thư mục này chứa khoá mã hoá → cấp quyền chỉ cho tài khoản chạy app, **không** commit, **không** đưa vào git.
- `SetApplicationName("NhaTro.Web")` cố định để khoá không đổi khi tên thư mục deploy thay đổi.

## Server (IIS)

Đặt biến môi trường `AdminSeed__Email` và `AdminSeed__Password` trên server.
