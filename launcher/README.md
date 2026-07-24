# Mithara Launcher

O launcher consulta a Release mais recente de `pobrematk-blip/MitharaOnlineGodot`.

Cada Release deve conter exatamente estes arquivos:

- `manifest.json`
- `MitharaOnline_Update.zip`

## Gerar uma atualização

Depois de exportar o jogo para `build/`:

```powershell
.\tools\Build-MitharaUpdate.ps1 -Version "0.1.0"
```

Os dois arquivos para anexar à Release serão criados em `release/`.

## Publicar o launcher

Use este comando por padrão. Ele gera um launcher self-contained, ou seja, o jogador não precisa instalar .NET:

```powershell
.\tools\Build-MitharaLauncherSelfContained.ps1
```

O arquivo para enviar aos jogadores fica em:

```text
release\MitharaLauncher_VPS_SelfContained.zip
```

## Publicação manual do launcher

```powershell
dotnet publish .\launcher\Mithara.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Distribua o launcher ao lado de uma pasta `Game`. Na primeira execução ele baixa o jogo automaticamente.
