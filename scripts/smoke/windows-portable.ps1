[CmdletBinding()]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(Position = 0, ParameterSetName = 'Smoke')]
    [string]$Path,

    [Parameter(ParameterSetName = 'Smoke')]
    [switch]$NoCli
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Usage: windows-portable.ps1 <portable-exe-or-directory> [-NoCli] [-Help]

Validates a CrossMacro Windows portable package:
  - verifies the artifact path exists
  - accepts either a portable CrossMacro.UI.exe path or a directory containing it
  - checks the expected executable and rejects obvious missing payloads
  - runs shared cli-smoke.ps1 against the executable when possible

Options:
  -NoCli  Skip executable CLI smoke after structure checks
  -Help   Show this help
'@
}

function Fail-Smoke {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Error "Windows portable smoke failed: $Message"
    exit 1
}

function Resolve-ExecutablePath {
    param([Parameter(Mandatory = $true)][string]$ArtifactPath)

    if (Test-Path -LiteralPath $ArtifactPath -PathType Leaf) {
        $leaf = Split-Path -Leaf $ArtifactPath
        if ($leaf -notmatch '\.exe$') {
            Fail-Smoke "portable artifact is a file but not an .exe: $ArtifactPath"
        }
        return (Resolve-Path -LiteralPath $ArtifactPath).Path
    }

    if (Test-Path -LiteralPath $ArtifactPath -PathType Container) {
        $preferred = Join-Path $ArtifactPath 'CrossMacro.UI.exe'
        if (Test-Path -LiteralPath $preferred -PathType Leaf) {
            return (Resolve-Path -LiteralPath $preferred).Path
        }

        $executables = @(Get-ChildItem -LiteralPath $ArtifactPath -Filter '*.exe' -File)
        if ($executables.Count -eq 1) {
            return $executables[0].FullName
        }

        Fail-Smoke "expected CrossMacro.UI.exe or exactly one .exe in directory: $ArtifactPath"
    }

    Fail-Smoke "missing portable artifact path: $ArtifactPath"
}

if ($Help) {
    Show-Usage
    exit 0
}

if (-not $Path) {
    Fail-Smoke 'missing portable artifact path'
}

$executable = Resolve-ExecutablePath $Path
$exeName = Split-Path -Leaf $executable
if ($exeName -notmatch '\.exe$') {
    Fail-Smoke "unexpected portable executable name: $exeName"
}

$artifactRoot = Split-Path -Parent $executable
if (Test-Path -LiteralPath $Path -PathType Container) {
    $unexpected = @(Get-ChildItem -LiteralPath $artifactRoot -File | Where-Object { $_.Name -notin @($exeName) -and $_.Extension -ne '.pdb' })
    if ($unexpected.Count -gt 0) {
        Fail-Smoke "portable directory contains unexpected top-level files: $($unexpected.Name -join ', ')"
    }
}

$cliSmoke = Join-Path $PSScriptRoot 'cli-smoke.ps1'
if (-not (Test-Path -LiteralPath $cliSmoke -PathType Leaf)) {
    Fail-Smoke "shared CLI smoke helper not found: $cliSmoke"
}

if (-not $NoCli) {
    & $cliSmoke -Executable $executable
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Output 'Windows portable smoke: OK'
