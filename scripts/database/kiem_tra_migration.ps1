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

Write-Step "Kiem tra rang buoc hoa don bang du lieu that"
Invoke-Sql $Database "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2026-10-01', '2026-10-31', 1, 0, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';" | Out-Null
Pass "Them duoc hoa don hop le tren DB cu"
Expect-SqlFail $Database "Unique chan trung (ContractId, PeriodStart)" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2026-10-01', '2026-10-31', 1, 0, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';"
Expect-SqlFail $Database "FK chan hop dong khong ton tai" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) VALUES (NEWID(), '2026-11-01', '2026-11-30', 1, 0, SYSUTCDATETIME());"
Expect-SqlFail $Database "Check chan ngay ket thuc truoc ngay bat dau" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2026-12-31', '2026-12-01', 1, 0, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';"
Expect-SqlFail $Database "Check chan tong tien am" "INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt) SELECT ContractId, '2027-01-01', '2027-01-31', 1, -1, SYSUTCDATETIME() FROM Contracts WHERE ContractNumber = N'VHT121-OLD';"
Expect-SqlFail $Database "Check chan dong thuong co tien am" "INSERT INTO InvoiceItems (InvoiceId, ItemType, Description, Quantity, UnitPrice, Amount, DisplayOrder) SELECT TOP 1 Id, 0, N'Thu', 1, 0, -1, 1 FROM Invoices;"

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
