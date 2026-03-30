<#
.SYNOPSIS
    Builds VoiceKeyboard as a self-contained ARM64 MSIX package for Microsoft Store submission.

.DESCRIPTION
    1. Publishes the WPF app as self-contained ARM64
    2. Copies Package.appxmanifest + Assets into the publish folder
    3. Packages with makeappx.exe
    4. Signs the package with signtool.exe (dev self-signed cert, or your real cert)

.REQUIREMENTS
    - Windows SDK 10.0.26100+ (makeappx.exe, signtool.exe)
    - .NET 8 SDK
    - Run from: C:\Users\markz\VoiceKeyboard\

.USAGE
    .\Build-Store.ps1                          # dev build (self-signed)
    .\Build-Store.ps1 -CertThumbprint <thumb>  # production build with your cert
    .\Build-Store.ps1 -ForStore                # for actual Store upload (no signing needed)
#>

param(
    [string]$CertThumbprint = "",
    [switch]$ForStore,
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$publish = "$root\publish\VoiceKeyboard"
$msix    = "$root\publish\VoiceKeyboard_$($Version)_arm64.msix"

# ── Find Windows SDK tools ────────────────────────────────────────────────────
# Check bundled tools first (extracted from Microsoft.Windows.SDK.BuildTools NuGet)
$bundledSdk  = "$root\tools\sdk\bin\10.0.26100.0"
$systemSdk   = "C:\Program Files (x86)\Windows Kits\10\bin"
$makeAppx    = $null
$signTool    = $null

foreach ($base in @($bundledSdk, (Get-ChildItem $systemSdk -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName))) {
    if (-not $base) { continue }
    foreach ($arch in @("arm64", "x64", "x86")) {
        $mx = "$base\$arch\makeappx.exe"
        $st = "$base\$arch\signtool.exe"
        if ((Test-Path $mx) -and (-not $makeAppx)) { $makeAppx = $mx }
        if ((Test-Path $st) -and (-not $signTool)) { $signTool = $st }
    }
}
if (-not $makeAppx) { Write-Error "makeappx.exe not found. Run: dotnet tool install Microsoft.Windows.SDK.BuildTools" }
Write-Host "makeappx: $makeAppx" -ForegroundColor Cyan
Write-Host "signtool:  $signTool" -ForegroundColor Cyan

# ── 1. Publish ────────────────────────────────────────────────────────────────
Write-Host "`n[1/4] Publishing self-contained ARM64 release..." -ForegroundColor Yellow
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

$dotnet = if (Test-Path "C:\dotnet\dotnet.exe") { "C:\dotnet\dotnet.exe" } else { "dotnet" }
& $dotnet publish "$root\VoiceKeyboard.csproj" `
    -c Release `
    -r win-arm64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publish

Write-Host "  Published to: $publish" -ForegroundColor Green

# ── 2. Copy manifest + assets ────────────────────────────────────────────────
Write-Host "`n[2/4] Copying manifest and assets..." -ForegroundColor Yellow

# Update version in manifest
$manifestSrc = "$root\Package.appxmanifest"
$manifestDst = "$publish\AppxManifest.xml"
# Read as raw bytes then decode — avoids PowerShell silently keeping BOM in the string
$rawBytes = [System.IO.File]::ReadAllBytes($manifestSrc)
# Strip UTF-8 BOM (EF BB BF) if present
if ($rawBytes.Length -ge 3 -and $rawBytes[0] -eq 0xEF -and $rawBytes[1] -eq 0xBB -and $rawBytes[2] -eq 0xBF) {
    $rawBytes = $rawBytes[3..($rawBytes.Length-1)]
}
$content = [System.Text.Encoding]::UTF8.GetString($rawBytes)
# Target ONLY the Version attribute inside the <Identity element.
# This avoids touching the <?xml version=...?> declaration, MinVersion=, or MaxVersionTested=.
$content = [regex]::Replace($content,
    '(<Identity\b[^>]*?\bVersion=")[^"]+(")',
    "`${1}$Version`${2}")
# Write UTF-8 without BOM (makeappx requirement)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($manifestDst, $content, $utf8NoBom)

# Copy store assets
$assetsDst = "$publish\Assets\Store"
New-Item -ItemType Directory -Path $assetsDst -Force | Out-Null
Copy-Item "$root\Assets\Store\*" $assetsDst -Force
Write-Host "  Manifest and assets copied." -ForegroundColor Green

# ── 3. Package with makeappx ─────────────────────────────────────────────────
Write-Host "`n[3/4] Creating MSIX package..." -ForegroundColor Yellow
if (Test-Path $msix) { Remove-Item $msix -Force }

& $makeAppx pack /d $publish /p $msix /nv
Write-Host "  Package created: $msix" -ForegroundColor Green

# ── 4. Sign ──────────────────────────────────────────────────────────────────
if (-not $ForStore) {
    Write-Host "`n[4/4] Signing package..." -ForegroundColor Yellow

    if ($CertThumbprint) {
        # Sign with a real/EV code signing certificate from your cert store
        & $signTool sign /fd SHA256 /sha1 $CertThumbprint /tr http://timestamp.digicert.com /td SHA256 $msix
        Write-Host "  Signed with certificate: $CertThumbprint" -ForegroundColor Green
    } else {
        # Create a temporary self-signed cert for local testing
        Write-Host "  Creating self-signed dev certificate..."
        $cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject "CN=VoiceKeyboard" `
            -KeyUsage DigitalSignature `
            -FriendlyName "VoiceKeyboard Dev Cert" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

        # Export PFX
        $pfxPath = "$root\publish\DevCert.pfx"
        $pfxPwd  = ConvertTo-SecureString -String "VoiceKeyboardDev" -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPwd | Out-Null

        & $signTool sign /fd SHA256 /f $pfxPath /p "VoiceKeyboardDev" /tr http://timestamp.digicert.com /td SHA256 $msix
        Write-Host "  Signed with self-signed dev cert (thumbprint: $($cert.Thumbprint))" -ForegroundColor Green
        Write-Host "  NOTE: To install locally, also trust DevCert.pfx in Trusted Root Authorities." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "`n[4/4] Skipping signing (Store upload - Microsoft signs the package)." -ForegroundColor Cyan
}

# ── Done ─────────────────────────────────────────────────────────────────────
Write-Host "`n========================================" -ForegroundColor Green
Write-Host " BUILD COMPLETE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host " MSIX: $msix"
Write-Host " Size: $([Math]::Round((Get-Item $msix).Length / 1MB, 1)) MB"
Write-Host ""
if ($ForStore) {
    Write-Host " Upload this file at:" -ForegroundColor Cyan
    Write-Host " https://partner.microsoft.com/dashboard" -ForegroundColor Cyan
}
