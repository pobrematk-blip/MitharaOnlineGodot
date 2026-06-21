$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$skillsRoot = Join-Path $projectRoot 'skills\habilidades'
$templatePath = Join-Path $projectRoot 'skills\ArvoresClasses\ArvoreArqueiro.tres'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$template = [IO.File]::ReadAllText($templatePath, [Text.Encoding]::UTF8)

function Escape-GodotString([string]$value) {
    if ($null -eq $value) { return '' }
    return $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '\n')
}

function Read-Skill([IO.FileInfo]$file) {
    $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    function Value([string]$name, [string]$fallback = '') {
        $match = [regex]::Match($text, "(?m)^$([regex]::Escape($name)) = `"(.*)`"$")
        if ($match.Success) { return $match.Groups[1].Value }
        return $fallback
    }
    $levelMatch = [regex]::Match($text, '(?m)^NivelRequerido = (\d+)$')
    [pscustomobject]@{
        File = $file
        Name = Value 'Nome' $file.BaseName
        Description = Value 'Descricao' ''
        Level = if ($levelMatch.Success) { [int]$levelMatch.Groups[1].Value } else { 1 }
        Passive = $text -match '(?m)^Passive = true$'
    }
}

function Set-Line([string]$block, [string]$property, [string]$value) {
    $pattern = "(?m)^$([regex]::Escape($property)) = .*$"
    $line = "$property = $value"
    if ([regex]::IsMatch($block, $pattern)) { return [regex]::Replace($block, $pattern, $line, 1) }
    return $block.TrimEnd() + "`n$line`n"
}

$blockMatches = [regex]::Matches($template, '(?ms)^\[sub_resource.*?(?=^\[sub_resource|^\[resource])')
$templateBlocks = @($blockMatches | ForEach-Object { $_.Value.TrimEnd() })
$oldRoutes = @('cacador', 'ranger', 'sniper')

$configs = @(
    @{ Folder='Arqueiro'; Output='ArvoreArqueiro.tres'; Prefix='arq'; Display='Arqueiro'; Specs=@('Caçador','Ranger','Sniper'); Common='Todas'; Stats=@(
        @('Agilidade','Evasao','Destreza'), @('Hp','RegeneracaoVida','Tenacidade'), @('Precisao','VelocidadeAtaque','VelocidadeMovimento'),
        @('PenetracaoArmadura','ChanceCritica','Precisao'), @('Mana','RegeneracaoMana','Agilidade'), @('DanoCriticoBonus','Destreza','Evasao')) },
    @{ Folder='Berserker'; Output='ArvoreBerseker.tres'; Prefix='ber'; Display='Berserker'; Specs=@('Bárbaro','Slayer','Titã'); Common='Todas'; Stats=@(
        @('Forca','RouboVida','VelocidadeAtaque'), @('ChanceCritica','DanoCriticoBonus','Precisao'), @('Hp','Tenacidade','RegeneracaoVida'),
        @('PenetracaoArmadura','Forca','Agilidade'), @('Evasao','VelocidadeMovimento','Precisao'), @('DefesaFisica','Hp','RouboVida')) },
    @{ Folder='Guardião'; Output='ArvoreGuardiao.tres'; Prefix='gua'; Display='Guardião'; Specs=@('Protetor','Vanguarda','Templário'); Common='Todas'; Stats=@(
        @('Hp','DefesaFisica','Tenacidade'), @('Forca','Precisao','ChanceCritica'), @('RegeneracaoVida','Hp','Evasao'),
        @('PenetracaoArmadura','Forca','RouboVida'), @('DefesaFisica','Tenacidade','Agilidade'), @('Hp','RegeneracaoVida','Precisao')) },
    @{ Folder='Ladino'; Output='ArvoreLadino.tres'; Prefix='lad'; Display='Ladino'; Specs=@('Assassino','Gatuno','Ninja'); Common='Compartilhadas'; Stats=@(
        @('Destreza','Agilidade','Evasao'), @('ChanceCritica','DanoCriticoBonus','Precisao'), @('VelocidadeAtaque','VelocidadeMovimento','PenetracaoArmadura'),
        @('RouboVida','RouboMana','Hp'), @('Destreza','ChanceCritica','Agilidade'), @('Evasao','Precisao','VelocidadeAtaque')) },
    @{ Folder='Mago'; Output='ArvoreMago.tres'; Prefix='mag'; Display='Mago'; Specs=@('Elementalista','Mago Negro','Summoner'); Common='Todas'; Stats=@(
        @('Inteligencia','Mana','RegeneracaoMana'), @('DanoMagico','Precisao','ChanceCritica'), @('ReducaoCooldown','RouboMana','Hp'),
        @('DanoCriticoBonus','Inteligencia','PenetracaoArmadura'), @('Mana','RegeneracaoMana','Evasao'), @('Hp','Tenacidade','VelocidadeMovimento')) },
    @{ Folder='Clérigo'; Output='ArvorePrist.tres'; Prefix='pri'; Display='Clérigo'; Specs=@('Sacerdote','Paladino','Inquisidor'); Common='Todas'; Stats=@(
        @('Inteligencia','Mana','RegeneracaoMana'), @('Forca','Hp','Tenacidade'), @('RegeneracaoVida','ReducaoCooldown','Evasao'),
        @('DanoMagico','Precisao','ChanceCritica'), @('RouboVida','RouboMana','DefesaFisica'), @('Hp','Mana','RegeneracaoVida')) }
)

foreach ($config in $configs) {
    $classPath = Join-Path $skillsRoot $config.Folder
    $commonFiles = @(Get-ChildItem (Join-Path $classPath $config.Common) -Filter '*.tres' | Sort-Object Name)
    if ($commonFiles.Count -eq 0) { throw "Nenhuma habilidade compartilhada encontrada para $($config.Display)." }

    $routeSlugs = @($config.Specs | ForEach-Object {
        $normalized = $_.Normalize([Text.NormalizationForm]::FormD)
        -join ($normalized.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne 'NonSpacingMark' })
    } | ForEach-Object { ($_ -replace '[^A-Za-z0-9]+','_').Trim('_').ToLowerInvariant() })

    $branchSkills = @()
    for ($routeIndex = 0; $routeIndex -lt 3; $routeIndex++) {
        $common = $commonFiles[[Math]::Min($routeIndex, $commonFiles.Count - 1)]
        $specific = @(Get-ChildItem (Join-Path $classPath $config.Specs[$routeIndex]) -Filter '*.tres' | Sort-Object Name)
        if ($specific.Count -ne 9) { throw "$($config.Display)/$($config.Specs[$routeIndex]) deveria ter 9 habilidades, mas tem $($specific.Count)." }
        $skillsForBranch = @((Read-Skill $common)) + @($specific | ForEach-Object { Read-Skill $_ })
        $branchSkills += ,$skillsForBranch
    }

    $resourceIdByPath = @{}
    $resourceLines = [Collections.Generic.List[string]]::new()
    $nextResourceId = 1
    foreach ($branch in $branchSkills) {
        foreach ($skill in $branch) {
            if ($resourceIdByPath.ContainsKey($skill.File.FullName)) { continue }
            $id = "sk_$nextResourceId"
            $nextResourceId++
            $resourceIdByPath[$skill.File.FullName] = $id
            $relative = $skill.File.FullName.Substring($projectRoot.Length + 1).Replace('\','/')
            $resourceLines.Add("[ext_resource type=`"Resource`" script_class=`"SkillResource`" path=`"res://$relative`" id=`"$id`"]")
        }
    }

    $newBlocks = [Collections.Generic.List[string]]::new()
    $newIds = [Collections.Generic.List[string]]::new()
    foreach ($templateBlock in $templateBlocks) {
        $headerMatch = [regex]::Match($templateBlock, '^\[sub_resource type="Resource" id="([^"]+)"\]')
        $oldId = $headerMatch.Groups[1].Value
        $newId = $oldId
        $routeIndex = -1
        for ($i = 0; $i -lt 3; $i++) {
            if ($oldId -eq $oldRoutes[$i] -or $oldId.StartsWith($oldRoutes[$i] + '_')) {
                $routeIndex = $i
                $newId = $oldId -replace "^$($oldRoutes[$i])", $routeSlugs[$i]
                break
            }
        }

        $block = $templateBlock.Replace("id=`"$oldId`"", "id=`"$newId`"")
        $block = $block.Replace('arq_', "$($config.Prefix)_")
        for ($i = 0; $i -lt 3; $i++) {
            $block = $block.Replace($oldRoutes[$i], $routeSlugs[$i])
        }

        if ($oldId -eq 'base') {
            $block = Set-Line $block 'Nome' "`"Fundamentos do $($config.Display)`""
            $block = Set-Line $block 'Descricao' "`"Nó central que abre as três especializações de $($config.Display).`""
        } elseif ($routeIndex -ge 0) {
            $spec = $config.Specs[$routeIndex]
            if ($oldId -eq $oldRoutes[$routeIndex] + '_rota') {
                $block = Set-Line $block 'Nome' "`"$(Escape-GodotString $spec)`""
                $block = Set-Line $block 'Descricao' "`"Caminho de especialização: $(Escape-GodotString $spec).`""
            } elseif ($oldId -match '_s(\d+)$') {
                $skillIndex = [int]$Matches[1] - 1
                $skill = $branchSkills[$routeIndex][$skillIndex]
                $resourceId = $resourceIdByPath[$skill.File.FullName]
                $block = Set-Line $block 'Nome' "`"$($skill.Name)`""
                $block = Set-Line $block 'Descricao' "`"$($skill.Description)`""
                $block = Set-Line $block 'HabilidadeAtiva' "ExtResource(`"$resourceId`")"
                $block = Set-Line $block 'NodeType' $(if ($skill.Passive) { '2' } else { '1' })
                $block = Set-Line $block 'NivelMinimo' $skill.Level
            } elseif ($oldId -match '_(ci|ce)(\d+)$') {
                $choiceOffset = if ($Matches[1] -eq 'ci') { 0 } else { 3 }
                $choiceIndex = $choiceOffset + [int]$Matches[2] - 1
                $stats = $config.Stats[($choiceIndex + $routeIndex * 2) % $config.Stats.Count]
                $block = Set-Line $block 'Nome' "`"Escolha de atributos — $(Escape-GodotString $spec)`""
                $block = Set-Line $block 'Descricao' '"Escolha um dos três atributos deste grupo."'
                $statArray = 'PackedStringArray("{0}", "{1}", "{2}")' -f $stats[0], $stats[1], $stats[2]
                $block = Set-Line $block 'StatOptionIds' $statArray
                $block = Set-Line $block 'StatOptionValues' 'PackedFloat32Array(0.1, 0.1, 0.1)'
            } elseif ($oldId -match '_(ul|ur)$') {
                $block = Set-Line $block 'Nome' "`"Desbloqueio — $(Escape-GodotString $spec)`""
                $block = Set-Line $block 'Descricao' "`"Marco de progressão do caminho $(Escape-GodotString $spec).`""
            }
        }

        $newBlocks.Add($block.TrimEnd())
        $newIds.Add($newId)
    }

    $headerLines = [Collections.Generic.List[string]]::new()
    $headerLines.Add("[gd_resource type=`"Resource`" script_class=`"TalentTreeResource`" load_steps=91 format=3]")
    $headerLines.Add('')
    $headerLines.Add("[ext_resource type=`"Script`" path=`"res://skills/TalentTreeResource.cs`" id=`"1_tree`"]")
    $headerLines.Add("[ext_resource type=`"Script`" path=`"res://skills/TalentNodeResource.cs`" id=`"2_node`"]")
    $headerLines.AddRange([string[]]$resourceLines.ToArray())
    $headerLines.Add('')
    $header = $headerLines -join "`n"
    $nodes = ($newIds | ForEach-Object { "SubResource(`"$_`")" }) -join ', '
    $footer = @"
[resource]
script = ExtResource("1_tree")
NomeArvore = "Árvore do $($config.Display)"
Nodes = Array[Resource]([$nodes])
RootNodeIds = PackedStringArray("$($config.Prefix)_base")
UsaLayoutPersonalizado = true
BoardSize = Vector2(1900, 1900)
"@
    $output = $header + ($newBlocks -join "`n`n") + "`n`n" + $footer.Trim() + "`n"
    $outputPath = Join-Path $projectRoot "skills\ArvoresClasses\$($config.Output)"
    [IO.File]::WriteAllText($outputPath, $output, $utf8)
    Write-Host "$($config.Display): $($newBlocks.Count) nós, $($resourceIdByPath.Count) habilidades únicas -> $($config.Output)"
}
