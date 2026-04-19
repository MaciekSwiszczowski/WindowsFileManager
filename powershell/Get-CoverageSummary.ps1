[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolPath = Join-Path $repoRoot '.tools\dotnet-coverage.exe'
$resultsRoot = Join-Path $repoRoot 'TestResults\Coverage'
$summaryPath = Join-Path $resultsRoot 'coverage-summary.json'
$solutionPath = Join-Path $repoRoot 'WinUiFileManager.sln'

$excludedFileNames = @()

$testRuns = @(
    @{
        Name = 'Application'
        ResultsDirectory = Join-Path $resultsRoot 'Application'
        ProjectPath = Join-Path $repoRoot 'tests\WinUiFileManager.Application.Tests\WinUiFileManager.Application.Tests.csproj'
        AssemblyName = 'WinUiFileManager.Application.Tests'
    },
    @{
        Name = 'Infrastructure'
        ResultsDirectory = Join-Path $resultsRoot 'Infrastructure'
        ProjectPath = Join-Path $repoRoot 'tests\WinUiFileManager.Infrastructure.Tests\WinUiFileManager.Infrastructure.Tests.csproj'
        AssemblyName = 'WinUiFileManager.Infrastructure.Tests'
    },
    @{
        Name = 'Interop'
        ResultsDirectory = Join-Path $resultsRoot 'Interop'
        ProjectPath = Join-Path $repoRoot 'tests\WinUiFileManager.Interop.Tests\WinUiFileManager.Interop.Tests.csproj'
        AssemblyName = 'WinUiFileManager.Interop.Tests'
    }
)

function Ensure-CoverageTool {
    if (Test-Path $toolPath) {
        return
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $toolPath) -Force | Out-Null
    & dotnet tool install --tool-path (Split-Path -Parent $toolPath) dotnet-coverage
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList,

        [string]$FailureMessage = 'Command failed.'
    )

    $commandOutput = & $FilePath @ArgumentList 2>&1
    foreach ($line in @($commandOutput)) {
        Write-Host $line
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

function Resolve-TestExecutable {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Run
    )

    $projectDirectory = Split-Path -Parent $Run.ProjectPath
    $binDirectory = Join-Path $projectDirectory 'bin'

    $candidateExecutables = @(
        Get-ChildItem -Path $binDirectory -Recurse -File -Filter "$($Run.AssemblyName).exe" -ErrorAction SilentlyContinue |
            Sort-Object -Property LastWriteTimeUtc, FullName -Descending
    )

    if ($candidateExecutables.Count -eq 0) {
        throw "Coverage collection cannot continue because the test executable for $($Run.Name) was not produced under $binDirectory."
    }

    return $candidateExecutables[0].FullName
}

function Ensure-BuildArtifacts {
    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

    Invoke-CheckedCommand `
        -FilePath 'dotnet' `
        -ArgumentList @(
            'build'
            $solutionPath
            '-v'
            'minimal'
            '-nologo'
            '-m:1'
            '/nodeReuse:false'
            '-p:NuGetAudit=false'
        ) `
        -FailureMessage 'Solution build failed before coverage collection.'

    foreach ($run in $testRuns) {
        $run.TestExe = Resolve-TestExecutable -Run $run
    }
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

    if (-not (Test-Path $Run.TestExe)) {
        throw "Missing test executable for $($Run.Name): $($Run.TestExe)"
    }

    if (Test-Path $outputPath) {
        Remove-Item -LiteralPath $outputPath -Force
    }

    Invoke-CheckedCommand -FilePath $toolPath -ArgumentList $arguments -FailureMessage "Coverage collection failed for $($Run.Name)."

    if (-not (Test-Path $outputPath)) {
        throw "Coverage collection did not produce an output file for $($Run.Name): $outputPath"
    }

    return $outputPath
}

function Get-FileCoverageRows {
    param(
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$CoverageFiles = @()
    )

    $CoverageFiles = @($CoverageFiles | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path $_) })
    if ($CoverageFiles.Count -eq 0) {
        throw 'No coverage files were produced. Build and collection must succeed before coverage can be summarized.'
    }

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
    Ensure-BuildArtifacts

    $coverageFiles = foreach ($run in $testRuns) {
        $coverageFile = Collect-Coverage -Run $run
        if (-not [string]::IsNullOrWhiteSpace($coverageFile)) {
            $coverageFile
        }
    }
    $coverageFiles = @($coverageFiles | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path $_) })

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
