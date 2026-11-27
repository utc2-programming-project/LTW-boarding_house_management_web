# SPEC — Trợ lý AI (AI Assistant)

> Trạng thái: **đã triển khai U1–U5; bổ sung U6–U8 ngày 21/11/2026**. Phạm vi v1, thiết kế để mở rộng.
> Đã đối chiếu pháp lý & kỹ thuật ngày 20/11/2026 — nguồn ở mục 8.
> Nền: NhaTro (Blazor .NET 10) — vai trò `ChuTro`, `QuanLy`.
> Liên quan: [Ra vào cổng](chuc-nang-ra-vao-cong-spec.md) · [Quản lý xe](chuc-nang-quan-ly-xe-spec.md) · [Tìm kiếm toàn cục](chuc-nang-tim-kiem-toan-cuc-spec.md)

## 0. Quy ước chung

| Mục | Quy ước |
|---|---|
| Nguyên tắc ghi dữ liệu | AI **không ghi** dữ liệu nghiệp vụ; chỉ tạo đề xuất, server thực thi sau khi duyệt |
| Phạm vi dữ liệu | Chỉ đọc dữ liệu trong scope của người đang dùng (`PropertyScope`, `RoomScope`, `TenantScope`…) |
| Thông báo | `INotificationService.NotifyAsync(userId, title, body, link)` |
| Mở rộng | Mỗi use case = một schema + một applier; thêm không phá lõi |

## 1. Nguyên tắc cốt lõi

1. AI **chỉ tạo bản nháp/đề xuất**, không có quyền tạo/sửa/gửi/xoá dữ liệu nghiệp vụ.
2. Mọi đề xuất phải qua **duyệt của Chủ trọ** trước khi hệ thống thực thi — việc thực thi do code server làm, qua service nghiệp vụ hiện có (validation giữ nguyên).
3. Mọi đề xuất có nhãn "Bản nháp do AI tạo" + ghi audit: ai yêu cầu, dữ liệu đầu vào nào, model nào, ai duyệt.
4. AI chỉ đọc dữ liệu **trong phạm vi quyền của người đang dùng**.

## 2. Phạm vi v1

| # | Việc AI làm | Đầu vào | Đầu ra (nháp) | Nơi duyệt |
|---|---|---|---|---|
| U1 | Tóm tắt & tối ưu báo cáo (doanh thu, công nợ, sự cố, bãi xe) | Số liệu từ `/reports`, `/home` theo kỳ | Bản tóm tắt + gợi ý hành động | Chủ trọ đọc, có thể lưu/xuất |
| U2 | Soạn tin nhắc việc (nhắc nợ, nhắc hạn cọc, thông báo chung) | Hợp đồng/hóa đơn liên quan | Nội dung thông báo | Chủ trọ duyệt → mới gửi |
| U3 | Tạo nháp phòng/nhà từ mô tả tự nhiên | Câu mô tả ("nhà 5 phòng, 2tr5/phòng, có gửi xe…") | Form phòng/nhà điền sẵn | Chủ trọ duyệt → tạo qua `RoomService`/`PropertyService` |
| U4 | Gợi ý phân loại & ưu tiên sự cố | Nội dung sự cố, phòng, lịch sử | Loại, mức ưu tiên, người phụ trách | Quản lý/Chủ trọ xác nhận |
| U5 | Tóm tắt việc cần làm trong ngày | Việc đến hạn: hóa đơn, sự cố, yêu cầu duyệt | Danh sách gọn + link | Chỉ hiển thị, không ghi |
| U6 | Tạo nháp hồ sơ khách thuê từ mô tả tự nhiên | Câu mô tả (họ tên, SĐT, email, ngày sinh, giới tính, địa chỉ) | Hồ sơ khách điền sẵn (không CCCD/số tài khoản) | Chủ trọ duyệt → tạo qua `ITenantService` |
| U7 | Tạo nháp danh mục dịch vụ + giá ban đầu | Câu mô tả (tên dịch vụ, đơn vị, cách tính, đơn giá, ngày hiệu lực) + nhà trọ | Danh mục dịch vụ + phiên bản giá | Chủ trọ duyệt → `IServiceCatalogService` |
| U8 | Tạo nháp hợp đồng thuê | Phòng đang trống + hồ sơ khách trong phạm vi + mô tả thời hạn/giá/cọc | Phòng, khách (id thật), thời hạn, giá thuê, cọc, kỳ thanh toán | Chủ trọ duyệt → `IContractService` (tạo ở trạng thái nháp) |

### 2.1. Chi tiết U6–U8 (bổ sung 21/11/2026)

**U6 — Nháp hồ sơ khách thuê (`TenantDraft`)**

| Mục | Nội dung |
|---|---|
| Đầu vào | Câu mô tả của Chủ trọ/Quản lý (≥ 3 ký tự) + danh sách nhà trong phạm vi |
| Đầu ra JSON | `fullName`, `phoneNumber`, `email?`, `dateOfBirth?` (yyyy-MM-dd), `gender?` (`Male`/`Female`/`Other`), `permanentAddress?`, `note?` |
| Kiểm duyệt | Họ tên 1–200; SĐT 8–15 chữ số; email hợp lệ nếu có; ngày đúng định dạng; **không có trường CCCD/tài khoản** |
| Áp dụng | `ITenantService.CreateAsync` với `TenantCaller` của người duyệt, `ConfirmDuplicatePhone = false`; trùng SĐT ⇒ báo lỗi, không tạo |
| Phạm vi | Service tự kiểm quyền; hồ sơ tạo bởi Chủ trọ được gắn `TenantOwnerLink` để dùng tiếp cho U8 |

**U7 — Nháp danh mục dịch vụ (`ServiceDraft`)**

| Mục | Nội dung |
|---|---|
| Đầu vào | Câu mô tả + **một nhà trọ** (chọn trên form; nếu chỉ có 1 nhà trong phạm vi thì tự chọn) |
| Đầu ra JSON | `name`, `unit` (phòng/người/xe/lượt/kWh/m³), `calculationMethod` (`fixed-monthly`/`meter`/`tiered`/`custom`), `unitPrice`, `effectiveDate`, `effectiveTo?`, `note?`, `description?` |
| Kiểm duyệt | Tên 1–200; đơn vị 1–30; đơn giá 1–10¹²; ngày đúng định dạng và `effectiveTo > effectiveDate` |
| Áp dụng | `IServiceCatalogService.CreateOrUpdateServicePriceAsync` (tạo danh mục + phiên bản giá đầu tiên); giữ nguyên mọi kiểm tra phạm vi nhà |
| Ghi chú | Điện/nước hệ thống là biểu giá chỉ đọc; AI không sửa các danh mục `IsReadOnly` |

**U8 — Nháp hợp đồng thuê (`ContractDraft`)**

| Mục | Nội dung |
|---|---|
| Đầu vào | Câu mô tả + danh sách phòng **Available và chưa có hợp đồng chờ/đang hiệu lực** (id, mã, giá chuẩn) và hồ sơ khách **trong phạm vi** (`TenantVisibility`) |
| Đầu ra JSON | `roomId` (bắt buộc, id thật), `roomCode?`, `tenantIds` (1–10 id thật, không trùng), `startDate`, `endDate`, `monthlyRent?`, `depositAmount?`, `paymentCycleMonths` (1/2/3/6/12), `meterReadingDay?` (1–28), `terms?` |
| Kiểm duyệt | `endDate > startDate`; id phòng/khách phải nằm trong ngữ cảnh; các rule nghiệp vụ còn lại do `IContractService` chặn (kể cả phòng vừa bị chiếm sau khi tạo nháp) |
| Áp dụng | `IContractService.CreateContractAsync` với `AsDraft = true` — hợp đồng vào **trạng thái nháp** để đi tiếp luồng gửi/duyệt hiện có, không tự kích hoạt |
| Ghi chú | PM có thể kết hợp U6 → U8: tạo hồ sơ khách trước, duyệt, rồi lập hợp đồng cho khách đó. Phòng có hợp đồng Pending/Approved/AwaitingTenant/ChangeRequested/TenantAccepted bị loại khỏi ngữ cảnh dù còn mang trạng thái Available. Danh sách khách trong ngữ cảnh tối đa 200, ưu tiên khách có họ tên/số điện thoại xuất hiện trong câu lệnh; nếu tên gọi khớp nhiều khách, AI không tự chọn mà báo *"Chưa xác định được khách thuê — hãy ghi rõ họ tên đầy đủ hoặc số điện thoại của khách."* Thẻ nháp hiển thị tên khách thuê đã chọn |

**Tạo tài khoản đăng nhập — ngoài phạm vi AI (cố định)**

AI **không** tạo tài khoản đăng nhập, không sinh/không lưu mật khẩu, không gán vai trò. Lý do: tài khoản là quyền truy cập, mật khẩu ban đầu là bí mật không được nằm trong `AiSuggestion.OutputJson`, audit hay log; đây cũng là hành động đặc quyền cần người thật chịu trách nhiệm. Sau khi U6 được duyệt, Chủ trọ dùng luồng có sẵn:

| Việc | Nơi làm | Cách |
|---|---|---|
| Mời khách tự tạo tài khoản | `/tenants` → nút **Mời**/QR | Gửi lời mời email hoặc QR; khách tự đặt mật khẩu |
| Tạo tài khoản (có mật khẩu ban đầu) | `/admin/users` → **Tạo tài khoản** | Chủ trọ/Admin nhập email, vai trò, mật khẩu; chọn hồ sơ khách nếu vai trò Khách thuê |

## 3. Cách hoạt động

1. Người dùng mở trợ lý (nút theo ngữ cảnh: `Tóm tắt bằng AI` ở `/reports`, `Tạo nháp bằng AI` ở `/rooms`, hoặc trang `/assistant`) và nhập yêu cầu.
2. Server gom dữ liệu **đúng scope**, che PII nhạy cảm (số CCCD, số tài khoản đầy đủ) rồi gọi model.
3. AI trả kết quả **có cấu trúc** theo JSON Schema của từng use case, ép bằng chế độ structured outputs của model (không trả văn bản tự do cho U2–U4); sai schema = từ chối đề xuất.
4. Lưu `AiSuggestion` trạng thái `Pending`; hiển thị cho Chủ trọ dạng **xem trước / diff**.
5. Chủ trọ: **Duyệt** (có thể sửa trước) / **Từ chối** (kèm lý do). Duyệt ⇒ backend áp dụng bằng service thật, ghi audit, gửi thông báo liên quan.
6. Quản lý dùng được AI nhưng đề xuất của Quản lý đi qua luồng duyệt như thao tác Quản lý hiện nay; Chủ trọ là người chốt.

## 4. Dữ liệu cần thêm

| Thực thể | Trường cốt lõi |
|---|---|
| `AiSuggestion` | Id, UseCase (**U1–U8**), RequestedByUserId, ScopeJson (nhà/phòng liên quan), InputRefs (id bản ghi đã dùng), OutputJson, Status (`Pending`/`Approved`/`Rejected`/`Applied`), DecidedByUserId, DecidedAt, DecisionReason, AppliedRefId, RowVersion |
| `AiUsageLog` | Id, UserId, UseCase, Model, Tokens/Cost, Thành công/Thất bại, Thời điểm (phục vụ hạn mức & chi phí) |

Tái dùng: giao diện duyệt và thông báo như module Approvals (thêm tab "Đề xuất AI"), `AuditLog`.

`UseCase` lưu dạng số nguyên và `OutputJson` là JSON tự do theo schema từng use case, nên thêm U6–U8 **không cần migration mới**; chỉ cần schema + validator + applier + nhãn giao diện.

## 5. Ràng buộc an toàn

| Ràng buộc | Cách làm |
|---|---|
| Không tự ghi | AI không có DB write, không có tool ghi; chỉ trả JSON đề xuất |
| Không lộ dữ liệu | Chỉ gửi dữ liệu trong scope + che CCCD/số tài khoản; không gửi tệp giấy tờ. SĐT/email được giữ lại vì cần cho việc tạo/nhắc khách và do chủ trọ chủ động nhập |
| Không tự gửi | Gửi thông báo/hợp đồng chỉ sau khi duyệt |
| Tài khoản đăng nhập | AI không tạo tài khoản, không sinh/không lưu mật khẩu, không gán vai trò; dùng luồng Mời/QR hoặc `/admin/users` (mục 2.1) |
| Dữ liệu ra nước ngoài | Nếu dùng model cloud nước ngoài: lập hồ sơ đánh giá tác động chuyển dữ liệu (Luật BVDLCN 2025); v1 chỉ gửi dữ liệu đã che PII, không gửi tệp/ảnh giấy tờ; ưu tiên model nội bộ |
| Không hợp lệ | Đề xuất sai schema/vi phạm rule nghiệp vụ ⇒ bị chặn ở bước duyệt hoặc apply, báo lỗi rõ |
| Lỗi AI | Fail closed: ẩn/không có đề xuất, không ảnh hưởng nghiệp vụ; có nút thử lại |
| Chi phí | Hạn mức theo tài khoản/nhà (màn cài đặt của module AI), rate limit, cache kết quả U1/U5 trong ngày |
| Tuỳ chọn | Chủ trọ bật/tắt trợ lý cho nhà mình (mặc định bật, tắt được toàn bộ) |

## 6. Liên hệ hệ thống

Báo cáo (`/reports`: doanh thu, công nợ, sự cố, phòng, khách thuê, bãi xe) · Dashboard (`/home`) · Phòng & Nhà (`/rooms`, `/properties`) · Khách thuê (`/tenants`) · Dịch vụ (`/services`) · Hợp đồng (`/contracts`) · Hóa đơn/Thanh toán (`/invoices`) · Sự cố (`/incidents`) · Thông báo · Duyệt/Approvals (`/approvals`) · Cài đặt (màn mới của module AI) · Audit.

## 7. Mở rộng sau

Chat hỏi đáp dữ liệu ("tháng này thu được bao nhiêu?"), đối soát thanh toán, dự báo phòng trống, soạn tin đăng phòng, trả lời khách thuê trong ngữ cảnh hợp đồng.

## 8. Tham chiếu (kiểm chứng 20/11/2026)

| Chủ đề | Nguồn |
|---|---|
| Structured Outputs (JSON Schema) | [OpenAI Docs](https://developers.openai.com/api/docs/guides/structured-outputs) |
| Luật BVDLCN 2025 (91/2025/QH15), hiệu lực 01/01/2026 | [Cổng TTĐT Chính phủ](https://chinhphu.vn/?pageid=27160&docid=214590&classid=1&typegroupid=3) |
