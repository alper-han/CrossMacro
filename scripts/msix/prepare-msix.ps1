param(
    [string]$Version = '',
    [string]$ManifestPath = '',
    [string]$AssetsPath = '',
    [string]$OutputDir = 'msix-content'
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$TargetScript = Join-Path $ScriptDir '../packaging/msix/prepare-msix.ps1'

$forwardArgs = @{
    OutputDir = $OutputDir
}
if (-not [string]::IsNullOrWhiteSpace($Version)) { $forwardArgs.Version = $Version }
if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) { $forwardArgs.ManifestPath = $ManifestPath }
if (-not [string]::IsNullOrWhiteSpace($AssetsPath)) { $forwardArgs.AssetsPath = $AssetsPath }

& $TargetScript @forwardArgs
exit $LASTEXITCODE
