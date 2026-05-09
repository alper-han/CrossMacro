$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$TargetScript = Join-Path $ScriptDir '../packaging/msix/build-msix.ps1'
& $TargetScript @args
exit $LASTEXITCODE
