# VHT-130 (2E) - Cap va kiem quyen thu muc cho ban publish IIS.
#
# Muc dich: mot lenh de
#   1. Check: chi doc, bao cao quyen cua identity app pool tren 4 duong dan.
#   2. Apply: publish = Read & Execute; storage/keys/log = Modify.
#
# Cach dung:
#   powershell -File scripts/database/cap_quyen_thu_muc_iis.ps1 -Mode Check `
#       -PublishPath "D:\Sites\NhaTro\releases\<SHA>"
#   powershell -File scripts/database/cap_quyen_thu_muc_iis.ps1 -Mode Apply `
#       -PublishPath "D:\Sites\NhaTro\releases\<SHA>" -CreateMissing
#   powershell -File scripts/database/cap_quyen_thu_muc_iis.ps1 -Mode Apply `
#       -PublishPath "..." -RemoveBroadRights
#
# Luu y:
#   - Check chay duoc khi KHONG co quyen admin; Apply can PowerShell quan tri.
#   - Script KHONG xoa thu muc, KHONG xoa file, KHONG dong vao quyen cua
#     Administrators/SYSTEM ngoai tru khi -RemoveBroadRights (khi do van giu lai 2 nhom nay).
#   - -RemoveBroadRights chi tac dong 3 thu muc du lieu: bo ACE ke thua, roi cap lai
#     Administrators (F), SYSTEM (F), identity app (M). Publish chi canh bao, khong tu go.
#   - Tao app pool truoc khi Apply; khong co app pool thi identity IIS APPPOOL\... khong ton tai.
#   - Storage/keys/log phai nam ngoai thu muc publish (kiem tra tu dong).

[CmdletBinding()]
param(
    [ValidateSet("Check", "Apply")]
    [string]$Mode = "Check",

    [string]$AppPoolName = "NhaTroVHT130",
    [string]$Identity = "",

    [string]$PublishPath = "",
    [string]$StoragePath = "D:\App_Data\NhaTro\storage",
    [string]$KeysPath = "D:\App_Data\NhaTro\keys",
    [string]$LogPath = "D:\App_Data\NhaTro\logs",

    [string]$LogDir = "",

    [switch]$CreateMissing,
    [switch]$RemoveBroadRights,
    [switch]$DryRun
)

# Native command stderr (icacls) la thong tin, khong phai loi terminating tren PS 5.1.
$ErrorActionPreference = "Continue"
$script:logFile = $null
$script:problems = 0
$script:warnings = 0
$script:repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$SidEveryone = "S-1-1-0"
$SidAuthenticatedUsers = "S-1-5-11"
$SidUsers = "S-1-5-32-545"
$SidIisIusrs = "S-1-5-32-568"
$SidAdministrators = "S-1-5-32-544"
$SidSystem = "S-1-5-18"

$BroadNames = @{
    $SidEveryone           = "Everyone"
    $SidAuthenticatedUsers = "Authenticated Users"
    $SidUsers              = "Users"
    $SidIisIusrs           = "IIS_IUSRS"
}

$RightsReadExecute = [System.Security.AccessControl.FileSystemRights]::ReadAndExecute
$RightsModify = [System.Security.AccessControl.FileSystemRights]::Modify
$RightsFull = [System.Security.AccessControl.FileSystemRights]::FullControl
$RightsWrite = [System.Security.AccessControl.FileSystemRights]::Write

function Write-Log {
    param([string]$Text, [string]$Level = "INFO")
    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Level, $Text
    switch ($Level) {
        "OK" { Write-Host $line -ForegroundColor Green }
        "WARN" { Write-Host $line -ForegroundColor Yellow }
        "FAIL" { Write-Host $line -ForegroundColor Red }
        default { Write-Host $line }
    }
    if ($script:logFile) {
        Add-Content -LiteralPath $script:logFile -Value $line -Encoding UTF8
    }
}

function Stop-With {
    param([int]$Code, [string]$Message)
    Write-Log $Message "FAIL"
    if ($script:logFile) { Write-Log ("Log: {0}" -f $script:logFile) }
    exit $Code
}

function Format-Rights {
    param([System.Security.AccessControl.FileSystemRights]$Rights)
    if ($Rights -eq 0) { return "None" }
    if (($Rights -band $RightsFull) -eq $RightsFull) { return "FullControl" }
    if (($Rights -band $RightsModify) -eq $RightsModify) { return "Modify" }
    if (($Rights -band $RightsReadExecute) -eq $RightsReadExecute) { return "ReadAndExecute" }
    if (($Rights -band $RightsWrite) -eq $RightsWrite) { return "Write" }
    return $Rights.ToString()
}

function ConvertTo-SidValue {
    param($IdentityReference)
    if ($null -eq $IdentityReference) { return $null }
    if ($IdentityReference -is [System.Security.Principal.SecurityIdentifier]) {
        return $IdentityReference.Value
    }
    try {
        return $IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
    }
    catch {
        return $null
    }
}

function Test-IsAdmin {
    $principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-AppIdentity {
    if ($Identity) { return $Identity }

    try {
        Import-Module WebAdministration -ErrorAction Stop
        $pool = Get-Item "IIS:\AppPools\$AppPoolName" -ErrorAction Stop
        $identityType = [string]$pool.processModel.identityType
        if ($identityType -eq "ApplicationPoolIdentity") {
            return "IIS APPPOOL\$AppPoolName"
        }
        if ($pool.processModel.userName) {
            Write-Log ("App pool '{0}' chay bang tai khoan tuy chinh: {1}" -f $AppPoolName, $pool.processModel.userName) "WARN"
            return [string]$pool.processModel.userName
        }
    }
    catch {
        Write-Log ("Khong doc duoc app pool '{0}' qua IIS provider; dung mac dinh IIS APPPOOL\{0}." -f $AppPoolName) "WARN"
    }
    return "IIS APPPOOL\$AppPoolName"
}

function Get-IdentityMask {
    param([System.Security.AccessControl.DirectorySecurity]$Acl, [string]$Sid)
    $mask = [System.Security.AccessControl.FileSystemRights]0
    $inherits = $false
    foreach ($rule in $Acl.Access) {
        if ($rule.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Allow) { continue }
        if ((ConvertTo-SidValue $rule.IdentityReference) -ne $Sid) { continue }
        $mask = $mask -bor $rule.FileSystemRights
        if ($rule.InheritanceFlags -ne [System.Security.AccessControl.InheritanceFlags]::None) { $inherits = $true }
    }
    return [pscustomobject]@{ Rights = $mask; Inherits = $inherits }
}

function Get-BroadRules {
    param([System.Security.AccessControl.DirectorySecurity]$Acl)
    $found = @{}
    foreach ($rule in $Acl.Access) {
        if ($rule.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Allow) { continue }
        $sid = ConvertTo-SidValue $rule.IdentityReference
        if (-not $sid -or -not $BroadNames.ContainsKey($sid)) { continue }
        if (-not $found.ContainsKey($sid)) {
            $found[$sid] = [pscustomobject]@{ Name = $BroadNames[$sid]; Rights = [System.Security.AccessControl.FileSystemRights]0 }
        }
        $found[$sid].Rights = $found[$sid].Rights -bor $rule.FileSystemRights
    }
    return @($found.Values)
}

function Get-FullPath {
    param([string]$Path)
    try { return [System.IO.Path]::GetFullPath($Path) } catch { return $Path }
}

function Test-PathInside {
    param([string]$Child, [string]$Parent)
    $c = (Get-FullPath $Child).TrimEnd("\") + "\"
    $p = (Get-FullPath $Parent).TrimEnd("\") + "\"
    return $c.StartsWith($p, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-Layout {
    if (Test-PathInside $PublishPath $StoragePath) {
        Write-Log "Thu muc publish nam trong storage; app se ghi file vao site. Doi duong dan." "FAIL"
        $script:problems++
    }
    if (Test-PathInside $StoragePath $PublishPath) {
        Write-Log "Storage nam trong thu muc publish; file upload co the tai truc tiep qua URL. Doi duong dan." "FAIL"
        $script:problems++
    }
    if (Test-PathInside $KeysPath $PublishPath) {
        Write-Log "Thu muc keys nam trong publish; key ring khong duoc nam trong site." "FAIL"
        $script:problems++
    }
    if (Test-PathInside $LogPath $PublishPath) {
        Write-Log "Thu muc log nam trong publish; log khong duoc nam trong site." "FAIL"
        $script:problems++
    }

    $dataPaths = @(
        @{ Kind = "storage"; Path = $StoragePath },
        @{ Kind = "keys"; Path = $KeysPath },
        @{ Kind = "log"; Path = $LogPath }
    )
    for ($i = 0; $i -lt $dataPaths.Count; $i++) {
        for ($j = $i + 1; $j -lt $dataPaths.Count; $j++) {
            $a = Get-FullPath $dataPaths[$i].Path
            $b = Get-FullPath $dataPaths[$j].Path
            if ($a.TrimEnd("\") -ieq $b.TrimEnd("\")) {
                Write-Log ("{0} va {1} trung cung mot thu muc: {2}" -f $dataPaths[$i].Kind, $dataPaths[$j].Kind, $a) "FAIL"
                $script:problems++
            }
            elseif ((Test-PathInside $a $b) -or (Test-PathInside $b $a)) {
                Write-Log ("{0} va {1} long nhau; nen tach rieng de phan quyen doc lap." -f $dataPaths[$i].Kind, $dataPaths[$j].Kind) "WARN"
            }
        }
    }
}

function Invoke-Icacls {
    param([string]$Path, [string[]]$Arguments)
    $display = 'icacls "{0}" {1}' -f $Path, ($Arguments -join ' ')
    if ($DryRun) {
        Write-Log ("[DRY-RUN] {0}" -f $display)
        return
    }
    $output = & icacls.exe $Path @Arguments 2>&1
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        Write-Log ("icacls loi (exit {0}): {1} | {2}" -f $code, $display, (($output | Out-String).Trim())) "FAIL"
        Stop-With 5 ("Khong cap duoc quyen tren: {0}" -f $Path)
    }
    Write-Log ("OK {0}" -f $display) "OK"
}

function Grant-PathRights {
    param([string]$Kind, [string]$Path, [string]$Sid)

    $isPublish = ($Kind -eq "publish")

    if (-not (Test-Path -LiteralPath $Path)) {
        if ($isPublish) {
            Stop-With 2 ("Khong tim thay thu muc publish: {0}. Publish artifact truoc khi cap quyen." -f $Path)
        }
        if (-not $CreateMissing) {
            Stop-With 2 ("Thieu thu muc {0}: {1}. Tao thu muc (hoac them -CreateMissing) roi chay lai." -f $Kind, $Path)
        }
        if ($DryRun) {
            Write-Log ("[DRY-RUN] se tao thu muc {0}: {1}" -f $Kind, $Path)
        }
        else {
            New-Item -ItemType Directory -Path $Path -Force | Out-Null
            Write-Log ("Da tao thu muc {0}: {1}" -f $Kind, $Path) "OK"
        }
    }

    if ($isPublish) {
        # Chi them Read & Execute; khong dung quyen ghi cua identity tren thu muc publish.
        Invoke-Icacls -Path $Path -Arguments @("/grant", ("*{0}:(OI)(CI)RX" -f $Sid))
        return
    }

    if ($RemoveBroadRights) {
        Write-Log ("Go ke thua va dung lai ACL toi thieu cho {0}: {1}" -f $Kind, $Path) "WARN"
        Invoke-Icacls -Path $Path -Arguments @("/inheritance:r", "/T", "/C", "/Q")
        Invoke-Icacls -Path $Path -Arguments @("/grant:r", ("*{0}:(OI)(CI)F" -f $SidAdministrators), "/T", "/C", "/Q")
        Invoke-Icacls -Path $Path -Arguments @("/grant:r", ("*{0}:(OI)(CI)F" -f $SidSystem), "/T", "/C", "/Q")
        Invoke-Icacls -Path $Path -Arguments @("/grant:r", ("*{0}:(OI)(CI)M" -f $Sid), "/T", "/C", "/Q")
        return
    }

    Invoke-Icacls -Path $Path -Arguments @("/grant", ("*{0}:(OI)(CI)M" -f $Sid))
}

function Test-PathAcl {
    param([string]$Kind, [string]$Path, [string]$Sid, [switch]$StrictBroadRights)

    $label = "{0,-8} {1}" -f $Kind, $Path
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Log ("{0} | chua ton tai" -f $label) "WARN"
        $script:problems++
        return
    }

    try {
        $acl = Get-Acl -LiteralPath $Path -ErrorAction Stop
    }
    catch {
        Write-Log ("{0} | khong doc duoc ACL: {1}" -f $label, $_.Exception.Message) "FAIL"
        $script:problems++
        return
    }

    $isPublish = ($Kind -eq "publish")
    $required = if ($isPublish) { $RightsReadExecute } else { $RightsModify }
    $mask = Get-IdentityMask -Acl $acl -Sid $Sid

    if (($mask.Rights -band $required) -eq $required) {
        Write-Log ("{0} | identity co {1}" -f $label, (Format-Rights $mask.Rights)) "OK"
    }
    else {
        Write-Log ("{0} | identity thieu {1} (ACE cua identity dang co: {2})" -f $label, (Format-Rights $required), (Format-Rights $mask.Rights)) "FAIL"
        $script:problems++
    }

    if ($mask.Rights -ne 0 -and -not $mask.Inherits) {
        Write-Log ("{0} | ACE cua identity khong ke thua xuong tep con; can co (OI)(CI)" -f $label) "FAIL"
        $script:problems++
    }

    if ($isPublish) {
        if (($mask.Rights -band $RightsWrite) -eq $RightsWrite) {
            Write-Log ("{0} | identity CO quyen ghi vao publish; app chi duoc doc va chay" -f $label) "FAIL"
            $script:problems++
        }
        # Nhom rong co quyen ghi = identity app (thanh vien nhom) ghi duoc vao publish.
        $broadWrite = @(Get-BroadRules -Acl $acl | Where-Object { ($_.Rights -band $RightsWrite) -eq $RightsWrite })
        foreach ($rule in $broadWrite) {
            $level = if ($StrictBroadRights) { "FAIL" } else { "WARN" }
            Write-Log ("{0} | nhom rong co quyen ghi: {1}={2}" -f $label, $rule.Name, (Format-Rights $rule.Rights)) $level
            if ($StrictBroadRights) { $script:problems++ } else { $script:warnings++ }
        }
        return
    }

    $broad = @(Get-BroadRules -Acl $acl)
    if ($broad.Count -gt 0) {
        $desc = ($broad | ForEach-Object { "{0}={1}" -f $_.Name, (Format-Rights $_.Rights) }) -join ", "
        $level = if ($StrictBroadRights) { "FAIL" } else { "WARN" }
        Write-Log ("{0} | con quyen rong: {1}" -f $label, $desc) $level
        if ($StrictBroadRights) { $script:problems++ } else { $script:warnings++ }
    }
}

# ---------------------------------------------------------------------------
# Bat dau
# ---------------------------------------------------------------------------

if (-not $LogDir) {
    if (Test-Path (Join-Path $script:repoRoot "NhaTro\Web\Web.csproj")) {
        $LogDir = Join-Path $script:repoRoot "outputs\iis-handoff"
    }
    else {
        $LogDir = $PSScriptRoot
    }
}
if (-not (Test-Path -LiteralPath $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}
$script:logFile = Join-Path $LogDir ("acl-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

Write-Log ("VHT-130 (2E) cap quyen thu muc IIS | Mode={0} | DryRun={1}" -f $Mode, $DryRun)
Write-Log ("May: {0} | Nguoi chay: {1} | Admin: {2}" -f $env:COMPUTERNAME, $env:USERNAME, (Test-IsAdmin))

if (-not $PublishPath) {
    Stop-With 2 "Thieu -PublishPath (thu muc release dang duoc site tro toi)."
}
if ($Mode -eq "Apply" -and -not (Test-IsAdmin) -and -not $DryRun) {
    Stop-With 2 "Mode Apply can PowerShell quan tri (Run as Administrator)."
}

$identityName = Resolve-AppIdentity
try {
    $identitySid = (New-Object System.Security.Principal.NTAccount($identityName)).Translate([System.Security.Principal.SecurityIdentifier]).Value
}
catch {
    $hint = if ($identityName -like "IIS APPPOOL\*") {
        "Tao app pool '{0}' (No Managed Code, Integrated, Load User Profile = true) truoc roi chay lai." -f $AppPoolName
    }
    else {
        "Kiem tra lai ten tai khoan hoac cap tai khoan cho may nay."
    }
    Stop-With 4 ("Khong phan giai duoc identity '{0}'. {1}" -f $identityName, $hint)
}

Write-Log ("Identity app: {0} (SID {1})" -f $identityName, $identitySid)
Write-Log ("Publish: {0} | Storage: {1} | Keys: {2} | Log: {3}" -f $PublishPath, $StoragePath, $KeysPath, $LogPath)

Test-Layout

$targets = @(
    @{ Kind = "publish"; Path = $PublishPath },
    @{ Kind = "storage"; Path = $StoragePath },
    @{ Kind = "keys"; Path = $KeysPath },
    @{ Kind = "log"; Path = $LogPath }
)

if ($Mode -eq "Check") {
    foreach ($t in $targets) {
        Test-PathAcl -Kind $t.Kind -Path $t.Path -Sid $identitySid -StrictBroadRights
    }
}
else {
    foreach ($t in $targets) {
        Grant-PathRights -Kind $t.Kind -Path $t.Path -Sid $identitySid
    }
    if ($DryRun) {
        Write-Log "Dry-run: khong doi ACL, bo qua buoc kiem lai." "WARN"
    }
    else {
        Write-Log "Kiem lai sau khi cap quyen:"
        foreach ($t in $targets) {
            Test-PathAcl -Kind $t.Kind -Path $t.Path -Sid $identitySid
        }
    }
}

Write-Log ("Log: {0}" -f $script:logFile)
if ($script:problems -gt 0) {
    Write-Log ("Ket luan: con {0} van de can xu ly." -f $script:problems) "FAIL"
    exit 3
}
if ($DryRun) {
    Write-Log "Ket luan: dry-run, chua thay doi ACL. Bo -DryRun de ap dung." "WARN"
    exit 0
}
if ($script:warnings -gt 0) {
    Write-Log ("Ket luan: dat yeu cau toi thieu, con {0} canh bao can ra soat." -f $script:warnings) "WARN"
    exit 0
}
Write-Log "Ket luan: quyen thu muc dat yeu cau cua ban giao 2E." "OK"
exit 0
