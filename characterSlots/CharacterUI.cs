using Godot;
using System.Collections.Generic;

public partial class CharacterUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private Button[] _tabBotoes;
    private ScrollContainer _petTabContent;
    private ScrollContainer _mountTabContent;

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private EquipamentoComponent _equipamento;
    private Player _player;
    private Label _pontosDisponiveisLabel;
    private TextureRect _standeePersonagem;

    private readonly Dictionary<string, Label> _atributosValores = new();
    private readonly Dictionary<string, Button> _atributosBotoes = new();
    private readonly List<SlotEquipamentoUI> _todosOsSlots = new();

    // Pet collection UI
    private PetColecaoComponent _petColecao;
    private GridContainer _petGrid;
    private Control _petDetalhesPanel;
    private TextureRect _petDetalhesIcone;
    private Label _petDetalhesNome;
    private Label _petDetalhesDescricao;
    private Label _petDetalhesStats;
    private int _petSelecionadoId = -1;
    private TextureButton _toggleButton;
    private static readonly Color PetPanelBg = new(0.015f, 0.018f, 0.026f, 0.88f);
    private static readonly Color PetPanelBorder = new(0.18f, 0.58f, 0.82f, 0.85f);
    private static readonly Color PetGold = new(1.0f, 0.86f, 0.12f, 1f);

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");

        if (HasNode("%ForcaValorLabel")) _atributosValores["Forca"] = GetNode<Label>("%ForcaValorLabel");
        if (HasNode("%AgilidadeValorLabel")) _atributosValores["Agilidade"] = GetNode<Label>("%AgilidadeValorLabel");
        if (HasNode("%DestrexaValorLabel")) _atributosValores["Destreza"] = GetNode<Label>("%DestrexaValorLabel");
        if (HasNode("%InteligenciaValorLabel")) _atributosValores["Inteligencia"] = GetNode<Label>("%InteligenciaValorLabel");

        if (HasNode("%ForcaBotaoPlus")) _atributosBotoes["Forca"] = GetNode<Button>("%ForcaBotaoPlus");
        if (HasNode("%AgilidadeBotaoPlus")) _atributosBotoes["Agilidade"] = GetNode<Button>("%AgilidadeBotaoPlus");
        if (HasNode("%DestrexaBotaoPlus")) _atributosBotoes["Destreza"] = GetNode<Button>("%DestrexaBotaoPlus");
        if (HasNode("%InteligenciaBotaoPlus")) _atributosBotoes["Inteligencia"] = GetNode<Button>("%InteligenciaBotaoPlus");

        if (HasNode("%PontosDisponiveisLabel"))
            _pontosDisponiveisLabel = GetNode<Label>("%PontosDisponiveisLabel");

        _standeePersonagem = GetNode<TextureRect>("Panel/ContentHBox/ColunaCentro/StandeePersonagem");
        AtualizarTexturaPersonagem();

        foreach (var kvp in _atributosBotoes)
        {
            string atributo = kvp.Key;
            Button botao = kvp.Value;
            botao.Pressed += () => OnAtributoPlus(atributo);
        }

        Visible = true;
        _panel.Visible = false;

        _closeButton.Pressed += OnCloseButtonPressed;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        BuscarTodosOsSlots();
        CallDeferred(MethodName.CentralizarPainelNaTela);
        CallDeferred(MethodName.ConectarEquipamento);
        CallDeferred(MethodName.ConectarPlayerStatus);
        CallDeferred(MethodName.CriarBotaoToggle);

        CallDeferred(MethodName.ConfigurarAbas);
    }

    private void ConfigurarAbas()
    {
        var contentHBox = _panel.GetNodeOrNull<Control>("ContentHBox");
        if (contentHBox == null) return;

        _panel.CustomMinimumSize = new Vector2(560, 510);

        var tabBar = new HBoxContainer();
        tabBar.Name = "TabBar";
        tabBar.AnchorTop = 0;
        tabBar.AnchorBottom = 0;
        tabBar.OffsetLeft = 0;
        tabBar.OffsetTop = 28;
        tabBar.OffsetRight = 0;
        tabBar.OffsetBottom = 52;
        tabBar.Alignment = BoxContainer.AlignmentMode.Center;
        _panel.AddChild(tabBar);

        string[] tabNames = { "Equipamento", "Pets", "Montarias" };
        _tabBotoes = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            var btn = new Button();
            btn.Text = tabNames[i];
            btn.ToggleMode = true;
            btn.ButtonPressed = i == 0;
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            int captured = i;
            btn.Pressed += () => OnTabSelected(captured);
            tabBar.AddChild(btn);
            _tabBotoes[i] = btn;
        }

        contentHBox.OffsetTop = 54;

        _petTabContent = new ScrollContainer();
        _petTabContent.Name = "PetTabContent";
        _petTabContent.AnchorLeft = 0;
        _petTabContent.AnchorTop = 0;
        _petTabContent.AnchorRight = 1;
        _petTabContent.AnchorBottom = 1;
        _petTabContent.OffsetLeft = 0;
        _petTabContent.OffsetTop = 54;
        _petTabContent.OffsetRight = 0;
        _petTabContent.OffsetBottom = -8;
        _petTabContent.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _petTabContent.Visible = false;
        _panel.AddChild(_petTabContent);

        _mountTabContent = new ScrollContainer();
        _mountTabContent.Name = "MountTabContent";
        _mountTabContent.AnchorLeft = 0;
        _mountTabContent.AnchorTop = 0;
        _mountTabContent.AnchorRight = 1;
        _mountTabContent.AnchorBottom = 1;
        _mountTabContent.OffsetLeft = 0;
        _mountTabContent.OffsetTop = 54;
        _mountTabContent.OffsetRight = 0;
        _mountTabContent.OffsetBottom = -8;
        _mountTabContent.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _mountTabContent.Visible = false;
        _panel.AddChild(_mountTabContent);

        CriarConteudoPets();
        CriarConteudoMontarias();

        CallDeferred(MethodName.ConectarPetColecao);
    }

    private void OnTabSelected(int index)
    {
        for (int i = 0; i < _tabBotoes.Length; i++)
            _tabBotoes[i].ButtonPressed = i == index;

        var hbox = _panel.GetNodeOrNull<Control>("ContentHBox");
        if (hbox != null) hbox.Visible = index == 0;
        if (_petTabContent != null) _petTabContent.Visible = index == 1;
        if (_mountTabContent != null) _mountTabContent.Visible = index == 2;
    }

    private void CriarConteudoPets()
    {
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 8);

        var gridWrapper = new Panel();
        gridWrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gridWrapper.SizeFlagsVertical = SizeFlags.ExpandFill;
        gridWrapper.AddThemeStyleboxOverride("panel", CriarPetStyle(PetPanelBg, PetPanelBorder, 6, 1));

        _petGrid = new GridContainer();
        _petGrid.Name = "PetGrid";
        _petGrid.Columns = 3;
        _petGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _petGrid.SizeFlagsVertical = SizeFlags.ExpandFill;
        _petGrid.AddThemeConstantOverride("h_separation", 8);
        _petGrid.AddThemeConstantOverride("v_separation", 8);
        _petGrid.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 10);

        var gridLabel = new Label();
        gridLabel.Text = "Selecione um pet para ver os detalhes";
        gridLabel.HorizontalAlignment = HorizontalAlignment.Center;
        gridLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gridLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        gridLabel.Modulate = new Color(0.85f, 0.9f, 1f);
        _petGrid.AddChild(gridLabel);

        gridWrapper.AddChild(_petGrid);
        vbox.AddChild(gridWrapper);

        var separator = new HSeparator();
        vbox.AddChild(separator);

        _petDetalhesPanel = new Panel();
        _petDetalhesPanel.Name = "PetDetalhes";
        _petDetalhesPanel.CustomMinimumSize = new Vector2(0, 160);
        _petDetalhesPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ((Panel)_petDetalhesPanel).AddThemeStyleboxOverride("panel", CriarPetStyle(new Color(0.02f, 0.025f, 0.036f, 0.90f), PetPanelBorder, 6, 1));

        var detalhesScroll = new ScrollContainer();
        detalhesScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detalhesScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        detalhesScroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 8);
        _petDetalhesPanel.AddChild(detalhesScroll);

        var detalhesHBox = new HBoxContainer();
        detalhesHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detalhesScroll.AddChild(detalhesHBox);

        _petDetalhesIcone = new TextureRect();
        _petDetalhesIcone.CustomMinimumSize = new Vector2(64, 64);
        _petDetalhesIcone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        detalhesHBox.AddChild(_petDetalhesIcone);

        var detalhesVBox = new VBoxContainer();
        detalhesVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detalhesHBox.AddChild(detalhesVBox);

        _petDetalhesNome = new Label();
        _petDetalhesNome.AddThemeFontSizeOverride("font_size", 18);
        _petDetalhesNome.AddThemeColorOverride("font_color", PetGold);
        detalhesVBox.AddChild(_petDetalhesNome);

        _petDetalhesDescricao = new Label();
        _petDetalhesDescricao.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _petDetalhesDescricao.SizeFlagsVertical = SizeFlags.ExpandFill;
        _petDetalhesDescricao.AddThemeColorOverride("font_color", new Color(0.86f, 0.91f, 1f, 1f));
        detalhesVBox.AddChild(_petDetalhesDescricao);

        _petDetalhesStats = new Label();
        _petDetalhesStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _petDetalhesStats.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.24f, 1f));
        detalhesVBox.AddChild(_petDetalhesStats);

        _petDetalhesPanel.Visible = false;
        vbox.AddChild(_petDetalhesPanel);

        _petTabContent.AddChild(vbox);
    }

    private void CriarConteudoMontarias()
    {
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.SizeFlagsVertical = SizeFlags.ExpandFill;

        var label = new Label();
        label.Text = "Montarias\n\nEm breve...";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.SizeFlagsVertical = SizeFlags.ExpandFill;
        label.Modulate = new Color(0.5f, 0.5f, 0.5f);
        vbox.AddChild(label);

        _mountTabContent.AddChild(vbox);
    }

    private void ConectarPetColecao()
    {
        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        if (_player == null) return;

        _petColecao = _player.FindChild("PetColecaoComponent", true, false) as PetColecaoComponent;
        if (_petColecao == null) return;

        _petColecao.ColecaoAtualizada += AtualizarGradePets;
        AtualizarGradePets();
    }

    private void AtualizarGradePets()
    {
        if (_petGrid == null || _petColecao == null) return;

        foreach (var child in _petGrid.GetChildren())
            child.QueueFree();

        var pets = _petColecao.GetPets();
        if (pets.Count == 0)
        {
            var vazio = new Label();
            vazio.Text = "Nenhum pet capturado ainda.\nCapture pets usando o Pergaminho do Pet!";
            vazio.HorizontalAlignment = HorizontalAlignment.Center;
            vazio.VerticalAlignment = VerticalAlignment.Center;
            vazio.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vazio.SizeFlagsVertical = SizeFlags.ExpandFill;
            vazio.Modulate = new Color(0.6f, 0.6f, 0.6f);
            _petGrid.AddChild(vazio);
            _petDetalhesPanel.Visible = false;
            _petSelecionadoId = -1;
            return;
        }

        foreach (var entry in pets)
        {
            var petBtn = new Button();
            petBtn.CustomMinimumSize = new Vector2(80, 80);
            petBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            petBtn.AddThemeStyleboxOverride("normal", CriarPetStyle(new Color(0.02f, 0.025f, 0.035f, 0.92f), new Color(0.15f, 0.48f, 0.72f, 0.95f), 5, 1));
            petBtn.AddThemeStyleboxOverride("hover", CriarPetStyle(new Color(0.04f, 0.06f, 0.085f, 0.96f), new Color(0.36f, 0.78f, 1f, 1f), 5, 1));
            petBtn.AddThemeStyleboxOverride("pressed", CriarPetStyle(new Color(0.07f, 0.08f, 0.10f, 1f), PetGold, 5, 1));

            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            var icon = new TextureRect();
            icon.CustomMinimumSize = new Vector2(48, 48);
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.Texture = entry.Recurso?.Icone;

            var nome = new Label();
            nome.Text = entry.Nome;
            nome.HorizontalAlignment = HorizontalAlignment.Center;
            nome.AddThemeFontSizeOverride("font_size", 10);
            nome.AddThemeColorOverride("font_color", PetGold);
            nome.AutowrapMode = TextServer.AutowrapMode.WordSmart;

            vbox.AddChild(icon);
            vbox.AddChild(nome);
            petBtn.AddChild(vbox);

            int capturedId = entry.PetID;
            petBtn.Pressed += () => TogglePet(capturedId);
            _petGrid.AddChild(petBtn);
        }

        if (_petSelecionadoId > 0)
            MostrarDetalhesPet(_petSelecionadoId);
    }

    private void MostrarDetalhesPet(int petId)
    {
        _petSelecionadoId = petId;

        var pets = _petColecao.GetPets();
        PetColecaoComponent.PetColecaoEntry entry = null;
        for (int i = 0; i < pets.Count; i++)
        {
            if (pets[i].PetID == petId)
            {
                entry = pets[i];
                break;
            }
        }

        if (entry == null) return;

        _petDetalhesIcone.Texture = entry.Recurso?.Icone;
        _petDetalhesNome.Text = entry.Nome;
        _petDetalhesDescricao.Text = entry.Recurso?.Descricao ?? "Sem descri??o";

        if (entry.Recurso != null)
        {
            var r = entry.Recurso;
            _petDetalhesStats.Text = $"Tipo: {(r.Tipo == TipoPet.Combate ? "Combate" : "Coleta")}\n" +
                $"HP: {r.HP}  |  Dano: {r.AttackDamage}\n" +
                $"Forca: {r.Forca}  Agilidade: {r.Agilidade}  Destreza: {r.Destreza}  Intel: {r.Inteligencia}\n" +
                $"Velocidade: {r.Speed}  Alcance: {r.AttackRange}";
        }
        else
        {
            _petDetalhesStats.Text = "";
        }

        _petDetalhesPanel.Visible = true;
    }

    private bool PetEstaAtivo(int petId)
    {
        if (_equipamento == null) return false;
        var slot = _equipamento.ObterSlot(TipoEquipamento.Pet);
        return slot != null && slot.Item != null && (slot.Item.ItemID - 200) == petId;
    }

    private void TogglePet(int petId)
    {
        MostrarDetalhesPet(petId);

        if (_equipamento == null) return;

        if (PetEstaAtivo(petId))
        {
            var slot = _equipamento.ObterSlot(TipoEquipamento.Pet);
            if (slot != null)
            {
                slot.Item = null;
                slot.Quantidade = 0;
                _equipamento.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
            }
            GD.Print($"[CHARACTER] Pet removido!");
            return;
        }

        var slotAtual = _equipamento.ObterSlot(TipoEquipamento.Pet);
        if (slotAtual == null)
        {
            slotAtual = new SlotInventario();
            _equipamento.ItensEquipados[TipoEquipamento.Pet] = slotAtual;
        }
        if (slotAtual != null && slotAtual.Item != null && (slotAtual.Item.ItemID - 200) != petId)
        {
            slotAtual.Item = null;
            slotAtual.Quantidade = 0;
        }

        var pets = _petColecao.GetPets();
        PetColecaoComponent.PetColecaoEntry entry = null;
        for (int i = 0; i < pets.Count; i++)
        {
            if (pets[i].PetID == petId)
            {
                entry = pets[i];
                break;
            }
        }

        if (entry == null) return;

        var itemPet = new ItemResource
        {
            ItemID = 200 + entry.PetID,
            Nome = entry.Nome,
            Descricao = entry.Recurso?.Descricao ?? $"Pet: {entry.Nome}",
            Icone = entry.Recurso?.Icone,
            Tipo = TipoEquipamento.Pet,
            Acumulavel = false,
            QuantidadeMaximaPorSlot = 1,
        };

        if (slotAtual != null)
        {
            slotAtual.Item = itemPet;
            slotAtual.Quantidade = 1;
            _equipamento.RecalcularBonusEquipamentos();
            _equipamento.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
            var controller = _player?.FindChild("PetController", true, false) as PetController;
            controller?.CallDeferred("OnEquipamentoAtualizado");
        }

        GD.Print($"[CHARACTER] Pet '{entry.Nome}' spawnado!");
    }

    private static StyleBoxFlat CriarPetStyle(Color bg, Color border, int radius, int borderWidth)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.BorderColor = border;
        style.BorderWidthTop = borderWidth;
        style.BorderWidthBottom = borderWidth;
        style.BorderWidthLeft = borderWidth;
        style.BorderWidthRight = borderWidth;
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
        return style;
    }

    private void OnCloseButtonPressed()
    {
        FecharPainel();
    }

    private void FecharPainel()
    {
        if (_panel == null) return;

        _panel.Visible = false;
        _arrastando = false;
        GD.Print("[CHARACTER UI] Tela de equipamentos fechada!");
    }

    private void CentralizarPainelNaTela()
    {
        if (_panel == null) return;

        Vector2 tamanhoDaTela = GetViewportRect().Size;
        _panel.Position = (tamanhoDaTela / 2) - (_panel.Size / 2);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                _arrastando = true;
                _pontoCliqueOriginal = mouseEvent.Position;
            }
            else
            {
                _arrastando = false;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void BuscarTodosOsSlots()
    {
        _todosOsSlots.Clear();

        if (_panel == null) return;

        foreach (Node no in _panel.FindChildren("*", recursive: true))
        {
            if (no is SlotEquipamentoUI slot)
                _todosOsSlots.Add(slot);
        }

        GD.Print($"[CHARACTER UI] {_todosOsSlots.Count} slots de equipamento encontrados nesta cena!");
    }

    private void ConectarEquipamento()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player == null)
        {
            GD.PrintErr("[CHARACTER UI] Player n?o encontrado!");
            return;
        }

        _equipamento = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (_equipamento == null)
        {
            GD.PrintErr("[CHARACTER UI] Player n?o tem EquipamentoComponent!");
            return;
        }

        GD.Print("[CHARACTER UI] Conectado ao EquipamentoComponent!");
        _equipamento.EquipamentoAtualizado += AtualizarTela;
        AtualizarTela();
    }

    private void ConectarPlayerStatus()
    {
        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        if (_player == null)
        {
            GD.PrintErr("[CHARACTER UI] Player n?o encontrado para status de vida/mana!");
            return;
        }

        _player.StatusAtualizado += AtualizarTela;
        GD.Print("[CHARACTER UI] Conectado ao Player para status de vida/mana!");

        AtualizarTexturaPersonagem();
    }

    private void AtualizarTexturaPersonagem()
    {
        if (_standeePersonagem == null || _player == null) return;

        AnimatedSprite2D playerAnimatedSprite = _player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite");
        if (playerAnimatedSprite != null && playerAnimatedSprite.SpriteFrames != null)
        {
            string currentAnimation = playerAnimatedSprite.Animation;
            int currentFrame = playerAnimatedSprite.Frame;
            Texture2D frameTexture = playerAnimatedSprite.SpriteFrames.GetFrameTexture(currentAnimation, currentFrame);

            if (frameTexture != null)
            {
                _standeePersonagem.Texture = frameTexture;
                return;
            }
        }

        _standeePersonagem.Texture = null;
        GD.Print("[CHARACTER UI] Nenhuma textura do personagem encontrada!");
    }

    private void AtualizarTela()
    {
        if (_equipamento == null) return;

        if (_pontosDisponiveisLabel != null)
            _pontosDisponiveisLabel.Text = $"Pontos Disponiveis: {_equipamento.PontosDisponiveis}";

        foreach (var kvp in _atributosValores)
        {
            int valor = kvp.Key switch
            {
                "Forca" => _equipamento.Forca,
                "Agilidade" => _equipamento.Agilidade,
                "Destreza" => _equipamento.Destreza,
                "Inteligencia" => _equipamento.Inteligencia,
                _ => 0
            };

            kvp.Value.Text = valor.ToString();
        }

        AtualizarTexturaPersonagem();
        BuscarEAtualizarTodosOsStatus();
        AtualizarSlotsDaTela();
    }

    private void BuscarEAtualizarTodosOsStatus()
    {
        if (_equipamento == null) return;

        AtualizarStatusLabel("DanoFisico", $"Dano Fisico: {_equipamento.DanoFisico}");
        AtualizarStatusLabel("DanoMagico", $"Dano Magico: {_equipamento.DanoMagico}");

        if (_player != null)
        {
            AtualizarStatusLabel("Hp", $"Vida: {_player.CurrentHealth}/{_player.MaxHealth}");
            AtualizarStatusLabel("Mana", $"Mana: {_player.CurrentMana}/{_player.MaxMana}");
            AtualizarStatusLabel("Stamina", $"Stamina: {_player.CurrentStamina}/{_equipamento.Stamina}");
        }
        else
        {
            AtualizarStatusLabel("Hp", $"Vida: {_equipamento.Hp}");
            AtualizarStatusLabel("Mana", $"Mana: {_equipamento.Mana}");
            AtualizarStatusLabel("Stamina", "Stamina: --/--");
        }
        AtualizarStatusLabel("ChanceCritica", $"Chance Critica: {_equipamento.ChanceCritica:F1}%");
        AtualizarStatusLabel("DanoCritico", $"Dano Critico: {_equipamento.DanoCritico:F2}x");
        AtualizarStatusLabel("Evasao", $"Evasao: {_equipamento.Evasao:F1}%");
        AtualizarStatusLabel("VelocidadeMovimento", $"Vel. Movimento: {_equipamento.VelocidadeMovimento:F2}x");
        AtualizarStatusLabel("VelocidadeAtaque", $"Vel. Ataque: {_equipamento.VelocidadeAtaque:F2}x");
        AtualizarStatusLabel("DefesaFisica", $"Defesa Fisica: {_equipamento.DefesaFisica}");
        AtualizarStatusLabel("DefesaMagica", $"Defesa Magica: {_equipamento.DefesaMagica}");
        AtualizarStatusLabel("Precisao", $"Precisao: {_equipamento.Precisao}");
        AtualizarStatusLabel("Tenacidade", $"Tenacidade: {_equipamento.Tenacidade}");
        AtualizarStatusLabel("DanoPvp", $"Dano PvP: {_equipamento.DanoPvp}");
        AtualizarStatusLabel("DefesaPvp", $"Defesa PvP: {_equipamento.DefesaPvp}");
        AtualizarStatusLabel("PenetracaoArmadura", $"Pen. Armadura: {_equipamento.PenetracaoArmadura}");
        AtualizarStatusLabel("RegeneracaoVida", $"Regen Vida: {_equipamento.RegeneracaoVida}/s");
        AtualizarStatusLabel("RegeneracaoMana", $"Regen Mana: {_equipamento.RegeneracaoMana}/s");
        AtualizarStatusLabel("RouboVida", $"Roubo Vida: {_equipamento.RouboVida:F2}%");
        AtualizarStatusLabel("RouboMana", $"Roubo Mana: {_equipamento.RouboMana:F2}%");
        AtualizarStatusLabel("ReducaoCooldown", $"Red. Cooldown: {_equipamento.ReducaoCooldown:F2}%");
        AtualizarStatusLabel("BonusExperiencia", $"Bonus XP: {_equipamento.BonusExperiencia}%");
        AtualizarStatusLabel("ReflexaoDano", $"Reflexao Dano: {_equipamento.ReflexaoDano}%");
        AtualizarStatusLabel("ResistenciaControle", $"Resist. Controle: {_equipamento.ResistenciaControle:F1}%");
    }

    private void AtualizarStatusLabel(string nomeStatus, string texto)
    {
        string labelName = $"%{nomeStatus}Label";
        if (HasNode(labelName))
            GetNode<Label>(labelName).Text = texto;
    }

    private void AtualizarSlotsDaTela()
    {
        if (_equipamento == null) return;

        foreach (var slot in _todosOsSlots)
            slot.AtualizarSlot(_equipamento.ObterSlot(slot.TipoDeSlot));
    }

    private void OnAtributoPlus(string atributo)
    {
        if (_equipamento == null || _equipamento.PontosDisponiveis <= 0) return;

        switch (atributo)
        {
            case "Forca": _equipamento.AdicionarPontoForca(); break;
            case "Agilidade": _equipamento.AdicionarPontoAgilidade(); break;
            case "Destreza": _equipamento.AdicionarPontoDestreza(); break;
            case "Inteligencia": _equipamento.AdicionarPontoInteligencia(); break;
        }

        AtualizarTela();
    }

    private void CriarBotaoToggle()
    {
        _toggleButton = new TextureButton();
        _toggleButton.Name = "CharacterToggleButton";
        _toggleButton.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Character.png");
        _toggleButton.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Character Selecionado.png");
        _toggleButton.CustomMinimumSize = new Vector2(36, 36);
        _toggleButton.StretchMode = TextureButton.StretchModeEnum.KeepCentered;
        _toggleButton.Pressed += () =>
        {
            _panel.Visible = !_panel.Visible;
            _arrastando = false;
            if (_panel.Visible)
            {
                CentralizarPainelNaTela();
                AtualizarTela();
            }
        };
        AddChild(_toggleButton);
        AtualizarPosicaoBotao(_toggleButton);
        GetTree().Root.SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged()
    {
        if (_toggleButton != null)
            AtualizarPosicaoBotao(_toggleButton);
    }

    private void AtualizarPosicaoBotao(Control btn)
    {
        Vector2 tela = GetViewportRect().Size;
        btn.Position = new Vector2(tela.X - 44, tela.Y - 264);
    }

    public override void _ExitTree()
    {
        if (_toggleButton != null)
            GetTree().Root.SizeChanged -= OnSizeChanged;
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (@event.IsActionPressed("equipamento"))
        {
            _panel.Visible = !_panel.Visible;
            _arrastando = false;
            GetViewport().SetInputAsHandled();

            if (_panel.Visible)
            {
                GD.Print("[CHARACTER UI] Tela de equipamentos aberta!");
                AtualizarTela();
            }
            else
            {
                GD.Print("[CHARACTER UI] Tela de equipamentos fechada!");
            }
        }

        if (_panel.Visible && @event is InputEventMouseButton mouseEvent
            && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left
            && _closeButton.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
        {
            FecharPainel();
            GetViewport().SetInputAsHandled();
        }
    }
}
