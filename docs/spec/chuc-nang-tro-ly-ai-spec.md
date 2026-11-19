# SPEC — Trợ lý AI (AI Assistant)

> Trạng thái: **đặc tả, chưa code**. Phạm vi v1, thiết kế để mở rộng.
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
| `AiSuggestion` | Id, UseCase (U1–U5), RequestedByUserId, ScopeJson (nhà/phòng liên quan), InputRefs (id bản ghi đã dùng), OutputJson, Status (`Pending`/`Approved`/`Rejected`/`Applied`), DecidedByUserId, DecidedAt, DecisionReason, AppliedRefId, RowVersion |
| `AiUsageLog` | Id, UserId, UseCase, Model, Tokens/Cost, Thành công/Thất bại, Thời điểm (phục vụ hạn mức & chi phí) |

Tái dùng: giao diện duyệt và thông báo như module Approvals (thêm tab "Đề xuất AI"), `AuditLog`.

## 5. Ràng buộc an toàn

| Ràng buộc | Cách làm |
|---|---|
| Không tự ghi | AI không có DB write, không có tool ghi; chỉ trả JSON đề xuất |
| Không lộ dữ liệu | Chỉ gửi dữ liệu trong scope + che CCCD/số tài khoản; không gửi tệp giấy tờ |
| Không tự gửi | Gửi thông báo/hợp đồng chỉ sau khi duyệt |
| Dữ liệu ra nước ngoài | Nếu dùng model cloud nước ngoài: lập hồ sơ đánh giá tác động chuyển dữ liệu (Luật BVDLCN 2025); v1 chỉ gửi dữ liệu đã che PII, không gửi tệp/ảnh giấy tờ; ưu tiên model nội bộ |
| Không hợp lệ | Đề xuất sai schema/vi phạm rule nghiệp vụ ⇒ bị chặn ở bước duyệt hoặc apply, báo lỗi rõ |
| Lỗi AI | Fail closed: ẩn/không có đề xuất, không ảnh hưởng nghiệp vụ; có nút thử lại |
| Chi phí | Hạn mức theo tài khoản/nhà (màn cài đặt của module AI), rate limit, cache kết quả U1/U5 trong ngày |
| Tuỳ chọn | Chủ trọ bật/tắt trợ lý cho nhà mình (mặc định bật, tắt được toàn bộ) |

## 6. Liên hệ hệ thống

Báo cáo (`/reports`: doanh thu, công nợ, sự cố, phòng, khách thuê, bãi xe) · Dashboard (`/home`) · Phòng & Nhà (`/rooms`, `/properties`) · Hóa đơn/Thanh toán (`/invoices`) · Sự cố (`/incidents`) · Thông báo · Duyệt/Approvals (`/approvals`) · Cài đặt (màn mới của module AI) · Audit.

## 7. Mở rộng sau

Chat hỏi đáp dữ liệu ("tháng này thu được bao nhiêu?"), đối soát thanh toán, dự báo phòng trống, soạn tin đăng phòng, trả lời khách thuê trong ngữ cảnh hợp đồng.

## 8. Tham chiếu (kiểm chứng 20/11/2026)

| Chủ đề | Nguồn |
|---|---|
| Structured Outputs (JSON Schema) | [OpenAI Docs](https://developers.openai.com/api/docs/guides/structured-outputs) |
| Luật BVDLCN 2025 (91/2025/QH15), hiệu lực 01/01/2026 | [Cổng TTĐT Chính phủ](https://chinhphu.vn/?pageid=27160&docid=214590&classid=1&typegroupid=3) |
