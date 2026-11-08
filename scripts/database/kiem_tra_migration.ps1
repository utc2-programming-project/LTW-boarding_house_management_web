# VHT-28 - Kiem tra migration tren DB sach va DB co du lieu.
#
# Muc dich: mot lenh duy nhat chay dung quy trinh review migration P4/P5/P7:
#   1. Tao DB sach, ap toan bo migration.
#   2. Kiem tra PK/FK/unique/check constraint bat buoc.
#   3. Nap du lieu mau toi thieu, chay lai migration de chung minh khong loi tren DB co du lieu.
#   4. Xoa DB kiem tra (tru khi -KeepDatabase).
#
# Cach dung:
#   powershell -File scripts/database/kiem_tra_migration.ps1
#   powershell -File scripts/database/kiem_tra_migration.ps1 -Database NhaTro_VHT28_contracts -KeepDatabase
#
# Luu y: script chi dung DB rieng, KHONG dung DB dung chung. Khong sua migration.

[CmdletBinding()]
param(
    [string]$Server = "(localdb)\mssqllocaldb",
    [string]$Database = "NhaTro_VHT28_check",
    [string]$Project = "NhaTro/Web",
    [switch]$KeepDatabase
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectPath = Join-Path $repoRoot $Project
$connection = "Server=$Server;Database=$Database;Trusted_Connection=True;MultipleActiveResultSets=true"
$failed = $false

function Write-Step([string]$text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Fail([string]$text) { Write-Host "  [FAIL] $text" -ForegroundColor Red; $script:failed = $true }
function Pass([string]$text) { Write-Host "  [ OK ] $text" -ForegroundColor Green }

function Invoke-Sql([string]$database, [string]$query) {
    $args = @("-S", $Server, "-d", $database, "-b", "-h-1", "-W", "-s", "|", "-Q", "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; $query")
    $output = & sqlcmd @args
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd loi tren DB '$database': $output" }
    return $output
}

function Get-DataCounts([string]$database) {
    return (Invoke-Sql $database "SELECT (SELECT COUNT(*) FROM AspNetUsers), (SELECT COUNT(*) FROM Properties), (SELECT COUNT(*) FROM Rooms), (SELECT COUNT(*) FROM Tenants), (SELECT COUNT(*) FROM Contracts)")
}

function Expect-SqlFail([string]$database, [string]$label, [string]$query) {
    $sqlArgs = @("-S", $Server, "-d", $database, "-b", "-h-1", "-W", "-Q", "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; $query")
    $null = & sqlcmd @sqlArgs 2>&1
    if ($LASTEXITCODE -eq 0) { Fail "$label (KHONG bi chan)" } else { Pass "$label (bi chan)" }
}

function Assert-InvoiceSchema([string]$database) {
    $tables = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Invoices', 'InvoiceItems')")
    if ($tables -eq 2) { Pass "Co bang Invoices va InvoiceItems" } else { Fail "Thieu bang hoa don ($tables/2)" }

    $pk = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.tables t WHERE t.name IN ('Invoices', 'InvoiceItems') AND EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = t.object_id AND i.is_primary_key = 1)")
    if ($pk -eq 2) { Pass "Hai bang hoa don co PK" } else { Fail "Bang hoa don thieu PK ($pk/2)" }

    $fk = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN ('FK_Invoices_Contracts_ContractId', 'FK_InvoiceItems_Invoices_InvoiceId') AND delete_referential_action_desc = 'NO_ACTION'")
    if ($fk -eq 2) { Pass "FK hoa don dung NO_ACTION" } else { Fail "FK hoa don sai ($fk/2)" }

    $unique = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_Invoices_ContractId_PeriodStart' AND is_unique = 1 AND has_filter = 0")
    if ($unique -eq 1) { Pass "Unique (ContractId, PeriodStart)" } else { Fail "Thieu unique (ContractId, PeriodStart)" }

    $checks = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID('Invoices'), OBJECT_ID('InvoiceItems'))")
    if ($checks -eq 6) { Pass "Du 6 check constraint hoa don" } else { Fail "Check hoa don thieu ($checks/6)" }

    $dates = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('Invoices') AND c.name IN ('PeriodStart', 'PeriodEnd') AND t.name = 'date'")
    if ($dates -eq 2) { Pass "Ky hoa don kieu date" } else { Fail "Ky hoa don sai kieu ($dates/2)" }

    $money = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.columns WHERE object_id IN (OBJECT_ID('Invoices'), OBJECT_ID('InvoiceItems')) AND name IN ('TotalAmount', 'Quantity', 'UnitPrice', 'Amount') AND precision = 18 AND scale = 2")
    if ($money -eq 4) { Pass "Tien/so luong decimal(18,2)" } else { Fail "Tien/so luong sai precision ($money/4)" }
}

function Assert-IncidentSchema([string]$database) {
    $tables = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.tables WHERE name IN ('MaintenanceIncidents', 'MaintenanceIncidentHistories', 'IncidentPhotoAttachments')")
    if ($tables -eq 3) { Pass "Co 3 bang su co P7" } else { Fail "Thieu bang su co P7 ($tables/3)" }

    $fk = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN ('FK_MaintenanceIncidents_Rooms_RoomId', 'FK_MaintenanceIncidents_AspNetUsers_ReportedByUserId', 'FK_MaintenanceIncidentHistories_MaintenanceIncidents_IncidentId', 'FK_IncidentPhotoAttachments_StoredFiles_FileId', 'FK_IncidentPhotoAttachments_MaintenanceIncidents_IncidentId', 'FK_IncidentPhotoAttachments_ManagerApprovalRequests_ApprovalRequestId') AND delete_referential_action_desc = 'NO_ACTION'")
    if ($fk -eq 6) { Pass "FK su co P7 dung NO_ACTION" } else { Fail "FK su co P7 sai ($fk/6)" }

    $checks = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.check_constraints WHERE name IN ('CK_MaintenanceIncidents_Category', 'CK_MaintenanceIncidents_Urgency', 'CK_MaintenanceIncidents_Status', 'CK_MaintenanceIncidents_RepairCost', 'CK_MaintenanceIncidentHistories_Action', 'CK_MaintenanceIncidentHistories_FromStatus', 'CK_MaintenanceIncidentHistories_ToStatus', 'CK_IncidentPhotoAttachments_Kind', 'CK_IncidentPhotoAttachments_Target')")
    if ($checks -eq 9) { Pass "Du 9 check constraint su co P7" } else { Fail "Check su co P7 thieu ($checks/9)" }

    $indexes = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_MaintenanceIncidents_RoomId', 'IX_MaintenanceIncidents_ReportedByUserId', 'IX_MaintenanceIncidentHistories_IncidentId', 'IX_IncidentPhotoAttachments_IncidentId_Kind', 'IX_IncidentPhotoAttachments_ApprovalRequestId')")
    if ($indexes -eq 5) { Pass "Du 5 index su co P7" } else { Fail "Index su co P7 thieu ($indexes/5)" }

    $note = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MaintenanceIncidentHistories') AND c.name = 'Note' AND t.name = 'nvarchar' AND c.max_length = 4000")
    if ($note -eq 1) { Pass "Note lich su su co la nvarchar(2000)" } else { Fail "Note lich su su co sai do dai (phai 2000)" }

    $cost = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('MaintenanceIncidents') AND name = 'RepairCost' AND precision = 18 AND scale = 0")
    if ($cost -eq 1) { Pass "RepairCost decimal(18,0)" } else { Fail "RepairCost sai precision/scale" }
}

function Assert-FinancialSchema([string]$database) {
    $tables = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Payments', 'UtilityReadingAudits', 'AuditLogEntries')")
    if ($tables -eq 3) { Pass "Co 3 bang tai chinh/audit" } else { Fail "Thieu bang tai chinh/audit ($tables/3)" }

    $checks = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.check_constraints WHERE name IN ('CK_Payments_Amount', 'CK_Payments_Status', 'CK_Payments_ReviewedAt', 'CK_Payments_RejectionReason')")
    if ($checks -eq 4) { Pass "Du 4 check constraint Payments" } else { Fail "Check Payments thieu ($checks/4)" }

    $unique = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.indexes WHERE name = 'UX_Payments_InvoiceConfirmed' AND is_unique = 1 AND has_filter = 1 AND filter_definition LIKE '%Status%1%'")
    if ($unique -eq 1) { Pass "Unique confirmed theo hoa don co filter" } else { Fail "Thieu unique UX_Payments_InvoiceConfirmed" }

    $lookup = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_Payments_InvoiceId_RequestedAt'")
    if ($lookup -eq 1) { Pass "Index tra cuu Payment theo hoa don" } else { Fail "Thieu index IX_Payments_InvoiceId_RequestedAt" }

    $fk = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN ('FK_Payments_Invoices_InvoiceId', 'FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId') AND delete_referential_action_desc = 'NO_ACTION'")
    if ($fk -eq 2) { Pass "FK tai chinh dung NO_ACTION" } else { Fail "FK tai chinh sai ($fk/2)" }

    $money = [int](Invoke-Sql $database "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'Amount' AND precision = 18 AND scale = 2")
    if ($money -eq 1) { Pass "Payment.Amount decimal(18,2)" } else { Fail "Payment.Amount sai precision" }
}

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw "Khong tim thay sqlcmd." }
if (-not (Test-Path $projectPath)) { throw "Khong tim thay project: $projectPath" }

Write-Step "Xoa DB kiem tra cu (neu co)"
& sqlcmd -S $Server -d master -b -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" | Out-Null
Pass "DB sach: $Database"

Write-Step "Liet ke migration trong project"
$migrations = & dotnet ef migrations list --project $projectPath 2>&1
if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations list that bai: $migrations" }
$migrations | ForEach-Object { Write-Host "  $_" }

Write-Step "Ap migration len DB sach"
& dotnet ef database update --project $projectPath --connection $connection | Out-Host
if ($LASTEXITCODE -ne 0) { throw "database update tren DB sach that bai." }

$afterUpdate = & dotnet ef migrations list --project $projectPath --connection $connection 2>&1
$pendingAfter = @($afterUpdate | Where-Object { $_ -match "\(Pending\)" })
if ($pendingAfter.Count -gt 0) { Fail "Con migration chua ap tren DB kiem tra: $($pendingAfter -join ', ')" }
else { Pass "DB sach da cap nhat, khong con migration pending" }

Write-Step "Kiem tra cau truc (PK/FK/unique/check)"
$tables = @(Invoke-Sql $Database "SELECT t.name FROM sys.tables t WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory' ORDER BY t.name")
Write-Host "  Bang nghiep vu: $($tables -join ', ')"
if ($tables.Count -eq 0) { Fail "Khong co bang nghiep vu nao duoc tao" }

$noPk = @(Invoke-Sql $Database "SELECT t.name FROM sys.tables t WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory' AND NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = t.object_id AND i.is_primary_key = 1)")
if ($noPk.Count -gt 0) { Fail "Bang thieu PK: $($noPk -join ', ')" } else { Pass "Moi bang nghiep vu co PK" }

$fks = @(Invoke-Sql $Database "SELECT fk.name, OBJECT_NAME(fk.referenced_object_id), fk.delete_referential_action_desc FROM sys.foreign_keys fk WHERE OBJECTPROPERTY(fk.parent_object_id, 'IsMSShipped') = 0 ORDER BY fk.name")
Write-Host "  FK (ten | bang cha | xoa):"
$fks | ForEach-Object { Write-Host "    $_" }
$cascade = @($fks | Where-Object { ($_ -split '\|')[-1] -eq "CASCADE" -and ($_ -split '\|')[0] -notmatch "^FK_AspNet" })
if ($cascade.Count -gt 0) { Fail "FK nghiep vu dung CASCADE (phai la NO_ACTION/Restrict): $($cascade -join '; ')" }
else { Pass "FK nghiep vu khong dung CASCADE" }

$unique = @(Invoke-Sql $Database "SELECT i.name, OBJECT_NAME(i.object_id), ISNULL(i.filter_definition, '(khong loc)') FROM sys.indexes i WHERE i.is_unique = 1 AND i.is_primary_key = 0 AND OBJECTPROPERTY(i.object_id, 'IsMSShipped') = 0 ORDER BY i.name")
Write-Host "  Unique index (ten | bang | filter):"
$unique | ForEach-Object { Write-Host "    $_" }

$checks = @(Invoke-Sql $Database "SELECT OBJECT_NAME(parent_object_id), name, definition FROM sys.check_constraints WHERE is_ms_shipped = 0 ORDER BY name")
if ($checks.Count -gt 0) { Write-Host "  Check constraint:"; $checks | ForEach-Object { Write-Host "    $_" } }
else { Write-Host "  Check constraint: (khong co)" }

Assert-InvoiceSchema $Database
Assert-IncidentSchema $Database
Assert-FinancialSchema $Database

Write-Step "Kiem tra duong nang cap: DB cu + migration hoa don"
& sqlcmd -S $Server -d master -b -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" | Out-Null

$migrationNames = @(& dotnet ef migrations list --project $projectPath 2>&1 | Where-Object { $_ -match '^\d{14}_' } | ForEach-Object { ($_ -split ' ')[0] })
$invoiceMigration = @($migrationNames | Where-Object { $_ -match '_AddInvoices$' })[0]
if (-not $invoiceMigration) { throw "Khong tim thay migration AddInvoices." }
$invoiceIndex = [Array]::IndexOf($migrationNames, $invoiceMigration)
if ($invoiceIndex -lt 1) { throw "Khong xac dinh duoc migration truoc AddInvoices." }
$previousMigration = $migrationNames[$invoiceIndex - 1]
Write-Host "  Ap migration truoc hoa don: $previousMigration"
& dotnet ef database update $previousMigration --project $projectPath --connection $connection | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Khong ap duoc migration nen $previousMigration." }

Write-Step "Nap du lieu cu truoc khi them bang hoa don"
$seed = @"
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = N'vht28-owner')
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, FullName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, ApprovedAt)
    VALUES (N'vht28-owner', N'vht28@example.com', N'VHT28@EXAMPLE.COM', N'vht28@example.com', N'VHT28@EXAMPLE.COM', N'Chu tro VHT-28', 1, 0, 0, 1, 0, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM Properties WHERE Name = N'VHT28 Clean')
    INSERT INTO Properties (Name, Address, OwnerId, IsActive, CreatedAt, IsDeleted)
    VALUES (N'VHT28 Clean', NULL, N'vht28-owner', 1, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE Code = N'VHT28-P1')
    INSERT INTO Rooms (PropertyId, Code, Floor, AreaM2, StandardPrice, Status, IsActive, CreatedAt, IsDeleted)
    SELECT TOP 1 Id, N'VHT28-P1', 1, 20, 2500000, 0, 1, SYSUTCDATETIME(), 0 FROM Properties WHERE Name = N'VHT28 Clean';
IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Phone = N'0900000028')
    INSERT INTO Tenants (FullName, Phone, Email, CitizenId, UserId, CreatedAt, IsDeleted, IsActive)
    VALUES (N'Khach VHT-28', N'0900000028', NULL, N'VHT28000000001', NULL, SYSUTCDATETIME(), 0, 1);
IF NOT EXISTS (SELECT 1 FROM Contracts WHERE ContractNumber = N'VHT121-OLD')
    INSERT INTO Contracts (ContractId, ContractNumber, RoomId, StartDate, EndDate, SnapshotPrice, DepositAmount, Status)
    SELECT NEWID(), N'VHT121-OLD', Id, '2026-01-01', '2026-12-31', 2500000, 5000000, 0 FROM Rooms WHERE Code = N'VHT28-P1';
"@
Invoke-Sql $Database $seed | Out-Null
$countsBefore = Get-DataCounts $Database
Pass "Da nap du lieu cu (Users|Properties|Rooms|Tenants|Contracts): $countsBefore"

Write-Step "Ap migration hoa don len DB co du lieu"
& dotnet ef database update --project $projectPath --connection $connection | Out-Host
if ($LASTEXITCODE -ne 0) { throw "database update tren DB co du lieu that bai." }
$countsAfter = Get-DataCounts $Database
if ($countsBefore -ne $countsAfter) { Fail "Du lieu cu bi thay doi: truoc=$countsBefore sau=$countsAfter" }
else { Pass "Du lieu cu con nguyen (Users|Properties|Rooms|Tenants|Contracts): $countsAfter" }

$afterUpgrade = & dotnet ef migrations list --project $projectPath --connection $connection 2>&1
$pendingAfterUpgrade = @($afterUpgrade | Where-Object { $_ -match "\(Pending\)" })
if ($pendingAfterUpgrade.Count -gt 0) { Fail "Con migration chua ap tren DB nang cap: $($pendingAfterUpgrade -join ', ')" }
else { Pass "DB nang cap khong con migration pending" }

Assert-InvoiceSchema $Database
Assert-IncidentSchema $Database
Assert-FinancialSchema $Database

Write-Step "Kiem tra rang buoc hoa don bang du lieu that"
Invoke-Sql $Database "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2026-10-01', '2026-10-31', 1, 0, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';" | Out-Null
Pass "Them duoc hoa don hop le tren DB cu"
Expect-SqlFail $Database "Unique chan trung (ContractId, PeriodStart)" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2026-10-01', '2026-10-31', 1, 0, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';"
Expect-SqlFail $Database "FK chan hop dong khong ton tai" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) VALUES (NEWID(), '2026-11-01', '2026-11-30', 1, 0, SYSUTCDATETIME());"
Expect-SqlFail $Database "Check chan ngay ket thuc truoc ngay bat dau" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2026-12-31', '2026-12-01', 1, 0, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';"
Expect-SqlFail $Database "Check chan tong tien am" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2027-01-01', '2027-01-31', 1, -1, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';"
Expect-SqlFail $Database "Check chan dong thuong co tien am" "INSERT INTO InvoiceItems (InvoiceId, ItemType, Description, Quantity, UnitPrice, Amount, DisplayOrder) SELECT TOP 1 Id, 0, N'Thu', 1, 0, -1, 1 FROM Invoices;"

Write-Step "Kiem tra rang buoc tai chinh bang du lieu that"
Invoke-Sql $Database "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, SubmittedByUserId) SELECT TOP 1 Id, 1000, SYSDATETIMEOFFSET(), 0, N'[]', SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;" | Out-Null
Pass "Them duoc Payment Pending hop le"
Expect-SqlFail $Database "Check chan Payment.Amount khong duong" "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, SubmittedByUserId) SELECT TOP 1 Id, 0, SYSDATETIMEOFFSET(), 0, N'[]', SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;"
Expect-SqlFail $Database "Check chan Payment.Status ngoai 0..2" "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, SubmittedByUserId) SELECT TOP 1 Id, 1000, SYSDATETIMEOFFSET(), 9, N'[]', SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;"
Expect-SqlFail $Database "Check chan Pending co ReviewedAt" "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, ReviewedAt, SubmittedByUserId) SELECT TOP 1 Id, 1000, SYSDATETIMEOFFSET(), 0, N'[]', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;"
Expect-SqlFail $Database "Check chan Confirmed thieu ReviewedAt" "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, SubmittedByUserId) SELECT TOP 1 Id, 1000, SYSDATETIMEOFFSET(), 1, N'[]', SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;"
Expect-SqlFail $Database "Check chan Rejected thieu ly do" "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, ReviewedAt, SubmittedByUserId) SELECT TOP 1 Id, 1000, SYSDATETIMEOFFSET(), 2, N'[]', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;"

Invoke-Sql $Database "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, ReviewedAt, SubmittedByUserId) SELECT TOP 1 Id, 1000, SYSDATETIMEOFFSET(), 1, N'[]', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;" | Out-Null
Pass "Them duoc Payment Confirmed hop le"
Expect-SqlFail $Database "Unique chan hai Confirmed cung hoa don" "INSERT INTO Payments (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, ReviewedAt, SubmittedByUserId) SELECT TOP 1 Id, 2000, SYSDATETIMEOFFSET(), 1, N'[]', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'vht28-owner' FROM Invoices;"

Invoke-Sql $Database "INSERT INTO UtilityReadings (RoomId, PeriodStart, PeriodEnd, ElectricStart, ElectricEnd, WaterStart, WaterEnd, RecordedByUserId, CreatedAt) SELECT TOP 1 Id, '2026-01-01', '2026-01-31', 0, 10, 0, 5, N'vht28-owner', SYSDATETIMEOFFSET() FROM Rooms WHERE Code = N'VHT28-P1';" | Out-Null
Invoke-Sql $Database "INSERT INTO UtilityReadingAudits (UtilityReadingId, OldElectricEnd, NewElectricEnd, OldWaterEnd, NewWaterEnd, Reason, ChangedByUserId, ChangedAt) SELECT TOP 1 Id, 10, 12, 5, 6, N'Dieu chinh chi so', N'vht28-owner', SYSDATETIMEOFFSET() FROM UtilityReadings;" | Out-Null
Expect-SqlFail $Database "FK chan xoa chi so khi con lich su" "DELETE FROM UtilityReadings;"

Write-Step "Kiem tra rang buoc su co P7 bang du lieu that"
$p7Rows = [int](Invoke-Sql $Database "SELECT (SELECT COUNT(*) FROM MaintenanceIncidents) + (SELECT COUNT(*) FROM MaintenanceIncidentHistories) + (SELECT COUNT(*) FROM IncidentPhotoAttachments)")
if ($p7Rows -eq 0) { Pass "Bang su co P7 rong sau khi nang cap (khong lam meo du lieu cu)" } else { Fail "Bang su co P7 co du lieu la ($p7Rows dong)" }

Invoke-Sql $Database "INSERT INTO MaintenanceIncidents (RoomId, ReportedByUserId, Title, Description, Category, Urgency, Status, CreatedAt) SELECT TOP 1 Id, N'vht28-owner', N'Su co VHT-123', N'Mo ta kiem tra', 0, 0, 0, SYSUTCDATETIME() FROM Rooms WHERE Code = N'VHT28-P1';" | Out-Null
Pass "Them duoc su co hop le tren DB cu"

Invoke-Sql $Database "INSERT INTO MaintenanceIncidentHistories (IncidentId, Action, FromStatus, ToStatus, ActorUserId, Note, CreatedAt) SELECT TOP 1 Id, 0, NULL, 0, N'vht28-owner', N'Ghi chu kiem tra', SYSUTCDATETIME() FROM MaintenanceIncidents;" | Out-Null
Pass "Them duoc lich su su co"

Invoke-Sql $Database "INSERT INTO StoredFiles (Id, FileName, StoredFileName, RelativePath, ContentType, FileSizeBytes, FileCategory, UploadedByUserId, UploadedAt, RelatedEntityId, RelatedEntityType, IsDeleted) VALUES (N'11111111-1111-1111-1111-111111111111', N'vht123.jpg', N'vht123.jpg', N'incident/vht123.jpg', N'image/jpeg', 10, 4, N'vht28-owner', SYSDATETIMEOFFSET(), N'1', 4, 0);" | Out-Null
Invoke-Sql $Database "INSERT INTO IncidentPhotoAttachments (FileId, Kind, IncidentId) SELECT N'11111111-1111-1111-1111-111111111111', 0, Id FROM MaintenanceIncidents;" | Out-Null
Pass "Them duoc anh su co tro toi tep va su co"

Expect-SqlFail $Database "Check chan anh khong co dich (IncidentId va ApprovalRequestId deu NULL)" "INSERT INTO IncidentPhotoAttachments (FileId, Kind) VALUES (N'11111111-1111-1111-1111-111111111111', 0);"
Expect-SqlFail $Database "Check chan Kind ngoai 0..1" "INSERT INTO IncidentPhotoAttachments (FileId, Kind, IncidentId) SELECT N'11111111-1111-1111-1111-111111111111', 2, Id FROM MaintenanceIncidents;"
Expect-SqlFail $Database "FK chan su co khong ton tai" "INSERT INTO IncidentPhotoAttachments (FileId, Kind, IncidentId) VALUES (N'11111111-1111-1111-1111-111111111111', 1, 2147483647);"
Expect-SqlFail $Database "FK chan tep khong ton tai" "INSERT INTO IncidentPhotoAttachments (FileId, Kind, IncidentId) SELECT NEWID(), 0, Id FROM MaintenanceIncidents;"
Expect-SqlFail $Database "FK chan yeu cau duyet khong ton tai" "INSERT INTO IncidentPhotoAttachments (FileId, Kind, ApprovalRequestId) VALUES (N'11111111-1111-1111-1111-111111111111', 1, NEWID());"

Write-Step "Kiem tra migration co the xuong (script Down)"
Write-Host "  Bo qua tu dong: chi chay tren DB tam khi can kiem rollback, tranh mat du lieu ngoai y muon." -ForegroundColor Yellow

if (-not $KeepDatabase) {
    Write-Step "Don DB kiem tra"
    & sqlcmd -S $Server -d master -b -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" | Out-Null
    Pass "Da xoa $Database"
}
else { Write-Host "  Giu lai DB $Database de kiem tra tay." -ForegroundColor Yellow }

Write-Host ""
if ($failed) { Write-Host "KET QUA: CO LOI - xem muc [FAIL] o tren." -ForegroundColor Red; exit 1 }
Write-Host "KET QUA: DAT - migration chay duoc tren DB sach va DB co du lieu; PK/FK/unique da liet ke." -ForegroundColor Green
exit 0
