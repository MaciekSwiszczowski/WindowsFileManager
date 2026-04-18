[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$rootPath = 'C:\temp\tests'
$fileCount = 10000
$fileSizeBytes = 1024

New-Item -ItemType Directory -Path $rootPath -Force | Out-Null

$buffer = New-Object byte[] $fileSizeBytes
$random = [System.Random]::new(42)
$random.NextBytes($buffer)

for ($index = 1; $index -le $fileCount; $index++) {
    $filePath = Join-Path $rootPath ('file-{0:D4}.bin' -f $index)
    [System.IO.File]::WriteAllBytes($filePath, $buffer)
}

Write-Host "Created $fileCount files of $fileSizeBytes bytes under $rootPath"
