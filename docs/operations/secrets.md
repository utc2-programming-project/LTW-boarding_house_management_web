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

## Server (IIS)

Đặt biến môi trường `AdminSeed__Email` và `AdminSeed__Password` trên server.
