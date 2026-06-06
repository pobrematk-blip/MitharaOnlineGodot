using Godot;
using System;
using System.Collections.Generic;

public partial class ClasseEditorUI : Control
{
    private Panel _panel;
    private ItemList _listaClasses;
    private LineEdit _nomeEdit;
    private TextEdit _descricaoEdit;
    private OptionButton _spriteOpcao;
    private OptionButton _racaOpcao;
    private OptionButton _ataqueOpcao;
    private OptionButton _arvoreOpcao;
    private CheckBox _ataqueIgualSpriteCheck;
    private CheckBox _usaProjetilCheck;
    private Label _previewStatus;
    private Label _labelAtaqueInfo;
    private ClasseCustomResource _classeEditando;
    private readonly Dictionary<string, SpinBox> _spins = new();
    private readonly List<string> _prefixosAtaqueLista = new();
    private bool _ignorarEventosUi;

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        var fechar = _panel.GetNode<Button>("Header/CloseButton");
        var header = _panel.GetNode<Control>("Header");
        fechar.Pressed += () => { _panel.Visible = false; };
        header.GuiInput += OnHeaderDrag;

        _listaClasses = GetNode<ItemList>("%ListaClasses");
        _nomeEdit = GetNode<LineEdit>("%NomeEdit");
        _descricaoEdit = GetNode<TextEdit>("%DescricaoEdit");
        _spriteOpcao = GetNode<OptionButton>("%SpriteOpcao");
        _racaOpcao = GetNode<OptionButton>("%RacaOpcao");
        _ataqueOpcao = GetNode<OptionButton>("%AtaqueOpcao");
        _arvoreOpcao = GetNode<OptionButton>("%ArvoreOpcao");
        _ataqueIgualSpriteCheck = GetNode<CheckBox>("%AtaqueIgualSpriteCheck");
        _usaProjetilCheck = GetNode<CheckBox>("%UsaProjetilCheck");
        _previewStatus = GetNode<Label>("%PreviewStatus");
        _labelAtaqueInfo = GetNode<Label>("%LabelAtaqueInfo");

        RegistrarSpin("%ForcaSpin", "Forca");
        RegistrarSpin("%AgilidadeSpin", "Agilidade");
        RegistrarSpin("%DestrezaSpin", "Destreza");
        RegistrarSpin("%InteligenciaSpin", "Inteligencia");
        RegistrarSpin("%PontosSpin", "Pontos");
        RegistrarSpin("%VidaSpin", "Vida");
        RegistrarSpin("%ManaSpin", "Mana");
        RegistrarSpin("%VelMovSpin", "VelMov");
        RegistrarSpin("%DanoMinSpin", "DanoMin");
        RegistrarSpin("%DanoMaxSpin", "DanoMax");
        RegistrarSpin("%VelProjSpin", "VelProj");
        RegistrarSpin("%BonusCritSpin", "BonusCrit");
        RegistrarSpin("%VelAtaqueSpin", "VelAtaque");

        GetNode<Button>("%BtnNova").Pressed += OnNova;
        GetNode<Button>("%BtnSalvar").Pressed += OnSalvar;
        GetNode<Button>("%BtnExcluir").Pressed += OnExcluir;
        GetNode<Button>("%BtnTestar").Pressed += OnTestar;
        GetNode<Button>("%BtnRecarregar").Pressed += OnRecarregar;

        _listaClasses.ItemSelected += OnClasseSelecionada;
        _nomeEdit.TextChanged += _ =>
        {
            SugerirPorNomeClasse();
            AtualizarPreview();
        };
        _usaProjetilCheck.Toggled += _ => AtualizarPreview();
        _spriteOpcao.ItemSelected += OnSpriteOuAtaqueAlterado;
        _ataqueOpcao.ItemSelected += OnSpriteOuAtaqueAlterado;
        _ataqueIgualSpriteCheck.Toggled += OnAtaqueIgualSpriteToggled;
        foreach (var spin in _spins.Values)
            spin.ValueChanged += _ => AtualizarPreview();

        _panel.Visible = false;
        CallDeferred(MethodName.InicializarListas);
    }

    private void RegistrarSpin(string path, string chave)
    {
        var spin = GetNode<SpinBox>(path);
        _spins[chave] = spin;
        spin.ValueChanged += _ => AtualizarPreview();
    }

    private void InicializarListas()
    {
        var registry = GetNodeOrNull<ClasseRegistry>("/root/ClasseRegistry");
        registry?.RecarregarTudo();
        PreencherOpcoesSprite(registry);
        PreencherOpcoesAtaque(registry);
        PreencherOpcoesRaca(registry);
        PreencherOpcoesArvore();
        AtualizarListaClasses(registry);
        AtualizarEstadoUiAtaque();
    }

    private void PreencherOpcoesAtaque(ClasseRegistry registry)
    {
        _ataqueOpcao.Clear();
        _prefixosAtaqueLista.Clear();
        if (registry == null) return;

        foreach (var prefixo in registry.PrefixosAnimacaoAtaque)
        {
            _prefixosAtaqueLista.Add(prefixo);
            _ataqueOpcao.AddItem(registry.ObterRotuloAtaque(prefixo), _prefixosAtaqueLista.Count - 1);
        }
    }

    private void OnAtaqueIgualSpriteToggled(bool pressed)
    {
        if (_ignorarEventosUi) return;
        AtualizarEstadoUiAtaque();
        if (pressed)
            SincronizarAtaqueComSprite();
        AtualizarPreview();
    }

    private void OnSpriteOuAtaqueAlterado(long _)
    {
        if (_ignorarEventosUi) return;

        if (_ataqueIgualSpriteCheck.ButtonPressed)
            SincronizarAtaqueComSprite();

        if (!_ataqueIgualSpriteCheck.ButtonPressed)
            SugerirCombatePorTipoAtaque();

        AtualizarPreview();
    }

    private void SincronizarAtaqueComSprite()
    {
        var registry = GetNodeOrNull<ClasseRegistry>("/root/ClasseRegistry");
        if (registry == null || _spriteOpcao.GetSelectedId() < 0) return;

        int idxSprite = _spriteOpcao.GetSelectedId();
        if (idxSprite >= registry.Sprites.Count) return;

        SelecionarPrefixoAtaque(registry.Sprites[idxSprite].PrefixoAnimacao);
    }

    private void SelecionarPrefixoAtaque(string prefixo)
    {
        int idx = _prefixosAtaqueLista.IndexOf(prefixo?.Trim().ToLower() ?? "");
        if (idx >= 0)
            _ataqueOpcao.Select(idx);
    }

    private void AtualizarEstadoUiAtaque()
    {
        _ataqueOpcao.Disabled = _ataqueIgualSpriteCheck.ButtonPressed;
    }

    private void SugerirCombatePorTipoAtaque()
    {
        if (_ignorarEventosUi) return;

        switch (ObterPrefixoAtaqueSelecionado())
        {
            case "mago":
                _usaProjetilCheck.ButtonPressed = true;
                _spins["VelAtaque"].Value = string.Equals(ObterNomeClasseNormalizado(), "prist", StringComparison.OrdinalIgnoreCase) ? 2.8 : 3.0;
                break;
            case "arqueiro":
                _usaProjetilCheck.ButtonPressed = true;
                _spins["VelAtaque"].Value = 3.5;
                break;
            case "guerreiro":
                _usaProjetilCheck.ButtonPressed = false;
                _spins["VelAtaque"].Value = ObterNomeClasseNormalizado() switch
                {
                    "guardiao" or "guradiao" => 2.2,
                    "berseker" or "berserker" => 2.8,
                    _ => 2.5
                };
                break;
            case "ladino":
                _usaProjetilCheck.ButtonPressed = false;
                _spins["VelAtaque"].Value = 4.0;
                break;
        }
    }

    private string ObterNomeClasseNormalizado() =>
        _nomeEdit.Text.Trim().ToLowerInvariant();

    private void SugerirPorNomeClasse()
    {
        if (_ignorarEventosUi) return;

        string nome = ObterNomeClasseNormalizado();
        if (string.IsNullOrEmpty(nome)) return;

        var registry = GetNodeOrNull<ClasseRegistry>("/root/ClasseRegistry");
        if (registry == null) return;

        string prefixoAtk = ClasseRegistry.ObterPrefixoAtaqueRecomendado(nome);
        string prefixoSprite = ClasseRegistry.ObterPrefixoSpriteRecomendado(nome);

        int idxSprite = registry.Sprites.FindIndex(s =>
            s.NomePreset.Equals(_nomeEdit.Text.Trim(), System.StringComparison.OrdinalIgnoreCase));
        if (idxSprite < 0)
        {
            idxSprite = registry.Sprites.FindIndex(s =>
                s.PrefixoAnimacao.Equals(prefixoSprite, System.StringComparison.OrdinalIgnoreCase));
        }
        if (idxSprite >= 0)
            _spriteOpcao.Select(idxSprite);

        bool ataqueDiferenteDoSprite = prefixoAtk != prefixoSprite;
        _ataqueIgualSpriteCheck.ButtonPressed = !ataqueDiferenteDoSprite;
        AtualizarEstadoUiAtaque();
        SelecionarPrefixoAtaque(prefixoAtk);

        if (System.Array.Exists(ClasseRegistry.ClassesOficiais, c =>
                c.Equals(_nomeEdit.Text.Trim(), System.StringComparison.OrdinalIgnoreCase)))
            SugerirCombatePorTipoAtaque();
    }

    private string ObterPrefixoAtaqueSelecionado()
    {
        int idx = _ataqueOpcao.GetSelectedId();
        if (idx >= 0 && idx < _prefixosAtaqueLista.Count)
            return _prefixosAtaqueLista[idx];
        return "mago";
    }

    private void PreencherOpcoesSprite(ClasseRegistry registry)
    {
        _spriteOpcao.Clear();
        if (registry == null) return;
        foreach (var s in registry.Sprites)
            _spriteOpcao.AddItem(s.NomePreset, registry.Sprites.IndexOf(s));
    }

    private void PreencherOpcoesRaca(ClasseRegistry registry)
    {
        _racaOpcao.Clear();
        _racaOpcao.AddItem("(Nenhuma)", -1);
        if (registry == null) return;
        foreach (var r in registry.Racas)
            _racaOpcao.AddItem(r.NomeRaca, registry.Racas.IndexOf(r));
    }

    private void PreencherOpcoesArvore()
    {
        _arvoreOpcao.Clear();
        _arvoreOpcao.AddItem("(Nenhuma)", -1);
        int idx = 1;
        var dir = DirAccess.Open("res://skills/ArvoresClasses");
        if (dir == null) return;
        dir.ListDirBegin();
        while (true)
        {
            var file = dir.GetNext();
            if (string.IsNullOrEmpty(file)) break;
            if (!file.EndsWith(".tres")) continue;
            var path = $"res://skills/ArvoresClasses/{file}";
            var tree = ResourceLoader.Load<TalentTreeResource>(path);
            if (tree == null) continue;
            _arvoreOpcao.AddItem(tree.NomeArvore, idx);
            _arvoreOpcao.SetItemMetadata(idx, path);
            idx++;
        }
        dir.ListDirEnd();
    }

    private void AtualizarListaClasses(ClasseRegistry registry)
    {
        _listaClasses.Clear();
        if (registry == null) return;
        foreach (var c in registry.Classes)
        {
            string atk = c.ObterPrefixoAtaque();
            string sprite = c.ObterPrefixoAnimacao();
            string sufixo = atk == sprite ? $"[{sprite}]" : $"[{sprite} / atk:{atk}]";
            string arvore = c.ArvoreTalentos != null ? $" [{c.ArvoreTalentos.NomeArvore}]" : "";
            _listaClasses.AddItem($"{c.NomeClasse}  {sufixo}{arvore}");
        }
        if (registry.Classes.Count > 0)
            _listaClasses.Select(0);
    }

    private void OnClasseSelecionada(long index)
    {
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        if (registry == null || index < 0 || index >= registry.Classes.Count) return;

        _classeEditando = registry.Classes[(int)index];
        CarregarClasseNaUi(_classeEditando);
    }

    private void CarregarClasseNaUi(ClasseCustomResource c)
    {
        if (c == null) return;

        _nomeEdit.Text = c.NomeClasse;
        _descricaoEdit.Text = c.Descricao;
        _usaProjetilCheck.ButtonPressed = c.UsaProjetil;

        _spins["Forca"].Value = c.Forca;
        _spins["Agilidade"].Value = c.Agilidade;
        _spins["Destreza"].Value = c.Destreza;
        _spins["Inteligencia"].Value = c.Inteligencia;
        _spins["Pontos"].Value = c.PontosDisponiveis;
        _spins["Vida"].Value = c.VidaMaxima;
        _spins["Mana"].Value = c.ManaMaxima;
        _spins["VelMov"].Value = c.VelocidadeMovimento;
        _spins["DanoMin"].Value = c.DanoProjetilMin;
        _spins["DanoMax"].Value = c.DanoProjetilMax;
        _spins["VelProj"].Value = c.VelocidadeDoProjetil;
        _spins["BonusCrit"].Value = c.BonusDanoCritico;
        _spins["VelAtaque"].Value = c.AttackAnimSpeedScale > 0f ? c.AttackAnimSpeedScale : 3f;

        _ignorarEventosUi = true;
        SelecionarSprite(c.SpritePreset);
        _ataqueIgualSpriteCheck.ButtonPressed = c.AtaqueUsaMesmoPrefixoDoSprite();
        SelecionarPrefixoAtaque(c.ObterPrefixoAtaque());
        AtualizarEstadoUiAtaque();
        SelecionarRaca(c.Raca);
        SelecionarArvore(c.ArvoreTalentos);
        _ignorarEventosUi = false;

        AtualizarPreview();
    }

    private void SelecionarSprite(SpritePresetResource preset)
    {
        if (preset == null) { _spriteOpcao.Select(0); return; }
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        int idx = registry.Sprites.IndexOf(preset);
        if (idx >= 0) _spriteOpcao.Select(idx);
    }

    private void SelecionarRaca(RacaResource raca)
    {
        if (raca == null) { _racaOpcao.Select(0); return; }
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        int idx = registry.Racas.IndexOf(raca);
        if (idx >= 0) _racaOpcao.Select(idx + 1);
    }

    private void SelecionarArvore(TalentTreeResource arvore)
    {
        if (arvore == null) { _arvoreOpcao.Select(0); return; }
        for (int i = 1; i < _arvoreOpcao.GetItemCount(); i++)
        {
            var path = _arvoreOpcao.GetItemMetadata(i).AsString();
            if (!string.IsNullOrEmpty(path))
            {
                var loaded = ResourceLoader.Load<TalentTreeResource>(path);
                if (loaded == arvore)
                {
                    _arvoreOpcao.Select(i);
                    return;
                }
            }
        }
        _arvoreOpcao.Select(0);
    }

    private ClasseCustomResource LerClasseDaUi()
    {
        var c = _classeEditando ?? new ClasseCustomResource();
        c.NomeClasse = _nomeEdit.Text.Trim();
        if (string.IsNullOrEmpty(c.NomeClasse)) c.NomeClasse = "Nova Classe";
        c.Descricao = _descricaoEdit.Text;
        c.Forca = (int)_spins["Forca"].Value;
        c.Agilidade = (int)_spins["Agilidade"].Value;
        c.Destreza = (int)_spins["Destreza"].Value;
        c.Inteligencia = (int)_spins["Inteligencia"].Value;
        c.PontosDisponiveis = (int)_spins["Pontos"].Value;
        c.VidaMaxima = (int)_spins["Vida"].Value;
        c.ManaMaxima = (int)_spins["Mana"].Value;
        c.VelocidadeMovimento = (float)_spins["VelMov"].Value;
        c.UsaProjetil = _usaProjetilCheck.ButtonPressed;
        c.DanoProjetilMin = (int)_spins["DanoMin"].Value;
        c.DanoProjetilMax = (int)_spins["DanoMax"].Value;
        c.VelocidadeDoProjetil = (float)_spins["VelProj"].Value;
        c.BonusDanoCritico = (float)_spins["BonusCrit"].Value;
        c.AttackAnimSpeedScale = (float)_spins["VelAtaque"].Value;

        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        int idxSprite = _spriteOpcao.GetSelectedId();
        if (idxSprite >= 0 && idxSprite < registry.Sprites.Count)
        {
            c.SpritePreset = registry.Sprites[idxSprite];
            c.PrefixoAnimacao = c.SpritePreset.PrefixoAnimacao;
        }

        if (_ataqueIgualSpriteCheck.ButtonPressed)
            c.PrefixoAnimacaoAtaque = "";
        else
            c.PrefixoAnimacaoAtaque = ObterPrefixoAtaqueSelecionado();

        int idxRaca = _racaOpcao.GetSelectedId();
        c.Raca = idxRaca >= 0 && idxRaca < registry.Racas.Count ? registry.Racas[idxRaca] : null;

        int idxArvore = _arvoreOpcao.GetSelectedId();
        if (idxArvore > 0)
        {
            var path = _arvoreOpcao.GetItemMetadata(idxArvore).AsString();
            if (!string.IsNullOrEmpty(path))
                c.ArvoreTalentos = ResourceLoader.Load<TalentTreeResource>(path);
        }
        else
            c.ArvoreTalentos = null;

        if (c.UsaProjetil && c.CenaDoProjetil == null)
        {
            c.CenaDoProjetil = ResourceLoader.Load<PackedScene>("res://resources/Projetil/Projetil.tscn");
            c.ProjetilDanoMagico = string.Equals(c.ObterPrefixoAtaque(), "mago", StringComparison.OrdinalIgnoreCase);
        }

        return c;
    }

    private void AtualizarPreview()
    {
        var c = LerClasseDaUi();
        EquipamentoComponent.PreencherPreview(c, out int f, out int a, out int d, out int i);

        int hp = c.VidaMaxima + (c.Raca?.BonusVidaMaxima ?? 0);
        int mana = c.ManaMaxima + (c.Raca?.BonusManaMaxima ?? 0);
        int danoFisMin = 8 + f / 2;
        int danoFisMax = 12 + f / 2;
        int danoMagMin = 8 + i / 2;
        int danoMagMax = 12 + i / 2;
        float crit = 1.5f + c.BonusDanoCritico + (c.Raca?.BonusDanoCritico ?? 0f);
        float chanceCrit = d * 0.5f;
        float evasao = d * 0.3f;

        var registry = GetNodeOrNull<ClasseRegistry>("/root/ClasseRegistry");
        string prefixoAtk = c.ObterPrefixoAtaque();
        string prefixoSprite = c.ObterPrefixoAnimacao();
        bool animOk = registry?.AnimacaoAtaqueExiste(prefixoAtk) == true;
        string rotuloAtk = registry?.ObterRotuloAtaque(prefixoAtk) ?? prefixoAtk;

        _labelAtaqueInfo.Text = animOk
            ? $"✓ Animações: {prefixoAtk}_attack_* ({rotuloAtk})"
            : $"⚠ Falta animação {prefixoAtk}_attack_* no player.tscn";

        _previewStatus.Text =
            $"── PREVIEW (igual Character UI) ──\n" +
            $"Força {f} | Agi {a} | Des {d} | Int {i}\n" +
            $"Vida {hp} | Mana {mana}\n" +
            $"Dano Físico: {danoFisMin}-{danoFisMax}\n" +
            $"Dano Mágico: {danoMagMin}-{danoMagMax}\n" +
            $"Crítico: {chanceCrit:F1}% | Dano Crít: {crit:F2}x\n" +
            $"Evasão: {evasao:F1}% | Def Fís: {a / 2} | Def Mag: {i / 3}\n" +
            $"Vel. Mov: {1f + a * 0.05f:F2}x | Vel. Ataque: {1f + a * 0.03f:F2}x\n" +
            $"Sprite: {prefixoSprite} | Ataque: {prefixoAtk} | Vel.anim: {c.AttackAnimSpeedScale:F1}x\n" +
            $"Projétil: {(c.UsaProjetil ? "Sim" : "Não")}\n" +
            $"Árvore: {c.ArvoreTalentos?.NomeArvore ?? "(nenhuma)"}";
    }

    private void OnNova()
    {
        _classeEditando = new ClasseCustomResource
        {
            NomeClasse = "Nova Classe",
            Forca = 10,
            Agilidade = 10,
            Destreza = 10,
            Inteligencia = 10,
            PontosDisponiveis = 10,
            PrefixoAnimacao = "mago",
            PrefixoAnimacaoAtaque = "",
            AttackAnimSpeedScale = 3f,
            UsaProjetil = true,
            DanoProjetilMin = 15,
            DanoProjetilMax = 22,
            VelocidadeDoProjetil = 220f
        };
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        if (registry.Sprites.Count > 0)
            _classeEditando.SpritePreset = registry.Sprites[0];
        CarregarClasseNaUi(_classeEditando);
        _listaClasses.DeselectAll();
    }

    private void OnSalvar()
    {
        var c = LerClasseDaUi();
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        if (registry.SalvarClasse(c))
        {
            _classeEditando = c;
            AtualizarListaClasses(registry);
            GD.Print($"[EDITOR CLASSE] Salvo: {c.NomeClasse}");
        }
    }

    private void OnExcluir()
    {
        if (_classeEditando == null) return;
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        if (registry.ExcluirClasse(_classeEditando))
        {
            _classeEditando = null;
            OnNova();
            AtualizarListaClasses(registry);
        }
    }

    private void OnTestar()
    {
        var c = LerClasseDaUi();
        var player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        player?.AplicarClasse(c);
        GD.Print($"[EDITOR CLASSE] Classe '{c.NomeClasse}' aplicada ao Player para teste.");
    }

    private void OnRecarregar()
    {
        var registry = GetNode<ClasseRegistry>("/root/ClasseRegistry");
        registry.RecarregarTudo();
        PreencherOpcoesSprite(registry);
        PreencherOpcoesAtaque(registry);
        PreencherOpcoesRaca(registry);
        PreencherOpcoesArvore();
        AtualizarListaClasses(registry);
        AtualizarEstadoUiAtaque();
    }

    private Vector2 _dragOffset;
    private bool _arrastando;

    private void OnHeaderDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mb.Pressed;
            if (mb.Pressed)
                _dragOffset = mb.Position;
        }
        else if (@event is InputEventMouseMotion mm && _arrastando)
            _panel.Position += mm.Position - _dragOffset;
    }

    public override void _Input(InputEvent @event)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;
        if (@event.IsActionPressed("editor_classe"))
        {
            _panel.Visible = !_panel.Visible;
            if (_panel.Visible)
                InicializarListas();
            GetViewport().SetInputAsHandled();
        }
    }
}
