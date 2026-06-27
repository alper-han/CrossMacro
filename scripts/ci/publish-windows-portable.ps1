[CmdletBinding(DefaultParameterSetName = 'Publish')]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(ParameterSetName = 'Publish')]
    [string]$Version = '',

    [Parameter(ParameterSetName = 'Publish')]
    [string]$OutputDir = '',

    [Parameter(ParameterSetName = 'Publish')]
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',

    [Parameter(ParameterSetName = 'Publish')]
    [switch]$NoCli
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Usage: publish-windows-portable.ps1 [-Version <version>] [-OutputDir <path>] [-Architecture <x64|arm64>] [-NoCli] [-Help]

Publishes the Windows portable CrossMacro artifact:
  - dotnet publish Release win-x64 or win-arm64 as a self-contained single-file executable
  - removes PDB symbols from the publish directory
  - requires a flat output containing exactly one file and exactly one .exe
  - runs scripts/smoke/windows-portable.ps1 against the resulting executable

Options:
  -Version <version>  Assembly/package version. Defaults to VERSION at repository root.
  -OutputDir <path>  Publish output directory. Defaults to <repo>/publish-windows.
  -Architecture      Windows architecture to publish: x64 or arm64. Defaults to x64.
  -NoCli             Skip executable CLI smoke after structure checks.
  -Help              Show this help.
'@
}

function Fail-PortablePublish {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Error "Windows portable publish failed: $Message"
    exit 1
}

function Get-RepositoryVersion {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $versionPath = Join-Path $RepositoryRoot 'VERSION'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        Fail-PortablePublish "VERSION file not found: $versionPath"
    }

    $value = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        Fail-PortablePublish "VERSION file is empty: $versionPath"
    }

    return $value
}

if ($Help) {
    Show-Usage
    exit 0
}

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptDir '../..')).Path
$projectPath = Join-Path $projectRoot 'src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj'
$smokeScript = Join-Path $projectRoot 'scripts/smoke/windows-portable.ps1'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-RepositoryVersion -RepositoryRoot $projectRoot
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $projectRoot 'publish-windows'
}

$runtimeIdentifier = switch ($Architecture) {
    'x64' { 'win-x64' }
    'arm64' { 'win-arm64' }
}

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    Fail-PortablePublish "Windows project not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $smokeScript -PathType Leaf)) {
    Fail-PortablePublish "Windows portable smoke helper not found: $smokeScript"
}

$resolvedOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (Test-Path -LiteralPath $resolvedOutputDir) {
    Remove-Item -LiteralPath $resolvedOutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null

$publishArgs = @(
    'publish', $projectPath,
    '-c', 'Release',
    '-r', $runtimeIdentifier,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:PublishTrimmed=true',
    '-p:TrimMode=partial',
    '-p:PublishAot=false',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishReadyToRun=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-p:ErrorOnDuplicatePublishOutputFiles=true',
    "-p:Version=$Version",
    '-o', $resolvedOutputDir
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$unexpectedDirectories = @(Get-ChildItem -LiteralPath $resolvedOutputDir -Directory)
if ($unexpectedDirectories.Count -gt 0) {
    Fail-PortablePublish "publish emitted unexpected directories: $($unexpectedDirectories.FullName -join ', ')"
}

Get-ChildItem -LiteralPath $resolvedOutputDir -File -Filter '*.pdb' | Remove-Item -Force

$files = @(Get-ChildItem -LiteralPath $resolvedOutputDir -File)
if ($files.Count -ne 1) {
    Fail-PortablePublish "publish output must contain exactly one file after symbol cleanup; found $($files.Count): $($files.Name -join ', ')"
}

$executables = @($files | Where-Object { $_.Extension -ieq '.exe' })
if ($executables.Count -ne 1) {
    Fail-PortablePublish "publish output must contain exactly one EXE; found $($executables.Count): $($files.Name -join ', ')"
}

$publishedExecutable = $executables[0].FullName

if ($NoCli) {
    & $smokeScript -Path $publishedExecutable -NoCli
}
else {
    & $smokeScript -Path $publishedExecutable
}
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "Windows portable publish: OK ($publishedExecutable)"
