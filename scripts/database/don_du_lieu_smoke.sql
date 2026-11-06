-- Dọn dữ liệu smoke/UI test còn sót trong NhaTroDb.
-- Giữ lại: 4 tài khoản dev, nhà trọ "Ho Chi Minh", phòng/phòng khách chính,
-- bảng giá điện nước và 21 công tắc Quản lý.
-- Chạy: sqlcmd -S "(localdb)\mssqllocaldb" -d NhaTroDb -i scripts/database/don_du_lieu_smoke.sql
-- Script idempotent: chạy lại khi không còn dữ liệu smoke sẽ không làm gì.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRAN;

-- 0) Gom id dữ liệu smoke do script ui-smoke tạo ra.
SELECT Id AS PropertyId INTO #SmokeProps
FROM Properties
WHERE Name LIKE 'UI Smoke VHT98%';

SELECT Id INTO #SmokeRooms
FROM Rooms
WHERE PropertyId IN (SELECT PropertyId FROM #SmokeProps)
   OR Code LIKE 'UI98%';

SELECT ContractId INTO #SmokeContracts
FROM Contracts
WHERE RoomId IN (SELECT Id FROM #SmokeRooms);

SELECT Id INTO #SmokeInvoices
FROM Invoices
WHERE ContractId IN (SELECT ContractId FROM #SmokeContracts);

SELECT Id INTO #SmokeTenants
FROM Tenants
WHERE FullName LIKE N'%VHT98%';

SELECT Id INTO #SmokeUsers
FROM AspNetUsers
WHERE Email LIKE '%smoke%@test.local'
   OR Email IN ('likavn1717@gmail.com', 'likavn1919@gmail.com');

-- 1) Thanh toán / hóa đơn.
DELETE FROM Payments WHERE InvoiceId IN (SELECT Id FROM #SmokeInvoices);
DELETE FROM InvoiceItems WHERE InvoiceId IN (SELECT Id FROM #SmokeInvoices);
DELETE FROM Invoices WHERE Id IN (SELECT Id FROM #SmokeInvoices);

-- 2) Hợp đồng + thành viên + yêu cầu gia hạn.
DELETE FROM ContractRenewalRequests WHERE ContractId IN (SELECT ContractId FROM #SmokeContracts);
DELETE FROM ContractMembers WHERE ContractId IN (SELECT ContractId FROM #SmokeContracts);
DELETE FROM Contracts WHERE ContractId IN (SELECT ContractId FROM #SmokeContracts);

-- 3) Chỉ số điện nước.
DELETE FROM UtilityReadingAudits
WHERE UtilityReadingId IN (SELECT Id FROM UtilityReadings WHERE RoomId IN (SELECT Id FROM #SmokeRooms));
DELETE FROM UtilityReadings WHERE RoomId IN (SELECT Id FROM #SmokeRooms);

-- 4) Sự cố bảo trì.
DELETE FROM IncidentPhotoAttachments
WHERE IncidentId IN (SELECT Id FROM MaintenanceIncidents WHERE RoomId IN (SELECT Id FROM #SmokeRooms));
DELETE FROM MaintenanceIncidentHistories
WHERE IncidentId IN (SELECT Id FROM MaintenanceIncidents WHERE RoomId IN (SELECT Id FROM #SmokeRooms));
DELETE FROM MaintenanceIncidents WHERE RoomId IN (SELECT Id FROM #SmokeRooms);

-- 5) Dịch vụ / phân công Quản lý / yêu cầu duyệt theo tòa nhà smoke.
DELETE FROM ServicePriceHistory
WHERE ServiceCatalogId IN (SELECT Id FROM ServiceCatalogs WHERE PropertyId IN (SELECT PropertyId FROM #SmokeProps));
DELETE FROM ServiceCatalogs WHERE PropertyId IN (SELECT PropertyId FROM #SmokeProps);
DELETE FROM ManagerApprovalRequests WHERE PropertyId IN (SELECT PropertyId FROM #SmokeProps);
DELETE FROM PropertyManagers WHERE PropertyId IN (SELECT PropertyId FROM #SmokeProps);

-- 6) Phòng + nhà trọ smoke.
DELETE FROM Rooms WHERE Id IN (SELECT Id FROM #SmokeRooms);
DELETE FROM Properties WHERE Id IN (SELECT PropertyId FROM #SmokeProps);

-- 7) Khách thuê smoke.
DELETE FROM ContractMembers WHERE TenantId IN (SELECT Id FROM #SmokeTenants);
DELETE FROM ContractRenewalRequests WHERE RequestedByTenantId IN (SELECT Id FROM #SmokeTenants);
DELETE FROM Tenants WHERE Id IN (SELECT Id FROM #SmokeTenants);

-- 8) Nối hồ sơ khách chính sang tài khoản khách chuẩn nếu đang trỏ tài khoản smoke.
UPDATE Tenants
SET UserId = 'd4eec07d-80bc-4af9-acc4-ae81ab60b54f'
WHERE Id = 1
  AND UserId = (SELECT Id FROM AspNetUsers WHERE Email = 'khach.smoke@test.local');

-- 9) Tài khoản smoke + tài khoản không còn vai trò.
DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM #SmokeUsers);
DELETE FROM AspNetUserClaims WHERE UserId IN (SELECT Id FROM #SmokeUsers);
DELETE FROM AspNetUserLogins WHERE UserId IN (SELECT Id FROM #SmokeUsers);
DELETE FROM AspNetUserTokens WHERE UserId IN (SELECT Id FROM #SmokeUsers);
DELETE FROM AspNetUserPasskeys WHERE UserId IN (SELECT Id FROM #SmokeUsers);
DELETE FROM AspNetUsers WHERE Id IN (SELECT Id FROM #SmokeUsers);

COMMIT;

-- Tổng kết sau khi dọn.
SELECT 'Properties' AS Bang, COUNT(*) AS SoDong FROM Properties
UNION ALL SELECT 'Rooms', COUNT(*) FROM Rooms
UNION ALL SELECT 'Tenants', COUNT(*) FROM Tenants
UNION ALL SELECT 'Contracts', COUNT(*) FROM Contracts
UNION ALL SELECT 'AspNetUsers', COUNT(*) FROM AspNetUsers
UNION ALL SELECT 'PropertyManagers', COUNT(*) FROM PropertyManagers;
