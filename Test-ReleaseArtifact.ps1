[CmdletBinding()]
param(
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
$extractDirectory = Join-Path $resolvedExtractRoot "$PackageName-$Runtime"
$requiredFiles =
@(
    "HapticDrive.Asio.App.exe",
    "HapticDrive.Asio.App.dll",
    "HapticDrive.Asio.App.deps.json",
    "HapticDrive.Asio.App.runtimeconfig.json"
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

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ($manifest.PackageName -ne $PackageName) {
    throw "Release manifest package name '$($manifest.PackageName)' did not match expected '$PackageName'"
}

if ($manifest.Runtime -ne $Runtime) {
    throw "Release manifest runtime '$($manifest.Runtime)' did not match expected '$Runtime'"
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

if ($manifest.ZipFileName -ne [System.IO.Path]::GetFileName($zipPath)) {
    throw "Release manifest zip file name '$($manifest.ZipFileName)' did not match expected '$([System.IO.Path]::GetFileName($zipPath))'"
}

if (Test-Path $extractDirectory) {
    Remove-Item -LiteralPath $extractDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $extractDirectory -Force | Out-Null
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDirectory -Force
Assert-RequiredFilesPresent -DirectoryPath $extractDirectory -Label "Extracted release zip"

$publishFiles = Get-ChildItem -Path $publishDirectory -File | Select-Object -ExpandProperty Name
$extractedFiles = Get-ChildItem -Path $extractDirectory -File | Select-Object -ExpandProperty Name
Write-Host "Release artifact smoke check passed."
Write-Host "Publish directory: $publishDirectory"
Write-Host "Zip package: $zipPath"
Write-Host "Checksum file: $checksumPath"
Write-Host "Manifest file: $manifestPath"
Write-Host "Extracted directory: $extractDirectory"
Write-Host "Publish file count: $($publishFiles.Count)"
Write-Host "Extracted file count: $($extractedFiles.Count)"
