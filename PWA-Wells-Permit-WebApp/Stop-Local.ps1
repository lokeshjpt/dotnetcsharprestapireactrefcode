<#
.SYNOPSIS
  Stop the 3 locally-running PWA Permit apps started by Start-Local.ps1 (no IIS).
  Stops whatever is listening on:
    3000  ecomm SPA        3003  intra SPA
    5242  API (http)       7242  API (https)

.PARAMETER Ports
  Override the port list (default 3000,3003,5242,7242).

.EXAMPLE
  .\Stop-Local.ps1
.EXAMPLE
  .\Stop-Local.ps1 -Ports 3000,3003        # stop only the SPAs, leave the API up
#>
[CmdletBinding()]
param(
    [int[]]$Ports = @(3000, 3003, 5242, 7242)
)
$ErrorActionPreference = "Stop"

$stopped = @{}
foreach ($port in ($Ports | Sort-Object -Unique)) {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if (-not $conns) { Write-Host ("  port {0,-5} : free" -f $port) -ForegroundColor DarkGray; continue }

    foreach ($c in $conns) {
        $procId = $c.OwningProcess
        if ($stopped.ContainsKey($procId)) { continue }
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if (-not $proc -or $proc.ProcessName -in @('System','Idle')) { continue }
        Write-Host ("  port {0,-5} : stopping {1} (PID {2})" -f $port, $proc.ProcessName, $procId) -ForegroundColor Yellow
        try {
            Stop-Process -Id $procId -Force -ErrorAction Stop
            $stopped[$procId] = "$($proc.ProcessName)"
        } catch {
            Write-Warning ("  could not stop PID {0}: {1}" -f $procId, $_.Exception.Message)
        }
    }
}

Start-Sleep -Seconds 2

# --- verify the ports are actually free now -------------------------------------
$still = Get-NetTCPConnection -State Listen -LocalPort ($Ports | Sort-Object -Unique) -ErrorAction SilentlyContinue
Write-Host ""
if ($still) {
    Write-Warning "Still listening:"
    $still | ForEach-Object {
        $p = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
        Write-Host ("  port {0} -> PID {1} {2}" -f $_.LocalPort, $_.OwningProcess, $p.ProcessName) -ForegroundColor Red
    }
} else {
    Write-Host ("All target ports free ({0}). {1} process(es) stopped." -f ($Ports -join ', '), $stopped.Count) -ForegroundColor Green
}
