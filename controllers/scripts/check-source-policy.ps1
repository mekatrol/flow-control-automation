param([Parameter(Mandatory = $true)][string]$ControllersDirectory)

$ErrorActionPreference = 'Stop'
$pattern = '^\s*\(void\)\s*[A-Za-z_][A-Za-z0-9_]*\s*;'
$roots = @('main.c', 'shared', 'platforms', 'boards', 'tests')
$violations = @()

foreach ($root in $roots) {
    $path = Join-Path $ControllersDirectory $root
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }
    $files = if (Test-Path -LiteralPath $path -PathType Leaf) {
        Get-Item -LiteralPath $path
    }
    else {
        Get-ChildItem -LiteralPath $path -Recurse -File -Include '*.c', '*.h'
    }
    foreach ($file in $files) {
        $lineNumber = 0
        foreach ($line in Get-Content -LiteralPath $file.FullName) {
            $lineNumber++
            if ($line -match $pattern) {
                $rootPath = (Resolve-Path -LiteralPath $ControllersDirectory).Path.TrimEnd('\')
                $relativePath = $file.FullName.Substring($rootPath.Length + 1)
                $violations += "${relativePath}:${lineNumber}:$line"
            }
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | Write-Error
    throw "Replace each '(void)parameter;' statement with an unnamed C23 parameter such as 'void * /* context */'."
}
