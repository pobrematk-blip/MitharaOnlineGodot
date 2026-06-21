param(
    [string[]]$Paths = @(
        'C:\Users\Wesley\Downloads\Mitthara_Online_Mago_Skills.xlsx',
        'C:\Users\Wesley\Downloads\Mitthara_Online_Arqueiro_Skills.xlsx',
        'C:\Users\Wesley\Downloads\Mitthara_Online_Ladino_Skills.xlsx',
        'C:\Users\Wesley\Downloads\Mitthara_Online_Berserker_Skills_Completo.xlsx',
        'C:\Users\Wesley\Downloads\Mitthara_Online_Guardiao_Skills_Completo.xlsx',
        'C:\Users\Wesley\Downloads\Mitthara_Online_Clerigo_Skills_Completo.xlsx'
    )
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputRoot = 'res://skills/habilidades'
$Utf8NoBom = [Text.UTF8Encoding]::new($false)
$SpecIndex = @{}
$NextSpecIndex = @{}

function Read-ZipXml($zip, [string]$entryName) {
    $entry = $zip.GetEntry($entryName)
    if (-not $entry) { throw "Entrada ausente: $entryName" }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Get-CellColumn([string]$reference) {
    $column = 0
    foreach ($letter in (($reference -replace '[^A-Z]', '').ToCharArray())) {
        $column = $column * 26 + ([int]$letter - [int][char]'A' + 1)
    }
    $column - 1
}

function ConvertTo-PlainText([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text) -or $text -eq '-') { return '' }
    $text.Trim()
}

function ConvertTo-InvariantText([string]$text) {
    if ($null -eq $text) { return '' }
    $normalized = $text.Normalize([Text.NormalizationForm]::FormD)
    -join ($normalized.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark })
}

function Get-HeaderKey([string]$text) {
    (ConvertTo-InvariantText $text).ToLowerInvariant() -replace '[^a-z0-9]', ''
}

function Escape-TresString([string]$text) {
    if ($null -eq $text) { return '' }
    $text.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '\n')
}

function Get-Number([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return 0 }
    $match = [regex]::Match($text.Replace(',', '.'), '[-+]?\d+(\.\d+)?')
    if (-not $match.Success) { return 0 }
    [double]::Parse($match.Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Get-IntNumber([string]$text) {
    [int][Math]::Round((Get-Number $text), 0)
}

function Get-DurationNumber([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return 0 }
    $clean = ConvertTo-InvariantText $text
    if ($clean -match '(?i)permanente|^-?$') { return 0 }
    Get-Number $text
}

function Get-EffectType([string]$tipo, [string]$efeito, [string]$buff) {
    $text = (ConvertTo-InvariantText "$tipo $efeito $buff").ToLowerInvariant()
    if ($text -match 'invis') { return 4 }
    if ($text -match 'invoc|familiar|golem|elemental|clone') { return 5 }
    if ($text -match 'cura|recupera hp|recuperacao|roubo de vida|roubo') { return 1 }
    if ($text -match 'teleport|salta|salto|avanca|avanco|reposicion|mobilidade|troca de lugar') { return 2 }
    if ($text -match 'escudo|absorve') { return 10 }
    if ($text -match 'stun|atordo') { return 8 }
    if ($text -match 'sangr') { return 9 }
    if ($text -match 'slow|lentid') { return 12 }
    if ($text -match 'root|imobil|prende|aprision') { return 13 }
    if ($text -match 'burn|queim|fogo|vulcan|meteoro') { return 15 }
    if ($text -match 'freeze|gelo|congela') { return 16 }
    if ($text -match 'veneno|toxic|praga') { return 24 }
    if ($text -match 'maldic|trevas|sombr') { return 25 }
    if ($text -match 'buff|\+') { return 3 }
    if ($text -match 'debuff|reduz|fraqueza') { return 25 }
    if ($text -match 'dano|area|dot|ultimate|controle|ataque') { return 7 }
    return 0
}

function Get-TargetType([int]$effectType, [string]$tipo, [string]$efeito) {
    if ($effectType -in @(1, 2, 3, 4, 5, 10)) { return 0 }
    $text = (ConvertTo-InvariantText "$tipo $efeito").ToLowerInvariant()
    if ($text -match 'aliad') { return 1 }
    return 2
}

function Get-ClassBase([string]$className) {
    switch ((ConvertTo-InvariantText $className).ToLowerInvariant()) {
        'arqueiro' { 10000 }
        'mago' { 11000 }
        'ladino' { 12000 }
        'berserker' { 13000 }
        'berseker' { 13000 }
        'guardiao' { 14000 }
        'clerigo' { 15000 }
        'todas' { 9000 }
        default { 19000 }
    }
}

function Get-SpecOffset([string]$className, [string]$specName) {
    if ([string]::IsNullOrWhiteSpace($specName) -or $specName -eq 'Todas') { return 0 }
    $classKey = if ([string]::IsNullOrWhiteSpace($className)) { 'Geral' } else { $className }
    $key = "$classKey|$specName"
    if ($SpecIndex.ContainsKey($key)) { return $SpecIndex[$key] }
    if (-not $NextSpecIndex.ContainsKey($classKey)) { $NextSpecIndex[$classKey] = 1 }
    $SpecIndex[$key] = $NextSpecIndex[$classKey]
    $NextSpecIndex[$classKey]++
    $SpecIndex[$key]
}

function Get-SafeFileName([string]$name) {
    $safe = $name
    foreach ($char in [IO.Path]::GetInvalidFileNameChars()) {
        $safe = $safe.Replace([string]$char, '')
    }
    $safe.Trim()
}

function Convert-ResPathToAbsolute([string]$resPath) {
    Join-Path $ProjectRoot ($resPath -replace '^res://', '')
}

function Read-WorkbookRows([string]$path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $shared = @()
        if ($zip.GetEntry('xl/sharedStrings.xml')) {
            $xml = Read-ZipXml $zip 'xl/sharedStrings.xml'
            foreach ($si in $xml.sst.si) {
                $shared += (($si.SelectNodes('.//*[local-name()="t"]') | ForEach-Object { $_.'#text' }) -join '')
            }
        }

        $workbook = Read-ZipXml $zip 'xl/workbook.xml'
        $rels = Read-ZipXml $zip 'xl/_rels/workbook.xml.rels'
        $result = @()

        foreach ($sheet in $workbook.workbook.sheets.sheet) {
            $rid = $sheet.GetAttribute('id', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships')
            $rel = $rels.Relationships.Relationship | Where-Object Id -eq $rid | Select-Object -First 1
            $target = $rel.Target.TrimStart('/')
            if (-not $target.StartsWith('xl/')) { $target = 'xl/' + $target }
            $xml = Read-ZipXml $zip $target
            $rows = @()
            foreach ($row in @($xml.worksheet.sheetData.row)) {
                $values = @{}
                foreach ($cell in $row.c) {
                    $value = if ($cell.t -eq 's') { $shared[[int]$cell.v] } elseif ($cell.t -eq 'inlineStr') { [string]$cell.is.t } else { [string]$cell.v }
                    $values[(Get-CellColumn ([string]$cell.r))] = $value
                }
                if ($values.Count -gt 0) {
                    $max = ($values.Keys | Measure-Object -Maximum).Maximum
                    $ordered = for ($i = 0; $i -le $max; $i++) { if ($values.ContainsKey($i)) { [string]$values[$i] } else { '' } }
                    $rows += ,$ordered
                }
            }
            $result += [pscustomobject]@{ Sheet = [string]$sheet.name; Rows = $rows }
        }
        $result
    } finally {
        $zip.Dispose()
    }
}

function Get-Field($map, [string]$name) {
    if ($map.ContainsKey($name)) { return ConvertTo-PlainText $map[$name] }
    $wanted = Get-HeaderKey $name
    foreach ($key in $map.Keys) {
        if ((Get-HeaderKey $key) -eq $wanted) {
            return ConvertTo-PlainText $map[$key]
        }
    }
    ''
}

function Write-SkillResource($skill) {
    $classFolder = Get-SafeFileName $skill.Classe
    $specFolderName = if ($skill.Especializacao -match '/') { 'Compartilhadas' } else { $skill.Especializacao }
    $specFolder = Get-SafeFileName $specFolderName
    if ([string]::IsNullOrWhiteSpace($classFolder)) { $classFolder = 'Geral' }
    if ([string]::IsNullOrWhiteSpace($specFolder)) { $specFolder = 'Todas' }

    $resDir = "$OutputRoot/$classFolder/$specFolder"
    $absDir = Convert-ResPathToAbsolute $resDir
    [IO.Directory]::CreateDirectory($absDir) | Out-Null

    $fileName = '{0:000} - {1}.tres' -f $skill.Ordem, (Get-SafeFileName $skill.Nome)
    $absPath = Join-Path $absDir $fileName

    $content = @"
[gd_resource type="Resource" script_class="SkillResource" load_steps=2 format=3]

[ext_resource type="Script" path="res://skills/SkillResource.cs" id="1_sk"]

[resource]
script = ExtResource("1_sk")
SkillId = $($skill.SkillId)
Nome = "$(Escape-TresString $skill.Nome)"
Descricao = "$(Escape-TresString $skill.Descricao)"
Valor = $($skill.Valor)
CustoMana = $($skill.CustoMana)
ClasseRestrita = "$(Escape-TresString $skill.Classe)"
Cooldown = $($skill.Cooldown.ToString([Globalization.CultureInfo]::InvariantCulture))
Duracao = $($skill.Duracao.ToString([Globalization.CultureInfo]::InvariantCulture))
TargetType = $($skill.TargetType)
EffectType = $($skill.EffectType)
Especializacao = "$(Escape-TresString $skill.Especializacao)"
NivelRequerido = $($skill.NivelRequerido)
Ordem = $($skill.Ordem)
Escopo = "$(Escape-TresString $skill.Escopo)"
Tipo = "$(Escape-TresString $skill.Tipo)"
EfeitoPrincipal = "$(Escape-TresString $skill.EfeitoPrincipal)"
DanoEscala = "$(Escape-TresString $skill.DanoEscala)"
BuffDebuff = "$(Escape-TresString $skill.BuffDebuff)"
DuracaoTexto = "$(Escape-TresString $skill.DuracaoTexto)"
Progressao = "$(Escape-TresString $skill.Progressao)"
Observacoes = "$(Escape-TresString $skill.Observacoes)"
"@

    [IO.File]::WriteAllText($absPath, $content, $Utf8NoBom)
    $absPath
}

$created = @()
foreach ($path in $Paths) {
    foreach ($sheet in Read-WorkbookRows $path) {
        if ($sheet.Rows.Count -lt 2) { continue }
        $headers = $sheet.Rows[0]
        if (-not ($headers -contains 'Nome da Skill')) { continue }

        for ($rowIndex = 1; $rowIndex -lt $sheet.Rows.Count; $rowIndex++) {
            $row = $sheet.Rows[$rowIndex]
            $map = @{}
            for ($i = 0; $i -lt $headers.Count; $i++) {
                if (-not [string]::IsNullOrWhiteSpace($headers[$i])) {
                    $map[$headers[$i]] = if ($i -lt $row.Count) { $row[$i] } else { '' }
                }
            }

            $name = Get-Field $map 'Nome da Skill'
            if ([string]::IsNullOrWhiteSpace($name)) { continue }

            $class = Get-Field $map 'Classe'
            $spec = Get-Field $map 'Especializacao'
            $order = Get-IntNumber (Get-Field $map 'Ordem')
            $classKey = (ConvertTo-InvariantText $class).ToLowerInvariant()
            if ($classKey -notin @('arqueiro', 'mago', 'ladino', 'berserker', 'berseker', 'guardiao', 'clerigo', 'todas') -or $order -le 0) {
                continue
            }
            $tipo = Get-Field $map 'Tipo'
            $mainEffect = Get-Field $map 'Efeito Principal'
            $buffDebuff = Get-Field $map 'Buff/Debuff'
            $effectType = Get-EffectType $tipo $mainEffect $buffDebuff

            $skill = [pscustomobject]@{
                SkillId = (Get-ClassBase $class) + ((Get-SpecOffset $class $spec) * 100) + $order
                Nome = $name
                Descricao = Get-Field $map 'Descricao'
                Valor = Get-IntNumber "$(Get-Field $map 'Dano/Escala') $(Get-Field $map 'Efeito Principal')"
                CustoMana = Get-IntNumber (Get-Field $map 'Custo de Mana')
                Classe = $class
                Cooldown = Get-Number (Get-Field $map 'Tempo de Recarga')
                Duracao = Get-DurationNumber (Get-Field $map 'Duracao')
                TargetType = Get-TargetType $effectType $tipo $mainEffect
                EffectType = $effectType
                Especializacao = $spec
                NivelRequerido = Get-IntNumber (Get-Field $map 'Nivel Requerido')
                Ordem = $order
                Escopo = Get-Field $map 'Escopo'
                Tipo = $tipo
                EfeitoPrincipal = $mainEffect
                DanoEscala = Get-Field $map 'Dano/Escala'
                BuffDebuff = $buffDebuff
                DuracaoTexto = Get-Field $map 'Duracao'
                Progressao = Get-Field $map 'Progressao'
                Observacoes = Get-Field $map 'Observacoes'
            }

            $created += Write-SkillResource $skill
        }
    }
}

"Skills importadas: $($created.Count)"
$created | ForEach-Object { $_ }
