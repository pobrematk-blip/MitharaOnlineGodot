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

    private LojaCashData _lojaData;
    private static readonly string LojaDataPath = "res://SistemaContas/LojaCashData.tres";
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        const int margin = 8;
        var panel = new Panel();
        panel.Visible = true;
        AddChild(panel);

        var hbox = new HBoxContainer();
        panel.AddChild(hbox);

        var leftPanel = new VBoxContainer();
        leftPanel.CustomMinimumSize = new Vector2(200, 0);
        hbox.AddChild(leftPanel);

        leftPanel.AddChild(new Label { Text = "Itens da Loja Premium" });

        _listaItens = new ItemList();
        _listaItens.SizeFlagsVertical = SizeFlags.ExpandFill;
        leftPanel.AddChild(_listaItens);

        _btnRemover = new Button { Text = "- Remover Item" };
        leftPanel.AddChild(_btnRemover);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(scroll);

        var form = new VBoxContainer();
        form.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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

        AddLabel("Descricao (opcional)");
        _descricaoEdit = new TextEdit();
        _descricaoEdit.CustomMinimumSize = new Vector2(0, 60);
        _descricaoEdit.PlaceholderText = "Descricao do item na loja";
        form.AddChild(_descricaoEdit);

        form.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _btnAdicionar = new Button { Text = "+ Adicionar / Atualizar Item" };
        _btnAdicionar.AddThemeColorOverride("font_color", new Color(0, 0, 0));
        _btnAdicionar.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.3f, 0.8f, 0.3f) });
        form.AddChild(_btnAdicionar);

        _btnSalvar = new Button { Text = "Salvar Loja Cash" };
        _btnSalvar.AddThemeColorOverride("font_color", new Color(0, 0, 0));
        _btnSalvar.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.3f, 0.6f, 0.9f) });
        form.AddChild(_btnSalvar);

        _btnAdicionar.Pressed += OnAdicionar;
        _btnRemover.Pressed += OnRemover;
        _btnSalvar.Pressed += OnSalvar;
        _listaItens.ItemSelected += OnItemSelected;

        CarregarDados();
        PopularLista();
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

        foreach (var entry in _lojaData.Itens)
        {
            string nome = !string.IsNullOrEmpty(entry.NomeExibicao)
                ? entry.NomeExibicao
                : itemDB?.GetItemName(entry.ItemID) ?? $"Item #{entry.ItemID}";

            _listaItens.AddItem($"#{entry.ItemID} - {nome} (💎{entry.PrecoDiamantes})");
        }
    }

    private void OnItemSelected(long index)
    {
        _selectedIndex = (int)index;
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
        }
        else
        {
            var entry = new LojaCashEntry();
            entry.ItemID = itemId;
            entry.PrecoDiamantes = (int)_precoSpin.Value;
            entry.NomeExibicao = _nomeExibicaoEdit.Text;
            entry.Descricao = _descricaoEdit.Text;
            _lojaData.Itens.Add(entry);
        }

        PopularLista();
    }

    private void OnRemover()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _lojaData.Itens.Count) return;
        _lojaData.Itens.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        PopularLista();
    }

    private void OnSalvar()
    {
        var err = ResourceSaver.Save(_lojaData, LojaDataPath);
        if (err == Error.Ok)
            GD.Print($"[EditorLojaCash] Salvo: {LojaDataPath}");
        else
            GD.PrintErr($"[EditorLojaCash] Erro ao salvar: {err}");
    }
}
