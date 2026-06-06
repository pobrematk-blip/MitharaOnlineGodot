using Godot;
using System;

public enum TipoItem { Normal, Elite }

public enum Raridade { Comum, Incomum, Raro, Epico, Lendario, Mistico }

public enum PesoItem { Leve, Medio, Pesado }

[GlobalClass]
public partial class ItemResource : Resource
{
    [ExportGroup("Identidade")]
    [Export] public int ItemID { get; set; } = 0;
    [Export] public string Nome { get; set; } = "Item Novo";
    [Export] public string Descricao { get; set; } = "";
    [Export] public Texture2D Icone { get; set; }

    [ExportGroup("Stack")]
    [Export] public bool Acumulavel { get; set; } = false;
    [Export] public int QuantidadeMaximaPorSlot { get; set; } = 99;

    [ExportGroup("Bolsa")]
    [Export] public bool EhBolsa { get; set; } = false;
    [Export] public int SlotsAdicionais { get; set; } = 10;

    [ExportGroup("Equipamento")]
    [Export] public TipoEquipamento Tipo { get; set; } = TipoEquipamento.Nenhum;
    [Export] public bool EhDuasMaos { get; set; } = false;
    [Export] public bool EhPvp { get; set; } = false;
    [Export] public PesoItem CategoriaPeso { get; set; } = PesoItem.Medio;

    [ExportGroup("Raridade")]
    [Export] public TipoItem TipoItem { get; set; } = TipoItem.Normal;
    [Export] public Raridade Raridade { get; set; } = Raridade.Comum;

    [ExportGroup("Requisitos")]
    [Export] public int NivelRequerido { get; set; } = 1;

    [ExportGroup("Valor")]
    [Export] public int Valor { get; set; } = 0;

    [ExportGroup("Atributos")]
    [Export] public int Forca { get; set; }
    [Export] public int ForcaMin { get; set; }
    [Export] public int ForcaMax { get; set; }
    [Export] public int Agilidade { get; set; }
    [Export] public int AgilidadeMin { get; set; }
    [Export] public int AgilidadeMax { get; set; }
    [Export] public int Destreza { get; set; }
    [Export] public int DestrezaMin { get; set; }
    [Export] public int DestrezaMax { get; set; }
    [Export] public int Inteligencia { get; set; }
    [Export] public int InteligenciaMin { get; set; }
    [Export] public int InteligenciaMax { get; set; }

    [ExportGroup("Combate")]
    [Export] public int DanoFisico { get; set; }
    [Export] public int DanoFisicoMin { get; set; }
    [Export] public int DanoFisicoMax { get; set; }
    [Export] public int DanoMagico { get; set; }
    [Export] public int DanoMagicoMin { get; set; }
    [Export] public int DanoMagicoMax { get; set; }
    [Export] public int DefesaFisica { get; set; }
    [Export] public int DefesaFisicaMin { get; set; }
    [Export] public int DefesaFisicaMax { get; set; }
    [Export] public int DefesaMagica { get; set; }
    [Export] public int DefesaMagicaMin { get; set; }
    [Export] public int DefesaMagicaMax { get; set; }

    [ExportGroup("Bonus")]
    [Export] public float ChanceCritica { get; set; }
    [Export] public float ChanceCriticaMin { get; set; }
    [Export] public float ChanceCriticaMax { get; set; }
    [Export] public float Evasao { get; set; }
    [Export] public float EvasaoMin { get; set; }
    [Export] public float EvasaoMax { get; set; }
    [Export] public float DanoCriticoBonus { get; set; }
    [Export] public float DanoCriticoBonusMin { get; set; }
    [Export] public float DanoCriticoBonusMax { get; set; }
    [Export] public float RouboVida { get; set; }
    [Export] public float RouboVidaMin { get; set; }
    [Export] public float RouboVidaMax { get; set; }
    [Export] public float RouboMana { get; set; }
    [Export] public float RouboManaMin { get; set; }
    [Export] public float RouboManaMax { get; set; }
    [Export] public float RegeneracaoVida { get; set; }
    [Export] public float RegeneracaoVidaMin { get; set; }
    [Export] public float RegeneracaoVidaMax { get; set; }
    [Export] public float RegeneracaoMana { get; set; }
    [Export] public float RegeneracaoManaMin { get; set; }
    [Export] public float RegeneracaoManaMax { get; set; }
    [Export] public int Hp { get; set; }
    [Export] public int HpMin { get; set; }
    [Export] public int HpMax { get; set; }
    [Export] public int Mana { get; set; }
    [Export] public int ManaMin { get; set; }
    [Export] public int ManaMax { get; set; }
    [Export] public int Stamina { get; set; }
    [Export] public int StaminaMin { get; set; }
    [Export] public int StaminaMax { get; set; }
    [Export] public float VelocidadeMovimento { get; set; }
    [Export] public float VelocidadeMovimentoMin { get; set; }
    [Export] public float VelocidadeMovimentoMax { get; set; }
    [Export] public float VelocidadeAtaque { get; set; }
    [Export] public float VelocidadeAtaqueMin { get; set; }
    [Export] public float VelocidadeAtaqueMax { get; set; }
    [Export] public float Precisao { get; set; }
    [Export] public float PrecisaoMin { get; set; }
    [Export] public float PrecisaoMax { get; set; }
    [Export] public float Tenacidade { get; set; }
    [Export] public float TenacidadeMin { get; set; }
    [Export] public float TenacidadeMax { get; set; }
    [Export] public int DanoPvp { get; set; }
    [Export] public int DanoPvpMin { get; set; }
    [Export] public int DanoPvpMax { get; set; }
    [Export] public int DefesaPvp { get; set; }
    [Export] public int DefesaPvpMin { get; set; }
    [Export] public int DefesaPvpMax { get; set; }
    [Export] public int PenetracaoArmadura { get; set; }
    [Export] public int PenetracaoArmaduraMin { get; set; }
    [Export] public int PenetracaoArmaduraMax { get; set; }
    [Export] public float ReducaoCooldown { get; set; }
    [Export] public float ReducaoCooldownMin { get; set; }
    [Export] public float ReducaoCooldownMax { get; set; }
    [Export] public float BonusExperiencia { get; set; }
    [Export] public float BonusExperienciaMin { get; set; }
    [Export] public float BonusExperienciaMax { get; set; }
    [Export] public float ChanceDropAumentada { get; set; }
    [Export] public float ChanceDropAumentadaMin { get; set; }
    [Export] public float ChanceDropAumentadaMax { get; set; }

    [ExportGroup("Visual")]
    [Export] public Texture2D SpritesheetEquipamento { get; set; }
    [Export] public SpriteFrames SpriteFramesEquipamento { get; set; }

    [ExportGroup("Animacao")]
    [Export] public string AnimacaoUsar { get; set; } = "";
    [Export] public string AnimacaoEquipado { get; set; } = "";

    [ExportGroup("Restricoes")]
    [Export] public string ClassesPermitidas { get; set; } = "";

    [ExportGroup("Comportamento")]
    [Export] public float TempoCooldown { get; set; } = 0f;
    [Export] public bool PodeDropar { get; set; } = true;
    [Export] public bool PodeTrocar { get; set; } = true;
    [Export] public bool PodeVender { get; set; } = true;
    [Export] public bool PodeArmazenarBanco { get; set; } = true;
    [Export] public bool PodeArmazenarBancoGuild { get; set; } = true;
    [Export] public bool DropEmPk { get; set; } = false;
    [Export] public float ChanceDrop { get; set; } = 0f;
    [Export] public float TempoDesaparecimento { get; set; } = 30f;
}