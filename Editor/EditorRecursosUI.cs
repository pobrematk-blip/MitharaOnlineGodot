using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class EditorRecursosUI : Control
{
    private ItemList _listaRecursos;
    private LineEdit _nomeEdit;
    private LineEdit _tipoEdit;
    private SpinBox _tempo1Spin;
    private SpinBox _tempo2Spin;
    private SpinBox _tempo3Spin;
    private SpinBox _tempo4Spin;
    private SpinBox _itemIdSpin;
    private LineEdit _ferramentaPathEdit;
    private Button _btnBrowseFerramenta;
    private ItemResource _ferramentaSelecionada;
    private SpinBox _qtdMinSpin;
    private SpinBox _qtdMaxSpin;
    private TextureRect _previewFase1;
    private TextureRect _previewFase2;
    private TextureRect _previewFase3;
    private TextureRect _previewFase4;
    private TextureRect _previewFase5;
    private Button _btnBrowse1;
    private Button _btnBrowse2;
    private Button _btnBrowse3;
    private Button _btnBrowse4;
    private Button _btnBrowse5;
    private Button _btnSalvar;
    private Button _btnNovo;
    private Button _btnExcluir;
    private Label _feedback;

    private RecursoResource _atual;
    private string _atualPath;
    private Texture2D _texFase1;
    private Texture2D _texFase2;
    private Texture2D _texFase3;
    private Texture2D _texFase4;
    private Texture2D _texFase5;

    private static readonly string DirRecursos = "res://Itens/Recursos/";

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f) });

        var hbox = new HBoxContainer();
        AddChild(hbox);
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var leftPanel = new VBoxContainer();
        leftPanel.CustomMinimumSize = new Vector2(200, 0);
        leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        hbox.AddChild(leftPanel);

        leftPanel.AddChild(new Label { Text = "Recursos" });

        _listaRecursos = new ItemList();
        _listaRecursos.SizeFlagsVertical = SizeFlags.ExpandFill;
        leftPanel.AddChild(_listaRecursos);

        _btnNovo = new Button { Text = "+ Novo Recurso" };
        _btnNovo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftPanel.AddChild(_btnNovo);

        _btnExcluir = new Button { Text = "- Excluir" };
        _btnExcluir.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftPanel.AddChild(_btnExcluir);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        hbox.AddChild(scroll);

        var form = new VBoxContainer();
        form.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        form.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.AddChild(form);

        void AddLabel(string text)
        {
            form.AddChild(new Label { Text = text, ThemeTypeVariation = "HeaderLabel" });
        }

        AddLabel("Nome");
        _nomeEdit = new LineEdit();
        form.AddChild(_nomeEdit);

        AddLabel("Tipo (Arvore, Mina, Planta...)");
        _tipoEdit = new LineEdit();
        form.AddChild(_tipoEdit);

        AddLabel("Item Drop ID");
        _itemIdSpin = new SpinBox { MaxValue = 9999, Value = 0 };
        form.AddChild(_itemIdSpin);

        AddLabel("Ferramenta Necessaria");
        var ferramentaHbox = new HBoxContainer();
        _ferramentaPathEdit = new LineEdit { PlaceholderText = "Nenhuma (clique em Procurar)", SizeFlagsHorizontal = SizeFlags.ExpandFill, Editable = false };
        _btnBrowseFerramenta = new Button { Text = "Procurar..." };
        ferramentaHbox.AddChild(_ferramentaPathEdit);
        ferramentaHbox.AddChild(_btnBrowseFerramenta);
        form.AddChild(ferramentaHbox);

        var qtdHbox = new HBoxContainer();
        _qtdMinSpin = new SpinBox { MaxValue = 999, Value = 1, Prefix = "Min " };
        _qtdMaxSpin = new SpinBox { MaxValue = 999, Value = 1, Prefix = "Max " };
        qtdHbox.AddChild(_qtdMinSpin);
        qtdHbox.AddChild(_qtdMaxSpin);
        form.AddChild(new Label { Text = "Quantidade Drop" });
        form.AddChild(qtdHbox);

        AddLabel("Tempo Crescimento (segundos)");
        var tempoHbox = new HBoxContainer();
        _tempo1Spin = new SpinBox { MaxValue = 9999, Value = 30, Prefix = "Fase1->2 " };
        _tempo2Spin = new SpinBox { MaxValue = 9999, Value = 30, Prefix = "Fase2->3 " };
        _tempo3Spin = new SpinBox { MaxValue = 9999, Value = 30, Prefix = "Fase3->4 " };
        _tempo4Spin = new SpinBox { MaxValue = 9999, Value = 30, Prefix = "Fase4->5 " };
        tempoHbox.AddChild(_tempo1Spin);
        tempoHbox.AddChild(_tempo2Spin);
        tempoHbox.AddChild(_tempo3Spin);
        tempoHbox.AddChild(_tempo4Spin);
        form.AddChild(tempoHbox);

        AddLabel("Textura Fase 1 (Toco)");
        var hbox1 = new HBoxContainer();
        _previewFase1 = new TextureRect { CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.FitWidth, StretchMode = TextureRect.StretchModeEnum.KeepAspect };
        _btnBrowse1 = new Button { Text = "Procurar..." };
        hbox1.AddChild(_previewFase1);
        hbox1.AddChild(_btnBrowse1);
        form.AddChild(hbox1);

        AddLabel("Textura Fase 2 (Broto)");
        var hbox2 = new HBoxContainer();
        _previewFase2 = new TextureRect { CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.FitWidth, StretchMode = TextureRect.StretchModeEnum.KeepAspect };
        _btnBrowse2 = new Button { Text = "Procurar..." };
        hbox2.AddChild(_previewFase2);
        hbox2.AddChild(_btnBrowse2);
        form.AddChild(hbox2);

        AddLabel("Textura Fase 3 (Crescendo)");
        var hbox3 = new HBoxContainer();
        _previewFase3 = new TextureRect { CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.FitWidth, StretchMode = TextureRect.StretchModeEnum.KeepAspect };
        _btnBrowse3 = new Button { Text = "Procurar..." };
        hbox3.AddChild(_previewFase3);
        hbox3.AddChild(_btnBrowse3);
        form.AddChild(hbox3);

        AddLabel("Textura Fase 4 (Quase Pronto)");
        var hbox4 = new HBoxContainer();
        _previewFase4 = new TextureRect { CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.FitWidth, StretchMode = TextureRect.StretchModeEnum.KeepAspect };
        _btnBrowse4 = new Button { Text = "Procurar..." };
        hbox4.AddChild(_previewFase4);
        hbox4.AddChild(_btnBrowse4);
        form.AddChild(hbox4);

        AddLabel("Textura Fase 5 (Pronto)");
        var hbox5 = new HBoxContainer();
        _previewFase5 = new TextureRect { CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.FitWidth, StretchMode = TextureRect.StretchModeEnum.KeepAspect };
        _btnBrowse5 = new Button { Text = "Procurar..." };
        hbox5.AddChild(_previewFase5);
        hbox5.AddChild(_btnBrowse5);
        form.AddChild(hbox5);

        form.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _btnSalvar = new Button { Text = "Salvar Recurso" };
        _btnSalvar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _btnSalvar.AddThemeColorOverride("font_color", new Color(0, 0, 0));
        _btnSalvar.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.3f, 0.8f, 0.3f) });
        form.AddChild(_btnSalvar);

        _feedback = new Label();
        _feedback.HorizontalAlignment = HorizontalAlignment.Center;
        _feedback.AddThemeFontSizeOverride("font_size", 12);
        form.AddChild(_feedback);

        _btnBrowse1.Pressed += () => BrowseTexture(idx => _texFase1 = idx, _previewFase1);
        _btnBrowse2.Pressed += () => BrowseTexture(idx => _texFase2 = idx, _previewFase2);
        _btnBrowse3.Pressed += () => BrowseTexture(idx => _texFase3 = idx, _previewFase3);
        _btnBrowse4.Pressed += () => BrowseTexture(idx => _texFase4 = idx, _previewFase4);
        _btnBrowse5.Pressed += () => BrowseTexture(idx => _texFase5 = idx, _previewFase5);

        _btnBrowseFerramenta.Pressed += OnBrowseFerramenta;

        _btnNovo.Pressed += OnNovo;
        _btnExcluir.Pressed += OnExcluir;
        _btnSalvar.Pressed += OnSalvar;
        _listaRecursos.ItemSelected += OnItemSelected;

        CallDeferred(nameof(CarregarLista));
    }

    private void MostrarFeedback(string texto, Color cor)
    {
        _feedback.Text = texto;
        _feedback.AddThemeColorOverride("font_color", cor);
        var timer = GetTree().CreateTimer(2.5);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(_feedback))
                _feedback.Text = "";
        };
    }

    private void BrowseTexture(System.Action<Texture2D> setter, TextureRect preview)
    {
        var dialog = new Godot.FileDialog
        {
            FileMode = Godot.FileDialog.FileModeEnum.OpenFile,
            Filters = new[] { "*.png ; PNG Images", "*.jpg ; JPEG Images" },
            CurrentDir = "res://"
        };
        dialog.FileSelected += (path) =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                setter(tex);
                preview.Texture = tex;
            }
            dialog.QueueFree();
        };
        dialog.CloseRequested += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(600, 400));
    }

    private void CarregarLista()
    {
        _listaRecursos.Clear();
        var dir = DirAccess.Open(DirRecursos);
        if (dir == null)
        {
            MostrarFeedback("Diret?rio 'Recursos' n?o encontrado!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        var paths = new List<string>();
        dir.ListDirBegin();
        while (true)
        {
            var f = dir.GetNext();
            if (string.IsNullOrEmpty(f)) break;
            if (f.EndsWith(".tres") || f.EndsWith(".res"))
                paths.Add(DirRecursos + f);
        }
        dir.ListDirEnd();

        paths.Sort();
        foreach (var p in paths)
        {
            var res = ResourceLoader.Load<RecursoResource>(p);
            if (res != null)
            {
                var idx = _listaRecursos.AddItem(res.Nome, null, false);
                _listaRecursos.SetItemMetadata((int)idx, p);
            }
        }

        if (paths.Count == 0)
            MostrarFeedback("Nenhum recurso encontrado. Crie um novo!", new Color(0.6f, 0.6f, 0.6f));
    }

    private void OnItemSelected(long index)
    {
        if (index < 0) return;

        var meta = _listaRecursos.GetItemMetadata((int)index);
        if (meta.Obj == null) return;

        _atualPath = meta.AsString();
        _atual = ResourceLoader.Load<RecursoResource>(_atualPath);

        if (_atual == null)
        {
            MostrarFeedback("Erro ao carregar recurso!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        _nomeEdit.Text = _atual.Nome;
        _tipoEdit.Text = _atual.Tipo;
        _tempo1Spin.Value = _atual.TempoFase1Para2;
        _tempo2Spin.Value = _atual.TempoFase2Para3;
        _tempo3Spin.Value = _atual.TempoFase3Para4;
        _tempo4Spin.Value = _atual.TempoFase4Para5;
        _itemIdSpin.Value = _atual.ItemDropID;
        _ferramentaSelecionada = _atual.FerramentaNecessaria;
        _ferramentaPathEdit.Text = _ferramentaSelecionada != null
            ? $"{_ferramentaSelecionada.Nome} (ID: {_ferramentaSelecionada.ItemID})"
            : "Nenhuma";
        _qtdMinSpin.Value = _atual.QuantidadeMinima;
        _qtdMaxSpin.Value = _atual.QuantidadeMaxima;

        _texFase1 = _atual.TexturaFase1;
        _texFase2 = _atual.TexturaFase2;
        _texFase3 = _atual.TexturaFase3;
        _texFase4 = _atual.TexturaFase4;
        _texFase5 = _atual.TexturaFase5;
        _previewFase1.Texture = _texFase1;
        _previewFase2.Texture = _texFase2;
        _previewFase3.Texture = _texFase3;
        _previewFase4.Texture = _texFase4;
        _previewFase5.Texture = _texFase5;
    }

    private void OnBrowseFerramenta()
    {
        var dialog = new Godot.FileDialog
        {
            FileMode = Godot.FileDialog.FileModeEnum.OpenFile,
            Filters = new[] { "*.tres ; Item Resources" },
            CurrentDir = "res://Itens/"
        };
        dialog.FileSelected += (path) =>
        {
            var item = ResourceLoader.Load<ItemResource>(path);
            if (item != null)
            {
                _ferramentaSelecionada = item;
                _ferramentaPathEdit.Text = $"{item.Nome} (ID: {item.ItemID})";
            }
            dialog.QueueFree();
        };
        dialog.CloseRequested += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(600, 400));
    }

    private void OnNovo()
    {
        _atual = new RecursoResource();
        _atualPath = null;

        _nomeEdit.Text = "";
        _tipoEdit.Text = "";
        _tempo1Spin.Value = 30;
        _tempo2Spin.Value = 30;
        _tempo3Spin.Value = 30;
        _tempo4Spin.Value = 30;
        _itemIdSpin.Value = 0;
        _ferramentaSelecionada = null;
        _ferramentaPathEdit.Text = "Nenhuma";
        _qtdMinSpin.Value = 1;
        _qtdMaxSpin.Value = 1;

        _texFase1 = null;
        _texFase2 = null;
        _texFase3 = null;
        _texFase4 = null;
        _texFase5 = null;
        _previewFase1.Texture = null;
        _previewFase2.Texture = null;
        _previewFase3.Texture = null;
        _previewFase4.Texture = null;
        _previewFase5.Texture = null;

        MostrarFeedback("Novo recurso. Preencha os campos e salve.", new Color(0.3f, 0.8f, 0.4f));
    }

    private void OnExcluir()
    {
        if (string.IsNullOrEmpty(_atualPath))
        {
            MostrarFeedback("Nenhum recurso selecionado para excluir.", new Color(0.9f, 0.6f, 0.0f));
            return;
        }
        var dir = DirAccess.Open("res://");
        if (dir != null)
        {
            var err = dir.Remove(_atualPath);
            _atual = null;
            _atualPath = null;
            OnNovo();
            CarregarLista();
            MostrarFeedback(err == Error.Ok ? "Recurso excluido!" : "Erro ao excluir.", err == Error.Ok ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.3f));
        }
    }

    private void OnSalvar()
    {
        if (_atual == null)
        {
            MostrarFeedback("Clique em '+ Novo Recurso' primeiro.", new Color(0.9f, 0.6f, 0.0f));
            return;
        }

        if (string.IsNullOrWhiteSpace(_nomeEdit.Text))
        {
            MostrarFeedback("Preencha o nome do recurso!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        _atual.Nome = _nomeEdit.Text;
        _atual.Tipo = _tipoEdit.Text;
        _atual.TempoFase1Para2 = (float)_tempo1Spin.Value;
        _atual.TempoFase2Para3 = (float)_tempo2Spin.Value;
        _atual.TempoFase3Para4 = (float)_tempo3Spin.Value;
        _atual.TempoFase4Para5 = (float)_tempo4Spin.Value;
        _atual.ItemDropID = (int)_itemIdSpin.Value;
        _atual.FerramentaNecessaria = _ferramentaSelecionada;
        _atual.QuantidadeMinima = (int)_qtdMinSpin.Value;
        _atual.QuantidadeMaxima = (int)_qtdMaxSpin.Value;
        _atual.TexturaFase1 = _texFase1;
        _atual.TexturaFase2 = _texFase2;
        _atual.TexturaFase3 = _texFase3;
        _atual.TexturaFase4 = _texFase4;
        _atual.TexturaFase5 = _texFase5;

        if (string.IsNullOrEmpty(_atualPath))
        {
            string nomeArquivo = _atual.Nome.Replace(" ", "") + ".tres";
            _atualPath = DirRecursos + nomeArquivo;
        }

        var err = ResourceSaver.Save(_atual, _atualPath);
        if (err == Error.Ok)
        {
            _feedback.Text = "";
            MostrarFeedback("Recurso salvo com sucesso!", new Color(0.3f, 0.8f, 0.3f));
            CarregarLista();
            GD.Print($"[EditorRecursos] Salvo: {_atualPath}");
        }
        else
        {
            MostrarFeedback($"Erro ao salvar: {err}", new Color(0.9f, 0.3f, 0.3f));
            GD.PrintErr($"[EditorRecursos] Erro ao salvar: {err}");
        }
    }
}
