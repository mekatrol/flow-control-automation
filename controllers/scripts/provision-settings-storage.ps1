param(
    [Parameter(Mandatory = $true)][string]$ControllersDirectory,
    [ValidateRange(1, 2147483647)][int]$FirstReservedSector = 2048
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$controllersPath = (Resolve-Path -LiteralPath $ControllersDirectory).Path
$sdkconfigPath = Join-Path $controllersPath 'sdkconfig'
if (-not (Test-Path -LiteralPath $sdkconfigPath -PathType Leaf)) {
    throw 'sdkconfig was not found; run the Set board task before provisioning settings storage.'
}

$sdkconfigContent = Get-Content -LiteralPath $sdkconfigPath
$sectorPattern = '^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=.*$'
$keyPattern = '^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=.*$'
if (-not ($sdkconfigContent -match $sectorPattern) -or -not ($sdkconfigContent -match $keyPattern)) {
    throw 'sdkconfig does not contain the controller settings-storage options; reconfigure the project first.'
}

$keyBytes = New-Object byte[] 32
$generator = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $generator.GetBytes($keyBytes)
    $settingsKeyHex = -join ($keyBytes | ForEach-Object { $_.ToString('x2') })
    $updatedContent = $sdkconfigContent -replace $sectorPattern,
        "CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=$FirstReservedSector"
    $updatedContent = $updatedContent -replace $keyPattern,
        "CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=`"$settingsKeyHex`""

    $temporaryPath = "$sdkconfigPath.provision.$([Guid]::NewGuid().ToString('N'))"
    try {
        Set-Content -LiteralPath $temporaryPath -Value $updatedContent -Encoding ascii
        Move-Item -LiteralPath $temporaryPath -Destination $sdkconfigPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}
finally {
    $generator.Dispose()
    if ($keyBytes) {
        [Array]::Clear($keyBytes, 0, $keyBytes.Length)
    }
    Remove-Variable settingsKeyHex -ErrorAction SilentlyContinue
}

$configuredContent = Get-Content -LiteralPath $sdkconfigPath
$sectorLine = $configuredContent | Where-Object { $_ -match '^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=' }
$keyLine = $configuredContent | Where-Object { $_ -match '^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=' }
$configuredSector = ($sectorLine -split '=', 2)[1]
$configuredKeyLength = (($keyLine -split '=', 2)[1].Trim('"')).Length
if ($configuredSector -ne $FirstReservedSector.ToString() -or $configuredKeyLength -ne 64) {
    throw 'Settings-storage provisioning verification failed.'
}

Write-Host "Provisioned settings storage in sdkconfig: reserved_sector=$configuredSector master_key_hex_length=$configuredKeyLength"
Write-Host 'Build and flash this configuration before initializing the reserved sectors on the controller.'
