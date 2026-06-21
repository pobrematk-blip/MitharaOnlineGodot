param(
    [string]$Workbook = (Join-Path $PSScriptRoot 'Mitthara_Online_Armaduras_Balanceadas_Final.xlsx')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-CellColumn([string]$reference) {
    $column = 0
    foreach ($letter in (($reference -replace '[^A-Z]', '').ToCharArray())) {
        $column = ($column * 26) + ([int]$letter - [int][char]'A' + 1)
    }
    return $column - 1
}

function Read-Worksheet([string]$path, [string]$sheetName) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        function Read-ZipXml([string]$entryName) {
            $entry = $zip.GetEntry($entryName)
            if (-not $entry) { throw "Entrada XLSX ausente: $entryName" }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { return [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        }

        $shared = @()
        if ($zip.GetEntry('xl/sharedStrings.xml')) {
            $sharedXml = Read-ZipXml 'xl/sharedStrings.xml'
            foreach ($si in $sharedXml.sst.si) {
                $shared += (($si.SelectNodes('.//*[local-name()="t"]') | ForEach-Object { $_.'#text' }) -join '')
            }
        }

        $workbookXml = Read-ZipXml 'xl/workbook.xml'
        $relsXml = Read-ZipXml 'xl/_rels/workbook.xml.rels'
        $sheet = $workbookXml.workbook.sheets.sheet | Where-Object { $_.name -eq $sheetName } | Select-Object -First 1
        if (-not $sheet) { throw "Aba '$sheetName' nao encontrada." }
        $relationshipId = $sheet.GetAttribute('id', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships')
        $relationship = $relsXml.Relationships.Relationship | Where-Object { $_.Id -eq $relationshipId } | Select-Object -First 1
        $sheetPath = $relationship.Target.TrimStart('/')
        if (-not $sheetPath.StartsWith('xl/')) { $sheetPath = 'xl/' + $sheetPath }
        $sheetXml = Read-ZipXml $sheetPath

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
            if (-not $row.ContainsKey($headers['Id'])) { continue }
            $item = [ordered]@{}
            foreach ($header in $headers.Keys) {
                $index = $headers[$header]
                $item[$header] = if ($row.ContainsKey($index)) { [string]$row[$index] } else { '' }
            }
            $result += [pscustomobject]$item
        }
        return $result
    } finally { $zip.Dispose() }
}

function Int-Value($value) { if ([string]::IsNullOrWhiteSpace($value)) { return 0 }; return [int][double]$value }
function Float-Value($value) { if ([string]::IsNullOrWhiteSpace($value)) { return 0.0 }; return [double]::Parse($value, [Globalization.CultureInfo]::InvariantCulture) }
function Average-Int($min, $max) { return [int][math]::Floor(((Int-Value $min) + (Int-Value $max)) / 2) }
function Average-Float($min, $max) { return ((Float-Value $min) + (Float-Value $max)) / 2.0 }
function Number-Text([double]$value) { return $value.ToString('0.####', [Globalization.CultureInfo]::InvariantCulture) }
function Escape-Text([string]$value) { return $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", ' ') }
function Safe-FileName([string]$value) { return ($value -replace '[<>:"/\\|?*]', '').Trim() }
function Normalize-Key([string]$value) {
    $formD = $value.Normalize([Text.NormalizationForm]::FormD)
    return -join ($formD.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark })
}

$slotConfig = @{
    'Capacete' = @{ Type = 1; Folder = 'Capacetes'; Icons = @('Capacete Perdido.png', 'Icon Bandana de Arqueiro.png', 'Chapel de Pirata.png') }
    'Peitoral' = @{ Type = 2; Folder = 'Peitorais'; Icons = @('Peitoral de Monstro 1.png', 'Peitoral de Monstros 2.png', 'Capa de Monstros.png') }
    'Cinto' = @{ Type = 3; Folder = 'Cintos'; Icons = @('Cinto de Troll.png') }
    'Luvas' = @{ Type = 4; Folder = 'Luvas'; Icons = @('Luva de couro.png', 'Luvas Perdidas.png') }
    'Calca' = @{ Type = 5; Folder = 'Calcas'; Icons = @('Calca de montros 1.png', 'Calca de monstros 2.png') }
    'Botas' = @{ Type = 6; Folder = 'Botas'; Icons = @('Botas de Montros.png', 'Botas de Monstros 2.png') }
}
$weightConfig = @{
    'Leve' = @{ Value = 0; Folder = 'Leves' }
    'Media' = @{ Value = 1; Folder = 'Medias' }
    'Pesada' = @{ Value = 2; Folder = 'Pesadas' }
}
$classMap = @{ 'Mago' = 'Mago'; 'Clerigo' = 'Prist'; 'Arqueiro' = 'Arqueiro'; 'Assassino' = 'Ladino'; 'Guardiao' = 'Guardiao'; 'Berserker' = 'Berseker' }

$items = Read-Worksheet $Workbook 'Armaduras'
if ($items.Count -ne 396) { throw "Esperados 396 registros, encontrados $($items.Count)." }

$armorRoot = Join-Path $PSScriptRoot 'Itens/Armaduras'
if (Test-Path $armorRoot) { Get-ChildItem $armorRoot -Filter '*.tres' -File -Recurse | Remove-Item -Force }
$iconsRoot = Join-Path $PSScriptRoot 'Itens/Incones'
$catalogLines = [Collections.Generic.List[string]]::new()
$ids = [Collections.Generic.HashSet[int]]::new()

foreach ($item in $items) {
    $id = Int-Value $item.Id
    if (-not $ids.Add($id)) { throw "ID duplicado: $id" }
    $slotKey = Normalize-Key $item.Slot
    $weightKey = Normalize-Key $item.TipoArmadura
    $slot = $slotConfig[$slotKey]
    $weight = $weightConfig[$weightKey]
    if (-not $slot -or -not $weight) { throw "Categoria desconhecida: $($item.TipoArmadura) / $($item.Slot)" }

    $classes = (($item.Classes -split ',') | ForEach-Object { $classMap[(Normalize-Key $_.Trim())] } | Where-Object { $_ }) -join ','
    $affixes = (($item.PoolAfixosAleatorios -split '[;,]') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ','
    $targetDir = Join-Path $armorRoot "$($weight.Folder)/$($slot.Folder)"
    if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }

    $availableIcons = @($slot.Icons | Where-Object { Test-Path (Join-Path $iconsRoot $_) })
    $iconName = if ($availableIcons.Count -gt 0) { $availableIcons[$id % $availableIcons.Count] } else { $null }
    $loadSteps = if ($iconName) { 3 } else { 2 }
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("[gd_resource type=`"Resource`" script_class=`"ItemResource`" load_steps=$loadSteps format=3]")
    $lines.Add('')
    $lines.Add('[ext_resource type="Script" path="res://resources/Inventario/ItemResource.cs" id="1_item"]')
    if ($iconName) { $lines.Add("[ext_resource type=`"Texture2D`" path=`"res://Itens/Incones/$(Escape-Text $iconName)`" id=`"2_icon`"]") }
    $lines.Add(''); $lines.Add('[resource]'); $lines.Add('script = ExtResource("1_item")')
    $lines.Add("ItemID = $id")
    $lines.Add("Nome = `"$(Escape-Text $item.Nome)`"")
    $lines.Add("Descricao = `"$(Escape-Text $item.Descricao)`"")
    if ($iconName) { $lines.Add('Icone = ExtResource("2_icon")') }
    $lines.Add('QuantidadeMaximaPorSlot = 1')
    $lines.Add("Tipo = $($slot.Type)")
    $lines.Add("CategoriaPeso = $($weight.Value)")
    if ($item.TipoTag -eq 'Elite') { $lines.Add('TipoItem = 1') }
    $lines.Add("NivelRequerido = $(Int-Value $item.Nivel)")
    $lines.Add("Valor = $((Int-Value $item.Nivel) * 10)")
    $lines.Add("ClassesPermitidas = `"$classes`"")
    $lines.Add("PoolDeAfixos = `"$(Escape-Text $affixes)`"")

    $integerStats = @(
        @('Hp','HpMin','HpMax'), @('Mana','ManaMin','ManaMax'),
        @('DefesaFisica','DefesaFisicaMin','DefesaFisicaMax'), @('DefesaMagica','DefesaMagicaMin','DefesaMagicaMax')
    )
    foreach ($stat in $integerStats) {
        $min = Int-Value $item.($stat[1]); $max = Int-Value $item.($stat[2])
        $lines.Add("$($stat[0]) = $(Average-Int $min $max)")
        $lines.Add("$($stat[0])Min = $min"); $lines.Add("$($stat[0])Max = $max")
    }
    $evasionMin = Float-Value $item.EvasaoMinPct; $evasionMax = Float-Value $item.EvasaoMaxPct
    $lines.Add("Evasao = $(Number-Text (Average-Float $evasionMin $evasionMax))")
    $lines.Add("EvasaoMin = $(Number-Text $evasionMin)"); $lines.Add("EvasaoMax = $(Number-Text $evasionMax)")

    $fileName = Safe-FileName "$id-$($item.Nome)-$($item.TipoTag).tres"
    [IO.File]::WriteAllText((Join-Path $targetDir $fileName), ($lines -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))

    $isElite = $item.TipoTag -eq 'Elite'
    $eliteLiteral = if ($isElite) { 'true' } else { 'false' }
    $catalogLines.Add("            new() { Id = $id, Name = `"$(Escape-Text $item.Nome)`", Type = (ItemType)$($slot.Type), RequiredLevel = $(Int-Value $item.Nivel), IsElite = $eliteLiteral, AllowedClasses = `"$classes`", Defense = $(Average-Int $item.DefesaFisicaMin $item.DefesaFisicaMax), DefenseMin = $(Int-Value $item.DefesaFisicaMin), DefenseMax = $(Int-Value $item.DefesaFisicaMax), MagicDefense = $(Average-Int $item.DefesaMagicaMin $item.DefesaMagicaMax), MagicDefenseMin = $(Int-Value $item.DefesaMagicaMin), MagicDefenseMax = $(Int-Value $item.DefesaMagicaMax), Hp = $(Average-Int $item.HpMin $item.HpMax), HpMin = $(Int-Value $item.HpMin), HpMax = $(Int-Value $item.HpMax), Mana = $(Average-Int $item.ManaMin $item.ManaMax), ManaMin = $(Int-Value $item.ManaMin), ManaMax = $(Int-Value $item.ManaMax), Evasion = $(Number-Text (Average-Float $item.EvasaoMinPct $item.EvasaoMaxPct))f, EvasionMin = $(Number-Text (Float-Value $item.EvasaoMinPct))f, EvasionMax = $(Number-Text (Float-Value $item.EvasaoMaxPct))f, BuyPrice = $((Int-Value $item.Nivel) * 10), AffixPool = new List<string>(`"$(Escape-Text $affixes)`".Split(',', StringSplitOptions.RemoveEmptyEntries)) },")
}

$armorBlock = @"
        // ARMOR_CATALOG_START
        var armorItems = new List<ItemDefinition>
        {
$($catalogLines -join "`r`n")
        };
        // ARMOR_CATALOG_END

"@
$dbPath = Join-Path $PSScriptRoot 'server/Database/DatabaseManager.cs'
$dbText = Get-Content $dbPath -Raw -Encoding UTF8
$markerPattern = '(?s)        // ARMOR_CATALOG_START.*?        // ARMOR_CATALOG_END\r?\n\r?\n'
if ([regex]::IsMatch($dbText, $markerPattern)) {
    $dbText = [regex]::Replace($dbText, $markerPattern, $armorBlock, 1)
} else {
    $seedStart = $dbText.IndexOf('    public void SeedItemDefinitions()')
    $insertAt = $dbText.IndexOf('        var definitionsWithRanges', $seedStart)
    if ($insertAt -lt 0) { throw 'Ponto de insercao do catalogo no servidor nao encontrado.' }
    $dbText = $dbText.Insert($insertAt, $armorBlock)
}
$dbText = $dbText.Replace('foreach (var item in items)', 'foreach (var item in items.Concat(armorItems))')
Set-Content -LiteralPath $dbPath -Value $dbText -Encoding UTF8

Write-Host "Armaduras importadas: $($items.Count) recursos, $($ids.Count) IDs unicos."

