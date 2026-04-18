[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolPath = Join-Path $repoRoot '.tools\dotnet-coverage.exe'
$resultsRoot = Join-Path $repoRoot 'TestResults\Coverage'
$summaryPath = Join-Path $resultsRoot 'coverage-summary.json'

$excludedFileNames = @(
    'SelectionSet.cs',
    'StructuredOperationLogger.cs',
    'WindowsShellService.cs'
)

$testRuns = @(
    @{
        Name = 'Application'
        ResultsDirectory = Join-Path $resultsRoot 'Application'
        TestExe = Join-Path $repoRoot 'tests\WinUiFileManager.Application.Tests\bin\x64\Debug\net10.0-windows10.0.19041.0\WinUiFileManager.Application.Tests.exe'
    },
    @{
        Name = 'Infrastructure'
        ResultsDirectory = Join-Path $resultsRoot 'Infrastructure'
        TestExe = Join-Path $repoRoot 'tests\WinUiFileManager.Infrastructure.Tests\bin\x64\Debug\net10.0-windows10.0.19041.0\WinUiFileManager.Infrastructure.Tests.exe'
    },
    @{
        Name = 'Interop'
        ResultsDirectory = Join-Path $resultsRoot 'Interop'
        TestExe = Join-Path $repoRoot 'tests\WinUiFileManager.Interop.Tests\bin\x64\Debug\net10.0-windows10.0.19041.0\WinUiFileManager.Interop.Tests.exe'
    }
)

function Ensure-CoverageTool {
    if (Test-Path $toolPath) {
        return
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $toolPath) -Force | Out-Null
    & dotnet tool install --tool-path (Split-Path -Parent $toolPath) dotnet-coverage
}

function Collect-Coverage {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Run
    )

    $outputPath = Join-Path $Run.ResultsDirectory 'coverage.cobertura.xml'
    New-Item -ItemType Directory -Path $Run.ResultsDirectory -Force | Out-Null

    $arguments = @(
        'collect'
        $Run.TestExe
        '--results-directory'
        $Run.ResultsDirectory
        '--no-progress'
        '-o'
        $outputPath
        '-f'
        'cobertura'
        '--nologo'
    )

    & $toolPath @arguments
    return $outputPath
}

function Get-FileCoverageRows {
    param(
        [Parameter(Mandatory)]
        [string[]]$CoverageFiles
    )

    $rows = @{}

    foreach ($coverageFile in $CoverageFiles) {
        [xml]$coverageXml = Get-Content $coverageFile

        foreach ($package in $coverageXml.coverage.packages.package) {
            $packageName = [string]$package.name
            if ($packageName -notlike 'WinUiFileManager.*' -or $packageName -like '*.Tests') {
                continue
            }

            foreach ($class in $package.classes.class) {
                $filePath = [string]$class.filename
                $fileName = [System.IO.Path]::GetFileName($filePath)

                if ($filePath -notlike "$repoRoot\src\*") {
                    continue
                }

                if ($filePath -like '*\obj\*' -or $filePath -like '*.g.cs' -or $filePath -like '*.Designer.cs') {
                    continue
                }

                if ($filePath -like '*.xaml' -or $filePath -like '*.xaml.cs') {
                    continue
                }

                if ($excludedFileNames -contains $fileName) {
                    continue
                }

                $lines = @($class.lines.line)
                if ($lines.Count -eq 0) {
                    continue
                }

                if (-not $rows.ContainsKey($filePath)) {
                    $rows[$filePath] = [pscustomobject]@{
                        Package = $packageName
                        File = $filePath
                        LinesCovered = 0
                        LinesValid = 0
                    }
                }

                $coveredLines = @($lines | Where-Object { [int]$_.hits -gt 0 }).Count
                $rows[$filePath].LinesCovered += $coveredLines
                $rows[$filePath].LinesValid += $lines.Count
            }
        }
    }

    return $rows.Values | ForEach-Object {
        [pscustomobject]@{
            Package = $_.Package
            File = $_.File
            LineRate = [math]::Round(($_.LinesCovered / [double]$_.LinesValid) * 100, 2)
            LinesCovered = $_.LinesCovered
            LinesValid = $_.LinesValid
        }
    }
}

function Get-CoverageSummary {
    param(
        [Parameter(Mandatory)]
        [object[]]$CoverageRows
    )

    $linesCovered = ($CoverageRows | Measure-Object -Property LinesCovered -Sum).Sum
    $linesValid = ($CoverageRows | Measure-Object -Property LinesValid -Sum).Sum

    [pscustomobject]@{
        LineRate = if ($linesValid -gt 0) { [math]::Round(($linesCovered / [double]$linesValid) * 100, 2) } else { 0 }
        LinesCovered = $linesCovered
        LinesValid = $linesValid
        CoverageFiles = $coverageFiles
        ExcludedFileNames = $excludedFileNames
    }
}

Push-Location $repoRoot
try {
    Ensure-CoverageTool

    $coverageFiles = foreach ($run in $testRuns) {
        Collect-Coverage -Run $run
    }

    $coverageRows = @(Get-FileCoverageRows -CoverageFiles $coverageFiles)
    $summary = Get-CoverageSummary -CoverageRows $coverageRows

    $leastCoveredFiles = $coverageRows |
        Sort-Object @{ Expression = 'LineRate'; Ascending = $true }, @{ Expression = 'LinesValid'; Ascending = $false }, @{ Expression = 'File'; Ascending = $true } |
        Select-Object -First 10

    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
    [pscustomobject]@{
        Summary = $summary
        LeastCoveredFiles = $leastCoveredFiles
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $summaryPath

    Write-Host 'Coverage summary'
    $summary | Format-List

    Write-Host ''
    Write-Host 'Excluded file names'
    $excludedFileNames | ForEach-Object { Write-Host " - $_" }

    Write-Host ''
    Write-Host 'Least covered files'
    $leastCoveredFiles | Format-Table -AutoSize
    Write-Host ''
    Write-Host "Summary saved to $summaryPath"
}
finally {
    Pop-Location
}
