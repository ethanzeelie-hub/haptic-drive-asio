[CmdletBinding()]
param(
    [string]$SearchRoot = "artifacts\TestResults",
    [double]$MinimumLineCoverage = 80.0,
    [string[]]$ExcludedPackages = @("HapticDrive.Asio.App")
)

$ErrorActionPreference = "Stop"

function Normalize-CoverageFileName {
    param(
        [string]$PackageName,
        [string]$FileName
    )

    $normalized = if ($null -eq $FileName) {
        [string]::Empty
    }
    else {
        [string]$FileName
    }

    $normalized = $normalized.Replace('\', '/').Trim()
    while ($normalized.StartsWith('./', [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }

    if ($normalized.StartsWith("$PackageName/", [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring($PackageName.Length + 1)
    }

    return $normalized
}

function Test-IsGeneratedCoverageFile {
    param(
        [string]$NormalizedFileName
    )

    if ([string]::IsNullOrWhiteSpace($NormalizedFileName)) {
        return $true
    }

    if ($NormalizedFileName.IndexOf('/obj/', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
    }

    if ($NormalizedFileName.EndsWith('.g.cs', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ($NormalizedFileName.EndsWith('.g.i.cs', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $false
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedSearchRoot = if ([System.IO.Path]::IsPathRooted($SearchRoot)) {
    $SearchRoot
}
else {
    Join-Path $repoRoot $SearchRoot
}

if (-not (Test-Path $resolvedSearchRoot)) {
    throw "Coverage search root was not found: $resolvedSearchRoot"
}

$coverageFiles = Get-ChildItem -LiteralPath $resolvedSearchRoot -Recurse -Filter coverage.cobertura.xml
if ($coverageFiles.Count -eq 0) {
    throw "No coverage.cobertura.xml files were found under $resolvedSearchRoot"
}

$mergedLineHits = @{}

foreach ($coverageFile in $coverageFiles) {
    [xml]$coverageXml = Get-Content -LiteralPath $coverageFile.FullName -Raw

    foreach ($packageNode in @($coverageXml.coverage.packages.package)) {
        $packageName = [string]$packageNode.name
        if ($ExcludedPackages -contains $packageName) {
            continue
        }

        foreach ($classNode in @($packageNode.classes.class)) {
            $fileName = Normalize-CoverageFileName -PackageName $packageName -FileName ([string]$classNode.filename)
            if (Test-IsGeneratedCoverageFile -NormalizedFileName $fileName) {
                continue
            }

            foreach ($lineNode in @($classNode.lines.line)) {
                $key = "$packageName|$fileName|$($lineNode.number)"
                $hits = [int]$lineNode.hits
                if ($mergedLineHits.ContainsKey($key)) {
                    if ($hits -gt $mergedLineHits[$key]) {
                        $mergedLineHits[$key] = $hits
                    }
                }
                else {
                    $mergedLineHits[$key] = $hits
                }
            }
        }
    }
}

if ($mergedLineHits.Count -le 0) {
    throw "Coverage files reported zero valid lines."
}

$totalLinesValid = [double]$mergedLineHits.Count
$totalLinesCovered = [double]@($mergedLineHits.GetEnumerator() | Where-Object { $_.Value -gt 0 }).Count
$lineCoverage = ($totalLinesCovered / $totalLinesValid) * 100.0
$lineCoverageText = $lineCoverage.ToString("0.00", [System.Globalization.CultureInfo]::InvariantCulture)
$minimumCoverageText = $MinimumLineCoverage.ToString("0.00", [System.Globalization.CultureInfo]::InvariantCulture)

Write-Host "Aggregated line coverage: $lineCoverageText% ($totalLinesCovered / $totalLinesValid)"
if ($ExcludedPackages.Count -gt 0) {
    Write-Host "Excluded packages: $($ExcludedPackages -join ', ')"
}

if ($lineCoverage -lt $MinimumLineCoverage) {
    throw "Line coverage $lineCoverageText% was below the required minimum of $minimumCoverageText%."
}

Write-Host "Coverage gate passed."
