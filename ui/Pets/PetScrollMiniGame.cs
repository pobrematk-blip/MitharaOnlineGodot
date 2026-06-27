using Godot;
using System.Collections.Generic;

public partial class PetScrollMiniGame : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Label _titulo;
    private Label _instrucao;
    private Label _chancesLabel;
    private Label _statusLabel;
    private HBoxContainer _progressContainer;
    private Button _capturarBtn;
    private Button _fecharBtn;

    private Control _barBg;
    private Panel _barGreen;
    private Panel _sweetSpot;
    private Label _marker;

    private readonly List<Label> _progressIndicators = new();

    private int _acertos;
    private int _tentativas;
    private const int MaxAcertos = 5;
    private int _maxTentativas = 7;
    private float _markerPos;
    private float _markerSpeed;
    private float _sweetSpotPos;
    private float _sweetSpotWidth;
    private float _barWidth;
    private bool _rodadaAtiva;
    private bool _podeClicar;

    private int _petIdReward;
    private string _petNameReward;

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    [Signal]
    public delegate void MiniGameConcluidoEventHandler(int petId, string petNome, bool sucesso);

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _panel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _panel.CustomMinimumSize = new Vector2(360, 230);
        _panel.Size = new Vector2(360, 230);
        _panel.AddThemeStyleboxOverride("panel", CriarStyle(
            new Color(0.006f, 0.008f, 0.014f, 0.90f),
            new Color(0.18f, 0.62f, 0.92f, 0.95f),
            8,
            1));

        _titulo = GetNode<Label>("%TituloLabel");
        _instrucao = GetNode<Label>("%InstrucaoLabel");
        _chancesLabel = new Label();
        _chancesLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _chancesLabel.AddThemeFontSizeOverride("font_size", 13);
        _chancesLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.05f, 1f));
        _statusLabel = GetNode<Label>("%StatusLabel");
        _progressContainer = GetNode<HBoxContainer>("%ProgressContainer");
        _capturarBtn = GetNode<Button>("%CapturarBtn");
        _fecharBtn = GetNode<Button>("%FecharBtn");

        _titulo.AddThemeFontSizeOverride("font_size", 18);
        _titulo.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.08f, 1f));
        _instrucao.AddThemeFontSizeOverride("font_size", 13);
        _instrucao.AddThemeColorOverride("font_color", new Color(0.90f, 0.94f, 1f, 1f));
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);

        _capturarBtn.Pressed += TentarCaptura;
        _fecharBtn.Pressed += () => Fechar(false);
        EstilizarBotao(_capturarBtn, new Color(0.10f, 0.40f, 0.62f, 1f), new Color(0.22f, 0.82f, 1f, 1f));
        EstilizarBotao(_fecharBtn, new Color(0.05f, 0.06f, 0.09f, 0.96f), new Color(0.26f, 0.34f, 0.46f, 1f));

        _progressContainer.AddThemeConstantOverride("separation", 8);

        var vbox = _panel.GetNode("VBox");
        if (vbox is VBoxContainer vboxContainer)
            vboxContainer.AddThemeConstantOverride("separation", 8);

        _titleBar = new Panel();
        _titleBar.CustomMinimumSize = new Vector2(0, 30);
        _titleBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleBar.MouseFilter = MouseFilterEnum.Pass;
        _titleBar.AddThemeStyleboxOverride("panel", CriarStyle(
            new Color(0.01f, 0.014f, 0.024f, 0.96f),
            new Color(1.0f, 0.86f, 0.08f, 0.90f),
            7,
            1));

        vbox.RemoveChild(_titulo);
        _titulo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titulo.VerticalAlignment = VerticalAlignment.Center;
        _titleBar.AddChild(_titulo);

        vbox.AddChild(_titleBar);
        vbox.MoveChild(_titleBar, 0);

        _titleBar.GuiInput += OnTitleBarGuiInput;

        for (int i = 0; i < MaxAcertos; i++)
        {
            var lbl = new Label();
            lbl.Text = "\u25CB";
            lbl.AddThemeFontSizeOverride("font_size", 24);
            lbl.AddThemeColorOverride("font_color", new Color(0.34f, 0.45f, 0.58f, 1f));
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            _progressContainer.AddChild(lbl);
            _progressIndicators.Add(lbl);
        }

        _chancesLabel.Text = $"Tentativas: {_maxTentativas}/{_maxTentativas}";
        vbox.AddChild(_chancesLabel);
        vbox.MoveChild(_chancesLabel, _instrucao.GetIndex() + 1);

        var barPanel = new Panel();
        barPanel.AddThemeStyleboxOverride("panel", CriarStyle(
            new Color(0.018f, 0.022f, 0.032f, 1f),
            new Color(0.20f, 0.62f, 0.92f, 1f),
            5,
            1));
        _barBg = barPanel;
        _barBg.CustomMinimumSize = new Vector2(280, 18);
        _barBg.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_barBg);
        vbox.MoveChild(_barBg, _chancesLabel.GetIndex() + 1);

        _barGreen = new Panel();
        _barGreen.MouseFilter = MouseFilterEnum.Ignore;
        _barGreen.AddThemeStyleboxOverride("panel", CriarStyle(
            new Color(0.05f, 0.58f, 0.82f, 0.95f),
            new Color(0.24f, 0.86f, 1.0f, 0.95f),
            5,
            0));
        _barBg.AddChild(_barGreen);

        _sweetSpot = new Panel();
        _sweetSpot.MouseFilter = MouseFilterEnum.Ignore;
        _sweetSpot.AddThemeStyleboxOverride("panel", CriarStyle(
            new Color(1.0f, 0.88f, 0.02f, 0.98f),
            new Color(1.0f, 1.0f, 0.45f, 1f),
            5,
            1));
        _barBg.AddChild(_sweetSpot);

        _marker = new Label();
        _marker.Text = "\u25BC";
        _marker.AddThemeFontSizeOverride("font_size", 18);
        _marker.AddThemeColorOverride("font_color", new Color(1.0f, 0.96f, 0.08f, 1f));
        _marker.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
        _marker.AddThemeConstantOverride("shadow_offset_x", 1);
        _marker.AddThemeConstantOverride("shadow_offset_y", 1);
        _marker.HorizontalAlignment = HorizontalAlignment.Center;
        _marker.VerticalAlignment = VerticalAlignment.Top;
        _marker.MouseFilter = MouseFilterEnum.Ignore;
        _marker.Size = new Vector2(18, 24);
        _barBg.AddChild(_marker);

        _panel.Visible = false;
        ZIndex = 260;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void IniciarMiniGame(int petId, string petNome, int maxTentativas = 7)
    {
        _maxTentativas = maxTentativas;
        _petIdReward = petId;
        _petNameReward = petNome;
        _acertos = 0;
        _tentativas = 0;
        _podeClicar = true;

        _titulo.Text = $"Capture: {petNome}";
        _instrucao.Text = "Clique quando a seta estiver na faixa dourada!";
        _statusLabel.Text = "";
        _chancesLabel.Text = $"Tentativas: {_maxTentativas}/{_maxTentativas}";
        AtualizarProgresso();

        GD.Print($"[PET MINIGAME] Iniciando captura de '{petNome}' ({MaxAcertos} acertos em {_maxTentativas} tentativas)");
        _panel.Visible = true;
        CallDeferred(nameof(PosicionarJanela));
        IniciarRodada();
    }

    private void PosicionarJanela()
    {
        if (_panel == null) return;

        Vector2 tela = GetViewportRect().Size;
        Vector2 panelSize = _panel.Size;
        if (panelSize.X <= 0 || panelSize.Y <= 0)
            panelSize = _panel.CustomMinimumSize;

        _panel.Position = new Vector2(
            Mathf.Clamp(24f, 8f, Mathf.Max(8f, tela.X - panelSize.X - 8f)),
            Mathf.Clamp(tela.Y * 0.5f - panelSize.Y * 0.5f, 64f, Mathf.Max(64f, tela.Y - panelSize.Y - 24f))
        );
        _panel.MoveToFront();
    }

    private void IniciarRodada()
    {
        _rodadaAtiva = true;
        _podeClicar = true;

        _sweetSpotPos = (float)GD.RandRange(0.2, 0.8);
        _sweetSpotWidth = (float)GD.RandRange(0.10, 0.18);

        _markerPos = (float)GD.RandRange(0.1, 0.9);
        _markerSpeed = (float)GD.RandRange(250, 400);
        if (GD.RandRange(0, 1) == 0)
            _markerSpeed = -_markerSpeed;
    }

    public override void _Process(double delta)
    {
        if (!_rodadaAtiva || !_podeClicar) return;

        _barWidth = _barBg.Size.X;
        if (_barWidth <= 0) _barWidth = _barBg.CustomMinimumSize.X;

        _markerPos += _markerSpeed * (float)delta / _barWidth;

        if (_markerPos < 0.02f)
        {
            _markerPos = 0.02f;
            _markerSpeed = Mathf.Abs(_markerSpeed);
        }
        if (_markerPos > 0.98f)
        {
            _markerPos = 0.98f;
            _markerSpeed = -Mathf.Abs(_markerSpeed);
        }

        _barGreen.Position = Vector2.Zero;
        const float barHeight = 18f;
        _barGreen.Size = new Vector2(_barWidth, barHeight);

        _sweetSpot.Position = new Vector2(
            (_sweetSpotPos - _sweetSpotWidth / 2) * _barWidth, 0);
        _sweetSpot.Size = new Vector2(
            _sweetSpotWidth * _barWidth, barHeight);

        _marker.Position = new Vector2(
            _markerPos * _barWidth - _marker.Size.X / 2, -16);

        if (Input.IsActionJustPressed("ui_accept"))
        {
            TentarCaptura();
        }
    }

    private void TentarCaptura()
    {
        if (!_rodadaAtiva || !_podeClicar) return;
        _podeClicar = false;
        _rodadaAtiva = false;

        float halfWidth = _sweetSpotWidth / 2;
        bool acertou = _markerPos >= _sweetSpotPos - halfWidth
                    && _markerPos <= _sweetSpotPos + halfWidth;

        _tentativas++;
        int restantes = _maxTentativas - _tentativas;
        _chancesLabel.Text = $"Tentativas: {restantes}/{_maxTentativas}";

        if (acertou)
        {
            _acertos++;
            _statusLabel.Text = $"Acertou! ({_acertos}/{MaxAcertos})";
            _statusLabel.Modulate = new Color(0, 1, 0);
            AtualizarProgresso();

            if (_acertos >= MaxAcertos)
            {
                _instrucao.Text = "Pet capturado!";
                _statusLabel.Text = "Sucesso!";
                _capturarBtn.Disabled = true;
                _fecharBtn.Disabled = true;
                var t = GetTree().CreateTimer(0.8f);
                t.Timeout += () => Fechar(true);
                return;
            }

            if (restantes <= 0)
            {
                _instrucao.Text = "Sem tentativas restantes!";
                _statusLabel.Text = "Falhou...";
                _capturarBtn.Disabled = true;
                _fecharBtn.Disabled = true;
                var t = GetTree().CreateTimer(0.8f);
                t.Timeout += () => Fechar(false);
                return;
            }

            var t2 = GetTree().CreateTimer(0.4f);
            t2.Timeout += IniciarRodada;
        }
        else
        {
            _statusLabel.Text = $"Errou! ({_acertos}/{MaxAcertos})";
            _statusLabel.Modulate = new Color(1, 0.5f, 0);

            if (_acertos >= MaxAcertos)
            {
                _instrucao.Text = "Pet capturado!";
                _statusLabel.Text = "Sucesso!";
                _capturarBtn.Disabled = true;
                _fecharBtn.Disabled = true;
                var t = GetTree().CreateTimer(0.8f);
                t.Timeout += () => Fechar(true);
                return;
            }

            if (restantes <= 0)
            {
                _instrucao.Text = "Sem tentativas restantes!";
                _statusLabel.Text = "Falhou...";
                var t = GetTree().CreateTimer(0.8f);
                t.Timeout += () => Fechar(false);
                return;
            }

            var t3 = GetTree().CreateTimer(0.4f);
            t3.Timeout += IniciarRodada;
        }
    }

    private void AtualizarProgresso()
    {
        for (int i = 0; i < MaxAcertos; i++)
        {
            if (i < _acertos)
            {
                _progressIndicators[i].Text = "\u25CF";
                _progressIndicators[i].AddThemeColorOverride("font_color", new Color(1.0f, 0.90f, 0.05f, 1f));
            }
            else
            {
                _progressIndicators[i].Text = "\u25CB";
                _progressIndicators[i].AddThemeColorOverride("font_color", new Color(0.34f, 0.45f, 0.58f, 1f));
            }
        }

        _instrucao.Text = $"Clique quando a seta estiver na faixa dourada! ({_acertos}/{MaxAcertos})";
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

    private void Fechar(bool sucesso)
    {
        _rodadaAtiva = false;
        _podeClicar = false;
        _capturarBtn.Disabled = false;
        _fecharBtn.Disabled = false;
        _panel.Visible = false;
        EmitSignal(SignalName.MiniGameConcluido, _petIdReward, _petNameReward, sucesso);
    }

    private static StyleBoxFlat CriarStyle(Color bg, Color border, int radius, int borderWidth)
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

    private static void EstilizarBotao(Button button, Color bg, Color border)
    {
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 1f, 1f));
        button.AddThemeStyleboxOverride("normal", CriarStyle(bg, border, 5, 1));
        button.AddThemeStyleboxOverride("hover", CriarStyle(bg.Lightened(0.12f), border.Lightened(0.15f), 5, 1));
        button.AddThemeStyleboxOverride("pressed", CriarStyle(bg.Darkened(0.08f), border, 5, 1));
        button.AddThemeStyleboxOverride("disabled", CriarStyle(new Color(0.08f, 0.09f, 0.11f, 0.85f), new Color(0.18f, 0.20f, 0.24f, 0.9f), 5, 1));
    }
}
