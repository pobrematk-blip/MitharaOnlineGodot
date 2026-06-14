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
        if (TemPersonagem) return true;
        return CarregarSalvo(out _, out _);
    }

    public bool PossuiArquivoSalvo()
    {
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save == null) return false;

        for (int i = 0; i < save.SlotsDisponiveis; i++)
        {
            if (save.SlotOcupado(i)) return true;
        }
        return false;
    }

    public int TotalSlotsOcupados()
    {
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        return save?.TotalSlotsOcupados() ?? 0;
    }

    public Godot.Collections.Array<int> SlotsOcupados()
    {
        var arr = new Godot.Collections.Array<int>();
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save != null)
            foreach (var s in save.SlotsOcupados())
                arr.Add(s);
        return arr;
    }

    public void ExcluirSalvo()
    {
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        save?.DeletarSlot(SlotAtivo);

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
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save == null) return false;

        int slot = save.Conta?.UltimoSlotSelecionado ?? SlotPadrao;
        if (!save.SlotOcupado(slot))
        {
            var ocupados = save.SlotsOcupados();
            if (ocupados.Count == 0) return false;
            slot = ocupados[0];
        }

        var cfg = new ConfigFile();
        if (cfg.Load(save.SlotPath(slot)) != Error.Ok) return false;

        string pathClasse = cfg.GetValue("personagem", "classe", "").AsString();
        string pathRaca = cfg.GetValue("personagem", "raca", "").AsString();
        if (string.IsNullOrEmpty(pathClasse) || string.IsNullOrEmpty(pathRaca)) return false;

        var classe = ResourceLoader.Load<ClasseCustomResource>(pathClasse);
        var raca = ResourceLoader.Load<RacaResource>(pathRaca);
        if (classe == null || raca == null) return false;

        string nome = cfg.GetValue("personagem", "nome", "Aventureiro").AsString();
        resumo = $"{nome} — {raca.ObterNomeFaccao()} | {classe.NomeClasse} ({raca.NomeRaca})";
        return true;
    }

    public void Definir(ClasseCustomResource classe, RacaResource raca, string nome = null)
    {
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save == null)
        {
            GD.PrintErr("[PERSONAGEM] SaveManager não encontrado.");
            return;
        }

        int slot = save.EncontrarSlotVazio();
        if (slot < 0)
        {
            GD.PrintErr("[PERSONAGEM] Sem slots disponíveis.");
            return;
        }

        SlotAtivo = slot;
        ClasseBase = classe;
        Raca = raca;
        NomePersonagem = string.IsNullOrWhiteSpace(nome) ? "Aventureiro" : nome.Trim();
        ResetarProgressaoSalva();

        save.SalvarSlot(slot, this);
        GD.Print($"[PERSONAGEM] Personagem criado no slot {slot}: {NomePersonagem}");
    }

    public void Salvar()
    {
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save != null && TemPersonagem)
            save.SalvarSlot(SlotAtivo, this);
    }

    public void SalvarProgressao(LevelProgressionComponent progressao, EquipamentoComponent equipamento)
    {
        if (!TemPersonagem || progressao == null || equipamento == null) return;

        NivelSalvo = progressao.Nivel;
        ExperienciaSalva = progressao.ExperienciaAtual;
        ForcaSalva = equipamento.Forca;
        AgilidadeSalva = equipamento.Agilidade;
        DestrezaSalva = equipamento.Destreza;
        InteligenciaSalva = equipamento.Inteligencia;
        PontosDisponiveisSalvos = equipamento.PontosDisponiveis;
        TemProgressaoSalva = true;

        EquipadosSalvos = new Dictionary<TipoEquipamento, int>();
        foreach (var kv in equipamento.ItensEquipados)
        {
            if (kv.Value?.Item != null)
                EquipadosSalvos[kv.Key] = kv.Value.Item.ItemID;
        }
        TemEquipadosSalvos = EquipadosSalvos.Count > 0;
        Salvar();
    }

    public void AplicarProgressaoSalva(LevelProgressionComponent progressao, EquipamentoComponent equipamento, ItemDatabase itemDB = null)
    {
        if (progressao == null || equipamento == null) return;

        if (TemProgressaoSalva)
        {
            progressao.DefinirProgresso(NivelSalvo, ExperienciaSalva);
            equipamento.ImportarEstado(
                ForcaSalva,
                AgilidadeSalva,
                DestrezaSalva,
                InteligenciaSalva,
                PontosDisponiveisSalvos);
        }

        if (TemEquipadosSalvos && itemDB != null)
        {
            equipamento.ImportarEquipamentos(EquipadosSalvos, itemDB);
        }
    }

    public bool CarregarSalvo(out ClasseCustomResource classe, out RacaResource raca)
    {
        classe = null;
        raca = null;

        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save == null) return false;

        int slot = save.Conta?.UltimoSlotSelecionado ?? SlotPadrao;
        if (!save.SlotOcupado(slot))
        {
            var ocupados = save.SlotsOcupados();
            if (ocupados.Count == 0) return false;
            slot = ocupados[0];
        }

        if (!CarregarSlot(slot))
            return false;

        classe = ClasseBase;
        raca = Raca;
        return true;
    }

    public bool CarregarSlot(int slotIndex)
    {
        var save = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (save == null) return false;

        ClasseBase = null;
        Raca = null;
        ResetarProgressaoSalva();

        if (!save.CarregarSlot(slotIndex, this))
        {
            GD.Print("[PERSONAGEM] Save inválido (classe ou raça removida). Recrie o personagem.");
            return false;
        }

        SlotAtivo = slotIndex;
        GD.Print($"[PERSONAGEM] Carregado: {NomePersonagem} | {Faccao?.NomeFaccao ?? "?"} | {ClasseBase?.NomeClasse ?? "?"} | {Raca?.NomeRaca ?? "?"}");
        return true;
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
