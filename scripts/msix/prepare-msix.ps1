param(
    [switch]$Help,
    [string]$Version = '',
    [string]$ManifestPath = '',
    [string]$AssetsPath = '',
    [string]$OutputDir = 'msix-content',
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$TargetScript = Join-Path $ScriptDir '../packaging/msix/prepare-msix.ps1'

$forwardArgs = @{}
if ($Help) { $forwardArgs.Help = $true }
if (-not [string]::IsNullOrWhiteSpace($Version)) { $forwardArgs.Version = $Version }
if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) { $forwardArgs.ManifestPath = $ManifestPath }
if (-not [string]::IsNullOrWhiteSpace($AssetsPath)) { $forwardArgs.AssetsPath = $AssetsPath }
if (-not $Help -and -not [string]::IsNullOrWhiteSpace($OutputDir)) { $forwardArgs.OutputDir = $OutputDir }
if (-not $Help -and -not [string]::IsNullOrWhiteSpace($Architecture)) { $forwardArgs.Architecture = $Architecture }

& $TargetScript @forwardArgs
exit $LASTEXITCODE
