using Godot;
using System.Collections.Generic;

public partial class PetScrollMiniGame : Control
{
    private Panel _panel;
    private Label _titulo;
    private Label _instrucao;
    private Label _statusLabel;
    private HBoxContainer _progressContainer;
    private Button _capturarBtn;
    private Button _fecharBtn;

    private Control _barBg;
    private ColorRect _barRed;
    private ColorRect _sweetSpot;
    private ColorRect _marker;

    private readonly List<Label> _progressIndicators = new();

    private int _acertos;
    private const int MaxAcertos = 5;
    private float _markerPos;
    private float _markerSpeed;
    private float _sweetSpotPos;
    private float _sweetSpotWidth;
    private float _barWidth;
    private bool _roundActive;
    private bool _canClick;

    private int _petIdReward;
    private string _petNameReward;

    [Signal]
    public delegate void MiniGameConcluidoEventHandler(int petId, string petNome, bool sucesso);

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titulo = GetNode<Label>("%TituloLabel");
        _instrucao = GetNode<Label>("%InstrucaoLabel");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _progressContainer = GetNode<HBoxContainer>("%ProgressContainer");
        _capturarBtn = GetNode<Button>("%CapturarBtn");
        _fecharBtn = GetNode<Button>("%FecharBtn");

        _capturarBtn.Pressed += TentarCaptura;
        _fecharBtn.Pressed += () => Fechar(false);

        _progressContainer.AddThemeConstantOverride("separation", 10);

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

        _barBg = new Control();
        _barBg.CustomMinimumSize = new Vector2(300, 30);
        _barBg.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        var vbox = _panel.GetNode("VBox");
        vbox.AddChild(_barBg);
        vbox.MoveChild(_barBg, _statusLabel.GetIndex());

        _barRed = new ColorRect();
        _barRed.Color = new Color(0.5f, 0.1f, 0.1f, 0.9f);
        _barRed.MouseFilter = MouseFilterEnum.Ignore;
        _barBg.AddChild(_barRed);

        _sweetSpot = new ColorRect();
        _sweetSpot.Color = new Color(1, 1, 0, 0.5f);
        _sweetSpot.MouseFilter = MouseFilterEnum.Ignore;
        _barBg.AddChild(_sweetSpot);

        _marker = new ColorRect();
        _marker.Color = new Color(1, 1, 1, 0.9f);
        _marker.CustomMinimumSize = new Vector2(4, 30);
        _marker.Size = new Vector2(4, 30);
        _marker.MouseFilter = MouseFilterEnum.Ignore;
        _barBg.AddChild(_marker);

        _panel.Visible = false;
    }

    public void IniciarMiniGame(int petId, string petNome)
    {
        _petIdReward = petId;
        _petNameReward = petNome;
        _acertos = 0;
        _canClick = true;

        _titulo.Text = $"Capture: {petNome}";
        _instrucao.Text = $"Clique quando a seta estiver na faixa amarela! (0/{MaxAcertos})";
        _statusLabel.Text = "";
        _statusLabel.Modulate = Colors.White;
        AtualizarProgresso();

        GD.Print($"[PET MINIGAME] Iniciando captura de '{petNome}'");
        _panel.Visible = true;
        IniciarRodada();
    }

    private void IniciarRodada()
    {
        _roundActive = true;
        _canClick = true;
        _instrucao.Text = $"Clique quando a seta estiver na faixa amarela! ({_acertos}/{MaxAcertos})";

        _sweetSpotPos = (float)GD.RandRange(0.2, 0.8);
        _sweetSpotWidth = (float)GD.RandRange(0.10, 0.18);

        _markerPos = (float)GD.RandRange(0.1, 0.9);
        _markerSpeed = (float)GD.RandRange(120, 200);
        if (GD.RandRange(0, 1) == 0)
            _markerSpeed = -_markerSpeed;
    }

    public override void _Process(double delta)
    {
        if (!_roundActive || !_canClick) return;

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

        _barRed.Position = Vector2.Zero;
        _barRed.Size = new Vector2(_barWidth, 30);

        _sweetSpot.Position = new Vector2(
            (_sweetSpotPos - _sweetSpotWidth / 2) * _barWidth, 0);
        _sweetSpot.Size = new Vector2(
            _sweetSpotWidth * _barWidth, 30);

        _marker.Position = new Vector2(
            _markerPos * _barWidth - _marker.Size.X / 2, 0);

        if (Input.IsActionJustPressed("ui_accept"))
        {
            TentarCaptura();
        }
    }

    private void TentarCaptura()
    {
        if (!_roundActive || !_canClick) return;
        _roundActive = false;

        float halfWidth = _sweetSpotWidth / 2;
        bool acertou = _markerPos >= _sweetSpotPos - halfWidth
                    && _markerPos <= _sweetSpotPos + halfWidth;

        if (acertou)
        {
            _acertos++;
            _statusLabel.Text = "Acertou!";
            _statusLabel.Modulate = new Color(0, 1, 0);
            AtualizarProgresso();

            if (_acertos >= MaxAcertos)
            {
                _instrucao.Text = "Pet capturado!";
                _statusLabel.Text = "Sucesso!";
                var t = GetTree().CreateTimer(0.8f);
                t.Timeout += () => Fechar(true);
                return;
            }

            var t2 = GetTree().CreateTimer(0.4f);
            t2.Timeout += IniciarRodada;
        }
        else
        {
            _statusLabel.Text = "Errou! Pet escapou...";
            _statusLabel.Modulate = new Color(1, 0, 0);
            var t = GetTree().CreateTimer(0.6f);
            t.Timeout += () => Fechar(false);
        }
    }

    private void AtualizarProgresso()
    {
        for (int i = 0; i < MaxAcertos; i++)
        {
            if (i < _acertos)
            {
                _progressIndicators[i].Text = "\u25CF";
                _progressIndicators[i].AddThemeColorOverride("font_color", new Color(1, 1, 0));
            }
            else
            {
                _progressIndicators[i].Text = "\u25CB";
                _progressIndicators[i].AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            }
        }
    }

    private void Fechar(bool sucesso)
    {
        _roundActive = false;
        _canClick = false;
        _panel.Visible = false;
        EmitSignal(SignalName.MiniGameConcluido, _petIdReward, _petNameReward, sucesso);
    }
}
