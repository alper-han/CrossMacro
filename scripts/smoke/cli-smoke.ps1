[CmdletBinding(DefaultParameterSetName = 'Executable')]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(ParameterSetName = 'Executable')]
    [string]$Executable,

    [Parameter(ParameterSetName = 'Project')]
    [string]$Project
)

function Show-Usage {
    @'
Usage: cli-smoke.ps1 [-Executable <path>] [-Project <path>] [-Help]

Runs the shared CrossMacro CLI smoke contract:
  - top-level --help contains "Usage:"
  - settings get --json contains "status": "ok" and "code": 0
  - absolute dry-run macro JSON contains "coordinateMode": "absolute"
  - mixed dry-run macro JSON contains "coordinateMode": "mixed"

Examples:
  pwsh -NoProfile -File scripts/smoke/cli-smoke.ps1 -Executable ./CrossMacro.exe
  pwsh -NoProfile -File scripts/smoke/cli-smoke.ps1 -Project src/CrossMacro.Ui/CrossMacro.Ui.csproj
'@
}

function Fail-Smoke {
    param(
        [Parameter(Mandatory = $true)][string]$Assertion,
        [string]$Output = ''
    )

    Write-Error "CLI smoke failed: $Assertion"
    if ($Output) {
        Write-Error $Output
    }
    exit 1
}

function Invoke-Cli {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CliArgs)

    $exitCode = 0
    if ($script:Project) {
        $output = (& dotnet run --no-build --project $script:Project -- @CliArgs 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
    } else {
        $output = (& $script:Executable @CliArgs 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
    }

    if ($exitCode -ne 0) {
        Fail-Smoke "command exited $exitCode`: $($CliArgs -join ' ')" $output
    }

    return $output
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)][string]$Assertion,
        [Parameter(Mandatory = $true)][string]$Output,
        [Parameter(Mandatory = $true)][string]$Needle
    )

    if (-not $Output.Contains($Needle)) {
        Fail-Smoke $Assertion $Output
    }
}

if ($Help) {
    Show-Usage
    exit 0
}

if (-not $Executable -and -not $Project) {
    Fail-Smoke 'missing CLI target; pass -Executable or -Project'
}

if ($Executable -and $Project) {
    Fail-Smoke 'pass only one CLI target: -Executable or -Project'
}

$helpOutput = Invoke-Cli --help
Assert-Contains 'help Usage:' $helpOutput 'Usage:'

$settingsOutput = Invoke-Cli settings get --json
Assert-Contains 'settings status/code: "status": "ok"' $settingsOutput '"status": "ok"'
Assert-Contains 'settings status/code: "code": 0' $settingsOutput '"code": 0'

$dryRunOutput = Invoke-Cli run --step 'move abs 10 10' --step 'click left' --dry-run --json
Assert-Contains 'dry-run coordinateMode: "coordinateMode": "absolute"' $dryRunOutput '"coordinateMode": "absolute"'

$mixedDryRunOutput = Invoke-Cli run --step 'move abs 10 10' --step 'move rel 1 -1' --dry-run --json
Assert-Contains 'mixed dry-run coordinateMode: "coordinateMode": "mixed"' $mixedDryRunOutput '"coordinateMode": "mixed"'

Write-Output 'CLI smoke: OK'
