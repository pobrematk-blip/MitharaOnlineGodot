param(
    [string]$Workbook = (Join-Path $PSScriptRoot "Mitthara_Online_Armas_Escudos_Final_10_Leveis.xlsx")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-CellColumn([string]$reference) {
    $letters = $reference -replace '[^A-Z]', ''
    $column = 0
    foreach ($letter in $letters.ToCharArray()) {
        $column = ($column * 26) + ([int]$letter - [int][char]'A' + 1)
    }
    return $column - 1
}

function Read-Worksheet([string]$path, [string]$sheetName) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $shared = @()
        $sharedEntry = $zip.GetEntry("xl/sharedStrings.xml")
        if ($sharedEntry) {
            $reader = [System.IO.StreamReader]::new($sharedEntry.Open())
            try { [xml]$sharedXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
            foreach ($si in $sharedXml.sst.si) {
                $shared += (($si.SelectNodes('.//*[local-name()="t"]') | ForEach-Object { $_.'#text' }) -join '')
            }
        }

        $reader = [System.IO.StreamReader]::new($zip.GetEntry("xl/workbook.xml").Open())
        try { [xml]$workbookXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $sheet = $workbookXml.workbook.sheets.sheet | Where-Object { $_.name -eq $sheetName } | Select-Object -First 1
        if (-not $sheet) { throw "Aba '$sheetName' nao encontrada." }

        $reader = [System.IO.StreamReader]::new($zip.GetEntry("xl/_rels/workbook.xml.rels").Open())
        try { [xml]$relsXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $relationshipId = $sheet.GetAttribute('id', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships')
        $relationship = $relsXml.Relationships.Relationship | Where-Object { $_.Id -eq $relationshipId } | Select-Object -First 1
        if (-not $relationship) { throw "Relacionamento da aba '$sheetName' nao encontrado." }
        $sheetPath = $relationship.Target.TrimStart('/')
        if (-not $sheetPath.StartsWith('xl/')) { $sheetPath = 'xl/' + $sheetPath }
        $reader = [System.IO.StreamReader]::new($zip.GetEntry($sheetPath).Open())
        try { [xml]$sheetXml = $reader.ReadToEnd() } finally { $reader.Dispose() }

        $rows = @()
        foreach ($row in $sheetXml.worksheet.sheetData.row) {
            $values = @{}
            foreach ($cell in $row.c) {
                $value = if ($cell.t -eq 's') { $shared[[int]$cell.v] } elseif ($cell.t -eq 'inlineStr') { [string]$cell.is.t } else { [string]$cell.v }
                $values[(Get-CellColumn ([string]$cell.r))] = $value
            }
            $rows += ,$values
        }

        $headers = @{}
        foreach ($key in $rows[0].Keys) { $headers[[string]$rows[0][$key]] = $key }
        $result = @()
        foreach ($row in $rows | Select-Object -Skip 1) {
            if (-not $row.ContainsKey($headers['ItemBaseId'])) { continue }
            $item = [ordered]@{}
            foreach ($header in $headers.Keys) {
                $index = $headers[$header]
                $item[$header] = if ($row.ContainsKey($index)) { [string]$row[$index] } else { '' }
            }
            $result += [pscustomobject]$item
        }
        return $result
    } finally {
        $zip.Dispose()
    }
}

function IntValue($value) { if ([string]::IsNullOrWhiteSpace($value)) { return 0 }; return [int][double]$value }
function Average($min, $max) { return [int][math]::Floor(((IntValue $min) + (IntValue $max)) / 2) }
function Escape-Godot([string]$value) { return $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", ' ') }
function Escape-CSharp([string]$value) { return $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", ' ') }
function Safe-FileName([string]$value) { return ($value -replace '[<>:"/\\|?*]', '').Trim() }

function Normalize-Key([string]$value) {
    $formD = $value.Normalize([Text.NormalizationForm]::FormD)
    return -join ($formD.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark })
}

$classMap = @{
    'Arqueiro' = 'Arqueiro'; 'Assassino' = 'Ladino'; 'Berserker' = 'Berseker'
    'GuardiÃ£o' = 'Guardiao'; 'Mago' = 'Mago'; 'ClÃ©rigo' = 'Prist'
}
$shieldTypes = @('Adaga SecundÃ¡ria', 'Escudo GuardiÃ£o', 'Escudo ClÃ©rigo')
$twoHandedTypes = @('Arco', 'Machado', 'Cajado')
$iconPatterns = @{
    'Arco' = '^Arco.*\.png$'; 'Adaga Principal' = '^Adaga.*\.png$'; 'Adaga SecundÃ¡ria' = '^Adaga.*\.png$'
    'Machado' = '^Machados.*\.png$'; 'Espada' = '^Espada.*\.png$'; 'Escudo GuardiÃ£o' = '^Escudo.*\.png$'
    'Cajado' = '^Cajado.*\.png$'; 'Martelo' = '^Martelo.*\.png$'; 'Escudo ClÃ©rigo' = '^Escudo.*\.png$'
}

$items = Read-Worksheet $Workbook 'Itens_Armas_Escudos'
if ($items.Count -ne 198) { throw "Esperados 198 registros, encontrados $($items.Count)." }

$weaponsDir = Join-Path $PSScriptRoot 'Itens/Armas'
$shieldsDir = Join-Path $PSScriptRoot 'Itens/Escudos'
$iconsDir = Join-Path $PSScriptRoot 'Itens/Incones'
Get-ChildItem $weaponsDir -Filter '*.tres' -File -Recurse | Remove-Item -Force
Get-ChildItem $shieldsDir -Filter '*.tres' -File -Recurse | Remove-Item -Force

$iconCache = @{}
foreach ($type in $iconPatterns.Keys) {
    $iconCache[$type] = @(Get-ChildItem $iconsDir -Filter '*.png' -File | Where-Object { $_.Name -match $iconPatterns[$type] } | Sort-Object Name)
}

$catalogLines = [System.Collections.Generic.List[string]]::new()
$generatedIds = [System.Collections.Generic.HashSet[int]]::new()
foreach ($item in $items) {
    $baseId = IntValue $item.ItemBaseId
    $isElite = $item.TipoTag -eq 'Elite'
    $eliteLiteral = if ($isElite) { 'true' } else { 'false' }
    $runtimeId = if ($isElite) { $baseId + 10000 } else { $baseId }
    $affixPool = (($item.PoolAfixos -split '[;,]') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ','
    if (-not $generatedIds.Add($runtimeId)) { throw "ID runtime duplicado: $runtimeId" }

    $typeKey = Normalize-Key $item.TipoEquipamento
    $classKey = Normalize-Key $item.Classe
    $isShield = @('Adaga Secundaria', 'Escudo Guardiao', 'Escudo Clerigo') -contains $typeKey
    $godotType = if ($isShield) { 8 } else { 7 }
    $serverType = if ($isShield) { 'ItemType.Shield' } else { 'ItemType.Weapon' }
    $subfolder = switch ($typeKey) {
        'Arco' { 'Arcos' }; 'Adaga Principal' { 'Adagas' }; 'Adaga Secundaria' { 'AdagasSecundarias' }
        'Machado' { 'Machados' }; 'Espada' { 'Espadas' }; 'Escudo Guardiao' { 'EscudosGuardiao' }
        'Cajado' { 'Cajados' }; 'Martelo' { 'Martelos' }; 'Escudo Clerigo' { 'EscudosClerigo' }
        default { 'Outros' }
    }
    $categoryRoot = if ($isShield) { $shieldsDir } else { $weaponsDir }
    $targetDir = Join-Path $categoryRoot $subfolder
    if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
    $className = switch ($classKey) {
        'Arqueiro' { 'Arqueiro' }; 'Assassino' { 'Ladino' }; 'Berserker' { 'Berseker' }
        'Guardiao' { 'Guardiao' }; 'Mago' { 'Mago' }; 'Clerigo' { 'Prist' }
        default { $null }
    }
    if (-not $className) { throw "Classe sem mapeamento: $($item.Classe)" }

    $iconPattern = switch ($typeKey) {
        'Arco' { '^Arco.*\.png$' }; 'Adaga Principal' { '^Adaga.*\.png$' }; 'Adaga Secundaria' { '^Adaga.*\.png$' }
        'Machado' { '^Machados.*\.png$' }; 'Espada' { '^Espada.*\.png$' }; 'Escudo Guardiao' { '^Escudo.*\.png$' }
        'Cajado' { '^Lanca quebrada\.png$' }; 'Martelo' { '^Martelo.*\.png$' }; 'Escudo Clerigo' { '^Escudo.*\.png$' }; default { '^$' }
    }
    $icons = @(Get-ChildItem $iconsDir -Filter '*.png' -File | Where-Object { $_.Name -match $iconPattern } | Sort-Object Name)
    $icon = if ($icons.Count -gt 0) { $icons[$baseId % $icons.Count] } else { $null }
    $loadSteps = if ($icon) { 3 } else { 2 }
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("[gd_resource type=`"Resource`" script_class=`"ItemResource`" load_steps=$loadSteps format=3]")
    $lines.Add('')
    $lines.Add('[ext_resource type="Script" path="res://resources/Inventario/ItemResource.cs" id="1_item"]')
    if ($icon) { $lines.Add("[ext_resource type=`"Texture2D`" path=`"res://Itens/Incones/$(Escape-Godot $icon.Name)`" id=`"2_icon`"]") }
    $lines.Add(''); $lines.Add('[resource]'); $lines.Add('script = ExtResource("1_item")')
    $lines.Add("ItemID = $runtimeId")
    $lines.Add("Nome = `"$(Escape-Godot $item.Nome)`"")
    $lines.Add("Descricao = `"$(Escape-Godot $item.Descricao)`"")
    if ($icon) { $lines.Add('Icone = ExtResource("2_icon")') }
    $lines.Add('QuantidadeMaximaPorSlot = 1')
    $lines.Add("Tipo = $godotType")
    if ($twoHandedTypes -contains $typeKey) { $lines.Add('EhDuasMaos = true') }
    if ($isElite) { $lines.Add('TipoItem = 1') }
    $lines.Add("NivelRequerido = $(IntValue $item.Nivel)")
    $lines.Add("Valor = $((IntValue $item.Nivel) * 10)")
    $lines.Add("ClassesPermitidas = `"$className`"")
    $lines.Add("PoolDeAfixos = `"$(Escape-Godot $affixPool)`"")

    $stats = @(
        @('DanoFisico','BaseAttackMin','BaseAttackMax'), @('DefesaFisica','DefenseMin','DefenseMax'),
        @('DefesaMagica','DefesaMagicaMin','DefesaMagicaMax'), @('Hp','HPMin','HPMax'),
        @('Forca','ForcaMin','ForcaMax'), @('Agilidade','AgilidadeMin','AgilidadeMax'),
        @('Destreza','DestrezaMin','DestrezaMax'), @('Inteligencia','InteligenciaMin','InteligenciaMax')
    )
    foreach ($stat in $stats) {
        $min = IntValue $item.($stat[1]); $max = IntValue $item.($stat[2])
        if ($min -eq 0 -and $max -eq 0) { continue }
        $lines.Add("$($stat[0]) = $(Average $min $max)")
        $lines.Add("$($stat[0])Min = $min")
        $lines.Add("$($stat[0])Max = $max")
    }

    $tag = if ($isElite) { 'Elite' } else { 'Normal' }
    $fileName = Safe-FileName "$runtimeId-$($item.Nome)-$tag.tres"
    [IO.File]::WriteAllText((Join-Path $targetDir $fileName), ($lines -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))

    $catalogLines.Add("            new() { Id = $runtimeId, Name = `"$(Escape-CSharp $item.Nome)`", Type = $serverType, RequiredLevel = $(IntValue $item.Nivel), IsElite = $eliteLiteral, AllowedClasses = `"$className`", Forca = $(Average $item.ForcaMin $item.ForcaMax), ForcaMin = $(IntValue $item.ForcaMin), ForcaMax = $(IntValue $item.ForcaMax), Agilidade = $(Average $item.AgilidadeMin $item.AgilidadeMax), AgilidadeMin = $(IntValue $item.AgilidadeMin), AgilidadeMax = $(IntValue $item.AgilidadeMax), Destreza = $(Average $item.DestrezaMin $item.DestrezaMax), DestrezaMin = $(IntValue $item.DestrezaMin), DestrezaMax = $(IntValue $item.DestrezaMax), Inteligencia = $(Average $item.InteligenciaMin $item.InteligenciaMax), InteligenciaMin = $(IntValue $item.InteligenciaMin), InteligenciaMax = $(IntValue $item.InteligenciaMax), BaseAttack = $(Average $item.BaseAttackMin $item.BaseAttackMax), BaseAttackMin = $(IntValue $item.BaseAttackMin), BaseAttackMax = $(IntValue $item.BaseAttackMax), Defense = $(Average $item.DefenseMin $item.DefenseMax), DefenseMin = $(IntValue $item.DefenseMin), DefenseMax = $(IntValue $item.DefenseMax), MagicDefenseMin = $(IntValue $item.DefesaMagicaMin), MagicDefenseMax = $(IntValue $item.DefesaMagicaMax), HpMin = $(IntValue $item.HPMin), HpMax = $(IntValue $item.HPMax), BuyPrice = $((IntValue $item.Nivel) * 10), AffixPool = new List<string>(`"$(Escape-CSharp $affixPool)`".Split(',', StringSplitOptions.RemoveEmptyEntries)) },")
}

$method = @"
    public void SeedItemDefinitions()
    {
        var items = new List<ItemDefinition>
        {
            new() { Id = 100, Name = "Pergaminho do Pet", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 50 },
            new() { Id = 101, Name = "Pergaminho de Ressurreicao", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },
            new() { Id = ItemDefinitions.PergaminhoCriacaoCla, Name = "Pergaminho de Criacao de Cla", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },
$($catalogLines -join "`r`n")
        };

        var definitionsWithRanges = new HashSet<int>();
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM item_definitions WHERE definition_data <> ''";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) definitionsWithRanges.Add(reader.GetInt32(0));
        }

        foreach (var item in items)
            if (item.Id <= 102 || !definitionsWithRanges.Contains(item.Id))
                SaveItemDefinition(item);

        Logger.Info(`$"{items.Count} definicoes de item sincronizadas com o catalogo oficial.");
    }
"@

$dbPath = Join-Path $PSScriptRoot 'server/Database/DatabaseManager.cs'
$dbText = Get-Content $dbPath -Raw -Encoding UTF8
$pattern = '(?s)    public void SeedItemDefinitions\(\)\s*\{.*?\r?\n    \}(?=\r?\n\}\r?\n\r?\npublic class CharacterRow)'
$updated = [regex]::Replace($dbText, $pattern, $method, 1)
if ($updated -eq $dbText) { throw 'Nao foi possivel localizar SeedItemDefinitions no DatabaseManager.cs.' }
Set-Content -LiteralPath $dbPath -Value $updated -Encoding UTF8

Write-Host "Catalogo importado: $($items.Count) recursos, $($generatedIds.Count) IDs unicos."

