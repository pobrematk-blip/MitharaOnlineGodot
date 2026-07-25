param(
    [string]$Version = '',
    [string]$OutputDirectory = '',
    [switch]$IncludeClientAssets
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format 'yyyyMMdd-HHmm'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'release'
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$packageName = "MitharaServer_VPS_$Version"
$packageRoot = Join-Path $outputRoot $packageName
$serverOut = Join-Path $packageRoot 'Servidor'
$archivePath = Join-Path $outputRoot "$packageName.zip"
$packagesPath = Join-Path $env:USERPROFILE '.nuget\packages'

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

New-Item -ItemType Directory -Path $serverOut -Force | Out-Null

dotnet publish (Join-Path $root 'server\Mithara.Server.csproj') `
    -c Release `
    -p:RestorePackagesPath="$packagesPath" `
    -p:RestoreIgnoreFailedSources=true `
    -o $serverOut

$publishedConfig = Join-Path $serverOut 'server_config.json'
if (Test-Path -LiteralPath $publishedConfig) {
    $json = Get-Content -LiteralPath $publishedConfig -Raw | ConvertFrom-Json
    $json.PgPassword = 'CHANGE_ME'
    $json | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $publishedConfig -Encoding UTF8
}

$dataOut = Join-Path $serverOut 'data'
New-Item -ItemType Directory -Path $dataOut -Force | Out-Null

$serverData = Join-Path $root 'server\data'
$tiles = Join-Path $serverData 'tiles'
$quests = Join-Path $serverData 'quests.json'

if (Test-Path -LiteralPath $tiles) {
    Copy-Item -LiteralPath $tiles -Destination (Join-Path $dataOut 'tiles') -Recurse -Force
}
if (Test-Path -LiteralPath $quests) {
    Copy-Item -LiteralPath $quests -Destination (Join-Path $dataOut 'quests.json') -Force
}

$skillSubfolders = @('habilidades', 'ArvoresClasses')
foreach ($subfolder in $skillSubfolders) {
    $source = Join-Path $root "skills\$subfolder"
    if (Test-Path -LiteralPath $source) {
        $destination = Join-Path $packageRoot "skills\$subfolder"
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
    }
}

if ($IncludeClientAssets) {
    $assetFolders = @('Itens', 'resources', 'scenes', 'Classes', 'Racas', 'Faccoes', 'audio')
    foreach ($folder in $assetFolders) {
        $source = Join-Path $root $folder
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $packageRoot $folder) -Recurse -Force
        }
    }
}

@"
MITHARA ONLINE - PACOTE DO SERVIDOR VPS

Versao do pacote: $Version

Como atualizar na VPS:

1. Pare o servidor antigo.
2. Extraia este pacote na VPS.
3. Copie/substitua o conteudo da pasta Servidor em:
   C:\MitharaServer\Servidor
4. Mantenha o PostgreSQL e o banco mithara_db como estao.
5. Configure a senha do PostgreSQL antes de iniciar:

   setx MITHARA_PG_PASSWORD "SUA_SENHA_DO_POSTGRES"

   Feche e abra o terminal de novo depois do setx.

6. Inicie o servidor:

   cd C:\MitharaServer\Servidor
   dotnet Mithara.Server.dll

Por padrao este pacote leva apenas o servidor e os dados online necessarios.
Use -IncludeClientAssets apenas se precisar montar uma VPS nova do zero com assets visuais tambem.

Nao envie nem restaure as pastas .git, .godot, build, release, bin, obj, tmp ou server\data\mysql.
"@ | Set-Content -LiteralPath (Join-Path $packageRoot 'LEIA-ME-ATUALIZAR-VPS.txt') -Encoding UTF8

Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
$size = (Get-Item -LiteralPath $archivePath).Length

Write-Host "Pacote do servidor criado: $archivePath"
Write-Host "Tamanho: $([math]::Round($size / 1MB, 1)) MB"
Write-Host "SHA-256: $hash"
