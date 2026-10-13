[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ProductVersion = '1.0.0'
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
& wix build (Join-Path $PSScriptRoot 'Package.wxs') $payloadPath -arch x64 -ext WixToolset.UI.wixext -culture en-us -d "ProductVersion=$ProductVersion" -d "AppIcon=$iconPath" -d "LicenseRtf=$licensePath" -intermediateFolder $buildRoot -o $outputFullPath
if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE" }
Write-Host "Built $outputFullPath"
