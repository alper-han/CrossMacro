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
    # Execute the actual workflow invocations against parameter-only copies of
    # their targets. Parsing alone cannot detect broken named-argument splatting.
    $workflow = Get-Content (Join-Path $repoRoot '.github/workflows/_package-windows.yml') -Raw
    $blocks = [regex]::Matches($workflow, '(?m)^        run: \|\r?\n((?:^          .*\r?\n|^\r?\n)+)')
    $checkedCalls = 0
    foreach ($block in $blocks) {
        $body = [regex]::Replace($block.Groups[1].Value, '(?m)^          ', '')
        $target = [regex]::Match($body, '(?m)^\./(scripts/(?:ci/publish-windows-portable|msix/build-msix|msix/build-msix-store-upload)\.ps1) @\w+')
        if (-not $target.Success) { continue }
        $relativePath = $target.Groups[1].Value
        $tokens = $null
        $parseErrors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $repoRoot $relativePath), [ref]$tokens, [ref]$parseErrors)
        $stubPath = Join-Path $fixture $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $stubPath) -Force | Out-Null
        $stub = $ast.ParamBlock.Extent.Text + @'

if ($args.Count -gt 0) { throw "Unbound workflow arguments: $args" }
Write-Output -NoEnumerate $PSBoundParameters
'@
        Set-Content -LiteralPath $stubPath -Value $stub
        foreach ($architecture in @('x64', 'arm64')) {
            foreach ($smoke in @('true', 'false')) {
                $expanded = $body.Replace('${{ inputs.version }}', '1.2.3').
                    Replace('${{ inputs.canonical_version }}', '1.2.3-ci.4').
                    Replace('${{ matrix.architecture }}', $architecture).
                    Replace('${{ inputs.smoke_enabled }}', $smoke)
                Push-Location $fixture
                try { $bound = & ([scriptblock]::Create($expanded)) }
                finally { Pop-Location }
                if ($bound.Version -ne '1.2.3') { throw "Workflow version binding failed: $relativePath" }
                if ($relativePath -like '*store-upload*') {
                    if ([bool]$bound.SkipSmoke -ne ($smoke -eq 'false') -or
                        $bound.BundlePath -ne 'artifacts/packages/msix/CrossMacro-1.2.3-ci.4.msixbundle' -or
                        $bound.UploadPath -ne 'artifacts/packages/msix/CrossMacro-1.2.3-ci.4.msixupload') {
                        throw 'Store workflow argument binding failed.'
                    }
                }
                elseif ($bound.Architecture -ne $architecture -or -not $bound.SkipSmoke) {
                    throw "Workflow architecture/switch binding failed: $relativePath"
                }
                if ($relativePath -like 'scripts/msix/*' -and $bound.PackageVersion -ne '1.2.3-ci.4') {
                    throw "Workflow package version binding failed: $relativePath"
                }
            }
        }
        $checkedCalls++
    }
    if ($checkedCalls -ne 3) { throw "Expected three Windows package workflow invocations, found $checkedCalls." }

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
Write-Output 'PowerShell scripts: syntax, workflow argument binding, MSIX repeat preparation, and output guards OK'
