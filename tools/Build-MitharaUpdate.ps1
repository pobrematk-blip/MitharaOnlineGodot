param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$GameDirectory = '',
    [string]$OutputDirectory = '',
    [string]$ArchiveName = 'MitharaOnline_Update.zip',
    [string]$GameExecutable = 'MitharaOnlineTeste.exe'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    $GameDirectory = Join-Path $PSScriptRoot '..\build'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot '..\release'
}
$gameRoot = [IO.Path]::GetFullPath($GameDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath $gameRoot -PathType Container)) {
    throw "Pasta do jogo não encontrada: $gameRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot $GameExecutable) -PathType Leaf)) {
    throw "Executável não encontrado: $GameExecutable"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$archivePath = Join-Path $outputRoot $ArchiveName
$manifestPath = Join-Path $outputRoot 'manifest.json'

$files = Get-ChildItem -LiteralPath $gameRoot -Recurse -File | Sort-Object FullName
$manifestFiles = foreach ($file in $files) {
    $rootPrefix = $gameRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $relative = $file.FullName.Substring($rootPrefix.Length).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    [ordered]@{
        path = $relative
        size = $file.Length
        sha256 = $hash
    }
}

Compress-Archive -Path (Join-Path $gameRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal -Force
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash

$manifest = [ordered]@{
    version = $Version
    archive = $ArchiveName
    archiveSha256 = $archiveHash
    gameExecutable = $GameExecutable
    files = @($manifestFiles)
    remove = @()
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Pacote criado: $archivePath"
Write-Host "Manifesto criado: $manifestPath"
Write-Host "SHA-256: $archiveHash"
