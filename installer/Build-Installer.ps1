[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ProductVersion,
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'AstralParty.Toys.exe'))) { throw 'Publish directory must contain AstralParty.Toys.exe.' }
if (-not $ProductVersion) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot 'src/AstralParty.Toys/AstralParty.Toys.csproj') -Raw
    $ProductVersion = [string]$project.Project.PropertyGroup.Version
}
if ($ProductVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Installer version must have three numeric parts.' }
$outputFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if ([IO.Path]::GetExtension($outputFullPath) -ne '.exe') { throw 'Inno Setup output must be an EXE.' }
if (-not $CompilerPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $CompilerPath = $command.Source }
    else {
        foreach ($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
            if (Test-Path -LiteralPath $candidate) { $CompilerPath = $candidate; break }
        }
    }
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) { throw 'Install Inno Setup 6, add ISCC.exe to PATH, or pass -CompilerPath.' }
$outputDirectory = Split-Path $outputFullPath -Parent
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $CompilerPath "/DPublishDirectory=$publishRoot" "/DProductVersion=$ProductVersion" "/DOutputDirectory=$outputDirectory" "/DOutputBaseName=$([IO.Path]::GetFileNameWithoutExtension($outputFullPath))" "/DRepoRoot=$repoRoot" (Join-Path $PSScriptRoot 'Setup.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup build failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $outputFullPath)) { throw 'Compiler did not produce the expected installer.' }
Write-Host "Built $outputFullPath"
