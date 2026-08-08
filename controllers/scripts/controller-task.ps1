param(
    [Parameter(Mandatory = $true)][string]$ControllersDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('set-board', 'format', 'clean', 'build', 'flash', 'monitor')][string]$Action,
    [string]$Argument = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$selectionFile = Join-Path $ControllersDirectory '.controller-board'
$board = 'kincony-kc868-a16'
if (Test-Path -LiteralPath $selectionFile) {
    $board = (Get-Content -LiteralPath $selectionFile -Raw).Trim()
}

# Gets the target and build directory for one supported controller board.
function Get-BoardConfiguration {
    param([Parameter(Mandatory = $true)][string]$Board)

    if ($Board -ne 'kincony-kc868-a16') {
        throw "Unknown controller board: $Board"
    }
    return @{ Target = 'esp32s3'; BuildDirectory = "build-$Board" }
}

# Gets the selected EIM installation, preferring the environment already activated by VS Code.
function Get-EspIdfInstallation {
    if ($env:IDF_PATH) {
        $pythonPath = $null
        if ($env:IDF_PYTHON_ENV_PATH) {
            $candidate = Join-Path $env:IDF_PYTHON_ENV_PATH 'Scripts\python.exe'
            if (Test-Path -LiteralPath $candidate) {
                $pythonPath = $candidate
            }
        }
        if (-not $pythonPath) {
            $python = Get-Command python.exe -ErrorAction SilentlyContinue
            if ($python) {
                $pythonPath = $python.Source
            }
        }
        if (-not $pythonPath) {
            $python = Get-Command python -ErrorAction SilentlyContinue
            if ($python) {
                $pythonPath = $python.Source
            }
        }
        if ($pythonPath -and (Test-Path -LiteralPath (Join-Path $env:IDF_PATH 'tools\idf.py'))) {
            return @{ Path = $env:IDF_PATH; Python = $pythonPath; ActivationScript = $null }
        }
    }

    $manifests = @(
        'C:\Espressif\tools\eim_idf.json',
        (Join-Path $env:USERPROFILE '.espressif\tools\eim_idf.json')
    )
    foreach ($manifestPath in $manifests) {
        if (-not (Test-Path -LiteralPath $manifestPath)) {
            continue
        }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $installation = $manifest.idfInstalled | Where-Object { $_.id -eq $manifest.idfSelectedId } | Select-Object -First 1
        if (-not $installation) {
            $installation = $manifest.idfInstalled | Select-Object -First 1
        }
        if ($installation -and (Test-Path -LiteralPath $installation.python) -and
            (Test-Path -LiteralPath (Join-Path $installation.path 'tools\idf.py'))) {
            return @{ Path = $installation.path; Python = $installation.python; ActivationScript = $installation.activationScript }
        }
    }
    throw 'ESP-IDF was not found. Install it with EIM, then select it with ESP-IDF: Select Current ESP-IDF Version.'
}

# Activates the selected Windows toolchain and invokes idf.py in the board-specific build directory.
function Invoke-Idf {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $installation = Get-EspIdfInstallation
    if ($installation.ActivationScript -and (Test-Path -LiteralPath $installation.ActivationScript)) {
        . $installation.ActivationScript
    }
    $env:IDF_PATH = $installation.Path
    $env:CONTROLLER_BOARD = $board
    $idfPy = Join-Path $installation.Path 'tools\idf.py'
    & $installation.Python $idfPy '-B' $configuration.BuildDirectory @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "idf.py failed with exit code $LASTEXITCODE"
    }
}

# Restores device-local controller settings after set-target regenerates sdkconfig.
function Restore-ControllerConfiguration {
    param([Parameter(Mandatory = $true)][hashtable]$SavedValues)

    $sdkconfigPath = Join-Path $ControllersDirectory 'sdkconfig'
    if ($SavedValues.Count -eq 0 -or -not (Test-Path -LiteralPath $sdkconfigPath)) {
        return
    }
    $lines = Get-Content -LiteralPath $sdkconfigPath
    $updated = foreach ($line in $lines) {
        if ($line -match '^(CONFIG_CONTROLLER_[^=]+)=') {
            $key = $Matches[1]
            if ($SavedValues.ContainsKey($key)) {
                $SavedValues[$key]
                continue
            }
        }
        $line
    }
    Set-Content -LiteralPath $sdkconfigPath -Value $updated -Encoding ascii
}

# Gets an explicit Windows COM port or uniquely detects the attached USB serial controller.
function Get-WindowsSerialPort {
    param([string]$RequestedPort)

    if ($RequestedPort -match '^/dev/') {
        throw "'$RequestedPort' is a Linux device path. Enter a Windows port such as COM5, or leave the prompt blank to auto-detect it."
    }
    if ($RequestedPort) {
        if ($RequestedPort -notmatch '^COM[0-9]+$') {
            throw "Invalid Windows serial port '$RequestedPort'. Expected a value such as COM5."
        }
        return $RequestedPort
    }
    $ports = @(Get-CimInstance Win32_SerialPort -ErrorAction SilentlyContinue | Select-Object -ExpandProperty DeviceID)
    if ($ports.Count -eq 1) {
        Write-Host "Auto-detected controller serial port $($ports[0])."
        return $ports[0]
    }
    if ($ports.Count -eq 0) {
        throw 'No Windows serial port was detected. Connect and power the controller, install its USB driver, then retry.'
    }
    throw "Multiple serial ports were detected ($($ports -join ', ')). Run the task again and enter the controller COM port."
}

$configuration = Get-BoardConfiguration -Board $board
Push-Location $ControllersDirectory
try {
    switch ($Action) {
        'set-board' {
            $savedValues = @{}
            if (Test-Path -LiteralPath 'sdkconfig') {
                foreach ($line in Get-Content -LiteralPath 'sdkconfig') {
                    if ($line -match '^(CONFIG_CONTROLLER_[^=]+)=') {
                        $savedValues[$Matches[1]] = $line
                    }
                }
            }
            $board = $Argument
            $configuration = Get-BoardConfiguration -Board $board
            Set-Content -LiteralPath $selectionFile -Value $board -Encoding ascii
            Invoke-Idf -Arguments @('set-target', $configuration.Target)
            Restore-ControllerConfiguration -SavedValues $savedValues
            Invoke-Idf -Arguments @('reconfigure')
            Write-Host "Selected $board; subsequent tasks use $($configuration.BuildDirectory)."
        }
        'format' {
            $installation = Get-EspIdfInstallation
            if ($installation.ActivationScript -and (Test-Path -LiteralPath $installation.ActivationScript)) {
                . $installation.ActivationScript
            }
            $formatter = Get-Command clang-format.exe -ErrorAction SilentlyContinue
            if (-not $formatter) {
                throw 'clang-format was not found in the selected ESP-IDF toolchain.'
            }
            $sources = & git ls-files --cached --others --exclude-standard -- '*.c' '*.h'
            foreach ($source in $sources) {
                if (Test-Path -LiteralPath $source) {
                    & $formatter.Source '-i' $source
                }
            }
        }
        'clean' { Invoke-Idf -Arguments @('fullclean') }
        'build' { Invoke-Idf -Arguments @('build') }
        'flash' { Invoke-Idf -Arguments @('-p', (Get-WindowsSerialPort -RequestedPort $Argument), 'flash') }
        'monitor' { Invoke-Idf -Arguments @('-p', (Get-WindowsSerialPort -RequestedPort $Argument), 'monitor') }
    }
}
finally {
    Pop-Location
}
