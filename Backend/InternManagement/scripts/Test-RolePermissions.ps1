param(
    [string]$ApiBaseUrl = "http://192.168.1.107:5024/api"
)

$ErrorActionPreference = "Stop"
$expected = @{
    ADMIN  = @("users.manage", "interns.manage")
    HR     = @("interns.manage")
    MENTOR = @("interns.assigned.read", "progress.review")
    INTERN = @("profile.own.read", "progress.own.read", "tasks.own.read")
}

function Get-HttpStatusCode {
    param([scriptblock]$Request)
    try {
        & $Request | Out-Null
        return 200
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        return [int]$response.StatusCode
    }
}

$anonymousStatus = Get-HttpStatusCode {
    Invoke-RestMethod -Uri "$ApiBaseUrl/auth/permissions" -Method Get
}
if ($anonymousStatus -ne 401) {
    throw "Unauthenticated permissions request should return 401, got $anonymousStatus."
}
Write-Host "PASS: request without a token is rejected (401)" -ForegroundColor Green

foreach ($role in @("ADMIN", "HR", "MENTOR", "INTERN")) {
    $credential = Get-Credential -Message "Nhập tài khoản thật có vai trò $role để kiểm tra quyền"
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($credential.Password)
    try {
        $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        $loginBody = @{
            username = $credential.UserName
            password = $password
        } | ConvertTo-Json
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
        $password = $null
    }

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/auth/login" -Method Post `
        -ContentType "application/json" -Body $loginBody
    if ($login.user.role.ToUpperInvariant() -ne $role) {
        throw "Expected a $role account, but the server returned role '$($login.user.role)'."
    }

    $headers = @{ Authorization = "Bearer $($login.token)" }
    $permissions = Invoke-RestMethod -Uri "$ApiBaseUrl/auth/permissions" -Method Get -Headers $headers
    $actual = @($permissions.permissions | Sort-Object)
    $wanted = @($expected[$role] | Sort-Object)
    if (($actual -join "|") -ne ($wanted -join "|")) {
        throw "Wrong permissions for $role. Expected [$($wanted -join ', ')], got [$($actual -join ', ')]."
    }

    $usersStatus = Get-HttpStatusCode {
        Invoke-RestMethod -Uri "$ApiBaseUrl/users" -Method Get -Headers $headers
    }
    $expectedUsersStatus = if ($role -eq "ADMIN") { 200 } else { 403 }
    if ($usersStatus -ne $expectedUsersStatus) {
        throw "$role /api/users should return $expectedUsersStatus, got $usersStatus."
    }

    Write-Host "PASS: $role permissions and /api/users access ($usersStatus)" -ForegroundColor Green
}

Write-Host "All four role checks passed." -ForegroundColor Green
