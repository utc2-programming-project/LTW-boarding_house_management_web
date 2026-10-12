# Quy ước ngày, tiền, giờ

Cả nhóm chốt một kiểu và dùng chung một bộ hàm, không ai tự hiện theo kiểu riêng. Code nằm ở `Web/Common/Utils` (định dạng) và `Web/Infrastructure/Time` (múi giờ).

## Tiền

- Mọi chỗ hiện giống nhau, ví dụ `1.500.000 ₫`.
- Đổi web sang tiếng Anh thì vẫn là tiền Việt, không biến thành đô la, dấu chấm dấu phẩy cũng giữ nguyên.
- Tiền lẻ chỉ có một cách làm tròn: tròn về số nguyên, đúng .5 thì làm tròn lên (1234,5 → 1235). Khi tính hóa đơn thì làm tròn từng dòng rồi mới cộng.
- Chỉ dùng `MoneyFormatter`, đừng tự viết `ToString("N0")` hay `Math.Round` riêng.

## Giờ

- Trong database lưu giờ quốc tế (UTC).
- Lúc hiện cho người Việt xem thì cộng 7 giờ (giờ Việt Nam).
- Cần biết "hôm nay" thì lấy từ `TimeProvider.TodayVietnam()`, không dùng `DateTime.Now`. Ví dụ 00:30 ngày 11/10 giờ VN là 17:30 ngày 10/10 theo UTC, lấy nhầm là lệch một ngày.

## Phân biệt ngày hợp đồng và lúc bấm nút

- **Ngày ghi trên hợp đồng** (bắt đầu, kết thúc, hạn thanh toán, kỳ hóa đơn) là một ngày trên giấy, ví dụ 12/10/2026. Lưu bằng `DateOnly`, không cộng trừ múi giờ.
- **Lúc bấm nút** (tạo, duyệt, xác nhận thu tiền) là thời điểm chính xác, tới giờ phút giây. Lưu UTC, để sau này có tranh cãi thì còn bằng chứng.

## Cách gọi

```csharp
MoneyFormatter.Format(room.StandardPrice);          // "1.500.000 ₫"
MoneyFormatter.RoundVnd(amount);                    // làm tròn khi tính tiền
MoneyFormatter.TryParse(input, out var amount);     // đọc ô nhập tiền
DateFormatter.Format(contract.StartDate);           // "12/10/2026"
DateFormatter.FormatMonth(invoice.Period);          // "10/2026"
DateFormatter.FormatDateTime(payment.ConfirmedAt);  // UTC -> "11/10/2026 00:30"
_time.TodayVietnam();                               // _time là TimeProvider được tiêm vào
```

Lọc dữ liệu theo một ngày VN thì dùng `VietnamTime.DayRangeUtc(day)`. Dữ liệu trống hiển thị `—`. Test nằm ở `NhaTro.UnitTests/Common/FormattingTests.cs`.