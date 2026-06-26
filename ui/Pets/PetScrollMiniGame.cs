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
        _titulo = GetNode<Label>("%TituloLabel");
        _instrucao = GetNode<Label>("%InstrucaoLabel");
        _chancesLabel = new Label();
        _chancesLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _chancesLabel.AddThemeFontSizeOverride("font_size", 14);
        _statusLabel = GetNode<Label>("%StatusLabel");
        _progressContainer = GetNode<HBoxContainer>("%ProgressContainer");
        _capturarBtn = GetNode<Button>("%CapturarBtn");
        _fecharBtn = GetNode<Button>("%FecharBtn");

        _capturarBtn.Pressed += TentarCaptura;
        _fecharBtn.Pressed += () => Fechar(false);

        _progressContainer.AddThemeConstantOverride("separation", 10);

        var vbox = _panel.GetNode("VBox");

        _titleBar = new Panel();
        _titleBar.CustomMinimumSize = new Vector2(0, 28);
        _titleBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleBar.MouseFilter = MouseFilterEnum.Pass;

        var titleStyle = new StyleBoxFlat();
        titleStyle.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        titleStyle.CornerRadiusTopLeft = 8;
        titleStyle.CornerRadiusTopRight = 8;
        _titleBar.AddThemeStyleboxOverride("panel", titleStyle);

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
            lbl.AddThemeFontSizeOverride("font_size", 28);
            lbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            _progressContainer.AddChild(lbl);
            _progressIndicators.Add(lbl);
        }

        _chancesLabel.Text = $"Tentativas: {MaxTentativas}/{MaxTentativas}";
        vbox.AddChild(_chancesLabel);
        vbox.MoveChild(_chancesLabel, _instrucao.GetIndex() + 1);

        _barBg = new Control();
        _barBg.CustomMinimumSize = new Vector2(300, 30);
        _barBg.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_barBg);
        vbox.MoveChild(_barBg, _chancesLabel.GetIndex() + 1);

        _barGreen = new Panel();
        _barGreen.MouseFilter = MouseFilterEnum.Ignore;
        var greenStyle = new StyleBoxFlat();
        greenStyle.BgColor = new Color(0.1f, 0.5f, 0.1f, 0.9f);
        greenStyle.CornerRadiusTopLeft = 6;
        greenStyle.CornerRadiusTopRight = 6;
        greenStyle.CornerRadiusBottomRight = 6;
        greenStyle.CornerRadiusBottomLeft = 6;
        _barGreen.AddThemeStyleboxOverride("panel", greenStyle);
        _barBg.AddChild(_barGreen);

        _sweetSpot = new Panel();
        _sweetSpot.MouseFilter = MouseFilterEnum.Ignore;
        var redStyle = new StyleBoxFlat();
        redStyle.BgColor = new Color(0.9f, 0.1f, 0.1f, 0.7f);
        redStyle.CornerRadiusTopLeft = 6;
        redStyle.CornerRadiusTopRight = 6;
        redStyle.CornerRadiusBottomRight = 6;
        redStyle.CornerRadiusBottomLeft = 6;
        _sweetSpot.AddThemeStyleboxOverride("panel", redStyle);
        _barBg.AddChild(_sweetSpot);

        _marker = new Label();
        _marker.Text = "\u25BC";
        _marker.AddThemeFontSizeOverride("font_size", 20);
        _marker.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _marker.HorizontalAlignment = HorizontalAlignment.Center;
        _marker.VerticalAlignment = VerticalAlignment.Top;
        _marker.MouseFilter = MouseFilterEnum.Ignore;
        _marker.Size = new Vector2(20, 30);
        _barBg.AddChild(_marker);

        _panel.Visible = false;
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
        _instrucao.Text = "Clique quando a seta estiver na faixa vermelha!";
        _statusLabel.Text = "";
        _chancesLabel.Text = $"Tentativas: {_maxTentativas}/{_maxTentativas}";
        AtualizarProgresso();

        GD.Print($"[PET MINIGAME] Iniciando captura de '{petNome}' ({MaxAcertos} acertos em {_maxTentativas} tentativas)");
        _panel.Visible = true;
        IniciarRodada();
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
        _barGreen.Size = new Vector2(_barWidth, 30);

        _sweetSpot.Position = new Vector2(
            (_sweetSpotPos - _sweetSpotWidth / 2) * _barWidth, 0);
        _sweetSpot.Size = new Vector2(
            _sweetSpotWidth * _barWidth, 30);

        _marker.Position = new Vector2(
            _markerPos * _barWidth - _marker.Size.X / 2, -18);

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
                _progressIndicators[i].AddThemeColorOverride("font_color", new Color(0, 1, 0));
            }
            else
            {
                _progressIndicators[i].Text = "\u25CB";
                _progressIndicators[i].AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            }
        }

        _instrucao.Text = $"Clique quando a seta estiver na faixa vermelha! ({_acertos}/{MaxAcertos})";
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
}
