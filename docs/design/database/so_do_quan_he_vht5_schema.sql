-- Tạo cơ sở dữ liệu NhaTro (chạy lại nhiều lần không lỗi)
--
-- ĐỒNG BỘ VHT-28 (đợt 3C / VHT-132, 09/11/2026): file này cập nhật theo TOÀN BỘ migration
-- đã merge trên `dev@c7d7411` tới 20261107152554_AddFinancialConstraints. Không vẽ bảng "dự kiến":
-- mọi bảng dưới đây đều đã có migration + entity + configuration trên `dev`.
-- Phạm vi: 30 bảng nghiệp vụ + AspNetUsers (31 bảng vẽ trong hình). Bảng Identity phụ (Roles, UserRoles, Claims,
-- Logins, Tokens, Passkeys) không thuộc ERD, xem ghi chú cuối hình SVG.
-- Delta đợt 3C (VHT-132, 09/11/2026):
--   * 9 bảng mới: ContractRenewalRequests, Payments, PaymentProofs, PaymentAuditLogs, AuditLogEntries,
--     UtilityReadings, UtilityReadingAudits, TenantInvitations, TenantOwnerLinks.
--   * Xóa mềm bổ sung: AspNetUsers + IsDeleted/DeletedAt/DeletedByUserId/OriginalEmail;
--     Tenants + DeletedAt/DeletedByUserId; Rooms + DeletedAt/DeletedBy; Properties + DeletedAt;
--     ServiceCatalogs + IsDeleted/DeletedAt/DeletedBy. Tenants.OwnerId thêm rồi bỏ (thay bằng TenantOwnerLinks).
--   * ServiceCatalogs + IsActive/Description; ServicePriceHistory + EffectiveTo/Note và đổi CHECK
--     đơn giá > 0, EffectiveTo > EffectiveDate.
--   * FK tài chính/lịch sử đổi Cascade -> Restrict; unique Payment `UX_Payments_InvoiceStatus`
--     (InvoiceId, Status) filter Status IN (0,1).
-- Delta đã bổ sung so với bản VHT-5 (đợt 2A):
--   * AspNetUsers: + FullName (VHT-8), + ApprovedAt (VHT-95).
--   * Tenants: + IsActive, RowVersion, Gender, DateOfBirth, IdIssuedDate, IdIssuedPlace,
--     PermanentAddress, Note; đổi lọc CitizenId (thêm IsActive = 1), UserId thành unique (VHT-23).
--   * StoredFiles (VHT-12/VHT-13), FileAccessAuditLogs (VHT-26), ManagerToggles + 21 công tắc (VHT-94).
-- Delta đợt 2D (VHT-122):
--   * PropertyManagers (VHT-24).
--   * Contracts + ContractMembers + trigger chống chồng hợp đồng đã duyệt (P4).
--   * ServiceCatalogs + ServicePriceHistory (P5).
--   * UtilityTariffs + UtilityTariffTiers (VHT-100); UtilityBillingPeriodSnapshots +
--     UtilityBillingCostLineSnapshots (P5).
--   * Invoices + InvoiceItems (P6); ManagerApprovalRequests (VHT-96).
-- Delta đợt 2E (VHT-123, 03/11/2026):
--   * MaintenanceIncidents + MaintenanceIncidentHistories (P7, VHT-33); Note lịch sử 2000 ký tự.
--   * IncidentPhotoAttachments (VHT-127), PK là FileId, luôn trỏ StoredFiles và phải có đích.
--   * Không đổi bảng/cột cũ: 3 migration P7 chỉ thêm bảng + nới Note 1000 → 2000.
IF DB_ID(N'NhaTro') IS NULL
    CREATE DATABASE NhaTro;
GO

USE NhaTro;
GO

-- sqlcmd mặc định tắt QUOTED_IDENTIFIER; filtered index và computed column cần bật.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Tài khoản đăng nhập (ASP.NET Identity, class ApplicationUser). 1 tài khoản có N nhà trọ và N hồ sơ khách
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
BEGIN
    CREATE TABLE AspNetUsers
    (
        Id NVARCHAR(450) CONSTRAINT PK_AspNetUsers PRIMARY KEY,  -- khóa chính (chuỗi)
        UserName NVARCHAR(256),  -- tên đăng nhập
        NormalizedUserName NVARCHAR(256),  -- tên đăng nhập viết hoa chuẩn hóa, không được trùng (UserNameIndex)
        Email NVARCHAR(256),  -- email
        NormalizedEmail NVARCHAR(256),  -- email chuẩn hóa, có index (EmailIndex)
        PasswordHash NVARCHAR(MAX),  -- mật khẩu đã mã hóa
        SecurityStamp NVARCHAR(MAX),  -- chuỗi bảo mật, đổi khi đổi mật khẩu
        ConcurrencyStamp NVARCHAR(MAX),  -- chống ghi đè đồng thời
        PhoneNumber NVARCHAR(256),  -- số điện thoại
        FullName NVARCHAR(200),  -- họ tên hiển thị (VHT-8), được để trống
        EmailConfirmed BIT NOT NULL,  -- đã xác nhận email
        PhoneNumberConfirmed BIT NOT NULL,  -- đã xác nhận số điện thoại
        TwoFactorEnabled BIT NOT NULL,  -- bật xác thực 2 bước
        LockoutEnabled BIT NOT NULL,  -- cho phép khóa tài khoản
        LockoutEnd DATETIMEOFFSET,  -- thời điểm hết khóa
        AccessFailedCount INT NOT NULL,  -- số lần đăng nhập sai
        IsDeleted BIT NOT NULL CONSTRAINT DF_AspNetUsers_IsDeleted DEFAULT 0,  -- xóa mềm tài khoản
        DeletedAt DATETIME2,  -- thời điểm xóa mềm
        DeletedByUserId NVARCHAR(MAX),  -- người xóa (liên kết mềm tới AspNetUsers.Id)
        OriginalEmail NVARCHAR(MAX)  -- email gốc giữ lại sau khi ẩn tài khoản
    )
END
GO

-- VHT-8: cột FullName thêm sau (DB đã tồn tại thì bổ sung, không tạo lại bảng)
IF COL_LENGTH(N'dbo.AspNetUsers', N'FullName') IS NULL
    ALTER TABLE AspNetUsers ADD FullName NVARCHAR(200) NULL;
GO

-- VHT-95: thời điểm tài khoản được duyệt (NULL = chưa duyệt, không đăng nhập được)
IF COL_LENGTH(N'dbo.AspNetUsers', N'ApprovedAt') IS NULL
    ALTER TABLE AspNetUsers ADD ApprovedAt DATETIME2 NULL;
GO

-- Delta 3C (20261106185549_AccountSoftDelete): xóa mềm tài khoản cho DB đã tồn tại
IF COL_LENGTH(N'dbo.AspNetUsers', N'IsDeleted') IS NULL
    ALTER TABLE AspNetUsers ADD IsDeleted BIT NOT NULL CONSTRAINT DF_AspNetUsers_IsDeleted2 DEFAULT 0;
GO
IF COL_LENGTH(N'dbo.AspNetUsers', N'DeletedAt') IS NULL
    ALTER TABLE AspNetUsers ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.AspNetUsers', N'DeletedByUserId') IS NULL
    ALTER TABLE AspNetUsers ADD DeletedByUserId NVARCHAR(MAX) NULL;
GO
IF COL_LENGTH(N'dbo.AspNetUsers', N'OriginalEmail') IS NULL
    ALTER TABLE AspNetUsers ADD OriginalEmail NVARCHAR(MAX) NULL;
GO

-- tên đăng nhập không được trùng
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UserNameIndex' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
    CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
GO
-- tìm nhanh theo email
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'EmailIndex' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
    CREATE INDEX EmailIndex ON AspNetUsers(NormalizedEmail);
GO

-- Nhà trọ. 1 nhà trọ thuộc 1 chủ trọ và có N phòng
IF OBJECT_ID(N'dbo.Properties', N'U') IS NULL
BEGIN
    CREATE TABLE Properties
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_Properties PRIMARY KEY,  -- khóa chính, tự tăng
        Name NVARCHAR(200) NOT NULL,  -- tên nhà trọ
        Address NVARCHAR(500),  -- địa chỉ, được để trống
        OwnerId NVARCHAR(450) NOT NULL,  -- chủ trọ, bắt buộc, trỏ tới AspNetUsers.Id
        IsActive BIT NOT NULL,  -- đang hoạt động (app gán true)
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo, giờ UTC, app gán
        UpdatedAt DATETIME2,  -- ngày sửa gần nhất, giờ UTC
        IsDeleted BIT NOT NULL,  -- xóa mềm: 1 = ẩn khỏi app, dữ liệu vẫn còn trong DB
        DeletedAt DATETIME2,  -- Delta 3C: thời điểm xóa mềm (20261105173632_AddPropertyTrash)
        CONSTRAINT FK_Properties_AspNetUsers_OwnerId FOREIGN KEY (OwnerId) REFERENCES AspNetUsers(Id)  -- Restrict: cấm xóa thật AspNetUsers khi còn Properties trỏ tới
    )
END
GO

-- Delta 3C: cột DeletedAt cho DB đã tồn tại
IF COL_LENGTH(N'dbo.Properties', N'DeletedAt') IS NULL
    ALTER TABLE Properties ADD DeletedAt DATETIME2 NULL;
GO

-- tìm nhanh các nhà của 1 chủ trọ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Properties_OwnerId' AND object_id = OBJECT_ID(N'dbo.Properties'))
    CREATE INDEX IX_Properties_OwnerId ON Properties(OwnerId);
GO

-- Phòng. 1 phòng thuộc 1 nhà trọ. Khớp EF CoreSchema (không thêm CHECK ngoài migration)
IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
BEGIN
    CREATE TABLE Rooms
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_Rooms PRIMARY KEY,  -- khóa chính, tự tăng
        PropertyId INT NOT NULL,  -- nhà trọ chứa phòng, bắt buộc, trỏ tới Properties.Id
        Code NVARCHAR(50) NOT NULL,  -- mã phòng (vd P101), không trùng trong cùng 1 nhà
        Floor INT,  -- tầng, NULL nếu chưa xác định
        AreaM2 DECIMAL(10,2),  -- diện tích (m2)
        StandardPrice DECIMAL(18,2) NOT NULL,  -- giá chuẩn (VND)
        Status INT NOT NULL,  -- 0 Trống, 1 Đang thuê, 2 Bảo trì
        IsActive BIT NOT NULL,  -- đang hoạt động (app gán true)
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo, giờ UTC, app gán
        UpdatedAt DATETIME2,  -- ngày sửa gần nhất, giờ UTC
        IsDeleted BIT NOT NULL,  -- xóa mềm: 1 = ẩn khỏi app, dữ liệu vẫn còn trong DB
        DeletedAt DATETIME2,  -- Delta 3C: thời điểm xóa mềm (20261104023558_AddRoomSoftDeleteAudit)
        DeletedBy NVARCHAR(450),  -- Delta 3C: người xóa, liên kết mềm tới AspNetUsers.Id
        CONSTRAINT FK_Rooms_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties(Id)  -- Restrict: cấm xóa thật Properties khi còn Rooms trỏ tới
    )
END
GO

-- Delta 3C: cột xóa mềm phòng cho DB đã tồn tại
IF COL_LENGTH(N'dbo.Rooms', N'DeletedAt') IS NULL
    ALTER TABLE Rooms ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.Rooms', N'DeletedBy') IS NULL
    ALTER TABLE Rooms ADD DeletedBy NVARCHAR(450) NULL;
GO

-- mã phòng không trùng trong cùng 1 nhà (bỏ qua phòng đã xóa mềm)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Rooms_PropertyId_Code' AND object_id = OBJECT_ID(N'dbo.Rooms'))
    CREATE UNIQUE INDEX IX_Rooms_PropertyId_Code ON Rooms(PropertyId,Code) WHERE IsDeleted = 0;
GO

-- Khách thuê. 1 hồ sơ khách có 0 hoặc 1 tài khoản (chưa có thì để trống UserId).
-- Khách không gắn trực tiếp với Phòng: quan hệ đi qua Contracts/ContractMembers (P4).
IF OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
BEGIN
    CREATE TABLE Tenants
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_Tenants PRIMARY KEY,  -- khóa chính, tự tăng
        FullName NVARCHAR(200) NOT NULL,  -- họ tên khách
        Phone NVARCHAR(20) NOT NULL,  -- số điện thoại, bắt buộc
        Email NVARCHAR(256),  -- email, được để trống
        CitizenId NVARCHAR(20),  -- CCCD, có thì không được trùng
        UserId NVARCHAR(450),  -- tài khoản đăng nhập, có thể trống, trỏ tới AspNetUsers.Id (unique khi có giá trị)
        IsActive BIT NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT 1,  -- đang sử dụng (khác IsDeleted: thùng rác)
        RowVersion ROWVERSION NOT NULL,  -- chống ghi đè đồng thời
        Gender NVARCHAR(10),  -- giới tính, lưu chuỗi theo enum DTO
        DateOfBirth DATE,  -- ngày sinh
        IdIssuedDate DATE,  -- ngày cấp CCCD
        IdIssuedPlace NVARCHAR(200),  -- nơi cấp CCCD
        PermanentAddress NVARCHAR(300),  -- địa chỉ thường trú
        Note NVARCHAR(500),  -- ghi chú
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo, giờ UTC, app gán
        UpdatedAt DATETIME2,  -- ngày sửa gần nhất, giờ UTC
        IsDeleted BIT NOT NULL,  -- xóa mềm: 1 = ẩn khỏi app, dữ liệu vẫn còn trong DB
        DeletedAt DATETIME2,  -- Delta 3C: thời điểm xóa mềm (20261103044209_AddTenantTrash)
        DeletedByUserId NVARCHAR(450),  -- Delta 3C: người xóa, liên kết mềm tới AspNetUsers.Id
        CONSTRAINT FK_Tenants_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)  -- Restrict: cấm xóa thật AspNetUsers khi còn Tenants trỏ tới
    )
END
GO

-- Delta 3C: cột xóa mềm khách thuê cho DB đã tồn tại (Tenants.OwnerId thêm rồi bỏ ở 20261106182818)
IF COL_LENGTH(N'dbo.Tenants', N'DeletedAt') IS NULL
    ALTER TABLE Tenants ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'DeletedByUserId') IS NULL
    ALTER TABLE Tenants ADD DeletedByUserId NVARCHAR(450) NULL;
GO

-- VHT-23: cac cot ho so khach them sau, bo sung cho DB da ton tai
IF COL_LENGTH(N'dbo.Tenants', N'IsActive') IS NULL
    ALTER TABLE Tenants ADD IsActive BIT NOT NULL CONSTRAINT DF_Tenants_IsActive2 DEFAULT 1;
GO
IF COL_LENGTH(N'dbo.Tenants', N'RowVersion') IS NULL
    ALTER TABLE Tenants ADD RowVersion ROWVERSION NOT NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'Gender') IS NULL
    ALTER TABLE Tenants ADD Gender NVARCHAR(10) NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'DateOfBirth') IS NULL
    ALTER TABLE Tenants ADD DateOfBirth DATE NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'IdIssuedDate') IS NULL
    ALTER TABLE Tenants ADD IdIssuedDate DATE NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'IdIssuedPlace') IS NULL
    ALTER TABLE Tenants ADD IdIssuedPlace NVARCHAR(200) NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'PermanentAddress') IS NULL
    ALTER TABLE Tenants ADD PermanentAddress NVARCHAR(300) NULL;
GO
IF COL_LENGTH(N'dbo.Tenants', N'Note') IS NULL
    ALTER TABLE Tenants ADD Note NVARCHAR(500) NULL;
GO

-- CCCD khong trung voi ho so dang su dung (bo qua ho so trong CCCD, da xoa mem, da ngung dung)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_CitizenId' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE UNIQUE INDEX IX_Tenants_CitizenId ON Tenants(CitizenId) WHERE CitizenId IS NOT NULL AND IsDeleted = 0 AND IsActive = 1;
GO
-- 1 tai khoan chi 1 ho so: unique co dieu kien (bo qua ho so chua co UserId hoac da xoa mem)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_UserId' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE UNIQUE INDEX IX_Tenants_UserId ON Tenants(UserId) WHERE UserId IS NOT NULL AND IsDeleted = 0;
GO
-- tim kiem theo tu khoa tren danh sach khach
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_FullName' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE INDEX IX_Tenants_FullName ON Tenants(FullName);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Phone' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE INDEX IX_Tenants_Phone ON Tenants(Phone);
GO
-- Delta 3C: lọc nhanh hồ sơ trong thùng rác (20261103044209_AddTenantTrash)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_IsDeleted' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE INDEX IX_Tenants_IsDeleted ON Tenants(IsDeleted);
GO

-- ============================================================================
-- VHT-12/VHT-13 — Metadata tệp dùng chung (StoredFiles)
-- Tệp vật lý nằm ngoài wwwroot; bảng này chỉ là metadata, tải qua API có kiểm quyền.
-- ============================================================================
IF OBJECT_ID(N'dbo.StoredFiles', N'U') IS NULL
BEGIN
    CREATE TABLE StoredFiles
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StoredFiles PRIMARY KEY,  -- khóa chính do service sinh (không tự tăng)
        FileName NVARCHAR(260) NOT NULL,  -- tên gốc đã làm sạch, dùng cho Content-Disposition
        StoredFileName NVARCHAR(128) NOT NULL,  -- tên lưu trên server (uuid + đuôi), chỉ dùng nội bộ
        RelativePath NVARCHAR(512) NOT NULL,  -- đường dẫn tương đối trong Storage:RootPath, chỉ dùng nội bộ
        ContentType NVARCHAR(128) NOT NULL,  -- MIME do server xác định từ nội dung thật
        FileSizeBytes BIGINT NOT NULL,  -- kích thước tệp (byte)
        FileCategory INT NOT NULL,  -- enum: RoomPhoto/Contract/Invoice/IdentityDocument/IncidentPhoto/PaymentProof
        UploadedByUserId NVARCHAR(450) NOT NULL,  -- người tải lên, trỏ tới AspNetUsers.Id
        UploadedAt DATETIMEOFFSET NOT NULL,  -- thời điểm tải lên
        RelatedEntityId NVARCHAR(450) NOT NULL,  -- ID đối tượng liên quan (kiểu chuỗi, xem ghi chú ERD)
        RelatedEntityType INT NOT NULL,  -- enum: Room/Contract/Invoice/Tenant/MaintenanceIncident/Payment
        Description NVARCHAR(500),  -- ghi chú, được để trống
        IsDeleted BIT NOT NULL,  -- xóa mềm, tệp vật lý giữ lại theo chính sách lưu trữ
        CONSTRAINT FK_StoredFiles_AspNetUsers_UploadedByUserId FOREIGN KEY (UploadedByUserId) REFERENCES AspNetUsers(Id)  -- Restrict
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoredFiles_RelatedEntityType_RelatedEntityId' AND object_id = OBJECT_ID(N'dbo.StoredFiles'))
    CREATE INDEX IX_StoredFiles_RelatedEntityType_RelatedEntityId ON StoredFiles(RelatedEntityType, RelatedEntityId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoredFiles_UploadedByUserId' AND object_id = OBJECT_ID(N'dbo.StoredFiles'))
    CREATE INDEX IX_StoredFiles_UploadedByUserId ON StoredFiles(UploadedByUserId);
GO

-- ============================================================================
-- VHT-26 — Nhật ký truy cập tệp (append-only)
-- Cố ý KHÔNG có khóa ngoại tới AspNetUsers/StoredFiles: log phải còn khi tài khoản
-- hoặc tệp đã bị xóa. ActorUserId/FileId chỉ là giá trị tham chiếu mềm.
-- ============================================================================
IF OBJECT_ID(N'dbo.FileAccessAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE FileAccessAuditLogs
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FileAccessAuditLogs PRIMARY KEY,
        OccurredAt DATETIMEOFFSET NOT NULL,  -- thời điểm truy cập
        ActorUserId NVARCHAR(450) NOT NULL,  -- người thực hiện (tham chiếu mềm, không FK)
        ActorRole NVARCHAR(20) NOT NULL,  -- vai trò tại thời điểm thao tác
        Action NVARCHAR(20) NOT NULL,  -- Upload/Download/Delete/List...
        FileId UNIQUEIDENTIFIER,  -- tệp liên quan (tham chiếu mềm, không FK)
        FileCategory INT,  -- loại tệp tại thời điểm thao tác
        RelatedEntityType INT,  -- loại đối tượng liên quan
        RelatedEntityId NVARCHAR(450),  -- ID đối tượng liên quan
        Succeeded BIT NOT NULL,  -- thao tác thành công hay bị chặn
        ErrorCode NVARCHAR(50)  -- mã lỗi khi bị chặn
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileAccessAuditLogs_ActorUserId_OccurredAt' AND object_id = OBJECT_ID(N'dbo.FileAccessAuditLogs'))
    CREATE INDEX IX_FileAccessAuditLogs_ActorUserId_OccurredAt ON FileAccessAuditLogs(ActorUserId, OccurredAt);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileAccessAuditLogs_FileId' AND object_id = OBJECT_ID(N'dbo.FileAccessAuditLogs'))
    CREATE INDEX IX_FileAccessAuditLogs_FileId ON FileAccessAuditLogs(FileId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileAccessAuditLogs_RelatedEntityType_RelatedEntityId_OccurredAt' AND object_id = OBJECT_ID(N'dbo.FileAccessAuditLogs'))
    CREATE INDEX IX_FileAccessAuditLogs_RelatedEntityType_RelatedEntityId_OccurredAt ON FileAccessAuditLogs(RelatedEntityType, RelatedEntityId, OccurredAt);
GO

-- ============================================================================
-- VHT-94 — 21 công tắc Quản lý (SW-01..SW-16 + SW-15a..e)
-- Mode: 0 = Cho phép, 1 = Duyệt, 2 = Không thể. SW-15* chỉ nhận 0/2.
-- ============================================================================
IF OBJECT_ID(N'dbo.ManagerToggles', N'U') IS NULL
BEGIN
    CREATE TABLE ManagerToggles
    (
        Code NVARCHAR(10) NOT NULL CONSTRAINT PK_ManagerToggles PRIMARY KEY,  -- mã công tắc SW-xx
        Mode INT NOT NULL,  -- 0 Cho phép, 1 Duyệt, 2 Không thể
        UpdatedAt DATETIMEOFFSET NOT NULL,  -- lần đổi gần nhất
        UpdatedByUserId NVARCHAR(450),  -- người đổi gần nhất, có thể trống (mặc định hệ thống)
        CONSTRAINT FK_ManagerToggles_AspNetUsers_UpdatedByUserId FOREIGN KEY (UpdatedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT CK_ManagerToggles_Mode CHECK (Mode IN (0, 1, 2)),
        CONSTRAINT CK_ManagerToggles_SW15 CHECK (Code <> 'SW-15' OR Mode IN (0, 2)),
        CONSTRAINT CK_ManagerToggles_SW15Sub CHECK (Code NOT LIKE 'SW-15_' OR Mode IN (0, 2))
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagerToggles_UpdatedByUserId' AND object_id = OBJECT_ID(N'dbo.ManagerToggles'))
    CREATE INDEX IX_ManagerToggles_UpdatedByUserId ON ManagerToggles(UpdatedByUserId);
GO

-- ============================================================================
-- VHT-24 — Phân công Quản lý theo tòa nhà
-- Mỗi dòng là một lượt giao. Thu hồi = đặt RevokedAt (giữ lịch sử), KHÔNG xóa dòng.
-- Quản lý chỉ thấy dữ liệu của tòa có dòng còn hiệu lực (RevokedAt IS NULL).
-- ============================================================================
IF OBJECT_ID(N'dbo.PropertyManagers', N'U') IS NULL
BEGIN
    CREATE TABLE PropertyManagers
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_PropertyManagers PRIMARY KEY,  -- khóa chính, tự tăng
        PropertyId INT NOT NULL,  -- tòa nhà được giao, trỏ tới Properties.Id
        ManagerUserId NVARCHAR(450) NOT NULL,  -- tài khoản Quản lý, trỏ tới AspNetUsers.Id
        AssignedAt DATETIME2 NOT NULL,  -- thời điểm giao
        AssignedByUserId NVARCHAR(450) NOT NULL,  -- Chủ trọ thực hiện giao, trỏ tới AspNetUsers.Id
        RevokedAt DATETIME2,  -- NULL = còn hiệu lực; có giá trị = đã thu hồi
        RevokedByUserId NVARCHAR(450),  -- người thu hồi, trỏ tới AspNetUsers.Id
        CONSTRAINT FK_PropertyManagers_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties(Id),  -- Restrict
        CONSTRAINT FK_PropertyManagers_AspNetUsers_ManagerUserId FOREIGN KEY (ManagerUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT FK_PropertyManagers_AspNetUsers_AssignedByUserId FOREIGN KEY (AssignedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT FK_PropertyManagers_AspNetUsers_RevokedByUserId FOREIGN KEY (RevokedByUserId) REFERENCES AspNetUsers(Id)  -- Restrict
    )
END
GO

-- 1 Quản lý chỉ có 1 dòng còn hiệu lực trên 1 tòa (dòng đã thu hồi được phép trùng)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PropertyManagers_PropertyId_ManagerUserId' AND object_id = OBJECT_ID(N'dbo.PropertyManagers'))
    CREATE UNIQUE INDEX IX_PropertyManagers_PropertyId_ManagerUserId ON PropertyManagers(PropertyId, ManagerUserId) WHERE RevokedAt IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PropertyManagers_PropertyId' AND object_id = OBJECT_ID(N'dbo.PropertyManagers'))
    CREATE INDEX IX_PropertyManagers_PropertyId ON PropertyManagers(PropertyId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PropertyManagers_ManagerUserId' AND object_id = OBJECT_ID(N'dbo.PropertyManagers'))
    CREATE INDEX IX_PropertyManagers_ManagerUserId ON PropertyManagers(ManagerUserId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PropertyManagers_AssignedByUserId' AND object_id = OBJECT_ID(N'dbo.PropertyManagers'))
    CREATE INDEX IX_PropertyManagers_AssignedByUserId ON PropertyManagers(AssignedByUserId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PropertyManagers_RevokedByUserId' AND object_id = OBJECT_ID(N'dbo.PropertyManagers'))
    CREATE INDEX IX_PropertyManagers_RevokedByUserId ON PropertyManagers(RevokedByUserId);
GO

-- ============================================================================
-- P4 — Hợp đồng thuê (Contract) và thành viên hợp đồng (ContractMembers)
-- 1 phòng có N hợp đồng theo thời gian, nhưng chỉ 1 hợp đồng ĐÃ DUYỆT không chồng lấn
-- (trigger TR_Contracts_PreventApprovedOverlap bên dưới). Không suy ra "đang thuê" từ Room.Status.
-- ============================================================================
IF OBJECT_ID(N'dbo.Contracts', N'U') IS NULL
BEGIN
    CREATE TABLE Contracts
    (
        ContractId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Contracts PRIMARY KEY,  -- khóa chính do service sinh
        ContractNumber NVARCHAR(40) NOT NULL,  -- số hợp đồng, không trùng toàn hệ thống
        RoomId INT NOT NULL,  -- phòng được thuê, trỏ tới Rooms.Id
        StartDate DATE NOT NULL,  -- ngày bắt đầu
        EndDate DATE NOT NULL,  -- ngày kết thúc (phải >= StartDate)
        SnapshotPrice DECIMAL(18,2) NOT NULL,  -- giá phòng chốt tại thời điểm lập hợp đồng (VND)
        DepositAmount DECIMAL(18,2) NOT NULL,  -- tiền cọc (VND)
        Status INT NOT NULL,  -- 0 Chờ duyệt, 1 Đã duyệt, 2 Từ chối
        RejectReason NVARCHAR(500),  -- lý do từ chối, chỉ có khi Status = 2
        CONSTRAINT FK_Contracts_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES Rooms(Id)  -- Restrict
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Contracts_ContractNumber' AND object_id = OBJECT_ID(N'dbo.Contracts'))
    CREATE UNIQUE INDEX IX_Contracts_ContractNumber ON Contracts(ContractNumber);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Contracts_RoomId' AND object_id = OBJECT_ID(N'dbo.Contracts'))
    CREATE INDEX IX_Contracts_RoomId ON Contracts(RoomId);
GO

-- Chặn 2 hợp đồng đã duyệt (Status = 1) chồng khoảng ngày trên cùng 1 phòng.
-- Migration AddContracts không đặt CHECK cho EndDate >= StartDate; service kiểm, ERD ghi nhận đúng như schema.
-- Nội dung trigger trùng với NhaTro/Web/Data/Migrations/ContractOverlapTriggerSql.cs (nguồn chuẩn dùng chung).
IF OBJECT_ID(N'dbo.TR_Contracts_PreventApprovedOverlap', N'TR') IS NULL
    EXEC(N'
    CREATE TRIGGER [dbo].[TR_Contracts_PreventApprovedOverlap]
    ON [dbo].[Contracts]
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;

        IF EXISTS
        (
            SELECT 1
            FROM inserted AS candidate
            INNER JOIN [dbo].[Contracts] AS existing WITH (UPDLOCK, HOLDLOCK)
                ON existing.[RoomId] = candidate.[RoomId]
                AND existing.[Status] = 1
                AND existing.[ContractId] <> candidate.[ContractId]
                AND existing.[StartDate] <= candidate.[EndDate]
                AND candidate.[StartDate] <= existing.[EndDate]
            WHERE candidate.[Status] = 1
        )
        BEGIN
            THROW 51030, ''ContractOverlap'', 1;
        END
    END;
    ');
GO

-- Thành viên hợp đồng: đại diện và người ở cùng. Khóa chính ghép (hợp đồng, khách).
IF OBJECT_ID(N'dbo.ContractMembers', N'U') IS NULL
BEGIN
    CREATE TABLE ContractMembers
    (
        ContractId UNIQUEIDENTIFIER NOT NULL,  -- trỏ tới Contracts.ContractId
        TenantId INT NOT NULL,  -- trỏ tới Tenants.Id
        CONSTRAINT PK_ContractMembers PRIMARY KEY (ContractId, TenantId),
        CONSTRAINT FK_ContractMembers_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts(ContractId),  -- Restrict
        CONSTRAINT FK_ContractMembers_Tenants_TenantId FOREIGN KEY (TenantId) REFERENCES Tenants(Id)  -- Restrict
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractMembers_TenantId' AND object_id = OBJECT_ID(N'dbo.ContractMembers'))
    CREATE INDEX IX_ContractMembers_TenantId ON ContractMembers(TenantId);
GO

-- ============================================================================
-- P5 — Dịch vụ và biểu giá dịch vụ
-- ServiceCatalogs: danh mục dịch vụ theo tòa (PropertyId NULL = dùng chung toàn hệ thống).
-- ServicePriceHistory: mỗi lần đổi giá thêm một dòng, không sửa dòng cũ.
-- ============================================================================
IF OBJECT_ID(N'dbo.ServiceCatalogs', N'U') IS NULL
BEGIN
    CREATE TABLE ServiceCatalogs
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_ServiceCatalogs PRIMARY KEY,  -- khóa chính, tự tăng
        PropertyId INT,  -- tòa áp dụng, NULL = dùng chung; trỏ tới Properties.Id
        IsReadOnly BIT NOT NULL CONSTRAINT DF_ServiceCatalogs_IsReadOnly DEFAULT 0,  -- 1 = danh mục hệ thống, app không sửa
        IsActive BIT NOT NULL CONSTRAINT DF_ServiceCatalogs_IsActive DEFAULT 1,  -- Delta 3C: đang dùng (20261102152923)
        Description NVARCHAR(500),  -- Delta 3C: mô tả dịch vụ (20261103033629)
        IsDeleted BIT NOT NULL CONSTRAINT DF_ServiceCatalogs_IsDeleted DEFAULT 0,  -- Delta 3C: xóa mềm (20261105135750)
        DeletedAt DATETIME2,  -- Delta 3C: thời điểm xóa mềm
        DeletedBy NVARCHAR(450),  -- Delta 3C: người xóa, liên kết mềm tới AspNetUsers.Id
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo, giờ UTC, app gán
        CONSTRAINT FK_ServiceCatalogs_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties(Id)  -- Restrict
    )
END
GO

-- Delta 3C: cột danh mục dịch vụ thêm sau, bổ sung cho DB đã tồn tại
IF COL_LENGTH(N'dbo.ServiceCatalogs', N'IsActive') IS NULL
    ALTER TABLE ServiceCatalogs ADD IsActive BIT NOT NULL CONSTRAINT DF_ServiceCatalogs_IsActive2 DEFAULT 1;
GO
IF COL_LENGTH(N'dbo.ServiceCatalogs', N'Description') IS NULL
    ALTER TABLE ServiceCatalogs ADD Description NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.ServiceCatalogs', N'IsDeleted') IS NULL
    ALTER TABLE ServiceCatalogs ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ServiceCatalogs_IsDeleted2 DEFAULT 0;
GO
IF COL_LENGTH(N'dbo.ServiceCatalogs', N'DeletedAt') IS NULL
    ALTER TABLE ServiceCatalogs ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.ServiceCatalogs', N'DeletedBy') IS NULL
    ALTER TABLE ServiceCatalogs ADD DeletedBy NVARCHAR(450) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServiceCatalogs_PropertyId' AND object_id = OBJECT_ID(N'dbo.ServiceCatalogs'))
    CREATE INDEX IX_ServiceCatalogs_PropertyId ON ServiceCatalogs(PropertyId);
GO

IF OBJECT_ID(N'dbo.ServicePriceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE ServicePriceHistory
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_ServicePriceHistory PRIMARY KEY,  -- khóa chính, tự tăng
        ServiceCatalogId INT NOT NULL,  -- dịch vụ, trỏ tới ServiceCatalogs.Id
        Name NVARCHAR(150) NOT NULL,  -- tên dịch vụ tại phiên bản giá này
        Unit NVARCHAR(30) NOT NULL,  -- đơn vị tính (người, phòng, kWh...)
        CalculationMethod NVARCHAR(100) NOT NULL,  -- cách tính, không được rỗng
        UnitPrice DECIMAL(18,4) NOT NULL,  -- đơn giá (VND), 4 số lẻ
        EffectiveDate DATE NOT NULL,  -- ngày bắt đầu áp dụng
        EffectiveTo DATE,  -- Delta 3C: ngày hết hiệu lực, NULL = còn hiệu lực (20261102152923)
        Note NVARCHAR(500),  -- Delta 3C: ghi chú lần đổi giá (20261102163137)
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo bản ghi, giờ UTC
        CreatedByUserId NVARCHAR(450),  -- người tạo, trỏ tới AspNetUsers.Id (20261102081030 đổi int -> nvarchar(450))
        CONSTRAINT FK_ServicePriceHistory_ServiceCatalogs_ServiceCatalogId FOREIGN KEY (ServiceCatalogId) REFERENCES ServiceCatalogs(Id),  -- Restrict
        CONSTRAINT FK_ServicePriceHistory_AspNetUsers_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT CK_ServicePriceHistory_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
        CONSTRAINT CK_ServicePriceHistory_Unit_NotBlank CHECK (LEN(LTRIM(RTRIM(Unit))) > 0),
        CONSTRAINT CK_ServicePriceHistory_CalculationMethod_NotBlank CHECK (LEN(LTRIM(RTRIM(CalculationMethod))) > 0),
        CONSTRAINT CK_ServicePriceHistory_UnitPrice_Positive CHECK (UnitPrice > 0)
    )
END
GO

-- Delta 3C: cột mới cho DB đã tồn tại (phải chạy trước CHECK EffectiveTo bên dưới)
IF COL_LENGTH(N'dbo.ServicePriceHistory', N'EffectiveTo') IS NULL
    ALTER TABLE ServicePriceHistory ADD EffectiveTo DATE NULL;
GO
IF COL_LENGTH(N'dbo.ServicePriceHistory', N'Note') IS NULL
    ALTER TABLE ServicePriceHistory ADD Note NVARCHAR(500) NULL;
GO

-- Delta 3C: đổi CHECK đơn giá cũ (>= 0) sang > 0 cho DB đã tồn tại; thêm CHECK EffectiveTo.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ServicePriceHistory_UnitPrice_NonNegative' AND parent_object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    ALTER TABLE ServicePriceHistory DROP CONSTRAINT CK_ServicePriceHistory_UnitPrice_NonNegative;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ServicePriceHistory_UnitPrice_Positive' AND parent_object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    ALTER TABLE ServicePriceHistory ADD CONSTRAINT CK_ServicePriceHistory_UnitPrice_Positive CHECK (UnitPrice > 0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ServicePriceHistory_EffectiveTo_After_EffectiveDate' AND parent_object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    ALTER TABLE ServicePriceHistory ADD CONSTRAINT CK_ServicePriceHistory_EffectiveTo_After_EffectiveDate CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveDate]);
GO

-- Delta 3C: đổi CreatedByUserId sang nvarchar(450) cho DB đã tồn tại
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
           WHERE c.object_id = OBJECT_ID(N'dbo.ServicePriceHistory') AND c.name = N'CreatedByUserId' AND t.name = 'int')
    ALTER TABLE ServicePriceHistory ALTER COLUMN CreatedByUserId NVARCHAR(450) NULL;
GO
-- Giữ đúng hành vi migration 20261102081030: giá trị không trỏ tới tài khoản nào thì đưa về NULL trước khi thêm FK.
UPDATE ServicePriceHistory
SET CreatedByUserId = NULL
WHERE CreatedByUserId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id = ServicePriceHistory.CreatedByUserId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ServicePriceHistory_AspNetUsers_CreatedByUserId' AND parent_object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    ALTER TABLE ServicePriceHistory ADD CONSTRAINT FK_ServicePriceHistory_AspNetUsers_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES AspNetUsers(Id);
GO

-- 1 dịch vụ chỉ có 1 giá cho 1 ngày hiệu lực
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServicePriceHistory_ServiceCatalogId_EffectiveDate' AND object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    CREATE UNIQUE INDEX IX_ServicePriceHistory_ServiceCatalogId_EffectiveDate ON ServicePriceHistory(ServiceCatalogId, EffectiveDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServicePriceHistory_EffectiveDate_ServiceCatalogId' AND object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    CREATE INDEX IX_ServicePriceHistory_EffectiveDate_ServiceCatalogId ON ServicePriceHistory(EffectiveDate, ServiceCatalogId);
GO
-- Delta 3C: tìm giá theo người tạo (20261102081030)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServicePriceHistory_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.ServicePriceHistory'))
    CREATE INDEX IX_ServicePriceHistory_CreatedByUserId ON ServicePriceHistory(CreatedByUserId);
GO

-- ============================================================================
-- VHT-100 — Biểu giá điện/nước theo bậc (dữ liệu chuẩn, chỉ đọc)
-- UtilityTariffs: 1 phiên bản biểu giá theo (loại, phạm vi, ngày hiệu lực).
-- UtilityTariffTiers: các bậc của phiên bản đó. Sửa giá = THÊM phiên bản mới, không sửa bản cũ.
-- CK_UtilityTariffs_ReadOnly ép IsReadOnly = 1: mọi bản ghi đều là dữ liệu tham chiếu bất biến.
-- ============================================================================
IF OBJECT_ID(N'dbo.UtilityTariffs', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityTariffs
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityTariffs PRIMARY KEY,  -- khóa chính, tự tăng
        Type INT NOT NULL,  -- 0 Điện, 1 Nước
        ScopeCode NVARCHAR(50) NOT NULL,  -- phạm vi áp dụng, vd VN-EVN-HOUSEHOLD, HCMC-BENTHANH
        Unit NVARCHAR(20) NOT NULL,  -- đơn vị: kWh, m³
        IsPerResident BIT NOT NULL,  -- 1 = tính theo đầu người, 0 = theo đồng hồ
        EffectiveDate DATE NOT NULL,  -- ngày bắt đầu áp dụng
        IsReadOnly BIT NOT NULL CONSTRAINT DF_UtilityTariffs_IsReadOnly DEFAULT 1,  -- luôn 1 (xem CHECK bên dưới)
        SourceName NVARCHAR(200) NOT NULL,  -- tên nguồn ban hành
        SourceReference NVARCHAR(500) NOT NULL,  -- đường dẫn/tài liệu nguồn
        CONSTRAINT CK_UtilityTariffs_ReadOnly CHECK (IsReadOnly = 1)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityTariffs_Type_ScopeCode_EffectiveDate' AND object_id = OBJECT_ID(N'dbo.UtilityTariffs'))
    CREATE UNIQUE INDEX IX_UtilityTariffs_Type_ScopeCode_EffectiveDate ON UtilityTariffs(Type, ScopeCode, EffectiveDate);
GO

IF OBJECT_ID(N'dbo.UtilityTariffTiers', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityTariffTiers
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityTariffTiers PRIMARY KEY,  -- khóa chính, tự tăng
        UtilityTariffId INT NOT NULL,  -- phiên bản biểu giá, trỏ tới UtilityTariffs.Id
        TierNumber INT NOT NULL,  -- bậc thứ mấy, bắt đầu từ 1
        [From] DECIMAL(18,4) NOT NULL,  -- cận dưới (>= 0)
        [To] DECIMAL(18,4),  -- cận trên, NULL = bậc cuối không giới hạn
        UnitPrice DECIMAL(18,4) NOT NULL,  -- đơn giá của bậc (VND)
        CONSTRAINT FK_UtilityTariffTiers_UtilityTariffs_UtilityTariffId FOREIGN KEY (UtilityTariffId) REFERENCES UtilityTariffs(Id),  -- Restrict
        CONSTRAINT CK_UtilityTariffTiers_TierNumber_Positive CHECK (TierNumber > 0),
        CONSTRAINT CK_UtilityTariffTiers_From_NonNegative CHECK ([From] >= 0),
        CONSTRAINT CK_UtilityTariffTiers_To_After_From CHECK ([To] IS NULL OR [To] > [From]),
        CONSTRAINT CK_UtilityTariffTiers_UnitPrice_NonNegative CHECK (UnitPrice >= 0)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityTariffTiers_UtilityTariffId_TierNumber' AND object_id = OBJECT_ID(N'dbo.UtilityTariffTiers'))
    CREATE UNIQUE INDEX IX_UtilityTariffTiers_UtilityTariffId_TierNumber ON UtilityTariffTiers(UtilityTariffId, TierNumber);
GO

-- ============================================================================
-- P5 — Ảnh chụp chỉ số và chi phí của 1 kỳ tính tiền
-- Ghi lại đúng biểu giá đã dùng tại thời điểm chốt kỳ, để sửa biểu giá sau này
-- không làm sai hóa đơn cũ. BillingPeriodId là tham chiếu mềm (P5 chưa có bảng kỳ).
-- ============================================================================
IF OBJECT_ID(N'dbo.UtilityBillingPeriodSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityBillingPeriodSnapshots
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityBillingPeriodSnapshots PRIMARY KEY,  -- khóa chính, tự tăng
        Type INT NOT NULL,  -- 0 Điện, 1 Nước
        ScopeCode NVARCHAR(50) NOT NULL,  -- phạm vi biểu giá đã áp
        BillingPeriodId INT NOT NULL,  -- kỳ tính tiền (số), KHÔNG có FK: P5 chưa có bảng kỳ
        Unit NVARCHAR(30) NOT NULL,  -- đơn vị: kWh, m³
        EffectiveDate DATE NOT NULL,  -- ngày hiệu lực của biểu giá đã dùng
        PeriodStart DATE NOT NULL,  -- đầu kỳ
        PeriodEnd DATE NOT NULL,  -- cuối kỳ, phải >= PeriodStart
        Quantity DECIMAL(18,4) NOT NULL,  -- tổng lượng tiêu thụ (>= 0)
        ResidentCount INT NOT NULL,  -- số người ở tại thời điểm chốt (> 0)
        Total DECIMAL(18,4) NOT NULL,  -- tổng tiền (>= 0)
        SourceName NVARCHAR(200) NOT NULL,  -- nguồn biểu giá
        SourceReference NVARCHAR(500) NOT NULL,  -- tài liệu nguồn
        CreatedAt DATETIME2 NOT NULL,  -- thời điểm chốt, giờ UTC
        CONSTRAINT CK_UtilityBillingPeriodSnapshots_BillingPeriodId CHECK (BillingPeriodId > 0),
        CONSTRAINT CK_UtilityBillingPeriodSnapshots_Period CHECK (PeriodEnd >= PeriodStart),
        CONSTRAINT CK_UtilityBillingPeriodSnapshots_Quantity CHECK (Quantity >= 0),
        CONSTRAINT CK_UtilityBillingPeriodSnapshots_ResidentCount CHECK (ResidentCount > 0),
        CONSTRAINT CK_UtilityBillingPeriodSnapshots_Total CHECK (Total >= 0)
    )
END
GO

-- 1 kỳ chỉ có 1 ảnh chụp cho mỗi loại (điện/nước)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityBillingPeriodSnapshots_BillingPeriodId_Type' AND object_id = OBJECT_ID(N'dbo.UtilityBillingPeriodSnapshots'))
    CREATE UNIQUE INDEX IX_UtilityBillingPeriodSnapshots_BillingPeriodId_Type ON UtilityBillingPeriodSnapshots(BillingPeriodId, Type);
GO

IF OBJECT_ID(N'dbo.UtilityBillingCostLineSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityBillingCostLineSnapshots
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityBillingCostLineSnapshots PRIMARY KEY,  -- khóa chính, tự tăng
        UtilityBillingPeriodSnapshotId INT NOT NULL,  -- ảnh chụp kỳ, trỏ tới UtilityBillingPeriodSnapshots.Id
        TierNumber INT NOT NULL,  -- bậc thứ mấy (> 0)
        TariffVersionId INT NOT NULL,  -- phiên bản biểu giá đã dùng, trỏ tới UtilityTariffs.Id
        EffectiveDate DATE NOT NULL,  -- ngày hiệu lực của phiên bản đó
        [From] DECIMAL(18,4) NOT NULL,  -- cận dưới của bậc
        [To] DECIMAL(18,4),  -- cận trên, NULL = không giới hạn
        Quantity DECIMAL(18,4) NOT NULL,  -- lượng tiêu thụ trong bậc (>= 0)
        UnitPrice DECIMAL(18,4) NOT NULL,  -- đơn giá (>= 0)
        Amount DECIMAL(18,4) NOT NULL,  -- thành tiền của bậc (>= 0)
        CONSTRAINT FK_UtilityBillingCostLineSnapshots_UtilityBillingPeriodSnapshots_UtilityBillingPeriodSnapshotId FOREIGN KEY (UtilityBillingPeriodSnapshotId) REFERENCES UtilityBillingPeriodSnapshots(Id),  -- Restrict
        CONSTRAINT FK_UtilityBillingCostLineSnapshots_UtilityTariffs_TariffVersionId FOREIGN KEY (TariffVersionId) REFERENCES UtilityTariffs(Id),  -- Restrict
        CONSTRAINT CK_UtilityBillingCostLineSnapshots_TierNumber CHECK (TierNumber > 0),
        CONSTRAINT CK_UtilityBillingCostLineSnapshots_Range CHECK ([To] IS NULL OR [To] > [From]),
        CONSTRAINT CK_UtilityBillingCostLineSnapshots_Quantity CHECK (Quantity >= 0),
        CONSTRAINT CK_UtilityBillingCostLineSnapshots_UnitPrice CHECK (UnitPrice >= 0),
        CONSTRAINT CK_UtilityBillingCostLineSnapshots_Amount CHECK (Amount >= 0)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityBillingCostLineSnapshots_UtilityBillingPeriodSnapshotId_TariffVersionId_TierNumber' AND object_id = OBJECT_ID(N'dbo.UtilityBillingCostLineSnapshots'))
    CREATE UNIQUE INDEX IX_UtilityBillingCostLineSnapshots_UtilityBillingPeriodSnapshotId_TariffVersionId_TierNumber ON UtilityBillingCostLineSnapshots(UtilityBillingPeriodSnapshotId, TariffVersionId, TierNumber);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityBillingCostLineSnapshots_TariffVersionId' AND object_id = OBJECT_ID(N'dbo.UtilityBillingCostLineSnapshots'))
    CREATE INDEX IX_UtilityBillingCostLineSnapshots_TariffVersionId ON UtilityBillingCostLineSnapshots(TariffVersionId);
GO

-- ============================================================================
-- P6 — Hóa đơn và dòng hóa đơn
-- D13: không cho hủy hóa đơn trong v1.0 nên enum không có Cancelled và bảng KHÔNG có IsDeleted.
-- Tiền lưu DECIMAL(18,2); dòng Discount nhập số dương, service đổi dấu khi cộng vào tổng.
-- ============================================================================
IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
BEGIN
    CREATE TABLE Invoices
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_Invoices PRIMARY KEY,  -- khóa chính, tự tăng
        ContractId UNIQUEIDENTIFIER NOT NULL,  -- hợp đồng được lập hóa đơn, trỏ tới Contracts.ContractId
        PeriodStart DATE NOT NULL,  -- đầu kỳ
        PeriodEnd DATE NOT NULL,  -- cuối kỳ, phải >= PeriodStart
        Status INT NOT NULL,  -- 0 Nháp, 1 Đã phát hành, 2 Đã thu
        TotalAmount DECIMAL(18,2) NOT NULL,  -- tổng tiền (>= 0)
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo, giờ UTC, app gán
        UpdatedAt DATETIME2,  -- ngày sửa gần nhất
        IssuedAt DATETIME2,  -- thời điểm phát hành, NULL khi còn nháp
        RowVersion ROWVERSION NOT NULL,  -- chống ghi đè đồng thời
        CONSTRAINT FK_Invoices_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts(ContractId),  -- Restrict
        CONSTRAINT CK_Invoices_Period CHECK (PeriodEnd >= PeriodStart),
        CONSTRAINT CK_Invoices_Status CHECK (Status IN (0, 1, 2)),
        CONSTRAINT CK_Invoices_TotalAmount CHECK (TotalAmount >= 0)
    )
END
GO

-- 1 hợp đồng chỉ có 1 hóa đơn cho 1 kỳ bắt đầu
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_ContractId_PeriodStart' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE UNIQUE INDEX IX_Invoices_ContractId_PeriodStart ON Invoices(ContractId, PeriodStart);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_PeriodStart_Status' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX IX_Invoices_PeriodStart_Status ON Invoices(PeriodStart, Status);
GO

IF OBJECT_ID(N'dbo.InvoiceItems', N'U') IS NULL
BEGIN
    CREATE TABLE InvoiceItems
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_InvoiceItems PRIMARY KEY,  -- khóa chính, tự tăng
        InvoiceId INT NOT NULL,  -- hóa đơn chứa dòng, trỏ tới Invoices.Id
        ItemType INT NOT NULL,  -- 0 Tiền phòng, 1 Điện, 2 Nước, 3 Dịch vụ, 4 Giảm giá
        Description NVARCHAR(250) NOT NULL,  -- diễn giải dòng
        Unit NVARCHAR(30),  -- đơn vị tính, được để trống
        Quantity DECIMAL(18,4) NOT NULL,  -- số lượng, phải > 0
        UnitPrice DECIMAL(18,4) NOT NULL,  -- đơn giá (>= 0)
        Amount DECIMAL(18,2) NOT NULL,  -- thành tiền; âm CHỈ khi ItemType = 4 (giảm giá)
        DisplayOrder INT NOT NULL,  -- thứ tự hiển thị; không unique nên đọc phải sắp kèm Id
        CONSTRAINT FK_InvoiceItems_Invoices_InvoiceId FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id),  -- Restrict
        CONSTRAINT CK_InvoiceItems_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_InvoiceItems_UnitPrice CHECK (UnitPrice >= 0),
        CONSTRAINT CK_InvoiceItems_AmountSign CHECK ((ItemType = 4 AND Amount <= 0) OR (ItemType <> 4 AND Amount >= 0))
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceItems_InvoiceId_DisplayOrder' AND object_id = OBJECT_ID(N'dbo.InvoiceItems'))
    CREATE INDEX IX_InvoiceItems_InvoiceId_DisplayOrder ON InvoiceItems(InvoiceId, DisplayOrder);
GO

-- VHT-51: placed after Contracts, Rooms and Invoices so all FK targets exist.
IF OBJECT_ID(N'dbo.UtilityReadingPeriods', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityReadingPeriods
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityReadingPeriods PRIMARY KEY,
        ContractId UNIQUEIDENTIFIER NOT NULL, RoomId INT NOT NULL,
        PeriodStart DATE NOT NULL, PeriodEnd DATE NOT NULL,
        ElectricStart DECIMAL(18,4) NOT NULL, ElectricEnd DECIMAL(18,4) NOT NULL,
        WaterStart DECIMAL(18,4) NOT NULL, WaterEnd DECIMAL(18,4) NOT NULL,
        ResidentCount INT NOT NULL, Status INT NOT NULL, InvoiceId INT NULL,
        HasReadings BIT NOT NULL, HasServiceSnapshots BIT NOT NULL CONSTRAINT DF_UtilityReadingPeriods_HasServiceSnapshots DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL, UpdatedAt DATETIME2 NOT NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_UtilityReadingPeriods_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts(ContractId),
        CONSTRAINT FK_UtilityReadingPeriods_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES Rooms(Id),
        CONSTRAINT FK_UtilityReadingPeriods_Invoices_InvoiceId FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id),
        CONSTRAINT CK_UtilityReadingPeriods_Period CHECK (PeriodEnd >= PeriodStart),
        CONSTRAINT CK_UtilityReadingPeriods_Electric CHECK (ElectricStart >= 0 AND ElectricEnd >= ElectricStart),
        CONSTRAINT CK_UtilityReadingPeriods_Water CHECK (WaterStart >= 0 AND WaterEnd >= WaterStart),
        CONSTRAINT CK_UtilityReadingPeriods_ResidentCount CHECK (ResidentCount > 0),
        CONSTRAINT CK_UtilityReadingPeriods_Status CHECK (Status IN (0, 1)),
        CONSTRAINT CK_UtilityReadingPeriods_InvoicedHasInvoice CHECK (Status = 0 OR InvoiceId IS NOT NULL),
        CONSTRAINT CK_UtilityReadingPeriods_InvoicedHasReadings CHECK (Status = 0 OR HasReadings = 1),
        CONSTRAINT CK_UtilityReadingPeriods_ServiceSnapshotsRequireReadings CHECK (HasServiceSnapshots = 0 OR HasReadings = 1)
    )
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityReadingPeriods_ContractId_PeriodStart' AND object_id = OBJECT_ID(N'dbo.UtilityReadingPeriods'))
    CREATE UNIQUE INDEX IX_UtilityReadingPeriods_ContractId_PeriodStart ON UtilityReadingPeriods(ContractId, PeriodStart);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityReadingPeriods_ContractId_PeriodEnd' AND object_id = OBJECT_ID(N'dbo.UtilityReadingPeriods'))
    CREATE INDEX IX_UtilityReadingPeriods_ContractId_PeriodEnd ON UtilityReadingPeriods(ContractId, PeriodEnd);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityReadingPeriods_RoomId' AND object_id = OBJECT_ID(N'dbo.UtilityReadingPeriods'))
    CREATE INDEX IX_UtilityReadingPeriods_RoomId ON UtilityReadingPeriods(RoomId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityReadingPeriods_InvoiceId' AND object_id = OBJECT_ID(N'dbo.UtilityReadingPeriods'))
    CREATE UNIQUE INDEX IX_UtilityReadingPeriods_InvoiceId ON UtilityReadingPeriods(InvoiceId) WHERE InvoiceId IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.UtilityServicePeriodSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityServicePeriodSnapshots
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityServicePeriodSnapshots PRIMARY KEY,
        UtilityReadingPeriodId INT NOT NULL, SourceServiceCatalogId INT NOT NULL, SourceServicePriceHistoryId INT NOT NULL,
        Description NVARCHAR(200) NOT NULL, Unit NVARCHAR(30) NOT NULL,
        Quantity DECIMAL(18,4) NOT NULL, UnitPrice DECIMAL(18,4) NOT NULL, Amount DECIMAL(18,0) NOT NULL,
        CONSTRAINT FK_UtilityServicePeriodSnapshots_UtilityReadingPeriods_UtilityReadingPeriodId FOREIGN KEY (UtilityReadingPeriodId) REFERENCES UtilityReadingPeriods(Id) ON DELETE CASCADE,
        CONSTRAINT CK_UtilityServicePeriodSnapshots_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_UtilityServicePeriodSnapshots_UnitPrice CHECK (UnitPrice >= 0),
        CONSTRAINT CK_UtilityServicePeriodSnapshots_Amount CHECK (Amount >= 0)
    )
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityServicePeriodSnapshots_UtilityReadingPeriodId_SourceServicePriceHistoryId' AND object_id = OBJECT_ID(N'dbo.UtilityServicePeriodSnapshots'))
    CREATE UNIQUE INDEX IX_UtilityServicePeriodSnapshots_UtilityReadingPeriodId_SourceServicePriceHistoryId ON UtilityServicePeriodSnapshots(UtilityReadingPeriodId, SourceServicePriceHistoryId);
GO

-- ============================================================================
-- VHT-96 — Hàng đợi chờ Chủ trọ duyệt thao tác của Quản lý
-- Yêu cầu đang Chờ duyệt chưa đổi dữ liệu nghiệp vụ; khi duyệt mới áp trong cùng transaction.
-- PayloadJson chứa thao tác gốc; ExpectedToken là token đồng thời của bản ghi đích.
-- UX_ManagerApprovalRequests_Pending: 1 đối tượng chỉ có 1 yêu cầu đang chờ.
-- ============================================================================
IF OBJECT_ID(N'dbo.ManagerApprovalRequests', N'U') IS NULL
BEGIN
    CREATE TABLE ManagerApprovalRequests
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ManagerApprovalRequests PRIMARY KEY,  -- khóa chính do service sinh
        OperationCode NVARCHAR(10) NOT NULL,  -- mã công tắc SW-xx của thao tác
        TargetType INT NOT NULL,  -- 0 Không rõ, 1 Phòng, 2 Khách thuê, 3 Hợp đồng, 4 Kỳ điện nước
        TargetKey NVARCHAR(200) NOT NULL,  -- khóa đối tượng bị tác động (dạng chuỗi)
        PropertyId INT,  -- tòa liên quan, trỏ tới Properties.Id; NULL nếu không gắn tòa
        PayloadJson NVARCHAR(MAX) NOT NULL,  -- nội dung thao tác chờ duyệt (JSON)
        ExpectedToken VARBINARY(MAX),  -- token đồng thời của bản ghi đích tại lúc gửi
        Status INT NOT NULL,  -- 0 Chờ duyệt, 1 Đã duyệt, 2 Từ chối, 3 Đã hủy
        RequestedByUserId NVARCHAR(450) NOT NULL,  -- Quản lý gửi, trỏ tới AspNetUsers.Id
        RequestedAt DATETIME2 NOT NULL,  -- thời điểm gửi
        DecidedByUserId NVARCHAR(450),  -- Chủ trọ quyết, trỏ tới AspNetUsers.Id
        DecidedAt DATETIME2,  -- thời điểm quyết
        DecisionReason NVARCHAR(500),  -- lý do từ chối (bắt buộc khi Status = 2)
        RowVersion ROWVERSION NOT NULL,  -- chống ghi đè đồng thời
        CONSTRAINT FK_ManagerApprovalRequests_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties(Id),  -- Restrict
        CONSTRAINT FK_ManagerApprovalRequests_AspNetUsers_RequestedByUserId FOREIGN KEY (RequestedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT FK_ManagerApprovalRequests_AspNetUsers_DecidedByUserId FOREIGN KEY (DecidedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT CK_ManagerApprovalRequests_Status CHECK (Status IN (0, 1, 2, 3))
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ManagerApprovalRequests_Pending' AND object_id = OBJECT_ID(N'dbo.ManagerApprovalRequests'))
    CREATE UNIQUE INDEX UX_ManagerApprovalRequests_Pending ON ManagerApprovalRequests(OperationCode, TargetType, TargetKey) WHERE Status = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagerApprovalRequests_PropertyId' AND object_id = OBJECT_ID(N'dbo.ManagerApprovalRequests'))
    CREATE INDEX IX_ManagerApprovalRequests_PropertyId ON ManagerApprovalRequests(PropertyId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagerApprovalRequests_DecidedByUserId' AND object_id = OBJECT_ID(N'dbo.ManagerApprovalRequests'))
    CREATE INDEX IX_ManagerApprovalRequests_DecidedByUserId ON ManagerApprovalRequests(DecidedByUserId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagerApprovalRequests_RequestedByUserId_Status' AND object_id = OBJECT_ID(N'dbo.ManagerApprovalRequests'))
    CREATE INDEX IX_ManagerApprovalRequests_RequestedByUserId_Status ON ManagerApprovalRequests(RequestedByUserId, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagerApprovalRequests_Status_RequestedAt' AND object_id = OBJECT_ID(N'dbo.ManagerApprovalRequests'))
    CREATE INDEX IX_ManagerApprovalRequests_Status_RequestedAt ON ManagerApprovalRequests(Status, RequestedAt);
GO

-- ============================================================================
-- VHT-33/VHT-127 (P7) — Sự cố bảo trì, lịch sử xử lý và ảnh đính kèm
-- Bản ghi sự cố do khách/quản lý tạo; lịch sử là append-only (không sửa/xóa).
-- Ảnh đính kèm trỏ StoredFiles (PK là FileId) và phải có ít nhất một đích:
-- sự cố đã duyệt hoặc yêu cầu chờ duyệt (CK_IncidentPhotoAttachments_Target).
-- ActorUserId của lịch sử là liên kết mềm, không đặt FK (xem ghi chú cuối hình).
-- ============================================================================
IF OBJECT_ID(N'dbo.MaintenanceIncidents', N'U') IS NULL
BEGIN
    CREATE TABLE MaintenanceIncidents
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_MaintenanceIncidents PRIMARY KEY,
        RoomId INT NOT NULL,  -- phòng gặp sự cố, trỏ tới Rooms.Id
        ReportedByUserId NVARCHAR(450) NOT NULL,  -- người báo, trỏ tới AspNetUsers.Id
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NOT NULL,
        Category INT NOT NULL,  -- 0..4: điện, nước, nội thất, vệ sinh, khác
        Urgency INT NOT NULL,  -- 0 Thấp, 1 Trung bình, 2 Cao
        Status INT NOT NULL,  -- 0 Mới, 1 Đang xử lý, 2 Đã xong
        AssigneeName NVARCHAR(100),  -- người/nhóm sửa, nhập tay
        TechnicalNotes NVARCHAR(2000),
        RepairCost DECIMAL(18, 0),  -- chi phí sửa; làm tròn đồng, không âm
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_MaintenanceIncidents_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES Rooms(Id),  -- Restrict
        CONSTRAINT FK_MaintenanceIncidents_AspNetUsers_ReportedByUserId FOREIGN KEY (ReportedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT CK_MaintenanceIncidents_Category CHECK (Category BETWEEN 0 AND 4),
        CONSTRAINT CK_MaintenanceIncidents_Urgency CHECK (Urgency BETWEEN 0 AND 2),
        CONSTRAINT CK_MaintenanceIncidents_Status CHECK (Status BETWEEN 0 AND 2),
        CONSTRAINT CK_MaintenanceIncidents_RepairCost CHECK (RepairCost IS NULL OR RepairCost >= 0)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaintenanceIncidents_RoomId' AND object_id = OBJECT_ID(N'dbo.MaintenanceIncidents'))
    CREATE INDEX IX_MaintenanceIncidents_RoomId ON MaintenanceIncidents(RoomId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaintenanceIncidents_ReportedByUserId' AND object_id = OBJECT_ID(N'dbo.MaintenanceIncidents'))
    CREATE INDEX IX_MaintenanceIncidents_ReportedByUserId ON MaintenanceIncidents(ReportedByUserId);
GO

IF OBJECT_ID(N'dbo.MaintenanceIncidentHistories', N'U') IS NULL
BEGIN
    CREATE TABLE MaintenanceIncidentHistories
    (
        Id BIGINT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_MaintenanceIncidentHistories PRIMARY KEY,
        IncidentId INT NOT NULL,  -- trỏ tới MaintenanceIncidents.Id
        Action INT NOT NULL,  -- 0..4: tạo, đổi trạng thái, gán người, ghi chú, duyệt
        FromStatus INT,  -- NULL khi là thao tác đầu tiên
        ToStatus INT NOT NULL,
        ActorUserId NVARCHAR(450) NOT NULL,  -- liên kết mềm tới AspNetUsers.Id
        Note NVARCHAR(2000),  -- VHT-33 nâng từ 1000 lên 2000 ký tự (20261102125721)
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_MaintenanceIncidentHistories_MaintenanceIncidents_IncidentId FOREIGN KEY (IncidentId) REFERENCES MaintenanceIncidents(Id),  -- Restrict
        CONSTRAINT CK_MaintenanceIncidentHistories_Action CHECK (Action BETWEEN 0 AND 4),
        CONSTRAINT CK_MaintenanceIncidentHistories_FromStatus CHECK (FromStatus IS NULL OR FromStatus BETWEEN 0 AND 2),
        CONSTRAINT CK_MaintenanceIncidentHistories_ToStatus CHECK (ToStatus BETWEEN 0 AND 2)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaintenanceIncidentHistories_IncidentId' AND object_id = OBJECT_ID(N'dbo.MaintenanceIncidentHistories'))
    CREATE INDEX IX_MaintenanceIncidentHistories_IncidentId ON MaintenanceIncidentHistories(IncidentId);
GO

IF OBJECT_ID(N'dbo.IncidentPhotoAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE IncidentPhotoAttachments
    (
        FileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_IncidentPhotoAttachments PRIMARY KEY,  -- 1 tệp chỉ thuộc 1 ảnh sự cố
        Kind INT NOT NULL,  -- 0 ảnh khách/quản lý gửi, 1 ảnh chờ duyệt kèm yêu cầu
        IncidentId INT,  -- sự cố đã tồn tại; NULL khi ảnh gắn với yêu cầu chờ duyệt
        ApprovalRequestId UNIQUEIDENTIFIER,  -- trỏ tới ManagerApprovalRequests.Id
        CONSTRAINT FK_IncidentPhotoAttachments_StoredFiles_FileId FOREIGN KEY (FileId) REFERENCES StoredFiles(Id),  -- Restrict
        CONSTRAINT FK_IncidentPhotoAttachments_MaintenanceIncidents_IncidentId FOREIGN KEY (IncidentId) REFERENCES MaintenanceIncidents(Id),  -- Restrict
        CONSTRAINT FK_IncidentPhotoAttachments_ManagerApprovalRequests_ApprovalRequestId FOREIGN KEY (ApprovalRequestId) REFERENCES ManagerApprovalRequests(Id),  -- Restrict
        CONSTRAINT CK_IncidentPhotoAttachments_Kind CHECK (Kind IN (0, 1)),
        CONSTRAINT CK_IncidentPhotoAttachments_Target CHECK (IncidentId IS NOT NULL OR ApprovalRequestId IS NOT NULL)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IncidentPhotoAttachments_IncidentId_Kind' AND object_id = OBJECT_ID(N'dbo.IncidentPhotoAttachments'))
    CREATE INDEX IX_IncidentPhotoAttachments_IncidentId_Kind ON IncidentPhotoAttachments(IncidentId, Kind);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IncidentPhotoAttachments_ApprovalRequestId' AND object_id = OBJECT_ID(N'dbo.IncidentPhotoAttachments'))
    CREATE INDEX IX_IncidentPhotoAttachments_ApprovalRequestId ON IncidentPhotoAttachments(ApprovalRequestId);
GO

-- ============================================================================
-- DELTA 3C (VHT-132) — 9 bảng thêm sau đợt 2E, đối chiếu migration trên dev@c7d7411.
-- Thứ tự tạo theo phụ thuộc FK: audit/tenant trước, payments sau Invoices,
-- utility sau Rooms. Mọi FK lịch sử/tài chính đều Restrict (giữ lịch sử).
-- ============================================================================

-- Nhật ký thao tác tổng hợp (20261105044209_AddAuditUtilityPayment)
IF OBJECT_ID(N'dbo.AuditLogEntries', N'U') IS NULL
BEGIN
    CREATE TABLE AuditLogEntries
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_AuditLogEntries PRIMARY KEY,  -- khóa chính, tự tăng
        Category INT NOT NULL,  -- nhóm thao tác (enum)
        Action NVARCHAR(200) NOT NULL,  -- mã hành động
        ActorUserId NVARCHAR(450),  -- người thực hiện, liên kết mềm tới AspNetUsers.Id
        PropertyId INT,  -- tòa liên quan, NULL nếu thao tác toàn hệ thống
        Metadata NVARCHAR(1000),  -- dữ liệu phụ dạng JSON/text
        OccurredAt DATETIMEOFFSET NOT NULL  -- thời điểm xảy ra
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogEntries_Category_OccurredAt' AND object_id = OBJECT_ID(N'dbo.AuditLogEntries'))
    CREATE INDEX IX_AuditLogEntries_Category_OccurredAt ON AuditLogEntries(Category, OccurredAt);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogEntries_PropertyId' AND object_id = OBJECT_ID(N'dbo.AuditLogEntries'))
    CREATE INDEX IX_AuditLogEntries_PropertyId ON AuditLogEntries(PropertyId);
GO

-- Lời mời tạo hồ sơ khách (20261106181757_TenantInvitations)
IF OBJECT_ID(N'dbo.TenantInvitations', N'U') IS NULL
BEGIN
    CREATE TABLE TenantInvitations
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_TenantInvitations PRIMARY KEY,  -- khóa chính, tự tăng
        Email NVARCHAR(256) NOT NULL,  -- email được mời
        FullName NVARCHAR(200) NOT NULL,  -- họ tên dự kiến
        Phone NVARCHAR(20),  -- số điện thoại, được để trống
        OwnerId NVARCHAR(450) NOT NULL,  -- chủ trọ gửi lời mời, liên kết mềm tới AspNetUsers.Id
        InvitedByUserId NVARCHAR(450) NOT NULL,  -- tài khoản bấm mời, liên kết mềm tới AspNetUsers.Id
        TokenHash NVARCHAR(64) NOT NULL,  -- băm token trong link mời (không lưu token gốc)
        ExpiresAt DATETIME2 NOT NULL,  -- hết hạn
        AcceptedAt DATETIME2,  -- thời điểm chấp nhận, NULL = chưa
        RevokedAt DATETIME2,  -- thời điểm thu hồi, NULL = chưa
        TenantId INT,  -- hồ sơ khách tạo ra sau khi chấp nhận, liên kết mềm tới Tenants.Id
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo
        RowVersion ROWVERSION NOT NULL  -- chống ghi đè đồng thời
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TenantInvitations_Email' AND object_id = OBJECT_ID(N'dbo.TenantInvitations'))
    CREATE INDEX IX_TenantInvitations_Email ON TenantInvitations(Email);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TenantInvitations_TokenHash' AND object_id = OBJECT_ID(N'dbo.TenantInvitations'))
    CREATE UNIQUE INDEX IX_TenantInvitations_TokenHash ON TenantInvitations(TokenHash);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TenantInvitations_OwnerId_AcceptedAt' AND object_id = OBJECT_ID(N'dbo.TenantInvitations'))
    CREATE INDEX IX_TenantInvitations_OwnerId_AcceptedAt ON TenantInvitations(OwnerId, AcceptedAt);
GO

-- Yêu cầu gia hạn hợp đồng (20261104033809 + 20261105184406 + 20261107152554)
IF OBJECT_ID(N'dbo.ContractRenewalRequests', N'U') IS NULL
BEGIN
    CREATE TABLE ContractRenewalRequests
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ContractRenewalRequests PRIMARY KEY,  -- khóa chính GUID
        ContractId UNIQUEIDENTIFIER NOT NULL,  -- hợp đồng xin gia hạn, trỏ tới Contracts.ContractId
        RequestedByTenantId INT NOT NULL,  -- khách gửi yêu cầu, trỏ tới Tenants.Id
        RequestedMonths INT NOT NULL,  -- số tháng xin gia hạn
        Note NVARCHAR(500),  -- ghi chú của khách
        Status INT NOT NULL,  -- 0 Chờ duyệt, 1 Đã duyệt, 2 Từ chối
        RequestedAt DATETIME2 NOT NULL,  -- thời điểm gửi
        DecidedAt DATETIME2,  -- thời điểm quyết định
        DecidedByUserId NVARCHAR(450),  -- người quyết định, trỏ tới AspNetUsers.Id
        RejectReason NVARCHAR(500),  -- lý do từ chối
        RowVersion ROWVERSION NOT NULL,  -- chống ghi đè đồng thời
        CONSTRAINT FK_ContractRenewalRequests_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts(ContractId),  -- Restrict: giữ lịch sử yêu cầu
        CONSTRAINT FK_ContractRenewalRequests_Tenants_RequestedByTenantId FOREIGN KEY (RequestedByTenantId) REFERENCES Tenants(Id),  -- Restrict
        CONSTRAINT FK_ContractRenewalRequests_AspNetUsers_DecidedByUserId FOREIGN KEY (DecidedByUserId) REFERENCES AspNetUsers(Id),  -- Restrict
        CONSTRAINT CK_ContractRenewalRequests_Months CHECK (RequestedMonths BETWEEN 1 AND 120),
        CONSTRAINT CK_ContractRenewalRequests_Status CHECK (Status IN (0, 1, 2))
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractRenewalRequests_RequestedByTenantId_Status' AND object_id = OBJECT_ID(N'dbo.ContractRenewalRequests'))
    CREATE INDEX IX_ContractRenewalRequests_RequestedByTenantId_Status ON ContractRenewalRequests(RequestedByTenantId, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractRenewalRequests_Status_RequestedAt' AND object_id = OBJECT_ID(N'dbo.ContractRenewalRequests'))
    CREATE INDEX IX_ContractRenewalRequests_Status_RequestedAt ON ContractRenewalRequests(Status, RequestedAt);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractRenewalRequests_DecidedByUserId' AND object_id = OBJECT_ID(N'dbo.ContractRenewalRequests'))
    CREATE INDEX IX_ContractRenewalRequests_DecidedByUserId ON ContractRenewalRequests(DecidedByUserId);
GO
-- 1 hợp đồng chỉ có 1 yêu cầu đang chờ duyệt
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ContractRenewalRequests_Pending' AND object_id = OBJECT_ID(N'dbo.ContractRenewalRequests'))
    CREATE UNIQUE INDEX UX_ContractRenewalRequests_Pending ON ContractRenewalRequests(ContractId) WHERE Status = 0;
GO

-- Khách thuê thuộc nhiều chủ trọ (nhiều-nhiều), thay Tenants.OwnerId đã bỏ (20261106182818)
IF OBJECT_ID(N'dbo.TenantOwnerLinks', N'U') IS NULL
BEGIN
    CREATE TABLE TenantOwnerLinks
    (
        TenantId INT NOT NULL,  -- khách thuê, trỏ tới Tenants.Id
        OwnerId NVARCHAR(450) NOT NULL,  -- chủ trọ, liên kết mềm tới AspNetUsers.Id
        CreatedAt DATETIME2 NOT NULL,  -- thời điểm tạo liên kết
        CONSTRAINT PK_TenantOwnerLinks PRIMARY KEY (TenantId, OwnerId),
        CONSTRAINT FK_TenantOwnerLinks_Tenants_TenantId FOREIGN KEY (TenantId) REFERENCES Tenants(Id)  -- Restrict: giữ liên kết lịch sử
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TenantOwnerLinks_OwnerId' AND object_id = OBJECT_ID(N'dbo.TenantOwnerLinks'))
    CREATE INDEX IX_TenantOwnerLinks_OwnerId ON TenantOwnerLinks(OwnerId);
GO

-- Phiếu thanh toán của hóa đơn (20261104122603_AddPayments + 20261107152554)
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE Payments
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_Payments PRIMARY KEY,  -- khóa chính, tự tăng
        InvoiceId INT NOT NULL,  -- hóa đơn được trả, trỏ tới Invoices.Id
        Amount DECIMAL(18,2) NOT NULL,  -- số tiền khách báo chuyển
        TransferredAt DATETIME2 NOT NULL,  -- thời điểm chuyển khoản
        TransactionCode NVARCHAR(100),  -- mã giao dịch ngân hàng
        Status INT NOT NULL,  -- 0 Chờ duyệt, 1 Đã xác nhận, 2 Từ chối
        RequestedAt DATETIME2 NOT NULL,  -- thời điểm gửi
        RequestedByUserId NVARCHAR(450) NOT NULL,  -- người gửi, liên kết mềm tới AspNetUsers.Id
        ReviewedAt DATETIME2,  -- thời điểm duyệt/từ chối
        ReviewedByUserId NVARCHAR(450),  -- người duyệt, liên kết mềm tới AspNetUsers.Id
        RejectionReason NVARCHAR(500),  -- lý do từ chối
        RowVersion ROWVERSION NOT NULL,  -- chống ghi đè đồng thời
        CONSTRAINT FK_Payments_Invoices_InvoiceId FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id),  -- Restrict: giữ lịch sử thanh toán
        CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Payments_Status CHECK (Status IN (0, 1, 2)),
        CONSTRAINT CK_Payments_RejectionReason CHECK ((Status <> 2 AND RejectionReason IS NULL) OR (Status = 2 AND RejectionReason IS NOT NULL)),
        CONSTRAINT CK_Payments_ReviewedAt CHECK ((Status = 0 AND ReviewedAt IS NULL) OR (Status <> 0 AND ReviewedAt IS NOT NULL))
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_InvoiceId_Status_RequestedAt' AND object_id = OBJECT_ID(N'dbo.Payments'))
    CREATE INDEX IX_Payments_InvoiceId_Status_RequestedAt ON Payments(InvoiceId, Status, RequestedAt);
GO
-- 1 hóa đơn chỉ có 1 phiếu đang chờ hoặc đã xác nhận
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Payments_InvoiceStatus' AND object_id = OBJECT_ID(N'dbo.Payments'))
    CREATE UNIQUE INDEX UX_Payments_InvoiceStatus ON Payments(InvoiceId, Status) WHERE Status IN (0, 1);
GO

-- Ảnh chứng từ chuyển khoản của phiếu thanh toán
IF OBJECT_ID(N'dbo.PaymentProofs', N'U') IS NULL
BEGIN
    CREATE TABLE PaymentProofs
    (
        PaymentId INT NOT NULL,  -- phiếu thanh toán, trỏ tới Payments.Id
        FileId UNIQUEIDENTIFIER NOT NULL,  -- tệp chứng từ, trỏ tới StoredFiles.Id
        CONSTRAINT PK_PaymentProofs PRIMARY KEY (PaymentId, FileId),
        CONSTRAINT FK_PaymentProofs_Payments_PaymentId FOREIGN KEY (PaymentId) REFERENCES Payments(Id),  -- Restrict
        CONSTRAINT FK_PaymentProofs_StoredFiles_FileId FOREIGN KEY (FileId) REFERENCES StoredFiles(Id)  -- Restrict
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentProofs_FileId' AND object_id = OBJECT_ID(N'dbo.PaymentProofs'))
    CREATE UNIQUE INDEX IX_PaymentProofs_FileId ON PaymentProofs(FileId);
GO

-- Nhật ký đổi trạng thái phiếu thanh toán
IF OBJECT_ID(N'dbo.PaymentAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE PaymentAuditLogs
    (
        Id BIGINT IDENTITY(1,1) CONSTRAINT PK_PaymentAuditLogs PRIMARY KEY,  -- khóa chính, tự tăng
        PaymentId INT NOT NULL,  -- phiếu thanh toán, trỏ tới Payments.Id
        InvoiceId INT NOT NULL,  -- hóa đơn liên quan (lưu kèm để tra cứu nhanh)
        Action INT NOT NULL,  -- hành động (enum)
        FromStatus INT,  -- trạng thái trước, NULL khi tạo mới
        ToStatus INT NOT NULL,  -- trạng thái sau
        Amount DECIMAL(18,2) NOT NULL,  -- số tiền tại thời điểm đổi
        ActorUserId NVARCHAR(450) NOT NULL,  -- người thao tác, liên kết mềm tới AspNetUsers.Id
        ActorRole NVARCHAR(20) NOT NULL,  -- vai trò người thao tác
        OccurredAt DATETIMEOFFSET NOT NULL,  -- thời điểm xảy ra
        Note NVARCHAR(500),  -- ghi chú
        CONSTRAINT FK_PaymentAuditLogs_Payments_PaymentId FOREIGN KEY (PaymentId) REFERENCES Payments(Id)  -- Restrict: giữ lịch sử
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentAuditLogs_InvoiceId_OccurredAt' AND object_id = OBJECT_ID(N'dbo.PaymentAuditLogs'))
    CREATE INDEX IX_PaymentAuditLogs_InvoiceId_OccurredAt ON PaymentAuditLogs(InvoiceId, OccurredAt);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentAuditLogs_PaymentId_OccurredAt' AND object_id = OBJECT_ID(N'dbo.PaymentAuditLogs'))
    CREATE INDEX IX_PaymentAuditLogs_PaymentId_OccurredAt ON PaymentAuditLogs(PaymentId, OccurredAt);
GO

-- Chỉ số điện/nước theo kỳ của phòng (20261105044209_AddAuditUtilityPayment)
IF OBJECT_ID(N'dbo.UtilityReadings', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityReadings
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityReadings PRIMARY KEY,  -- khóa chính, tự tăng
        RoomId INT NOT NULL,  -- phòng, trỏ tới Rooms.Id
        PeriodStart DATE NOT NULL,  -- đầu kỳ
        PeriodEnd DATE NOT NULL,  -- cuối kỳ
        ElectricStart DECIMAL(18,2) NOT NULL,  -- chỉ số điện đầu kỳ
        ElectricEnd DECIMAL(18,2) NOT NULL,  -- chỉ số điện cuối kỳ
        WaterStart DECIMAL(18,2) NOT NULL,  -- chỉ số nước đầu kỳ
        WaterEnd DECIMAL(18,2) NOT NULL,  -- chỉ số nước cuối kỳ
        RecordedByUserId NVARCHAR(MAX) NOT NULL,  -- người ghi chỉ số, liên kết mềm tới AspNetUsers.Id
        CreatedAt DATETIMEOFFSET NOT NULL,  -- thời điểm tạo
        UpdatedAt DATETIMEOFFSET,  -- thời điểm sửa gần nhất
        RowVersion ROWVERSION NOT NULL,  -- chống ghi đè đồng thời
        CONSTRAINT FK_UtilityReadings_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES Rooms(Id)  -- Restrict: giữ lịch sử chỉ số
    )
END
GO

-- 1 phòng chỉ có 1 bản ghi chỉ số cho 1 kỳ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityReadings_RoomId_PeriodStart' AND object_id = OBJECT_ID(N'dbo.UtilityReadings'))
    CREATE UNIQUE INDEX IX_UtilityReadings_RoomId_PeriodStart ON UtilityReadings(RoomId, PeriodStart);
GO

-- Lịch sử sửa chỉ số điện/nước (20261105044209_AddAuditUtilityPayment)
IF OBJECT_ID(N'dbo.UtilityReadingAudits', N'U') IS NULL
BEGIN
    CREATE TABLE UtilityReadingAudits
    (
        Id INT IDENTITY(1,1) CONSTRAINT PK_UtilityReadingAudits PRIMARY KEY,  -- khóa chính, tự tăng
        UtilityReadingId INT NOT NULL,  -- bản ghi chỉ số bị sửa, trỏ tới UtilityReadings.Id
        OldElectricEnd DECIMAL(18,2) NOT NULL,  -- chỉ số điện cuối kỳ trước khi sửa
        NewElectricEnd DECIMAL(18,2) NOT NULL,  -- chỉ số điện cuối kỳ sau khi sửa
        OldWaterEnd DECIMAL(18,2) NOT NULL,  -- chỉ số nước cuối kỳ trước khi sửa
        NewWaterEnd DECIMAL(18,2) NOT NULL,  -- chỉ số nước cuối kỳ sau khi sửa
        Reason NVARCHAR(500) NOT NULL,  -- lý do sửa
        ChangedByUserId NVARCHAR(450) NOT NULL,  -- người sửa, liên kết mềm tới AspNetUsers.Id
        ChangedAt DATETIMEOFFSET NOT NULL,  -- thời điểm sửa
        CONSTRAINT FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId FOREIGN KEY (UtilityReadingId) REFERENCES UtilityReadings(Id)  -- Restrict: giữ lịch sử sửa
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UtilityReadingAudits_UtilityReadingId' AND object_id = OBJECT_ID(N'dbo.UtilityReadingAudits'))
    CREATE INDEX IX_UtilityReadingAudits_UtilityReadingId ON UtilityReadingAudits(UtilityReadingId);
GO

-- Nạp 21 công tắc mặc định (chạy lại không nhân đôi). Mặc định Duyệt, riêng SW-15* mặc định Cho phép.
INSERT INTO ManagerToggles (Code, Mode, UpdatedAt, UpdatedByUserId)
SELECT v.Code, v.Mode, CAST('2026-01-01T00:00:00+00:00' AS DATETIMEOFFSET), NULL
FROM (VALUES (N'SW-01', 1), (N'SW-02', 1), (N'SW-03', 1), (N'SW-04', 1), (N'SW-05', 1),
             (N'SW-06', 1), (N'SW-07', 1), (N'SW-08', 1), (N'SW-09', 1), (N'SW-10', 1),
             (N'SW-11', 1), (N'SW-12', 1), (N'SW-13', 1), (N'SW-14', 1), (N'SW-15', 0),
             (N'SW-15a', 0), (N'SW-15b', 0), (N'SW-15c', 0), (N'SW-15d', 0), (N'SW-15e', 0),
             (N'SW-16', 1)) v (Code, Mode)
WHERE NOT EXISTS (SELECT 1 FROM ManagerToggles t WHERE t.Code = v.Code);
GO

-- ============================================================================
-- Dữ liệu mẫu để thử (chạy lại nhiều lần không bị nhân đôi)
-- Chỉ nạp thứ cần để dựng một chuỗi nghiệp vụ đầy đủ: tài khoản → tòa → phòng →
-- khách → hợp đồng → hóa đơn, cộng dữ liệu tham chiếu bắt buộc (21 công tắc,
-- biểu giá điện/nước, danh mục dịch vụ).
-- KHÔNG nạp: UtilityBillingPeriodSnapshots / UtilityBillingCostLineSnapshots (chỉ sinh
-- khi chốt kỳ trên app), ManagerApprovalRequests (chỉ sinh khi Quản lý gửi thao tác),
-- 3 bảng P7 MaintenanceIncidents / MaintenanceIncidentHistories / IncidentPhotoAttachments
-- (chỉ sinh khi báo sự cố trên app) và 9 bảng delta 3C (ContractRenewalRequests, Payments,
-- PaymentProofs, PaymentAuditLogs, AuditLogEntries, UtilityReadings, UtilityReadingAudits,
-- TenantInvitations, TenantOwnerLinks) — chỉ sinh khi chạy nghiệp vụ trên app.
-- Tài khoản mẫu không có mật khẩu nên không đăng nhập được.
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = N'mau-chu-tro')
BEGIN
    -- 3 tài khoản: 1 chủ trọ, 1 quản lý, 1 khách (đều đã duyệt theo VHT-95)
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, FullName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, ApprovedAt)
    VALUES (N'mau-chu-tro', N'chutro@example.com', N'CHUTRO@EXAMPLE.COM', N'chutro@example.com', N'CHUTRO@EXAMPLE.COM', N'Chủ trọ mẫu', 1, 0, 0, 1, 0, SYSUTCDATETIME()),
           (N'mau-quan-ly', N'quanly@example.com', N'QUANLY@EXAMPLE.COM', N'quanly@example.com', N'QUANLY@EXAMPLE.COM', N'Quản lý mẫu', 1, 0, 0, 1, 0, SYSUTCDATETIME()),
           (N'mau-khach-01', N'khach01@example.com', N'KHACH01@EXAMPLE.COM', N'khach01@example.com', N'KHACH01@EXAMPLE.COM', N'Nguyễn Văn A', 1, 0, 0, 1, 0, SYSUTCDATETIME());

    -- 2 nhà trọ của chủ trọ (Address được để trống)
    INSERT INTO Properties (Name, Address, OwnerId, IsActive, CreatedAt, IsDeleted)
    VALUES (N'Trọ An Bình', N'12 Nguyễn Trãi, Phường Bến Thành, Thành phố Hồ Chí Minh', N'mau-chu-tro', 1, SYSUTCDATETIME(), 0),
           (N'Trọ Hoa Mai', NULL, N'mau-chu-tro', 1, SYSUTCDATETIME(), 0);

    -- 4 phòng (Status: 0 Trống, 1 Đang thuê, 2 Bảo trì; P101 dùng lại được ở nhà khác; Floor NULL = chưa xác định)
    INSERT INTO Rooms (PropertyId, Code, Floor, AreaM2, StandardPrice, Status, IsActive, CreatedAt, IsDeleted)
    SELECT p.Id, v.Code, v.Floor, v.AreaM2, v.Price, v.Status, 1, SYSUTCDATETIME(), 0
    FROM Properties p
    JOIN (VALUES (N'Trọ An Bình', N'P101', 1, 20.5, 2500000, 1),
                 (N'Trọ An Bình', N'P102', 1, 18, 2200000, 0),
                 (N'Trọ An Bình', N'P201', 2, 22, 2700000, 2),
                 (N'Trọ Hoa Mai', N'P101', NULL, 25, 3000000, 0)) v (PropertyName, Code, Floor, AreaM2, Price, Status)
        ON p.Name = v.PropertyName;

    -- 3 khách: có tài khoản; chưa có tài khoản và chưa có CCCD; chưa có tài khoản nhưng có CCCD
    INSERT INTO Tenants (FullName, Phone, Email, CitizenId, UserId, CreatedAt, IsDeleted)
    VALUES (N'Nguyễn Văn A', N'0901234567', N'a@example.com', N'079200000001', N'mau-khach-01', SYSUTCDATETIME(), 0),
           (N'Bà Nam', N'0907654321', NULL, NULL, NULL, SYSUTCDATETIME(), 0),
           (N'Trần Thị B', N'0912345678', NULL, N'079200000002', NULL, SYSUTCDATETIME(), 0);

    -- Quản lý mẫu được giao tòa Trọ An Bình (còn hiệu lực)
    INSERT INTO PropertyManagers (PropertyId, ManagerUserId, AssignedAt, AssignedByUserId)
    SELECT p.Id, N'mau-quan-ly', SYSUTCDATETIME(), N'mau-chu-tro'
    FROM Properties p WHERE p.Name = N'Trọ An Bình';

    -- Danh mục dịch vụ dùng chung + giá đầu tiên (P5)
    DECLARE @svcElectric INT, @svcWater INT;
    INSERT INTO ServiceCatalogs (PropertyId, IsReadOnly, CreatedAt) VALUES (NULL, 0, SYSUTCDATETIME());
    SET @svcElectric = SCOPE_IDENTITY();
    INSERT INTO ServiceCatalogs (PropertyId, IsReadOnly, CreatedAt) VALUES (NULL, 0, SYSUTCDATETIME());
    SET @svcWater = SCOPE_IDENTITY();
    INSERT INTO ServicePriceHistory (ServiceCatalogId, Name, Unit, CalculationMethod, UnitPrice, EffectiveDate, CreatedAt, CreatedByUserId)
    VALUES (@svcElectric, N'Điện', N'kWh', N'Theo chỉ số đồng hồ', 3500, '2026-01-01', SYSUTCDATETIME(), N'mau-chu-tro'),
           (@svcWater, N'Nước', N'm³', N'Theo chỉ số đồng hồ', 15000, '2026-01-01', SYSUTCDATETIME(), N'mau-chu-tro');

    -- 1 hợp đồng đã duyệt cho phòng P101 + khách đại diện (P4)
    DECLARE @contractA UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
    INSERT INTO Contracts (ContractId, ContractNumber, RoomId, StartDate, EndDate, SnapshotPrice, DepositAmount, Status)
    SELECT @contractA, N'HD-MAU-0001', r.Id, '2026-01-01', '2026-12-31', 2500000, 5000000, 1
    FROM Rooms r
    JOIN Properties p ON p.Id = r.PropertyId
    WHERE p.Name = N'Trọ An Bình' AND r.Code = N'P101';
    INSERT INTO ContractMembers (ContractId, TenantId)
    SELECT @contractA, t.Id FROM Tenants t WHERE t.CitizenId = N'079200000001';

    -- 1 hóa đơn đã phát hành cho kỳ 10/2026 + 3 dòng (tiền phòng, điện, giảm giá)
    INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt, IssuedAt)
    VALUES (@contractA, '2026-10-01', '2026-10-31', 1, 2750000, SYSUTCDATETIME(), SYSUTCDATETIME());
    DECLARE @invoiceA INT = (SELECT Id FROM Invoices WHERE ContractId = @contractA AND PeriodStart = '2026-10-01');
    INSERT INTO InvoiceItems (InvoiceId, ItemType, Description, Unit, Quantity, UnitPrice, Amount, DisplayOrder)
    VALUES (@invoiceA, 0, N'Tiền phòng tháng 10/2026', N'tháng', 1, 2500000, 2500000, 1),
           (@invoiceA, 1, N'Điện 150 kWh', N'kWh', 150, 3500, 525000, 2),
           (@invoiceA, 4, N'Giảm giá khách quen', NULL, 1, 275000, -275000, 3);
END
GO

-- ============================================================================
-- Biểu giá điện/nước chuẩn (VHT-100) — dữ liệu tham chiếu, chỉ đọc.
-- Giá trị dưới đây copy đúng từ migration 20261030190913_AddLockedUtilityTariffs và
-- 20261031012852_AddUtilityPeriodTariffSnapshots (đã cập nhật lại phạm vi giá nước
-- phiên bản 2026-01-01 thành HCMC-BENTHANH). Không sửa dòng đã có: đổi giá = thêm dòng mới.
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM UtilityTariffs)
BEGIN
    SET IDENTITY_INSERT UtilityTariffs ON;
    INSERT INTO UtilityTariffs (Id, Type, ScopeCode, Unit, IsPerResident, EffectiveDate, IsReadOnly, SourceName, SourceReference)
    VALUES
        (1, 0, N'VN-EVN-HOUSEHOLD', N'kWh', 0, '2025-05-10', 1, N'EVN / Bộ Công Thương',
         N'https://evn.com.vn/userfile/User/tcdl/files/2025/5/BanTinEVNSo182025-20250513160725352.pdf (Quyết định 1279/QĐ-BCT; giá bán lẻ điện sinh hoạt, chưa gồm VAT)'),
        (2, 1, N'HCMC-BENTHANH', N'm³', 1, '2026-01-01', 1, N'Công ty Cổ phần Cấp nước Bến Thành; Thông báo 3698/TB-CNBT-KDDVKH',
         N'https://cdn.thuvienphapluat.vn//phap-luat/2022-2/NVK/2026/T4-2026/280426/thong-bao-3698-tb-cnbt-kddvkh-nam-2025.pdf (giá nước sạch sinh hoạt, chưa gồm VAT và phí thoát nước)'),
        (3, 0, N'VN-EVN-HOUSEHOLD', N'kWh', 0, '2023-11-09', 1, N'EVN / Bộ Công Thương',
         N'https://www.evn.com.vn/userfile/User/tcdl/files/2023-11-08%20QD%202941-QD-BCT%20quy%20dinh%20ve%20gia%20ban%20dien.PDF (Quyết định 2941/QĐ-BCT)'),
        (4, 0, N'VN-EVN-HOUSEHOLD', N'kWh', 0, '2024-10-11', 1, N'EVN / Bộ Công Thương',
         N'https://www.evn.com.vn/userfile/User/tcdl/files/2024_10_11_QD2699_QD_BCT_quydinhvegiadien%281%29.pdf (Quyết định 2699/QĐ-BCT)'),
        (5, 1, N'HCMC-BENTHANH', N'm³', 1, '2025-01-01', 1, N'Công ty Cổ phần Cấp nước Bến Thành; Thông báo 3698/TB-CNBT-KDDVKH',
         N'https://cdn.thuvienphapluat.vn//phap-luat/2022-2/NVK/2026/T4-2026/280426/thong-bao-3698-tb-cnbt-kddvkh-nam-2025.pdf (giá nước sạch sinh hoạt, chưa gồm VAT và phí thoát nước)');
    SET IDENTITY_INSERT UtilityTariffs OFF;

    SET IDENTITY_INSERT UtilityTariffTiers ON;
    INSERT INTO UtilityTariffTiers (Id, UtilityTariffId, TierNumber, [From], [To], UnitPrice)
    VALUES
        (1, 1, 1, 0, 50, 1984), (2, 1, 2, 50, 100, 2050), (3, 1, 3, 100, 200, 2380),
        (4, 1, 4, 200, 300, 2998), (5, 1, 5, 300, 400, 3350), (6, 1, 6, 400, NULL, 3460),
        (7, 2, 1, 0, 4, 6700), (8, 2, 2, 4, 6, 12900), (9, 2, 3, 6, NULL, 14400),
        (10, 3, 1, 0, 50, 1806), (11, 3, 2, 50, 100, 1866), (12, 3, 3, 100, 200, 2167),
        (13, 3, 4, 200, 300, 2729), (14, 3, 5, 300, 400, 3050), (15, 3, 6, 400, NULL, 3151),
        (16, 4, 1, 0, 50, 1893), (17, 4, 2, 50, 100, 1956), (18, 4, 3, 100, 200, 2271),
        (19, 4, 4, 200, 300, 2860), (20, 4, 5, 300, 400, 3197), (21, 4, 6, 400, NULL, 3302),
        (22, 5, 1, 0, 4, 6700), (23, 5, 2, 4, 6, 12900), (24, 5, 3, 6, NULL, 14400);
    SET IDENTITY_INSERT UtilityTariffTiers OFF;
END
GO
