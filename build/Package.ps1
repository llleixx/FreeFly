param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
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
$zip = Join-Path $outputDir "$Author-$PackageName-$Version.zip"
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
Write-Host "Created $zip"
