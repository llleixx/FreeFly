param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Version,
    [string]$Author,
    [string]$PackageName,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\FreeFly\FreeFly.csproj'
$outputDir = Join-Path $projectRoot "src\FreeFly\bin\$Configuration"
$artifactDir = Join-Path $projectRoot 'artifacts'
$manifestPath = Join-Path $projectRoot 'manifest.json'

if ([string]::IsNullOrWhiteSpace($Version) -or
    [string]::IsNullOrWhiteSpace($Author) -or
    [string]::IsNullOrWhiteSpace($PackageName)) {
    $propertyOutput = (& dotnet msbuild $project -nologo `
        -getProperty:Version `
        -getProperty:ThunderstoreAuthor `
        -getProperty:ThunderstorePackageName) -join [Environment]::NewLine
    if ($LASTEXITCODE -ne 0) { throw 'Could not read package properties from FreeFly.csproj.' }

    $jsonStart = $propertyOutput.IndexOf('{')
    if ($jsonStart -lt 0) { throw 'MSBuild did not return package properties as JSON.' }
    $properties = ($propertyOutput.Substring($jsonStart) | ConvertFrom-Json).Properties

    if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $properties.Version }
    if ([string]::IsNullOrWhiteSpace($Author)) { $Author = $properties.ThunderstoreAuthor }
    if ([string]::IsNullOrWhiteSpace($PackageName)) { $PackageName = $properties.ThunderstorePackageName }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must contain three numeric components, got '$Version'."
}
foreach ($packagePart in @($Author, $PackageName)) {
    if ($packagePart -notmatch '^[A-Za-z0-9_]+$') {
        throw "Thunderstore author and package name may contain only letters, numbers, and underscores, got '$packagePart'."
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.name -cne $PackageName) {
    throw "manifest.json name '$($manifest.name)' does not match MSBuild package name '$PackageName'."
}
if ($manifest.version_number -cne $Version) {
    throw "manifest.json version '$($manifest.version_number)' does not match MSBuild version '$Version'."
}
if ([string]::IsNullOrWhiteSpace($manifest.description) -or $manifest.description.Length -gt 250) {
    throw 'manifest.json description must contain between 1 and 250 characters.'
}
if ($manifest.website_url -notmatch '^https://') {
    throw 'manifest.json website_url must be an HTTPS URL.'
}
if ($null -eq $manifest.dependencies -or $manifest.dependencies.Count -eq 0) {
    throw 'manifest.json must declare at least one dependency.'
}
foreach ($dependency in $manifest.dependencies) {
    if ($dependency -notmatch '^[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+$') {
        throw "Invalid Thunderstore dependency string '$dependency'."
    }
}

$packageId = "$Author-$PackageName"
$stageDir = Join-Path $artifactDir "staging\$packageId"
$pluginDir = Join-Path $stageDir 'plugins'
$archive = Join-Path $artifactDir "$packageId-$Version.zip"

if (-not $SkipBuild) {
    & dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'FreeFly build failed.' }
}

$pluginAssembly = Join-Path $outputDir 'FreeFly.dll'
if (-not (Test-Path -LiteralPath $pluginAssembly)) {
    throw "Plugin DLL was not found at $pluginAssembly."
}

$expectedAssemblyVersion = [Version]::Parse("$Version.0")
$actualAssemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($pluginAssembly).Version
if ($actualAssemblyVersion -ne $expectedAssemblyVersion) {
    throw "DLL assembly version '$actualAssemblyVersion' does not match package version '$Version'."
}

$productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($pluginAssembly).ProductVersion
$semanticProductVersion = ($productVersion -split '\+', 2)[0]
if ($semanticProductVersion -cne $Version) {
    throw "DLL product version '$productVersion' does not match package version '$Version'."
}

$icon = Join-Path $projectRoot 'icon.png'
if (-not (Test-Path -LiteralPath $icon)) {
    & (Join-Path $PSScriptRoot 'Generate-Icon.ps1') -OutputPath $icon
}

$image = [System.Drawing.Image]::FromFile($icon)
try {
    if ($image.Width -ne 256 -or $image.Height -ne 256) {
        throw "icon.png must be 256x256, got $($image.Width)x$($image.Height)."
    }
}
finally {
    $image.Dispose()
}

$resolvedStage = [System.IO.Path]::GetFullPath($stageDir)
$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactDir)
if (-not $resolvedStage.StartsWith($resolvedArtifacts + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean staging path outside artifacts: $resolvedStage"
}

if (Test-Path -LiteralPath $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

foreach ($file in @('manifest.json', 'icon.png', 'README.md', 'CHANGELOG.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $stageDir
}
Copy-Item -LiteralPath $pluginAssembly -Destination $pluginDir

if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $archive -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
try {
    $actual = @($zip.Entries |
        Where-Object { -not $_.FullName.EndsWith('/') } |
        ForEach-Object { $_.FullName.Replace('\', '/') } |
        Sort-Object)
    $expected = @(
        'plugins/FreeFly.dll',
        'CHANGELOG.md',
        'LICENSE',
        'README.md',
        'icon.png',
        'manifest.json'
    ) | Sort-Object
    if (Compare-Object $expected $actual) {
        throw "Unexpected archive entries: $($actual -join ', ')"
    }
}
finally {
    $zip.Dispose()
}

Remove-Item -LiteralPath $stageDir -Recurse -Force
$stagingRoot = Split-Path -Parent $stageDir
if ((Test-Path -LiteralPath $stagingRoot) -and -not (Get-ChildItem -LiteralPath $stagingRoot -Force)) {
    Remove-Item -LiteralPath $stagingRoot -Force
}
Write-Output "Created $archive"
