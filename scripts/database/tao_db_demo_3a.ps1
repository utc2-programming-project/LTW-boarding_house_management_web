# VHT-49 (3A) - Tao DB demo tu DB sach, ap migration va seed du lieu demo.
#
# Cach dung:
#   powershell -File scripts/database/tao_db_demo_3a.ps1
#   powershell -File scripts/database/tao_db_demo_3a.ps1 -Database NhaTro_Demo_3A -Recreate
#   powershell -File scripts/database/tao_db_demo_3a.ps1 -Password "MatKhauKhac"
#
# An toan:
#   - Khong bao gio drop DB neu thieu -Recreate.
#   - Chi dung DB demo; khong dung DB dev/test.
#   - Mat khau mac dinh la mat khau demo, khong dung cho Production.

[CmdletBinding()]
param(
    [string]$Server = "(localdb)\MSSQLLocalDB",
    [string]$Database = "NhaTro_Demo_3A",
    [string]$Password = "Demo@123456",
    [switch]$Recreate
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webProject = Join-Path $repoRoot "NhaTro\Web\Web.csproj"
$startupProject = $webProject
$connection = "Server=$Server;Database=$Database;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;Application Name=NhaTro-Demo-3A"
$failed = 0

function Write-Step([string]$Text) {
    Write-Host "`n== $Text ==" -ForegroundColor Cyan
}

function Fail([string]$Text) {
    Write-Host "  [FAIL] $Text" -ForegroundColor Red
    $script:failed++
}

function Pass([string]$Text) {
    Write-Host "  [ OK ] $Text" -ForegroundColor Green
}

function Assert-Equal([string]$Label, [string]$Actual, [string]$Expected) {
    if ($Actual -eq $Expected) { Pass "$Label = $Actual" }
    else { Fail "$Label = '$Actual' (mong doi '$Expected')" }
}

function Invoke-Sql([string]$DatabaseName, [string]$Query) {
    $sqlArgs = @(
        "-S", $Server, "-C", "-d", $DatabaseName, "-b", "-h", "-1",
        "-W", "-f", "65001", "-Q", "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; $Query"
    )
    $output = @(& sqlcmd @sqlArgs 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd loi tren '$DatabaseName': $($output -join ' | ')"
    }
    return (@($output | Where-Object { "$_".Trim() -ne "" }) | Select-Object -First 1)
}

function Invoke-Checked([string]$Label, [string[]]$Arguments) {
    Write-Host "  > dotnet $($Arguments -join ' ')"
    & dotnet @Arguments 2>&1 | ForEach-Object { Write-Host "    $_" }
    if ($LASTEXITCODE -ne 0) {
        throw "$Label that bai (exit $LASTEXITCODE)."
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "Khong tim thay dotnet." }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw "Khong tim thay sqlcmd." }

Write-Step "Kiem tra server SQL"
$serverName = Invoke-Sql "master" "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256));"
if (-not $serverName) { throw "Khong ket noi duoc SQL Server tai '$Server'." }
Write-Host "  Server: $serverName | Database: $Database"

if ($Recreate) {
    Write-Step "Xoa DB demo (chi vi co -Recreate)"
    Invoke-Sql "master" "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END;" | Out-Null
    Pass "Da xoa neu DB ton tai"
}
else {
    Write-Host "  Khong xoa DB cu; migration idempotent se chay tiep."
}

Write-Step "Build Web"
Invoke-Checked "Build Web" @("build", $webProject, "--nologo", "-v", "minimal")

Write-Step "Ap migration tu dau"
Invoke-Checked "Migration" @(
    "ef", "database", "update",
    "--project", $webProject,
    "--startup-project", $startupProject,
    "--connection", $connection,
    "--no-build"
)
Pass "Migration da ap"

Write-Step "Seed du lieu demo"
$env:ConnectionStrings__DefaultConnection = $connection
$env:DemoSeed__Password = $Password
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
Invoke-Checked "Seed demo" @(
    "run", "--project", $webProject, "--no-build", "--", "--seed-demo"
)
Pass "Seeder chay xong"

Write-Step "Kiem tra du lieu demo"
$migrationCount = Invoke-Sql $Database "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;"
if ([int]$migrationCount -gt 0) { Pass "Da ap $migrationCount migration" } else { Fail "Khong co migration" }

Assert-Equal "Tai khoan demo" (Invoke-Sql $Database "SELECT COUNT(*) FROM AspNetUsers WHERE Email LIKE N'%@demo.local';") "6"
Assert-Equal "Vai tro" (Invoke-Sql $Database "SELECT COUNT(*) FROM AspNetRoles;") "4"
Assert-Equal "Nha tro" (Invoke-Sql $Database "SELECT COUNT(*) FROM Properties WHERE Name LIKE N'%Demo 3A%';") "2"
Assert-Equal "Phong (gồm thùng rác)" (Invoke-Sql $Database "SELECT COUNT(*) FROM Rooms WHERE Code IN (N'A101',N'A102',N'A103',N'B201',N'B202');") "5"
Assert-Equal "Hop dong" (Invoke-Sql $Database "SELECT COUNT(*) FROM Contracts WHERE ContractNumber IN (N'DEMO-A101-2026',N'DEMO-B201-2026');") "2"
Assert-Equal "Ky chi so" (Invoke-Sql $Database "SELECT COUNT(*) FROM UtilityReadings;") "4"
Assert-Equal "Lich su sua chi so" (Invoke-Sql $Database "SELECT COUNT(*) FROM UtilityReadingAudits;") "1"
Assert-Equal "Dich vu (gồm thùng rác)" (Invoke-Sql $Database "SELECT COUNT(*) FROM ServiceCatalogs WHERE Description LIKE N'Demo 3A:%';") "7"
Assert-Equal "Cong tac" (Invoke-Sql $Database "SELECT COUNT(*) FROM ManagerToggles;") "21"
Assert-Equal "Cong tac du 3 che do" (Invoke-Sql $Database "SELECT COUNT(DISTINCT Mode) FROM ManagerToggles;") "3"
Assert-Equal "Yeu cau cho duyet" (Invoke-Sql $Database "SELECT COUNT(*) FROM ManagerApprovalRequests WHERE Status = 0;") "1"
Assert-Equal "Phong trong thung rac" (Invoke-Sql $Database "SELECT COUNT(*) FROM Rooms WHERE IsDeleted = 1;") "1"
Assert-Equal "Khach trong thung rac" (Invoke-Sql $Database "SELECT COUNT(*) FROM Tenants WHERE IsDeleted = 1;") "1"
Assert-Equal "Dich vu trong thung rac" (Invoke-Sql $Database "SELECT COUNT(*) FROM ServiceCatalogs WHERE IsDeleted = 1;") "1"
Assert-Equal "Khong co hoa don" (Invoke-Sql $Database "SELECT COUNT(*) FROM Invoices;") "0"
Assert-Equal "Khong co payment" (Invoke-Sql $Database "SELECT COUNT(*) FROM Payments;") "0"

Write-Host "`nTai khoan demo (mat khau: $Password)" -ForegroundColor Yellow
Invoke-Sql $Database "SELECT Email + N' | ' + ISNULL(FullName, N'') FROM AspNetUsers WHERE Email LIKE N'%@demo.local' ORDER BY Email;" | Out-Null
& sqlcmd -S $Server -C -d $Database -W -f 65001 -Q "SET NOCOUNT ON; SELECT Email + N' | ' + ISNULL(FullName, N'') FROM AspNetUsers WHERE Email LIKE N'%@demo.local' ORDER BY Email;"

if ($failed -gt 0) {
    Write-Host "`nKET QUA: LOI ($failed kiem tra)" -ForegroundColor Red
    exit 1
}

Write-Host "`nKET QUA: DAT - DB $Database san sang; chay lai khong -Recreate de kiem idempotent." -ForegroundColor Green
Write-Host "Chuoi ket noi: $connection"
exit 0
