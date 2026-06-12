using Godot;
using System.Collections.Generic;

public partial class SaveManager : Node
{
    public const string ContaPath = "user://conta.cfg";

    private string SaveDir => AccountId == 0
        ? "user://characters/"
        : $"user://characters_{AccountId}/";

    private int AccountId
    {
        get
        {
            var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            return net?.AccountId ?? 0;
        }
    }
    private const string PrefixoSlot = "slot_";
    private const string Extensao = ".cfg";

    private ContaData _contaData;

    public ContaData Conta
    {
        get
        {
            if (_contaData == null) CarregarConta();
            return _contaData;
        }
    }

    public int SlotsDisponiveis => Conta.SlotsMaximos;

    public override void _Ready()
    {
        GarantirPasta();
        CarregarConta();
    }

    private void GarantirPasta()
    {
        var userDir = DirAccess.Open("user://");
        if (userDir == null) return;

        string subPath = SaveDir.StartsWith("user://") ? SaveDir.Substring(7) : SaveDir;
        if (!userDir.DirExists(subPath))
        {
            var err = userDir.MakeDirRecursive(subPath);
            if (err != Error.Ok)
                GD.PrintErr($"[SAVE] Erro ao criar pasta {SaveDir}: {err}");
        }
    }

    public void CarregarConta()
    {
        _contaData = new ContaData();

        if (!FileAccess.FileExists(ContaPath)) return;

        var cfg = new ConfigFile();
        if (cfg.Load(ContaPath) != Error.Ok) return;

        _contaData.Diamantes = cfg.GetValue("conta", "diamantes", 100).AsInt32();
        _contaData.SlotsComprados = cfg.GetValue("conta", "slots_comprados", 0).AsInt32();
        _contaData.SlotsBase = cfg.GetValue("conta", "slots_base", 3).AsInt32();
        _contaData.UltimoSlotSelecionado = cfg.GetValue("conta", "ultimo_slot", 0).AsInt32();
        _contaData.AdminAtivado = cfg.GetValue("conta", "admin", false).AsBool();

        var accounts = cfg.GetValue("conta", "admin_accounts", new Godot.Collections.Array());
        _contaData.AdminAccountsList.Clear();
        foreach (var a in accounts.AsGodotArray())
            _contaData.AdminAccountsList.Add(a.AsString());
    }

    public void SalvarConta()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("conta", "diamantes", _contaData.Diamantes);
        cfg.SetValue("conta", "slots_comprados", _contaData.SlotsComprados);
        cfg.SetValue("conta", "slots_base", _contaData.SlotsBase);
        cfg.SetValue("conta", "ultimo_slot", _contaData.UltimoSlotSelecionado);
        cfg.SetValue("conta", "admin", _contaData.AdminAtivado);

        var accountsArr = new Godot.Collections.Array();
        foreach (var a in _contaData.AdminAccountsList)
            accountsArr.Add(a);
        cfg.SetValue("conta", "admin_accounts", accountsArr);

        var err = cfg.Save(ContaPath);
        if (err != Error.Ok)
            GD.PrintErr($"[SAVE] Erro ao salvar conta: {err}");
    }

    public string SlotPath(int slotIndex)
    {
        return SaveDir + PrefixoSlot + slotIndex + Extensao;
    }

    public bool SlotOcupado(int slotIndex)
    {
        return FileAccess.FileExists(SlotPath(slotIndex));
    }

    public int EncontrarSlotVazio()
    {
        for (int i = 0; i < SlotsDisponiveis; i++)
        {
            if (!SlotOcupado(i)) return i;
        }
        return -1;
    }

    public int TotalSlotsOcupados()
    {
        int count = 0;
        for (int i = 0; i < SlotsDisponiveis; i++)
        {
            if (SlotOcupado(i)) count++;
        }
        return count;
    }

    public List<int> SlotsOcupados()
    {
        var lista = new List<int>();
        for (int i = 0; i < SlotsDisponiveis; i++)
        {
            if (SlotOcupado(i)) lista.Add(i);
        }
        return lista;
    }

    public bool SalvarSlot(int slotIndex, PersonagemEscolhido personagem)
    {
        if (personagem == null || !personagem.TemPersonagem) return false;

        var cfg = new ConfigFile();
        cfg.SetValue("personagem", "classe", personagem.ClasseBase?.ResourcePath ?? "");
        cfg.SetValue("personagem", "raca", personagem.Raca?.ResourcePath ?? "");
        cfg.SetValue("personagem", "nome", personagem.NomePersonagem);
        cfg.SetValue("personagem", "cabelo_path", personagem.CabeloPath ?? "");
        cfg.SetValue("personagem", "barba_path", personagem.BarbaPath ?? "");
        cfg.SetValue("personagem", "cabelo_cor", personagem.CabeloCor.ToHtml());
        cfg.SetValue("personagem", "barba_cor", personagem.BarbaCor.ToHtml());

        if (personagem.Faccao != null && !string.IsNullOrEmpty(personagem.Faccao.ResourcePath))
            cfg.SetValue("personagem", "faccao", personagem.Faccao.ResourcePath);

        if (personagem.TemProgressaoSalva)
        {
            cfg.SetValue("progressao", "nivel", personagem.NivelSalvo);
            cfg.SetValue("progressao", "xp", personagem.ExperienciaSalva);
            cfg.SetValue("progressao", "forca", personagem.ForcaSalva);
            cfg.SetValue("progressao", "agilidade", personagem.AgilidadeSalva);
            cfg.SetValue("progressao", "destreza", personagem.DestrezaSalva);
            cfg.SetValue("progressao", "inteligencia", personagem.InteligenciaSalva);
            cfg.SetValue("progressao", "pontos", personagem.PontosDisponiveisSalvos);
        }

        var err = cfg.Save(SlotPath(slotIndex));
        if (err != Error.Ok)
        {
            GD.PrintErr($"[SAVE] Erro ao salvar slot {slotIndex}: {err}");
            return false;
        }

        Conta.UltimoSlotSelecionado = slotIndex;
        SalvarConta();
        GD.Print($"[SAVE] Slot {slotIndex} salvo: {personagem.NomePersonagem}");
        return true;
    }

    public bool CarregarSlot(int slotIndex, PersonagemEscolhido personagem)
    {
        if (!SlotOcupado(slotIndex))
        {
            GD.PrintErr($"[SAVE] Slot {slotIndex} vazio.");
            return false;
        }

        var cfg = new ConfigFile();
        if (cfg.Load(SlotPath(slotIndex)) != Error.Ok)
        {
            GD.PrintErr($"[SAVE] Erro ao ler slot {slotIndex}.");
            return false;
        }

        string pathClasse = cfg.GetValue("personagem", "classe", "").AsString();
        string pathRaca = cfg.GetValue("personagem", "raca", "").AsString();
        string nome = cfg.GetValue("personagem", "nome", "Aventureiro").AsString();

        if (string.IsNullOrEmpty(pathClasse) || string.IsNullOrEmpty(pathRaca))
        {
            GD.PrintErr($"[SAVE] Slot {slotIndex}: dados inválidos.");
            return false;
        }

        var classe = ResourceLoader.Load<ClasseCustomResource>(pathClasse);
        var raca = ResourceLoader.Load<RacaResource>(pathRaca);

        if (classe == null || raca == null)
        {
            GD.Print($"[SAVE] Slot {slotIndex}: classe ou raça não encontrada.");
            return false;
        }

        personagem.ClasseBase = classe;
        personagem.Raca = raca;
        personagem.NomePersonagem = nome;
        personagem.CabeloPath = cfg.GetValue("personagem", "cabelo_path", "").AsString();
        personagem.BarbaPath = cfg.GetValue("personagem", "barba_path", "").AsString();
        personagem.CabeloCor = Color.FromHtml(cfg.GetValue("personagem", "cabelo_cor", "#ffffff").AsString());
        personagem.BarbaCor = Color.FromHtml(cfg.GetValue("personagem", "barba_cor", "#ffffff").AsString());

        if (cfg.HasSection("progressao"))
        {
            personagem.NivelSalvo = cfg.GetValue("progressao", "nivel", 1).AsInt32();
            personagem.ExperienciaSalva = cfg.GetValue("progressao", "xp", 0).AsInt32();
            personagem.ForcaSalva = cfg.GetValue("progressao", "forca", 0).AsInt32();
            personagem.AgilidadeSalva = cfg.GetValue("progressao", "agilidade", 0).AsInt32();
            personagem.DestrezaSalva = cfg.GetValue("progressao", "destreza", 0).AsInt32();
            personagem.InteligenciaSalva = cfg.GetValue("progressao", "inteligencia", 0).AsInt32();
            personagem.PontosDisponiveisSalvos = cfg.GetValue("progressao", "pontos", 0).AsInt32();
            personagem.TemProgressaoSalva = true;
        }
        else
        {
            personagem.ResetarProgressaoSalva();
        }

        Conta.UltimoSlotSelecionado = slotIndex;
        SalvarConta();

        GD.Print($"[SAVE] Slot {slotIndex} carregado: {nome} | {classe.NomeClasse} | {raca.NomeRaca}");
        return true;
    }

    public bool DeletarSlot(int slotIndex)
    {
        string path = SlotPath(slotIndex);
        if (!FileAccess.FileExists(path)) return false;

        var err = DirAccess.RemoveAbsolute(path);
        if (err == Error.Ok)
        {
            GD.Print($"[SAVE] Slot {slotIndex} deletado.");
            return true;
        }
        return false;
    }

    public void SalvarInventario(System.Collections.Generic.List<SlotInventario> slots)
    {
        var path = SlotPath(Conta.UltimoSlotSelecionado).Replace(".cfg", "_inv.json");
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return;
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Item == null) continue;
            list.Add(new Godot.Collections.Dictionary
            {
                ["slot"] = i,
                ["item_id"] = slots[i].Item.ItemID,
                ["quantity"] = slots[i].Quantidade,
            });
        }
        file.StoreString(Json.Stringify(list));
    }

    public void CarregarInventario(System.Collections.Generic.List<SlotInventario> slots, ItemDatabase itemDB)
    {
        var path = SlotPath(Conta.UltimoSlotSelecionado).Replace(".cfg", "_inv.json");
        if (!FileAccess.FileExists(path)) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return;
        var json = file.GetAsText();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = Json.ParseString(json).AsGodotArray<Godot.Collections.Dictionary>();
            if (data == null) return;
            foreach (var entry in data)
            {
                int slot = (int)entry["slot"];
                int itemId = (int)entry["item_id"];
                int qty = (int)entry["quantity"];
                if (slot >= 0 && slot < slots.Count)
                {
                    var resource = itemDB.GetItem(itemId);
                    if (resource != null)
                        slots[slot] = new SlotInventario(resource, qty);
                }
            }
        }
        catch { }
    }

    public bool IsAdmin => Conta.AdminAtivado;

    public void SetAdmin(bool ativo)
    {
        _contaData.AdminAtivado = ativo;
        SalvarConta();
        GD.Print($"[SAVE] Modo admin {(ativo ? "ativado" : "desativado")}.");
    }

    public bool ComprarSlot()
    {
        int preco = _contaData.PrecoSlot;
        if (_contaData.Diamantes < preco) return false;

        _contaData.Diamantes -= preco;
        _contaData.SlotsComprados++;
        SalvarConta();
        GD.Print($"[SAVE] Slot comprado! Total: {SlotsDisponiveis}");
        return true;
    }
}
