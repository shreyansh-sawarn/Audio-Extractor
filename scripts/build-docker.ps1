param(
    [string]$ImageName = "audio-extractor"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

docker build -t $ImageName $repoRoot
