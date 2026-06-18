$scriptPath = "res://resources/Inventario/ItemResource.cs"
$scriptUid = "uid://ck8h0m5wglv8a"
$baseDir = "C:\Users\Wesley\Documents\aprendizado\Itens"

function New-ItemTres {
    param($ItemID,$Nome,$Descricao,$Tipo,$CategoriaPeso,$NivelRequerido,$Valor,
          $EhDuasMaos=$false,$Acumulavel=$false,$QuantidadeMaximaPorSlot=1,
          $Forca=0,$ForcaMin=0,$ForcaMax=0,
          $Agilidade=0,$AgilidadeMin=0,$AgilidadeMax=0,
          $Destreza=0,$DestrezaMin=0,$DestrezaMax=0,
          $Inteligencia=0,$InteligenciaMin=0,$InteligenciaMax=0,
          $DanoFisico=0,$DanoFisicoMin=0,$DanoFisicoMax=0,
          $DefesaFisica=0,$DefesaFisicaMin=0,$DefesaFisicaMax=0,
          $Hp=0,$HpMin=0,$HpMax=0,
          $Mana=0,$ManaMin=0,$ManaMax=0,
          $ClassesPermitidas="")

    $dm = if ($EhDuasMaos) {"true"} else {"false"}
    $ac = if ($Acumulavel) {"true"} else {"false"}

@"
[gd_resource type="Resource" script_class="ItemResource" load_steps=2 format=3]
[ext_resource type="Script" uid="$scriptUid" path="$scriptPath" id="1_taxcw"]

[resource]
script = ExtResource("1_taxcw")
ItemID = $ItemID
Nome = "$Nome"
Descricao = "$Descricao"
Acumulavel = $ac
QuantidadeMaximaPorSlot = $QuantidadeMaximaPorSlot
EhDuasMaos = $dm
Tipo = $Tipo
CategoriaPeso = $CategoriaPeso
NivelRequerido = $NivelRequerido
Valor = $Valor
Forca = $Forca
ForcaMin = $ForcaMin
ForcaMax = $ForcaMax
Agilidade = $Agilidade
AgilidadeMin = $AgilidadeMin
AgilidadeMax = $AgilidadeMax
Destreza = $Destreza
DestrezaMin = $DestrezaMin
DestrezaMax = $DestrezaMax
Inteligencia = $Inteligencia
InteligenciaMin = $InteligenciaMin
InteligenciaMax = $InteligenciaMax
DanoFisico = $DanoFisico
DanoFisicoMin = $DanoFisicoMin
DanoFisicoMax = $DanoFisicoMax
DefesaFisica = $DefesaFisica
DefesaFisicaMin = $DefesaFisicaMin
DefesaFisicaMax = $DefesaFisicaMax
Hp = $Hp
HpMin = $HpMin
HpMax = $HpMax
Mana = $Mana
ManaMin = $ManaMin
ManaMax = $ManaMax
ClassesPermitidas = "$ClassesPermitidas"
PodeDropar = true
PodeTrocar = true
PodeVender = true
PodeArmazenarBanco = true
PodeArmazenarBancoGuild = true
ChanceDrop = 0.0
TempoDesaparecimento = 30.0
TipoItem = 0
Raridade = 0
"@
}

$arcosDir = "$baseDir\Arcos"
$aljavasDir = "$baseDir\Aljavas"

# === ARCOS (ID 1001-1021) ===
$arcosFile = @(
    "ArcoIniciante.tres",   # 1001
    "ArcoDeMadeira.tres",   # 1002
    "ArcoCurto.tres",       # 1003
    "ArcoReforcado.tres",   # 1004
    "ArcoLongo.tres",       # 1005
    "ArcoDeCaca.tres",      # 1006
    "ArcoDeGuerra.tres",    # 1007
    "ArcoElfico.tres",      # 1008
    "ArcoDePrecisao.tres",  # 1009
    "ArcoDeVento.tres",     # 1010
    "ArcoTempestade.tres",  # 1011
    "ArcoDeFogo.tres",      # 1012
    "ArcoDeGelo.tres",      # 1013
    "ArcoTrovejante.tres",  # 1014
    "ArcoDeLuz.tres",       # 1015
    "ArcoSombrio.tres",     # 1016
    "ArcoCelestial.tres",   # 1017
    "ArcoDoDragao.tres",    # 1018
    "ArcoDoFenix.tres",     # 1019
    "ArcoSupremo.tres",     # 1020
    "ArcoLendario.tres"     # 1021
)
$arcosNome = @(
    "Arco Iniciante","Arco de Madeira","Arco Curto","Arco Reforcado","Arco Longo",
    "Arco de Caca","Arco de Guerra","Arco Elfico","Arco de Precisao","Arco de Vento",
    "Arco Tempestade","Arco de Fogo","Arco de Gelo","Arco Trovejante","Arco de Luz",
    "Arco Sombrio","Arco Celestial","Arco do Dragao","Arco do Fenix","Arco Supremo",
    "Arco Lendario"
)
$arcosLvl  = @(1,5,10,15,20,25,30,35,40,45,50,55,60,65,70,75,80,85,90,95,100)
$arcosDano = @(@(5,7),@(10,14),@(17,22),@(23,30),@(30,38),@(36,46),@(43,54),@(49,62),@(56,70),@(62,78),@(69,86),@(75,94),@(82,102),@(88,110),@(95,118),@(101,126),@(108,134),@(114,142),@(121,150),@(127,158),@(134,166))
$arcosDes  = @(@(1,2),@(4,6),@(7,10),@(10,14),@(13,18),@(16,22),@(19,26),@(22,30),@(25,34),@(28,38),@(31,42),@(34,46),@(37,50),@(40,54),@(43,58),@(46,62),@(49,66),@(52,70),@(55,74),@(58,78),@(61,82))
$arcosAgi  = @(@(0,1),@(1,2),@(2,4),@(3,6),@(5,8),@(6,10),@(7,11),@(8,13),@(10,15),@(11,17),@(12,19),@(13,20),@(15,22),@(16,24),@(17,26),@(18,27),@(20,29),@(21,31),@(22,33),@(23,35),@(25,36))
$arcosVal  = @(18,50,90,130,170,210,250,290,330,370,410,450,490,530,570,610,650,690,730,770,810)

for ($i = 0; $i -lt $arcosFile.Length; $i++) {
    $lvl = $arcosLvl[$i]
    $id = 1001 + $i
    $danoM = $arcosDano[$i][0]; $danoX = $arcosDano[$i][1]
    $desM  = $arcosDes[$i][0];  $desX  = $arcosDes[$i][1]
    $agiM  = $arcosAgi[$i][0];  $agiX  = $arcosAgi[$i][1]

    $content = New-ItemTres -ItemID $id -Nome $arcosNome[$i] -Descricao "" `
        -Tipo 7 -CategoriaPeso 0 -NivelRequerido $lvl -Valor $arcosVal[$i] -EhDuasMaos $true `
        -Destreza ([math]::Floor(($desM+$desX)/2)) -DestrezaMin $desM -DestrezaMax $desX `
        -Agilidade ([math]::Floor(($agiM+$agiX)/2)) -AgilidadeMin $agiM -AgilidadeMax $agiX `
        -DanoFisico ([math]::Floor(($danoM+$danoX)/2)) -DanoFisicoMin $danoM -DanoFisicoMax $danoX `
        -ClassesPermitidas "Arqueiro"

    Set-Content -Path "$arcosDir\$($arcosFile[$i])" -Value $content -Encoding UTF8
    Write-Output "OK $($arcosFile[$i])"
}

# === ALJAVAS (ID 1051-1071) ===
$aljFile = @(
    "AljavaDePano.tres",
    "AljavaDeCouro.tres",
    "AljavaReforcada.tres",
    "AljavaDeCaca.tres",
    "AljavaDeGuerra.tres",
    "AljavaElfica.tres",
    "AljavaDePrecisao.tres",
    "AljavaDeVento.tres",
    "AljavaTempestade.tres",
    "AljavaDeFogo.tres",
    "AljavaDeGelo.tres",
    "AljavaTrovejante.tres",
    "AljavaDeLuz.tres",
    "AljavaSombria.tres",
    "AljavaCelestial.tres",
    "AljavaDoDragao.tres",
    "AljavaDoFenix.tres",
    "AljavaSuprema.tres",
    "AljavaMitica.tres",
    "AljavaDoInfinito.tres",
    "AljavaLendaria.tres"
)
$aljNome = @(
    "Aljava de Pano","Aljava de Couro","Aljava Reforcada","Aljava de Caca","Aljava de Guerra",
    "Aljava Elfica","Aljava de Precisao","Aljava de Vento","Aljava Tempestade","Aljava de Fogo",
    "Aljava de Gelo","Aljava Trovejante","Aljava de Luz","Aljava Sombria","Aljava Celestial",
    "Aljava do Dragao","Aljava do Fenix","Aljava Suprema","Aljava Mitica","Aljava do Infinito",
    "Aljava Lendaria"
)
$aljLvl  = @(1,5,10,15,20,25,30,35,40,45,50,55,60,65,70,75,80,85,90,95,100)
$aljDes  = @(@(1,2),@(3,5),@(6,9),@(8,12),@(11,16),@(13,19),@(16,23),@(18,26),@(21,30),@(23,33),@(26,37),@(28,40),@(31,44),@(33,47),@(36,51),@(38,54),@(41,58),@(43,61),@(46,65),@(48,68),@(51,72))
$aljAgi  = @(@(1,2),@(3,5),@(5,8),@(7,11),@(9,14),@(11,17),@(13,20),@(15,23),@(17,26),@(19,29),@(21,32),@(23,35),@(25,38),@(27,41),@(29,44),@(31,47),@(33,50),@(35,53),@(37,56),@(39,59),@(41,62))
$aljHp   = @(@(2,5),@(4,9),@(7,13),@(9,17),@(12,21),@(14,25),@(17,29),@(19,33),@(22,37),@(24,41),@(27,45),@(29,49),@(32,53),@(34,57),@(37,61),@(39,65),@(42,69),@(44,73),@(47,77),@(49,81),@(52,85))
$aljVal  = @(14,38,68,98,128,158,188,218,248,278,308,338,368,398,428,458,488,518,548,578,608)

for ($i = 0; $i -lt $aljFile.Length; $i++) {
    $lvl = $aljLvl[$i]
    $id = 1051 + $i
    $desM = $aljDes[$i][0]; $desX = $aljDes[$i][1]
    $agiM = $aljAgi[$i][0]; $agiX = $aljAgi[$i][1]
    $hpM  = $aljHp[$i][0];  $hpX  = $aljHp[$i][1]

    $content = New-ItemTres -ItemID $id -Nome $aljNome[$i] -Descricao "" `
        -Tipo 3 -CategoriaPeso 0 -NivelRequerido $lvl -Valor $aljVal[$i] `
        -Destreza ([math]::Floor(($desM+$desX)/2)) -DestrezaMin $desM -DestrezaMax $desX `
        -Agilidade ([math]::Floor(($agiM+$agiX)/2)) -AgilidadeMin $agiM -AgilidadeMax $agiX `
        -Hp ([math]::Floor(($hpM+$hpX)/2)) -HpMin $hpM -HpMax $hpX `
        -ClassesPermitidas "Arqueiro"

    Set-Content -Path "$aljavasDir\$($aljFile[$i])" -Value $content -Encoding UTF8
    Write-Output "OK $($aljFile[$i])"
}

Write-Output "`n=== TODOS OS ITENS CRIADOS ==="
