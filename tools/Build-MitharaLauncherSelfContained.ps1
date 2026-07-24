param(
    [string]$OutputDirectory = '',
    [string]$ArchiveName = 'MitharaLauncher_VPS_SelfContained.zip'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'release'
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$publishDir = Join-Path $outputRoot 'LauncherVPS_SC'
$archivePath = Join-Path $outputRoot $ArchiveName
$packagesPath = Join-Path $env:USERPROFILE '.nuget\packages'

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

dotnet publish (Join-Path $root 'launcher\Mithara.Launcher.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:RestorePackagesPath="$packagesPath" `
    -p:RestoreIgnoreFailedSources=true `
    -o $publishDir

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archivePath -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash

Write-Host "Launcher self-contained criado: $archivePath"
Write-Host "SHA-256: $hash"
