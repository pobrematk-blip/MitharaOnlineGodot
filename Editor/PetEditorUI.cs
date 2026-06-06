using Godot;
using System;
using System.Collections.Generic;

public partial class PetEditorUI : Control
{
    private Panel _panel;
    private ItemList _petList;
    private Label _previewStatus;

    private SpinBox _petIdSpin;
    private LineEdit _nomeEdit;
    private TextEdit _descricaoEdit;
    private TextureRect _iconePreview;
    private TextureRect _spritePreview;
    private OptionButton _tipoDropdown;
    private SpinBox _levelSpin;
    private SpinBox _hpSpin;
    private SpinBox _manaSpin;
    private SpinBox _attackDamageSpin;
    private SpinBox _forcaSpin;
    private SpinBox _agilidadeSpin;
    private SpinBox _destrezaSpin;
    private SpinBox _inteligenciaSpin;
    private SpinBox _speedSpin;
    private SpinBox _attackRangeSpin;
    private SpinBox _attackCooldownSpin;
    private SpinBox _coletaRangeSpin;
    private SpinBox _guardRangeSpin;

    private PetResource _currentPet;
    private string _currentPetPath;
    private bool _ignorarEventosUi;
    private static readonly string PetsDir = "res://Pets/";

    private readonly string[] _tiposPet = Enum.GetNames<TipoPet>();

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        var fechar = _panel.GetNode<Button>("Header/CloseButton");
        var header = _panel.GetNode<Control>("Header");
        fechar.Pressed += () => { _panel.Visible = false; };
        header.GuiInput += OnHeaderDrag;

        _petList = GetNode<ItemList>("%PetList");
        _previewStatus = GetNode<Label>("%PreviewStatus");

        _petIdSpin = GetNode<SpinBox>("%PetIdSpin");
        _nomeEdit = GetNode<LineEdit>("%NomeEdit");
        _descricaoEdit = GetNode<TextEdit>("%DescricaoEdit");
        _iconePreview = GetNode<TextureRect>("%IconePreview");
        _spritePreview = GetNode<TextureRect>("%SpritePreview");
        _tipoDropdown = GetNode<OptionButton>("%TipoDropdown");
        _levelSpin = GetNode<SpinBox>("%LevelSpin");
        _hpSpin = GetNode<SpinBox>("%HpSpin");
        _manaSpin = GetNode<SpinBox>("%ManaSpin");
        _attackDamageSpin = GetNode<SpinBox>("%AttackDamageSpin");
        _forcaSpin = GetNode<SpinBox>("%ForcaSpin");
        _agilidadeSpin = GetNode<SpinBox>("%AgilidadeSpin");
        _destrezaSpin = GetNode<SpinBox>("%DestrezaSpin");
        _inteligenciaSpin = GetNode<SpinBox>("%InteligenciaSpin");
        _speedSpin = GetNode<SpinBox>("%SpeedSpin");
        _attackRangeSpin = GetNode<SpinBox>("%AttackRangeSpin");
        _attackCooldownSpin = GetNode<SpinBox>("%AttackCooldownSpin");
        _coletaRangeSpin = GetNode<SpinBox>("%ColetaRangeSpin");
        _guardRangeSpin = GetNode<SpinBox>("%GuardRangeSpin");

        GetNode<Button>("%BtnBrowseIcone").Pressed += OnBrowseIcone;
        GetNode<Button>("%BtnBrowseSprite").Pressed += OnBrowseSprite;
        GetNode<Button>("%BtnSalvar").Pressed += OnSalvar;
        GetNode<Button>("%BtnAddPet").Pressed += OnAddPet;
        GetNode<Button>("%BtnRemovePet").Pressed += OnRemovePet;
        GetNode<Button>("%BtnReload").Pressed += OnReload;

        _petList.ItemSelected += OnPetSelected;

        foreach (var tipo in _tiposPet)
            _tipoDropdown.AddItem(tipo);

        var formFields = new Control[]
        {
            _petIdSpin, _nomeEdit, _levelSpin, _hpSpin, _manaSpin,
            _attackDamageSpin, _forcaSpin, _agilidadeSpin, _destrezaSpin,
            _inteligenciaSpin, _speedSpin, _attackRangeSpin, _attackCooldownSpin,
            _coletaRangeSpin, _guardRangeSpin
        };
        foreach (var f in formFields)
        {
            if (f is SpinBox sb) sb.ValueChanged += _ => OnFormDirty();
            else if (f is LineEdit le) le.TextChanged += _ => OnFormDirty();
        }
        _tipoDropdown.ItemSelected += _ => OnFormDirty();
        _descricaoEdit.TextChanged += OnFormDirty;

        _panel.Visible = false;
        CallDeferred(nameof(CarregarListaPets));
    }

    public void Mostrar() => _panel.Visible = true;
    public void Esconder() => _panel.Visible = false;
    public bool EstaVisivel => _panel?.Visible ?? false;

    private void OnHeaderDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            GetViewport().SetInputAsHandled();
    }

    private void CarregarListaPets()
    {
        _petList.Clear();
        _currentPet = null;
        _currentPetPath = null;

        if (!DirAccess.DirExistsAbsolute(PetsDir))
        {
            _previewStatus.Text = "Pasta Pets/ nao encontrada.";
            return;
        }

        var dir = DirAccess.Open(PetsDir);
        if (dir == null) return;

        var paths = new List<string>();
        dir.ListDirBegin();
        string fileName;
        while ((fileName = dir.GetNext()) != "")
        {
            if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                paths.Add(fileName);
        }
        dir.ListDirEnd();
        paths.Sort();

        foreach (var p in paths)
            _petList.AddItem(p.Replace(".tres", "").Replace(".res", ""));

        _previewStatus.Text = $"Encontrados {paths.Count} pet(ns). Selecione um.";
    }

    private void OnPetSelected(long index)
    {
        if (_ignorarEventosUi) return;
        if (index < 0 || index >= _petList.ItemCount) return;

        string name = _petList.GetItemText((int)index);
        string path = PetsDir + name + ".tres";
        if (!ResourceLoader.Exists(path))
        {
            path = PetsDir + name + ".res";
            if (!ResourceLoader.Exists(path)) return;
        }

        var pet = ResourceLoader.Load<PetResource>(path);
        if (pet == null)
        {
            _previewStatus.Text = "Erro ao carregar pet.";
            return;
        }

        _currentPet = pet;
        _currentPetPath = path;
        CarregarPetNoFormulario();
    }

    private void CarregarPetNoFormulario()
    {
        if (_currentPet == null) return;
        _ignorarEventosUi = true;

        _petIdSpin.Value = _currentPet.PetID;
        _nomeEdit.Text = _currentPet.Nome ?? "";
        _descricaoEdit.Text = _currentPet.Descricao ?? "";
        _tipoDropdown.Select((int)_currentPet.Tipo);
        _levelSpin.Value = _currentPet.Level;
        _hpSpin.Value = _currentPet.HP;
        _manaSpin.Value = _currentPet.Mana;
        _attackDamageSpin.Value = _currentPet.AttackDamage;
        _forcaSpin.Value = _currentPet.Forca;
        _agilidadeSpin.Value = _currentPet.Agilidade;
        _destrezaSpin.Value = _currentPet.Destreza;
        _inteligenciaSpin.Value = _currentPet.Inteligencia;
        _speedSpin.Value = (double)_currentPet.Speed;
        _attackRangeSpin.Value = (double)_currentPet.AttackRange;
        _attackCooldownSpin.Value = (double)_currentPet.AttackCooldown;
        _coletaRangeSpin.Value = (double)_currentPet.ColetaRange;
        _guardRangeSpin.Value = (double)_currentPet.GuardRange;

        if (_iconePreview != null)
            _iconePreview.Texture = _currentPet.Icone;
        if (_spritePreview != null)
            _spritePreview.Texture = _currentPet.SpriteAtlas;

        _ignorarEventosUi = false;
        _previewStatus.Text = $"Editando: {_currentPet.Nome} (ID: {_currentPet.PetID})";
    }

    private void LimparFormulario()
    {
        _ignorarEventosUi = true;
        _currentPet = null;
        _currentPetPath = null;

        _petIdSpin.Value = 0;
        _nomeEdit.Text = "";
        _descricaoEdit.Text = "";
        _tipoDropdown.Select(0);
        _levelSpin.Value = 1;
        _hpSpin.Value = 50;
        _manaSpin.Value = 20;
        _attackDamageSpin.Value = 5;
        _forcaSpin.Value = 1;
        _agilidadeSpin.Value = 1;
        _destrezaSpin.Value = 1;
        _inteligenciaSpin.Value = 1;
        _speedSpin.Value = 200;
        _attackRangeSpin.Value = 40;
        _attackCooldownSpin.Value = 1.5;
        _coletaRangeSpin.Value = 150;
        _guardRangeSpin.Value = 200;
        if (_iconePreview != null)
            _iconePreview.Texture = null;
        if (_spritePreview != null)
            _spritePreview.Texture = null;

        _ignorarEventosUi = false;
        _previewStatus.Text = "Nenhum pet selecionado.";
    }

    private void OnFormDirty()
    {
        if (_ignorarEventosUi || _currentPet == null) return;
        AplicarFormularioAoPet();
    }

    private void AplicarFormularioAoPet()
    {
        if (_currentPet == null) return;

        _currentPet.PetID = (int)_petIdSpin.Value;
        _currentPet.Nome = _nomeEdit.Text.Trim();
        _currentPet.Descricao = _descricaoEdit.Text.Trim();
        _currentPet.Tipo = (TipoPet)_tipoDropdown.Selected;
        _currentPet.Level = (int)_levelSpin.Value;
        _currentPet.HP = (int)_hpSpin.Value;
        _currentPet.Mana = (int)_manaSpin.Value;
        _currentPet.AttackDamage = (int)_attackDamageSpin.Value;
        _currentPet.Forca = (int)_forcaSpin.Value;
        _currentPet.Agilidade = (int)_agilidadeSpin.Value;
        _currentPet.Destreza = (int)_destrezaSpin.Value;
        _currentPet.Inteligencia = (int)_inteligenciaSpin.Value;
        _currentPet.Speed = (float)_speedSpin.Value;
        _currentPet.AttackRange = (float)_attackRangeSpin.Value;
        _currentPet.AttackCooldown = (float)_attackCooldownSpin.Value;
        _currentPet.ColetaRange = (float)_coletaRangeSpin.Value;
        _currentPet.GuardRange = (float)_guardRangeSpin.Value;

        _previewStatus.Text = $"Editando: {_currentPet.Nome} (ID: {_currentPet.PetID})";
    }

    private void OnBrowseIcone()
    {
        if (_currentPet == null) return;
        var dialog = new FileDialog();
        dialog.Title = "Selecionar Icone do Pet";
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        dialog.CurrentDir = "res://";

        dialog.FileSelected += path =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                _currentPet.Icone = tex;
                if (_iconePreview != null)
                    _iconePreview.Texture = tex;
            }
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private void OnBrowseSprite()
    {
        if (_currentPet == null) return;
        var dialog = new FileDialog();
        dialog.Title = "Selecionar Sprite do Pet";
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        dialog.CurrentDir = "res://";

        dialog.FileSelected += path =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                _currentPet.SpriteAtlas = tex;
                if (_spritePreview != null)
                    _spritePreview.Texture = tex;
            }
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private void OnAddPet()
    {
        var pet = new PetResource();
        pet.Nome = "Pet Novo";

        string basePath = PetsDir + "PetNovo.tres";
        string path = basePath;
        int counter = 1;
        while (ResourceLoader.Exists(path))
        {
            path = PetsDir + $"PetNovo{counter}.tres";
            counter++;
        }

        var err = ResourceSaver.Save(pet, path);
        if (err != Error.Ok)
        {
            _previewStatus.Text = $"Erro ao criar pet: {err}";
            return;
        }

        _currentPet = pet;
        _currentPetPath = path;
        CarregarPetNoFormulario();
        CarregarListaPets();
        _previewStatus.Text = $"Pet criado em {path}. Preencha os dados e salve.";
    }

    private void OnRemovePet()
    {
        if (_currentPet == null || string.IsNullOrEmpty(_currentPetPath)) return;

        var dir = DirAccess.Open(PetsDir);
        if (dir != null)
        {
            string fileName = _currentPetPath.Replace(PetsDir, "");
            dir.Remove(fileName);
        }

        LimparFormulario();
        CarregarListaPets();
    }

    private void OnSalvar()
    {
        AplicarFormularioAoPet();
        if (_currentPet == null || string.IsNullOrEmpty(_currentPetPath))
        {
            _previewStatus.Text = "Nenhum pet carregado para salvar.";
            return;
        }

        var err = ResourceSaver.Save(_currentPet, _currentPetPath);
        if (err == Error.Ok)
        {
            _previewStatus.Text = $"Salvo em {_currentPetPath}";
        }
        else
        {
            _previewStatus.Text = $"Erro ao salvar: {err}";
        }
    }

    private void OnReload()
    {
        LimparFormulario();
        CarregarListaPets();
    }
}
