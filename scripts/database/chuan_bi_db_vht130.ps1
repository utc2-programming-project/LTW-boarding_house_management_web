# VHT-130 (2E) - Chuan bi database va ban giao cho nguoi cau hinh IIS.
#
# Muc dich: mot lenh duy nhat de
#   1. Generate: khoa dung mot SHA, build, kiem tra 29 migration, sinh script SQL idempotent.
#   2. Apply:    tao DB NhaTro_VHT130_2E, ap migration, cap quyen cho app pool IIS.
#   3. Verify:   kiem tra doc lap DB da san sang (khong can chay web).
#
# Cach dung:
#   powershell -File scripts/database/chuan_bi_db_vht130.ps1 -Mode Generate
#   powershell -File scripts/database/chuan_bi_db_vht130.ps1 -Mode Apply -Server "." -DryRun
#   powershell -File scripts/database/chuan_bi_db_vht130.ps1 -Mode Apply -Server "."
#   powershell -File scripts/database/chuan_bi_db_vht130.ps1 -Mode Verify  -Server "."
#
# Luu y:
#   - Script KHONG BAO GIO drop/xoa database hay du lieu.
#   - Phai tao app pool truoc khi Apply (neu khong login IIS APPPOOL\... khong ton tai).
#   - Chi dung Windows integrated; SQL o may khac thi dung lai (virtual account khong resolve).

[CmdletBinding()]
param(
    [ValidateSet("Generate", "Apply", "Verify")]
    [string]$Mode = "Verify",

    [string]$Server = ".",
    [string]$Database = "NhaTro_VHT130_2E",
    [string]$ExpectedSha = "0535041b2864990c43167883dd389eaaaca623ca",
    [string]$AppPoolName = "NhaTroVHT130",
    [string]$LoginName = "",
    [string]$Collation = "",

    [string]$EvidenceDir = "",
    [string]$ArtifactPath = "",

    [switch]$DryRun,
    [switch]$Force,
    [switch]$SkipGrant,
    [switch]$SkipImpersonationCheck,
    [switch]$AllowCaseSensitiveCollation,
    [switch]$KeepWorktree
)

# Native command stderr (git/dotnet/sqlcmd) la thong tin, khong phai loi terminating tren PS 5.1.
$ErrorActionPreference = "Continue"
$script:logFile = $null
$script:lastSqlExit = 0
$script:failed = $false

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sha7 = $ExpectedSha.Substring(0, [Math]::Min(7, $ExpectedSha.Length))
if (-not $LoginName) { $LoginName = "IIS APPPOOL\$AppPoolName" }
if (-not $EvidenceDir) {
    if (Test-Path (Join-Path $repoRoot "NhaTro\Web\Web.csproj")) {
        $EvidenceDir = Join-Path $repoRoot "outputs\db-handoff\VHT130_2E_$sha7"
    }
    else {
        $EvidenceDir = Join-Path $PSScriptRoot "VHT130_2E_$sha7"
    }
}

$LastMigrationId = "20261104033809_AddContractRenewalRequests"
$ForbiddenMigrationId = "20261104122603_AddPayments"
$ExpectedMigrationCount = 29

function Write-Log {
    param([string]$Text, [string]$Level = "INFO")
    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Level, $Text
    switch ($Level) {
        "OK" { Write-Host $line -ForegroundColor Green }
        "WARN" { Write-Host $line -ForegroundColor Yellow }
        "FAIL" { Write-Host $line -ForegroundColor Red }
        "ERROR" { Write-Host $line -ForegroundColor Red }
        default { Write-Host $line }
    }
    if ($script:logFile) {
        Add-Content -LiteralPath $script:logFile -Value $line -Encoding UTF8
    }
}

function Stop-With {
    param([int]$Code, [string]$Message)
    Write-Log $Message "ERROR"
    exit $Code
}

function Assert-Command {
    param([string]$Name, [int]$Code = 2)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Stop-With $Code "Khong tim thay lenh '$Name'. Cai dat/them vao PATH roi chay lai."
    }
}

function Save-Text {
    param([string]$Path, [string]$Text, [bool]$WithBom = $false)
    $encoding = New-Object System.Text.UTF8Encoding($WithBom)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function New-EvidenceDir {
    New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
    return $EvidenceDir
}

# ---------------------------------------------------------------------------
# SQL helpers
# ---------------------------------------------------------------------------

function Invoke-SqlcmdCapture {
    param([string]$DatabaseName, [string]$Query)
    $sqlArgs = @("-S", $Server, "-C")
    if ($DatabaseName) { $sqlArgs += @("-d", $DatabaseName) }
    $sqlArgs += @("-b", "-h", "-1", "-W", "-s", "|", "-f", "65001")
    $sqlArgs += @("-Q", ("SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; " + $Query))
    $out = @(& sqlcmd @sqlArgs 2>&1 | ForEach-Object { "$_" })
    $script:lastSqlExit = $LASTEXITCODE
    return $out
}

function Get-SqlScalar {
    param([string]$DatabaseName, [string]$Query)
    $out = @(Invoke-SqlcmdCapture -DatabaseName $DatabaseName -Query $Query)
    if ($script:lastSqlExit -ne 0) { return $null }
    $line = @($out | Where-Object { "$_".Trim() -ne "" }) | Select-Object -First 1
    if ($null -eq $line) { return $null }
    return "$line".Trim()
}

function Invoke-SqlRequired {
    param([string]$DatabaseName, [string]$Query, [int]$FailCode, [string]$Context)
    $out = @(Invoke-SqlcmdCapture -DatabaseName $DatabaseName -Query $Query)
    if ($script:lastSqlExit -ne 0) {
        $out | ForEach-Object { Write-Log "  sql: $_" "ERROR" }
        Stop-With $FailCode "$Context that bai (sqlcmd exit $script:lastSqlExit)."
    }
    return $out
}

function Get-SqlInstanceNames {
    $path = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"
    if (-not (Test-Path $path)) { return @() }
    return @((Get-ItemProperty -Path $path).PSObject.Properties |
        Where-Object { $_.Name -notmatch "^PS" } |
        ForEach-Object { "{0} (instance '{1}')" -f $_.Name, $_.Value })
}

function Get-SqlServiceStartType {
    $instance = $Server.TrimStart(".", "\")
    $serviceName = if ([string]::IsNullOrWhiteSpace($instance)) { "MSSQLSERVER" } else { "MSSQL`$$($instance.ToUpperInvariant())" }
    try {
        $svc = Get-Service -Name $serviceName -ErrorAction Stop
        return [pscustomobject]@{ Name = $svc.Name; Status = "$($svc.Status)"; StartType = "$($svc.StartType)" }
    } catch {
        return $null
    }
}

function Write-SqlServiceNote {
    $svc = Get-SqlServiceStartType
    if (-not $svc) {
        Write-Log "Khong doc duoc dich vu SQL Server tuong ung voi instance '$Server'." "WARN"
        return $null
    }
    Write-Log "Dich vu SQL Server: $($svc.Name) | $($svc.Status) | startup $($svc.StartType)"
    if ($svc.StartType -ne "Automatic") {
        Write-Log "StartupType dang la $($svc.StartType): dat thanh Automatic de web con ket noi DB sau khi reboot server." "WARN"
    }
    return $svc
}

function Get-ServerInfo {
    $query = @"
SELECT CONCAT(
    CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)), '|',
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)), '|',
    CAST(SERVERPROPERTY('ProductMajorVersion') AS nvarchar(10)), '|',
    CAST(SERVERPROPERTY('Collation') AS nvarchar(128)), '|',
    CAST(SERVERPROPERTY('Edition') AS nvarchar(128)));
"@
    $line = Get-SqlScalar -DatabaseName "" -Query $query
    if ($null -eq $line) {
        $names = (Get-SqlInstanceNames) -join "; "
        if (-not $names) { $names = "(khong doc duoc registry)" }
        Stop-With 2 "Khong ket noi duoc SQL Server tai '$Server'. Instance dang co: $names"
    }
    $parts = $line -split "\|"
    return [pscustomobject]@{
        ServerName    = $parts[0]
        Version       = $parts[1]
        MajorVersion  = [int]$parts[2]
        Collation     = $parts[3]
        Edition       = $parts[4]
        Raw           = $line
    }
}

function Test-DatabaseExists {
    $value = Get-SqlScalar -DatabaseName "" -Query "SELECT CASE WHEN DB_ID(N'$($Database.Replace("'", "''"))') IS NULL THEN 0 ELSE 1 END;"
    if ($null -eq $value) { Stop-With 2 "Khong kiem tra duoc DB_ID tren '$Server'." }
    return ([int]$value -eq 1)
}

function Get-MigrationHistory {
    $hasTable = Get-SqlScalar -DatabaseName $Database -Query "SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END;"
    if ($null -eq $hasTable) { Stop-With 4 "Khong doc duoc __EFMigrationsHistory tren $Database." }
    if ([int]$hasTable -eq 0) { return @() }
    return @(Invoke-SqlRequired -DatabaseName $Database -FailCode 4 -Context "Doc lich su migration" `
            -Query "SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;")
}

# ---------------------------------------------------------------------------
# Generate
# ---------------------------------------------------------------------------

function Invoke-Generate {
    Assert-Command git
    Assert-Command dotnet
    New-EvidenceDir | Out-Null
    $script:logFile = Join-Path $EvidenceDir ("generate-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

    $sha = (& git -C $repoRoot rev-parse "$ExpectedSha^{commit}" 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $sha) {
        Stop-With 2 "Khong resolve duoc SHA '$ExpectedSha' trong repo $repoRoot."
    }
    $sha = "$sha".Trim()
    Write-Log "SHA nguon: $sha"

    $devSha = (& git -C $repoRoot rev-parse origin/dev 2>$null)
    if ($LASTEXITCODE -eq 0 -and $devSha -and "$devSha".Trim() -ne $sha) {
        Write-Log "origin/dev hien la $("$devSha".Trim()), khac SHA dich. Artifact van khoa theo $sha." "WARN"
    }

    $tmp = Join-Path $env:TEMP ("nhatro-db-" + $sha.Substring(0, 7) + "-" + [guid]::NewGuid().ToString("N").Substring(0, 6))
    Write-Log "Tao worktree tam: $tmp"
    & git -C $repoRoot worktree add -q --detach $tmp $sha 2>&1 | ForEach-Object { Write-Log "  git: $_" }
    if ($LASTEXITCODE -ne 0) { Stop-With 3 "Khong tao duoc worktree tai $sha." }

    Push-Location $tmp
    try {
        $wtHead = (& git rev-parse HEAD).Trim()
        if ($wtHead -ne $sha) { Stop-With 3 "Worktree khong dung SHA (dang la $wtHead)." }
        $dirty = @(& git status --porcelain)
        if ($dirty.Count -gt 0) { Stop-With 3 "Worktree co thay doi chua commit, khong dung de sinh artifact." }
        $tree = (& git rev-parse "HEAD^{tree}").Trim()

        $dotnetVersion = (& dotnet --version).Trim()
        Write-Log "dotnet SDK: $dotnetVersion"
        & dotnet tool restore 2>&1 | ForEach-Object { Write-Log "  tool: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-With 2 "dotnet tool restore that bai (can mang/cache NuGet)." }

        $efVersion = ((& dotnet ef --version 2>&1) -join " ").Trim()
        if ($efVersion -match 'Run "dotnet tool restore"') {
            # Cache resolver tro duong dan cu (thuong do doi NUGET_PACKAGES).
            $resolverCache = Join-Path $env:USERPROFILE ".dotnet\toolResolverCache\1\dotnet-ef"
            if (Test-Path $resolverCache) {
                Write-Log "Cache resolver cua dotnet-ef bi lech; xoa cache va restore lai." "WARN"
                Remove-Item -LiteralPath $resolverCache -Force
                & dotnet tool restore 2>&1 | ForEach-Object { Write-Log "  tool: $_" }
                $efVersion = ((& dotnet ef --version 2>&1) -join " ").Trim()
            }
        }
        Write-Log "dotnet ef: $efVersion"
        if ($efVersion -match "older than that of the runtime") {
            Stop-With 2 "dotnet-ef cu hon EF runtime; cap nhat .config/dotnet-tools.json roi chay lai."
        }

        Write-Log "Build Release..."
        & dotnet build "NhaTro/Web/Web.csproj" -c Release --nologo -nodeReuse:false 2>&1 |
            ForEach-Object { Write-Log "  build: $_" }
        if ($LASTEXITCODE -ne 0) { Stop-With 1 "Build that bai." }

        Write-Log "Liet ke migration..."
        $list = @(& dotnet ef migrations list --no-connect --no-build --configuration Release `
                --project NhaTro/Web --startup-project NhaTro/Web 2>&1 | ForEach-Object { "$_" })
        Save-Text (Join-Path $EvidenceDir "migrations-list.txt") (($list -join "`r`n") + "`r`n")
        $ids = @($list | ForEach-Object { $_.Trim() } | Where-Object { $_ -match "^\d{14}_" })

        if ($ids.Count -ne $ExpectedMigrationCount) {
            Stop-With 3 "So migration la $($ids.Count), mong doi $ExpectedMigrationCount. Kiem tra lai SHA."
        }
        if ($ids[-1] -ne $LastMigrationId) {
            Stop-With 3 "Migration cuoi la '$($ids[-1])', mong doi '$LastMigrationId'."
        }
        $forbidden = @($ids | Where-Object { $_ -like "*$ForbiddenMigrationId*" })
        if ($forbidden.Count -gt 0) {
            Stop-With 3 "Phat hien migration chua merge '$($forbidden -join ', ')'. Khong dung SHA nay."
        }
        Write-Log "$($ids.Count) migration, cuoi: $($ids[-1])" "OK"

        Write-Log "Kiem tra model lech snapshot..."
        $pending = @(& dotnet ef migrations has-pending-model-changes --no-build --configuration Release `
                --project NhaTro/Web --startup-project NhaTro/Web 2>&1 | ForEach-Object { "$_" })
        Save-Text (Join-Path $EvidenceDir "has-pending.txt") (($pending -join "`r`n") + "`r`n")
        if ($LASTEXITCODE -ne 0) {
            $pending | ForEach-Object { Write-Log "  ef: $_" "ERROR" }
            Stop-With 3 "Model dang lech snapshot (has-pending-model-changes fail)."
        }

        Write-Log "Sinh script idempotent..."
        $sqlPath = Join-Path $EvidenceDir "01_migrations.sql"
        & dotnet ef migrations script --idempotent --no-build --configuration Release `
            --project NhaTro/Web --startup-project NhaTro/Web --output $sqlPath 2>&1 |
            ForEach-Object { Write-Log "  ef: $_" }
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $sqlPath)) { Stop-With 1 "Sinh script migration that bai." }

        $preamble = @"
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

"@
        $body = [System.IO.File]::ReadAllText($sqlPath)
        # EF phat migrationBuilder.Sql inline trong cung batch voi AddColumn; SQL Server
        # khong resolve duoc cot vua them trong batch do. Boc EXEC de defer compilation.
        $approvedNeedle = "UPDATE [AspNetUsers] SET [ApprovedAt] = SYSUTCDATETIME() WHERE [ApprovedAt] IS NULL;"
        $approvedPatched = "EXEC(N'UPDATE [AspNetUsers] SET [ApprovedAt] = SYSUTCDATETIME() WHERE [ApprovedAt] IS NULL;');"
        $approvedHits = ([regex]::Matches($body, [regex]::Escape($approvedNeedle))).Count
        if ($approvedHits -ne 1) {
            Stop-With 3 "Mong doi 1 cau lenh ApprovedAt trong script, tim thay $approvedHits. Dung SHA/EF khac nen dung lai."
        }
        $body = $body.Replace($approvedNeedle, $approvedPatched)
        Write-Log "Da va batch ApprovedAt (deferred name resolution)."
        Save-Text $sqlPath ($preamble + $body)

        $hash = (Get-FileHash -LiteralPath $sqlPath -Algorithm SHA256).Hash
        $manifest = [ordered]@{
            formatVersion  = 1
            task           = "VHT-130"
            phase          = "2E"
            expectedSha    = $sha
            tree           = $tree
            generatedAt    = (Get-Date).ToString("o")
            generatedBy    = "$env:USERDOMAIN\$env:USERNAME"
            dotnetSdk      = $dotnetVersion
            dotnetEf       = $efVersion
            migrationCount = $ids.Count
            migrationIds   = $ids
            lastMigration  = $ids[-1]
            scriptFile     = "01_migrations.sql"
            scriptSha256   = $hash
            patches        = @("AddUserApprovedAt UPDATE wrapped in EXEC for deferred name resolution")
            database       = $Database
            appPool        = $AppPoolName
            login          = $LoginName
        }
        $json = $manifest | ConvertTo-Json -Depth 5
        Save-Text (Join-Path $EvidenceDir "manifest.json") $json

        Write-Log "Artifact: $EvidenceDir"
        Write-Log "SHA-256 script: $hash"
        Write-Log "Generate thanh cong. Copy thu muc nay + script sang server." "OK"
    }
    finally {
        Pop-Location
        if (-not $KeepWorktree) {
            & git -C $repoRoot worktree remove --force --force $tmp 2>&1 | Out-Null
            & git -C $repoRoot worktree prune 2>&1 | Out-Null
        }
    }
    exit 0
}

# ---------------------------------------------------------------------------
# Artifact resolver
# ---------------------------------------------------------------------------

function Resolve-Artifact {
    $dir = if ($ArtifactPath) { $ArtifactPath } else { $EvidenceDir }
    $sqlPath = Join-Path $dir "01_migrations.sql"
    $manifestPath = Join-Path $dir "manifest.json"
    if (-not (Test-Path $sqlPath)) { Stop-With 2 "Khong thay $sqlPath. Chay -Mode Generate truoc hoac truyen -ArtifactPath." }
    if (-not (Test-Path $manifestPath)) { Stop-With 2 "Khong thay $manifestPath. Artifact phai co manifest.json." }

    $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
    $manifestSha = "$($manifest.expectedSha)".Trim()
    $wanted = "$ExpectedSha".Trim().ToLowerInvariant()
    if ($manifestSha.ToLowerInvariant() -ne $wanted -and -not $manifestSha.ToLowerInvariant().StartsWith($wanted)) {
        Stop-With 3 "Artifact duoc sinh tu $manifestSha, khong phai $ExpectedSha."
    }
    $actualHash = (Get-FileHash -LiteralPath $sqlPath -Algorithm SHA256).Hash
    if ($actualHash -ne "$($manifest.scriptSha256)".Trim()) {
        Stop-With 3 "SHA-256 cua 01_migrations.sql khong khop manifest (file bi thay doi)."
    }
    if ([int]$manifest.migrationCount -ne $ExpectedMigrationCount) {
        Stop-With 3 "Artifact co $($manifest.migrationCount) migration, mong doi $ExpectedMigrationCount."
    }
    Write-Log "Artifact hop le: $dir (SHA $manifestSha, hash $actualHash)"
    return [pscustomobject]@{
        Dir      = $dir
        SqlPath  = $sqlPath
        Manifest = $manifest
        Hash     = $actualHash
    }
}

# ---------------------------------------------------------------------------
# Apply
# ---------------------------------------------------------------------------

function Invoke-Apply {
    Assert-Command sqlcmd
    New-EvidenceDir | Out-Null
    $script:logFile = Join-Path $EvidenceDir ("apply-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

    $artifact = Resolve-Artifact
    $info = Get-ServerInfo
    Write-Log "Server: $($info.ServerName) | $($info.Version) | $($info.Edition) | collation $($info.Collation)"
    Save-Text (Join-Path $EvidenceDir "server-info.txt") ($info.Raw + "`r`n")
    $svcInfo = Write-SqlServiceNote

    if ($info.MajorVersion -lt 13) {
        Stop-With 2 "SQL Server $($info.Version) qua cu; can SQL Server 2016 tro len (trigger dung CREATE OR ALTER)."
    }
    if ($info.Collation -match "_(CS|BIN)" -and -not $AllowCaseSensitiveCollation) {
        Stop-With 2 "Server collation '$($info.Collation)' phan biet hoa/thuong. Dung -AllowCaseSensitiveCollation neu da xac nhan, hoac -Collation de chi dinh."
    }
    if ($Collation -and $Collation -notmatch "^[A-Za-z0-9_]+$") {
        Stop-With 2 "Ten collation '$Collation' khong hop le."
    }

    $exists = Test-DatabaseExists
    if ($exists -and -not $Force) {
        Stop-With 4 "Database '$Database' da ton tai. Dung -Force neu chac chan muon ap tiep (script idempotent, khong xoa du lieu)."
    }
    if ($exists) {
        $history = Get-MigrationHistory
        $expectedIds = @($artifact.Manifest.migrationIds)
        $prefixOk = $true
        for ($i = 0; $i -lt $history.Count; $i++) {
            if ($i -ge $expectedIds.Count -or $history[$i] -ne $expectedIds[$i]) { $prefixOk = $false; break }
        }
        if (-not $prefixOk) {
            Stop-With 4 "Lich su migration cua '$Database' khong phai tien to cua artifact. Kiem tra lai truoc khi ap."
        }
        Write-Log "DB da ton tai, da co $($history.Count)/$($expectedIds.Count) migration." "WARN"
    }

    if ($DryRun) {
        Write-Log "DRY-RUN: ke hoach tren '$Server' / '$Database'"
        Write-Log "  1. $(if ($exists) { 'Bo qua CREATE DATABASE (da ton tai)' } else { "CREATE DATABASE [$Database]$(if ($Collation) { " COLLATE $Collation" }) + READ_COMMITTED_SNAPSHOT ON + AUTO_CLOSE OFF" })"
        Write-Log "  2. sqlcmd -i 01_migrations.sql (idempotent, $($artifact.Manifest.migrationCount) migration)"
        Write-Log "  3. $(if ($SkipGrant) { 'BO QUA grant (-SkipGrant)' } else { "Cap quyen cho $LoginName (db_datareader + db_datawriter, default schema dbo)" })"
        Write-Log "  4. Verify + xuat BAN_GIAO.md vao $EvidenceDir"
        Write-Log "DRY-RUN: khong thay doi gi." "OK"
        exit 0
    }

    if (-not $exists) {
        $createSql = "CREATE DATABASE [$Database]"
        if ($Collation) { $createSql += " COLLATE $Collation" }
        Invoke-SqlRequired -DatabaseName "" -FailCode 5 -Context "Tao database" -Query ($createSql + ";")
        Write-Log "Da tao database [$Database]" "OK"
        Invoke-SqlRequired -DatabaseName "" -FailCode 5 -Context "Bat READ_COMMITTED_SNAPSHOT" `
            -Query "ALTER DATABASE [$Database] SET READ_COMMITTED_SNAPSHOT ON;" | Out-Null
        Invoke-SqlRequired -DatabaseName "" -FailCode 5 -Context "Tat AUTO_CLOSE" `
            -Query "ALTER DATABASE [$Database] SET AUTO_CLOSE OFF;" | Out-Null
    }

    $before = @(Get-MigrationHistory).Count
    Write-Log "Ap migration bang sqlcmd (truoc: $before migration)..."
    $applyLog = Join-Path $EvidenceDir ("sqlcmd-apply-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
    # -I: filtered index can QUOTED_IDENTIFIER; -f 65001: doc seed UTF-8 dung (m3, tieng Viet).
    $sqlArgs = @("-S", $Server, "-C", "-d", $Database, "-b", "-I", "-f", "65001", "-r", "1",
        "-i", $artifact.SqlPath, "-o", $applyLog)
    & sqlcmd @sqlArgs | Out-Null
    $applyExit = $LASTEXITCODE
    if ($applyExit -ne 0) {
        if (Test-Path $applyLog) {
            Get-Content -LiteralPath $applyLog -Tail 40 | ForEach-Object { Write-Log "  sql: $_" "ERROR" }
        }
        Stop-With 5 "Ap migration that bai (sqlcmd exit $applyExit). Log: $applyLog"
    }
    $after = @(Get-MigrationHistory).Count
    Write-Log "Migration da ap: $before -> $after (log: $applyLog)" "OK"

    if (-not $SkipGrant) {
        Invoke-Grant
    }
    else {
        Write-Log "Bo qua cap quyen theo -SkipGrant. Chi dung cho dien tap." "WARN"
    }

    $verifyOk = Invoke-VerifyCore -Artifact $artifact
    if (-not $verifyOk) { exit 6 }
    Write-BanGiao -Info $info -Artifact $artifact
    exit 0
}

function Invoke-Grant {
    $loginSql = $LoginName.Replace("'", "''")
    $loginQuoted = $LoginName.Replace("]", "]]")

    $serverSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$loginSql')
    CREATE LOGIN [$loginQuoted] FROM WINDOWS;
"@
    $serverOut = @(Invoke-SqlcmdCapture -DatabaseName "" -Query $serverSql)
    if ($script:lastSqlExit -ne 0) {
        $serverOut | ForEach-Object { Write-Log "  sql: $_" "ERROR" }
        Stop-With 7 "Khong tao duoc login '$LoginName'. Thuong do app pool '$AppPoolName' chua ton tai (loi 15401) - tao app pool truoc roi chay lai. Khong cap db_owner."
    }

    $dbSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$loginSql')
    CREATE USER [$loginQuoted] FOR LOGIN [$loginQuoted];
ALTER USER [$loginQuoted] WITH DEFAULT_SCHEMA = [dbo];
GRANT CONNECT TO [$loginQuoted];
IF IS_ROLEMEMBER('db_datareader', N'$loginSql') <> 1 ALTER ROLE db_datareader ADD MEMBER [$loginQuoted];
IF IS_ROLEMEMBER('db_datawriter', N'$loginSql') <> 1 ALTER ROLE db_datawriter ADD MEMBER [$loginQuoted];
DENY INSERT, UPDATE, DELETE ON [dbo].[__EFMigrationsHistory] TO [$loginQuoted];
"@
    Invoke-SqlRequired -DatabaseName $Database -FailCode 7 -Context "Cap quyen cho $LoginName" -Query $dbSql | Out-Null
    Write-Log "Da cap quyen: $LoginName -> $Database (db_datareader, db_datawriter, default schema dbo)" "OK"
}

# ---------------------------------------------------------------------------
# Verify
# ---------------------------------------------------------------------------

function Test-Check {
    param([string]$Label, $Actual, $Expected)
    if ("$Actual" -eq "$Expected") {
        Write-Log "  [ OK ] $Label = $Actual" "OK"
        return $true
    }
    Write-Log "  [FAIL] $Label = '$Actual' (mong doi '$Expected')" "FAIL"
    $script:failed = $true
    return $false
}

function Test-CheckAtLeast {
    param([string]$Label, $Actual, [int]$Minimum)
    $number = 0
    if ($null -ne $Actual -and [int]::TryParse("$Actual", [ref]$number) -and $number -ge $Minimum) {
        Write-Log "  [ OK ] $Label = $number (>= $Minimum)" "OK"
        return $true
    }
    Write-Log "  [FAIL] $Label = '$Actual' (can >= $Minimum)" "FAIL"
    $script:failed = $true
    return $false
}

function Invoke-VerifyCore {
    param($Artifact)
    $script:failed = $false
    $migrationCount = if ($Artifact) { [int]$Artifact.Manifest.migrationCount } else { $ExpectedMigrationCount }

    Write-Log "Verify schema..."
    Test-Check "So migration trong lich su" (Get-SqlScalar $Database "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;") $migrationCount | Out-Null
    Test-Check "Migration cuoi" (Get-SqlScalar $Database "SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory;") $LastMigrationId | Out-Null
    Test-Check "Khong co AddPayments (VHT-54)" (Get-SqlScalar $Database "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%AddPayments%';") 0 | Out-Null
    Test-Check "Khong co bang Payments" (Get-SqlScalar $Database "SELECT CASE WHEN OBJECT_ID(N'dbo.Payments', N'U') IS NULL THEN 'NULL' ELSE 'CO' END;") "NULL" | Out-Null
    Test-Check "Trigger chong trung hop dong bat" (Get-SqlScalar $Database "SELECT COUNT(*) FROM sys.triggers WHERE name = N'TR_Contracts_PreventApprovedOverlap' AND is_disabled = 0;") 1 | Out-Null
    Test-Check "Rooms co DeletedAt/DeletedBy" (Get-SqlScalar $Database "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Rooms') AND name IN (N'DeletedAt', N'DeletedBy');") 2 | Out-Null
    Test-Check "Filtered unique index ContractRenewalRequests" (Get-SqlScalar $Database "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContractRenewalRequests') AND name = N'UX_ContractRenewalRequests_Pending' AND is_unique = 1 AND has_filter = 1;") 1 | Out-Null
    Test-CheckAtLeast "Seed Unicode m3" (Get-SqlScalar $Database "SELECT COUNT(*) FROM dbo.UtilityTariffs WHERE Unit = N'm³';") 1 | Out-Null
    Test-CheckAtLeast "Seed Unicode Ben Thanh" (Get-SqlScalar $Database "SELECT COUNT(*) FROM dbo.UtilityTariffs WHERE SourceName LIKE N'%Bến Thành%';") 1 | Out-Null
    $filteredIndexes = Get-SqlScalar $Database "SELECT COUNT(*) FROM sys.indexes WHERE has_filter = 1 AND OBJECTPROPERTY(object_id, 'IsMSShipped') = 0;"
    Write-Log "  [INFO] So filtered index: $filteredIndexes"

    if (-not $SkipGrant) {
        Write-Log "Verify quyen $LoginName..."
        Test-Check "Default schema" (Get-SqlScalar $Database "SELECT default_schema_name FROM sys.database_principals WHERE name = N'$($LoginName.Replace("'", "''"))';") "dbo" | Out-Null
        Test-Check "Vai tro doc/ghi" (Get-SqlScalar $Database "SELECT COUNT(*) FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id = m.role_principal_id JOIN sys.database_principals u ON u.principal_id = m.member_principal_id WHERE u.name = N'$($LoginName.Replace("'", "''"))' AND r.name IN ('db_datareader', 'db_datawriter');") 2 | Out-Null
        Test-Check "Khong phai db_owner" (Get-SqlScalar $Database "SELECT IS_ROLEMEMBER('db_owner', N'$($LoginName.Replace("'", "''"))');") 0 | Out-Null
    }

    if (-not $SkipGrant -and -not $SkipImpersonationCheck) {
        Write-Log "Verify quyen bang EXECUTE AS LOGIN (doc/ghi/khong sua lich su)..."
        $loginSql = $LoginName.Replace("'", "''")
        $impersonate = @"
EXECUTE AS LOGIN = N'$loginSql';
SELECT IS_ROLEMEMBER('db_owner');
SELECT CONCAT(HAS_PERMS_BY_NAME(N'dbo.Contracts', N'OBJECT', N'SELECT'), '|', HAS_PERMS_BY_NAME(N'dbo.Contracts', N'OBJECT', N'INSERT'), '|', HAS_PERMS_BY_NAME(N'dbo.__EFMigrationsHistory', N'OBJECT', N'UPDATE'));
BEGIN TRAN;
INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (N'vht130-smoke', N'VHT130_SMOKE', N'VHT130_SMOKE', N'x');
SELECT COUNT(*) FROM dbo.Contracts;
ROLLBACK;
REVERT;
"@
        $out = @(Invoke-SqlcmdCapture -DatabaseName $Database -Query $impersonate)
        if ($script:lastSqlExit -ne 0) {
            $out | ForEach-Object { Write-Log "  sql: $_" "WARN" }
            Write-Log "Khong chay duoc EXECUTE AS LOGIN (can sysadmin). Bo qua, dung -SkipImpersonationCheck de tat han." "WARN"
        }
        else {
            $lines = @($out | Where-Object { "$_".Trim() -ne "" } | ForEach-Object { $_.Trim() })
            Test-Check "Khong phai db_owner (impersonate)" $lines[0] 0 | Out-Null
            Test-Check "Quyen doc/ghi/thu tu lich su (1|1|0)" $lines[1] "1|1|0" | Out-Null
            Test-CheckAtLeast "Doc duoc Contracts" $lines[2] 0 | Out-Null
        }
    }

    if ($script:failed) {
        Write-Log "Verify: CO LOI." "FAIL"
        return $false
    }
    Write-Log "Verify: DAT." "OK"
    return $true
}

function Invoke-VerifyMode {
    Assert-Command sqlcmd
    New-EvidenceDir | Out-Null
    $script:logFile = Join-Path $EvidenceDir ("verify-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
    $info = Get-ServerInfo
    Write-Log "Server: $($info.ServerName) | $($info.Version) | collation $($info.Collation)"
    Write-SqlServiceNote | Out-Null
    if (-not (Test-DatabaseExists)) { Stop-With 4 "Database '$Database' chua ton tai tren '$Server'." }
    $artifact = $null
    try { $artifact = Resolve-Artifact } catch { Write-Log "Khong doc duoc artifact, dung ky vong mac dinh ($ExpectedMigrationCount migration)." "WARN" }
    $ok = Invoke-VerifyCore -Artifact $artifact
    if (-not $ok) { exit 6 }
    exit 0
}

# ---------------------------------------------------------------------------
# Handoff document
# ---------------------------------------------------------------------------

function Write-BanGiao {
    param($Info, $Artifact)
    $connection = "Server=$Server;Database=$Database;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Application Name=NhaTro-Web"
    $manifest = $Artifact.Manifest
    $svc = Get-SqlServiceStartType
    $svcRow = if ($svc) { "$($svc.Name): $($svc.Status), startup $($svc.StartType)" } else { "khong doc duoc" }
    $svcNote = ""
    if ($svc -and $svc.StartType -ne "Automatic") {
        $svcNote = "`r`nDịch vụ đang để startup ``$($svc.StartType)``: đặt thành ``Automatic`` để web còn kết nối DB sau khi reboot."
    }
    $content = @"
# Bàn giao database VHT-130 (2E) cho người cấu hình IIS

Sinh lúc: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")

## Nguồn schema

| Mục | Giá trị |
|---|---|
| Commit | ``$($manifest.expectedSha)`` |
| Tree | ``$($manifest.tree)`` |
| Số migration | $($manifest.migrationCount) |
| Migration cuối | ``$($manifest.lastMigration)`` |
| SHA-256 file SQL | ``$($Artifact.Hash)`` |
| dotnet SDK / dotnet-ef | $($manifest.dotnetSdk) / $($manifest.dotnetEf) |

## Database đã tạo

| Mục | Giá trị |
|---|---|
| Server | ``$($Info.ServerName)`` (tham số ``$Server``) |
| Database | ``$Database`` |
| Collation | $($Info.Collation) |
| READ_COMMITTED_SNAPSHOT | ON |
| AUTO_CLOSE | OFF |
| Bản SQL Server | $($Info.Version) ($($Info.Edition)) |
| Dịch vụ SQL Server | ``$svcRow`` |
$svcNote

## Tài khoản chạy web

- App pool: ``$AppPoolName`` (ApplicationPoolIdentity)
- SQL login: ``$LoginName``, default schema ``dbo``
- Vai trò: ``db_datareader`` + ``db_datawriter`` (KHÔNG phải db_owner/sysadmin)
- Ghi ``__EFMigrationsHistory`` bị DENY; migration chỉ chạy bằng tài khoản admin.

## Chuỗi kết nối (đặt làm env var của app pool)

``````
ConnectionStrings__DefaultConnection=$connection
``````

## Biến môi trường còn lại của app pool

``````
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
ASPNETCORE_HTTPS_PORT=443
AllowedHosts=<tên miền thật>
Storage__RootPath=D:\App_Data\NhaTro\storage
Security__DataProtectionKeysPath=D:\App_Data\NhaTro\keys
Security__ProtectKeysWithDpapi=true
Logging__Directory=D:\App_Data\NhaTro\logs
AdminSeed__Email=<email admin>
AdminSeed__Password=<mật khẩu mạnh, đặt tạm>
``````

Lưu ý về ``AdminSeed__*``: Production không tự tạo admin. Đặt hai biến này trước lần khởi động đầu,
đăng nhập kiểm tra user thuộc role ``Admin``, rồi gỡ hai biến đi. Mật khẩu không nằm trong artifact này.

## Điều kiện trước khi Apply (nếu chưa chạy)

1. SQL Server đã cài, instance ``$Server`` truy cập được, tài khoản chạy script có quyền tạo DB + login.
2. App pool ``$AppPoolName`` đã tồn tại (No Managed Code, Integrated, Load User Profile = true, 32-bit = false).
3. Chạy ``-Mode Apply -DryRun`` trước, rồi ``-Mode Apply``, rồi ``-Mode Verify``.

## Bằng chứng

- ``01_migrations.sql``, ``manifest.json``, ``migrations-list.txt``, ``has-pending.txt``
- ``server-info.txt``, ``apply-*.log``, ``sqlcmd-apply-*.log``, ``verify-*.log``

## Ngoài phạm vi bàn giao này

Publish/deploy gói, cấu hình site/binding/certificate, ACL storage–keys–logs, DPAPI, smoke test web,
và migration ``AddPayments`` của nhánh VHT-54 (chưa merge).
"@
    $path = Join-Path $EvidenceDir "BAN_GIAO.md"
    Save-Text $path $content
    Write-Log "Da xuat $path" "OK"
}

# ---------------------------------------------------------------------------
# Dispatch
# ---------------------------------------------------------------------------

switch ($Mode) {
    "Generate" { Invoke-Generate }
    "Apply" { Invoke-Apply }
    "Verify" { Invoke-VerifyMode }
}
