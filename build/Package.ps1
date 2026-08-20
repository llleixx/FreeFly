param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$Author = "lllei",
    [string]$PackageName = "FreeFly",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\FreeFly\FreeFly.csproj"
$manifestPath = Join-Path $root "manifest.json"
$outputDir = Join-Path $root "artifacts"
$stage = Join-Path $outputDir "$Author-$PackageName-$Version"
$iconPath = Join-Path $root "icon.png"

if (-not $SkipBuild) {
    dotnet build $project -c $Configuration
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version_number -ne $Version) {
    throw "Manifest version $($manifest.version_number) does not match requested version $Version."
}

$dll = Join-Path $root "src\FreeFly\bin\$Configuration\FreeFly.dll"
if (-not (Test-Path $dll)) {
    throw "Compiled DLL was not found: $dll"
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item $dll (Join-Path $stage "FreeFly.dll") -Force
Copy-Item $manifestPath (Join-Path $stage "manifest.json") -Force
Copy-Item (Join-Path $root "README.md") (Join-Path $stage "README.md") -Force
if (-not (Test-Path -LiteralPath $iconPath)) {
    & (Join-Path $PSScriptRoot "Generate-Icon.ps1") -OutputPath $iconPath
}
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile($iconPath)
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256) {
        throw "icon.png must be 256x256, got $($icon.Width)x$($icon.Height)."
    }
}
finally {
    $icon.Dispose()
}
Copy-Item $iconPath (Join-Path $stage "icon.png") -Force
$zip = Join-Path $outputDir "$Author-$PackageName-$Version.zip"
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
Write-Host "Created $zip"
