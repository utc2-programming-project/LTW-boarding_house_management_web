-- Tạo cơ sở dữ liệu NhaTro (chạy lại nhiều lần không lỗi)
--
-- ĐỒNG BỘ VHT-28 (đợt 2A): file này cập nhật theo migration đã merge trên `dev` tới
-- 20261020080707_AddManagerToggles. Phần P4/P5/P7 chưa có migration nên CHƯA có ở đây;
-- khi migration P4/P5/P7 merge sẽ bổ sung ở đợt 2D (xem docs/Imp/VHT-28-*.md).
-- Delta đã bổ sung so với bản VHT-5:
--   * AspNetUsers: + FullName (VHT-8).
--   * Tenants: + IsActive, RowVersion, Gender, DateOfBirth, IdIssuedDate, IdIssuedPlace,
--     PermanentAddress, Note; đổi lọc CitizenId (thêm IsActive = 1), UserId thành unique (VHT-23).
--   * StoredFiles (VHT-12/VHT-13), FileAccessAuditLogs (VHT-26), ManagerToggles + 21 công tắc (VHT-94).
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
        AccessFailedCount INT NOT NULL  -- số lần đăng nhập sai
    )
END
GO

-- VHT-8: cot FullName them sau (DB da ton tai thi bo sung, khong tao lai bang)
IF COL_LENGTH(N'dbo.AspNetUsers', N'FullName') IS NULL
    ALTER TABLE AspNetUsers ADD FullName NVARCHAR(200) NULL;
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
        CONSTRAINT FK_Properties_AspNetUsers_OwnerId FOREIGN KEY (OwnerId) REFERENCES AspNetUsers(Id)  -- Restrict: cấm xóa thật AspNetUsers khi còn Properties trỏ tới
    )
END
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
        CONSTRAINT FK_Rooms_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties(Id)  -- Restrict: cấm xóa thật Properties khi còn Rooms trỏ tới
    )
END
GO

-- mã phòng không trùng trong cùng 1 nhà (bỏ qua phòng đã xóa mềm)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Rooms_PropertyId_Code' AND object_id = OBJECT_ID(N'dbo.Rooms'))
    CREATE UNIQUE INDEX IX_Rooms_PropertyId_Code ON Rooms(PropertyId,Code) WHERE IsDeleted = 0;
GO

-- Khách thuê. 1 hồ sơ khách có 0 hoặc 1 tài khoản (chưa có thì để trống UserId). Chưa nối với Rooms, chờ mô hình Hợp đồng
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
        CONSTRAINT FK_Tenants_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)  -- Restrict: cấm xóa thật AspNetUsers khi còn Tenants trỏ tới
    )
END
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

-- Dữ liệu mẫu để thử (chạy lại nhiều lần không bị nhân đôi). Tài khoản mẫu không có mật khẩu nên không đăng nhập được.
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = N'mau-chu-tro')
BEGIN
    -- 2 tài khoản: 1 chủ trọ, 1 khách
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
    VALUES (N'mau-chu-tro', N'chutro@example.com', N'CHUTRO@EXAMPLE.COM', N'chutro@example.com', N'CHUTRO@EXAMPLE.COM', 1, 0, 0, 1, 0),
           (N'mau-khach-01', N'khach01@example.com', N'KHACH01@EXAMPLE.COM', N'khach01@example.com', N'KHACH01@EXAMPLE.COM', 1, 0, 0, 1, 0);

    -- 2 nhà trọ của chủ trọ (Address được để trống)
    INSERT INTO Properties (Name, Address, OwnerId, IsActive, CreatedAt, IsDeleted)
    VALUES (N'Trọ An Bình', N'12 Nguyễn Trãi, Quận 5, TP.HCM', N'mau-chu-tro', 1, SYSUTCDATETIME(), 0),
           (N'Trọ Hoa Mai', NULL, N'mau-chu-tro', 1, SYSUTCDATETIME(), 0);

    -- 4 phòng (Status: 0 Trống, 1 Đang thuê, 2 Bảo trì; P101 dùng lại được ở nhà khác; Tang NULL = chưa xác định)
    INSERT INTO Rooms (PropertyId, Code, Floor, AreaM2, StandardPrice, Status, IsActive, CreatedAt, IsDeleted)
    SELECT p.Id, v.Code, v.Tang, v.DienTich, v.Gia, v.TrangThai, 1, SYSUTCDATETIME(), 0
    FROM Properties p
    JOIN (VALUES (N'Trọ An Bình', N'P101', 1, 20.5, 2500000, 1),
                 (N'Trọ An Bình', N'P102', 1, 18, 2200000, 0),
                 (N'Trọ An Bình', N'P201', 2, 22, 2700000, 2),
                 (N'Trọ Hoa Mai', N'P101', NULL, 25, 3000000, 0)) v (TenNha, Code, Tang, DienTich, Gia, TrangThai)
        ON p.Name = v.TenNha;

    -- 3 khách: có tài khoản; chưa có tài khoản và chưa có CCCD; chưa có tài khoản nhưng có CCCD
    INSERT INTO Tenants (FullName, Phone, Email, CitizenId, UserId, CreatedAt, IsDeleted)
    VALUES (N'Nguyễn Văn A', N'0901234567', N'a@example.com', N'079200000001', N'mau-khach-01', SYSUTCDATETIME(), 0),
           (N'Bà Nam', N'0907654321', NULL, NULL, NULL, SYSUTCDATETIME(), 0),
           (N'Trần Thị B', N'0912345678', NULL, N'079200000002', NULL, SYSUTCDATETIME(), 0);
END
GO
