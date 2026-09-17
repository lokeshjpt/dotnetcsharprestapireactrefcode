<#
.SYNOPSIS
  Start all 3 PWA Permit apps LOCALLY (no IIS), each in its own window:

    API    -> https://localhost:7242  (+ http://localhost:5242)   dotnet run, Development
    ecomm  -> http://localhost:3000                               npm start (local profile)
    intra  -> http://localhost:3003                               npm start (local profile)

  Swagger: https://localhost:7242/swagger   (VPN required for SQL Server / SMTP)

.PARAMETER Offline
  Run fully offline — mock FTP / ICAP / email / IntelliPay (no real integrations).

.PARAMETER NoApi
  Start only the two SPAs (API already running).

.NOTES
  *** LOCAL DEV ONLY — this file contains dev credentials. Do NOT commit it. ***
  (It lives in build\permit\, which is outside all three git repos.)

.EXAMPLE
  .\Start-Local.ps1
.EXAMPLE
  .\Start-Local.ps1 -Offline
#>
[CmdletBinding()]
param(
    [switch]$Offline,
    [switch]$NoApi
)
$ErrorActionPreference = "Stop"

$ApiSrc = ".\pwa-wells-permit-api\src\PWA.PermitsApi.WebApi"
$Ecomm  = ".\pwa-wells-permit-ecomm-web"
$Intra  = ".\pwa-wells-permit-intra-web"

foreach ($d in @($ApiSrc, $Ecomm, $Intra)) {
    if (-not (Test-Path $d)) { throw "Path not found: $d" }
}

# --- free the ports from any prior instance -------------------------------------
$ports = @(3000, 3003)
if (-not $NoApi) { $ports += 5242, 7242 }
foreach ($port in ($ports | Sort-Object -Unique)) {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | ForEach-Object {
        $proc = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
        if ($proc -and $proc.ProcessName -notin @('System','Idle')) {
            Write-Host "   freeing port $port (stopping $($proc.ProcessName) PID $($proc.Id))" -ForegroundColor DarkYellow
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
Start-Sleep -Seconds 2

# --- shared env (child windows inherit this process's environment) --------------
# NOTE: single-quoted so the '$u' in the password is NOT treated as a variable.
$env:PWA_DB_CONNECTION_STRING = 'Server=US01DDB011V,1433;Database=eedpwa;User Id=;Password=;Encrypt=false;TrustServerCertificate=true;'
$env:ConnectionStrings__PWAWellPermitsDBConnection = 'Server=US01DDB011V,1433;Database=eedpwa;User Id=;Password=;Encrypt=false;TrustServerCertificate=true;'
$env:Ftp__Password            = ''
$env:ASPNETCORE_URLS          = 'https://localhost:7242;http://localhost:5242'
$env:ASPNETCORE_ENVIRONMENT   = 'Development'
$env:REACT_APP_ENV            = 'local'
$env:REACT_APP_MAP_API_KEY    = ''
$env:REACT_APP_RECAPTCHA_SITE_KEY = ''  # public site key; ecomm npm start inherits it
$env:Captcha__SecretKey      = ""  # env var overrides the dotnet user-secret

if ($Offline) {
    $env:Ftp__UseMock        = 'true'
    $env:Icap__UseMock       = 'true'
    $env:Email__UseMock      = 'true'
    $env:IntelliPay__UseMock = 'true'
    Write-Host "Offline mode: FTP/ICAP/Email/IntelliPay mocked." -ForegroundColor Yellow
}

function Start-Win {
    param([string]$Title, [string]$Cmd)
    Start-Process powershell -ArgumentList @(
        '-NoExit', '-ExecutionPolicy', 'Bypass',
        '-Command', "`$host.UI.RawUI.WindowTitle = '$Title'; $Cmd"
    ) | Out-Null
}

if (-not $NoApi) {
    Write-Host "==> API   -> https://localhost:7242 (dotnet run)" -ForegroundColor Cyan
    Start-Win -Title "PWA API (7242)"  -Cmd "Set-Location '$ApiSrc'; dotnet run --no-launch-profile"
}
Write-Host "==> ecomm -> http://localhost:3000 (npm start)" -ForegroundColor Cyan
Start-Win -Title "PWA ecomm (3000)" -Cmd "Set-Location '$Ecomm'; npm start"

Write-Host "==> intra -> http://localhost:3003 (npm start)" -ForegroundColor Cyan
Start-Win -Title "PWA intra (3003)" -Cmd "Set-Location '$Intra'; npm start"

Write-Host ""
Write-Host "Launched. Give the SPAs ~30-60s to compile, then open:" -ForegroundColor Green
if (-not $NoApi) { Write-Host "  API    https://localhost:7242/swagger" }
Write-Host "  ecomm  http://localhost:3000"
Write-Host "  intra  http://localhost:3003"
Write-Host "Close a window (or Ctrl+C in it) to stop that app."
