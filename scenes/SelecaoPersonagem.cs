#nullable enable
using Godot;
using System.Collections.Generic;

public partial class SelecaoPersonagem : Control
{
    private Label _subTitulo;
    private VBoxContainer _listaContainer;
    private Button _btnJogar;
    private Button _btnCriarNovo;
    private Button _btnLoja;
    private Button _btnExcluir;
    private AcceptDialog _confirmacaoExcluir;
    private Control _vazio;
    private Control _personagemPreview;
    private AnimatedSprite2D _previewSprite;
    private Control _previewInfo;
    private Label _previewNome;
    private Label _previewNivel;
    private Label _previewClasse;
    private Label _previewRaca;
    private Label _previewFaccao;
    private TextureRect _previewEmblema;
    private Control _areaSprite;

    private readonly List<CharacterCard> _cards = new();
    private int? _slotSelecionado;
    private GameNetwork? _net;
    private ClasseRegistry? _registry;

    private class CharacterCard
    {
        public int SlotIndex;
        public string Nome;
        public int Nivel;
        public PanelContainer Panel;
        public Label NomeLabel;
        public Label ClasseLabel;
        public Label NivelLabel;
        public Label FaccaoLabel;
        public string ClassePath;
        public string RacaPath;
    }

    public override void _Ready()
    {
        _subTitulo = GetNode<Label>("%SubTitulo");
        _listaContainer = GetNode<VBoxContainer>("%ListaContainer");
        _btnJogar = GetNode<Button>("%BtnJogar");
        _btnCriarNovo = GetNode<Button>("%BtnCriarNovo");
        _btnLoja = GetNode<Button>("%BtnLoja");
        _btnExcluir = GetNode<Button>("%BtnExcluir");
        _vazio = GetNode<Control>("%Vazio");
        _personagemPreview = GetNode<Control>("%PersonagemPreview");
        _previewSprite = GetNode<AnimatedSprite2D>("%PreviewSprite");
        _previewInfo = GetNode<Control>("%PreviewInfo");
        _previewNome = GetNode<Label>("%PreviewNome");
        _previewNivel = GetNode<Label>("%PreviewNivel");
        _previewClasse = GetNode<Label>("%PreviewClasse");
        _previewRaca = GetNode<Label>("%PreviewRaca");
        _previewFaccao = GetNode<Label>("%PreviewFaccao");
        _previewEmblema = GetNode<TextureRect>("%PreviewEmblema");
        _areaSprite = GetNode<Control>("%AreaSprite");

        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _registry = GetNodeOrNull<ClasseRegistry>("/root/ClasseRegistry");
        _registry?.RecarregarTudo();

        _btnJogar.Pressed += OnJogar;
        _btnCriarNovo.Pressed += OnCriarNovo;
        _btnLoja.Pressed += OnAbrirLoja;
        _btnExcluir.Pressed += OnExcluir;

        _confirmacaoExcluir = new AcceptDialog();
        _confirmacaoExcluir.Title = "Excluir Personagem";
        _confirmacaoExcluir.DialogText = "Tem certeza que deseja excluir este personagem? Esta acao nao pode ser desfeita.";
        _confirmacaoExcluir.OkButtonText = "Excluir";
        _confirmacaoExcluir.AddCancelButton("Cancelar");
        _confirmacaoExcluir.Confirmed += ConfirmarExclusao;
        AddChild(_confirmacaoExcluir);

        PopulatarLista();
    }

    private void PopulatarLista()
    {
        foreach (var card in _cards)
        {
            card.Panel.QueueFree();
        }
        _cards.Clear();
        _slotSelecionado = null;

        bool online = _net != null && _net.Characters.Count > 0;

        if (online)
            PopulatarDeDadosServidor(_net!.Characters);
        else
            PopulatarDeSlotsLocais();

        var save = GetNode<SaveManager>("/root/SaveManager");
        int total = save.SlotsDisponiveis;
        _vazio.Visible = _cards.Count == 0;
        _btnJogar.Disabled = true;
        _btnExcluir.Disabled = true;
        _btnCriarNovo.Disabled = _cards.Count >= total;
        _btnCriarNovo.Text = _cards.Count >= total
            ? "Slots cheios"
            : "Criar novo personagem";
        _subTitulo.Text = _cards.Count == 0
            ? "Nenhum personagem"
            : $"Selecione seu personagem ({_cards.Count}/{total})";

        EsconderPreview();
    }

    private void PopulatarDeDadosServidor(List<CharacterEntry> characters)
    {
        foreach (var ch in characters)
        {
            string classePath = $"res://Classes/{ch.Class}.tres";
            string racaFile = ch.Race.Replace(" ", "");
            string racaPath = $"res://Racas/{racaFile}.tres";

            var classe = ResourceLoader.Load<ClasseCustomResource>(classePath);
            if (classe == null) continue;

            string racaTexto = "?";
            string faccaoTexto = "";
            Color faccaoCor = new Color(0.7f, 0.75f, 0.85f);

            if (ResourceLoader.Exists(racaPath))
            {
                var raca = ResourceLoader.Load<RacaResource>(racaPath);
                if (raca != null)
                {
                    racaTexto = raca.NomeRaca;
                    if (raca.Faccao != null)
                    {
                        faccaoTexto = raca.Faccao.NomeFaccao;
                        faccaoCor = raca.Faccao.CorTema;
                    }
                }
            }

            var card = CriarCard(ch.SlotIndex, ch.Name, classe.NomeClasse, ch.Level, racaTexto, faccaoTexto, faccaoCor, classePath, racaPath);
            _cards.Add(card);
        }
    }

    private void PopulatarDeSlotsLocais()
    {
        var save = GetNode<SaveManager>("/root/SaveManager");

        for (int i = 0; i < save.SlotsDisponiveis; i++)
        {
            if (!save.SlotOcupado(i)) continue;

            var cfg = new ConfigFile();
            if (cfg.Load(save.SlotPath(i)) != Error.Ok) continue;

            string pathClasse = cfg.GetValue("personagem", "classe", "").AsString();
            string pathRaca = cfg.GetValue("personagem", "raca", "").AsString();
            string nome = cfg.GetValue("personagem", "nome", "?").AsString();
            int nivel = cfg.GetValue("progressao", "nivel", 1).AsInt32();

            var classe = ResourceLoader.Load<ClasseCustomResource>(pathClasse);
            if (classe == null) continue;

            var racaTexto = "?";
            var faccaoTexto = "";
            Color faccaoCor = new Color(0.7f, 0.75f, 0.85f);

            if (ResourceLoader.Exists(pathRaca))
            {
                var raca = ResourceLoader.Load<RacaResource>(pathRaca);
                if (raca != null)
                {
                    racaTexto = raca.NomeRaca;
                    if (raca.Faccao != null)
                    {
                        faccaoTexto = raca.Faccao.NomeFaccao;
                        faccaoCor = raca.Faccao.CorTema;
                    }
                }
            }

            var card = CriarCard(i, nome, classe.NomeClasse, nivel, racaTexto, faccaoTexto, faccaoCor, pathClasse, pathRaca);
            _cards.Add(card);
        }
    }

    private CharacterCard CriarCard(int slotIndex, string nome, string classe, int nivel, string raca, string faccao, Color corFaccao, string classePath, string racaPath)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(0, 60);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.11f, 0.15f, 0.94f);
        style.BorderWidthLeft = 2;
        style.BorderWidthTop = 2;
        style.BorderWidthRight = 2;
        style.BorderWidthBottom = 2;
        style.BorderColor = new Color(0.3f, 0.35f, 0.5f, 1f);
        style.CornerRadiusTopLeft = 8;
        style.CornerRadiusTopRight = 8;
        style.CornerRadiusBottomRight = 8;
        style.CornerRadiusBottomLeft = 8;
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.CustomMinimumSize = new Vector2(0, 60);
        hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var nomeLabel = new Label();
        nomeLabel.Text = nome;
        nomeLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nomeLabel.AddThemeFontSizeOverride("font_size", 18);
        nomeLabel.VerticalAlignment = VerticalAlignment.Center;
        nomeLabel.CustomMinimumSize = new Vector2(160, 0);

        var classeLabel = new Label();
        classeLabel.Text = classe;
        classeLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        classeLabel.AddThemeFontSizeOverride("font_size", 15);
        classeLabel.VerticalAlignment = VerticalAlignment.Center;
        classeLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.9f));

        var nivelLabel = new Label();
        nivelLabel.Text = $"Nv {nivel}";
        nivelLabel.AddThemeFontSizeOverride("font_size", 14);
        nivelLabel.VerticalAlignment = VerticalAlignment.Center;
        nivelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.55f));
        nivelLabel.CustomMinimumSize = new Vector2(50, 0);
        nivelLabel.HorizontalAlignment = HorizontalAlignment.Center;

        var faccaoLabel = new Label();
        faccaoLabel.Text = faccao;
        faccaoLabel.AddThemeFontSizeOverride("font_size", 13);
        faccaoLabel.VerticalAlignment = VerticalAlignment.Center;
        faccaoLabel.AddThemeColorOverride("font_color", corFaccao);
        faccaoLabel.CustomMinimumSize = new Vector2(90, 0);

        hbox.AddChild(nomeLabel);
        hbox.AddChild(classeLabel);
        hbox.AddChild(nivelLabel);
        hbox.AddChild(faccaoLabel);
        panel.AddChild(hbox);
        _listaContainer.AddChild(panel);

        var card = new CharacterCard
        {
            SlotIndex = slotIndex,
            Nome = nome,
            Nivel = nivel,
            Panel = panel,
            NomeLabel = nomeLabel,
            ClasseLabel = classeLabel,
            NivelLabel = nivelLabel,
            FaccaoLabel = faccaoLabel,
            ClassePath = classePath,
            RacaPath = racaPath,
        };

        panel.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                SelecionarCard(card);
        };
        panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        return card;
    }

    private void SelecionarCard(CharacterCard card)
    {
        foreach (var c in _cards)
        {
            var cor = (c == card)
                ? new Color(0.6f, 0.7f, 1f, 1f)
                : new Color(0.3f, 0.35f, 0.5f, 1f);
            var estilo = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.11f, 0.15f, 0.94f),
                BorderWidthLeft = 2, BorderWidthTop = 2,
                BorderWidthRight = 2, BorderWidthBottom = 2,
                BorderColor = cor,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
                CornerRadiusBottomRight = 8, CornerRadiusBottomLeft = 8,
            };
            c.Panel.AddThemeStyleboxOverride("panel", estilo);
        }

        _slotSelecionado = card.SlotIndex;
        _btnJogar.Disabled = false;
        _btnExcluir.Disabled = false;

        MostrarPreview(card);
    }

    private void MostrarPreview(CharacterCard card)
    {
        _personagemPreview.Visible = true;
        _previewInfo.Visible = true;

        bool online = _net != null && _net.Characters.Count > 0;

        string nome;
        int nivel;
        if (online)
        {
            nome = card.Nome;
            nivel = card.Nivel;
        }
        else
        {
            var cfg = new ConfigFile();
            var save = GetNode<SaveManager>("/root/SaveManager");
            if (cfg.Load(save.SlotPath(card.SlotIndex)) != Error.Ok) return;

            nome = cfg.GetValue("personagem", "nome", "?").AsString();
            nivel = cfg.GetValue("progressao", "nivel", 1).AsInt32();
        }

        var classe = ResourceLoader.Load<ClasseCustomResource>(card.ClassePath);
        var raca = ResourceLoader.Load<RacaResource>(card.RacaPath);

        _previewNome.Text = nome;
        _previewNivel.Text = $"Nivel {nivel}";
        _previewClasse.Text = classe?.NomeClasse ?? "?";
        _previewRaca.Text = raca?.NomeRaca ?? "?";

        if (raca?.Faccao != null)
        {
            _previewFaccao.Text = raca.Faccao.NomeFaccao;
            _previewFaccao.AddThemeColorOverride("font_color", raca.Faccao.CorTema);
            if (raca.Faccao.ObterEmblema() != null)
                _previewEmblema.Texture = raca.Faccao.ObterEmblema();
        }

        if (raca != null)
        {
            var prefixo = classe?.ObterPrefixoAnimacao() ?? "guerreiro";
            var frames = raca.CriarSpriteFrames(prefixo);
            if (frames != null)
            {
                _previewSprite.SpriteFrames = frames;
                _previewSprite.Centered = true;
                _previewSprite.Scale = new Vector2(2.2f, 2.2f);
                _previewSprite.ZIndex = 100;

                var anim = ResolverAnimacaoIdle(frames);
                if (!string.IsNullOrEmpty(anim))
                    _previewSprite.Play(anim);

                _previewSprite.Visible = true;
                CallDeferred(nameof(CentralizarSprite));
                return;
            }
        }

        _previewSprite.Visible = false;
    }

    private void CentralizarSprite()
    {
        if (!GodotObject.IsInstanceValid(_areaSprite) || !GodotObject.IsInstanceValid(_previewSprite)) return;
        var centro = _areaSprite.Size * 0.5f;
        _previewSprite.Position = centro;
    }

    private static string ResolverAnimacaoIdle(SpriteFrames frames)
    {
        var idleNames = new[] { "idle_down", "idle_right", "idle_left", "idle_up", "idle" };
        foreach (var name in idleNames)
        {
            if (frames.HasAnimation(name))
                return name;
        }
        foreach (var name in frames.GetAnimationNames())
        {
            if (name.Contains("idle"))
                return name;
        }
        return string.Empty;
    }

    private void EsconderPreview()
    {
        _personagemPreview.Visible = false;
        _previewInfo.Visible = false;
        _previewSprite.Visible = false;
    }

    private void OnJogar()
    {
        if (_slotSelecionado == null) return;

        MostrarTelaCarregamento();

        var escolhido = GetNode<PersonagemEscolhido>("/root/PersonagemEscolhido");

        if (_net != null && _net.IsConnected)
        {
            var card = _cards.Find(c => c.SlotIndex == _slotSelecionado.Value);
            if (card == null) { GD.PrintErr("[SELECAO] Card nao encontrado para slot " + _slotSelecionado); FecharTelaCarregamento(); return; }

            GD.Print($"[SELECAO] OnJogar: slot={card.SlotIndex} nome={card.Nome} classePath={card.ClassePath} racaPath={card.RacaPath}");

            var classe = ResourceLoader.Load<ClasseCustomResource>(card.ClassePath);
            var raca = ResourceLoader.Load<RacaResource>(card.RacaPath);
            if (classe == null) { GD.PrintErr($"[SELECAO] Falha ao carregar classe: {card.ClassePath}"); FecharTelaCarregamento(); return; }
            if (raca == null) { GD.PrintErr($"[SELECAO] Falha ao carregar raca: {card.RacaPath}"); FecharTelaCarregamento(); return; }

            escolhido.ClasseBase = classe;
            escolhido.Raca = raca;
            escolhido.NomePersonagem = card.Nome;
            escolhido.SlotAtivo = card.SlotIndex;
            escolhido.ResetarProgressaoSalva();

            _net.SendSelectCharacter(card.SlotIndex);
            _net.SendEnterWorld(
                card.Nome,
                classe.NomeClasse,
                raca.NomeRaca,
                230f, 300f
            );
        }
        else
        {
            if (!escolhido.CarregarSlot(_slotSelecionado.Value)) return;
            GetTree().ChangeSceneToFile(SceneConstants.MAIN);
        }
    }

    private void MostrarTelaCarregamento()
    {
        var existing = GetTree().Root.GetNodeOrNull("LoadingScreen");
        if (existing != null) return;

        var loading = ResourceLoader.Load<PackedScene>("res://ui/LoadingScreen.tscn").Instantiate<LoadingScreen>();
        loading.Name = "LoadingScreen";
        GetTree().Root.AddChild(loading);
    }

    private void FecharTelaCarregamento()
    {
        var loading = GetTree()?.Root.GetNodeOrNull("LoadingScreen");
        if (loading != null)
            loading.QueueFree();
    }

    private void OnCriarNovo()
    {
        GetTree().ChangeSceneToFile(SceneConstants.CRIACAO_PERSONAGEM);
    }

    private void OnAbrirLoja()
    {
        var lojaCena = ResourceLoader.Load<PackedScene>(SceneConstants.LOJA_CASH_UI);
        if (lojaCena == null) return;

        var loja = lojaCena.Instantiate<LojaCashUI>();
        AddChild(loja);
        loja.Abrir();
    }

    private void OnExcluir()
    {
        if (_slotSelecionado == null) return;
        _confirmacaoExcluir.PopupCentered();
    }

    private void ConfirmarExclusao()
    {
        if (_slotSelecionado == null) return;

        if (_net != null && _net.IsConnected)
        {
            _net.SendDeleteCharacter(_slotSelecionado.Value);
            _net.OnCharacterList += OnCharacterListRefreshed;
            _slotSelecionado = null;
        }
        else
        {
            var save = GetNode<SaveManager>("/root/SaveManager");
            save.DeletarSlot(_slotSelecionado.Value);
            _slotSelecionado = null;
            PopulatarLista();
        }
    }

    private void OnCharacterListRefreshed(Godot.Collections.Array<Godot.Collections.Dictionary> characters)
    {
        if (GodotObject.IsInstanceValid(_net))
            _net.OnCharacterList -= OnCharacterListRefreshed;
        PopulatarLista();
    }
}
