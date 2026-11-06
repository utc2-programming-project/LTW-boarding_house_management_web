-- Chuẩn hóa 1 nhà trọ + 1 phòng còn lại sau khi dọn dữ liệu smoke.
-- Chạy 1 lần; chạy lại vẫn cho cùng kết quả (idempotent).
-- sqlcmd -S "(localdb)\mssqllocaldb" -d NhaTroDb -i scripts/database/chuan_hoa_du_lieu_chinh.sql -f 65001

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRAN;

UPDATE Properties
SET Name = N'Nhà trọ Hòa Bình',
    Address = N'12 Nguyễn Trãi, Phường Bến Thành, Quận 1, TP.HCM'
WHERE Id = 1;

UPDATE Rooms
SET Code = N'P101',
    Floor = 1,
    AreaM2 = 25.00,
    StandardPrice = 3500000.00,
    Status = 0 -- Available
WHERE Id = 1;

COMMIT;

SELECT Id, Name, Address, OwnerId FROM Properties;
SELECT Id, PropertyId, Code, Floor, AreaM2, StandardPrice, Status FROM Rooms;
