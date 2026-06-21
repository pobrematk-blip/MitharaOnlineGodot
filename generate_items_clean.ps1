# generate_items.ps1 - L?? items_data.tsv e gera .tres + C# seed
# Uso: powershell -ExecutionPolicy Bypass -File generate_items.ps1
# Op????es: -GenerateTres, -GenerateSeed, -ShowSummary

param(
    [switch]$GenerateTres,
    [switch]$GenerateSeed,
    [switch]$ShowSummary
)

$ErrorActionPreference = "Stop"

# ============================================================
# Utilitario: normalizacao de acentos para lookup
# ============================================================
function Normalize-Tipo {
    param($tipo)
    # Remove acentos substituindo por ASCII puro
    $map = @{
        [char]0xE1 = 'a'; [char]0xC1 = 'A'  # a acute
        [char]0xE0 = 'a'; [char]0xC0 = 'A'  # a grave
        [char]0xE2 = 'a'; [char]0xC2 = 'A'  # a circumflex
        [char]0xE3 = 'a'; [char]0xC3 = 'A'  # a tilde
        [char]0xE4 = 'a'; [char]0xC4 = 'A'  # a umlaut
        [char]0xE5 = 'a'; [char]0xC5 = 'A'  # a ring
        [char]0xE7 = 'c'; [char]0xC7 = 'C'  # c cedil
        [char]0xE9 = 'e'; [char]0xC9 = 'E'  # e acute
        [char]0xE8 = 'e'; [char]0xC8 = 'E'  # e grave
        [char]0xEA = 'e'; [char]0xCA = 'E'  # e circumflex
        [char]0xED = 'i'; [char]0xCD = 'I'  # i acute
        [char]0xEC = 'i'; [char]0xCC = 'I'  # i grave
        [char]0xEE = 'i'; [char]0xCE = 'I'  # i circumflex
        [char]0xF3 = 'o'; [char]0xD3 = 'O'  # o acute
        [char]0xF2 = 'o'; [char]0xD2 = 'O'  # o grave
        [char]0xF4 = 'o'; [char]0xD4 = 'O'  # o circumflex
        [char]0xF5 = 'o'; [char]0xD5 = 'O'  # o tilde
        [char]0xF6 = 'o'; [char]0xD6 = 'O'  # o umlaut
        [char]0xFA = 'u'; [char]0xDA = 'U'  # u acute
        [char]0xF9 = 'u'; [char]0xD9 = 'U'  # u grave
        [char]0xFB = 'u'; [char]0xDB = 'U'  # u circumflex
        [char]0xFC = 'u'; [char]0xDC = 'U'  # u umlaut
        [char]0xFD = 'y'; [char]0xDD = 'Y'  # y acute
    }
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $tipo.Length; $i++) {
        $c = $tipo[$i]
        if ($map.ContainsKey($c)) { $c = $map[$c] }
        [void]$sb.Append($c)
    }
    return $sb.ToString()
}

# ============================================================
# Mapeamento: cada Tipo -> configuracao dos stats fixos + pool
# ============================================================
# Constroi dicionario com chaves normalizadas (sem acentos)
$rawConfig = @{
    "Arco" = @{
        ItemType = 7  # ItemType.Weapon
        DuasMaos = $true
        Fixo1 = "DanoFisico"
        Fixo2 = "Destreza"
        Fixo3 = $null
        F2RatioMin = 0.40  # Destreza = 40-60% do DanoFisico
        F2RatioMax = 0.60
        EhEscudo = $false
        Pool = "ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura,Evasao"
    }
    "Adaga Principal" = @{
        ItemType = 7
        DuasMaos = $true
        Fixo1 = "DanoFisico"
        Fixo2 = "Destreza"
        Fixo3 = $null
        F2RatioMin = 0.40
        F2RatioMax = 0.60
        EhEscudo = $false
        Pool = "ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,Evasao,RouboVida,PenetracaoArmadura"
    }
    "Adaga Secundaria" = @{
        ItemType = 7
        DuasMaos = $false
        Fixo1 = "Destreza"
        Fixo2 = "Agilidade"
        Fixo3 = $null
        F2RatioMin = 0.70  # Agilidade = 70-90% da Destreza
        F2RatioMax = 0.90
        EhEscudo = $false
        Pool = "ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,Evasao,RouboVida,VelocidadeMovimento,Tenacidade"
    }
    "Machado" = @{
        ItemType = 7
        DuasMaos = $true
        Fixo1 = "DanoFisico"
        Fixo2 = "Forca"
        Fixo3 = $null
        F2RatioMin = 0.40
        F2RatioMax = 0.60
        EhEscudo = $false
        Pool = "ChanceCritica,DanoCriticoBonus,Precisao,PenetracaoArmadura,RouboVida,Tenacidade,Agilidade"
    }
    "Espada" = @{
        ItemType = 7
        DuasMaos = $true
        Fixo1 = "DanoFisico"
        Fixo2 = "Forca"
        Fixo3 = $null
        F2RatioMin = 0.40
        F2RatioMax = 0.60
        EhEscudo = $false
        Pool = "ChanceCritica,DanoCriticoBonus,Precisao,PenetracaoArmadura,Tenacidade,RouboVida,Agilidade"
    }
    "Escudo Guardiao" = @{
        ItemType = 8  # ItemType.Shield
        DuasMaos = $false
        Fixo1 = "DefesaFisica"
        Fixo2 = "Forca"
        Fixo3 = "Agilidade"
        F2RatioMin = 0.50  # Forca = 50-70% da Defesa
        F2RatioMax = 0.70
        F3RatioMin = 0.40  # Agilidade = 40-60% da Defesa
        F3RatioMax = 0.60
        EhEscudo = $true
        Pool = "Tenacidade,DefesaFisica,DefesaMagica,Hp,RegeneracaoVida,Evasao,Precisao"
    }
    "Cajado" = @{
        ItemType = 7
        DuasMaos = $true
        Fixo1 = "DanoFisico"
        Fixo2 = "Inteligencia"
        Fixo3 = $null
        F2RatioMin = 0.40
        F2RatioMax = 0.60
        EhEscudo = $false
        Pool = "ChanceCritica,DanoCriticoBonus,DanoMagico,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown"
    }
    "Martelo" = @{
        ItemType = 7
        DuasMaos = $true
        Fixo1 = "DanoFisico"
        Fixo2 = "Forca"
        Fixo3 = "Inteligencia"
        F2RatioMin = 0.30  # Forca = 30-50% do Dano
        F2RatioMax = 0.50
        F3RatioMin = 0.30  # Inteligencia = 30-50% do Dano
        F3RatioMax = 0.50
        EhEscudo = $false
        Pool = "ChanceCritica,DanoCriticoBonus,Precisao,Tenacidade,RouboVida,RouboMana,RegeneracaoVida,RegeneracaoMana"
    }
    "Escudo Clerigo" = @{
        ItemType = 8
        DuasMaos = $false
        Fixo1 = "DefesaFisica"
        Fixo2 = "Forca"
        Fixo3 = "Inteligencia"
        F2RatioMin = 0.50
        F2RatioMax = 0.70
        F3RatioMin = 0.40
        F3RatioMax = 0.60
        EhEscudo = $true
        Pool = "Tenacidade,DefesaFisica,DefesaMagica,Hp,Mana,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown"
    }
}

# Normaliza as chaves do dicionario (remove acentos)
$tipoConfig = @{}
foreach ($key in $rawConfig.Keys) {
    $tipoConfig[(Normalize-Tipo $key)] = $rawConfig[$key]
}

# ============================================================
# Leitura do TSV
# ============================================================
function Read-ItemsTsv {
    $tsvPath = Join-Path $PSScriptRoot "items_data.tsv"
    if (-not (Test-Path $tsvPath)) {
        Write-Error "Arquivo items_data.tsv n??o encontrado em $tsvPath"
        return $null
    }

    $lines = Get-Content -Path $tsvPath -Encoding UTF8
    if ($lines.Count -lt 2) {
        Write-Error "TSV vazio ou apenas cabe??alho"
        return $null
    }

    $header = $lines[0] -split "`t"
    # Expected: ItemID, Classe, Slot, Tipo, TipoTag, Nivel, Nome, Descricao, Fixo1_Min, Fixo1_Max

    $items = @()
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ([string]::IsNullOrWhiteSpace($lines[$i])) { continue }
        $cols = $lines[$i] -split "`t"
        if ($cols.Count -lt 10) {
            Write-Warning "Linha $($i+1): poucas colunas ($($cols.Count)), ignorando"
            continue
        }

        $item = [PSCustomObject]@{
            ItemID   = [int]$cols[0]
            Classe   = $cols[1]
            Slot     = $cols[2]
            Tipo     = $cols[3]
            TipoKey  = Normalize-Tipo $cols[3]
            TipoTag  = $cols[4]
            Nivel    = [int]$cols[5]
            Nome     = $cols[6]
            Descricao = $cols[7]
            Fixo1Min = [int]$cols[8]
            Fixo1Max = [int]$cols[9]
        }
        $items += $item
    }

    return $items
}

# ============================================================
# C??lculo de stats secund??rios baseado no tipo + stat prim??rio
# ============================================================
function Get-SecundarioRange {
    param($fixo1Min, $fixo1Max, $ratioMin, $ratioMax)

    $min = [math]::Max(1, [math]::Floor($fixo1Min * $ratioMin))
    $max = [math]::Max($min + 1, [math]::Ceiling($fixo1Max * $ratioMax))
    return @{ Min = $min; Max = $max }
}

# ============================================================
# Gera????o .tres
# ============================================================
function Write-TresFile {
    param($item, $config)

    $tipoItemVal = if ($item.TipoTag -eq "Elite") { 1 } else { 0 }
    $tag = if ($item.TipoTag -eq "Elite") { " (Elite)" } else { " (Normal)" }
    $filename = "$($item.ItemID) - $($item.Nome)$tag.tres" -replace '[<>:"/\\|?*]', ''
    $outDir = Join-Path $PSScriptRoot "generated_tres"
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
    $path = Join-Path $outDir $filename

    $lines = [System.Collections.ArrayList]::new()
    [void]$lines.Add('[gd_resource type="Resource" script_class="ItemResource" load_steps=2 format=3]')
    [void]$lines.Add('')
    [void]$lines.Add('[ext_resource type="Script" path="res://resources/Inventario/ItemResource.cs" id="1_taxcw"]')
    [void]$lines.Add('')
    [void]$lines.Add('[resource]')
    [void]$lines.Add('script = ExtResource("1_taxcw")')
    [void]$lines.Add("ItemID = $($item.ItemID)")
    [void]$lines.Add('Nome = "' + $item.Nome + '"')
    [void]$lines.Add('Descricao = "' + $item.Descricao + '"')
    [void]$lines.Add("QuantidadeMaximaPorSlot = 1")
    [void]$lines.Add("Tipo = $($config.ItemType)")
    [void]$lines.Add("NivelRequerido = $($item.Nivel)")
    if ($config.DuasMaos) { [void]$lines.Add("EhDuasMaos = true") }
    [void]$lines.Add("CategoriaPeso = 0")
    if ($tipoItemVal -eq 1) { [void]$lines.Add("TipoItem = 1") }
    [void]$lines.Add('ClassesPermitidas = "' + $item.Classe + '"')
    [void]$lines.Add('PoolDeAfixos = "' + $config.Pool + '"')

    # Fixo1
    $avg1 = [math]::Floor(($item.Fixo1Min + $item.Fixo1Max) / 2)
    Add-StatLine $lines $config.Fixo1 $avg1 $item.Fixo1Min $item.Fixo1Max

    # Fixo2
    if ($config.Fixo2) {
        $r2 = Get-SecundarioRange $item.Fixo1Min $item.Fixo1Max $config.F2RatioMin $config.F2RatioMax
        $avg2 = [math]::Floor(($r2.Min + $r2.Max) / 2)
        Add-StatLine $lines $config.Fixo2 $avg2 $r2.Min $r2.Max
    }

    # Fixo3
    if ($config.Fixo3) {
        $r3 = Get-SecundarioRange $item.Fixo1Min $item.Fixo1Max $config.F3RatioMin $config.F3RatioMax
        $avg3 = [math]::Floor(($r3.Min + $r3.Max) / 2)
        Add-StatLine $lines $config.Fixo3 $avg3 $r3.Min $r3.Max
    }

    $content = $lines -join "`r`n"
    Set-Content -Path $path -Value $content -Encoding UTF8
    Write-Host "  + $filename"
}

function Add-StatLine {
    param($lines, $statName, $avg, $min, $max)

    [void]$lines.Add("$statName = $avg")
    [void]$lines.Add("${statName}Min = $min")
    [void]$lines.Add("${statName}Max = $max")
}

# ============================================================
# Gera????o C# Seed
# ============================================================
function Write-CSharpSeed {
    param($items)

    $sb = [System.Text.StringBuilder]::new()

    [void]$sb.AppendLine("// ============================================================")
    [void]$sb.AppendLine("// SeedItemDefinitions() ??? gerado automaticamente")
    [void]$sb.AppendLine("// Cole este metodo dentro de DatabaseManager, ou use como partial class.")
    [void]$sb.AppendLine("// ============================================================")
    [void]$sb.AppendLine("partial class DatabaseManager")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    public void SeedItemDefinitions()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("    // Consum??veis principais")
    [void]$sb.AppendLine("    var coreConsumables = new List<ItemDefinition>")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        new() { Id = 100, Name = ""Pergaminho do Pet"", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 50 },")
    [void]$sb.AppendLine("        new() { Id = 101, Name = ""Pergaminho de Ressurrei????o"", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },")
    [void]$sb.AppendLine("        new() { Id = ItemDefinitions.PergaminhoCriacaoCla, Name = ""Pergaminho de Cria????o de Cl??"", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },")
    [void]$sb.AppendLine("    };")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("    using var conn = new NpgsqlConnection(_connectionString);")
    [void]$sb.AppendLine("    conn.Open();")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("    using var checkCmd = conn.CreateCommand();")
    [void]$sb.AppendLine("    checkCmd.CommandText = ""SELECT COUNT(*) FROM item_definitions"";")
    [void]$sb.AppendLine("    var count = Convert.ToInt32(checkCmd.ExecuteScalar());")
    [void]$sb.AppendLine("    if (count > 0)")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        foreach (var item in coreConsumables)")
    [void]$sb.AppendLine("            SaveItemDefinition(item);")
    [void]$sb.AppendLine('        Logger.Info($"Item definitions j?? existem ({count} registros). Consum??veis atualizados.");')
    [void]$sb.AppendLine("        return;")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("    Logger.Info(""Populando item_definitions..."");")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("    var items = new List<ItemDefinition>")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        coreConsumables[0],")
    [void]$sb.AppendLine("        coreConsumables[1],")
    [void]$sb.AppendLine("        coreConsumables[2],")

    # Agrupar por Tipo + TipoTag
    $sorted = $items | Sort-Object Tipo, TipoTag, Nivel
    $currentTipo = ""
    $currentTag = ""

    foreach ($item in $sorted) {
        $cfg = $tipoConfig[$item.TipoKey]
        if (-not $cfg) { continue }

        $label = "$($item.Tipo) - $($item.TipoTag)"
        if ($label -ne "$currentTipo - $currentTag") {
            $currentTipo = $item.Tipo
            $currentTag = $item.TipoTag
            [void]$sb.AppendLine("")
            [void]$sb.AppendLine("            // $label")
        }

        $typeStr = if ($cfg.EhEscudo) { "ItemType.Shield" } else { "ItemType.Weapon" }
        $avg1 = [math]::Floor(($item.Fixo1Min + $item.Fixo1Max) / 2)

        $props = "Id = $($item.ItemID), Name = ""$($item.Nome)"", Type = $typeStr"

        if ($cfg.Fixo1 -eq "DanoFisico" -or $cfg.Fixo1 -eq "DefesaFisica") {
            $field = if ($cfg.Fixo1 -eq "DanoFisico") { "BaseAttack" } else { "Defense" }
            $props += ", $field = $avg1"
        } elseif ($cfg.Fixo1 -eq "Destreza") {
            $props += ", Destreza = $avg1"
        }

        if ($cfg.Fixo2) {
            $r2 = Get-SecundarioRange $item.Fixo1Min $item.Fixo1Max $cfg.F2RatioMin $cfg.F2RatioMax
            $avg2 = [math]::Floor(($r2.Min + $r2.Max) / 2)
            $props += ", $($cfg.Fixo2) = $avg2"
        }

        if ($cfg.Fixo3) {
            $r3 = Get-SecundarioRange $item.Fixo1Min $item.Fixo1Max $cfg.F3RatioMin $cfg.F3RatioMax
            $avg3 = [math]::Floor(($r3.Min + $r3.Max) / 2)
            $props += ", $($cfg.Fixo3) = $avg3"
        }

        $props += ", BuyPrice = $($item.Nivel * 10)"

        [void]$sb.AppendLine("            new() { $props, AffixPool = new List<string>(""$($cfg.Pool)"".Split(',')) },")
    }

    [void]$sb.AppendLine("    };")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("    foreach (var item in items)")
    [void]$sb.AppendLine("        SaveItemDefinition(item);")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine('    Logger.Info($"{items.Count} defini????es inseridas no banco.");')
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")

    $outputPath = Join-Path $PSScriptRoot "generated_SeedItemDefinitions.cs"
    Set-Content -Path $outputPath -Value $sb.ToString() -Encoding UTF8
    Write-Host "Gerado: $outputPath"
}

# ============================================================
# Sum??rio
# ============================================================
function Show-Summary {
    param($items)
    $groups = $items | Group-Object Tipo, TipoTag | Sort-Object Name
    Write-Host "`n=== SUM??RIO ==="
    Write-Host "Total de itens no TSV: $($items.Count)"
    foreach ($g in $groups) {
        Write-Host "  $($g.Name): $($g.Count) itens"
    }
    Write-Host ""
}

# ============================================================
# MAIN
# ============================================================
$allItems = Read-ItemsTsv
if (-not $allItems) { exit 1 }

if ($ShowSummary) { Show-Summary $allItems }

if ($GenerateTres) {
    Write-Host "`n=== Gerando .tres files ==="
    foreach ($item in $allItems) {
        $cfg = $tipoConfig[$item.TipoKey]
        if (-not $cfg) {
            Write-Warning "Tipo desconhecido: $($item.Tipo) - ItemID $($item.ItemID)"
            continue
        }
        Write-TresFile $item $cfg
    }
}

if ($GenerateSeed) {
    Write-Host "`n=== Gerando C# Seed ==="
    Write-CSharpSeed $allItems
}

if (-not $GenerateTres -and -not $GenerateSeed -and -not $ShowSummary) {
    Write-Host "`nItens carregados: $($allItems.Count)"
    Write-Host ""
    Write-Host "Flags dispon??veis:"
    Write-Host "  -GenerateTres    Gera .tres em generated_tres/"
    Write-Host "  -GenerateSeed    Gera C# SeedItemDefinitions.cs"
    Write-Host "  -ShowSummary     Mostra resumo dos itens"
    Write-Host ""
    Write-Host 'Exemplo: .\generate_items.ps1 -ShowSummary'
}
