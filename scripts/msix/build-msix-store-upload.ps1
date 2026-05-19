param(
    [switch]$Help,
    [string]$Version = '',
    [string]$PackageVersion = '',
    [string]$PackageDir = '',
    [string]$SymbolsDir = '',
    [string]$BundlePath = '',
    [string]$AppxSymPath = '',
    [string]$UploadPath = ''
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$TargetScript = Join-Path $ScriptDir '../packaging/msix/build-msix-store-upload.ps1'

$forwardArgs = @{}
if ($Help) { $forwardArgs.Help = $true }
if (-not [string]::IsNullOrWhiteSpace($Version)) { $forwardArgs.Version = $Version }
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) { $forwardArgs.PackageVersion = $PackageVersion }
if (-not [string]::IsNullOrWhiteSpace($PackageDir)) { $forwardArgs.PackageDir = $PackageDir }
if (-not [string]::IsNullOrWhiteSpace($SymbolsDir)) { $forwardArgs.SymbolsDir = $SymbolsDir }
if (-not [string]::IsNullOrWhiteSpace($BundlePath)) { $forwardArgs.BundlePath = $BundlePath }
if (-not [string]::IsNullOrWhiteSpace($AppxSymPath)) { $forwardArgs.AppxSymPath = $AppxSymPath }
if (-not [string]::IsNullOrWhiteSpace($UploadPath)) { $forwardArgs.UploadPath = $UploadPath }

& $TargetScript @forwardArgs
exit $LASTEXITCODE
