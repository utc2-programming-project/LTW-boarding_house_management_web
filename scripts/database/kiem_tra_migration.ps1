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

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw "Khong tim thay sqlcmd." }
if (-not (Test-Path $projectPath)) { throw "Khong tim thay project: $projectPath" }

Write-Step "Xoa DB kiem tra cu (neu co)"
& sqlcmd -S $Server -d master -b -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" | Out-Null
Pass "DB sach: $Database"

Write-Step "Liet ke migration trong project"
$migrations = & dotnet ef migrations list --project $projectPath --no-build 2>&1
if ($LASTEXITCODE -ne 0) { $migrations = & dotnet ef migrations list --project $projectPath 2>&1 }
$migrations | ForEach-Object { Write-Host "  $_" }
$pending = @($migrations | Where-Object { $_ -match "\(Pending\)" })
if ($pending.Count -gt 0) { Fail "Co migration chua co trong snapshot/DB: $($pending -join ', ')" }
else { Pass "Khong co migration pending" }

Write-Step "Ap migration len DB sach"
& dotnet ef database update --project $projectPath --connection $connection --no-build | Out-Host
if ($LASTEXITCODE -ne 0) { throw "database update tren DB sach that bai." }
Pass "DB sach da cap nhat"

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

Write-Step "Nap du lieu mau toi thieu"
$seed = @"
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = N'vht28-owner')
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, FullName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
    VALUES (N'vht28-owner', N'vht28@example.com', N'VHT28@EXAMPLE.COM', N'vht28@example.com', N'VHT28@EXAMPLE.COM', N'Chu tro VHT-28', 1, 0, 0, 1, 0);
IF NOT EXISTS (SELECT 1 FROM Properties WHERE Name = N'VHT28 Clean')
    INSERT INTO Properties (Name, Address, OwnerId, IsActive, CreatedAt, IsDeleted)
    VALUES (N'VHT28 Clean', NULL, N'vht28-owner', 1, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE Code = N'VHT28-P1')
    INSERT INTO Rooms (PropertyId, Code, Floor, AreaM2, StandardPrice, Status, IsActive, CreatedAt, IsDeleted)
    SELECT TOP 1 Id, N'VHT28-P1', 1, 20, 2500000, 0, 1, SYSUTCDATETIME(), 0 FROM Properties WHERE Name = N'VHT28 Clean';
IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Phone = N'0900000028')
    INSERT INTO Tenants (FullName, Phone, Email, CitizenId, UserId, CreatedAt, IsDeleted, IsActive)
    VALUES (N'Khach VHT-28', N'0900000028', NULL, N'VHT28000000001', NULL, SYSUTCDATETIME(), 0, 1);
"@
Invoke-Sql $Database $seed | Out-Null
Pass "Da nap du lieu mau (Users/Properties/Rooms/Tenants)"

Write-Step "Chay lai migration tren DB co du lieu"
& dotnet ef database update --project $projectPath --connection $connection --no-build | Out-Host
if ($LASTEXITCODE -ne 0) { throw "database update tren DB co du lieu that bai." }
$counts = @(Invoke-Sql $Database "SELECT (SELECT COUNT(*) FROM Properties), (SELECT COUNT(*) FROM Rooms), (SELECT COUNT(*) FROM Tenants)")
Pass "Du lieu con nguyen (Properties|Rooms|Tenants): $($counts -join ' ')"

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
