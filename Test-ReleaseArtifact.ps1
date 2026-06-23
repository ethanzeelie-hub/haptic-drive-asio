[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "artifacts",
    [string]$PackageName = "HapticDrive.Asio",
    [string]$ExtractRoot = "artifacts\smoke"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $repoRoot $OutputRoot
}

$resolvedExtractRoot = if ([System.IO.Path]::IsPathRooted($ExtractRoot)) {
    $ExtractRoot
}
else {
    Join-Path $repoRoot $ExtractRoot
}

$publishDirectory = Join-Path $resolvedOutputRoot "publish\$PackageName-$Runtime"
$zipPath = Join-Path $resolvedOutputRoot "release\$PackageName-$Runtime.zip"
$checksumPath = Join-Path $resolvedOutputRoot "release\$PackageName-$Runtime.sha256"
$manifestPath = Join-Path $resolvedOutputRoot "release\$PackageName-$Runtime.manifest.json"
$summaryPath = Join-Path $resolvedOutputRoot "release\$PackageName-$Runtime.release-summary.md"
$packageManifestPath = Join-Path $resolvedOutputRoot "release\$PackageName-$Runtime.package-manifest.json"
$extractDirectory = Join-Path $resolvedExtractRoot "$PackageName-$Runtime"
$requiredFiles =
@(
    "HapticDrive.Asio.App.exe",
    "HapticDrive.Asio.App.dll",
    "HapticDrive.Asio.App.deps.json",
    "HapticDrive.Asio.App.runtimeconfig.json"
)
$requiredDocumentationFiles =
@(
    "README.md",
    "QUICK_START.md",
    "LICENSE.md",
    "RELEASE_STATUS.md",
    "THIRD_PARTY_NOTICES.md"
)

function Assert-RequiredFilesPresent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path $DirectoryPath)) {
        throw "$Label was not found at $DirectoryPath"
    }

    foreach ($file in $requiredFiles) {
        $candidate = Join-Path $DirectoryPath $file
        if (-not (Test-Path $candidate)) {
            throw "$Label is missing required file $file"
        }
    }
}

Assert-RequiredFilesPresent -DirectoryPath $publishDirectory -Label "Publish directory"

if (-not (Test-Path $zipPath)) {
    throw "Release zip was not found at $zipPath"
}

if (-not (Test-Path $checksumPath)) {
    throw "Release checksum was not found at $checksumPath"
}

if (-not (Test-Path $manifestPath)) {
    throw "Release manifest was not found at $manifestPath"
}

if (-not (Test-Path $summaryPath)) {
    throw "Release summary was not found at $summaryPath"
}

if (-not (Test-Path $packageManifestPath)) {
    throw "Package manifest was not found at $packageManifestPath"
}

$manifestJsonText = Get-Content -LiteralPath $manifestPath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$packageManifestJsonText = Get-Content -LiteralPath $packageManifestPath -Raw
$packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw | ConvertFrom-Json
$summaryText = Get-Content -LiteralPath $summaryPath -Raw

if ($manifest.PackageName -ne $PackageName) {
    throw "Release manifest package name '$($manifest.PackageName)' did not match expected '$PackageName'"
}

if ($manifest.Runtime -ne $Runtime) {
    throw "Release manifest runtime '$($manifest.Runtime)' did not match expected '$Runtime'"
}

if ($manifest.Configuration -ne $Configuration) {
    throw "Release manifest configuration '$($manifest.Configuration)' did not match expected '$Configuration'"
}

if ($manifest.RuntimeIdentifier -ne $Runtime) {
    throw "Release manifest runtime identifier '$($manifest.RuntimeIdentifier)' did not match expected '$Runtime'"
}

$zipHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$checksumLine = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
$expectedChecksumLine = "$($zipHash.Hash) *$([System.IO.Path]::GetFileName($zipPath))"

if ($checksumLine -ne $expectedChecksumLine) {
    throw "Release checksum content did not match the actual zip hash"
}

if ($manifest.ZipSha256 -ne $zipHash.Hash) {
    throw "Release manifest zip SHA256 did not match the actual zip hash"
}

if ($manifest.PackageSha256 -ne $zipHash.Hash) {
    throw "Release manifest package SHA256 did not match the actual zip hash"
}

if ($manifest.ZipFileName -ne [System.IO.Path]::GetFileName($zipPath)) {
    throw "Release manifest zip file name '$($manifest.ZipFileName)' did not match expected '$([System.IO.Path]::GetFileName($zipPath))'"
}

if ($manifestJsonText.IndexOf($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw "Release manifest must not contain the absolute repository path"
}

if ($packageManifestJsonText.IndexOf($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw "Package manifest must not contain the absolute repository path"
}

if ($manifest.IncludesPortablePdbs -ne $false) {
    throw "Release manifest must report portable PDBs as excluded"
}

$packageFiles = @($packageManifest.PackageFiles)
if ($packageFiles.Count -eq 0) {
    throw "Package manifest did not contain any packaged files"
}

foreach ($documentationFile in $requiredDocumentationFiles) {
    if ($packageFiles -notcontains $documentationFile) {
        throw "Package manifest is missing expected documentation file $documentationFile"
    }
}

if ($packageFiles -notcontains "HapticDrive.Asio.App.exe") {
    throw "Package manifest is missing the exact packaged executable HapticDrive.Asio.App.exe"
}

$packagedPdbs = $packageFiles | Where-Object { $_ -like "*.pdb" }
if ($packagedPdbs.Count -gt 0) {
    throw "Package manifest must not include portable PDBs: $($packagedPdbs -join ', ')"
}

$requiredSummaryTerms =
@(
    "# Release Summary",
    "- Package: $PackageName",
    "- Runtime: $Runtime",
    "- Zip: $([System.IO.Path]::GetFileName($zipPath))",
    "- SHA-256 file: $([System.IO.Path]::GetFileName($checksumPath))",
    "- Manifest: $([System.IO.Path]::GetFileName($manifestPath))",
    "- Zip SHA-256: $($manifest.ZipSha256)"
)

foreach ($term in $requiredSummaryTerms) {
    if ($summaryText.IndexOf($term, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Release summary was missing expected content: $term"
    }
}

if (Test-Path $extractDirectory) {
    Remove-Item -LiteralPath $extractDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $extractDirectory -Force | Out-Null
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDirectory -Force
Assert-RequiredFilesPresent -DirectoryPath $extractDirectory -Label "Extracted release zip"

$packagedExecutable = Join-Path $extractDirectory "HapticDrive.Asio.App.exe"
if (-not (Test-Path $packagedExecutable)) {
    throw "Extracted release zip did not contain the exact packaged executable at $packagedExecutable"
}

foreach ($documentationFile in $requiredDocumentationFiles) {
    $documentationPath = Join-Path $extractDirectory $documentationFile
    if (-not (Test-Path $documentationPath)) {
        throw "Extracted release zip is missing documentation file $documentationFile"
    }
}

$zipPdbs = Get-ChildItem -LiteralPath $extractDirectory -Recurse -Filter *.pdb
if ($zipPdbs.Count -gt 0) {
    throw "Extracted release zip must not include portable PDBs"
}

$publishFiles = Get-ChildItem -Path $publishDirectory -File | Select-Object -ExpandProperty Name
$extractedFiles = Get-ChildItem -Path $extractDirectory -File | Select-Object -ExpandProperty Name
Write-Host "Release artifact smoke check passed."
Write-Host "Publish directory: $publishDirectory"
Write-Host "Zip package: $zipPath"
Write-Host "Checksum file: $checksumPath"
Write-Host "Manifest file: $manifestPath"
Write-Host "Package manifest file: $packageManifestPath"
Write-Host "Release summary: $summaryPath"
Write-Host "Extracted directory: $extractDirectory"
Write-Host "Publish file count: $($publishFiles.Count)"
Write-Host "Extracted file count: $($extractedFiles.Count)"
