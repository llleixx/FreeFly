param(
    [string]$InputDirectory = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\media')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$screenshots = @(
    @{ Input = '20260821023413_1.jpg'; Output = 'peak-teleport.jpg' },
    @{ Input = '20260821023457_1.jpg'; Output = 'nadir-teleport.jpg' }
)

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$jpegCodec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
    Where-Object MimeType -eq 'image/jpeg'
$encoderParameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
$encoderParameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new(
    [System.Drawing.Imaging.Encoder]::Quality,
    [long]90
)

foreach ($screenshot in $screenshots) {
    $inputPath = Join-Path $InputDirectory $screenshot.Input
    $outputPath = Join-Path $OutputDirectory $screenshot.Output
    if (-not (Test-Path -LiteralPath $inputPath)) {
        throw "Screenshot was not found: $inputPath"
    }

    $source = [System.Drawing.Bitmap]::FromFile($inputPath)
    $target = [System.Drawing.Bitmap]::new(1280, 800)
    $graphics = [System.Drawing.Graphics]::FromImage($target)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, 1280, 800))
        $target.Save($outputPath, $jpegCodec, $encoderParameters)
    }
    finally {
        $graphics.Dispose()
        $target.Dispose()
        $source.Dispose()
    }

    Write-Output "Processed $inputPath -> $outputPath (1280x800 JPEG)."
}

$encoderParameters.Dispose()
