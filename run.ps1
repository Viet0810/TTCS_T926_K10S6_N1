$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Mo chuong trinh tai http://localhost:5024 sau khi backend khoi dong.'
try {
    $health = Invoke-RestMethod 'http://localhost:5024/api/database/status' -TimeoutSec 3
    if ($health.connected) {
        Write-Host 'Now listening on: http://localhost:5024 (backend already running)'
        exit 0
    }
} catch {
    # Start the backend when the existing API is unavailable.
}
dotnet run --project Backend/InternManagement --launch-profile http
exit $LASTEXITCODE
