#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StationUI : Control
{
    private Panel _panel;
    private Button _closeButton;
    private Label _titleLabel;
    private Label _levelLabel;
    private ProgressBar _xpBar;
    private Label _critLabel;
    private ItemList _materiaisList;
    private GridContainer _slotsGrid;
    private readonly List<Button> _slotButtons = new();
    private Label _previewNome;
    private Label _previewProduz;
    private Label _previewReq;
    private Label _previewXpGold;
    private Label _previewChances;
    private Button _craftButton;
    private Label _statusLabel;

    private GameNetwork _gameNet;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private const int MaxSlots = 8;

    private string _profId = "";
    private Godot.Collections.Array _receitas = new();
    private readonly Dictionary<int, long> _estoque = new();
    private readonly Dictionary<int, string> _nomes = new();
    private readonly List<int> _stagedIds = new();
    private readonly Dictionary<int, int> _stagedQty = new();
    private readonly Dictionary<int, Texture2D> _iconesCache = new();

    private ItemList _receitasList;
    private Button _botaoColocar;
    private Button _comprarButton;
    private TextureRect _previewIcone;
    private VBoxContainer _ingredientesBox;
    private Label _goldLabel;
    private int _receitaSelecionadaIndex = -1;
    private int _receitaSelecionadaId;
    private ItemDatabase _itemDb;

    public override void _Ready()
    {
        Visible = false;

        _panel = new Panel();
        _panel.CustomMinimumSize = new Vector2(940, 540);
        _panel.OffsetRight = 940;
        _panel.OffsetBottom = 540;
        AddChild(_panel);

        var titleBar = new Panel();
        titleBar.OffsetRight = 940;
        titleBar.OffsetBottom = 28;
        _panel.AddChild(titleBar);

        _titleLabel = new Label();
        _titleLabel.OffsetRight = 900;
        _titleLabel.OffsetBottom = 28;
        _titleLabel.Text = "Mesa de Profissão";
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleBar.AddChild(_titleLabel);

        _closeButton = new Button();
        _closeButton.OffsetLeft = 912;
        _closeButton.OffsetRight = 936;
        _closeButton.OffsetTop = 4;
        _closeButton.OffsetBottom = 24;
        _closeButton.Text = "X";

        var closeNormal = new StyleBoxFlat();
        closeNormal.BgColor = new Color(0.8f, 0.15f, 0.15f);
        closeNormal.SetCornerRadiusAll(3);
        closeNormal.SetContentMarginAll(0);
        _closeButton.AddThemeStyleboxOverride("normal", closeNormal);

        var closeHover = new StyleBoxFlat();
        closeHover.BgColor = new Color(1f, 0.25f, 0.25f);
        closeHover.SetCornerRadiusAll(3);
        closeHover.SetContentMarginAll(0);
        _closeButton.AddThemeStyleboxOverride("hover", closeHover);

        var closePressed = new StyleBoxFlat();
        closePressed.BgColor = new Color(0.6f, 0.1f, 0.1f);
        closePressed.SetCornerRadiusAll(3);
        closePressed.SetContentMarginAll(0);
        _closeButton.AddThemeStyleboxOverride("pressed", closePressed);

        _closeButton.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _panel.AddChild(_closeButton);

        _levelLabel = new Label();
        _levelLabel.Position = new Vector2(10, 34);
        _levelLabel.Size = new Vector2(400, 20);
        _levelLabel.Text = "NV.1";
        _panel.AddChild(_levelLabel);

        _critLabel = new Label();
        _critLabel.Position = new Vector2(610, 34);
        _critLabel.Size = new Vector2(320, 20);
        _critLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _critLabel.Text = "Chance de Crítico: 5%";
        _panel.AddChild(_critLabel);

        _xpBar = new ProgressBar();
        _xpBar.Position = new Vector2(10, 56);
        _xpBar.Size = new Vector2(920, 16);
        _xpBar.MinValue = 0;
        _xpBar.MaxValue = 100;
        _panel.AddChild(_xpBar);

        var body = new HBoxContainer();
        body.Position = new Vector2(0, 80);
        body.Size = new Vector2(940, 420);
        body.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(body);

        // Coluna 0: receitas (aprendidas e disponiveis)
        var receitasCol = new VBoxContainer();
        receitasCol.CustomMinimumSize = new Vector2(190, 0);
        receitasCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(receitasCol);
        receitasCol.AddChild(new Label { Text = "Receitas" });

        var recScroll = new ScrollContainer();
        recScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        recScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        receitasCol.AddChild(recScroll);

        _receitasList = new ItemList();
        _receitasList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _receitasList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _receitasList.SelectMode = ItemList.SelectModeEnum.Single;
        _receitasList.ItemSelected += OnReceitaSelecionada;
        recScroll.AddChild(_receitasList);

        _botaoColocar = new Button { Text = "Colocar na Mesa", Disabled = true };
        _botaoColocar.Pressed += ColocarReceitaNaMesa;
        receitasCol.AddChild(_botaoColocar);

        _comprarButton = new Button { Text = "Comprar Receita", Disabled = true, Visible = false };
        _comprarButton.CustomMinimumSize = new Vector2(0, 30);
        _comprarButton.Pressed += OnComprarReceita;
        receitasCol.AddChild(_comprarButton);

        // Coluna esquerda: materiais disponiveis
        var leftCol = new VBoxContainer();
        leftCol.CustomMinimumSize = new Vector2(230, 0);
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(leftCol);
        leftCol.AddChild(new Label { Text = "Materiais Disponíveis" });

        var matScroll = new ScrollContainer();
        matScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        matScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftCol.AddChild(matScroll);

        _materiaisList = new ItemList();
        _materiaisList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _materiaisList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _materiaisList.SelectMode = ItemList.SelectModeEnum.Single;
        _materiaisList.ItemActivated += OnMaterialAtivado;
        matScroll.AddChild(_materiaisList);

        var dicaMats = new Label
        {
            Text = "Duplo clique adiciona à mesa.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        leftCol.AddChild(dicaMats);

        // Coluna central: slots da mesa
        var midCol = new VBoxContainer();
        midCol.CustomMinimumSize = new Vector2(250, 0);
        midCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(midCol);
        midCol.AddChild(new Label { Text = "Mesa (clique no slot p/ devolver)" });

        var slotsCenter = new CenterContainer();
        slotsCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        midCol.AddChild(slotsCenter);

        _slotsGrid = new GridContainer();
        _slotsGrid.Columns = 4;
        _slotsGrid.AddThemeConstantOverride("h_separation", 6);
        _slotsGrid.AddThemeConstantOverride("v_separation", 6);
        slotsCenter.AddChild(_slotsGrid);

        for (int i = 0; i < MaxSlots; i++)
        {
            var slot = new Button();
            slot.CustomMinimumSize = new Vector2(56, 56);
            slot.Text = "";
            int captured = i;
            slot.Pressed += () => OnSlotClicado(captured);
            _slotsGrid.AddChild(slot);
            _slotButtons.Add(slot);
        }

        var botaoLimpar = new Button { Text = "Devolver Tudo" };
        botaoLimpar.Pressed += LimparStaging;
        midCol.AddChild(botaoLimpar);

        // Coluna direita: detalhes + craft
        var rightCol = new VBoxContainer();
        rightCol.CustomMinimumSize = new Vector2(240, 0);
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(rightCol);
        // --- Secao 1: resultado da receita ---
        rightCol.AddChild(new Label { Text = "DETALHES DA RECEITA" });

        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 10);
        rightCol.AddChild(previewRow);

        _previewIcone = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Texture = null,
        };
        previewRow.AddChild(_previewIcone);

        var previewTextos = new VBoxContainer();
        previewTextos.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        previewRow.AddChild(previewTextos);

        _previewNome = new Label
        {
            Text = "-",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _previewNome.AddThemeFontSizeOverride("font_size", 15);
        previewTextos.AddChild(_previewNome);

        _previewProduz = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        previewTextos.AddChild(_previewProduz);

        rightCol.AddChild(new HSeparator());

        // --- Secao 2: custos e chances ---
        _previewReq = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        rightCol.AddChild(_previewReq);

        _previewXpGold = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        rightCol.AddChild(_previewXpGold);

        _previewChances = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        rightCol.AddChild(_previewChances);

        rightCol.AddChild(new HSeparator());

        // --- Secao 3: ingredientes ---
        var ingredientesTitulo = new Label { Text = "INGREDIENTES" };
        ingredientesTitulo.AddThemeFontSizeOverride("font_size", 13);
        rightCol.AddChild(ingredientesTitulo);

        _ingredientesBox = new VBoxContainer();
        _ingredientesBox.AddThemeConstantOverride("separation", 4);
        rightCol.AddChild(_ingredientesBox);

        rightCol.AddChild(new HSeparator());

        // --- Secao 4: gold e craft ---
        _goldLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        rightCol.AddChild(_goldLabel);

        rightCol.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        _craftButton = new Button { Text = "CRAFT", Disabled = true };
        _craftButton.CustomMinimumSize = new Vector2(0, 44);
        _craftButton.Pressed += OnCraftPressionado;
        rightCol.AddChild(_craftButton);

        var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        rightCol.AddChild(spacer);

        _statusLabel = new Label();
        _statusLabel.Position = new Vector2(10, 506);
        _statusLabel.Size = new Vector2(740, 28);
        _statusLabel.Text = "";
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _panel.AddChild(_statusLabel);

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnCraftResult += OnCraftResult;
            _gameNet.OnGoldUpdate += OnGoldAtualizado;
            _gameNet.OnBuyRecipeResult += OnBuyRecipeResult;
        }

        _itemDb = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase");

        _closeButton.Pressed += Fechar;
        titleBar.GuiInput += OnTitleBarGuiInput;
    }

    public void AbrirComDados(string profId, string profNome, int level, long xp, int xpForNext,
        float critChancePct, Godot.Collections.Array receitas, Godot.Collections.Array estoque)
    {
        _profId = profId;
        _receitas = receitas;

        _estoque.Clear();
        _nomes.Clear();
        foreach (var entryObj in estoque)
        {
            var entry = entryObj.AsGodotDictionary();
            if (entry == null || entry.Count == 0) continue;
            int itemId = entry["itemId"].AsInt32();
            _estoque[itemId] = entry["quantidade"].AsInt64();
            _nomes[itemId] = entry["nome"].AsString();
        }

        _titleLabel.Text = $"Mesa de {profNome}";
        _levelLabel.Text = $"{profNome} NV.{level}";
        if (xpForNext > 0)
        {
            _levelLabel.Text += $" ({xp}/{xpForNext} XP)";
            _xpBar.MaxValue = xpForNext;
            _xpBar.Value = xp;
        }
        else
        {
            _levelLabel.Text += " (MAX)";
            _xpBar.MaxValue = 1;
            _xpBar.Value = 1;
        }

        _critLabel.Text = $"Chance de Crítico: {critChancePct:0}%";

        RecalcularEstoqueComStaging();
        RebuildReceitasList();
        RebuildMateriaisList();
        AtualizarSlots();
        AtualizarPreview();
        AtualizarGold();

        Visible = true;
        Centralizar();
    }

    private void RecalcularEstoqueComStaging()
    {
        foreach (int id in _stagedIds.ToList())
        {
            if (_stagedQty.TryGetValue(id, out int q) && q > 0) continue;
            _stagedIds.Remove(id);
            _stagedQty.Remove(id);
        }
    }

    private void RebuildMateriaisList()
    {
        _materiaisList.Clear();

        foreach (var recObj in _receitas)
        {
            var rec = recObj.AsGodotDictionary();
            if (rec == null || rec.Count == 0) continue;
            foreach (var ingObj in rec["ingredientes"].AsGodotArray())
            {
                var ing = ingObj.AsGodotDictionary();
                if (ing == null || ing.Count == 0) continue;
                int itemId = ing["itemId"].AsInt32();
                _nomes.TryAdd(itemId, ing["nome"].AsString());
            }
        }

        foreach (var kv in _estoque.OrderBy(kv => _nomes.GetValueOrDefault(kv.Key, $"#{kv.Key}")))
        {
            long staged = _stagedQty.TryGetValue(kv.Key, out int q) ? q : 0;
            long livre = kv.Value - staged;
            if (livre <= 0) continue;
            int idx = _materiaisList.AddItem($"{_nomes.GetValueOrDefault(kv.Key, $"#{kv.Key}")} x{livre}");
            var icone = ObterIcone(kv.Key);
            if (icone != null)
            {
                _materiaisList.SetItemIcon(idx, icone);
            }
        }
    }

    private List<int> MateriaisVisiveis()
    {
        var lista = new List<int>();
        foreach (var kv in _estoque.OrderBy(kv => _nomes.GetValueOrDefault(kv.Key, $"#{kv.Key}")))
        {
            long staged = _stagedQty.TryGetValue(kv.Key, out int q) ? q : 0;
            if (kv.Value - staged > 0)
                lista.Add(kv.Key);
        }
        return lista;
    }

    // Icone padronizado em 32px, igual ao tamanho usado nas demais janelas.
    private Texture2D ObterIcone(int itemId)
    {
        if (_iconesCache.TryGetValue(itemId, out var emCache))
            return emCache;

        Texture2D resultado = null;
        var original = _itemDb?.GetItem(itemId)?.Icone;
        if (original != null)
        {
            var img = original.GetImage();
            if (img != null)
            {
                int maior = Math.Max(img.GetWidth(), img.GetHeight());
                if (maior > 32)
                {
                    float fator = 32f / maior;
                    int largura = Math.Max(1, (int)MathF.Round(img.GetWidth() * fator));
                    int altura = Math.Max(1, (int)MathF.Round(img.GetHeight() * fator));
                    img.Resize(largura, altura, Image.Interpolation.Nearest);
                }
                resultado = ImageTexture.CreateFromImage(img);
            }
        }

        _iconesCache[itemId] = resultado;
        return resultado;
    }

    private void RebuildReceitasList()
    {
        _receitasList.Clear();
        int selecionar = -1;

        int index = 0;
        foreach (var recObj in _receitas)
        {
            var rec = recObj.AsGodotDictionary();
            if (rec == null || rec.Count == 0) continue;
            bool known = rec["known"].AsBool();
            if (!known) continue;
            int recipeItemId = rec["id"].AsInt32();
            string nome = rec["nome"].AsString();
            _receitasList.AddItem(nome);
            var icone = ObterIcone(recipeItemId);
            if (icone != null)
            {
                _receitasList.SetItemIcon(index, icone);
            }

            if (recipeItemId == _receitaSelecionadaId)
                selecionar = index;
            index++;
        }

        if (selecionar >= 0)
        {
            _receitasList.Select(selecionar);
            _receitaSelecionadaIndex = selecionar;
        }
        else
        {
            _receitaSelecionadaIndex = -1;
            _botaoColocar.Disabled = true;
            _comprarButton.Disabled = true;
        }
    }

    private Godot.Collections.Dictionary ReceitaPorIndice(long index)
    {
        int visivel = 0;
        foreach (var recObj in _receitas)
        {
            var rec = recObj.AsGodotDictionary();
            if (rec == null || rec.Count == 0) continue;
            if (visivel == index) return rec;
            visivel++;
        }
        return null;
    }

    private void OnReceitaSelecionada(long index)
    {
        var rec = ReceitaPorIndice(index);
        if (rec == null) return;
        _receitaSelecionadaIndex = (int)index;
        _receitaSelecionadaId = rec["id"].AsInt32();
        bool known = rec["known"].AsBool();
        _botaoColocar.Disabled = !known;
        _comprarButton.Disabled = known;
        AtualizarPreview();
    }

    private void ColocarReceitaNaMesa()
    {
        var rec = ReceitaPorIndice(_receitaSelecionadaIndex);
        if (rec == null) return;

        var necessarios = new List<(int id, int qtd)>();
        foreach (var ingObj in rec["ingredientes"].AsGodotArray())
        {
            var ing = ingObj.AsGodotDictionary();
            if (ing == null || ing.Count == 0) continue;
            necessarios.Add((ing["itemId"].AsInt32(), ing["quantity"].AsInt32()));
        }

        foreach (var (id, qtd) in necessarios)
        {
            long staged = _stagedQty.TryGetValue(id, out int q) ? q : 0;
            if (_estoque.GetValueOrDefault(id) - staged < qtd)
            {
                _statusLabel.Text = $"Materiais insuficientes: {_nomes.GetValueOrDefault(id, $"#{id}")}.";
                return;
            }
        }

        LimparStaging();
        foreach (var (id, qtd) in necessarios)
            for (int i = 0; i < qtd; i++)
                AdicionarMaterial(id);
    }

    private void OnGoldAtualizado(int gold) => AtualizarGold();

    private void AtualizarGold()
    {
        long gold = _gameNet?.Gold ?? 0;
        int custo = 0;
        var rec = ReceitaCorrespondente() ?? ReceitaPorIndice(_receitaSelecionadaIndex);
        if (rec != null)
            custo = rec["goldCost"].AsInt32();

        string texto = custo > 0 ? $"Gold: {gold}  (-{custo})" : $"Gold: {gold}";
        _goldLabel.Text = gold >= custo ? texto : $"{texto}  [gold insuficiente]";
    }

    private void OnMaterialAtivado(long index)
    {
        var visiveis = MateriaisVisiveis();
        if (index < 0 || index >= visiveis.Count) return;
        AdicionarMaterial(visiveis[(int)index]);
    }

    private void AdicionarMaterial(int itemId)
    {
        if (_stagedIds.Count == 0 || !_stagedIds.Contains(itemId))
        {
            if (_stagedIds.Count >= MaxSlots)
            {
                _statusLabel.Text = "A mesa está cheia. Devolva algum material primeiro.";
                return;
            }
            _stagedIds.Add(itemId);
            _stagedQty[itemId] = 0;
        }

        long disponivel = _estoque.GetValueOrDefault(itemId) - (_stagedQty.TryGetValue(itemId, out int atual) ? atual : 0);
        if (disponivel <= 0)
        {
            _statusLabel.Text = $"Sem mais {_nomes.GetValueOrDefault(itemId, $"#{itemId}")} no inventário.";
            return;
        }

        _stagedQty[itemId] = atual + 1;
        RebuildMateriaisList();
        AtualizarSlots();
        AtualizarPreview();
    }

    private void OnSlotClicado(int index)
    {
        if (index < 0 || index >= _stagedIds.Count) return;
        int itemId = _stagedIds[index];
        _stagedIds.RemoveAt(index);
        _stagedQty.Remove(itemId);
        RebuildMateriaisList();
        AtualizarSlots();
        AtualizarPreview();
    }

    private void LimparStaging()
    {
        _stagedIds.Clear();
        _stagedQty.Clear();
        RebuildMateriaisList();
        AtualizarSlots();
        AtualizarPreview();
    }

    private void AtualizarSlots()
    {
        for (int i = 0; i < _slotButtons.Count; i++)
        {
            var slot = _slotButtons[i];
            if (i < _stagedIds.Count)
            {
                int id = _stagedIds[i];
                int qty = _stagedQty.TryGetValue(id, out int q) ? q : 0;
                slot.Text = $"x{qty}";
                slot.Icon = ObterIcone(id);
                slot.ExpandIcon = false;
                slot.Disabled = false;
                slot.TooltipText = _nomes.GetValueOrDefault(id, $"#{id}");
            }
            else
            {
                slot.Text = "";
                slot.Icon = null;
                slot.TooltipText = "";
                slot.Disabled = true;
            }
        }
    }

    private static string TruncarNome(string nome)
        => nome.Length <= 12 ? nome : nome[..11] + "...";

    private Godot.Collections.Dictionary ReceitaCorrespondente()
    {
        foreach (var recObj in _receitas)
        {
            var rec = recObj.AsGodotDictionary();
            if (rec == null || rec.Count == 0) continue;
            var esperado = new Dictionary<int, int>();
            foreach (var ingObj in rec["ingredientes"].AsGodotArray())
            {
                var ing = ingObj.AsGodotDictionary();
                if (ing == null || ing.Count == 0) continue;
                esperado[ing["itemId"].AsInt32()] = ing["quantity"].AsInt32();
            }

            if (esperado.Count != _stagedIds.Count) continue;

            bool igual = true;
            foreach (var kv in esperado)
            {
                if (!_stagedIds.Contains(kv.Key) || _stagedQty.GetValueOrDefault(kv.Key) != kv.Value)
                {
                    igual = false;
                    break;
                }
            }
            if (!igual) continue;

            return rec;
        }
        return null;
    }

    private void AtualizarPreview()
    {
        var correspondente = ReceitaCorrespondente();
        var exibida = correspondente ?? ReceitaPorIndice(_receitaSelecionadaIndex);

        _previewIcone.Texture = null;
        PreencherIngredientes(exibida);

        if (exibida == null)
        {
            _previewNome.Text = _stagedIds.Count == 0
                ? "Selecione uma receita aprendida ao lado."
                : "Os materiais não formam uma receita conhecida.";
            _previewProduz.Text = "";
            _previewReq.Text = "";
            _previewXpGold.Text = "";
            _previewChances.Text = "";
            _goldLabel.Text = $"Gold: {_gameNet?.Gold ?? 0}";
            _craftButton.Disabled = true;
            return;
        }

        string produzNome = exibida["producedNome"].AsString();
        int produzQtd = exibida["producedQuantity"].AsInt32();
        int produzItemId = exibida["producedItemId"].AsInt32();
        int reqLevel = exibida["requiredLevel"].AsInt32();
        int xpReward = exibida["xpReward"].AsInt32();
        int goldCost = exibida["goldCost"].AsInt32();
        int successRate = exibida["successRate"].AsInt32();
        int critBonus = exibida["critBonus"].AsInt32();

        _previewIcone.Texture = ObterIcone(produzItemId);
        _previewNome.Text = exibida["nome"].AsString();
        _previewProduz.Text = $"Produz: {produzNome} x{produzQtd}";
        _previewReq.Text = $"Nível necessário: NV.{reqLevel}";
        _previewChances.Text = $"Sucesso: {successRate}% | Bônus crítico: +{critBonus}";

        bool known = exibida["known"].AsBool();
        if (known)
        {
            _previewXpGold.Text = $"+{xpReward} XP | Custo: {goldCost} gold";
            _craftButton.Disabled = correspondente == null;
            _comprarButton.Disabled = true;
        }
        else
        {
            int recipePrice = goldCost * 3;
            _previewXpGold.Text = $"Custo para aprender: {recipePrice} gold";
            _craftButton.Disabled = true;
            _comprarButton.Disabled = false;
        }

        if (known && correspondente == null && _stagedIds.Count > 0)
            _statusLabel.Text = "Os materiais na mesa não correspondem a esta receita.";

        AtualizarGold();
    }

    private void PreencherIngredientes(Godot.Collections.Dictionary rec)
    {
        foreach (var child in _ingredientesBox.GetChildren())
            child.QueueFree();

        if (rec == null) return;

        foreach (var ingObj in rec["ingredientes"].AsGodotArray())
        {
            var ing = ingObj.AsGodotDictionary();
            if (ing == null || ing.Count == 0) continue;
            int id = ing["itemId"].AsInt32();
            int qtd = ing["quantity"].AsInt32();
            long tem = _estoque.GetValueOrDefault(id);
            bool suficiente = tem >= qtd;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var icone = new TextureRect
            {
                CustomMinimumSize = new Vector2(24, 24),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Texture = ObterIcone(id),
            };
            row.AddChild(icone);

            var label = new Label { Text = $"{qtd}x {_nomes.GetValueOrDefault(id, $"#{id}")}" };
            label.TooltipText = suficiente ? $"Voce tem {tem}." : $"Voce tem apenas {tem}.";
            if (!suficiente)
                label.Text += $" (tem {tem})";
            row.AddChild(label);

            _ingredientesBox.AddChild(row);
        }
    }

    private void OnCraftPressionado()
    {
        if (string.IsNullOrEmpty(_profId)) return;
        var rec = ReceitaCorrespondente();
        if (rec == null) return;

        var materiais = _stagedIds.Select(id => (id, _stagedQty.GetValueOrDefault(id))).ToList();
        _gameNet?.SendStationCraft(_profId, materiais);
        _statusLabel.Text = "Criando...";
    }

    private void OnCraftResult(bool success, int recipeId, int producedItemId, string message)
    {
        _statusLabel.Text = message;
        if (!success) return;

        _stagedIds.Clear();
        _stagedQty.Clear();
        RebuildMateriaisList();
        AtualizarSlots();
        AtualizarPreview();
        AtualizarGold();
        _gameNet?.SendOpenStation(_profId);
    }

    private void OnComprarReceita()
    {
        if (string.IsNullOrEmpty(_profId)) return;
        var rec = ReceitaPorIndice(_receitaSelecionadaIndex);
        if (rec == null) return;
        if (rec["known"].AsBool()) return;

        int recipeId = rec["id"].AsInt32();
        int recipePrice = rec["goldCost"].AsInt32() * 3;
        long gold = _gameNet?.Gold ?? 0;

        if (gold < recipePrice)
        {
            _statusLabel.Text = $"Gold insuficiente. Necessário {recipePrice} gold.";
            return;
        }

        _gameNet?.SendBuyRecipe(recipeId, _profId);
        _statusLabel.Text = "Comprando receita...";
    }

    private void OnBuyRecipeResult(bool success, int recipeId, int goldCost, string message)
    {
        _statusLabel.Text = message;
        if (!success) return;

        _gameNet?.SendOpenStation(_profId);
    }

    public void Fechar() => Visible = false;

    private void Centralizar()
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        _panel.OffsetLeft = (int)((vpSize.X - _panel.CustomMinimumSize.X) / 2);
        _panel.OffsetTop = (int)((vpSize.Y - _panel.CustomMinimumSize.Y) / 2);
        _panel.OffsetRight = _panel.OffsetLeft + (int)_panel.CustomMinimumSize.X;
        _panel.OffsetBottom = _panel.OffsetTop + (int)_panel.CustomMinimumSize.Y;
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
            {
                _arrastando = true;
                _pontoCliqueOriginal = mouseEvent.Position;
            }
            else
                _arrastando = false;
        }
        else if (@event is InputEventMouseMotion motion && _arrastando)
        {
            _panel.OffsetLeft += (int)motion.Relative.X;
            _panel.OffsetTop += (int)motion.Relative.Y;
            _panel.OffsetRight += (int)motion.Relative.X;
            _panel.OffsetBottom += (int)motion.Relative.Y;
        }
    }
}
