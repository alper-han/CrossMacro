param(
    [switch]$Help,
    [string]$Version = '',
    [string]$PackageVersion = '',
    [string]$OutputDir = '',
    [string]$PackagePath = '',
    [switch]$NoCli
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$TargetScript = Join-Path $ScriptDir '../packaging/msix/build-msix.ps1'

$forwardArgs = @{}
if ($Help) { $forwardArgs.Help = $true }
if (-not [string]::IsNullOrWhiteSpace($Version)) { $forwardArgs.Version = $Version }
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) { $forwardArgs.PackageVersion = $PackageVersion }
if (-not [string]::IsNullOrWhiteSpace($OutputDir)) { $forwardArgs.OutputDir = $OutputDir }
if (-not [string]::IsNullOrWhiteSpace($PackagePath)) { $forwardArgs.PackagePath = $PackagePath }
if ($NoCli) { $forwardArgs.NoCli = $true }

& $TargetScript @forwardArgs
exit $LASTEXITCODE
