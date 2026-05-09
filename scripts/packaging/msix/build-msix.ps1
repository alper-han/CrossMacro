[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(ParameterSetName = 'Build')]
    [string]$Version = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$PackageVersion = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$OutputDir = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$PackagePath = '',

    [Parameter(ParameterSetName = 'Build')]
    [switch]$NoCli
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Usage: build-msix.ps1 [-Version <version>] [-PackageVersion <name-version>] [-OutputDir <path>] [-PackagePath <path>] [-NoCli] [-Help]

Builds and validates the CrossMacro MSIX package without installing it:
  - dotnet publish Release win-x64 as non-single-file staged content
  - calls scripts/packaging/msix/prepare-msix.ps1 with manifest, assets, version, and output paths
  - validates the staged AppxManifest.xml and CrossMacro.UI.exe with scripts/smoke/msix.ps1
  - runs makeappx.exe pack to create the .msix package
  - unpacks the .msix and inspects AppxManifest.xml before package validation
  - calls scripts/smoke/msix.ps1 against the final .msix package

Options:
  -Version <version>         Three-part package version. Defaults to VERSION at repository root.
  -PackageVersion <version>  Version text used in the package filename. Defaults to -Version.
  -OutputDir <path>          Staged MSIX content directory. Defaults to <repo>/msix-content.
  -PackagePath <path>        Output .msix path. Defaults to <repo>/CrossMacro-<PackageVersion>.msix.
  -NoCli                     Skip executable CLI smoke after structure checks.
  -Help                      Show this help.
'@
}

function Fail-MsixBuild {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Error "MSIX build failed: $Message"
    exit 1
}

function Get-RepositoryVersion {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $versionPath = Join-Path $RepositoryRoot 'VERSION'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        Fail-MsixBuild "VERSION file not found: $versionPath"
    }

    $value = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        Fail-MsixBuild "VERSION file is empty: $versionPath"
    }

    return $value
}

function Find-MakeAppx {
    $command = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $sdkRoot = Join-Path $programFilesX86 'Windows Kits\10\bin'
        if (Test-Path -LiteralPath $sdkRoot -PathType Container) {
            $candidate = Get-ChildItem -LiteralPath $sdkRoot -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($candidate) {
                return $candidate.FullName
            }
        }
    }

    return $null
}

if ($Help) {
    Show-Usage
    exit 0
}

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$scriptsDir = (Resolve-Path -LiteralPath (Join-Path $scriptDir '../..')).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptsDir '..')).Path
$projectPath = Join-Path $projectRoot 'src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj'
$prepareScript = Join-Path $scriptDir 'prepare-msix.ps1'
$manifestPath = Join-Path $scriptsDir 'msix/AppxManifest.xml'
$assetsPath = Join-Path $scriptsDir 'msix/Assets'
$smokeScript = Join-Path $projectRoot 'scripts/smoke/msix.ps1'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-RepositoryVersion -RepositoryRoot $projectRoot
}

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    Fail-MsixBuild "MSIX Version must be three-part numeric for prepare-msix.ps1: $Version"
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = $Version
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $projectRoot 'msix-content'
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $projectRoot "CrossMacro-$PackageVersion.msix"
}

foreach ($requiredPath in @($projectPath, $prepareScript, $manifestPath, $smokeScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        Fail-MsixBuild "required file not found: $requiredPath"
    }
}

if (-not (Test-Path -LiteralPath $assetsPath -PathType Container)) {
    Fail-MsixBuild "MSIX assets directory not found: $assetsPath"
}

$makeappx = Find-MakeAppx
if (-not $makeappx) {
    Fail-MsixBuild 'makeappx.exe was not found. Install the Windows SDK or run on windows-latest.'
}

$resolvedOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$resolvedPackagePath = [System.IO.Path]::GetFullPath($PackagePath)
$packageParent = Split-Path -Parent $resolvedPackagePath
if (-not (Test-Path -LiteralPath $packageParent -PathType Container)) {
    New-Item -ItemType Directory -Path $packageParent -Force | Out-Null
}

if (Test-Path -LiteralPath $resolvedOutputDir) {
    Remove-Item -LiteralPath $resolvedOutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null

if (Test-Path -LiteralPath $resolvedPackagePath -PathType Leaf) {
    Remove-Item -LiteralPath $resolvedPackagePath -Force
}

$publishArgs = @(
    'publish', $projectPath,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=false',
    '-p:PublishTrimmed=false',
    '-p:PublishReadyToRun=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    "-p:Version=$Version",
    '-o', $resolvedOutputDir
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$expectedExecutable = Join-Path $resolvedOutputDir 'CrossMacro.UI.exe'
if (-not (Test-Path -LiteralPath $expectedExecutable -PathType Leaf)) {
    $publishedExecutables = @(Get-ChildItem -LiteralPath $resolvedOutputDir -Filter '*.exe' -File)
    if ($publishedExecutables.Count -ne 1) {
        Fail-MsixBuild "MSIX publish output must contain CrossMacro.UI.exe or exactly one EXE; found $($publishedExecutables.Count): $($publishedExecutables.Name -join ', ')"
    }

    Move-Item -LiteralPath $publishedExecutables[0].FullName -Destination $expectedExecutable
}

& $prepareScript -Version $Version -ManifestPath $manifestPath -AssetsPath $assetsPath -OutputDir $resolvedOutputDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$expectedMsixVersion = "$Version.0"
if ($NoCli) {
    & $smokeScript -Path $resolvedOutputDir -Staged -ExpectedVersion $expectedMsixVersion -NoCli
}
else {
    & $smokeScript -Path $resolvedOutputDir -Staged -ExpectedVersion $expectedMsixVersion
}
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $makeappx pack /d $resolvedOutputDir /p $resolvedPackagePath /nv
if ($LASTEXITCODE -ne 0) {
    Fail-MsixBuild "makeappx pack failed for $resolvedPackagePath"
}

$inspectDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crossmacro-msix-inspect-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $inspectDir -Force | Out-Null
    & $makeappx unpack /p $resolvedPackagePath /d $inspectDir /o
    if ($LASTEXITCODE -ne 0) {
        Fail-MsixBuild "makeappx unpack failed for $resolvedPackagePath"
    }

    $inspectedManifestPath = Join-Path $inspectDir 'AppxManifest.xml'
    if (-not (Test-Path -LiteralPath $inspectedManifestPath -PathType Leaf)) {
        Fail-MsixBuild "unpacked MSIX is missing AppxManifest.xml"
    }
}
finally {
    if (Test-Path -LiteralPath $inspectDir -PathType Container) {
        Remove-Item -LiteralPath $inspectDir -Recurse -Force
    }
}

if ($NoCli) {
    & $smokeScript -Path $resolvedPackagePath -ExpectedVersion $expectedMsixVersion -NoCli
}
else {
    & $smokeScript -Path $resolvedPackagePath -ExpectedVersion $expectedMsixVersion
}
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "MSIX build: OK ($resolvedPackagePath)"
