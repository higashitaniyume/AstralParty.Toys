[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ProductVersion = '1.0.1'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$publishRoot = (Resolve-Path $PublishDirectory).Path
if (-not (Test-Path (Join-Path $publishRoot 'AstralParty.Toys.exe'))) { throw 'Publish directory must contain AstralParty.Toys.exe.' }
if (-not (Test-Path (Join-Path $publishRoot 'coreclr.dll'))) { throw 'MSI requires a self-contained publish directory.' }
if ($ProductVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'MSI version must have three numeric parts.' }
$outputFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$buildRoot = Join-Path (Split-Path $outputFullPath -Parent) 'msi-build'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
# Include the license in the installed files as well as the setup UI.
Copy-Item (Join-Path $repoRoot 'LICENSE') (Join-Path $publishRoot 'LICENSE.txt') -Force
function XmlEscape([string]$value) { [System.Security.SecurityElement]::Escape($value) }
function StableId([string]$prefix, [string]$value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($value.ToLowerInvariant())
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    return $prefix + $hash.Substring(0, 32)
}
$directoryXml = [Text.StringBuilder]::new()
$componentXml = [Text.StringBuilder]::new()
$directories = @{ '.' = 'INSTALLFOLDER' }
foreach ($directory in Get-ChildItem $publishRoot -Directory -Recurse | Sort-Object { $_.FullName.Length }, FullName) {
    $relative = [IO.Path]::GetRelativePath($publishRoot, $directory.FullName)
    $parent = Split-Path $relative -Parent
    if (-not $parent) { $parent = '.' }
    $id = StableId 'D' $relative
    $directories[$relative] = $id
    [void]$directoryXml.AppendLine("<DirectoryRef Id=`"$($directories[$parent])`"><Directory Id=`"$id`" Name=`"$(XmlEscape $directory.Name)`" /></DirectoryRef>")
    [void]$componentXml.AppendLine("<Component Id=`"Cleanup$id`" Directory=`"$id`" Guid=`"*`"><RemoveFolder Id=`"Remove$id`" On=`"uninstall`" /><RegistryValue Root=`"HKCU`" Key=`"Software\AstralParty.Toys\Installer\Directories`" Name=`"$id`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" /></Component>")
}
foreach ($file in Get-ChildItem $publishRoot -File -Recurse | Sort-Object FullName) {
    $relative = [IO.Path]::GetRelativePath($publishRoot, $file.FullName)
    $parent = Split-Path $relative -Parent
    if (-not $parent) { $parent = '.' }
    $id = StableId 'F' $relative
    # A file plus an HKCU keypath needs an explicit stable component GUID.
    $componentGuid = [guid]::ParseExact($id.Substring(1), 'N').ToString('D')
    [void]$componentXml.AppendLine("<Component Id=`"C$id`" Directory=`"$($directories[$parent])`" Guid=`"$componentGuid`"><File Id=`"$id`" Source=`"$(XmlEscape $file.FullName)`" /><RegistryValue Root=`"HKCU`" Key=`"Software\AstralParty.Toys\Installer\Files`" Name=`"$id`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" /></Component>")
}
$payload = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    $directoryXml
    <ComponentGroup Id="PublishedFiles">$componentXml</ComponentGroup>
  </Fragment>
</Wix>
"@
$payloadPath = Join-Path $buildRoot 'Payload.wxs'
Set-Content $payloadPath $payload -Encoding utf8
$licenseText = Get-Content (Join-Path $repoRoot 'LICENSE') -Raw
$rtfText = $licenseText.Replace('\', '\\').Replace('{', '\{').Replace('}', '\}').Replace("`r", '').Replace("`n", '\par ')
$licensePath = Join-Path $buildRoot 'License.rtf'
Set-Content $licensePath ('{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs16 ' + $rtfText + '}') -Encoding ascii
$iconPath = Join-Path $repoRoot 'src/AstralParty.Toys/WebUI/images/UT_AccountHeadShot_70902.ico'
$logoPath = Join-Path $repoRoot 'src/AstralParty.Toys/WebUI/images/astral-party-logo.png'
$bgPath = Join-Path $repoRoot 'src/AstralParty.Toys/WebUI/images/Anni2nd.png'
$chibiPath = Join-Path $PSScriptRoot 'UT_Hero_LevelUp_107_04.png'
$bannerPath = Join-Path $buildRoot 'Banner.bmp'
$dialogPath = Join-Path $buildRoot 'Dialog.bmp'

Add-Type -AssemblyName System.Drawing
$iconBmp = [Drawing.Icon]::new($iconPath, 256, 256).ToBitmap()
$logoBmp = [Drawing.Bitmap]::new($logoPath)
$bgBmp = [Drawing.Bitmap]::new($bgPath)
$chibiBmp = [Drawing.Bitmap]::new($chibiPath)
try {
    $banner = [Drawing.Bitmap]::new(493, 58, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $g = [Drawing.Graphics]::FromImage($banner)
        try {
            $g.Clear([Drawing.Color]::White)
            $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
            $g.DrawImage($iconBmp, [Drawing.Rectangle]::new(438, 7, 44, 44))
        } finally {
            $g.Dispose()
        }
        $banner.Save($bannerPath, [Drawing.Imaging.ImageFormat]::Bmp)
    } finally {
        $banner.Dispose()
    }

    $dialog = [Drawing.Bitmap]::new(493, 312, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $gd = [Drawing.Graphics]::FromImage($dialog)
        try {
            $gd.Clear([Drawing.Color]::White)
            $gd.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
            $gd.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $gd.SetClip([Drawing.Rectangle]::new(0, 0, 164, 312))
            $srcH = $bgBmp.Height
            $srcW = [int]($srcH * (164.0 / 312.0))
            $srcX = [int]($bgBmp.Width * 0.52)
            $gd.DrawImage($bgBmp, [Drawing.Rectangle]::new(0, 0, 164, 312), [Drawing.Rectangle]::new($srcX, 0, $srcW, $srcH), [Drawing.GraphicsUnit]::Pixel)
            $overlay = [Drawing.Drawing2D.LinearGradientBrush]::new(
                [Drawing.Point]::new(0, 0),
                [Drawing.Point]::new(0, 312),
                [Drawing.Color]::FromArgb(150, 15, 17, 30),
                [Drawing.Color]::FromArgb(210, 15, 17, 30)
            )
            try {
                $gd.FillRectangle($overlay, 0, 0, 164, 312)
            } finally {
                $overlay.Dispose()
            }
            $logoW = 144
            $logoH = [int]($logoBmp.Height * ($logoW / [double]$logoBmp.Width))
            $gd.DrawImage($logoBmp, [Drawing.Rectangle]::new(10, 18, $logoW, $logoH))
            $chibiH = 175
            $chibiW = [int]($chibiBmp.Width * ($chibiH / [double]$chibiBmp.Height))
            $chibiX = [int]((164 - $chibiW) / 2)
            $gd.DrawImage($chibiBmp, [Drawing.Rectangle]::new($chibiX, 126, $chibiW, $chibiH))
            $gd.ResetClip()
            $pen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(226, 232, 240), 1)
            try {
                $gd.DrawLine($pen, 163, 0, 163, 312)
            } finally {
                $pen.Dispose()
            }
        } finally {
            $gd.Dispose()
        }
        $dialog.Save($dialogPath, [Drawing.Imaging.ImageFormat]::Bmp)
    } finally {
        $dialog.Dispose()
    }
} finally {
    $iconBmp.Dispose()
    $logoBmp.Dispose()
    $bgBmp.Dispose()
    $chibiBmp.Dispose()
}

& wix build (Join-Path $PSScriptRoot 'Package.wxs') $payloadPath -arch x64 -ext WixToolset.UI.wixext -culture zh-cn -d "ProductVersion=$ProductVersion" -d "AppIcon=$iconPath" -d "LicenseRtf=$licensePath" -d "BannerBmp=$bannerPath" -d "DialogBmp=$dialogPath" -intermediateFolder $buildRoot -o $outputFullPath
if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE" }
Write-Host "Built $outputFullPath"
