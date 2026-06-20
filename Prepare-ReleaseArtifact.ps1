[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "artifacts",
    [string]$PackageName = "HapticDrive.Asio"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetRoot = Join-Path $repoRoot ".dotnet"
$localDotnet = Join-Path $dotnetRoot "dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$solution = Join-Path $repoRoot "HapticDrive.Asio.sln"
$appProject = Join-Path $repoRoot "src\HapticDrive.Asio.App\HapticDrive.Asio.App.csproj"
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $repoRoot $OutputRoot
}

$releasePrefix = "$PackageName-$Runtime"
$stagingDirectory = Join-Path $resolvedOutputRoot "staged-release\$releasePrefix"
$publishScript = Join-Path $repoRoot "Publish-HapticDrive.ps1"
$smokeScript = Join-Path $repoRoot "Test-ReleaseArtifact.ps1"
$runScript = Join-Path $repoRoot "Run-HapticDrive.ps1"
$releaseDirectory = Join-Path $resolvedOutputRoot "release"
$releaseFiles =
@(
    (Join-Path $releaseDirectory "$releasePrefix.zip")
    (Join-Path $releaseDirectory "$releasePrefix.sha256")
    (Join-Path $releaseDirectory "$releasePrefix.manifest.json")
    (Join-Path $releaseDirectory "$releasePrefix.release-summary.md")
)

if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
}

& $dotnet restore $solution --configfile (Join-Path $repoRoot "NuGet.Config") -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnet restore $appProject -r $Runtime --configfile (Join-Path $repoRoot "NuGet.Config") -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnet build $solution --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnet test $solution --no-build
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnet format $solution --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $runScript -NoBuild -CheckOnly
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $publishScript -Configuration $Configuration -Runtime $Runtime -OutputRoot $resolvedOutputRoot -PackageName $PackageName -NoRestore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $smokeScript -Runtime $Runtime -OutputRoot $resolvedOutputRoot -PackageName $PackageName
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (Test-Path $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
foreach ($file in $releaseFiles) {
    Copy-Item -LiteralPath $file -Destination (Join-Path $stagingDirectory ([System.IO.Path]::GetFileName($file))) -Force
}

Write-Host "Release staging complete."
Write-Host "Staging directory: $stagingDirectory"
foreach ($file in $releaseFiles) {
    Write-Host "Staged file: $(Join-Path $stagingDirectory ([System.IO.Path]::GetFileName($file)))"
}
