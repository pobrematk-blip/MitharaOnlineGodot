using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class EditorLojaCashUI : Control
{
    private ItemList _listaItens;
    private SpinBox _itemIdSpin;
    private SpinBox _precoSpin;
    private LineEdit _nomeExibicaoEdit;
    private TextEdit _descricaoEdit;
    private Button _btnAdicionar;
    private Button _btnRemover;
    private Button _btnSalvar;
    private Label _feedback;

    private LojaCashData _lojaData;
    private static readonly string LojaDataPath = "res://SistemaContas/LojaCashData.tres";
    private int _selectedIndex = -1;

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

        leftPanel.AddChild(new Label { Text = "Itens da Loja Premium" });

        _listaItens = new ItemList();
        _listaItens.SizeFlagsVertical = SizeFlags.ExpandFill;
        leftPanel.AddChild(_listaItens);

        _btnRemover = new Button { Text = "- Remover Item" };
        _btnRemover.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftPanel.AddChild(_btnRemover);

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
            form.AddChild(new Label { Text = text });
        }

        AddLabel("Item ID");
        _itemIdSpin = new SpinBox { MaxValue = 9999, Value = 0 };
        form.AddChild(_itemIdSpin);

        AddLabel("Preco em Diamantes");
        _precoSpin = new SpinBox { MaxValue = 99999, Value = 100 };
        form.AddChild(_precoSpin);

        AddLabel("Nome de Exibicao (opcional)");
        _nomeExibicaoEdit = new LineEdit();
        _nomeExibicaoEdit.PlaceholderText = "Deixe vazio para usar o nome do item";
        form.AddChild(_nomeExibicaoEdit);

        AddLabel("Descri??o (opcional)");
        _descricaoEdit = new TextEdit();
        _descricaoEdit.CustomMinimumSize = new Vector2(0, 60);
        _descricaoEdit.PlaceholderText = "Descri??o do item na loja";
        form.AddChild(_descricaoEdit);

        form.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _btnAdicionar = new Button { Text = "+ Adicionar / Atualizar Item" };
        _btnAdicionar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _btnAdicionar.AddThemeColorOverride("font_color", new Color(0, 0, 0));
        _btnAdicionar.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.3f, 0.8f, 0.3f) });
        form.AddChild(_btnAdicionar);

        _btnSalvar = new Button { Text = "Salvar Loja Cash" };
        _btnSalvar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _btnSalvar.AddThemeColorOverride("font_color", new Color(0, 0, 0));
        _btnSalvar.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.3f, 0.6f, 0.9f) });
        form.AddChild(_btnSalvar);

        _feedback = new Label();
        _feedback.HorizontalAlignment = HorizontalAlignment.Center;
        _feedback.AddThemeFontSizeOverride("font_size", 12);
        form.AddChild(_feedback);

        _btnAdicionar.Pressed += OnAdicionar;
        _btnRemover.Pressed += OnRemover;
        _btnSalvar.Pressed += OnSalvar;
        _listaItens.ItemSelected += OnItemSelected;

        CarregarDados();
        PopularLista();
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

    private void CarregarDados()
    {
        if (ResourceLoader.Exists(LojaDataPath))
            _lojaData = ResourceLoader.Load<LojaCashData>(LojaDataPath);

        _lojaData ??= new LojaCashData();
    }

    private void PopularLista()
    {
        _listaItens.Clear();
        var itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");

        for (int i = 0; i < _lojaData.Itens.Count; i++)
        {
            var entry = _lojaData.Itens[i];
            string nome = !string.IsNullOrEmpty(entry.NomeExibicao)
                ? entry.NomeExibicao
                : itemDB?.GetItemName(entry.ItemID) ?? $"Item #{entry.ItemID}";

            var idx = _listaItens.AddItem($"#{entry.ItemID} - {nome} (💎{entry.PrecoDiamantes})");
            _listaItens.SetItemMetadata(idx, i);
        }

        if (_lojaData.Itens.Count == 0)
            MostrarFeedback("Nenhum item na loja. Adicione um item acima.", new Color(0.6f, 0.6f, 0.6f));
    }

    private void OnItemSelected(long index)
    {
        var meta = _listaItens.GetItemMetadata((int)index);
        if (meta.VariantType == Variant.Type.Nil) return;
        _selectedIndex = meta.AsInt32();
        if (_selectedIndex < 0 || _selectedIndex >= _lojaData.Itens.Count) return;

        var entry = _lojaData.Itens[_selectedIndex];
        _itemIdSpin.Value = entry.ItemID;
        _precoSpin.Value = entry.PrecoDiamantes;
        _nomeExibicaoEdit.Text = entry.NomeExibicao;
        _descricaoEdit.Text = entry.Descricao;
    }

    private void OnAdicionar()
    {
        int itemId = (int)_itemIdSpin.Value;

        var existing = _lojaData.Itens.FirstOrDefault(e => e.ItemID == itemId);
        if (existing != null)
        {
            existing.PrecoDiamantes = (int)_precoSpin.Value;
            existing.NomeExibicao = _nomeExibicaoEdit.Text;
            existing.Descricao = _descricaoEdit.Text;
            MostrarFeedback($"Item #{itemId} atualizado!", new Color(0.3f, 0.8f, 0.4f));
        }
        else
        {
            var entry = new LojaCashEntry();
            entry.ItemID = itemId;
            entry.PrecoDiamantes = (int)_precoSpin.Value;
            entry.NomeExibicao = _nomeExibicaoEdit.Text;
            entry.Descricao = _descricaoEdit.Text;
            _lojaData.Itens.Add(entry);
            MostrarFeedback($"Item #{itemId} adicionado a loja!", new Color(0.3f, 0.8f, 0.4f));
        }

        PopularLista();
    }

    private void OnRemover()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _lojaData.Itens.Count)
        {
            MostrarFeedback("Selecione um item na lista para remover.", new Color(0.9f, 0.6f, 0.0f));
            return;
        }
        _lojaData.Itens.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        PopularLista();
        MostrarFeedback("Item removido da loja.", new Color(0.9f, 0.6f, 0.0f));
    }

    private void OnSalvar()
    {
        if (_lojaData.Itens.Count == 0)
        {
            MostrarFeedback("N?o h? itens para salvar. Adicione itens primeiro.", new Color(0.9f, 0.6f, 0.0f));
            return;
        }
        var err = ResourceSaver.Save(_lojaData, LojaDataPath);
        if (err == Error.Ok)
        {
            MostrarFeedback("Loja Cash salva com sucesso!", new Color(0.3f, 0.8f, 0.4f));
            GD.Print($"[EditorLojaCash] Salvo: {LojaDataPath}");
        }
        else
        {
            MostrarFeedback($"Erro ao salvar: {err}", new Color(0.9f, 0.3f, 0.3f));
            GD.PrintErr($"[EditorLojaCash] Erro ao salvar: {err}");
        }
    }
}
