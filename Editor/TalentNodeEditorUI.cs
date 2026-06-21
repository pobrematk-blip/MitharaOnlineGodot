using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TalentNodeEditorUI : Control
{
    private Panel _panel;
    private ItemList _treeList;
    private ItemList _nodeList;
    private Label _previewStatus;
    private Label _bonusPreview;

    private LineEdit _nodeIdEdit;
    private LineEdit _nomeEdit;
    private TextEdit _descricaoEdit;
    private TextureRect _iconePreview;
    private OptionButton _nodeTypeOpcao;
    private LineEdit _requisitosEdit;
    private SpinBox _custoSpin;
    private SpinBox _nivelSpin;
    private SpinBox _forcaSpin;
    private SpinBox _agilidadeSpin;
    private SpinBox _destrezaSpin;
    private SpinBox _inteligenciaSpin;
    private SpinBox _danoSpin;
    private SpinBox _velocidadeSpin;
    private SpinBox _vidaSpin;
    private SpinBox _manaSpin;

    private Control _skillSection;
    private Label _skillNomeLabel;
    private TextureRect _skillIcone;
    private Button _btnBrowseSkill;
    private Button _btnClearSkill;
    private Control _atributosSection;
    private Control _percentuaisSection;

    private TalentTreeResource _currentTree;
    private TalentNodeResource _currentNode;
    private string _currentTreePath;
    private bool _ignorarEventosUi;
    private static readonly string ArvoresDir = "res://skills/ArvoresClasses/";

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        var fechar = _panel.GetNode<Button>("Header/CloseButton");
        var header = _panel.GetNode<Control>("Header");
        fechar.Pressed += () => { _panel.Visible = false; };
        header.GuiInput += OnHeaderDrag;

        _treeList = GetNode<ItemList>("%TreeList");
        _nodeList = GetNode<ItemList>("%NodeList");
        _previewStatus = GetNode<Label>("%PreviewStatus");
        _bonusPreview = GetNode<Label>("%BonusPreview");

        _nodeIdEdit = GetNode<LineEdit>("%NodeIdEdit");
        _nomeEdit = GetNode<LineEdit>("%NomeEdit");
        _descricaoEdit = GetNode<TextEdit>("%DescricaoEdit");
        _iconePreview = GetNode<TextureRect>("%IconePreview");
        _nodeTypeOpcao = GetNode<OptionButton>("%NodeTypeOpcao");
        _requisitosEdit = GetNode<LineEdit>("%RequisitosEdit");

        _custoSpin = GetNode<SpinBox>("%CustoSpin");
        _nivelSpin = GetNode<SpinBox>("%NivelSpin");
        _forcaSpin = GetNode<SpinBox>("%ForcaSpin");
        _agilidadeSpin = GetNode<SpinBox>("%AgilidadeSpin");
        _destrezaSpin = GetNode<SpinBox>("%DestrezaSpin");
        _inteligenciaSpin = GetNode<SpinBox>("%InteligenciaSpin");
        _danoSpin = GetNode<SpinBox>("%DanoSpin");
        _velocidadeSpin = GetNode<SpinBox>("%VelocidadeSpin");
        _vidaSpin = GetNode<SpinBox>("%VidaSpin");
        _manaSpin = GetNode<SpinBox>("%ManaSpin");

        _skillSection = GetNode<Control>("%SkillSection");
        _skillNomeLabel = GetNode<Label>("%SkillNomeLabel");
        _skillIcone = GetNode<TextureRect>("%SkillIcone");
        _btnBrowseSkill = GetNode<Button>("%BtnBrowseSkill");
        _btnClearSkill = GetNode<Button>("%BtnClearSkill");
        _atributosSection = GetNode<Control>("%AtributosSection");
        _percentuaisSection = GetNode<Control>("%PercentuaisSection");

        foreach (TalentNodeType t in Enum.GetValues<TalentNodeType>())
            _nodeTypeOpcao.AddItem(TalentNodeResource.ObterRotuloTipo(t), (int)t);

        GetNode<Button>("%BtnBrowseIcone").Pressed += OnBrowseIcone;
        GetNode<Button>("%BtnSalvar").Pressed += OnSalvar;
        GetNode<Button>("%BtnAddNode").Pressed += OnAddNode;
        GetNode<Button>("%BtnRemoveNode").Pressed += OnRemoveNode;
        GetNode<Button>("%BtnReloadTrees").Pressed += OnReloadTrees;

        _nodeList.ItemSelected += OnNodeSelected;
        _treeList.ItemSelected += OnTreeSelected;
        _nodeTypeOpcao.ItemSelected += OnNodeTypeChanged;
        _btnBrowseSkill.Pressed += OnBrowseSkill;
        _btnClearSkill.Pressed += OnClearSkill;

        var formFields = new Control[]
        {
            _nodeIdEdit, _nomeEdit, _descricaoEdit, _requisitosEdit,
            _custoSpin, _nivelSpin, _forcaSpin, _agilidadeSpin,
            _destrezaSpin, _inteligenciaSpin, _danoSpin, _velocidadeSpin,
            _vidaSpin, _manaSpin
        };
        foreach (var f in formFields)
        {
            if (f is LineEdit le) le.TextChanged += _ => OnFormDirty();
            else if (f is TextEdit te) te.TextChanged += () => OnFormDirty();
            else if (f is SpinBox sb) sb.ValueChanged += _ => OnFormDirty();
        }

        _panel.Visible = false;
        EnsureInputAction();
        CallDeferred(nameof(CarregarListaArvores));
    }

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction("talent_node_editor")) return;
        InputMap.AddAction("talent_node_editor");
        InputMap.ActionAddEvent("talent_node_editor", new InputEventKey { PhysicalKeycode = Key.F11, Keycode = Key.F11, Unicode = 0, Pressed = false });
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("talent_node_editor"))
            TogglePanelVisibility();
    }

    private void TogglePanelVisibility()
    {
        if (_panel == null) return;
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible)
            CarregarListaArvores();
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null || !_panel.Visible) return;
        if (@event is InputEventKey ek && ek.Pressed && !ek.Echo && ek.Keycode == Key.Escape)
        { _panel.Visible = false; GetViewport().SetInputAsHandled(); }
    }

    private void OnHeaderDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            GetViewport().SetInputAsHandled();
    }

    // ----- Tree management -----

    private void CarregarListaArvores()
    {
        _treeList.Clear();
        _currentTree = null;
        _currentTreePath = null;

        if (!DirAccess.DirExistsAbsolute(ArvoresDir))
        {
            _previewStatus.Text = "Pasta skills/ArvoresClasses/ n?o encontrada.";
            return;
        }

        var dir = DirAccess.Open(ArvoresDir);
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
        {
            _treeList.AddItem(p.Replace(".tres", "").Replace(".res", ""));
        }

        _previewStatus.Text = $"Encontradas {paths.Count} arvore(s). Selecione uma.";
    }

    private void OnTreeSelected(long index)
    {
        if (_ignorarEventosUi) return;
        if (index < 0 || index >= _treeList.ItemCount) return;

        string name = _treeList.GetItemText((int)index);
        string path = ArvoresDir + name + ".tres";
        if (!ResourceLoader.Exists(path))
        {
            path = ArvoresDir + name + ".res";
            if (!ResourceLoader.Exists(path)) return;
        }

        var tree = ResourceLoader.Load<TalentTreeResource>(path);
        if (tree == null)
        {
            _previewStatus.Text = "Erro ao carregar arvore.";
            return;
        }

        _currentTree = tree;
        _currentTreePath = path;
        _previewStatus.Text = $"Arvore: {tree.NomeArvore} ({tree.Nodes?.Length ?? 0} nos)";
        AtualizarNodeList();
        LimparFormulario();
    }

    private void AtualizarNodeList()
    {
        _nodeList.Clear();
        if (_currentTree?.Nodes == null) return;
        foreach (var node in _currentTree.Nodes)
        {
            if (node == null) continue;
            string rotulo = TalentNodeResource.ObterRotuloTipo(node.NodeType);
            string itemText = $"[{rotulo}] {node.NodeId} - {node.Nome}";
            _nodeList.AddItem(itemText);
            int idx = _nodeList.ItemCount - 1;
            var cor = TalentNodeResource.ObterCorTipo(node.NodeType);
            _nodeList.SetItemCustomFgColor(idx, cor);
        }
    }

    // ----- Node selection -----

    private void OnNodeSelected(long index)
    {
        if (_ignorarEventosUi) return;
        if (_currentTree?.Nodes == null || index < 0 || index >= _currentTree.Nodes.Length) return;

        _currentNode = _currentTree.Nodes[(int)index];
        CarregarNodeNoFormulario();
    }

    private void CarregarNodeNoFormulario()
    {
        if (_currentNode == null) return;

        _ignorarEventosUi = true;

        _nodeIdEdit.Text = _currentNode.NodeId ?? "";
        _nomeEdit.Text = _currentNode.Nome ?? "";
        _descricaoEdit.Text = _currentNode.Descricao ?? "";
        _nodeTypeOpcao.Select((int)_currentNode.NodeType);
        _requisitosEdit.Text = _currentNode.Requisitos != null ? string.Join(", ", _currentNode.Requisitos) : "";
        _custoSpin.Value = _currentNode.CustoPontos;
        _nivelSpin.Value = _currentNode.NivelMinimo;
        _forcaSpin.Value = _currentNode.BonusForca;
        _agilidadeSpin.Value = _currentNode.BonusAgilidade;
        _destrezaSpin.Value = _currentNode.BonusDestreza;
        _inteligenciaSpin.Value = _currentNode.BonusInteligencia;
        _danoSpin.Value = (double)(_currentNode.BonusDanoPercent * 100f);
        _velocidadeSpin.Value = (double)(_currentNode.BonusVelocidadePercent * 100f);
        _vidaSpin.Value = (double)(_currentNode.BonusVidaPercent * 100f);
        _manaSpin.Value = (double)(_currentNode.BonusManaPercent * 100f);

        if (_iconePreview != null)
            _iconePreview.Texture = _currentNode.Icone;

        CarregarSkillNoFormulario();

        _ignorarEventosUi = false;
        AtualizarVisibilidadeSecoes();
        AtualizarBonusPreview();
    }

    private void CarregarSkillNoFormulario()
    {
        var skill = _currentNode?.HabilidadeAtiva;
        if (skill != null)
        {
            _skillNomeLabel.Text = skill.Nome;
            _skillIcone.Texture = skill.Icone;
        }
        else
        {
            _skillNomeLabel.Text = "(nenhuma)";
            _skillIcone.Texture = null;
        }
    }

    private void LimparFormulario()
    {
        _ignorarEventosUi = true;
        _currentNode = null;

        _nodeIdEdit.Text = "";
        _nomeEdit.Text = "";
        _descricaoEdit.Text = "";
        _nodeTypeOpcao.Select(0);
        _requisitosEdit.Text = "";
        _custoSpin.Value = 1;
        _nivelSpin.Value = 1;
        _forcaSpin.Value = 0;
        _agilidadeSpin.Value = 0;
        _destrezaSpin.Value = 0;
        _inteligenciaSpin.Value = 0;
        _danoSpin.Value = 0;
        _velocidadeSpin.Value = 0;
        _vidaSpin.Value = 0;
        _manaSpin.Value = 0;

        if (_iconePreview != null)
            _iconePreview.Texture = null;

        _skillNomeLabel.Text = "(nenhuma)";
        _skillIcone.Texture = null;

        _ignorarEventosUi = false;
        AtualizarVisibilidadeSecoes();
        _bonusPreview.Text = "";
    }

    // ----- Form editing -----

    private void OnFormDirty()
    {
        if (_ignorarEventosUi || _currentNode == null) return;
        AplicarFormularioAoNode();
        AtualizarBonusPreview();
        AtualizarNodeList();
    }

    private void AplicarFormularioAoNode()
    {
        if (_currentNode == null) return;

        _currentNode.NodeId = _nodeIdEdit.Text.Trim();
        _currentNode.Nome = _nomeEdit.Text.Trim();
        _currentNode.Descricao = _descricaoEdit.Text;
        _currentNode.NodeType = (TalentNodeType)_nodeTypeOpcao.GetSelectedId();

        var reqs = _requisitosEdit.Text.Split(',')
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToArray();
        _currentNode.Requisitos = reqs;

        _currentNode.CustoPontos = (int)_custoSpin.Value;
        _currentNode.NivelMinimo = (int)_nivelSpin.Value;
        _currentNode.BonusForca = (float)_forcaSpin.Value;
        _currentNode.BonusAgilidade = (float)_agilidadeSpin.Value;
        _currentNode.BonusDestreza = (float)_destrezaSpin.Value;
        _currentNode.BonusInteligencia = (float)_inteligenciaSpin.Value;
        _currentNode.BonusDanoPercent = (float)(_danoSpin.Value / 100.0);
        _currentNode.BonusVelocidadePercent = (float)(_velocidadeSpin.Value / 100.0);
        _currentNode.BonusVidaPercent = (float)(_vidaSpin.Value / 100.0);
        _currentNode.BonusManaPercent = (float)(_manaSpin.Value / 100.0);

        _previewStatus.Text = $"Editando: {_currentNode.Nome} [{TalentNodeResource.ObterRotuloTipo(_currentNode.NodeType)}]";
    }

    private void AtualizarBonusPreview()
    {
        if (_currentNode == null) { _bonusPreview.Text = ""; return; }

        var partes = new List<string>();
        if (_currentNode.HabilidadeAtiva != null)
            partes.Add($"Skill: {_currentNode.HabilidadeAtiva.Nome}");
        if (_currentNode.BonusForca != 0) partes.Add($"Forca {_currentNode.BonusForca:+0.0;-0.0}");
        if (_currentNode.BonusAgilidade != 0) partes.Add($"Agilidade {_currentNode.BonusAgilidade:+0.0;-0.0}");
        if (_currentNode.BonusDestreza != 0) partes.Add($"Destreza {_currentNode.BonusDestreza:+0.0;-0.0}");
        if (_currentNode.BonusInteligencia != 0) partes.Add($"Inteligencia {_currentNode.BonusInteligencia:+0.0;-0.0}");
        if (_currentNode.BonusDanoPercent != 0) partes.Add($"Dano {_currentNode.BonusDanoPercent * 100:+#;-#}%");
        if (_currentNode.BonusVelocidadePercent != 0) partes.Add($"Velocidade {_currentNode.BonusVelocidadePercent * 100:+#;-#}%");
        if (_currentNode.BonusVidaPercent != 0) partes.Add($"Vida {_currentNode.BonusVidaPercent * 100:+#;-#}%");
        if (_currentNode.BonusManaPercent != 0) partes.Add($"Mana {_currentNode.BonusManaPercent * 100:+#;-#}%");

        _bonusPreview.Text = partes.Count > 0 ? string.Join(" | ", partes) : "(sem bonus)";
    }

    private void AtualizarVisibilidadeSecoes()
    {
        if (_currentNode == null)
        {
            _skillSection.Visible = false;
            _atributosSection.Visible = false;
            _percentuaisSection.Visible = false;
            return;
        }

        var tipo = _currentNode.NodeType;
        _skillSection.Visible = tipo == TalentNodeType.Skill || tipo == TalentNodeType.Status || tipo == TalentNodeType.Hybrid;
        _atributosSection.Visible = tipo == TalentNodeType.Attribute || tipo == TalentNodeType.Hybrid;
        _percentuaisSection.Visible = tipo == TalentNodeType.Passive || tipo == TalentNodeType.Hybrid;
    }

    private void OnNodeTypeChanged(long index)
    {
        if (_ignorarEventosUi || _currentNode == null) return;
        _currentNode.NodeType = (TalentNodeType)index;
        AtualizarVisibilidadeSecoes();
        AtualizarNodeList();
        AtualizarBonusPreview();
        _previewStatus.Text = $"Editando: {_currentNode.Nome} [{TalentNodeResource.ObterRotuloTipo(_currentNode.NodeType)}]";
    }

    // ----- Icon selection -----

    private void OnBrowseIcone()
    {
        var dialog = new FileDialog();
        dialog.Title = "Selecionar Icone do Talento";
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" };
        dialog.Access = FileDialog.AccessEnum.Resources;

        if (_currentNode?.Icone != null && !string.IsNullOrEmpty(_currentNode.Icone.ResourcePath))
            dialog.CurrentFile = _currentNode.Icone.ResourcePath;

        dialog.FileSelected += path =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                _currentNode.Icone = tex;
                if (_iconePreview != null)
                    _iconePreview.Texture = tex;
            }
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    // ----- Skill selection -----

    private void OnBrowseSkill()
    {
        var dialog = new FileDialog();
        dialog.Title = "Selecionar Skill";
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new[] { "*.tres ; Recursos de Skill" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        dialog.CurrentDir = "res://skills/habilidades";

        dialog.FileSelected += path =>
        {
            var skill = ResourceLoader.Load<SkillResource>(path);
            if (skill != null && _currentNode != null)
            {
                _currentNode.HabilidadeAtiva = skill;
                _skillNomeLabel.Text = skill.Nome;
                _skillIcone.Texture = skill.Icone;
                AtualizarBonusPreview();
            }
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private void OnClearSkill()
    {
        if (_currentNode == null) return;
        _currentNode.HabilidadeAtiva = null;
        _skillNomeLabel.Text = "(nenhuma)";
        _skillIcone.Texture = null;
        AtualizarBonusPreview();
    }

    // ----- Add / Remove nodes -----

    private void OnAddNode()
    {
        if (_currentTree == null) return;

        var node = new TalentNodeResource();
        node.NodeId = "novo_no_" + GD.Randi() % 10000;
        node.Nome = "Novo Talento";
        node.Descricao = "Descri??o do talento.";
        node.CustoPontos = 1;
        node.NivelMinimo = 1;

        var list = _currentTree.Nodes?.ToList() ?? new List<TalentNodeResource>();
        list.Add(node);
        _currentTree.Nodes = list.ToArray();

        AtualizarNodeList();
        _nodeList.Select(_nodeList.ItemCount - 1);
        OnNodeSelected(_nodeList.ItemCount - 1);
        _previewStatus.Text = $"No adicionado. Salve para persistir.";
    }

    private void OnRemoveNode()
    {
        if (_currentTree?.Nodes == null) return;
        var sel = _nodeList.GetSelectedItems();
        if (sel == null || sel.Length == 0) return;

        int idx = sel[0];
        if (idx < 0 || idx >= _currentTree.Nodes.Length) return;

        var removedId = _currentTree.Nodes[idx].NodeId;

        var list = _currentTree.Nodes.ToList();
        list.RemoveAt(idx);
        _currentTree.Nodes = list.ToArray();

        foreach (var node in _currentTree.Nodes)
        {
            if (node?.Requisitos != null)
                node.Requisitos = node.Requisitos.Where(r => r != removedId).ToArray();
        }

        LimparFormulario();
        AtualizarNodeList();
        _previewStatus.Text = $"No '{removedId}' removido.";
    }

    // ----- Save -----

    private void OnSalvar()
    {
        if (_currentNode != null)
            AplicarFormularioAoNode();

        if (_currentTree == null || string.IsNullOrEmpty(_currentTreePath))
        {
            _previewStatus.Text = "Nenhuma arvore carregada para salvar.";
            return;
        }

        var err = ResourceSaver.Save(_currentTree, _currentTreePath);
        if (err == Error.Ok)
        {
            _previewStatus.Text = $"Salvo em {_currentTreePath}";
        }
        else
        {
            _previewStatus.Text = $"Erro ao salvar: {err}";
        }
    }

    private void OnReloadTrees()
    {
        CarregarListaArvores();
        LimparFormulario();
    }
}
