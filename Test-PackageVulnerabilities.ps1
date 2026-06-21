[CmdletBinding()]
param(
    [string]$SolutionPath = "HapticDrive.Asio.sln",
    [ValidateSet("Low", "Moderate", "High", "Critical")]
    [string]$FailOnMinimumSeverity = "High"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetRoot = Join-Path $repoRoot ".dotnet"
$localDotnet = Join-Path $dotnetRoot "dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$resolvedSolutionPath = if ([System.IO.Path]::IsPathRooted($SolutionPath)) {
    $SolutionPath
}
else {
    Join-Path $repoRoot $SolutionPath
}

$severityRank = @{
    Low = 1
    Moderate = 2
    High = 3
    Critical = 4
}

$rawJson = & $dotnet list $resolvedSolutionPath package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$report = $rawJson | ConvertFrom-Json
$minimumRank = $severityRank[$FailOnMinimumSeverity]
$matches = New-Object System.Collections.Generic.List[object]

foreach ($project in @($report.projects)) {
    foreach ($framework in @($project.frameworks)) {
        foreach ($packageGroupName in @("topLevelPackages", "transitivePackages")) {
            foreach ($package in @($framework.$packageGroupName)) {
                foreach ($vulnerability in @($package.vulnerabilities)) {
                    if ($null -eq $vulnerability) {
                        continue
                    }

                    $severity = [string]$vulnerability.severity
                    if (-not $severityRank.ContainsKey($severity)) {
                        continue
                    }

                    if ($severityRank[$severity] -ge $minimumRank) {
                        $matches.Add([pscustomobject]@{
                                Project = $project.path
                                Framework = $framework.framework
                                PackageId = $package.id
                                ResolvedVersion = $package.resolvedVersion
                                Severity = $severity
                                AdvisoryUrl = $vulnerability.advisoryurl
                            })
                    }
                }
            }
        }
    }
}

if ($matches.Count -gt 0) {
    Write-Host "Package vulnerability audit failed."
    $matches | Sort-Object Severity, PackageId, Project | Format-Table -AutoSize
    exit 1
}

Write-Host "Package vulnerability audit passed with no $FailOnMinimumSeverity or higher advisories."
