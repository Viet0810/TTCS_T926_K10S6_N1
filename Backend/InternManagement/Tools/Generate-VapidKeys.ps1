param(
    [string]$Subject = "mailto:admin@example.com"
)

$ErrorActionPreference = "Stop"
$configPath = Join-Path (Split-Path -Parent $PSScriptRoot) "appsettings.Push.json"
if (Test-Path -LiteralPath $configPath) {
    throw "appsettings.Push.json đã tồn tại. Sao lưu hoặc xóa file đó trước khi tạo cặp khóa mới."
}

$curve = [System.Security.Cryptography.ECCurve]::CreateFromFriendlyName("nistP256")
$key = [System.Security.Cryptography.ECDsa]::Create($curve)
try {
    $parameters = $key.ExportParameters($true)
    $publicBytes = [byte[]]::new(65)
    $publicBytes[0] = 4
    [Array]::Copy($parameters.Q.X, 0, $publicBytes, 1, 32)
    [Array]::Copy($parameters.Q.Y, 0, $publicBytes, 33, 32)
    $toBase64Url = { param([byte[]]$bytes) [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_") }
    $config = @{
        WebPush = @{
            Subject = $Subject
            PublicKey = (& $toBase64Url $publicBytes)
            PrivateKey = (& $toBase64Url $parameters.D)
        }
    } | ConvertTo-Json -Depth 3
    [System.IO.File]::WriteAllText($configPath, $config + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Đã tạo cấu hình Web Push tại $configPath. File này đã được loại khỏi Git; không chia sẻ PrivateKey."
}
finally {
    if ($null -ne $key) { $key.Dispose() }
}
