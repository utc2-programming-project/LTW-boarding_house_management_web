-- PLAN-005 (điều chỉnh): bổ sung liên kết khách ↔ chủ trọ từ hợp đồng đã có.
-- Bảng TenantOwnerLinks là nhiều-nhiều: một khách có thể thuộc nhiều chủ trọ.
-- Idempotent: chỉ thêm liên kết chưa tồn tại. Chạy sau migration "TenantOwnerLinks".

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID('dbo.TenantOwnerLinks', 'U') IS NULL
BEGIN
    PRINT N'Chưa có bảng TenantOwnerLinks - hãy chạy migration TenantOwnerLinks trước.';
    RETURN;
END

DECLARE @Truoc INT = (SELECT COUNT(*) FROM dbo.TenantOwnerLinks);

INSERT INTO dbo.TenantOwnerLinks (TenantId, OwnerId, CreatedAt)
SELECT DISTINCT cm.TenantId, p.OwnerId, SYSUTCDATETIME()
FROM dbo.ContractMembers cm
JOIN dbo.Contracts c ON c.ContractId = cm.ContractId
JOIN dbo.Rooms r ON r.Id = c.RoomId
JOIN dbo.Properties p ON p.Id = r.PropertyId
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.TenantOwnerLinks l
    WHERE l.TenantId = cm.TenantId AND l.OwnerId = p.OwnerId);

DECLARE @Sau INT = (SELECT COUNT(*) FROM dbo.TenantOwnerLinks);

SELECT @Truoc AS LienKetTruoc, @Sau AS LienKetSau,
       (SELECT COUNT(*) FROM dbo.Tenants) AS TongHoSo;
