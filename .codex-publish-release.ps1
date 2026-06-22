$ErrorActionPreference = 'Stop'

$owner = 'pobrematk-blip'
$repository = 'MitharaOnlineGodot'
$tag = 'v0.1.8'
$apiBase = "https://api.github.com/repos/$owner/$repository"
$root = $PSScriptRoot

$assetsToUpload = @(
    @{ Name = 'manifest.json'; Path = Join-Path $root 'release\manifest.json' },
    @{ Name = 'MitharaOnline_Update.zip'; Path = Join-Path $root 'release\MitharaOnline_Update.zip' },
    @{ Name = 'MitharaLauncher.exe'; Path = Join-Path $root 'release\LauncherAtualizado\MitharaLauncher.exe' }
)

foreach ($asset in $assetsToUpload) {
    if (-not (Test-Path -LiteralPath $asset.Path -PathType Leaf)) {
        throw "Arquivo não encontrado: $($asset.Path)"
    }
}

$credentialProcess = New-Object System.Diagnostics.Process
$credentialProcess.StartInfo = New-Object System.Diagnostics.ProcessStartInfo
$credentialProcess.StartInfo.FileName = 'C:\Program Files\Git\mingw64\bin\git-credential-manager.exe'
$credentialProcess.StartInfo.Arguments = 'get'
$credentialProcess.StartInfo.UseShellExecute = $false
$credentialProcess.StartInfo.RedirectStandardInput = $true
$credentialProcess.StartInfo.RedirectStandardOutput = $true
$credentialProcess.StartInfo.RedirectStandardError = $true
$credentialProcess.StartInfo.CreateNoWindow = $true
[void]$credentialProcess.Start()
$credentialProcess.StandardInput.Write("protocol=https`nhost=github.com`nusername=$owner`n`n")
$credentialProcess.StandardInput.Flush()
$credentialProcess.StandardInput.Close()
$credentialOutput = $credentialProcess.StandardOutput.ReadToEnd()
$credentialError = $credentialProcess.StandardError.ReadToEnd()
$credentialProcess.WaitForExit()
if ($credentialProcess.ExitCode -ne 0) {
    throw "Não foi possível obter a credencial salva do GitHub. $credentialError"
}

$token = ($credentialOutput -split "`r?`n" | Where-Object { $_ -like 'password=*' } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Nenhuma credencial GitHub foi encontrada no Git Credential Manager.'
}
$token = $token.Substring('password='.Length)
$headers = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'MitharaReleasePublisher/1.0'
}

$release = $null
try {
    $release = Invoke-RestMethod -Uri "$apiBase/releases/tags/$tag" -Headers $headers -Method Get
}
catch {
    $statusCode = [int]$_.Exception.Response.StatusCode
    if ($statusCode -ne 404) { throw }
}

$createdAsDraft = $false
if ($null -eq $release) {
    $body = @{
        tag_name = $tag
        name = 'Mithara Online v0.1.8'
        body = "Sistema completo de refinamento, correções de guildas, drops de equipamentos e melhorias no servidor."
        draft = $true
        prerelease = $false
    } | ConvertTo-Json
    $release = Invoke-RestMethod -Uri "$apiBase/releases" -Headers $headers -Method Post -ContentType 'application/json' -Body $body
    $createdAsDraft = $true
}

foreach ($asset in $assetsToUpload) {
    $existing = $release.assets | Where-Object { $_.name -eq $asset.Name } | Select-Object -First 1
    if ($null -ne $existing) {
        Invoke-RestMethod -Uri "$apiBase/releases/assets/$($existing.id)" -Headers $headers -Method Delete | Out-Null
    }

    $uploadBase = $release.upload_url -replace '\{.*$', ''
    $escapedName = [Uri]::EscapeDataString($asset.Name)
    Write-Host "Enviando $($asset.Name)..."
    Invoke-WebRequest -Uri "$uploadBase`?name=$escapedName" -Headers $headers -Method Post -ContentType 'application/octet-stream' -InFile $asset.Path -UseBasicParsing | Out-Null
}

if ($createdAsDraft -or $release.draft) {
    $publishBody = @{ draft = $false; prerelease = $false } | ConvertTo-Json
    $release = Invoke-RestMethod -Uri "$apiBase/releases/$($release.id)" -Headers $headers -Method Patch -ContentType 'application/json' -Body $publishBody
}

$published = Invoke-RestMethod -Uri "$apiBase/releases/tags/$tag" -Headers $headers -Method Get
$names = @($published.assets | ForEach-Object { $_.name })
foreach ($asset in $assetsToUpload) {
    if ($names -notcontains $asset.Name) {
        throw "Asset não encontrado após publicação: $($asset.Name)"
    }
}

Write-Host "Release publicada: $($published.html_url)"
Write-Host "Assets: $($names -join ', ')"
