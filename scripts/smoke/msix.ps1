[CmdletBinding(DefaultParameterSetName = 'Smoke')]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(Position = 0, ParameterSetName = 'Smoke')]
    [string]$Path,

    [Parameter(ParameterSetName = 'Smoke')]
    [switch]$Staged,

    [Parameter(ParameterSetName = 'Smoke')]
    [string]$ExpectedVersion,

    [Parameter(ParameterSetName = 'Smoke')]
    [string]$ExpectedArchitecture,

    [Parameter(ParameterSetName = 'Smoke')]
    [switch]$NoCli
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Usage: msix.ps1 <package.msix-or-staged-directory> [-Staged] [-ExpectedVersion <version>] [-ExpectedArchitecture <x64|arm64>] [-NoCli] [-Help]

Validates a CrossMacro MSIX package or staged MSIX directory without installing it:
  - staged mode validates AppxManifest.xml directly
  - package mode unpacks with makeappx when available, or Expand-Archive when practical
  - validates Identity version, application executable CrossMacro.UI.exe, runFullTrust capability, and expected assets
  - smokes staged CrossMacro.UI.exe with cli-smoke.ps1 when the executable is present and runnable

Options:
  -Staged                    Treat Path as an already unpacked/staged MSIX content directory
  -ExpectedVersion <version> Require AppxManifest.xml Identity Version to match this value
  -ExpectedArchitecture <a>  Require AppxManifest.xml Identity ProcessorArchitecture to match x64 or arm64
  -NoCli                     Skip executable CLI smoke after structure checks
  -Help                      Show this help
'@
}

function Fail-Smoke {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Error "MSIX smoke failed: $Message"
    exit 1
}

function Find-MakeAppx {
    $command = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $sdkToolArchitectures = @('x64', 'arm64', 'x86')
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        $sdkToolArchitectures = @('arm64', 'x64', 'x86')
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        return $null
    }
    $sdkRoot = Join-Path $programFilesX86 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $sdkRoot -PathType Container) {
        foreach ($sdkToolArchitecture in $sdkToolArchitectures) {
            $candidate = Get-ChildItem -LiteralPath $sdkRoot -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match "\\$sdkToolArchitecture\\makeappx\.exe$" } |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($candidate) {
                return $candidate.FullName
            }
        }
    }

    return $null
}

function Expand-MsixPackage {
    param(
        [Parameter(Mandatory = $true)][string]$PackagePath,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $makeappx = Find-MakeAppx
    if ($makeappx) {
        & $makeappx unpack /p $PackagePath /d $Destination /o | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Fail-Smoke "makeappx unpack failed for $PackagePath"
        }
        return
    }

    try {
        Expand-Archive -LiteralPath $PackagePath -DestinationPath $Destination -Force
    }
    catch {
        Fail-Smoke "makeappx was not found and Expand-Archive could not unpack MSIX: $($_.Exception.Message)"
    }
}

function Get-ManifestNamespaceManager {
    param([Parameter(Mandatory = $true)][xml]$Manifest)

    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($Manifest.NameTable)
    $namespaceManager.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespaceManager.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
    $namespaceManager.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3')
    $namespaceManager.AddNamespace('uap10', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/10')
    $namespaceManager.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')
    $namespaceManager.AddNamespace('rescap', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities')
    return ,$namespaceManager
}

function Test-CliSmokeSupported {
    param([Parameter(Mandatory = $true)][string]$Architecture)

    $isWindowsRuntime = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
    if (-not $isWindowsRuntime) {
        return $false
    }

    if ($Architecture -ne 'arm64') {
        return $true
    }

    return [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64
}

if ($Help) {
    Show-Usage
    exit 0
}

if (-not $Path) {
    Fail-Smoke 'missing MSIX artifact or staged directory path'
}

$tempDir = $null
try {
    if ($Staged -or (Test-Path -LiteralPath $Path -PathType Container)) {
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
            Fail-Smoke "missing staged MSIX directory: $Path"
        }
        $contentDir = (Resolve-Path -LiteralPath $Path).Path
    }
    else {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
            Fail-Smoke "missing MSIX artifact: $Path"
        }
        if ((Split-Path -Leaf $Path) -notmatch '\.msix$') {
            Fail-Smoke "package artifact is not an .msix file: $Path"
        }
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("crossmacro-msix-smoke-" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        Expand-MsixPackage -PackagePath (Resolve-Path -LiteralPath $Path).Path -Destination $tempDir
        $contentDir = $tempDir
    }

    $manifestPath = Join-Path $contentDir 'AppxManifest.xml'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Fail-Smoke "AppxManifest.xml not found in MSIX content: $contentDir"
    }

    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    $namespaceManager = Get-ManifestNamespaceManager $manifest
    $identity = $manifest.SelectSingleNode('/appx:Package/appx:Identity', $namespaceManager)
    if ($null -eq $identity) {
        Fail-Smoke 'AppxManifest.xml missing Identity element'
    }

    if ($identity.Name -ne 'CrossMacro.CrossMacro') {
        Fail-Smoke "unexpected MSIX Identity Name: $($identity.Name)"
    }

    if ($ExpectedArchitecture -and $ExpectedArchitecture -notin @('x64', 'arm64')) {
        Fail-Smoke "ExpectedArchitecture must be x64 or arm64: $ExpectedArchitecture"
    }

    if ($ExpectedArchitecture -and $identity.ProcessorArchitecture -ne $ExpectedArchitecture) {
        Fail-Smoke "MSIX architecture mismatch. Expected $ExpectedArchitecture, got $($identity.ProcessorArchitecture)"
    }

    if ($ExpectedVersion -and $identity.Version -ne $ExpectedVersion) {
        Fail-Smoke "MSIX version mismatch. Expected $ExpectedVersion, got $($identity.Version)"
    }

    if ($identity.Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        Fail-Smoke "MSIX Identity Version is not four-part numeric: $($identity.Version)"
    }

    $application = $manifest.SelectSingleNode('/appx:Package/appx:Applications/appx:Application', $namespaceManager)
    if ($null -eq $application) {
        Fail-Smoke 'AppxManifest.xml missing Application element'
    }

    $executableRelative = $application.Executable
    if ($executableRelative -ne 'CrossMacro.UI.exe') {
        Fail-Smoke "unexpected Application Executable: $executableRelative"
    }

    if ($application.EntryPoint -ne 'Windows.FullTrustApplication') {
        Fail-Smoke "unexpected Application EntryPoint: $($application.EntryPoint)"
    }

    $runtimeBehavior = $application.GetAttribute('RuntimeBehavior', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/10')
    if (-not [string]::IsNullOrWhiteSpace($runtimeBehavior)) {
        Fail-Smoke "Application should use EntryPoint compatibility mode instead of uap10 RuntimeBehavior: $runtimeBehavior"
    }

    $trustLevel = $application.GetAttribute('TrustLevel', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/10')
    if (-not [string]::IsNullOrWhiteSpace($trustLevel)) {
        Fail-Smoke "Application should use EntryPoint compatibility mode instead of uap10 TrustLevel: $trustLevel"
    }

    $targetDeviceFamily = $manifest.SelectSingleNode('/appx:Package/appx:Dependencies/appx:TargetDeviceFamily[@Name="Windows.Desktop"]', $namespaceManager)
    if ($null -eq $targetDeviceFamily) {
        Fail-Smoke 'AppxManifest.xml missing Windows.Desktop TargetDeviceFamily'
    }

    if ([version]$targetDeviceFamily.MinVersion -lt [version]'10.0.19041.0') {
        Fail-Smoke "MSIX manifest requires TargetDeviceFamily MinVersion 10.0.19041.0 or newer, got $($targetDeviceFamily.MinVersion)"
    }

    $ignorableNamespaces = @($manifest.Package.IgnorableNamespaces -split '\s+' | Where-Object { $_ })
    foreach ($requiredNamespace in @('uap3', 'desktop', 'rescap')) {
        if ($requiredNamespace -notin $ignorableNamespaces) {
            Fail-Smoke "Package IgnorableNamespaces missing $requiredNamespace"
        }
    }

    $executionAliasExtension = $manifest.SelectSingleNode('/appx:Package/appx:Applications/appx:Application/appx:Extensions/uap3:Extension[@Category="windows.appExecutionAlias"]', $namespaceManager)
    if ($null -eq $executionAliasExtension) {
        Fail-Smoke 'AppxManifest.xml missing app execution alias extension'
    }
    if ($executionAliasExtension.Executable -ne 'CrossMacro.UI.exe') {
        Fail-Smoke "unexpected app execution alias executable: $($executionAliasExtension.Executable)"
    }
    if ($executionAliasExtension.EntryPoint -ne 'Windows.FullTrustApplication') {
        Fail-Smoke "unexpected app execution alias EntryPoint: $($executionAliasExtension.EntryPoint)"
    }

    $executionAlias = $manifest.SelectSingleNode('/appx:Package/appx:Applications/appx:Application/appx:Extensions/uap3:Extension[@Category="windows.appExecutionAlias"]/uap3:AppExecutionAlias/desktop:ExecutionAlias[@Alias="crossmacro.exe"]', $namespaceManager)
    if ($null -eq $executionAlias) {
        Fail-Smoke 'AppxManifest.xml missing crossmacro.exe app execution alias'
    }

    $capability = $manifest.SelectSingleNode('/appx:Package/appx:Capabilities/rescap:Capability[@Name="runFullTrust"]', $namespaceManager)
    if ($null -eq $capability) {
        Fail-Smoke 'AppxManifest.xml missing runFullTrust capability'
    }

    $executablePath = Join-Path $contentDir $executableRelative
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        Fail-Smoke "expected executable not found: $executableRelative"
    }

    $debugArtifacts = @(Get-ChildItem -LiteralPath $contentDir -Recurse -File -Filter '*.pdb' -ErrorAction SilentlyContinue)
    if ($debugArtifacts.Count -gt 0) {
        Fail-Smoke "MSIX content contains PDB debug artifacts: $($debugArtifacts.FullName -join ', ')"
    }

    $requiredAssets = @(
        'Assets\StoreLogo.png',
        'Assets\Square44x44Logo.png',
        'Assets\Square150x150Logo.png',
        'Assets\Wide310x150Logo.png'
    )

    foreach ($asset in $requiredAssets) {
        $assetPath = Join-Path $contentDir $asset
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            Fail-Smoke "expected MSIX asset not found: $asset"
        }
    }

    $cliSmoke = Join-Path $PSScriptRoot 'cli-smoke.ps1'
    if (-not (Test-Path -LiteralPath $cliSmoke -PathType Leaf)) {
        Fail-Smoke "shared CLI smoke helper not found: $cliSmoke"
    }

    $skipCli = $NoCli -or -not (Test-CliSmokeSupported -Architecture $identity.ProcessorArchitecture)
    if (-not $skipCli) {
        & $cliSmoke -Executable $executablePath
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    Write-Output 'MSIX smoke: OK'
}
finally {
    if ($tempDir -and (Test-Path -LiteralPath $tempDir -PathType Container)) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force
    }
}
