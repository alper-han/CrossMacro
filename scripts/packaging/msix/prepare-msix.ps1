[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help,

    [Parameter(Mandatory = $true, ParameterSetName = 'Build')]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [Parameter(ParameterSetName = 'Build')]
    [string]$ManifestPath = "",

    [Parameter(ParameterSetName = 'Build')]
    [string]$AssetsPath = "",

    [Parameter(ParameterSetName = 'Build')]
    [string]$OutputDir = "",

    [Parameter(ParameterSetName = 'Build')]
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"

function Show-Usage {
    @'
Usage: prepare-msix.ps1 -Version <version> [-ManifestPath <path>] [-AssetsPath <path>] [-OutputDir <path>] [-Architecture <x64|arm64>] [-Help]

Stages static MSIX metadata for CrossMacro:
  - copies MSIX assets into the staging directory
  - writes AppxManifest.xml with the requested package version and architecture

Options:
  -Version <version>         Three-part package version written as <version>.0.
  -ManifestPath <path>       Source AppxManifest.xml. Defaults to scripts/msix/AppxManifest.xml.
  -AssetsPath <path>         Source MSIX assets directory. Defaults to scripts/msix/Assets.
  -OutputDir <path>          Staged MSIX content directory. Defaults to <repo>/artifacts/work/msix/content.
  -Architecture <x64|arm64>  ProcessorArchitecture written to the manifest. Defaults to x64.
  -Help                      Show this help.
'@
}

if ($Help) {
    Show-Usage
    exit 0
}

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ScriptsDir = Resolve-Path -LiteralPath (Join-Path $ScriptDir "../..")
$ProjectRoot = Resolve-Path -LiteralPath (Join-Path $ScriptsDir "..")

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $ScriptsDir "msix/AppxManifest.xml"
}

if ([string]::IsNullOrWhiteSpace($AssetsPath)) {
    $AssetsPath = Join-Path $ScriptsDir "msix/Assets"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $ProjectRoot "artifacts/work/msix/content"
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Manifest file not found: $ManifestPath"
}

if (-not (Test-Path -LiteralPath $AssetsPath -PathType Container)) {
    throw "Assets directory not found: $AssetsPath"
}

$msixVersion = "$Version.0"
$Architecture = $Architecture.ToLowerInvariant()

$stagedAssetsPath = Join-Path $OutputDir "Assets"
$manifestOutputPath = Join-Path $OutputDir "AppxManifest.xml"
$resolvedAssetsPath = [System.IO.Path]::GetFullPath($AssetsPath)
$resolvedStagedAssetsPath = [System.IO.Path]::GetFullPath($stagedAssetsPath)
$resolvedManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$resolvedManifestOutputPath = [System.IO.Path]::GetFullPath($manifestOutputPath)

if ([System.StringComparer]::OrdinalIgnoreCase.Equals($resolvedAssetsPath, $resolvedStagedAssetsPath)) {
    throw "OutputDir cannot replace the source MSIX assets directory: $OutputDir"
}

if ([System.StringComparer]::OrdinalIgnoreCase.Equals($resolvedManifestPath, $resolvedManifestOutputPath)) {
    throw "OutputDir cannot replace the source MSIX manifest: $OutputDir"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Keep repeated preparation safe without deleting caller-owned files in the
# output directory. Only the two files owned by this staging operation are
# replaced.
if (Test-Path -LiteralPath $stagedAssetsPath -PathType Container) {
    Remove-Item -LiteralPath $stagedAssetsPath -Recurse -Force
}

if (Test-Path -LiteralPath $manifestOutputPath -PathType Leaf) {
    Remove-Item -LiteralPath $manifestOutputPath -Force
}

Copy-Item -Path $AssetsPath -Destination $stagedAssetsPath -Recurse -Force

[xml]$manifest = Get-Content -LiteralPath $ManifestPath -Raw
$namespaceManager = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$namespaceManager.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")

$identity = $manifest.SelectSingleNode("/appx:Package/appx:Identity", $namespaceManager)
if ($null -eq $identity) {
    throw "Identity element not found in manifest: $ManifestPath"
}

$identity.SetAttribute("Version", $msixVersion)
$identity.SetAttribute("ProcessorArchitecture", $Architecture)

$writerSettings = New-Object System.Xml.XmlWriterSettings
$writerSettings.Encoding = New-Object System.Text.UTF8Encoding($false)
$writerSettings.Indent = $true
$writerSettings.NewLineChars = "`n"
$writerSettings.NewLineHandling = [System.Xml.NewLineHandling]::Replace

$writer = [System.Xml.XmlWriter]::Create($manifestOutputPath, $writerSettings)
try {
    $manifest.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Host "Prepared MSIX content with Identity Version $msixVersion and ProcessorArchitecture $Architecture at $OutputDir"
