[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$CheckOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetRoot = Join-Path $repoRoot ".dotnet"
$dotnet = Join-Path $dotnetRoot "dotnet.exe"
$solution = Join-Path $repoRoot "HapticDrive.Asio.sln"
$appExe = Join-Path $repoRoot "src\HapticDrive.Asio.App\bin\$Configuration\net8.0-windows\HapticDrive.Asio.App.exe"

if (-not (Test-Path $dotnet)) {
    Write-Host "Local .NET SDK was not found at $dotnet"
    Write-Host "Restore the repo-local .NET SDK before launching Haptic Drive ASIO."
    exit 1
}

$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"

$runtimes = & $dotnet --list-runtimes
$desktopRuntime = $runtimes | Where-Object { $_ -like "Microsoft.WindowsDesktop.App 8.*" }
if (-not $desktopRuntime) {
    Write-Host ".NET 8 Desktop Runtime was not found in the repo-local runtime."
    Write-Host "Install the x64 .NET 8 Desktop Runtime from Microsoft or restore the repo-local runtime:"
    Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}

if (-not $NoBuild) {
    & $dotnet build $solution -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $appExe)) {
    Write-Host "The app executable was not found at $appExe"
    Write-Host "Run this script without -NoBuild once, or build the solution first."
    exit 1
}

if ($CheckOnly) {
    Write-Host "Haptic Drive ASIO launch preflight passed."
    Write-Host "Executable: $appExe"
    exit 0
}

Start-Process -FilePath $appExe -WorkingDirectory (Split-Path -Parent $appExe)
