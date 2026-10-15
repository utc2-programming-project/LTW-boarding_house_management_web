# Localization dùng chung — VHT-20

## Ngôn ngữ và nơi lưu lựa chọn

Ứng dụng hỗ trợ `vi-VN` và `en-US`, mặc định `vi-VN`. `Program.cs` chỉ đăng ký `CookieRequestCultureProvider`: khi chưa có cookie hợp lệ thì dùng VI, không lấy ngôn ngữ từ query string hay `Accept-Language`.

Selector VI/EN trên `AppTopbar` dùng JavaScript trong `wwwroot/js/layout.js` gọi `POST /culture/set?culture=en-US` (hoặc `vi-VN`) cùng origin rồi reload trang để tạo request/circuit Blazor với culture mới. Trong lúc chờ, selector khóa nút và thông báo đang tải; nếu request thất bại, hiện lỗi đã dịch và cho thử lại. Không gán culture mặc định toàn tiến trình.

Endpoint của VHT-6 chỉ nhận hai culture trên, trả 400 cho giá trị khác; lưu cookie `.AspNetCore.Culture` một năm, HttpOnly, SameSite=Lax và Secure khi HTTPS. `GET /culture/current` trả culture hiện tại và danh sách hỗ trợ. Điểm tích hợp với Tính: VHT-20 cấu hình rõ provider chỉ dùng cookie, thay cho danh sách provider mặc định của framework.

Cookie giữ lựa chọn qua reload và đăng nhập/đăng xuất Identity trên cùng trình duyệt. Identity vẫn dùng static SSR để xử lý cookie qua HTTP. Không lưu ngôn ngữ vào user/database và không đồng bộ đa thiết bị. Các browser context có cookie riêng độc lập; các tài khoản dùng cùng browser context dùng chung lựa chọn ngôn ngữ.

## Dùng resource

Marker là `Web.Shared` tại `NhaTro/Web/Shared.cs`. Với `ResourcesPath = "Resources"`, hai file tương ứng là:

- `NhaTro/Web/Resources/Shared.vi-VN.resx`
- `NhaTro/Web/Resources/Shared.en-US.resx`

Ví dụ Razor (namespace và localization đã có trong `_Imports.razor` chung):

```razor
@inject IStringLocalizer<Shared> L

<button type="button">@L["Restore"]</button>
```

Resource chung dành cho layout và nhãn dùng lại. Mỗi tác giả chịu trách nhiệm dịch màn hình của mình: dùng key có nghĩa, bổ sung đồng thời hai bản VI/EN, giữ nguyên placeholder giữa hai ngôn ngữ; dùng resource riêng theo feature khi cần, theo cùng cấu hình ResourcesPath. Không ghép câu từ nhiều mảnh dịch hoặc dùng bản dịch làm giá trị trạng thái/role lưu trong dữ liệu. Bản dịch Identity đầy đủ thuộc VHT-8.

### Khóa bổ sung D1–D17

| Key | VI | EN |
| --- | --- | --- |
| AccountStatusPendingApproval | Chờ duyệt | Pending approval |
| AccountStatusActive | Hoạt động | Active |
| AccountStatusLocked | Khóa | Locked |
| Reject | Từ chối | Reject |
| CancelRequest | Hủy yêu cầu | Cancel request |
| RoleAdmin | Admin | Admin |
| Trash | Thùng rác | Trash |
| Restore | Khôi phục | Restore |

`Trash` đã có từ layout; các key này chỉ là nhãn, không triển khai nghiệp vụ duyệt/khóa/khôi phục hoặc thay đổi quyền.

## Định dạng của VHT-10

Tiếp tục dùng `MoneyFormatter` và `DateFormatter` tại `Web/Common/Utils`, theo [quy ước ngày, tiền, giờ](../quy_uoc_ngay_tien_gio.md). Ví dụ `MoneyFormatter.Format(1500000m)` luôn trả `1.500.000 ₫` ở cả VI/EN. Đổi ngôn ngữ chỉ đổi chữ giao diện, không đổi tiền, tỷ giá hay dữ liệu đầu vào. Không tự định dạng bằng culture UI.

## Kiểm tra khi tích hợp

Rà bộ key hai ngôn ngữ và kiểm tra resource resolve qua `IStringLocalizer<Shared>`; kiểm tra selector ở cả layout interactive và trang Identity static SSR. Dùng hai browser context để kiểm chứng cô lập cookie. Kiểm tra reload/login/logout và trường hợp API đổi culture thất bại. Các mục này là hướng dẫn kiểm tra, không phải xác nhận đã chạy thành công.
