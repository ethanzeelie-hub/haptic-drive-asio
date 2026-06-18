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

if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
}

if (-not $NoRestore) {
    & $dotnet restore $project -r $Runtime --configfile (Join-Path $repoRoot "NuGet.Config")
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

& $dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $NoZip) {
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -Force
}

Write-Host "Publish complete."
Write-Host "Publish directory: $publishDirectory"
if (-not $NoZip) {
    Write-Host "Zip package: $zipPath"
}
