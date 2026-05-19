param(
    [switch]$Help,
    [string]$Version = '',
    [string]$PackageVersion = '',
    [string]$OutputDir = '',
    [string]$PackagePath = '',
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [string]$SymbolsDir = '',
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
if (-not $Help -and -not [string]::IsNullOrWhiteSpace($Architecture)) { $forwardArgs.Architecture = $Architecture }
if (-not [string]::IsNullOrWhiteSpace($SymbolsDir)) { $forwardArgs.SymbolsDir = $SymbolsDir }
if ($NoCli) { $forwardArgs.NoCli = $true }

& $TargetScript @forwardArgs
exit $LASTEXITCODE
