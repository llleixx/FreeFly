param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'icon.png'),
    [string]$SourcePath = (Join-Path $PSScriptRoot 'FreeFly-Source.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Icon source was not found at '$SourcePath'."
}

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $SourcePath))
$bitmap = New-Object System.Drawing.Bitmap 256, 256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
# Scale the complete source image to the 256x256 canvas before placing the title text over it.
$artRect = [System.Drawing.Rectangle]::new(0, 0, 256, 256)
$graphics.DrawImage($source, $artRect)

$fontFamily = New-Object System.Drawing.FontFamily 'Arial Black'
$textFormat = New-Object System.Drawing.StringFormat
$textFormat.Alignment = [System.Drawing.StringAlignment]::Near
$textFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
$textOutlinePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(18, 23, 25)), 3.5
$textOutlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(246, 193, 77))

function Draw-SpacedText {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Text,
        [System.Drawing.FontFamily]$FontFamily,
        [float]$FontSize,
        [float]$Top,
        [float]$Height,
        [float]$Spacing,
        [System.Drawing.StringFormat]$Format,
        [System.Drawing.Pen]$OutlinePen,
        [System.Drawing.Brush]$Brush
    )

    $font = [System.Drawing.Font]::new($FontFamily, $FontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    try {
        $widths = @($Text.ToCharArray() | ForEach-Object {
            $Graphics.MeasureString($_, $font).Width
        })
        $totalWidth = (($widths | Measure-Object -Sum).Sum) + ($Spacing * ($widths.Count - 1))
        $x = (256 - $totalWidth) / 2

        for ($i = 0; $i -lt $Text.Length; $i++) {
            $path = New-Object System.Drawing.Drawing2D.GraphicsPath
            try {
                $rect = [System.Drawing.RectangleF]::new($x, $Top, $widths[$i] + 2, $Height)
                $path.AddString($Text[$i], $FontFamily, [int][System.Drawing.FontStyle]::Regular, $FontSize, $rect, $Format)
                $Graphics.DrawPath($OutlinePen, $path)
                $Graphics.FillPath($Brush, $path)
            }
            finally {
                $path.Dispose()
            }
            $x += $widths[$i] + $Spacing
        }
    }
    finally {
        $font.Dispose()
    }
}

Draw-SpacedText $graphics 'FREE' $fontFamily 37 0 48 4 $textFormat $textOutlinePen $textBrush
Draw-SpacedText $graphics 'FLY' $fontFamily 40 207 49 5 $textFormat $textOutlinePen $textBrush

$directory = Split-Path -Parent $OutputPath
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}
$bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)

$textBrush.Dispose()
$textOutlinePen.Dispose()
$textFormat.Dispose()
$fontFamily.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
$source.Dispose()

Write-Output "Generated $OutputPath (256x256 PNG)."
