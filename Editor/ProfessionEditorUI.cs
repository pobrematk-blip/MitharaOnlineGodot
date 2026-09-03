#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public partial class ProfessionEditorUI : Control
{
    private const string CatalogPath = "res://server/data/professions.json";

    private class ProfIngredient
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; } = 1;
        public string Nome { get; set; } = "";
    }

    private class ProfRecipe
    {
        public int RecipeItemId { get; set; }
        public string Nome { get; set; } = "";
        public int ProducedItemId { get; set; }
        public string ProducaoNome { get; set; } = "";
        public int ProducedQuantity { get; set; } = 1;
        public int CritBonusQuantity { get; set; } = 1;
        public int RequiredProfLevel { get; set; } = 1;
        public int XpReward { get; set; } = 10;
        public int GoldCost { get; set; } = 0;
        public int SuccessRate { get; set; } = 100;
        public bool Runa { get; set; }
        public List<ProfIngredient> Ingredientes { get; set; } = new();
    }

    private class ProfProfession
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public byte TipoByte { get; set; } = 6;
        public string NpcPrefab { get; set; } = "";
        public List<ProfRecipe> Receitas { get; set; } = new();
    }

    private class ProfCatalogFile
    {
        public int Version { get; set; } = 1;
        public List<ProfProfession> Profissoes { get; set; } = new();
    }

    private Panel _panel;
    private ItemList _profList;
    private ItemList _recipeList;
    private ItemList _ingList;
    private Label _statusLabel;

    private LineEdit _profIdEdit;
    private LineEdit _profNomeEdit;
    private SpinBox _tipoByteSpin;
    private LineEdit _npcPrefabEdit;

    private SpinBox _recipeIdSpin;
    private LineEdit _recipeNomeEdit;
    private SpinBox _producedIdSpin;
    private LineEdit _producedNomeEdit;
    private SpinBox _producedQtySpin;
    private SpinBox _critBonusSpin;
    private SpinBox _requiredLevelSpin;
    private SpinBox _xpSpin;
    private SpinBox _goldSpin;
    private SpinBox _successRateSpin;
    private CheckButton _runaCheck;

    private SpinBox _ingItemIdSpin;
    private SpinBox _ingQtySpin;

    private readonly List<ProfProfession> _profissoes = new();
    private int _selectedProf = -1;
    private int _selectedRecipe = -1;
    private bool _loading;

    public override void _Ready()
    {
        BuildUi();
        _panel.Visible = false;
        CallDeferred(nameof(LoadCatalog));
    }

    private void BuildUi()
    {
        _panel = new Panel { Name = "Panel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_panel);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 8;
        root.OffsetTop = 8;
        root.OffsetRight = -8;
        root.OffsetBottom = -8;
        _panel.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);
        header.AddChild(new Label
        {
            Text = "Editor de Profissoes e Mesas",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });

        AddHeaderButton(header, "Nova Profisso", NovaProfissao);
        AddHeaderButton(header, "Remover Profisso", RemoverProfissao);
        AddHeaderButton(header, "Nova Receita", NovaReceita);
        AddHeaderButton(header, "Remover Receita", RemoverReceita);
        AddHeaderButton(header, "Recarregar", LoadCatalog);
        AddHeaderButton(header, "Salvar Catlogo", SaveCatalog);

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(body);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        body.AddChild(left);

        left.AddChild(new Label { Text = "Profisses" });
        _profList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _profList.ItemSelected += OnProfSelecionada;
        left.AddChild(_profList);

        _statusLabel = new Label
        {
            Text = "Selecione ou crie uma profisso.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        left.AddChild(_statusLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(scroll);

        var form = new VBoxContainer();
        form.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(form);

        // ---- Secao profissao ----
        form.AddChild(new Label { Text = "Profissao / Mesa" });
        var profGrid = new GridContainer { Columns = 2 };
        profGrid.AddThemeConstantOverride("h_separation", 12);
        profGrid.AddThemeConstantOverride("v_separation", 4);
        form.AddChild(profGrid);

        _profIdEdit = AddLineRow(profGrid, "ID (nico):");
        _profNomeEdit = AddLineRow(profGrid, "Nome:");
        _npcPrefabEdit = AddLineRow(profGrid, "NPC da Mesa (prefab):");
        _tipoByteSpin = AddSpinRow(profGrid, "TipoByte (persistncia):", 1, 255, 6);

        form.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        // ---- Secao receitas ----
        form.AddChild(new Label { Text = "Receitas da Profisso" });
        _recipeList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 110),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _recipeList.ItemSelected += OnReceitaSelecionada;
        form.AddChild(_recipeList);

        var recipeGrid = new GridContainer { Columns = 2 };
        recipeGrid.AddThemeConstantOverride("h_separation", 12);
        recipeGrid.AddThemeConstantOverride("v_separation", 4);
        form.AddChild(recipeGrid);

        _recipeIdSpin = AddSpinRow(recipeGrid, "ID do item receita:", 1, 999999999, 2001);
        _recipeNomeEdit = AddLineRow(recipeGrid, "Nome da receita:");
        _producedIdSpin = AddSpinRow(recipeGrid, "Item produzido (ID):", 1, 999999999, 2301);
        _producedNomeEdit = AddLineRow(recipeGrid, "Nome do produzido:");
        _producedQtySpin = AddSpinRow(recipeGrid, "Quantidade produzida:", 1, 99, 1);
        _critBonusSpin = AddSpinRow(recipeGrid, "Bnus crtico (+qtd):", 0, 99, 1);
        _requiredLevelSpin = AddSpinRow(recipeGrid, "Nvel de profisso exigido:", 1, 10, 1);
        _xpSpin = AddSpinRow(recipeGrid, "XP por criao:", 0, 100000, 10);
        _goldSpin = AddSpinRow(recipeGrid, "Custo em gold:", 0, 100000000, 50);
        _successRateSpin = AddSpinRow(recipeGrid, "Chance de sucesso (%):", 1, 100, 100);
        _runaCheck = new CheckButton { Text = "? runa (afeta mensagem do craft antigo)" };
        recipeGrid.AddChild(new Label { Text = "" });
        recipeGrid.AddChild(_runaCheck);

        form.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        // ---- Secao ingredientes ----
        form.AddChild(new Label { Text = "Ingredientes da receita selecionada" });
        _ingList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 90),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        form.AddChild(_ingList);

        var ingRow = new HBoxContainer();
        ingRow.AddThemeConstantOverride("separation", 8);
        form.AddChild(ingRow);

        ingRow.AddChild(new Label { Text = "Item ID:" });
        _ingItemIdSpin = NewSpin(1, 999999999, 107);
        ingRow.AddChild(_ingItemIdSpin);

        ingRow.AddChild(new Label { Text = "Qtd:" });
        _ingQtySpin = NewSpin(1, 9999, 3);
        ingRow.AddChild(_ingQtySpin);

        var addIngBtn = new Button { Text = "Adicionar" };
        addIngBtn.Pressed += AdicionarIngrediente;
        ingRow.AddChild(addIngBtn);

        var remIngBtn = new Button { Text = "Remover Selecionado" };
        remIngBtn.Pressed += RemoverIngrediente;
        ingRow.AddChild(remIngBtn);

        WireAutoStore(form);
    }

    private static void AddHeaderButton(HBoxContainer header, string text, Action callback)
    {
        var btn = new Button { Text = text };
        btn.Pressed += () => callback();
        header.AddChild(btn);
    }

    private static LineEdit AddLineRow(GridContainer grid, string label)
    {
        grid.AddChild(new Label { Text = label });
        var edit = new LineEdit { CustomMinimumSize = new Vector2(260, 0) };
        grid.AddChild(edit);
        return edit;
    }

    private static SpinBox AddSpinRow(GridContainer grid, string label, double min, double max, double value)
    {
        grid.AddChild(new Label { Text = label });
        var spin = NewSpin(min, max, value);
        grid.AddChild(spin);
        return spin;
    }

    private static SpinBox NewSpin(double min, double max, double value)
    {
        return new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Value = value,
            Step = 1,
            CustomMinimumSize = new Vector2(120, 0),
        };
    }

    private void WireAutoStore(Control control)
    {
        foreach (var child in control.GetChildren())
        {
            if (child is Control childControl)
                WireAutoStore(childControl);

            switch (child)
            {
                case LineEdit line:
                    line.TextChanged += _ => StoreCurrent();
                    break;
                case SpinBox spin:
                    spin.ValueChanged += _ => StoreCurrent();
                    break;
                case CheckButton check:
                    check.Toggled += _ => StoreCurrent();
                    break;
            }
        }
    }

    private ProfProfession? ProfAtual =>
        _selectedProf >= 0 && _selectedProf < _profissoes.Count ? _profissoes[_selectedProf] : null;

    private ProfRecipe? ReceitaAtual
    {
        get
        {
            var p = ProfAtual;
            if (p == null || _selectedRecipe < 0 || _selectedRecipe >= p.Receitas.Count) return null;
            return p.Receitas[_selectedRecipe];
        }
    }

    private void NovaProfissao()
    {
        int proximo = _profissoes.Count + 1;
        var nova = new ProfProfession
        {
            Id = $"profissao_{proximo}",
            Nome = $"Nova Profisso {proximo}",
            TipoByte = (byte)Math.Min(255, 11 + _profissoes.Count),
            NpcPrefab = "",
            Receitas = new List<ProfRecipe>(),
        };
        _profissoes.Add(nova);
        RefreshProfList();
        SelectProf(_profissoes.Count - 1);
    }

    private void RemoverProfissao()
    {
        if (ProfAtual == null) return;
        _profissoes.RemoveAt(_selectedProf);
        _selectedProf = -1;
        _selectedRecipe = -1;
        RefreshProfList();
        RefreshRecipeForm(null);
        RefreshRecipeList(null);
    }

    private void NovaReceita()
    {
        var p = ProfAtual;
        if (p == null)
        {
            _statusLabel.Text = "Crie/selecione uma profisso primeiro.";
            return;
        }

        var receita = new ProfRecipe
        {
            RecipeItemId = SugerirIdReceita(),
            Nome = $"Nova Receita NV.1",
            ProducedItemId = 2301,
            ProducedQuantity = 1,
        };
        p.Receitas.Add(receita);
        RefreshRecipeList(p);
        SelectRecipe(p.Receitas.Count - 1);
    }

    private int SugerirIdReceita()
    {
        int id = 2500;
        var usados = _profissoes.SelectMany(x => x.Receitas).Select(r => r.RecipeItemId).ToHashSet();
        while (usados.Contains(id)) id++;
        return id;
    }

    private void RemoverReceita()
    {
        var p = ProfAtual;
        if (p == null || ReceitaAtual == null) return;
        p.Receitas.RemoveAt(_selectedRecipe);
        _selectedRecipe = -1;
        RefreshRecipeList(p);
        RefreshRecipeForm(null);
        RefreshIngList(null);
    }

    private void AdicionarIngrediente()
    {
        var r = ReceitaAtual;
        if (r == null)
        {
            _statusLabel.Text = "Selecione uma receita primeiro.";
            return;
        }
        if (r.Ingredientes.Any(i => i.ItemId == (int)_ingItemIdSpin.Value))
        {
            _statusLabel.Text = "Esse item j  ingrediente desta receita.";
            return;
        }
        r.Ingredientes.Add(new ProfIngredient
        {
            ItemId = (int)_ingItemIdSpin.Value,
            Quantity = (int)_ingQtySpin.Value,
        });
        RefreshIngList(r);
    }

    private void RemoverIngrediente()
    {
        var r = ReceitaAtual;
        if (r == null) return;
        int[] selected = _ingList.GetSelectedItems();
        if (selected.Length == 0) return;
        int idx = selected[0];
        if (idx < 0 || idx >= r.Ingredientes.Count) return;
        r.Ingredientes.RemoveAt(idx);
        RefreshIngList(r);
    }

    private void OnProfSelecionada(long index)
    {
        StoreCurrent();
        SelectProf((int)index);
    }

    private void SelectProf(int index)
    {
        _selectedProf = index;
        _selectedRecipe = -1;
        _profList.Select(index);

        var p = ProfAtual;
        if (p == null) return;
        _loading = true;
        _profIdEdit.Text = p.Id;
        _profNomeEdit.Text = p.Nome;
        _npcPrefabEdit.Text = p.NpcPrefab;
        _tipoByteSpin.Value = p.TipoByte;
        _loading = false;

        RefreshRecipeList(p);
        RefreshRecipeForm(null);
        RefreshIngList(null);
    }

    private void OnReceitaSelecionada(long index)
    {
        StoreCurrent();
        SelectRecipe((int)index);
    }

    private void SelectRecipe(int index)
    {
        var p = ProfAtual;
        if (p == null) return;
        _selectedRecipe = index;
        _recipeList.Select(index);

        var r = ReceitaAtual;
        if (r == null) return;
        _loading = true;
        _recipeIdSpin.Value = r.RecipeItemId;
        _recipeNomeEdit.Text = r.Nome;
        _producedIdSpin.Value = r.ProducedItemId;
        _producedNomeEdit.Text = r.ProducaoNome;
        _producedQtySpin.Value = r.ProducedQuantity;
        _critBonusSpin.Value = r.CritBonusQuantity;
        _requiredLevelSpin.Value = r.RequiredProfLevel;
        _xpSpin.Value = r.XpReward;
        _goldSpin.Value = r.GoldCost;
        _successRateSpin.Value = r.SuccessRate;
        _runaCheck.ButtonPressed = r.Runa;
        _loading = false;

        RefreshIngList(r);
    }

    private void StoreCurrent()
    {
        if (_loading) return;
        var p = ProfAtual;
        if (p == null) return;

        p.Id = _profIdEdit.Text.Trim();
        p.Nome = _profNomeEdit.Text.Trim();
        p.NpcPrefab = _npcPrefabEdit.Text.Trim();
        p.TipoByte = (byte)_tipoByteSpin.Value;

        var r = ReceitaAtual;
        if (r != null)
        {
            r.RecipeItemId = (int)_recipeIdSpin.Value;
            r.Nome = _recipeNomeEdit.Text;
            r.ProducedItemId = (int)_producedIdSpin.Value;
            r.ProducaoNome = _producedNomeEdit.Text;
            r.ProducedQuantity = (int)_producedQtySpin.Value;
            r.CritBonusQuantity = (int)_critBonusSpin.Value;
            r.RequiredProfLevel = (int)_requiredLevelSpin.Value;
            r.XpReward = (int)_xpSpin.Value;
            r.GoldCost = (int)_goldSpin.Value;
            r.SuccessRate = (int)_successRateSpin.Value;
            r.Runa = _runaCheck.ButtonPressed;
        }

        RefreshProfList(p);
        RefreshRecipeList(p);
    }

    private void RefreshProfList(ProfProfession? manter = null)
    {
        long manterId = manter?.GetHashCode() ?? -1;
        _profList.Clear();
        for (int i = 0; i < _profissoes.Count; i++)
        {
            var p = _profissoes[i];
            _profList.AddItem($"{p.Nome} ({p.Id}) - {p.Receitas.Count} receitas");
            if (manter != null && p.GetHashCode() == manterId)
            {
                _profList.Select(i);
                _selectedProf = i;
            }
        }
    }

    private void RefreshRecipeList(ProfProfession? p)
    {
        _recipeList.Clear();
        if (p == null) return;
        foreach (var r in p.Receitas)
            _recipeList.AddItem($"#{r.RecipeItemId} - {(string.IsNullOrWhiteSpace(r.Nome) ? "(sem nome)" : r.Nome)}");
    }

    private void RefreshIngList(ProfRecipe? r)
    {
        _ingList.Clear();
        if (r == null) return;
        foreach (var ing in r.Ingredientes)
            _ingList.AddItem($"{(string.IsNullOrEmpty(ing.Nome) ? $"#{ing.ItemId}" : ing.Nome)} x{ing.Quantity}");
    }

    private void RefreshRecipeForm(ProfRecipe? r)
    {
        _loading = true;
        if (r == null)
        {
            _recipeNomeEdit.Text = "";
            _producedNomeEdit.Text = "";
        }
        _loading = false;
    }

    private void LoadCatalog()
    {
        _profissoes.Clear();
        _selectedProf = -1;
        _selectedRecipe = -1;

        string absolutePath = ProjectSettings.GlobalizePath(CatalogPath);
        if (File.Exists(absolutePath))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var catalog = JsonSerializer.Deserialize<ProfCatalogFile>(File.ReadAllText(absolutePath), options);
                if (catalog?.Profissoes != null)
                {
                    foreach (var p in catalog.Profissoes)
                        _profissoes.Add(Normalizar(p));
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Erro ao ler catlogo: {ex.Message}";
            }
        }

        if (_profissoes.Count == 0)
            SeedDefault();

        RefreshProfList();
        if (_profissoes.Count > 0)
            SelectProf(0);

        _statusLabel.Text = $"Carregado: {_profissoes.Count} profisso(es).";
    }

    private static ProfProfession Normalizar(ProfProfession p)
    {
        foreach (var r in p.Receitas)
        {
            r.ProducaoNome ??= "";
            foreach (var ing in r.Ingredientes)
                ing.Nome ??= "";
        }
        return p;
    }

    private void SeedDefault()
    {
        _profissoes.Add(new ProfProfession
        {
            Id = "alquimista",
            Nome = "Alquimista",
            TipoByte = 6,
            NpcPrefab = "alquimista_herbert",
            Receitas = new List<ProfRecipe>
            {
                new()
                {
                    RecipeItemId = 2001,
                    Nome = "Receita: Poo de Vida NV.1",
                    ProducedItemId = 2301,
                    ProducaoNome = "Poo de Vida NV.1",
                    RequiredProfLevel = 1,
                    XpReward = 8,
                    GoldCost = 50,
                    SuccessRate = 100,
                    Ingredientes = new List<ProfIngredient>
                    {
                        new() { ItemId = 107, Quantity = 3 },
                        new() { ItemId = 204, Quantity = 1 },
                    },
                },
            },
        });
    }

    private void SaveCatalog()
    {
        StoreCurrent();

        string absolutePath = ProjectSettings.GlobalizePath(CatalogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        var catalog = new ProfCatalogFile
        {
            Version = 1,
            Profissoes = _profissoes,
        };
        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(absolutePath, json);

        _statusLabel.Text = $"Catlogo salvo: {CatalogPath}\nReinicie o servidor para aplicar.";
    }
}
