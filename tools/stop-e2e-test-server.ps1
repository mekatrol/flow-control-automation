param(
    [ValidateRange(1, 65535)]
    [int]$Port = 5018
)

$connections = @(
    Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
)

if ($connections.Count -eq 0) {
    Write-Host "No process is listening on port $Port."
    exit 0
}

$connections | Select-Object LocalAddress, LocalPort, OwningProcess

$processIds = @($connections.OwningProcess | Sort-Object -Unique)
foreach ($processId in $processIds) {
    Get-CimInstance Win32_Process -Filter "ProcessId = $processId" |
        Select-Object ProcessId, ParentProcessId, Name, CommandLine
}

Stop-Process -Id $processIds -ErrorAction Stop
Write-Host "Stopped the process listening on port $Port."
