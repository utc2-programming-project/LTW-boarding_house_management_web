# Cập nhật dữ liệu địa chỉ hành chính Việt Nam dùng cho ô nhập địa chỉ.
# Nguồn: provinces.open-api.vn (API v2, dữ liệu 2 cấp sau sáp nhập).
# Dữ liệu được tải một lần và lưu trong NhaTro/Web/wwwroot/data để ứng dụng
# không phụ thuộc API bên ngoài khi người dùng đang nhập địa chỉ.
# Chạy lại vài tháng một lần khi nhà nước công bố thay đổi đơn vị hành chính:
#   powershell -File scripts/data/cap_nhat_dia_chi_hanh_chinh.ps1
# Sau khi chạy, kiểm tra diff của vn-address.json/vn-address.meta.json và chạy
# các bài kiểm thử địa chỉ trước khi commit.
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\NhaTro\Web\wwwroot\data\vn-address.json')
)

$ErrorActionPreference = 'Stop'

$ProvinceApi = 'https://provinces.open-api.vn/api/v2/p/'
$WardApi = 'https://provinces.open-api.vn/api/v2/w/'

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
$provinces = Get-Utf8Json $ProvinceApi

Write-Host 'Đang tải danh sách phường/xã...'
$wards = Get-Utf8Json $WardApi

if (@($provinces).Count -eq 0) {
    throw "API $ProvinceApi không trả về tỉnh/thành phố nào; giữ nguyên dữ liệu cũ."
}

if (@($wards).Count -eq 0) {
    throw "API $WardApi không trả về phường/xã nào; giữ nguyên dữ liệu cũ."
}

$provinceCodes = @{}
foreach ($province in $provinces) {
    $provinceCodes[$province.code] = $true
}

$wardsByProvince = @{}
$orphanedWards = 0
foreach ($ward in $wards) {
    if (-not $provinceCodes.ContainsKey($ward.province_code)) {
        $orphanedWards++
        continue
    }

    if (-not $wardsByProvince.ContainsKey($ward.province_code)) {
        $wardsByProvince[$ward.province_code] = New-Object System.Collections.ArrayList
    }

    [void]$wardsByProvince[$ward.province_code].Add([pscustomobject]@{
        code = $ward.code
        name = $ward.name
    })
}

if ($orphanedWards -gt 0) {
    Write-Warning "$orphanedWards phường/xã có province_code không nằm trong danh sách tỉnh/thành phố và đã bị bỏ qua."
}

$data = @(foreach ($province in $provinces) {
    [pscustomobject]@{
        code  = $province.code
        name  = $province.name
        wards = @($wardsByProvince[$province.code] | Sort-Object name)
    }
})

$wardCount = 0
foreach ($province in $data) {
    $wardCount += $province.wards.Count
}

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path -Parent $fullPath
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$json = $data | ConvertTo-Json -Depth 6
$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($fullPath, $json, $utf8)

# Ghi kèm ảnh chụp nguồn dữ liệu để biết lần cập nhật gần nhất và nhắc lịch làm mới.
$metadataPath = [System.IO.Path]::ChangeExtension($fullPath, '.meta.json')
$metadata = [pscustomobject]@{
    source              = 'provinces.open-api.vn'
    apiVersion          = 'v2'
    provinceEndpoint    = $ProvinceApi
    wardEndpoint        = $WardApi
    downloadedAtUtc     = [DateTime]::UtcNow.ToString('o')
    provinceCount       = $data.Count
    wardCount           = $wardCount
    administrativeModel = '2 cấp: tỉnh/thành phố + phường/xã/đặc khu (sau sáp nhập 2025)'
    refreshCadence      = 'Chạy lại 3 tháng/lần hoặc ngay sau khi có nghị quyết thay đổi đơn vị hành chính.'
    notes               = @(
        'Ứng dụng chỉ đọc tệp JSON này lúc chạy, không gọi API bên ngoài.',
        'Thông tin Đồng Nai trở thành thành phố trực thuộc trung ương từ 30/4/2026 cần đối chiếu văn bản chính thức (chưa kiểm chứng trong mã nguồn).'
    )
}

[System.IO.File]::WriteAllText(
    $metadataPath,
    ($metadata | ConvertTo-Json -Depth 4),
    $utf8)

Write-Host "Đã ghi $($data.Count) tỉnh/thành phố và $wardCount phường/xã vào $fullPath"
Write-Host "Đã ghi thông tin nguồn dữ liệu vào $metadataPath"
