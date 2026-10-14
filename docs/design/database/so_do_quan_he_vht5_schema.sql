-- Tạo cơ sở dữ liệu NhaTro (chạy lại nhiều lần không lỗi)
IF DB_ID(N'NhaTro') IS NULL
    CREATE DATABASE NhaTro;
GO

USE NhaTro;
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
        EmailConfirmed BIT NOT NULL,  -- đã xác nhận email
        PhoneNumberConfirmed BIT NOT NULL,  -- đã xác nhận số điện thoại
        TwoFactorEnabled BIT NOT NULL,  -- bật xác thực 2 bước
        LockoutEnabled BIT NOT NULL,  -- cho phép khóa tài khoản
        LockoutEnd DATETIMEOFFSET,  -- thời điểm hết khóa
        AccessFailedCount INT NOT NULL  -- số lần đăng nhập sai
    )
END
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
        UserId NVARCHAR(450),  -- tài khoản đăng nhập, có thể trống, trỏ tới AspNetUsers.Id (không unique)
        CreatedAt DATETIME2 NOT NULL,  -- ngày tạo, giờ UTC, app gán
        UpdatedAt DATETIME2,  -- ngày sửa gần nhất, giờ UTC
        IsDeleted BIT NOT NULL,  -- xóa mềm: 1 = ẩn khỏi app, dữ liệu vẫn còn trong DB
        CONSTRAINT FK_Tenants_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)  -- Restrict: cấm xóa thật AspNetUsers khi còn Tenants trỏ tới
    )
END
GO

-- CCCD không trùng (bỏ qua hồ sơ trống CCCD hoặc đã xóa mềm)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_CitizenId' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE UNIQUE INDEX IX_Tenants_CitizenId ON Tenants(CitizenId) WHERE CitizenId IS NOT NULL AND IsDeleted = 0;
GO
-- tìm nhanh hồ sơ theo tài khoản, KHÔNG unique
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_UserId' AND object_id = OBJECT_ID(N'dbo.Tenants'))
    CREATE INDEX IX_Tenants_UserId ON Tenants(UserId);
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
