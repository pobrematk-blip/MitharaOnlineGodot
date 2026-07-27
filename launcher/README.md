# Mithara Launcher

O launcher consulta a Release mais recente de `pobrematk-blip/MitharaOnlineGodot`.

Cada Release do cliente deve conter estes arquivos:

- `manifest.json`
- `MitharaOnline_Update.zip`
- `MitharaLauncher.exe`, somente quando o launcher tambem mudar

## Checklist obrigatório de release

Antes de publicar uma versao nova:

1. Rode o build:

```powershell
dotnet build .\Mithara.sln
```

2. Confira se nao ficou alteracao fora do commit:

```powershell
git status --short
```

3. Exporte no Godot o preset `Windows Teste Externo`.

O export precisa atualizar estes arquivos:

```text
build\MitharaOnlineTeste.exe
build\MitharaOnlineTeste.pck
```

4. Gere o pacote do cliente:

```powershell
.\tools\Build-MitharaUpdate.ps1 -Version "0.1.0"
```

O script bloqueia a criacao do update se:

- existir arquivo modificado fora do commit;
- o `.pck` exportado estiver mais antigo que arquivos do projeto.

5. Se houve mudanca no servidor, gere tambem o pacote da VPS:

```powershell
.\tools\Build-MitharaServerVpsPackage.ps1 -Version "0.1.0-vps"
```

6. Publique a release correta:

```powershell
.\.codex-publish-release.ps1 -Tag "v0.1.0" -ServerPackage ".\release\MitharaServer_VPS_0.1.0-vps.zip"
```

## Publicar o launcher

Use este comando quando o launcher em si mudar. Ele gera um launcher self-contained, ou seja, o jogador nao precisa instalar .NET:

```powershell
.\tools\Build-MitharaLauncherSelfContained.ps1
```

O arquivo para enviar aos jogadores fica em:

```text
release\MitharaLauncher_VPS_SelfContained.zip
```

## Publicacao manual do launcher

```powershell
dotnet publish .\launcher\Mithara.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Distribua o launcher ao lado de uma pasta `Game`. Na primeira execucao ele baixa o jogo automaticamente.
