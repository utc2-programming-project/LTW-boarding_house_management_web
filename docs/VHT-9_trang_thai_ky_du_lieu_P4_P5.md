VHT-9 — Chốt trạng thái, kỳ và dữ liệu P4/P5
Web vận hành nhà trọ / Boarding House Operations Web

Đợt: 1B (Sprint 1B) · Phân hệ: P4 Hợp đồng, P5 Điện nước/dịch vụ
Người phụ trách: Đào · Phối hợp: Tính (schema), Phát (tiền thuê/chi phí hóa đơn)
Trạng thái: bản nháp để nhóm chốt các điểm ở mục 7
Nguồn: kế hoạch tuần · timeline song song · thao_tac_va_quyen.xlsx (ma trận #12–#16, #22–#33; công tắc SW-07, SW-08, SW-09; quyết định D1–D17)
1. Mục tiêu và phạm vi
Chốt trường/DTO mẫu, trạng thái và bước chuyển; thống nhất giá, cọc, kỳ, công thức với nhóm trước khi code.

Chỉ làm trong Đợt 1B, phân hệ P4/P5, theo quyết định D1–D17.
Không làm việc ngoài các đợt và phân hệ này.
Bàn giao: bảng trạng thái/chuyển trạng thái, DTO P4/P5, ví dụ tính một kỳ.
Tiêu chí hoàn thành: Tính đủ thông tin để thiết kế SQL; Phát tính ra cùng tổng tiền với Đào trên ví dụ đã chốt.
2. Trạng thái hợp đồng và bước chuyển
Trạng thái: Chờ duyệt, Hiệu lực, Sắp hết hạn, Đã thanh lý, Đã hủy, Đã từ chối. Ba trạng thái cuối (Đã thanh lý, Đã hủy, Đã từ chối) không chuyển tiếp.

Từ	Sang	Ai làm	Điều kiện
(tạo mới)	Chờ duyệt	Quản lý (luôn, D3) hoặc Chủ trọ	Khách và phòng hợp lệ; có ngày, giá, cọc, thành viên. Khách thuê không tạo hợp đồng.
Chờ duyệt	Hiệu lực	Chỉ Chủ trọ	Phòng không có hợp đồng Hiệu lực khác. Chốt snapshot giá, cọc, chỉ số đầu kỳ. Cập nhật phòng cùng transaction. Quản lý không tự duyệt hợp đồng mình tạo.
Chờ duyệt	Đã từ chối	Chỉ Chủ trọ	Bắt buộc nhập lý do.
Hiệu lực	Sắp hết hạn	Hệ thống	Còn ≤ N ngày (N chưa chốt).
Hiệu lực / Sắp hết hạn	Hiệu lực (gia hạn)	Chủ trọ; Quản lý theo SW-07	Ngày kết thúc mới lớn hơn cũ. Giữ lịch sử. Giá mới tạo snapshot mới.
Hiệu lực / Sắp hết hạn	Đã thanh lý	Chủ trọ; Quản lý theo SW-07	Điều kiện đề xuất ở mục 7.
Chờ duyệt, Hiệu lực	Đã hủy	Chưa có trong ma trận	Xem câu hỏi 1, mục 7.
Khi công tắc của Quản lý là Duyệt, các thao tác gia hạn, thanh lý và cọc chưa đổi dữ liệu mà tạo một yêu cầu trong hàng đợi (VHT-96) để Chủ trọ duyệt. Phản hồi vì vậy có kết quả Applied, PendingApproval hoặc Blocked.
Một phòng chỉ có một hợp đồng Hiệu lực hoặc Sắp hết hạn. Phòng và hồ sơ khách còn hợp đồng loại này thì không xóa được (ma trận #15, #20).
Trạng thái kỳ
Trạng thái	Cho phép
Mở	Nhập và sửa chỉ số bình thường.
Đã chốt	Chưa phát hành hóa đơn. Chỉ kỳ liền trước được sửa chỉ số (D10); bắt buộc có lý do và lưu lịch sử (ai, khi nào, cũ → mới, lý do). Quyền sửa: Chủ trọ, hoặc Quản lý theo SW-09. Sau khi sửa, tính lại chi phí snapshot trong cùng transaction (VHT-102).
Đã phát hành hóa đơn	Chặn sửa. Không hủy hóa đơn, không trả một phần (D13).
3. Quy tắc giá và kỳ
Snapshot hợp đồng: khi duyệt hoặc gia hạn, sao chép giá thuê, cọc và các dòng dịch vụ vào hợp đồng. Đổi giá chuẩn sau đó không đổi snapshot (case test VHT-42).
Điện/nước (D7): biểu giá bậc thang theo quy định nhà nước, cấu hình sẵn, hiệu lực theo ngày, snapshot vào chi phí, không có API hay màn hình sửa. Số liệu thật do nhóm xác nhận khi làm VHT-100.
Dịch vụ khác: Chủ trọ thiết lập đơn giá, hiệu lực theo ngày. Mở rộng bằng dòng dịch vụ, không dùng cột phí cố định (VHT-32).
Chỉ số đầu kỳ: kỳ đầu lấy từ lúc duyệt hợp đồng; các kỳ sau lấy chỉ số cuối kỳ trước (VHT-51).
Tiền cọc: ghi nhận khi thu (Chủ trọ; Quản lý theo SW-08). Khách thuê chỉ xem cọc của mình.
Công thức một kỳ: Tổng = tiền thuê + Σ bậc điện + Σ bậc nước + Σ dịch vụ. Cọc không nằm trong hóa đơn tháng.
4. DTO P4/P5
Code DTO nằm trong repo, đây là nguồn chính. Không chép lại vào tài liệu để tránh lệch khi sửa code.

File	Nội dung
NhaTro/Web/Features/Contracts/ContractDtos.cs	ContractStatus, ContractAction, ActionOutcome, ActionResult, ContractMemberDto, ContractHistoryDto, ContractDto, ContractCreateRequest, ContractActionRequest
NhaTro/Web/Features/Utilities/BillingPeriodDtos.cs	BillingPeriodStatus, CostLineType, CostLineDto, IndexCorrectionDto, BillingPeriodDto
NhaTro/Web/Features/Utilities/TariffDtos.cs	TariffType, TariffTierDto, TariffDto, ServiceLineDto, ServicePriceDto
Quy ước:

Tên trong code là tiếng Anh (PendingApproval, Active, ExpiringSoon…); nhãn VI/EN lấy từ resource.
Tiền dùng decimal; chỉ số điện nước dùng decimal.
TariffDto chỉ đọc, không có request tạo hoặc sửa (D7).
ContractDto dùng ServiceLineDto từ Utilities.
5. Ví dụ tính một kỳ
Số minh họa, không phải biểu giá thật. Khi VHT-100 có số thật, thay ví dụ này rồi cho Phát tính lại.

Khoản	Cách tính	Thành tiền (đ)
Tiền thuê		3.000.000
Điện 120 kWh	50×1.800 + 50×2.000 + 20×3.000	250.000
Nước 6 m³	6 × 15.000	90.000
Dịch vụ	Internet 100.000 + rác 30.000	130.000
Tổng kỳ		3.470.000
Cọc ví dụ 3.000.000 không tính vào tổng này. Đào và Phát cùng tính lại; hai bên phải ra cùng một tổng thì mới đạt tiêu chí hoàn thành.

6. Bổ sung D1–D17 (12/10/2026)
Danh sách trạng thái hợp đồng gồm Chờ duyệt, Hiệu lực, Sắp hết hạn, Đã thanh lý, Đã hủy và Đã từ chối (bổ sung vì Chủ trọ được từ chối).
Trạng thái kỳ: Mở / Đã chốt / Đã phát hành hóa đơn.
Quy tắc kỳ liền trước theo D10.
Biểu giá điện/nước cấu hình khóa theo D7.
7. Điểm chưa thống nhất, cần nhóm quyết định
Hủy hợp đồng: "Đã hủy" có trong danh sách trạng thái nhưng ma trận không có thao tác tương ứng. Cần chốt ai hủy, hủy được ở trạng thái nào và cọc xử lý ra sao.
Chủ trọ tự tạo và tự duyệt (D4): đi qua Chờ duyệt rồi duyệt ngay trong cùng thao tác (để có dấu vết), hay vào thẳng Hiệu lực? Đề xuất cách thứ nhất.
Ngưỡng Sắp hết hạn: 30 ngày hay giá trị khác?
Điều kiện thanh lý (đề xuất): kỳ cuối đã chốt, đã nhập chỉ số cuối, cọc đã quyết toán.
Kỳ lẻ: kỳ đầu và kỳ cuối không tròn tháng thì tính theo tỷ lệ ngày hay cả tháng?
Gia hạn: thêm snapshot trong cùng hợp đồng, hay tạo hợp đồng mới liên kết?
Dịch vụ khác: tính cố định theo phòng, theo đầu người hay theo mức dùng?
Làm tròn: từng dòng hay chỉ tổng, đến đơn vị nào? Phát cũng chốt điểm này ở VHT-34.
Biểu giá đổi giữa kỳ (đã chốt VHT-100, 31/10/2026): tách kỳ tại từng ngày hiệu lực; phân bổ đều tổng chỉ số theo số ngày lịch của từng đoạn vì đầu vào chưa có chỉ số tại ngày đổi giá. Ghi rõ giả định phân bổ đều theo ngày.

### VHT-100 tariff crossover decision (2026-10-31)

- A billing period that crosses a tariff effective date is split into inclusive date segments. Each segment uses the tariff effective on its first day.
- The calculation API currently receives only total consumption for the full period, not meter readings on each change date. Allocate that total to segments in proportion to each segment's inclusive day count (assumption: uniform consumption per calendar day), then calculate each segment with its applicable tariff. Prorate the tariff tier limits by the same day ratio so a partial segment does not receive a full month's tier allowance.
- Snapshot every applied tariff version and tier cost line, including `TariffVersionId`, `UnitPrice`, allocated `Quantity`, and `Amount`, against the billing period. Historical period reads use these saved values and do not recalculate from the current tariff table.
