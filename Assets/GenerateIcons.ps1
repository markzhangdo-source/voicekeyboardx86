<#
.SYNOPSIS
    Generates all required Windows Store icon assets for VoiceKeyboard.
    Run this once to create placeholder icons, then replace with your final artwork.

.DESCRIPTION
    Creates PNG files at every size required by the Microsoft Store and Windows taskbar/Start.
    The icons use a dark purple background (#1E1E2E) with a white microphone shape.

.USAGE
    cd C:\Users\markz\VoiceKeyboard\Assets
    .\GenerateIcons.ps1
#>

Add-Type -AssemblyName System.Drawing

function New-VoiceKeyboardIcon {
    param(
        [int]$Width,
        [int]$Height,
        [string]$OutputPath
    )

    $bmp    = New-Object System.Drawing.Bitmap($Width, $Height)
    $gfx    = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # Background — rounded rect or full fill
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 30, 46))
    $gfx.FillRectangle($bgBrush, 0, 0, $Width, $Height)

    # Accent gradient circle
    $cx = $Width / 2
    $cy = $Height / 2
    $r  = [Math]::Min($Width, $Height) * 0.38
    $accentBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new($cx - $r, $cy - $r),
        [System.Drawing.PointF]::new($cx + $r, $cy + $r),
        [System.Drawing.Color]::FromArgb(255, 80,  80, 200),
        [System.Drawing.Color]::FromArgb(255, 120, 60, 220)
    )
    $gfx.FillEllipse($accentBrush, $cx - $r, $cy - $r, $r * 2, $r * 2)

    # Microphone body (white rectangle with rounded top)
    $micW  = $Width  * 0.22
    $micH  = $Height * 0.34
    $micX  = $cx - $micW / 2
    $micY  = $cy - $micH * 0.6
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $pen   = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [Math]::Max(1, $Width * 0.025))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    # Body
    $gfx.FillRectangle($white, $micX, $micY + $micW / 2, $micW, $micH - $micW / 2)
    $gfx.FillEllipse($white, $micX, $micY, $micW, $micW)

    # Stand arc
    $arcR = $micW * 1.4
    $arcX = $cx - $arcR
    $arcY = $micY + $micH * 0.55
    $gfx.DrawArc($pen, $arcX, $arcY, $arcR * 2, $arcR * 1.1, 0, 180)

    # Stand pole
    $poleW = [Math]::Max(1.5, $Width * 0.025)
    $polePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $poleW)
    $gfx.DrawLine($polePen, $cx, $arcY + $arcR * 1.1 * 0.5, $cx, $micY + $micH + $Height * 0.07)

    # Base line
    $baseW = $micW * 1.2
    $basePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $poleW)
    $baseY = $micY + $micH + $Height * 0.07
    $gfx.DrawLine($basePen, $cx - $baseW / 2, $baseY, $cx + $baseW / 2, $baseY)

    $gfx.Dispose()
    $dir = Split-Path $OutputPath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  Created: $OutputPath ($Width x $Height)"
}

Write-Host "Generating VoiceKeyboard Store icons..."
$store = "$PSScriptRoot\Store"

# Windows Store required sizes
New-VoiceKeyboardIcon -Width  50  -Height  50  -OutputPath "$store\StoreLogo.png"
New-VoiceKeyboardIcon -Width  44  -Height  44  -OutputPath "$store\Square44x44Logo.png"
New-VoiceKeyboardIcon -Width  71  -Height  71  -OutputPath "$store\Square71x71Logo.png"
New-VoiceKeyboardIcon -Width 150  -Height 150  -OutputPath "$store\Square150x150Logo.png"
New-VoiceKeyboardIcon -Width 310  -Height 310  -OutputPath "$store\Square310x310Logo.png"
New-VoiceKeyboardIcon -Width 310  -Height 150  -OutputPath "$store\Wide310x150Logo.png"
New-VoiceKeyboardIcon -Width 620  -Height 300  -OutputPath "$store\SplashScreen.png"

# Windows taskbar / desktop bridge sizes
$icons = "$PSScriptRoot\Icons"
foreach ($size in @(16, 24, 32, 48, 64, 96, 128, 256)) {
    New-VoiceKeyboardIcon -Width $size -Height $size -OutputPath "$icons\icon_$($size)x$($size).png"
}

Write-Host ""
Write-Host "Done!  Replace these placeholders with your final artwork before Store submission."
Write-Host "Recommended tool: Adobe Express, Figma, or DALL-E 3 for the hero/promotional images."
