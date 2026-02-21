[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [Parameter()]
    [string]$ManifestPath = "scripts/msix/AppxManifest.xml",

    [Parameter()]
    [string]$AssetsPath = "scripts/msix/Assets",

    [Parameter()]
    [string]$OutputDir = "msix-content"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Manifest file not found: $ManifestPath"
}

if (-not (Test-Path -LiteralPath $AssetsPath -PathType Container)) {
    throw "Assets directory not found: $AssetsPath"
}

$msixVersion = "$Version.0"

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Copy-Item -Path $AssetsPath -Destination (Join-Path $OutputDir "Assets") -Recurse -Force

[xml]$manifest = Get-Content -LiteralPath $ManifestPath -Raw
$namespaceManager = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$namespaceManager.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")

$identity = $manifest.SelectSingleNode("/appx:Package/appx:Identity", $namespaceManager)
if ($null -eq $identity) {
    throw "Identity element not found in manifest: $ManifestPath"
}

$identity.SetAttribute("Version", $msixVersion)

$manifestOutputPath = Join-Path $OutputDir "AppxManifest.xml"
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

Write-Host "Prepared MSIX content with Identity Version $msixVersion at $OutputDir"
