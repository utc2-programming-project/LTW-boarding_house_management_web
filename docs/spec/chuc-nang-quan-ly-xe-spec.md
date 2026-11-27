# SPEC — Quản lý xe (bãi xe & mọi loại phương tiện)

> Trạng thái: **đặc tả, chưa code**. Phạm vi v1, thiết kế để mở rộng.
> Đã đối chiếu pháp lý & kỹ thuật ngày 20/11/2026 — nguồn ở mục 2.
> Nền: NhaTro (Blazor .NET 10) — vai trò `ChuTro`, `QuanLy`, `KhachThue`.
> Liên quan: [Ra vào cổng](chuc-nang-ra-vao-cong-spec.md) · [Trợ lý AI](chuc-nang-tro-ly-ai-spec.md) · [Tìm kiếm toàn cục](chuc-nang-tim-kiem-toan-cuc-spec.md)

## 0. Quy ước chung

| Mục | Quy ước |
|---|---|
| Nguyên tắc ghi dữ liệu | Mọi thao tác ghi qua service nghiệp vụ hiện có + audit |
| Phạm vi dữ liệu | Chủ trọ = nhà mình; Quản lý = nhà được giao; Khách thuê = xe của mình |
| Thông báo | `INotificationService.NotifyAsync(userId, title, body, link)` |
| Mở rộng | Điểm cắm riêng (provider/config/adapter) — thêm không phá lõi |

## 1. Quản lý xe (bãi xe & mọi loại phương tiện)

> Quản lý **mọi loại xe** gắn với người thuê: xe đạp, xe đạp điện, xe máy, xe máy điện, **ô tô (chỉ khi khu đỗ đủ chỗ)**, loại khác. Xe là "chứng thư thứ hai" dùng chung hạ tầng cổng ở [Ra vào cổng](chuc-nang-ra-vao-cong-spec.md): QR thẻ xe, camera biển số (ANPR), barrier, nhật ký In/Out — không dựng hệ thống riêng.

### 1.1 Mục tiêu

- Danh sách xe theo phòng/khách, có biển số (hoặc mã thẻ với xe không biển) và ảnh; xe lạ không vào bãi.
- Vào/ra **đồng bộ với chức năng ra vào cổng**; biết chính xác xe nào đang trong bãi.
- Thu phí gửi xe đúng loại xe, đúng kỳ, tự vào hóa đơn.
- Ô tô chỉ nhận khi khu đỗ còn chỗ; cảnh báo an toàn xe điện/sạc (PCCC).

### 1.2 Phạm vi v1

| # | Việc | Cách làm |
|---|---|---|
| X1 | Đăng ký xe | Khách tự khai hoặc chủ trọ nhập: loại xe (xe đạp / xe đạp điện / xe máy / xe máy điện / ô tô / khác), biển số (xe đạp có thể không biển → cấp mã thẻ), màu, hãng, ảnh xe, cà vẹt (tuỳ chọn); giới hạn số xe/phòng theo loại |
| X2 | Duyệt xe | Chủ trọ/Quản lý duyệt đối chiếu phòng + hợp đồng; từ chối kèm lý do |
| X3 | Kiểm soát ra vào xe | Mức A: QR thẻ xe trên app quét tại kiosk. Mức B: camera biển số (ANPR) + barrier. Ô tô có thể yêu cầu thêm **định danh người lái** (phiếu cổng/QR cư dân) theo cấu hình |
| X4 | Xe khách vãng lai | Gắn phiếu vào cổng + biển số + hạn dùng; hết hạn tự loại; ô tô khách chỉ nhận khi zone còn chỗ |
| X5 | Phí gửi xe | Bảng giá theo loại xe + khu vực; tháng (mặc định), ngày hoặc lượt; tự thêm dòng vào hóa đơn kỳ |
| X6 | Khu vực & sức chứa | Khu theo loại xe (xe đạp/xe máy/ô tô), trong nhà/ngoài trời; zone ô tô = 0 nghĩa là **không nhận ô tô**; cảnh báo sắp đầy/vượt |
| X7 | Ghép lượt vào/ra | Biết xe đang trong bãi, chống ghi nhận vào trùng, cảnh báo xe vào quá X giờ chưa ra |
| X8 | Xe điện & sạc | Ghi phiên sạc (xe máy điện, ô tô điện), phí theo kWh/giờ, nhắc giới hạn giờ; cảnh báo sạc trong phòng/qua đêm |
| X9 | Sự cố | Mất xe, hỏng xe, xe lạ chiếm chỗ → tạo `Incident` |

### 1.3 Cách hoạt động

**A. Đăng ký xe:**
1. Khách vào `/my/vehicles`: chọn loại xe, nhập biển số (hệ thống chuẩn hoá: bỏ dấu cách/gạch, chữ hoa, hỗ trợ biển 2 dòng); xe đạp/không biển thì hệ thống cấp **mã thẻ xe** (QR dán lên xe); nhập màu, hãng, ảnh xe; tuỳ chọn số khung/cà vẹt (có thể chia sẻ từ VNeID).
2. Chủ trọ/Quản lý duyệt: đối chiếu phòng, hợp đồng, giới hạn số xe, còn chỗ theo loại → `Active`.
3. Trả xe hoặc hợp đồng kết thúc → `Inactive` (giữ lịch sử, không xoá).

**B. Xe vào/ra (dùng chung cổng):**
1. Xác thực xe: QR thẻ xe (mức A) → hoặc biển số khớp danh sách (mức B) → hoặc bảo vệ đối chiếu. Ô tô: nếu bật, kiểm tra thêm người lái có phiếu/QR hợp lệ.
2. Server kiểm tra: xe `Active`, đúng nhà, đúng loại được phép vào zone, còn chỗ (số xe đang trong bãi + sức chứa), hạn phiếu (xe khách), phí còn hiệu lực (mặc định chỉ **cảnh báo** nợ phí, không chặn — cấu hình được).
3. Hợp lệ → mở barrier/cổng, ghi `GateLog` (`SubjectType = Vehicle`) kèm ảnh biển số, thời điểm, hướng; **ghép cặp In/Out** để tính xe đang trong bãi.

**C. Xe lạ (kể cả ô tô):** biển không khớp → chụp ảnh + thông báo bảo vệ/chủ trọ → cho **đăng ký tạm tại chỗ** (biển số, phòng/người liên hệ, thời hạn) nếu còn chỗ, hoặc từ chối.

**D. Phí gửi xe:** job kỳ tính `số xe Active × đơn giá` (theo loại xe + khu vực) → thêm dòng "Phí gửi xe" vào hóa đơn kỳ qua service hóa đơn hiện có. Xe đạp có thể đặt phí 0. Trả xe giữa kỳ theo quy tắc cấu hình (giữ nguyên tháng hoặc tính theo ngày). Miễn/giảm theo phòng/khách có ghi lý do.

**E. Ô tô:** chỉ hoạt động khi `ParkingZone` khai báo sức chứa ô tô > 0; có thể giới hạn khung giờ vào/ra và số ô tô mỗi phòng; ưu tiên xếp ô đỗ cụ thể (mở rộng). Khách đi ô tô vào cổng dùng phiếu khách hoặc đăng ký tạm như X4.

**F. Sạc xe điện:** khách chọn khu sạc, bấm bắt đầu/kết thúc (hoặc chủ trọ nhập thủ công) → phí theo kWh/giờ → nhắc quá giờ; cảnh báo nếu khai sạc trong phòng/qua đêm. App chỉ ghi nhận và nhắc an toàn, không điều khiển ổ sạc (v1).

### 1.4 Trạng thái

- Xe: `Pending` → `Active` → `Suspended` (nợ phí/vi phạm) → `Inactive` (trả xe/hết hợp đồng) / `Lost` (báo mất, đang điều tra).
- Lượt xe: `In` → `Out` (ghép cặp), `Unmatched` khi không khớp; xe quá X giờ chưa ra → cảnh báo.
- Phiên sạc: `Running` → `Ended`.

### 1.5 Dữ liệu cần thêm

| Thực thể | Trường cốt lõi |
|---|---|
| `Vehicle` | Id, PropertyId, RoomId, TenantId?, ContractId?, GatePassId? (xe khách tạm), VehicleType (`Bicycle`/`ElectricBicycle`/`Motorbike`/`ElectricMotorbike`/`Car`/`Other`), PlateNumber? (đã chuẩn hoá), VehicleTagCode? (xe không biển), Brand?, Color?, PhotoRef?, RegistrationDocRef?, Status, ValidFrom/To, ApprovedByUserId, Note, RowVersion |
| `ParkingZone` | Id, PropertyId, Tên khu, Trong nhà (bool), Loại xe được phép, Sức chứa theo loại (0 = không nhận loại đó), Đang dùng, FeePlanId? |
| `ParkingFeePlan` | Id, PropertyId, VehicleType, MonthlyFee, DailyFee?, EffectiveFrom, Ghi chú (hoặc tái dùng `ServiceCatalog` + `PriceHistory` nếu mô hình dịch vụ phù hợp) |
| `ChargingSession` | Id, VehicleId, ZoneId, StartAt, EndAt?, Kwh?, Fee, Status |
| `GateLog` (mở rộng) | Thêm `SubjectType` (`Person`/`Vehicle`), `VehicleId?`, `PlateSnapshot?`, `MatchScore?` — dùng chung nhật ký với cổng, không tạo log thứ hai |

Tái dùng: `Tenant`, `Contract`, `Room`, `Property`, `GateDevice`, `GatePolicy`, `StoredFile`, `AppNotification`, `Incident`, hóa đơn (dòng phí), `AuditLog`.

### 1.6 Phân quyền

| Hành động | Chủ trọ | Quản lý | Khách thuê | Khách vãng lai |
|---|---|---|---|---|
| Cấu hình khu vực, sức chứa, bảng giá | ✔ | – | – | – |
| Duyệt/từ chối/khoá xe | ✔ | Theo cấu hình | – | – |
| Đăng ký xe của mình | – | – | ✔ (chờ duyệt) | Theo phiếu nếu được bật |
| Đăng ký tạm xe khách tại cổng | ✔ | Theo cấu hình | – | – |
| Xem danh sách xe | ✔ | Nhà được giao | Xe của mình | – |
| Xem nhật ký ra/vào | ✔ | Nhà được giao | Xe của mình | – |
| Báo mất/hỏng xe | ✔ | Theo cấu hình | ✔ (xe của mình) | – |
| Ghi nhận phiên sạc | ✔ | Theo cấu hình | ✔ (xe của mình) | – |

### 1.7 Bảo mật & an toàn

- **Biển số, ảnh xe, ảnh biển số, mã thẻ xe là dữ liệu cá nhân** (Luật BVDLCN 2025): chỉ dùng cho mục đích giữ xe/an ninh; ảnh ANPR tự xoá sau N ngày (mặc định 30), không chia sẻ ngoài trừ yêu cầu hợp pháp của cơ quan chức năng.
- Camera chỉ hướng vào khu đỗ/lối ra vào, không hướng vào phòng ở; không lưu ảnh người lái nếu không bật kiểm tra định danh.
- **PCCC xe điện** (xe máy điện và ô tô điện): theo NĐ 105/2025/NĐ-CP (hiệu lực 01/7/2025), khu sạc trong nhà/hầm phải có giải pháp ngăn cháy và có thể thuộc diện thẩm duyệt thiết kế PCCC; không có giải pháp ngăn cháy cho khu sạc tập trung trong nhà bị phạt tham khảo 40–50 triệu. App nhắc: sạc khu vực riêng/thoáng, không sạc trong phòng/qua đêm — không thay trách nhiệm của chủ trọ.
- Không dùng nhận diện khuôn mặt cho xe; nhận diện người vẫn theo [Ra vào cổng](chuc-nang-ra-vao-cong-spec.md).

### 1.8 Liên hệ hệ thống

| Module | Liên hệ |
|---|---|
| Ra vào cổng | Xe là subject trong `GateLog`; dùng chung kiosk/đầu đọc/barrier; QR thẻ xe là một loại chứng thư; ANPR là một phương thức của cổng; ô tô có thể ghép thêm phiếu người lái |
| Hợp đồng | Điều kiện `Active`/`Inactive` của xe theo hợp đồng; giới hạn số xe/phòng theo loại |
| Hóa đơn | Phí gửi xe là dòng trong hóa đơn kỳ; điều chỉnh/miễn giảm theo quy tắc |
| Dịch vụ & giá | Tái dùng `ServiceCatalog`/`PriceHistory` nếu muốn quản lý phí như một dịch vụ |
| Sự cố | Mất xe/hỏng xe → `Incident` có gắn biển số/mã thẻ |
| Thông báo | Xe lạ, xe bị khoá, sắp hết hạn phí, xe chưa ra quá giờ, sạc quá giờ |
| Dashboard | Số xe đang gửi theo loại, công suất bãi, doanh thu phí gửi xe |
| Tìm kiếm | Tìm theo biển số/mã thẻ, tên khách, phòng — xem [Tìm kiếm](chuc-nang-tim-kiem-toan-cuc-spec.md) |
| AI | AI tóm tắt bãi xe/thu phí trong báo cáo (U1) — xem [Trợ lý AI](chuc-nang-tro-ly-ai-spec.md) |

### 1.9 Mở rộng sau

Bản đồ ô đỗ (slot) và đặt chỗ ô tô theo giờ, thẻ RFID dán xe, barrier tự động hoàn toàn, điều khiển ổ sạc, đối soát cà vẹt qua VNeID, phí theo lượt cho khách ngoài, báo cáo mất xe xuất biên bản.

## 2. Tham chiếu (kiểm chứng 20/11/2026)

| Chủ đề | Nguồn |
|---|---|
| Camera nhận diện biển số (ANPR) cho bãi xe | [Hikvision Việt Nam](https://hikvisionvietnam.net/blog-hikvision-huong-dan-su-dung-tu-van-camera-hikvision/dong-camera-chuyen-dung-nhan-dang-bien-so-xe.html), [MegaParking](https://megaparking.vn/camera-chup-bien-so-hikvision-giai-phap-bai-dau-xe-thong-minh/) |
| PCCC khu sạc xe điện (NĐ 105/2025/NĐ-CP, hiệu lực 01/7/2025) | [LuatVietnam](https://luatvietnam.vn/an-ninh-trat-tu/nghi-dinh-105-2025-nd-cp-cua-chinh-phu-quy-dinh-chi-tiet-mot-so-dieu-va-bien-phap-thi-hanh-luat-phong-chay-chua-chay-va-cuu-nan-cuu-ho-400511-d1.html) |
| Phạt khu sạc xe điện trong nhà không có giải pháp ngăn cháy (40–50 triệu) | [Thư Viện Nhà Đất](https://thuviennhadat.vn/phap-ly-nha-dat/khong-co-giai-phap-ngan-chay-cho-khu-vuc-sac-xe-dien-bi-phat-bao-nhieu-tien-theo-nghi-dinh-106-687200.html) |
| VNeID tích hợp giấy đăng ký xe (cà vẹt) | [LuatVietnam](https://luatvietnam.vn/giao-thong/cach-tich-hop-ca-vet-xe-vao-vneid-863-99594-article.html) |
| Luật BVDLCN 2025 (91/2025/QH15) | [Cổng TTĐT Chính phủ](https://chinhphu.vn/?pageid=27160&docid=214590&classid=1&typegroupid=3) |

> Mức phạt và điều kiện PCCC có thể thay đổi; khi triển khai thật cần đối chiếu văn bản hiện hành.
