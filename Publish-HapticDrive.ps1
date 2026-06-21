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
$vulnerabilityScript = Join-Path $repoRoot "Test-PackageVulnerabilities.ps1"
$solution = Join-Path $repoRoot "HapticDrive.Asio.sln"
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
$packageManifestPath = Join-Path $releaseDirectory "$PackageName-$Runtime.package-manifest.json"
$zipStagingDirectory = Join-Path $resolvedOutputRoot "zip-staging\$PackageName-$Runtime"
$requiredFiles =
@(
    "HapticDrive.Asio.App.exe",
    "HapticDrive.Asio.App.dll",
    "HapticDrive.Asio.App.deps.json",
    "HapticDrive.Asio.App.runtimeconfig.json"
)
$documentationFiles = @(
    @{ Source = (Join-Path $repoRoot "README.md"); Destination = "README.md" },
    @{ Source = (Join-Path $repoRoot "docs\QUICK_START.md"); Destination = "QUICK_START.md" },
    @{ Source = (Join-Path $repoRoot "LICENSE.md"); Destination = "LICENSE.md" },
    @{ Source = (Join-Path $repoRoot "RELEASE_STATUS.md"); Destination = "RELEASE_STATUS.md" },
    @{ Source = (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md"); Destination = "THIRD_PARTY_NOTICES.md" }
)

function Copy-ArtifactFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $destinationDirectory = Split-Path -Parent $DestinationPath
    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
}

function Get-RelativeArtifactPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $resolvedBasePath = (Resolve-Path -LiteralPath $BasePath).Path
    $resolvedTargetPath = (Resolve-Path -LiteralPath $TargetPath).Path

    if (-not $resolvedBasePath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedBasePath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [Uri]$resolvedBasePath
    $targetUri = [Uri]$resolvedTargetPath
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
}

if (-not $NoRestore) {
    & $dotnet restore $solution --locked-mode --configfile (Join-Path $repoRoot "NuGet.Config")
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & $dotnet restore $project -r $Runtime --locked-mode --configfile (Join-Path $repoRoot "NuGet.Config")
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

& $vulnerabilityScript -SolutionPath $solution -FailOnMinimumSeverity High
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
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

if (Test-Path $packageManifestPath) {
    Remove-Item -LiteralPath $packageManifestPath -Force
}

if (Test-Path $zipStagingDirectory) {
    Remove-Item -LiteralPath $zipStagingDirectory -Recurse -Force
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
    "--no-restore"
)

& $dotnet @publishArguments

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $NoZip) {
    New-Item -ItemType Directory -Path $zipStagingDirectory -Force | Out-Null

    $publishFiles = Get-ChildItem -LiteralPath $publishDirectory -Recurse -File
    foreach ($file in $publishFiles) {
        if ($file.Extension -ieq ".pdb") {
            continue
        }

        $relativePath = Get-RelativeArtifactPath -BasePath $publishDirectory -TargetPath $file.FullName
        Copy-ArtifactFile -SourcePath $file.FullName -DestinationPath (Join-Path $zipStagingDirectory $relativePath)
    }

    foreach ($documentationFile in $documentationFiles) {
        if (-not (Test-Path $documentationFile.Source)) {
            throw "Required documentation file was not found: $($documentationFile.Source)"
        }

        Copy-ArtifactFile -SourcePath $documentationFile.Source -DestinationPath (Join-Path $zipStagingDirectory $documentationFile.Destination)
    }

    Compress-Archive -Path (Join-Path $zipStagingDirectory '*') -DestinationPath $zipPath -Force

    $zipHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "$($zipHash.Hash) *$([System.IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $checksumPath -NoNewline

    $packageFiles = Get-ChildItem -LiteralPath $zipStagingDirectory -Recurse -File | ForEach-Object { (Get-RelativeArtifactPath -BasePath $zipStagingDirectory -TargetPath $_.FullName).Replace('\', '/') } | Sort-Object -Unique

    $packageManifest = [ordered]@{
        SchemaVersion = 1
        PackageName = $PackageName
        Runtime = $Runtime
        Configuration = $Configuration
        GeneratedUtc = [DateTimeOffset]::UtcNow.ToString("O")
        IncludesPortablePdbs = $false
        RequiredFiles = $requiredFiles
        DocumentationFiles = $documentationFiles.Destination
        PackageFiles = $packageFiles
    }

    $packageManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $packageManifestPath

    $manifest = [ordered]@{
        SchemaVersion = 2
        PackageName = $PackageName
        Runtime = $Runtime
        Configuration = $Configuration
        GeneratedUtc = [DateTimeOffset]::UtcNow.ToString("O")
        PublishFileCount = (Get-ChildItem -LiteralPath $publishDirectory -File).Count
        PackageFileCount = $packageFiles.Count
        RequiredFiles = $requiredFiles
        ZipFileName = [System.IO.Path]::GetFileName($zipPath)
        ZipSizeBytes = (Get-Item -LiteralPath $zipPath).Length
        ZipSha256 = $zipHash.Hash
        ChecksumFileName = [System.IO.Path]::GetFileName($checksumPath)
        ManifestFileName = [System.IO.Path]::GetFileName($manifestPath)
        SummaryFileName = [System.IO.Path]::GetFileName($summaryPath)
        PackageManifestFileName = [System.IO.Path]::GetFileName($packageManifestPath)
        IncludesPortablePdbs = $false
        DocumentationFiles = $documentationFiles.Destination
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
        "- License status: redistribution blocked until owner selects a license"
        ""
        "## Files"
        ""
        "- Zip: $([System.IO.Path]::GetFileName($zipPath))"
        "- SHA-256 file: $([System.IO.Path]::GetFileName($checksumPath))"
        "- Manifest: $([System.IO.Path]::GetFileName($manifestPath))"
        "- Package manifest: $([System.IO.Path]::GetFileName($packageManifestPath))"
        "- Publish file count: $($manifest.PublishFileCount)"
        "- Packaged file count: $($manifest.PackageFileCount)"
        "- Zip size (bytes): $($manifest.ZipSizeBytes)"
        "- Zip SHA-256: $($manifest.ZipSha256)"
        "- Portable PDBs included: $($manifest.IncludesPortablePdbs)"
        ""
        "## Required app payload"
        ""
    )

    foreach ($file in $requiredFiles) {
        $summaryLines += "- $file"
    }

    $summaryLines += @(
        ""
        "## Included documentation"
        ""
    )

    foreach ($documentationFile in $documentationFiles) {
        $summaryLines += "- $($documentationFile.Destination)"
    }

    $summaryLines += @(
        ""
        "## Verification"
        ""
        "- Vulnerability audit: .\Test-PackageVulnerabilities.ps1 -FailOnMinimumSeverity High"
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
    Write-Host "Package manifest: $packageManifestPath"
    Write-Host "Release summary: $summaryPath"
}
