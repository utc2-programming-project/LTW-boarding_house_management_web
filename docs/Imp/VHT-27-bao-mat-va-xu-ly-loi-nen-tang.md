# VHT-27 — Bảo mật và xử lý lỗi dùng chung

**Nhánh:** `VHT-27-bao-mat-va-xu-ly-loi-nen-tang` (tạo từ `dev` local `20a04fb`)
**Nguồn:** `docs/task/cong_viec_theo_tuan.md` (VHT-27), `docs/task/timeline_song_song.md` (nhịp 2A)
**Người chính:** Tính · **Phối hợp:** Phát (endpoint tệp), Phú (error UI/IIS)
**Phạm vi 2A:** nền tảng backend bảo mật + xử lý lỗi; **không** làm UI nghiệp vụ mới.

> Kế hoạch này là tài liệu sống: cập nhật khi chốt quyết định và ghi tiến độ ở mục 7.

---

## Tóm tắt đã làm (VHT-27)

| # | Hạng mục | Kết quả |
|---|---|---|
| 1 | **Antiforgery** | Giữ `UseAntiforgery()` toàn cục; `/api/files` tự `ValidateRequestAsync`. Thêm test: thiếu token ở login, `/culture/set`, `/api/files` → **400** |
| 2 | **Giới hạn đăng nhập** | Rate limit theo IP: login **10/phút**, `/api/files` **20/phút**, `/culture/set` **20/phút** → **429** (JSON `UploadResult` cho `/api`). Giữ lockout 5 lần/15 phút sẵn có |
| 3 | **CAPTCHA** | Sau **3 lần sai** hiện thử thách cộng hai số; đáp án mã hoá bằng Data Protection (`ICaptchaVerifier` + `LocalCaptchaVerifier`), sẵn sàng thay captcha thật không sửa luồng login |
| 4 | **Lọc log** | `SensitiveDataScrubber` (Serilog enricher) che `Password`, `Token`, `Authorization`, `Cookie`, `IdNumber/CitizenId`, `ConnectionString`, `Secret`… thành `***` |
| 5 | **Lỗi an toàn** | `/api` non-dev: `ApiExceptionHandlerMiddleware` trả JSON **500** theo shape `UploadResult`, không lộ exception. Viết lại `/Error`, `/not-found` song ngữ, an toàn |
| 6 | **Health** | `/health/live` (process sống) và `/health/ready` (kết nối DB) — ẩn danh, chỉ trả `Healthy`/`Unhealthy`, `Cache-Control: no-store` |
| 7 | **Data Protection** | Khoá lưu ngoài thư mục publish theo `Security:DataProtectionKeysPath`, `SetApplicationName("NhaTro.Web")`; hướng dẫn trong `docs/operations/secrets.md` |

**Cấu hình mới:** section `Security` trong `appsettings.json` (`DataProtectionKeysPath`, `Captcha.RequiredAfterFailedAttempts`, `RateLimit.*`).

**Kiểm chứng:** build **0 warning / 0 error**; **Unit 348 pass**; **Integration 70 pass**.

---

## 0. Mục tiêu

Rà **và** cấu hình các lớp bảo mật / xử lý lỗi dùng chung để:

- Form/endpoint ghi được bảo vệ bằng antiforgery.
- Đăng nhập và upload có giới hạn, chống lạm dụng.
- Log **không** chứa dữ liệu nhạy cảm (mật khẩu, token, CCCD, nội dung tệp).
- Lỗi trả về **an toàn**: đúng mã HTTP, đúng cấu trúc, **không lộ** chi tiết nội bộ ở môi trường thật.
- Có **health live/ready** để giám sát mà không lộ thông tin nhạy cảm.

Đây là **nền** cho cả hệ thống; bản rà cuối là **VHT-75** (secrets/log/dữ liệu cá nhân) và smoke IIS là **VHT-77**.

---

## 1. Hiện trạng (đã có gì trong repo)

| Hạng mục | Đã có | Còn thiếu / cần làm |
|---|---|---|
| **Antiforgery** | `AddAntiforgery` (HeaderName `RequestVerificationToken`) — `Program.cs:182`; `UseAntiforgery()` — `:222`; `/api/files` và `/culture/set` tự validate | Rà toàn bộ form POST; test thiếu token → 400; chốt cấu hình cookie antiforgery (SameSite/Secure) |
| **Giới hạn đăng nhập** | Lockout 5 lần / 15 phút — `Program.cs:119-121`; Dev seeder gỡ lockout | **Chưa có rate limiting**; **chưa có CAPTCHA sau 3 lần sai**; chưa có test lockout |
| **Giới hạn upload** | `FormOptions.MultipartBodyLengthLimit` + Kestrel `MaxRequestBodySize` = 12 MB — `:177-179`; giới hạn per-type trong `Storage/Configuration`; `FileTypeRules`; middleware 413 — `Infrastructure/ErrorHandling/FileUploadExceptionMiddleware.cs` | Test 413/loại tệp/thiếu antiforgery; chốt `maxFilesPerEntity` (đang `null`); ghi chú `maxAllowedContentLength` cho IIS |
| **Lọc log nhạy cảm** | Serilog Console+File, `MinimumLevel.Override("Microsoft", Warning)` — `:41-49`; `UseSerilogRequestLogging()` — `:202` | **Chưa** có danh sách trường cấm log; chưa có test/redaction |
| **Lỗi an toàn** | `UseExceptionHandler("/Error")` + `UseHsts()` (chỉ non-dev) — `:207-216`; `UseStatusCodePagesWithReExecute("/not-found")` — `:217`; 401/403 cho `/api` — `:66-92`; `JsonUnmappedMemberHandling.Disallow` — `:164-166` | **Chưa** thống nhất định dạng lỗi; `/Error` còn câu template, `/not-found` tiếng Anh, chưa đúng style; chưa test non-dev không lộ stack |
| **Health live/ready** | **Chưa có** (tech-stack yêu cầu `Infrastructure/HealthChecks/`, `Endpoints/Health/`) | Thêm health checks + endpoint + test |
| **Data Protection keys** | Dùng mặc định của `AddIdentityCookies()` | Chưa persist keys ngoài thư mục publish (cho deploy) |
| **HTTPS/HSTS** | `UseHttpsRedirection()`; `UseHsts()` non-dev (mặc định 30 ngày) | Rà lại tham số HSTS khi deploy |

**Tham chiếu hợp đồng:** `docs/task/de_xuat_hop_dong_storage.md` mục 2 (giới hạn server + 413) và mục 5 (bảng mã lỗi tệp); `docs/tech-stack/cong_nghe_su_dung.md` (Bảo mật web: HTTPS/HSTS, Antiforgery, Data Protection, Rate Limiting; Health Checks).

---

## 2. Phạm vi

**LÀM (VHT-27):**

- Rà + cấu hình antiforgery, giới hạn đăng nhập/upload.
- Rate limiting cho endpoint nhạy cảm.
- Chuẩn hóa lỗi an toàn (non-dev không lộ chi tiết) + dọn trang `/Error`, `/not-found`.
- Lọc log nhạy cảm: danh sách trường cấm + cấu hình + test.
- Health live/ready.
- Data Protection keys persist (mức cấu hình, chi tiết đường dẫn do Phú/IIS chốt).

**KHÔNG LÀM (để task khác):**

- UI nghiệp vụ mới; thay đổi nghiệp vụ P1–P8.
- Virus scan tệp (`MaliciousFile` — để sau theo VHT-12).
- Bản rà secrets/publish cuối cùng → **VHT-75**; smoke IIS → **VHT-77**.
- Chốt số file tối đa mỗi đối tượng (chờ Phúc/Phú) — VHT-27 chỉ giữ cơ chế.

---

## 3. Kế hoạch chi tiết

### A. Antiforgery

- [x] Rà mọi route ghi (POST/PUT/DELETE): Identity form, `/culture/set`, `/api/files`. Xác nhận `UseAntiforgery()` phủ và endpoint nào `DisableAntiforgery()` thì **tự** `ValidateRequestAsync`.
- [x] Chốt cấu hình cookie antiforgery (`Cookie.SameSite`, `Cookie.SecurePolicy`) khớp cookie Identity; chỉ siết nếu không phá luồng hiện tại. → **Giữ mặc định** (`SameSite=Strict`, `HttpOnly`, `SecurePolicy=None`); `Secure` xử lý ở tầng deploy (HTTPS termination), ghi chú khi triển khai.
- [x] Test: POST thiếu token → **400**; POST sai token → **400**; form hợp lệ vẫn chạy. → `SecurityEndpointTests`: thiếu token ở login, `/culture/set`, `/api/files` (sau khi đăng nhập) đều **400**; luồng form hợp lệ chạy qua `AuthFlowTests` sẵn có.

**File:** `Program.cs`, `Endpoints/Files/FileEndpoints.cs`; test mới trong `NhaTro.IntegrationTests`.

### B. Giới hạn đăng nhập (đã chốt)

**Quyết định:** giữ lockout + **rate limiting** (login + `/api/files` + `/culture/set`); **sau 3 lần nhập sai** hiển thị **CAPTCHA cục bộ** (giai đoạn 1), **thiết kế tách lớp** để sau thay bằng CAPTCHA thật (reCAPTCHA/hCaptcha) mà không sửa luồng login.

- [x] Giữ lockout 5 lần / 15 phút (đã có); test: sai liên tiếp → khóa tới khi hết hạn. → **Giữ nguyên** cấu hình Identity. Không thêm test lockout riêng vì CAPTCHA chặn sau 3 lần (đã có test ngưỡng CAPTCHA); lockout là hành vi framework.
- [x] Rate limiting (`AddRateLimiter`, .NET có sẵn, **không cần package**): policy theo **IP** cho `/Account/Login` + `/login`, `/api/files`, `/culture/set`; vượt ngưỡng → **429** an toàn (JSON cho `/api`). → `RateLimitPolicies` + global limiter (chỉ POST login) + policy `file-upload`, `culture-set`.
- [x] CAPTCHA cục bộ sau 3 lần sai: hiện thử thách (câu hỏi/toán đơn giản) ở form login, server xác minh; chưa đạt → **không** cho thử tiếp.
  - [x] Tách interface `ICaptchaVerifier` (hoặc tương đương) + cài đặt cục bộ → sau chỉ thay cài đặt, giữ nguyên `Login.razor`. → `ICaptchaVerifier` + `LocalCaptchaVerifier` (đáp án mã hoá bằng Data Protection, không lộ trong form).
  - [x] Đếm số lần sai qua `UserManager`/lockout hiện có; ngưỡng hiện thử thách = 3 (đặt cấu hình, không hard-code). → `Security:Captcha:RequiredAfterFailedAttempts`.
- [x] `UseRateLimiter()` đặt đúng pipeline (sau `UseRouting`, trước endpoint). → đặt trước `UseAntiforgery()`.

**Kết quả test:** `LoginRateLimitTests` (429 sau 10 lần/phút) và `LoginCaptchaTests` (CAPTCHA hiện sau 3 lần sai) — **pass**.

**File:** `Program.cs`, `Components/Account/Pages/Login.razor`, `Infrastructure/Security/` (`RateLimitPolicies` + `ICaptchaVerifier`/cài đặt cục bộ).

> CAPTCHA thật về sau (hướng B) sẽ cần **cập nhật `docs/tech-stack/cong_nghe_su_dung.md`** + secret; thiết kế tách lớp ở trên để việc thay thế chỉ là đổi cài đặt.

### C. Giới hạn upload

- [x] Xác nhận tầng server 12 MB ≥ max per-type (10 MB + overhead) — đúng hợp đồng VHT-12 mục 2. → `maxUploadRequestBytes = 12 MiB` (Kestrel + `FormOptions`).
- [x] Rà/giữ middleware 413 map về `FileSizeExceeded` (đã có). → `FileUploadExceptionMiddleware` giữ nguyên.
- [x] Test: quá lớn → **413** `FileSizeExceeded`; sai loại → **400** `InvalidFileType`; thiếu antiforgery → **400**. → Test HTTP thiếu antiforgery **đã thêm** (`SecurityEndpointTests`). 413/`InvalidFileType` đã có kiểm ở tầng service/rules từ VHT-12; **chưa thêm** test HTTP nặng 12 MB (tránh test chậm) — ghi nhận là phần còn lại nếu cần.
- [x] Ghi chú deploy: IIS `maxAllowedContentLength` **≥** giới hạn Kestrel (Phú).
- [ ] (Chờ chốt) `MaxFilesPerEntity` — chỉ bật khi Phúc/Phú chốt số. → **Giữ nguyên** (chưa chốt số).

**File:** `Endpoints/Files/FileEndpoints.cs`, `Infrastructure/ErrorHandling/FileUploadExceptionMiddleware.cs`; test mới.

### D. Lọc log nhạy cảm (đã chốt)

**Quyết định:** cấm hoàn toàn `IdNumber` (CCCD), cùng mật khẩu/hash, token, `Authorization`/cookie, nội dung tệp, connection string/secret.

- [x] Áp danh sách cấm log ở trên; rà chỗ ghi log user/email hiện có. → Rà toàn bộ `Log*` trong `Web/`: không chỗ nào ghi `IdNumber`/mật khẩu/hash/token/cookie/nội dung tệp. Chỉ log UserId/email/fileCategory/tên tệp server — chấp nhận được.
- [x] Rà `UseSerilogRequestLogging`: đảm bảo không log query string nhạy cảm; cân nhắc `EnrichDiagnosticContext` chỉ ghi path/method/status. → Mặc định chỉ ghi `RequestPath` (không query string); giữ nguyên.
- [x] Kiểm các chỗ hiện có ghi log user/email — chấp nhận hay che (chốt). → Chấp nhận email/UserId (định danh tài khoản), không chấp nhận dữ liệu nhạy cảm khác.
- [x] (Nếu cần) helper che dữ liệu trong `Infrastructure/Logging/`. → `SensitiveDataScrubber` (Serilog enricher) che thuộc tính tên `Password`, `Token`, `IdNumber`, `Cookie`, `ConnectionString`… thành `***`.
- [x] Test: thao tác có `IdNumber`/login sai **không** xuất hiện giá trị nhạy cảm trong log. → Unit test `SensitiveDataScrubberTests` (che đúng, giữ thuộc tính thường). **Chưa** thêm test bắt log toàn trình qua HTTP (Serilog ghi file/console khó chặn trong `WebApplicationFactory`) — phần còn lại nếu cần.

**File:** `Program.cs`, `Infrastructure/Logging/` (mới nếu cần).

### E. Lỗi an toàn (đã chốt)

**Quyết định:** giữ **shape per-feature** (như `UploadResult`) + thêm xử lý **500 an toàn** cho `/api`; **không** đổi sang `ProblemDetails` toàn cục.

- [x] Giữ shape per-feature cho `/api`; thêm lỗi 500 an toàn (JSON, không lộ chi tiết) cho `/api`. → `ApiExceptionHandlerMiddleware` (chỉ nhánh `/api`, non-dev) trả `UploadResult` + 500, không lộ exception.
- [x] Đảm bảo **non-dev không lộ** stack/exception/đường dẫn máy; `UseExceptionHandler("/Error")` + `UseHsts()` chỉ non-dev (đã có) — kiểm lại. → Giữ nguyên; `/Error` không hiển thị exception.
- [x] Dọn trang lỗi: `/Error` bỏ câu template, song ngữ + đúng style; `/not-found` đổi sang song ngữ + layout (Phú). → Viết lại `Error.razor`, `NotFound.razor` (song ngữ, icon, nút về trang chủ, chỉ hiện mã yêu cầu).
- [x] Kiểm `UseStatusCodePagesWithReExecute("/not-found")` không rò thông tin. → Trang not-found tĩnh, không lộ path/chi tiết.
- [x] Test: môi trường non-dev khi lỗi 500 → response không chứa stack trace; `/api` trả JSON an toàn. → Unit test `ApiExceptionHandlerMiddlewareTests` (JSON 500, không lộ message). **Chưa** thêm test HTTP non-dev đầy đủ (cần dựng app môi trường Production) — ghi nhận.

**File:** `Program.cs`, `Components/Pages/Error.razor`, `Components/Pages/NotFound.razor`, `Resources/Shared.*.resx`.

### F. Health live/ready

- [x] Thêm `AddHealthChecks()`:
  - **live**: chỉ app sống (không phụ thuộc ngoài).
  - **ready**: kiểm **kết nối DB** bằng package **`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`** (đã chốt) → thêm vào `Web.csproj`. → `AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"])`.
- [x] Endpoint `Endpoints/Health/HealthEndpoints.cs`:
  - `/health/live`, `/health/ready`.
  - **Ẩn danh** (đã chốt), response **tối giản** (`Healthy`/`Unhealthy`), **không** lộ exception/connection/version.
  - Chặn cache (`Cache-Control: no-store`).
- [x] Test: DB sẵn sàng → ready `Healthy`; DB lỗi → `Unhealthy` (không lộ chi tiết). → `SecurityEndpointTests`: `/health/live` và `/health/ready` trả 200 `Healthy`. Nhánh DB lỗi dựa vào framework (không thêm test phá DB).

**File:** `Infrastructure/HealthChecks/`, `Endpoints/Health/HealthEndpoints.cs`, `Program.cs`.

### G. Data Protection keys / HTTPS-HSTS (đã chốt một phần)

**Quyết định:** làm **Data Protection keys** trong VHT-27; **security headers** (`nosniff` toàn cục, `X-Frame-Options`/CSP) **để sau**.

- [x] `AddDataProtection()` persist keys ra **file system ngoài thư mục publish** + `SetApplicationName` cố định (đường dẫn từ cấu hình/môi trường, **không** hard-code) — tránh mất key làm logout toàn bộ / lỗi giải mã cookie. → `Security:DataProtectionKeysPath` (mặc định `%LOCALAPPDATA%\NhaTro\keys`), `SetApplicationName("NhaTro.Web")`.
- [ ] Rà tham số `UseHsts` (maxAge/includeSubDomains) khi deploy. → **Để bước deploy** (chưa đổi mặc định).

**File:** `Program.cs`, `Infrastructure/Security/` (mới nếu tách).

---

## 4. File mới / file sửa (dự kiến)

**Mới:**

| File | Nội dung |
|---|---|
| `NhaTro/Web/Infrastructure/Security/RateLimitPolicies.cs` | Định nghĩa policy rate limit (nếu chưa gộp vào Program.cs) |
| `NhaTro/Web/Infrastructure/HealthChecks/` | Health check (DB / self) |
| `NhaTro/Web/Endpoints/Health/HealthEndpoints.cs` | `/health/live`, `/health/ready` |
| `NhaTro/Web/Infrastructure/Logging/` (nếu cần) | Helper che dữ liệu log |

**Sửa:**

| File | Thay đổi |
|---|---|
| `NhaTro/Web/Program.cs` | Rate limiter, health checks, Data Protection, HSTS/header, chuẩn hóa lỗi |
| `NhaTro/Web/Components/Pages/Error.razor` | Bỏ câu template, song ngữ, an toàn non-dev |
| `NhaTro/Web/Components/Pages/NotFound.razor` | Song ngữ + layout |
| `NhaTro/Web/Resources/Shared.vi-VN.resx`, `Shared.en-US.resx` | Key trang lỗi/not-found/thông báo 429 |
| `NhaTro/Web/Endpoints/Files/FileEndpoints.cs` | (Rà) antiforgery + lỗi upload an toàn |
| `NhaTro/Web/Web.csproj` | Thêm package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` |
| `docs/operations/secrets.md` | Ghi chú Data Protection keys path (và secret CAPTCHA nếu chọn hướng B) |

**Không đụng WIP việc khác** đang có trong working tree.

---

## 5. Test

| Loại | Case |
|---|---|
| Integration | Thiếu/sai antiforgery → 400; upload quá lớn → 413; sai loại → 400 |
| Integration | Lockout sau N lần sai; rate limit → 429; CAPTCHA xuất hiện sau 3 lần sai (theo hướng chốt) |
| Integration | `/health/live` = 200; `/health/ready` phản ánh DB |
| Integration | Non-dev lỗi 500 không lộ stack; `/api` lỗi trả JSON an toàn |
| Unit / Integration | Log không chứa trường nhạy cảm (mật khẩu, CCCD, token) |
| E2E | Trang `/Error`, `/not-found` hiển thị đúng, song ngữ |

---

## 6. Quyết định (đã chốt)

| # | Vấn đề | Quyết định |
|---|---|---|
| 1 | Health | Endpoint **ẩn danh**, chỉ trả trạng thái `/health/live`, `/health/ready` |
| 2 | Ready check DB | Dùng package **`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`** |
| 3 | Trường cấm log | **Cấm hoàn toàn** `IdNumber` + mật khẩu/hash, token, cookie |
| 4 | Data Protection | **Làm trong VHT-27**; security headers để sau |
| 5 | Chống dò mật khẩu | **Rate limiting + CAPTCHA sau 3 lần nhập sai** |
| 6 | CAPTCHA giai đoạn 1 | **Thử thách cục bộ** (không thêm phụ thuộc), **tách lớp** để sau thay bằng captcha thật |
| 7 | Rate limiting phạm vi | `/Account/Login` + `/login`, `/api/files`, `/culture/set` |
| 8 | Lỗi `/api` | Giữ **shape per-feature** + thêm **500 an toàn** (không dùng `ProblemDetails` toàn cục) |

> CAPTCHA **không thay** rate limiting/lockout — là lớp thêm sau 3 lần sai. CAPTCHA thật (hướng B, về sau) sẽ cần cập nhật `docs/tech-stack/cong_nghe_su_dung.md` + secret.

---

## 7. Checklist tiến độ

- [x] Tạo nhánh `VHT-27-bảo-mật-va-xử-lý-lỗi-nền-tảng` từ `dev`
- [x] Rà hiện trạng + hợp đồng VHT-12 + tech-stack
- [x] Chốt 8 quyết định (mục 6)
- [x] A. Antiforgery: rà + test (3 test HTTP thiếu token → 400)
- [x] B. Rate limiting (login + /api/files + /culture/set) + CAPTCHA cục bộ sau 3 lần sai (429 + CAPTCHA có test)
- [x] C. Upload: rà + test thiếu antiforgery → 400 (413/loại tệp đã có từ VHT-12, chưa thêm test HTTP nặng)
- [x] D. Lọc log nhạy cảm (`SensitiveDataScrubber`) + unit test
- [x] E. Lỗi an toàn (`ApiExceptionHandlerMiddleware` + 500 JSON) + dọn `/Error`, `/not-found`
- [x] F. Health live/ready (package EF, ẩn danh) + test 200 `Healthy`
- [x] G. Data Protection keys (HSTS rà khi deploy — còn mở)
- [x] Build 0 warning / 0 error
- [x] Chạy unit + integration: **Unit 348 pass**, **Integration 70 pass** (E2E chưa chạy lại — không đổi luồng UI đăng nhập thường)
- [x] Cập nhật tài liệu liên quan (`docs/operations/secrets.md` — Data Protection keys; tech-stack cập nhật khi thay CAPTCHA thật)

**Còn mở (không chặn merge):**
- Test HTTP 413/`InvalidFileType` và test HTTP non-dev 500 (hiện đã có unit/service test).
- Rà `UseHsts` khi deploy; IIS `maxAllowedContentLength`.
- Khi chuyển sang CAPTCHA thật: cập nhật `docs/tech-stack/cong_nghe_su_dung.md` + secret.
