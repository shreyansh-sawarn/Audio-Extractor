param(
    [string]$InputPath = ".\input",
    [string]$OutputPath = ".\output",
    [string]$ImageName = "audio-extractor",
    [switch]$Build,
    [switch]$Recursive
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if ($Build) {
    docker build -t $ImageName $repoRoot
}

if (-not (Test-Path $InputPath)) {
    New-Item -ItemType Directory -Path $InputPath | Out-Null
}

if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
}

$inputFullPath = (Resolve-Path $InputPath).Path
$outputFullPath = (Resolve-Path $OutputPath).Path
$arguments = @("--output", "/output", "/input")

if ($Recursive) {
    $arguments = @("--recursive") + $arguments
}

docker run --rm `
    -v "${inputFullPath}:/input:ro" `
    -v "${outputFullPath}:/output" `
    $ImageName `
    @arguments
