using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class PersonagemEscolhido : Node
{
    public static readonly int SlotPadrao = 0;

    public ClasseCustomResource ClasseBase { get; set; }
    public RacaResource Raca { get; set; }
    public FaccaoResource Faccao => Raca?.Faccao;
    public string NomePersonagem { get; set; } = "Aventureiro";

    public bool TemPersonagem => ClasseBase != null && Raca != null;

    public int SlotAtivo { get; set; } = SlotPadrao;
    public int NivelSalvo { get; set; } = LevelProgressionUtil.NivelInicial;
    public int ExperienciaSalva { get; set; }
    public int ForcaSalva { get; set; }
    public int AgilidadeSalva { get; set; }
    public int DestrezaSalva { get; set; }
    public int InteligenciaSalva { get; set; }
    public int PontosDisponiveisSalvos { get; set; }
    public bool TemProgressaoSalva { get; set; }

    public System.Collections.Generic.Dictionary<TipoEquipamento, int> EquipadosSalvos { get; set; }
    public bool TemEquipadosSalvos { get; set; }

    public string CabeloPath { get; set; }
    public string BarbaPath { get; set; }
    public Color CabeloCor { get; set; } = Colors.White;
    public Color BarbaCor { get; set; } = Colors.White;

    public override void _Ready()
    {
        GarantirDadosCarregados();
    }

    public bool GarantirDadosCarregados()
    {
        return TemPersonagem;
    }

    public bool PossuiArquivoSalvo()
    {
        return false;
    }

    public int TotalSlotsOcupados()
    {
        return 0;
    }

    public Godot.Collections.Array<int> SlotsOcupados()
    {
        var arr = new Godot.Collections.Array<int>();
        return arr;
    }

    public void ExcluirSalvo()
    {
        ClasseBase = null;
        Raca = null;
        NomePersonagem = "Aventureiro";
        ResetarProgressaoSalva();
        SlotAtivo = SlotPadrao;

        GD.Print("[PERSONAGEM] Personagem deletado.");
    }

    public bool ObterResumoSalvo(out string resumo)
    {
        resumo = "";
        return false;
    }

    public void Definir(ClasseCustomResource classe, RacaResource raca, string nome = null)
    {
        SlotAtivo = SlotPadrao;
        ClasseBase = classe;
        Raca = raca;
        NomePersonagem = string.IsNullOrWhiteSpace(nome) ? "Aventureiro" : nome.Trim();
        ResetarProgressaoSalva();
        GD.Print("[PERSONAGEM] Dados temporarios definidos. Persistencia local bloqueada; criacao real deve vir do servidor.");
    }

    public void Salvar()
    {
        GD.Print("[PERSONAGEM] Salvar local bloqueado. O jogo e 100% online.");
    }

    public void SalvarProgressao(LevelProgressionComponent progressao, EquipamentoComponent equipamento)
    {
        GD.Print("[PERSONAGEM] Progressao local bloqueada. XP/level/equipamentos devem vir do servidor.");
    }

    public void AplicarProgressaoSalva(LevelProgressionComponent progressao, EquipamentoComponent equipamento, ItemDatabase itemDB = null)
    {
        GD.Print("[PERSONAGEM] AplicarProgressaoSalva bloqueado. Estado do personagem vem do servidor.");
    }

    public bool CarregarSalvo(out ClasseCustomResource classe, out RacaResource raca)
    {
        classe = null;
        raca = null;

        return false;
    }

    public bool CarregarSlot(int slotIndex)
    {
        GD.PrintErr("[PERSONAGEM] Carregamento local bloqueado. Selecione um personagem vindo do servidor.");
        return false;
    }

    public ClasseCustomResource MontarClasseParaPlayer()
    {
        if (ClasseBase == null || Raca == null) return null;

        var copia = (ClasseCustomResource)ClasseBase.Duplicate(false);
        copia.Raca = Raca;

        if (copia.UsaProjetil && copia.CenaDoProjetil == null)
            copia.CenaDoProjetil = ResourceLoader.Load<PackedScene>("res://resources/Projetil/Projetil.tscn");

        return copia;
    }

    public void ResetarProgressaoSalva()
    {
        NivelSalvo = LevelProgressionUtil.NivelInicial;
        ExperienciaSalva = 0;
        ForcaSalva = 0;
        AgilidadeSalva = 0;
        DestrezaSalva = 0;
        InteligenciaSalva = 0;
        PontosDisponiveisSalvos = 0;
        TemProgressaoSalva = false;
        EquipadosSalvos = null;
        TemEquipadosSalvos = false;
    }
}
