# VHT-44 — Publish IIS thử (2B và 2E)

Áp dụng cho một site ở root `/`, một máy Windows/IIS và SQL Server thử. Phú ghép/publish; Tính bàn giao migration, cấu hình và keys; Phát phối hợp smoke. Theo dõi từng đợt trong [release record](vht-44-release-record.md). Hướng dẫn này không chứng minh đã có bản IIS chạy đạt.

## 1. Điền thông tin trước khi thao tác server

Ghi vào hồ sơ vận hành được giới hạn truy cập: server/cách truy cập, URL/hostname, site, app pool/identity, chứng thư HTTPS và hạn dùng, SQL/database thử, migration đã được Tính xác nhận, đường dẫn bản phát hành và bản trước, storage/keys/log. Secret thật cấu hình trực tiếp trên server; không ghi vào release record hoặc chat.

Đường dẫn minh họa trên **server thử**, cần thay bằng đường dẫn đã kiểm tra:

| Mục | Ví dụ | Quyền identity chạy app |
| --- | --- | --- |
| Mỗi bản publish | `D:\Sites\NhaTro\releases\<SHA>` | Read & Execute |
| Private storage | `D:\App_Data\NhaTro\storage` | Modify |
| Data Protection keys | `D:\App_Data\NhaTro\keys` | Modify |
| Log | `D:\App_Data\NhaTro\logs` | Modify |

Ba thư mục dữ liệu nằm ngoài cây site/publish, không ánh xạ virtual directory/alias/static file. Dùng cùng đường dẫn storage/keys qua các lần publish.

## 2. Chuẩn bị IIS

- Bật IIS và **WebSocket Protocol**. Cài Hosting Bundle .NET 10 phù hợp với framework-dependent artifact; nếu cài bundle trước IIS thì repair bundle. Xác nhận Windows/runtime được hỗ trợ theo phiên bản sẽ cài, không dùng danh sách hệ điều hành của tài liệu .NET cũ.
- Tạo app pool riêng: **No Managed Code**, Integrated, identity riêng ổn định. Với runtime x64 dùng `Enable 32-Bit Applications = False`. Bật **Load User Profile = True**; kiểm `setProfileEnvironment` không bị tắt để DPAPI theo user dùng được.
- Tạo site trỏ vào thư mục release đã chuẩn bị. Bật Anonymous Authentication cho ứng dụng dùng Identity cookie; không bật Windows Authentication để thay luồng đăng nhập.
- Bind HTTPS 443 với hostname/certificate đúng SAN, có private key và được client tin cậy; SNI khi cần. Bind HTTP 80 cùng hostname nếu kiểm redirect; cấu hình `ASPNETCORE_HTTPS_PORT=443` khi ứng dụng không tự xác định được cổng HTTPS. Kiểm firewall theo đúng host/port cần dùng.
- Chỉ dùng IIS trực tiếp trong cấu hình này. Nếu có reverse proxy/load balancer, chốt TLS forwarding, trusted proxy và session affinity trước khi publish; không thêm trust mọi forwarded header.

Nguồn: [IIS hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0), [Blazor hosting](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/server/?view=aspnetcore-10.0).

## 3. Cấu hình môi trường và ACL

Đặt biến theo **app pool riêng** trên server (IIS 10: `system.applicationHost/applicationPools`, collection `environmentVariables` của pool, trong cấu hình cấp server). Dùng công cụ quản trị có quyền phù hợp; không in/export giá trị secret, không chép secret vào profile hoặc `web.config` của artifact. Bảo vệ cấu hình IIS và bản backup của nó vì biến môi trường không phải kho mã hóa secret.

| Biến | Giá trị/ý nghĩa |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DOTNET_ENVIRONMENT` | `Production`; tránh hai biến mâu thuẫn |
| `AllowedHosts` | Hostname thực tế; nhiều host phân cách `;`, không gồm scheme/port |
| `ASPNETCORE_HTTPS_PORT` | `443` cho binding HTTPS tương ứng |
| `ConnectionStrings__DefaultConnection` | Tính đặt kết nối SQL Server thử; không dùng LocalDB của máy dev |
| `Storage__RootPath` | Đường dẫn storage tuyệt đối ngoài site/publish |
| `Security__DataProtectionKeysPath` | Đường dẫn keys tuyệt đối bền vững ngoài site/publish, không để trống |
| `Security__ProtectKeysWithDpapi` | `true` cho Windows một máy/identity ổn định |
| `Logging__Directory` | Đường dẫn log tuyệt đối ngoài site/publish |
| `AdminSeed__Email`, `AdminSeed__Password` | Chỉ khi cần tạo Admin đầu tiên; Tính đặt trực tiếp, gỡ sau khi xác nhận seed |

Không cấu hình `DevAccounts__Password` trên server. Production không seed tài khoản mẫu dev. Tính chuẩn bị tài khoản thử bốn vai trò bằng luồng quản trị phù hợp. Với Windows authentication tới SQL, xác minh principal thực sự dùng tới SQL; không giả định tên `IIS AppPool\...` trên máy web dùng được trên SQL từ xa. Tách quyền migration khỏi quyền chạy app khi có thể, không cấp `sysadmin` cho runtime. Kết nối SQL phải xác minh TLS/chứng thư theo cấu hình của Tính; không mặc định tắt kiểm chứng certificate.

Sau khi đổi biến, restart app pool để worker mới nhận cấu hình. Không đọc `dotnet user-secrets list` để tìm secret server.

Trong Explorer → Security → Advanced, tạo/kiểm ACL cho từng đường dẫn đã xác minh:

1. Giữ quyền quản trị cần thiết của Administrators/SYSTEM; cấp đúng identity chạy app theo bảng trên (vd `IIS AppPool\NhaTroTrial` nếu dùng ApplicationPoolIdentity).
2. Rà cả quyền kế thừa. Không chỉ thêm ACE rồi bỏ qua quyền đọc sẵn có của Users/Everyone/IIS_IUSRS trên storage/keys. Gỡ quyền rộng trên thư mục dữ liệu chuyên dụng sau khi xác minh không ảnh hưởng ứng dụng khác.
3. Identity app không được ghi vào bản publish. Tệp upload không được đặt trong site; thử URL trực tiếp tới tệp phải không tải được.
4. Kiểm không có tài khoản app khác được đọc keys/private storage. Không thay ACL đệ quy trên ổ đĩa hoặc thư mục dùng chung.

### Keys qua lần phát hành

`Security__ProtectKeysWithDpapi=true` mã hóa **keys mới** theo Windows user hiện tại. Giữ cùng máy, identity/profile, đường dẫn keys và application name `NhaTro.Web`. Không chuyển keys dev sang IIS và không xóa keys cũ để xử lý lỗi cookie. Các key cũ dạng plaintext không được tự mã hóa lại bởi tùy chọn này; nếu server đã có key ring cũ, Tính cần đánh giá và lập phương án chuyển đổi riêng, giữ khả năng đọc cookie hiện có.

DPAPI theo user không dùng để mang keys sang máy/identity khác. Khôi phục trên máy khác cần phương án keys riêng; công việc backup/restore đầy đủ thuộc VHT-74. Nếu có web farm hoặc cần certificate bảo vệ key ring, chốt lại cấu hình trước khi dùng hướng dẫn một máy này. Trên hệ điều hành khác Windows, bật tùy chọn DPAPI với đường dẫn keys sẽ khiến app từ chối khởi động.

Nguồn: [mã hóa keys khi lưu](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0), [cấu hình key ring](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

## 4. Ghép PR và tạo artifact

Chỉ ghép PR về `dev` có phạm vi/phụ thuộc rõ, review độc lập và CI phù hợp với phiên bản hiện tại. Ghi approval/CI URL và SHA, phần skip hoặc chờ tác giả. Không bỏ qua migration conflict hoặc tự duyệt PR của mình. CI hiện có chưa tạo artifact publish; bước này tạo artifact riêng từ commit đã chốt.

Trên checkout sạch của SHA được chọn, từ gốc repo, sau khi được phép chạy kiểm/publish:

```powershell
git status --short
git rev-parse HEAD
dotnet restore NhaTro/NhaTro.slnx
dotnet build NhaTro/NhaTro.slnx -c Release --no-restore --nologo
dotnet publish NhaTro/Web/Web.csproj -c Release --no-restore -p:PublishProfile=IIS -o ./publish/iis
```

Dừng khi một lệnh trả exit code khác 0. Output `publish/iis` đã được Git ignore; mỗi artifact dùng output mới/rỗng để không lẫn bản cũ. Không dùng `DeleteExistingFiles` hoặc lệnh xóa tự động lên site thật. SHA chỉ đại diện artifact nếu source tracked sạch; ghi riêng thay đổi local chưa commit nếu đang kiểm bản chuẩn bị.

Profile `IIS.pubxml` là Folder publish, framework-dependent, InProcess; `web.config` được SDK sinh. Kiểm output có `Web.dll`, `.deps.json`, `.runtimeconfig.json`, static assets và `web.config` dùng `AspNetCoreModuleV2`/InProcess. Không đưa `.env`, User Secrets, file keys, storage, log hoặc dữ liệu thử vào artifact. Không tự sửa `web.config` rồi quên ghi lại cấu hình server cần giữ.

Giới hạn upload hiện có: multipart/Kestrel 12 MiB, từng loại tệp tối đa 5 hoặc 10 MiB. Kiểm request filtering IIS cho phép request tới mức 12 MiB (`12582912` bytes), không cấu hình giới hạn thấp hơn làm hỏng upload hợp lệ. Với InProcess, đối chiếu cả IIS server request limit; không coi Kestrel limit là limit duy nhất. Giữ giới hạn loại tệp của ứng dụng, không tăng chúng để né lỗi deploy.

Nguồn: [publish profile](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-10.0).

## 5. Migration và đưa bản vào IIS

1. Chốt SHA, danh sách PR, artifact, migration cuối và schema hiện tại; Tính kiểm migration/script trên SQL thử trước. Migration áp bằng bước riêng do Tính bàn giao, không trong startup/circuit. Không chạy `database update` mặc định vào DB suy đoán từ máy dev.
2. Ghi bản code hiện đang phục vụ, physical path, cấu hình app pool/site, phương án quay lại và tính tương thích schema. Dùng quy trình sao lưu DB hiện có trước migration khi cần; thiếu phương án bảo toàn dữ liệu thì dừng bước DB.
3. Tạo thư mục release mới bên cạnh bản cũ, chuyển **nội dung** artifact vào đó; cấp Read & Execute cho identity. Xác nhận storage/keys/log và cấu hình môi trường trỏ đúng nơi cũ.
4. Trong cửa sổ bảo trì, stop **app pool của site thử** để tránh ghi khi migration/đổi bản. Thông báo mất circuit theo quy trình nhóm; không restart toàn IIS trên máy dùng chung.
5. Tính áp migration được xác nhận bằng cơ chế cấp secret riêng; lưu mã migration, thời gian và kết quả không nhạy cảm. Nếu thất bại, giữ site dừng hoặc quay lại code tương thích được xác nhận; không tự downgrade schema.
6. Đổi physical path sang release mới, start app pool, chạy smoke đã được duyệt. Ghi SHA và URL thực tế; truy cập được trang home chưa đủ để kết luận deploy đạt.

**2B:** chọn bản nền có phụ thuộc sẵn sàng, phát hành thử sớm. **2E:** cập nhật danh sách PR/migration và tạo artifact mới từ SHA tích hợp tuần 2; lặp quy trình. Không đánh dấu 2E dựa trên kết quả 2B.

## 6. Smoke và xử lý lỗi

Chạy trên dữ liệu thử, theo test-plan đã được duyệt. Tối thiểu:

| Kiểm tra | Kết quả cần có |
| --- | --- |
| HTTP/HTTPS | HTTP chuyển HTTPS đúng host/port, certificate được tin cậy, không vòng redirect |
| `/health/live`, `/health/ready` qua HTTPS | 200/Healthy; readiness không thay smoke ghi/đọc SQL |
| Login | Bốn vai trò đăng nhập tài khoản của mình; Production không phụ thuộc seed dev |
| SQL | Tạo dữ liệu thử bằng luồng thật có quyền, reload/đọc lại từ SQL; ghi ID thử không nhạy cảm |
| Private files | Upload/download hợp lệ qua endpoint kiểm quyền; user khác phạm vi/Admin nghiệp vụ bị từ chối, không trả nội dung tệp |
| WebSocket | DevTools Network xác nhận `/_blazor` nâng cấp WebSocket (thường 101), có frame/hoạt động tương tác; negotiate thành công hoặc fallback long polling chưa đủ |
| Reconnect | Ngắt mạng ngắn trong thời gian giữ circuit thực tế, nối lại và thao tác được; không nhân đôi thao tác ghi |
| Publish lại | Tệp cũ đọc được; cookie còn hạn dùng được với keys/identity giữ nguyên. Circuit trong bộ nhớ có thể mất sau restart, phải tải lại trang |

Nếu backend/endpoint đang dùng dữ liệu mẫu hoặc chưa ghép SQL, ghi blocked cho luồng đó. Không dùng giao diện mẫu làm bằng chứng ghi/đọc SQL thật.

Khi có lỗi, ghi thời gian, SHA, bước tái hiện, kỳ vọng/thực tế, HTTP status/trace ID và log đã lọc nhạy cảm; không đính kèm cookie/token/connection string/giấy tờ. 500.30/500.31: rà Hosting Bundle, startup, cấu hình SQL/ACL/keys; 502.5: rà host/process nếu dùng OutOfProcess; lỗi upload: rà request filtering và ACL; reconnect: rà WebSocket, proxy và app pool recycle. Đây là hướng điều tra, không phải kết luận nguyên nhân.

Không sửa code trong lượt smoke. Lập báo cáo nguyên nhân/phạm vi sửa và duyệt fix theo quy tắc repo.

## 7. Rollback code

Stop app pool của site thử, xác nhận bản code trước tương thích schema hiện tại, đổi physical path về release trước và start lại. Giữ nguyên storage/keys và cấu hình môi trường; smoke lại trên SHA rollback. Nếu schema không tương thích hoặc migration dở dang, giữ bảo trì và chuyển Tính xử lý theo phương án phục hồi đã xác nhận. Không tự chạy migration Down/drop/reset DB. Ghi SHA trước/sau, lý do, tác động và kết quả thực tế.
