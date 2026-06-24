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

```powershell
dotnet publish .\launcher\Mithara.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Distribua o launcher ao lado de uma pasta `Game`. Na primeira execução ele baixa o jogo automaticamente.
