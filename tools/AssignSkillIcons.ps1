$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$skillRoot = Join-Path $root 'skills\habilidades'
$iconRoot = 'skills/Incone Skills'
$utf8 = [Text.UTF8Encoding]::new($false)

function Icons([string]$folder, [int[]]$numbers) {
    return @($numbers | ForEach-Object { "$folder/$_.png" })
}

function Set-SkillIcon([IO.FileInfo]$skillFile, [string]$relativeIcon) {
    $iconDiskPath = Join-Path $root ($relativeIcon.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $iconDiskPath)) {
        throw "Ícone não encontrado: $relativeIcon"
    }

    $text = [IO.File]::ReadAllText($skillFile.FullName, [Text.Encoding]::UTF8).Replace("`r`n", "`n")
    $resourcePath = 'res://' + $relativeIcon.Replace('\', '/')
    $textureLine = "[ext_resource type=`"Texture2D`" path=`"$resourcePath`" id=`"2_icon`"]"

    $text = [regex]::Replace($text, '(?m)^\[gd_resource (.*?)load_steps=\d+(.*?)\]$', '[gd_resource $1load_steps=3$2]')
    if ($text -match '(?m)^\[ext_resource type="Texture2D".*id="2_icon"\]$') {
        $text = [regex]::Replace($text, '(?m)^\[ext_resource type="Texture2D".*id="2_icon"\]$', $textureLine)
    } else {
        $text = [regex]::Replace($text, '(?m)^(\[ext_resource type="Script".*\])$', "`$1`n$textureLine", 1)
    }

    if ($text -match '(?m)^Icone = ') {
        $text = [regex]::Replace($text, '(?m)^Icone = .*$', 'Icone = ExtResource("2_icon")')
    } else {
        $text = [regex]::Replace($text, '(?m)^(Descricao = .*)$', "`$1`nIcone = ExtResource(`"2_icon`")", 1)
    }
    [IO.File]::WriteAllText($skillFile.FullName, $text, $utf8)
}

$maps = @{
    'Arqueiro' = @{
        'Todas' = (Icons "$iconRoot/Arqueiro" @(48,18))
        'Caçador' = (Icons "$iconRoot/Arqueiro" @(42,26,31,32,44,47,25,35,23))
        'Ranger' = (Icons "$iconRoot/Arqueiro" @(10,37,43,9,16,29,4,47,36))
        'Sniper' = (Icons "$iconRoot/Arqueiro" @(14,47,38,13,39,17,16,24,45))
    }
    'Berserker' = @{
        'Todas' = (Icons "$iconRoot/Berseker" @(33,17,40))
        'Bárbaro' = (Icons "$iconRoot/Berseker" @(24,29,44,18,25,36,34,43,26))
        'Slayer' = (Icons "$iconRoot/Berseker" @(42,30,12,45,4,35,7,38,48))
        'Titã' = (Icons "$iconRoot/Berseker" @(40,33,19,34,16,46,37,43,2))
    }
    'Guardião' = @{
        'Todas' = (Icons "$iconRoot/Guerreiro" @(33,17,16))
        'Protetor' = (Icons "$iconRoot/Guerreiro" @(19,11,14,37,43,18,15,21,16))
        'Vanguarda' = (Icons "$iconRoot/Guerreiro" @(44,34,46,10,12,17,22,37,32))
        'Templário' = (Icons "$iconRoot/Guerreiro" @(47,33,38,21,27,36,2,43,26))
    }
    'Ladino' = @{
        'Compartilhadas' = (Icons "$iconRoot/Assasino" @(41))
        'Assassino' = (Icons "$iconRoot/Assasino" @(35,43,23,10,25,24,28,37,47))
        'Gatuno' = (Icons "$iconRoot/Assasino" @(33,15,16,11,6,29,38,12,44))
        'Ninja' = (Icons "$iconRoot/Assasino" @(31,46,20,43,39,28,36,42,48))
    }
    'Mago' = @{
        'Todas' = @(
            "$iconRoot/Buffs/PNG/39.png", "$iconRoot/Dark Mage/39.png", "$iconRoot/Buffs/PNG/6.png")
        'Elementalista' = @(
            "$iconRoot/Mage/19.png", "$iconRoot/Debuffs/31.png", "$iconRoot/Dark Mage/48.png",
            "$iconRoot/Mage/16.png", "$iconRoot/Debuffs/33.png", "$iconRoot/Dark Mage/15.png",
            "$iconRoot/Mage/22.png", "$iconRoot/Mage/34.png", "$iconRoot/Mage/21.png")
        'Mago Negro' = (Icons "$iconRoot/Dark Mage" @(19,29,35,14,38,33,31,28,26))
        'Summoner' = (Icons "$iconRoot/Dark Mage" @(7,8,24,35,16,5,22,25,27))
    }
    'Clérigo' = @{
        'Todas' = (Icons "$iconRoot/Buffs/PNG" @(39,36,47))
        'Sacerdote' = (Icons "$iconRoot/Buffs/PNG" @(38,47,37,48,39,6,44,48,37))
        'Paladino' = @(
            "$iconRoot/Guerreiro/47.png", "$iconRoot/Buffs/PNG/6.png", "$iconRoot/Guerreiro/19.png",
            "$iconRoot/Guerreiro/27.png", "$iconRoot/Buffs/PNG/40.png", "$iconRoot/Guerreiro/38.png",
            "$iconRoot/Buffs/PNG/48.png", "$iconRoot/Guerreiro/43.png", "$iconRoot/Buffs/PNG/47.png")
        'Inquisidor' = @(
            "$iconRoot/Mage/19.png", "$iconRoot/Debuffs/27.png", "$iconRoot/Dark Mage/48.png",
            "$iconRoot/Debuffs/3.png", "$iconRoot/Guerreiro/38.png", "$iconRoot/Mage/35.png",
            "$iconRoot/Guerreiro/44.png", "$iconRoot/Buffs/PNG/40.png", "$iconRoot/Mage/41.png")
    }
}

$assignedByName = @{}
$total = 0
foreach ($className in $maps.Keys) {
    foreach ($specName in $maps[$className].Keys) {
        $folder = Join-Path (Join-Path $skillRoot $className) $specName
        $files = @(Get-ChildItem -LiteralPath $folder -Filter '*.tres' | Sort-Object Name)
        $icons = @($maps[$className][$specName])
        if ($files.Count -ne $icons.Count) {
            throw "$className/${specName}: $($files.Count) skills para $($icons.Count) ícones."
        }
        for ($i = 0; $i -lt $files.Count; $i++) {
            Set-SkillIcon $files[$i] $icons[$i]
            $nameMatch = [regex]::Match([IO.File]::ReadAllText($files[$i].FullName, [Text.Encoding]::UTF8), '(?m)^Nome = "(.*)"$')
            if ($nameMatch.Success -and $className -eq 'Arqueiro') {
                $assignedByName[$nameMatch.Groups[1].Value] = $icons[$i]
            }
            $total++
        }
    }
}

# Mantém os 29 recursos antigos do Arqueiro utilizáveis pelo editor externo.
$legacyArcherFiles = @(Get-ChildItem -LiteralPath $skillRoot -File -Filter '*.tres' | Sort-Object Name)
$legacyArcherIcons = Icons "$iconRoot/Arqueiro" @(
    48,42,26,31,32,44,47,25,35,23,14,47,38,13,39,17,16,24,45,18,10,37,43,9,16,29,4,47,36)
if ($legacyArcherFiles.Count -ne $legacyArcherIcons.Count) {
    throw "Arqueiro legado: $($legacyArcherFiles.Count) skills para $($legacyArcherIcons.Count) ícones."
}
for ($i = 0; $i -lt $legacyArcherFiles.Count; $i++) {
    Set-SkillIcon $legacyArcherFiles[$i] $legacyArcherIcons[$i]
    $total++
}

# Recurso universal mantido pronto para futuras árvores.
$universal = Get-Item -LiteralPath (Join-Path $skillRoot 'Todas\Todas\001 - Segundo Fôlego.tres') -ErrorAction SilentlyContinue
if ($universal) {
    Set-SkillIcon $universal "$iconRoot/Buffs/PNG/39.png"
    $total++
}

Write-Host "$total recursos de habilidade receberam ícones."
