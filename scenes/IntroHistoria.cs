using Godot;

public partial class IntroHistoria : Control
{

    [Export] public float VelocidadeRolagem = 42f;
    [Export] public float PausaFinalSegundos = 1.2f;

    private Control _areaRecorte;
    private RichTextLabel _textoHistoria;
    private Label _dicaPular;
    private ColorRect _fade;
    private float _posicaoY;
    private float _alturaTexto;
    private bool _rolagemAtiva;
    private bool _transicaoIniciada;
    private double _tempoPausaFinal;

    public override void _Ready()
    {
        _areaRecorte = GetNode<Control>("%AreaRecorte");
        _textoHistoria = GetNode<RichTextLabel>("%TextoHistoria");
        _dicaPular = GetNode<Label>("%DicaPular");
        _fade = GetNode<ColorRect>("%Fade");

        _textoHistoria.BbcodeEnabled = true;
        _textoHistoria.Text = LoreMithara.TextoCompleto;
        _textoHistoria.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _fade.Modulate = new Color(1, 1, 1, 1);
        _dicaPular.Modulate = new Color(1, 1, 1, 0);

        var tweenDica = CreateTween();
        tweenDica.TweenInterval(1.5);
        tweenDica.TweenProperty(_dicaPular, "modulate:a", 1f, 0.8);

        var tweenFadeIn = CreateTween();
        tweenFadeIn.TweenProperty(_fade, "modulate:a", 0f, 1.2);

        CallDeferred(nameof(IniciarRolagem));
    }

    private void IniciarRolagem()
    {
        float largura = _areaRecorte.Size.X;
        _textoHistoria.CustomMinimumSize = new Vector2(largura, 0);
        _textoHistoria.Size = new Vector2(largura, 0);
        _textoHistoria.FitContent = true;

        CallDeferred(nameof(CalcularAlturaEIniciar));
    }

    private void CalcularAlturaEIniciar()
    {
        _alturaTexto = _textoHistoria.GetContentHeight() + 80f;
        _textoHistoria.CustomMinimumSize = new Vector2(_areaRecorte.Size.X, _alturaTexto);
        _textoHistoria.Size = new Vector2(_areaRecorte.Size.X, _alturaTexto);

        _posicaoY = _areaRecorte.Size.Y + 40f;
        _textoHistoria.Position = new Vector2(0, _posicaoY);
        _rolagemAtiva = true;
    }

    public override void _Process(double delta)
    {
        if (!_rolagemAtiva || _transicaoIniciada) return;

        _posicaoY -= VelocidadeRolagem * (float)delta;
        _textoHistoria.Position = new Vector2(0, _posicaoY);

        if (_posicaoY + _alturaTexto < -20f)
        {
            _rolagemAtiva = false;
            _tempoPausaFinal = PausaFinalSegundos;
        }

        if (!_rolagemAtiva && !_transicaoIniciada)
        {
            _tempoPausaFinal -= delta;
            if (_tempoPausaFinal <= 0)
                IrParaCriacaoPersonagem();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_transicaoIniciada) return;

        if (@event is InputEventMouseButton mouse && mouse.Pressed)
        {
            IrParaCriacaoPersonagem();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            IrParaCriacaoPersonagem();
            GetViewport().SetInputAsHandled();
        }
    }

    private void IrParaCriacaoPersonagem()
    {
        if (_transicaoIniciada) return;
        _transicaoIniciada = true;
        _rolagemAtiva = false;

        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", 1f, 0.6);
        tween.TweenCallback(Callable.From(() =>
        {
            GetTree().ChangeSceneToFile(SceneConstants.CRIACAO_PERSONAGEM);
        }));
    }
}
