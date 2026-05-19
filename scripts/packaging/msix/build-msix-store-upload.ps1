[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(ParameterSetName = 'Build')]
    [string]$Version = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$PackageVersion = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$PackageDir = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$SymbolsDir = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$BundlePath = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$AppxSymPath = '',

    [Parameter(ParameterSetName = 'Build')]
    [string]$UploadPath = ''
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Usage: build-msix-store-upload.ps1 [-Version <version>] [-PackageVersion <name-version>] -PackageDir <dir> [-SymbolsDir <dir>] [-BundlePath <path>] [-AppxSymPath <path>] [-UploadPath <path>] [-Help]

Builds Store-oriented MSIX artifacts from architecture-specific MSIX packages:
  - creates CrossMacro-<PackageVersion>.msixbundle from x64 and arm64 .msix files
  - creates CrossMacro-<PackageVersion>.msixupload containing the bundle

Options:
  -Version <version>         Three-part package version. Defaults to VERSION at repository root.
  -PackageVersion <version>  Version text used in filenames. Defaults to -Version.
  -PackageDir <dir>          Directory containing CrossMacro-<PackageVersion>-x64.msix and -arm64.msix.
  -SymbolsDir <dir>          Ignored. Debug symbols are disabled for Store uploads.
  -BundlePath <path>         Output .msixbundle path. Defaults to <repo>/CrossMacro-<PackageVersion>.msixbundle.
  -AppxSymPath <path>        Ignored. Debug symbols are disabled for Store uploads.
  -UploadPath <path>         Output .msixupload path. Defaults to <repo>/CrossMacro-<PackageVersion>.msixupload.
  -Help                      Show this help.
'@
}

function Fail-MsixStoreBuild {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Error "MSIX Store upload build failed: $Message"
    exit 1
}

function Get-RepositoryVersion {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $versionPath = Join-Path $RepositoryRoot 'VERSION'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        Fail-MsixStoreBuild "VERSION file not found: $versionPath"
    }

    $value = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        Fail-MsixStoreBuild "VERSION file is empty: $versionPath"
    }

    return $value
}

function Find-MakeAppx {
    $command = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $sdkToolArchitectures = @('x64', 'arm64', 'x86')
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        $sdkToolArchitectures = @('arm64', 'x64', 'x86')
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $sdkRoot = Join-Path $programFilesX86 'Windows Kits\10\bin'
        if (Test-Path -LiteralPath $sdkRoot -PathType Container) {
            foreach ($sdkToolArchitecture in $sdkToolArchitectures) {
                $candidate = Get-ChildItem -LiteralPath $sdkRoot -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
                    Where-Object { $_.FullName -match "\\$sdkToolArchitecture\\makeappx\.exe$" } |
                    Sort-Object FullName -Descending |
                    Select-Object -First 1
                if ($candidate) {
                    return $candidate.FullName
                }
            }
        }
    }

    return $null
}

function Resolve-OutputPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$DefaultPath
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [System.IO.Path]::GetFullPath($DefaultPath)
    }

    return [System.IO.Path]::GetFullPath($Path)
}

function Remove-ExistingFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Test-ZipEntries {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$ExpectedEntries
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $actualEntries = @($zip.Entries | ForEach-Object { $_.FullName })
        foreach ($expectedEntry in $ExpectedEntries) {
            if ($expectedEntry -notin $actualEntries) {
                Fail-MsixStoreBuild "$Path is missing expected entry: $expectedEntry"
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

if ($Help) {
    Show-Usage
    exit 0
}

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$scriptsDir = (Resolve-Path -LiteralPath (Join-Path $scriptDir '../..')).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptsDir '..')).Path
$smokeScript = Join-Path $projectRoot 'scripts/smoke/msix.ps1'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-RepositoryVersion -RepositoryRoot $projectRoot
}

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    Fail-MsixStoreBuild "MSIX bundle version must be three-part numeric before .0 is appended: $Version"
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = $Version
}

if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    Fail-MsixStoreBuild '-PackageDir is required'
}

$makeappx = Find-MakeAppx
if (-not $makeappx) {
    Fail-MsixStoreBuild 'makeappx.exe was not found. Install the Windows SDK or run on windows-2025.'
}

$resolvedPackageDir = [System.IO.Path]::GetFullPath($PackageDir)
if (-not (Test-Path -LiteralPath $resolvedPackageDir -PathType Container)) {
    Fail-MsixStoreBuild "package directory not found: $resolvedPackageDir"
}

if (-not [string]::IsNullOrWhiteSpace($SymbolsDir)) {
    Write-Warning '-SymbolsDir is ignored because Release publishes disable debug symbols.'
}

if (-not [string]::IsNullOrWhiteSpace($AppxSymPath)) {
    Write-Warning '-AppxSymPath is ignored; no .appxsym is generated.'
}

$resolvedBundlePath = Resolve-OutputPath -Path $BundlePath -DefaultPath (Join-Path $projectRoot "CrossMacro-$PackageVersion.msixbundle")
$resolvedUploadPath = Resolve-OutputPath -Path $UploadPath -DefaultPath (Join-Path $projectRoot "CrossMacro-$PackageVersion.msixupload")

$x64Package = Join-Path $resolvedPackageDir "CrossMacro-$PackageVersion-x64.msix"
$arm64Package = Join-Path $resolvedPackageDir "CrossMacro-$PackageVersion-arm64.msix"
foreach ($package in @($x64Package, $arm64Package)) {
    if (-not (Test-Path -LiteralPath $package -PathType Leaf)) {
        Fail-MsixStoreBuild "required architecture MSIX missing: $package"
    }
}

$bundleSourceDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crossmacro-msix-bundle-source-" + [guid]::NewGuid().ToString('N'))
$bundleInspectDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crossmacro-msix-bundle-inspect-" + [guid]::NewGuid().ToString('N'))
$uploadSourceDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crossmacro-msix-upload-source-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $bundleSourceDir -Force | Out-Null
    Copy-Item -LiteralPath $x64Package -Destination $bundleSourceDir -Force
    Copy-Item -LiteralPath $arm64Package -Destination $bundleSourceDir -Force

    Remove-ExistingFile -Path $resolvedBundlePath
    $bundleVersion = "$Version.0"
    & $makeappx bundle /d $bundleSourceDir /p $resolvedBundlePath /bv $bundleVersion
    if ($LASTEXITCODE -ne 0) {
        Fail-MsixStoreBuild "makeappx bundle failed for $resolvedBundlePath"
    }

    New-Item -ItemType Directory -Path $bundleInspectDir -Force | Out-Null
    & $makeappx unbundle /p $resolvedBundlePath /d $bundleInspectDir /o
    if ($LASTEXITCODE -ne 0) {
        Fail-MsixStoreBuild "makeappx unbundle failed for $resolvedBundlePath"
    }

    $unbundledPackages = @(Get-ChildItem -LiteralPath $bundleInspectDir -Recurse -File -Filter '*.msix')
    if ($unbundledPackages.Count -ne 2) {
        Fail-MsixStoreBuild "expected 2 MSIX files in bundle, found $($unbundledPackages.Count): $($unbundledPackages.Name -join ', ')"
    }

    foreach ($architecture in @('x64', 'arm64')) {
        $package = @($unbundledPackages | Where-Object { $_.Name -like "*-$architecture.msix" })
        if ($package.Count -ne 1) {
            Fail-MsixStoreBuild "expected one $architecture package in bundle, found $($package.Count)"
        }
        & $smokeScript -Path $package[0].FullName -ExpectedVersion $bundleVersion -ExpectedArchitecture $architecture -NoCli
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    if (Test-Path -LiteralPath $uploadSourceDir -PathType Container) {
        Remove-Item -LiteralPath $uploadSourceDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $uploadSourceDir -Force | Out-Null
    Copy-Item -LiteralPath $resolvedBundlePath -Destination $uploadSourceDir -Force

    Remove-ExistingFile -Path $resolvedUploadPath
    $uploadZip = "$resolvedUploadPath.zip"
    if (Test-Path -LiteralPath $uploadZip -PathType Leaf) {
        Remove-Item -LiteralPath $uploadZip -Force
    }
    Compress-Archive -Path (Join-Path $uploadSourceDir '*') -DestinationPath $uploadZip -Force
    Move-Item -LiteralPath $uploadZip -Destination $resolvedUploadPath -Force
    $expectedUploadEntries = @((Split-Path -Leaf $resolvedBundlePath))
    Test-ZipEntries -Path $resolvedUploadPath -ExpectedEntries $expectedUploadEntries
}
finally {
    foreach ($tempPath in @($bundleSourceDir, $bundleInspectDir, $uploadSourceDir)) {
        if (Test-Path -LiteralPath $tempPath -PathType Container) {
            Remove-Item -LiteralPath $tempPath -Recurse -Force
        }
    }
}

Write-Output "MSIX Store upload build: OK ($resolvedBundlePath, $resolvedUploadPath)"
