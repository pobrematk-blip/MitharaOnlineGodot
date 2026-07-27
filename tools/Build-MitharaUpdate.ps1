param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$GameDirectory = '',
    [string]$OutputDirectory = '',
    [string]$ArchiveName = 'MitharaOnline_Update.zip',
    [string]$GameExecutable = 'MitharaOnlineTeste.exe',
    [switch]$AllowDirty,
    [switch]$SkipFreshnessCheck
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    $GameDirectory = Join-Path $PSScriptRoot '..\build'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot '..\release'
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gameRoot = [IO.Path]::GetFullPath($GameDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath $gameRoot -PathType Container)) {
    throw "Pasta do jogo nao encontrada: $gameRoot"
}

$gameExecutablePath = Join-Path $gameRoot $GameExecutable
if (-not (Test-Path -LiteralPath $gameExecutablePath -PathType Leaf)) {
    throw "Executavel nao encontrado: $GameExecutable"
}

if (-not $AllowDirty) {
    $gitStatus = git -C $root status --porcelain --untracked-files=no
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel verificar o Git antes de criar o update."
    }

    if (-not [string]::IsNullOrWhiteSpace(($gitStatus | Out-String))) {
        throw @"
Existem alteracoes modificadas fora do commit. O update foi bloqueado para evitar publicar conteudo incompleto.

Arquivos alterados:
$($gitStatus -join "`n")

Faca commit das correcoes ou rode com -AllowDirty somente para teste local.
"@
    }
}

if (-not $SkipFreshnessCheck) {
    $pckPath = [IO.Path]::ChangeExtension($gameExecutablePath, '.pck')
    if (-not (Test-Path -LiteralPath $pckPath -PathType Leaf)) {
        throw "Arquivo .pck nao encontrado ao lado do executavel: $pckPath"
    }

    $pckTime = (Get-Item -LiteralPath $pckPath).LastWriteTimeUtc
    $ignoredRoots = @(
        '\.git\', '\.godot\', '\build\', '\release\', '\bin\', '\obj\', '\tmp\',
        '\.agents\', '\.codex\', '\.nuget_packages\', '\.godot_export_home\', '\.godot_export_local\'
    )

    $sourceFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Force |
        Where-Object {
            $full = $_.FullName
            -not ($ignoredRoots | Where-Object { $full -like "*$_*" }) -and
            $_.Extension -notin @('.zip', '.log')
        }

    $newerSources = $sourceFiles |
        Where-Object { $_.LastWriteTimeUtc -gt $pckTime.AddSeconds(2) } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 25 FullName, LastWriteTime

    if ($newerSources) {
        $list = ($newerSources | ForEach-Object {
            $relative = $_.FullName.Substring($root.Length).TrimStart('\', '/')
            " - $relative ($($_.LastWriteTime))"
        }) -join "`n"

        throw @"
O export do Godot esta mais antigo que arquivos do projeto. O update foi bloqueado.

Exporte novamente o preset Windows para build\$GameExecutable antes de gerar o manifest.

Arquivos mais novos que o .pck:
$list
"@
    }
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
