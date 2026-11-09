# Cập nhật dữ liệu địa chỉ hành chính Việt Nam dùng cho ô nhập địa chỉ.
# Nguồn: provinces.open-api.vn (API v2, dữ liệu sau sáp nhập).
# Chạy lại vài tháng một lần khi nhà nước công bố thay đổi đơn vị hành chính:
#   pwsh scripts/data/cap_nhat_dia_chi_hanh_chinh.ps1
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\NhaTro\Web\wwwroot\data\vn-address.json')
)

$ErrorActionPreference = 'Stop'

function Get-Utf8Json([string]$Url) {
    $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 120
    $stream = New-Object System.IO.MemoryStream
    try {
        $response.RawContentStream.CopyTo($stream)
        $text = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
        return $text | ConvertFrom-Json
    }
    finally {
        $stream.Dispose()
    }
}

Write-Host 'Đang tải danh sách tỉnh/thành phố...'
$provinces = Get-Utf8Json 'https://provinces.open-api.vn/api/v2/p/'

Write-Host 'Đang tải danh sách phường/xã...'
$wards = Get-Utf8Json 'https://provinces.open-api.vn/api/v2/w/'

$wardsByProvince = @{}
foreach ($ward in $wards) {
    if (-not $wardsByProvince.ContainsKey($ward.province_code)) {
        $wardsByProvince[$ward.province_code] = New-Object System.Collections.ArrayList
    }

    [void]$wardsByProvince[$ward.province_code].Add([pscustomobject]@{
        code = $ward.code
        name = $ward.name
    })
}

$data = foreach ($province in $provinces) {
    [pscustomobject]@{
        code  = $province.code
        name  = $province.name
        wards = @($wardsByProvince[$province.code] | Sort-Object name)
    }
}

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path -Parent $fullPath
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$json = $data | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($fullPath, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host "Đã ghi $($data.Count) tỉnh/thành phố và $($wards.Count) phường/xã vào $fullPath"
