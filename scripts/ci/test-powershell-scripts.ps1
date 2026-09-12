$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach ($script in Get-ChildItem (Join-Path $repoRoot 'scripts') -Recurse -Filter '*.ps1' -File) {
    $tokens = $null
    $parseErrors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) { throw ($parseErrors | Out-String) }
}

$fixture = Join-Path ([System.IO.Path]::GetTempPath()) ('crossmacro-msix-contract-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $fixture | Out-Null
    $output = Join-Path $fixture 'output with spaces'
    New-Item -ItemType Directory -Path $output | Out-Null
    Set-Content (Join-Path $output 'caller-owned.txt') 'keep me'
    $prepare = Join-Path $repoRoot 'scripts/packaging/msix/prepare-msix.ps1'
    Push-Location $fixture
    try {
        & $prepare -Version 1.2.3 -Architecture arm64 -OutputDir $output
        & $prepare -Version 1.2.4 -Architecture arm64 -OutputDir $output
    }
    finally { Pop-Location }
    [xml]$manifest = Get-Content (Join-Path $output 'AppxManifest.xml') -Raw
    if ($manifest.Package.Identity.Version -ne '1.2.4.0' -or $manifest.Package.Identity.ProcessorArchitecture -ne 'arm64') {
        throw 'Repeated preparation did not update identity correctly.'
    }
    if ((Get-Content (Join-Path $output 'caller-owned.txt') -Raw).Trim() -ne 'keep me') {
        throw 'Preparation replaced a caller-owned file.'
    }

    . (Join-Path $repoRoot 'scripts/lib/output-path.ps1')
    foreach ($path in @($repoRoot, (Join-Path $repoRoot 'src'), (Split-Path -Parent $repoRoot),
                        (Join-Path $repoRoot 'scripts/msix/Assets'))) {
        $rejected = $false
        try { Assert-CrossMacroOutputDirectory -Directory $path -RepositoryRoot $repoRoot }
        catch { $rejected = $true }
        if (-not $rejected) { throw "Unsafe output directory accepted: $path" }
    }
    Assert-CrossMacroOutputDirectory -Directory (Join-Path $fixture 'new-output') -RepositoryRoot $repoRoot
}
finally {
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
Write-Output 'PowerShell scripts: syntax, MSIX repeat preparation, and output guards OK'
