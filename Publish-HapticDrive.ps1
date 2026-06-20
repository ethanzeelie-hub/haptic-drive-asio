[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "artifacts",
    [string]$PackageName = "HapticDrive.Asio",
    [switch]$NoRestore,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetRoot = Join-Path $repoRoot ".dotnet"
$localDotnet = Join-Path $dotnetRoot "dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$project = Join-Path $repoRoot "src\HapticDrive.Asio.App\HapticDrive.Asio.App.csproj"
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $repoRoot $OutputRoot
}

$publishDirectory = Join-Path $resolvedOutputRoot "publish\$PackageName-$Runtime"
$releaseDirectory = Join-Path $resolvedOutputRoot "release"
$zipPath = Join-Path $releaseDirectory "$PackageName-$Runtime.zip"
$checksumPath = Join-Path $releaseDirectory "$PackageName-$Runtime.sha256"
$manifestPath = Join-Path $releaseDirectory "$PackageName-$Runtime.manifest.json"
$summaryPath = Join-Path $releaseDirectory "$PackageName-$Runtime.release-summary.md"
$requiredFiles =
@(
    "HapticDrive.Asio.App.exe",
    "HapticDrive.Asio.App.dll",
    "HapticDrive.Asio.App.deps.json",
    "HapticDrive.Asio.App.runtimeconfig.json"
)

if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
}

if (-not $NoRestore) {
    & $dotnet restore $project -r $Runtime --configfile (Join-Path $repoRoot "NuGet.Config") -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (Test-Path $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

if (Test-Path $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
}

if (Test-Path $summaryPath) {
    Remove-Item -LiteralPath $summaryPath -Force
}

$publishArguments = @(
    "publish",
    $project,
    "-c",
    $Configuration,
    "-r",
    $Runtime,
    "--self-contained",
    "false",
    "-o",
    $publishDirectory,
    "--no-restore",
    "-p:NuGetAudit=false"
)

& $dotnet @publishArguments

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $NoZip) {
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -Force

    $zipHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "$($zipHash.Hash) *$([System.IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $checksumPath -NoNewline

    $manifest = [ordered]@{
        PackageName = $PackageName
        Runtime = $Runtime
        Configuration = $Configuration
        GeneratedUtc = [DateTime]::UtcNow.ToString("O")
        PublishDirectory = $publishDirectory
        PublishFileCount = (Get-ChildItem -LiteralPath $publishDirectory -File).Count
        RequiredFiles = $requiredFiles
        ZipPath = $zipPath
        ZipFileName = [System.IO.Path]::GetFileName($zipPath)
        ZipSizeBytes = (Get-Item -LiteralPath $zipPath).Length
        ZipSha256 = $zipHash.Hash
        ChecksumPath = $checksumPath
    }

    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath

    $commitHash = "unknown"
    $commitSubject = "unknown"
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $gitCommand) {
        $commitHashResult = (& $gitCommand.Source rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commitHashResult)) {
            $commitHash = ($commitHashResult | Select-Object -First 1).Trim()
        }

        $commitSubjectResult = (& $gitCommand.Source log -1 --pretty=%s 2>$null)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commitSubjectResult)) {
            $commitSubject = ($commitSubjectResult | Select-Object -First 1).Trim()
        }
    }

    $summaryLines =
    @(
        "# Release Summary"
        ""
        "- Package: $PackageName"
        "- Runtime: $Runtime"
        "- Configuration: $Configuration"
        "- Generated (UTC): $($manifest.GeneratedUtc)"
        "- Commit: $commitHash"
        "- Commit subject: $commitSubject"
        ""
        "## Files"
        ""
        "- Zip: $([System.IO.Path]::GetFileName($zipPath))"
        "- SHA-256 file: $([System.IO.Path]::GetFileName($checksumPath))"
        "- Manifest: $([System.IO.Path]::GetFileName($manifestPath))"
        "- Publish file count: $($manifest.PublishFileCount)"
        "- Zip size (bytes): $($manifest.ZipSizeBytes)"
        "- Zip SHA-256: $($manifest.ZipSha256)"
        ""
        "## Required app payload"
        ""
    )

    foreach ($file in $requiredFiles) {
        $summaryLines += "- $file"
    }

    $summaryLines += @(
        ""
        "## Verification"
        ""
        "- Publish script: .\Publish-HapticDrive.ps1 -Configuration $Configuration -Runtime $Runtime"
        "- Smoke check: .\Test-ReleaseArtifact.ps1 -Runtime $Runtime"
    )

    $summaryLines | Set-Content -LiteralPath $summaryPath
}

Write-Host "Publish complete."
Write-Host "Publish directory: $publishDirectory"
if (-not $NoZip) {
    Write-Host "Zip package: $zipPath"
    Write-Host "Checksum file: $checksumPath"
    Write-Host "Manifest file: $manifestPath"
    Write-Host "Release summary: $summaryPath"
}
