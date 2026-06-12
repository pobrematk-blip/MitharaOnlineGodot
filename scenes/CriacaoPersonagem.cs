using Godot;
using System.Collections.Generic;

public partial class CriacaoPersonagem : Control
{
    private enum Etapa { Faccao, Raca, Classe }

    private const float LarguraOverlay = 340f;

    private Control _etapaFaccao;
    private Control _etapaRaca;
    private Control _etapaClasse;
    private Button _btnVoltarRaca;
    private Button _btnVoltarClasse;
    private Button _btnAvancarRaca;
    private Button _btnEntrarJogo;
    private ItemList _listaRacas;
    private ItemList _listaClasses;
    private Label _tituloRaca;
    private Label _tituloClasse;
    private Label _descricaoRaca;
    private Label _descricaoClasse;
    private Label _dicaClique;
    private Label _descricaoPersonagem;
    private LineEdit _nomeEdit;
    private Control _overlayConfirmacao;
    private Control _areaPreviewRaca;
    private Control _areaPreviewClasse;
    private AnimatedSprite2D _personagemPreview;
    private AnimatedSprite2D _previewRaca;
    private AnimatedSprite2D _previsoCabelo;
    private AnimatedSprite2D _previsoBarba;
    private TextureRect _emblemaSolari;
    private TextureRect _emblemaNoctori;
    private Label _descricaoSolari;
    private Label _descricaoNoctori;
    private Control _aparenciaPanel;
    private OptionButton _opcaoCabelo;
    private OptionButton _opcaoBarba;
    private ColorPickerButton _corCabelo;
    private ColorPickerButton _corBarba;

    private Etapa _etapaAtual = Etapa.Faccao;
    private FaccaoResource _faccaoSelecionada;
    private RacaResource _racaSelecionada;
    private ClasseCustomResource _classeSelecionada;
    private readonly List<RacaResource> _racasVisiveis = new();
    private Tween _tweenCaminhada;
    private bool _aguardandoFimAtaque;

    private void OnCancelar()
    {
        GetTree().ChangeSceneToFile(SceneConstants.SELECAO_PERSONAGEM);
    }

    public override void _Ready()
    {
        GetNode<Button>("%BtnCancelar").Pressed += OnCancelar;

        _etapaFaccao = GetNode<Control>("%EtapaFaccao");
        _etapaRaca = GetNode<Control>("%EtapaRaca");
        _etapaClasse = GetNode<Control>("%EtapaClasse");
        _btnVoltarRaca = GetNode<Button>("%BtnVoltarRaca");
        _btnVoltarClasse = GetNode<Button>("%BtnVoltarClasse");
        _btnAvancarRaca = GetNode<Button>("%BtnAvancarRaca");
        _btnEntrarJogo = GetNode<Button>("%BtnEntrarJogo");
        _listaRacas = GetNode<ItemList>("%ListaRacas");
        _listaClasses = GetNode<ItemList>("%ListaClasses");
        _tituloRaca = GetNode<Label>("%TituloRaca");
        _tituloClasse = GetNode<Label>("%TituloClasse");
        _descricaoRaca = GetNode<Label>("%DescricaoRaca");
        _descricaoClasse = GetNode<Label>("%DescricaoClasse");
        _dicaClique = GetNode<Label>("%DicaClique");
        _descricaoPersonagem = GetNode<Label>("%DescricaoPersonagem");
        _nomeEdit = GetNode<LineEdit>("%NomeEdit");
        _overlayConfirmacao = GetNode<Control>("%OverlayConfirmacao");
        _areaPreviewRaca = GetNode<Control>("%AreaPreviewRaca");
        _areaPreviewClasse = GetNode<Control>("%AreaPreviewClasse");
        _personagemPreview = GetNode<AnimatedSprite2D>("%PersonagemPreview");
        _previewRaca = GetNode<AnimatedSprite2D>("%PreviewRaca");
        _previsoCabelo = GetNode<AnimatedSprite2D>("%PrevisoCabelo");
        _previsoBarba = GetNode<AnimatedSprite2D>("%PrevisoBarba");
        _emblemaSolari = GetNode<TextureRect>("%EmblemaSolari");
        _emblemaNoctori = GetNode<TextureRect>("%EmblemaNoctori");

        _aparenciaPanel = GetNode<Control>("%AparenciaPanel");
        _opcaoCabelo = GetNode<OptionButton>("%OpcaoCabelo");
        _opcaoBarba = GetNode<OptionButton>("%OpcaoBarba");
        _corCabelo = GetNode<ColorPickerButton>("%CorCabelo");
        _corBarba = GetNode<ColorPickerButton>("%CorBarba");
        _descricaoSolari = GetNode<Label>("%DescricaoSolari");
        _descricaoNoctori = GetNode<Label>("%DescricaoNoctori");

        GetNode<Button>("%BtnEscolherSolari").Pressed += () => EscolherFaccao(FaccaoUtil.IdSolari);
        GetNode<Button>("%BtnEscolherNoctori").Pressed += () => EscolherFaccao(FaccaoUtil.IdNoctori);
        _btnVoltarRaca.Pressed += () => IrParaEtapa(Etapa.Faccao);
        _btnVoltarClasse.Pressed += () => IrParaEtapa(Etapa.Raca);
        _btnAvancarRaca.Pressed += OnAvancarRaca;
        _listaRacas.ItemSelected += OnRacaSelecionada;
        _listaClasses.ItemSelected += OnClasseSelecionada;
        _opcaoCabelo.ItemSelected += OnCabeloAlterado;
        _opcaoBarba.ItemSelected += OnBarbaAlterado;
        _corCabelo.ColorChanged += _ => OnCabeloAlterado(0);
        _corBarba.ColorChanged += _ => OnBarbaAlterado(0);
        _btnEntrarJogo.Pressed += OnEntrarJogo;
        _nomeEdit.TextChanged += _ => AtualizarOverlayConfirmacao();
        _personagemPreview.AnimationFinished += OnPersonagemPreviewAnimacaoFinalizada;

        CallDeferred(nameof(ConfigurarAposReady));
    }

    private void ConfigurarAposReady()
    {
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        registry.RecarregarTudo();
        ConfigurarFaccoes(registry);
        PreencherClasses(registry);
        IrParaEtapa(Etapa.Faccao);
    }

    private void ConfigurarFaccoes(ClasseRegistry registry)
    {
        var solari = registry.ObterFaccaoPorId(FaccaoUtil.IdSolari);
        var noctori = registry.ObterFaccaoPorId(FaccaoUtil.IdNoctori);
        AplicarDadosFaccao(solari, _emblemaSolari, _descricaoSolari, GetNode<Label>("%NomeSolari"));
        AplicarDadosFaccao(noctori, _emblemaNoctori, _descricaoNoctori, GetNode<Label>("%NomeNoctori"));
    }

    private static void AplicarDadosFaccao(FaccaoResource faccao, TextureRect emblema, Label descricao, Label nome)
    {
        if (faccao == null) return;

        nome.Text = faccao.NomeFaccao.ToUpperInvariant();
        nome.AddThemeColorOverride("font_color", faccao.CorTema);
        descricao.Text = faccao.Descricao;
        descricao.AddThemeColorOverride("font_color", faccao.CorTema.Lightened(0.25f));

        var tex = faccao.ObterEmblema();
        if (tex != null)
            emblema.Texture = tex;
    }

    private void PreencherClasses(ClasseRegistry registry)
    {
        _listaClasses.Clear();
        GD.Print($"[CRIACAO] Classes carregadas: {registry.Classes.Count}");
        for (int i = 0; i < registry.Classes.Count; i++)
        {
            var c = registry.Classes[i];
            var nome = string.IsNullOrWhiteSpace(c.NomeClasse) ? $"Classe #{i + 1} (sem nome)" : c.NomeClasse;
            GD.Print($"[CRIACAO]   Classe[{i}] = {nome}");
            _listaClasses.AddItem(nome);
        }
    }

    private void EscolherFaccao(string idFaccao)
    {
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        _faccaoSelecionada = registry.ObterFaccaoPorId(idFaccao);
        if (_faccaoSelecionada == null) return;

        _racaSelecionada = null;
        _classeSelecionada = null;
        PreencherRacas(registry);
        IrParaEtapa(Etapa.Raca);
    }

    private void PreencherRacas(ClasseRegistry registry)
    {
        _racasVisiveis.Clear();
        _listaRacas.Clear();
        _racasVisiveis.AddRange(registry.ObterRacasDaFaccao(_faccaoSelecionada));

        foreach (var raca in _racasVisiveis)
            _listaRacas.AddItem(raca.NomeRaca);

        _tituloRaca.Text = $"Escolha sua raça — {_faccaoSelecionada.NomeFaccao}";
        _tituloRaca.AddThemeColorOverride("font_color", _faccaoSelecionada.CorTema);
        _descricaoRaca.Text = "Selecione uma raça da sua facção.";
        _btnAvancarRaca.Disabled = true;
        ResetarPreviewRaca();
    }

    private void OnRacaSelecionada(long index)
    {
        if (index < 0 || index >= _racasVisiveis.Count) return;

        _racaSelecionada = _racasVisiveis[(int)index];
        _descricaoRaca.Text = string.IsNullOrWhiteSpace(_racaSelecionada.Descricao)
            ? _racaSelecionada.NomeRaca
            : _racaSelecionada.Descricao;
        _btnAvancarRaca.Disabled = false;
        AtualizarPreviewRaca();
    }

    private void AtualizarPreviewRaca()
    {
        if (_previewRaca == null || _racaSelecionada == null)
        {
            ResetarPreviewRaca();
            return;
        }

        var frames = _racaSelecionada.CriarSpriteFrames("guerreiro");
        if (frames == null || !frames.HasAnimation("idle_down"))
        {
            _previewRaca.Visible = false;
            return;
        }

        _previewRaca.SpriteFrames = frames;
        _previewRaca.Play("idle_down");
        _previewRaca.Visible = true;
        SpriteVisualUtil.AplicarVisualSuave(_previewRaca, 3f);
        CallDeferred(nameof(CentralizarPreviewRaca));
    }

    private void CentralizarPreviewRaca()
    {
        if (_previewRaca == null || !_previewRaca.Visible || _areaPreviewRaca == null) return;
        _previewRaca.GlobalPosition = _areaPreviewRaca.GetGlobalRect().GetCenter().Round();
    }

    private void ResetarPreviewRaca()
    {
        if (_previewRaca == null) return;
        _previewRaca.Visible = false;
        _previewRaca.SpriteFrames = null;
    }

    private void OnAvancarRaca()
    {
        if (_racaSelecionada == null) return;
        _classeSelecionada = null;
        _tituloClasse.Text = $"Escolha sua classe — {_racaSelecionada.NomeRaca}";
        if (_faccaoSelecionada != null)
            _tituloClasse.AddThemeColorOverride("font_color", _faccaoSelecionada.CorTema);

        _descricaoClasse.Text = "Selecione uma classe. Seu personagem entrará de frente executando um ataque.";
        ResetarPreviewRaca();
        ResetarPreviewClasse();
        IrParaEtapa(Etapa.Classe);
    }

    private void OnClasseSelecionada(long index)
    {
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        if (index < 0 || index >= registry.Classes.Count) return;

        _classeSelecionada = registry.Classes[(int)index];
        _descricaoClasse.Text = string.IsNullOrWhiteSpace(_classeSelecionada.Descricao)
            ? _classeSelecionada.NomeClasse
            : _classeSelecionada.Descricao;

        IniciarEntradaPersonagem();
    }

    private void ResetarPreviewClasse()
    {
        _tweenCaminhada?.Kill();
        _aguardandoFimAtaque = false;
        _overlayConfirmacao.Visible = false;
        _dicaClique.Visible = false;
        _nomeEdit.Text = "";
        _nomeEdit.Visible = true;
        _btnEntrarJogo.Visible = false;
        _personagemPreview.SpriteFrames = null;
        _personagemPreview.Visible = false;
        _previsoCabelo.Visible = false;
        _previsoBarba.Visible = false;
        _aparenciaPanel.Visible = false;
        _descricaoPersonagem.Text = "";
        _descricaoClasse.Visible = true;
    }

    private (Vector2 inicial, Vector2 finalPos) CalcularPosicoesEntradaClasse()
    {
        if (_areaPreviewClasse == null)
        {
            var fallback = new Vector2(790, 340);
            return (fallback + new Vector2(0, -200), fallback);
        }

        var rect = _areaPreviewClasse.GetGlobalRect();
        float centroX = rect.GetCenter().X;
        float topoY = rect.Position.Y + rect.Size.Y * 0.12f;
        float finalY = rect.GetCenter().Y + rect.Size.Y * 0.08f;

        var posInicial = ConverterGlobalParaLocal(new Vector2(centroX, topoY));
        var posFinal = ConverterGlobalParaLocal(new Vector2(centroX, finalY));
        return (posInicial, posFinal);
    }

    private Vector2 ConverterGlobalParaLocal(Vector2 globalPos)
    {
        return GetGlobalTransformWithCanvas().AffineInverse() * globalPos;
    }

    private void IniciarEntradaPersonagem()
    {
        if (_classeSelecionada == null || _racaSelecionada == null) return;

        var preview = MontarPreview();
        var frames = preview.ObterSpriteFramesCompletos();
        if (frames == null || !frames.HasAnimation("walk_down"))
        {
            GD.PrintErr("[CRIACAO] Sprites de caminhada (walk_down) não encontrados para o preview.");
            return;
        }

        _tweenCaminhada?.Kill();
        _aguardandoFimAtaque = false;
        _overlayConfirmacao.Visible = false;
        _btnEntrarJogo.Visible = false;
        _nomeEdit.Text = "";
        _nomeEdit.Visible = true;
        _dicaClique.Visible = false;

        var (posInicial, posFinal) = CalcularPosicoesEntradaClasse();

        _personagemPreview.SpriteFrames = frames;
        _personagemPreview.Visible = true;
        SpriteVisualUtil.AplicarVisualSuave(_personagemPreview, 3f);
        _personagemPreview.Position = posInicial;
        _personagemPreview.SpeedScale = 1.4f;
        _personagemPreview.Play("walk_down");

        _tweenCaminhada = CreateTween();
        _tweenCaminhada.SetEase(Tween.EaseType.InOut);
        _tweenCaminhada.SetTrans(Tween.TransitionType.Sine);
        _tweenCaminhada.TweenProperty(_personagemPreview, "position", posFinal, 1.6);
        _tweenCaminhada.TweenCallback(Callable.From(() => IniciarAnimacaoAtaqueEntrada(preview)));
    }

    private void IniciarAnimacaoAtaqueEntrada(ClasseCustomResource preview)
    {
        string prefixo = preview.ObterPrefixoAtaque();
        string animAtaque = ResolverNomeAnimacaoAtaque(_personagemPreview.SpriteFrames, $"{prefixo}_attack_down");

        if (_personagemPreview.SpriteFrames == null || !_personagemPreview.SpriteFrames.HasAnimation(animAtaque))
        {
            GD.PrintErr($"[CRIACAO] Animação de entrada '{animAtaque}' não encontrada.");
            FinalizarEntradaPersonagem();
            return;
        }

        _aguardandoFimAtaque = true;
        _personagemPreview.SpeedScale = preview.AttackAnimSpeedScale > 0f ? preview.AttackAnimSpeedScale : 3f;
        _personagemPreview.Play(animAtaque);
    }

    private void OnPersonagemPreviewAnimacaoFinalizada()
    {
        if (!_aguardandoFimAtaque) return;
        _aguardandoFimAtaque = false;
        FinalizarEntradaPersonagem();
    }

    private static string ResolverNomeAnimacaoAtaque(SpriteFrames frames, string nomeBase)
    {
        if (frames == null) return nomeBase;
        if (frames.HasAnimation(nomeBase)) return nomeBase;
        string comSufixo = nomeBase + "_";
        if (frames.HasAnimation(comSufixo)) return comSufixo;
        return nomeBase;
    }

    private void FinalizarEntradaPersonagem()
    {
        _personagemPreview.SpeedScale = 1f;

        if (_personagemPreview.SpriteFrames?.HasAnimation("idle_down") == true)
            _personagemPreview.Play("idle_down");

        _descricaoClasse.Visible = false;
        _aparenciaPanel.Visible = true;
        _overlayConfirmacao.Visible = true;
        AtualizarOverlayConfirmacao();
        CallDeferred(nameof(PosicionarOverlayConfirmacao));
        CallDeferred(nameof(FocarCampoNome));
        AtualizarPrevisaoAparencia();
    }

    private void OnCabeloAlterado(long index)
    {
        AtualizarPrevisaoAparencia();
    }

    private void OnBarbaAlterado(long index)
    {
        AtualizarPrevisaoAparencia();
    }

    private void AtualizarPrevisaoAparencia()
    {
        if (_personagemPreview.SpriteFrames == null) return;

        AtualizarOverlayPrevisao(_previsoCabelo, _opcaoCabelo.Selected, _corCabelo.Color, "res://Itens/Cabelos/Cabelo Dread Branco.png");
        AtualizarOverlayPrevisao(_previsoBarba, _opcaoBarba.Selected, _corBarba.Color, "res://Itens/Cabelos/Barba Normal Branca.png");
    }

    private void AtualizarOverlayPrevisao(AnimatedSprite2D overlay, int selected, Color cor, string path)
    {
        if (selected <= 0 || string.IsNullOrEmpty(path))
        {
            overlay.Visible = false;
            return;
        }

        var tex = GD.Load<Texture2D>(path);
        if (tex == null)
        {
            overlay.Visible = false;
            return;
        }

        var frames = LpcSpriteFramesBuilder.Construir(tex, "");
        if (frames == null || !frames.HasAnimation("idle_down"))
        {
            overlay.Visible = false;
            return;
        }

        overlay.SpriteFrames = frames;
        overlay.Modulate = cor;
        overlay.Visible = true;

        string anim = _personagemPreview.Animation.ToString();
        if (frames.HasAnimation(anim))
            overlay.Play(anim);
        else
            overlay.Play("idle_down");
        overlay.Frame = _personagemPreview.Frame;
        overlay.SpeedScale = _personagemPreview.SpeedScale;
    }

    private void FocarCampoNome()
    {
        _nomeEdit.GrabFocus();
    }

    private void AtualizarOverlayConfirmacao()
    {
        if (_faccaoSelecionada == null || _racaSelecionada == null || _classeSelecionada == null)
        {
            _descricaoPersonagem.Text = "";
            _btnEntrarJogo.Visible = false;
            return;
        }

        string descClasse = string.IsNullOrWhiteSpace(_classeSelecionada.Descricao)
            ? _classeSelecionada.NomeClasse
            : _classeSelecionada.Descricao;

        bool temNome = !string.IsNullOrWhiteSpace(_nomeEdit.Text);
        _nomeEdit.Visible = true;
        _btnEntrarJogo.Visible = temNome;

        string nome = _nomeEdit.Text.Trim();
        _descricaoPersonagem.Text = temNome
            ? $"{nome}\n{_faccaoSelecionada.NomeFaccao} · {_racaSelecionada.NomeRaca} · {_classeSelecionada.NomeClasse}\n{descClasse}"
            : $"{_faccaoSelecionada.NomeFaccao} · {_racaSelecionada.NomeRaca} · {_classeSelecionada.NomeClasse}\n{descClasse}";

        CallDeferred(nameof(PosicionarOverlayConfirmacao));
    }

    private float ObterMetadeAlturaSprite()
    {
        const float frameSize = LpcSpriteFramesBuilder.FrameSize;
        return frameSize * _personagemPreview.Scale.Y * 0.5f;
    }

    private void PosicionarOverlayConfirmacao()
    {
        if (_overlayConfirmacao == null || !_overlayConfirmacao.Visible || !_personagemPreview.Visible)
            return;

        var centro = _personagemPreview.GlobalPosition;
        float metadeAltura = ObterMetadeAlturaSprite();
        float centerX = centro.X;

        const float alturaDescricao = 88f;
        const float alturaNome = 36f;
        const float alturaBotao = 40f;
        const float espaco = 10f;

        float topoDescricao = centro.Y - metadeAltura - alturaDescricao - espaco;
        float topoNome = centro.Y + metadeAltura + espaco;

        _descricaoPersonagem.CustomMinimumSize = new Vector2(LarguraOverlay, alturaDescricao);
        _descricaoPersonagem.Size = new Vector2(LarguraOverlay, alturaDescricao);
        _descricaoPersonagem.GlobalPosition = new Vector2(centerX - LarguraOverlay * 0.5f, topoDescricao);

        _nomeEdit.CustomMinimumSize = new Vector2(LarguraOverlay, alturaNome);
        _nomeEdit.Size = new Vector2(LarguraOverlay, alturaNome);
        _nomeEdit.GlobalPosition = new Vector2(centerX - LarguraOverlay * 0.5f, topoNome);

        float topoBotao = topoNome + alturaNome + 6f;
        _btnEntrarJogo.CustomMinimumSize = new Vector2(LarguraOverlay, alturaBotao);
        _btnEntrarJogo.Size = new Vector2(LarguraOverlay, alturaBotao);
        _btnEntrarJogo.GlobalPosition = new Vector2(centerX - LarguraOverlay * 0.5f, topoBotao);
    }

    private ClasseCustomResource MontarPreview()
    {
        var copia = (ClasseCustomResource)_classeSelecionada.Duplicate(false);
        copia.Raca = _racaSelecionada;
        return copia;
    }

    private void OnEntrarJogo()
    {
        if (_faccaoSelecionada == null || _racaSelecionada == null || _classeSelecionada == null) return;
        if (string.IsNullOrWhiteSpace(_nomeEdit.Text)) return;

        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        if (!registry.RacaPermitidaNaFaccao(_racaSelecionada, _faccaoSelecionada)) return;

        var escolhido = GetNode<PersonagemEscolhido>("/root/PersonagemEscolhido");
        escolhido.Definir(_classeSelecionada, _racaSelecionada, _nomeEdit.Text);

        escolhido.CabeloPath = _opcaoCabelo.Selected > 0 ? "res://Itens/Cabelos/Cabelo Dread Branco.png" : "";
        escolhido.BarbaPath = _opcaoBarba.Selected > 0 ? "res://Itens/Cabelos/Barba Normal Branca.png" : "";
        escolhido.CabeloCor = _corCabelo.Color;
        escolhido.BarbaCor = _corBarba.Color;
        escolhido.Salvar();

        if (!escolhido.TemPersonagem)
        {
            GD.PrintErr("[CRIACAO] Personagem não foi salvo corretamente. Tente novamente.");
            return;
        }

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected && net.LoggedIn)
        {
            string nomeClasse = _classeSelecionada.NomeClasse;
            string nomeRaca = _racaSelecionada.NomeRaca;
            net.SendCreateCharacter(escolhido.NomePersonagem, nomeClasse, nomeRaca);
            net.SendEnterWorld(escolhido.NomePersonagem, nomeClasse, nomeRaca, 230f, 300f);
        }
        else
        {
            GetTree().ChangeSceneToFile(SceneConstants.MAIN);
        }
    }

    private void IrParaEtapa(Etapa etapa)
    {
        _etapaAtual = etapa;
        _etapaFaccao.Visible = etapa == Etapa.Faccao;
        _etapaRaca.Visible = etapa == Etapa.Raca;
        _etapaClasse.Visible = etapa == Etapa.Classe;

        if (etapa != Etapa.Classe)
            ResetarPreviewClasse();

        if (etapa != Etapa.Raca)
            ResetarPreviewRaca();
        else if (_racaSelecionada != null)
            CallDeferred(nameof(CentralizarPreviewRaca));
    }

    public override void _Process(double delta)
    {
        if (_personagemPreview == null || !_personagemPreview.Visible) return;
        SincronizarOverlayPrevisao(_previsoCabelo);
        SincronizarOverlayPrevisao(_previsoBarba);
    }

    private void SincronizarOverlayPrevisao(AnimatedSprite2D overlay)
    {
        if (overlay == null || !overlay.Visible) return;
        if (overlay.SpriteFrames == null || _personagemPreview.SpriteFrames == null) return;
        string anim = _personagemPreview.Animation.ToString();
        if (!overlay.SpriteFrames.HasAnimation(anim))
        {
            overlay.Visible = false;
            return;
        }
        if (overlay.Animation.ToString() != anim)
            overlay.Play(anim);
        overlay.Frame = _personagemPreview.Frame;
        overlay.SpeedScale = _personagemPreview.SpeedScale;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            if (_etapaAtual == Etapa.Raca && _previewRaca?.Visible == true)
                CentralizarPreviewRaca();
            if (_etapaAtual == Etapa.Classe && _overlayConfirmacao?.Visible == true)
                PosicionarOverlayConfirmacao();
        }
    }
}
