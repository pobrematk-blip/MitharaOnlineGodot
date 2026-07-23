using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class SkillEditorUI : Control
{
    private Panel _panel;
    private ItemList _skillList;
    private LineEdit _searchEdit;
    private Label _statusLabel;
    private TextureRect _iconPreview;

    private SpinBox _skillIdSpin;
    private LineEdit _nomeEdit;
    private TextEdit _descricaoEdit;
    private LineEdit _classeEdit;
    private LineEdit _especializacaoEdit;
    private SpinBox _manaSpin;
    private SpinBox _cooldownSpin;
    private SpinBox _duracaoSpin;
    private SpinBox _nivelSpin;
    private LineEdit _efeitoAlvoEdit;
    private LineEdit _efeitoPlayerEdit;
    private LineEdit _projetilEdit;
    private LineEdit _lancamentoEdit;

    private readonly List<string> _skillPaths = new();
    private SkillResource _currentSkill;
    private string _currentSkillPath = "";
    private bool _loading;

    private const string SkillsDir = "res://skills/habilidades";
    private const string IconesDir = "res://skills/Incone Skills";
    private const string AnimacoesDir = "res://skills";

    public override void _Ready()
    {
        BuildUi();
        _panel.Visible = false;
        CallDeferred(nameof(CarregarListaSkills));
    }

    private void BuildUi()
    {
        _panel = new Panel { Name = "Panel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_panel);

        var root = new VBoxContainer { Name = "Root" };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 8;
        root.OffsetTop = 8;
        root.OffsetRight = -8;
        root.OffsetBottom = -8;
        _panel.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);
        header.AddChild(new Label
        {
            Text = "Editor de Skills",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        var reloadButton = new Button { Text = "Recarregar" };
        reloadButton.Pressed += CarregarListaSkills;
        header.AddChild(reloadButton);

        var saveButton = new Button { Text = "Salvar Skill" };
        saveButton.Pressed += OnSalvar;
        header.AddChild(saveButton);

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(body);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        body.AddChild(left);

        _searchEdit = new LineEdit { PlaceholderText = "Buscar skill..." };
        _searchEdit.TextChanged += _ => AtualizarListaFiltrada();
        left.AddChild(_searchEdit);

        _skillList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _skillList.ItemSelected += OnSkillSelected;
        left.AddChild(_skillList);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(scroll);

        var form = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        scroll.AddChild(form);

        _statusLabel = new Label
        {
            Text = "Selecione uma skill.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        form.AddChild(_statusLabel);

        var iconRow = new HBoxContainer();
        form.AddChild(iconRow);
        _iconPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(72, 72),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        iconRow.AddChild(_iconPreview);

        var iconButtons = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        iconRow.AddChild(iconButtons);
        var iconButton = new Button { Text = "Trocar icone da skill" };
        iconButton.Pressed += OnBrowseIcone;
        iconButtons.AddChild(iconButton);
        var clearIconButton = new Button { Text = "Limpar icone" };
        clearIconButton.Pressed += () =>
        {
            if (_currentSkill == null) return;
            _currentSkill.Icone = null;
            _iconPreview.Texture = null;
        };
        iconButtons.AddChild(clearIconButton);

        _skillIdSpin = CriarSpin(form, "Skill ID", 0, 999999, 1);
        _nomeEdit = CriarLineEdit(form, "Nome");
        _descricaoEdit = CriarTextEdit(form, "Descricao", 86);
        _classeEdit = CriarLineEdit(form, "Classe restrita");
        _especializacaoEdit = CriarLineEdit(form, "Especializacao");
        _manaSpin = CriarSpin(form, "Custo de mana", 0, 9999, 1);
        _cooldownSpin = CriarSpin(form, "Cooldown", 0, 9999, 0.1);
        _duracaoSpin = CriarSpin(form, "Duracao", 0, 9999, 0.1);
        _nivelSpin = CriarSpin(form, "Nivel requerido", 1, 999, 1);

        AdicionarSeparador(form, "Cenas e efeitos");
        _efeitoAlvoEdit = CriarPathRow(form, "Cena de efeito no alvo", "Escolher cena de efeito no alvo");
        _efeitoPlayerEdit = CriarPathRow(form, "Cena de efeito no player", "Escolher cena de efeito no player");
        _projetilEdit = CriarPathRow(form, "Cena de projetil", "Escolher cena de projetil");
        _lancamentoEdit = CriarPathRow(form, "Cena de lancamento", "Escolher cena de lancamento");
    }

    private static void AdicionarSeparador(VBoxContainer form, string text)
    {
        form.AddChild(new HSeparator());
        form.AddChild(new Label { Text = text });
    }

    private static LineEdit CriarLineEdit(VBoxContainer form, string label)
    {
        form.AddChild(new Label { Text = label });
        var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddChild(edit);
        return edit;
    }

    private static TextEdit CriarTextEdit(VBoxContainer form, string label, float height)
    {
        form.AddChild(new Label { Text = label });
        var edit = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        form.AddChild(edit);
        return edit;
    }

    private static SpinBox CriarSpin(VBoxContainer form, string label, double min, double max, double step)
    {
        form.AddChild(new Label { Text = label });
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        form.AddChild(spin);
        return spin;
    }

    private LineEdit CriarPathRow(VBoxContainer form, string label, string dialogTitle)
    {
        form.AddChild(new Label { Text = label });
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddChild(row);

        var edit = new LineEdit
        {
            PlaceholderText = "res://...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(edit);

        var browse = new Button { Text = "..." };
        browse.Pressed += () => OnBrowseScene(edit, dialogTitle);
        row.AddChild(browse);

        var clear = new Button { Text = "Limpar" };
        clear.Pressed += () => edit.Text = "";
        row.AddChild(clear);

        return edit;
    }

    private void CarregarListaSkills()
    {
        _skillPaths.Clear();
        string globalDir = ProjectSettings.GlobalizePath(SkillsDir);
        if (!Directory.Exists(globalDir))
        {
            _statusLabel.Text = "Pasta de skills nao encontrada.";
            return;
        }

        foreach (string file in Directory.GetFiles(globalDir, "*.tres", SearchOption.AllDirectories))
            _skillPaths.Add(ToResourcePath(file));
        foreach (string file in Directory.GetFiles(globalDir, "*.res", SearchOption.AllDirectories))
            _skillPaths.Add(ToResourcePath(file));

        _skillPaths.Sort(StringComparer.OrdinalIgnoreCase);
        AtualizarListaFiltrada();
        _statusLabel.Text = $"Encontradas {_skillPaths.Count} skill(s).";
    }

    private void AtualizarListaFiltrada()
    {
        string filtro = (_searchEdit?.Text ?? "").Trim();
        _skillList.Clear();

        foreach (string path in _skillPaths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(filtro)
                && !name.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                continue;

            _skillList.AddItem(name);
            int index = _skillList.ItemCount - 1;
            _skillList.SetItemMetadata(index, path);
            if (ResourceLoader.Exists(path))
            {
                var skill = ResourceLoader.Load<SkillResource>(path);
                if (skill?.Icone != null)
                    _skillList.SetItemIcon(index, skill.Icone);
            }
        }
    }

    private void OnSkillSelected(long index)
    {
        if (index < 0 || index >= _skillList.ItemCount)
            return;

        string path = _skillList.GetItemMetadata((int)index).AsString();
        var skill = ResourceLoader.Load<SkillResource>(path);
        if (skill == null)
        {
            _statusLabel.Text = $"Falha ao carregar {path}";
            return;
        }

        _currentSkill = skill;
        _currentSkillPath = path;
        CarregarSkillNoFormulario();
    }

    private void CarregarSkillNoFormulario()
    {
        if (_currentSkill == null)
            return;

        _loading = true;
        _skillIdSpin.Value = _currentSkill.SkillId;
        _nomeEdit.Text = _currentSkill.Nome ?? "";
        _descricaoEdit.Text = _currentSkill.Descricao ?? "";
        _classeEdit.Text = _currentSkill.ClasseRestrita ?? "";
        _especializacaoEdit.Text = _currentSkill.Especializacao ?? "";
        _manaSpin.Value = _currentSkill.CustoMana;
        _cooldownSpin.Value = _currentSkill.Cooldown;
        _duracaoSpin.Value = _currentSkill.Duracao;
        _nivelSpin.Value = _currentSkill.NivelRequerido;
        _efeitoAlvoEdit.Text = _currentSkill.TargetEffectScenePath ?? "";
        _efeitoPlayerEdit.Text = _currentSkill.CenaEfeitoNoPlayer ?? "";
        _projetilEdit.Text = _currentSkill.CenaProjetil ?? "";
        _lancamentoEdit.Text = _currentSkill.CenaLancamento ?? "";
        _iconPreview.Texture = _currentSkill.Icone;
        _statusLabel.Text = $"Editando: {_currentSkill.Nome} ({_currentSkillPath})";
        _loading = false;
    }

    private void AplicarFormulario()
    {
        if (_currentSkill == null || _loading)
            return;

        _currentSkill.SkillId = (int)_skillIdSpin.Value;
        _currentSkill.Nome = _nomeEdit.Text.Trim();
        _currentSkill.Descricao = _descricaoEdit.Text;
        _currentSkill.ClasseRestrita = _classeEdit.Text.Trim();
        _currentSkill.Especializacao = _especializacaoEdit.Text.Trim();
        _currentSkill.CustoMana = (int)_manaSpin.Value;
        _currentSkill.Cooldown = (float)_cooldownSpin.Value;
        _currentSkill.Duracao = (float)_duracaoSpin.Value;
        _currentSkill.NivelRequerido = (int)_nivelSpin.Value;
        _currentSkill.TargetEffectScenePath = NormalizarResPath(_efeitoAlvoEdit.Text);
        _currentSkill.CenaEfeitoNoPlayer = NormalizarResPath(_efeitoPlayerEdit.Text);
        _currentSkill.CenaProjetil = NormalizarResPath(_projetilEdit.Text);
        _currentSkill.CenaLancamento = NormalizarResPath(_lancamentoEdit.Text);
    }

    private void OnBrowseIcone()
    {
        if (_currentSkill == null)
        {
            _statusLabel.Text = "Selecione uma skill antes de trocar o icone.";
            return;
        }

        var dialog = CriarDialogoArquivo("Selecionar icone da skill", FileDialog.FileModeEnum.OpenFile);
        dialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" };
        dialog.CurrentDir = IconesDir;
        dialog.FileSelected += path =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex == null)
            {
                _statusLabel.Text = $"Nao foi possivel carregar o icone: {path}";
                return;
            }

            _currentSkill.Icone = tex;
            _iconPreview.Texture = tex;
            _statusLabel.Text = $"Icone definido: {path}";
        };
        dialog.PopupCentered(new Vector2I(760, 520));
    }

    private void OnBrowseScene(LineEdit target, string title)
    {
        if (_currentSkill == null)
        {
            _statusLabel.Text = "Selecione uma skill antes de escolher cenas.";
            return;
        }

        var dialog = CriarDialogoArquivo(title, FileDialog.FileModeEnum.OpenFile);
        dialog.Filters = new[] { "*.tscn,*.scn ; Cenas Godot" };
        dialog.CurrentDir = AnimacoesDir;
        dialog.FileSelected += path =>
        {
            target.Text = path;
            _statusLabel.Text = $"{title}: {path}";
        };
        dialog.PopupCentered(new Vector2I(820, 560));
    }

    private FileDialog CriarDialogoArquivo(string title, FileDialog.FileModeEnum mode)
    {
        var dialog = new FileDialog
        {
            Title = title,
            FileMode = mode,
            Access = FileDialog.AccessEnum.Resources,
        };
        AddChild(dialog);
        dialog.CloseRequested += dialog.QueueFree;
        dialog.FileSelected += _ => dialog.QueueFree();
        return dialog;
    }

    private void OnSalvar()
    {
        if (_currentSkill == null || string.IsNullOrWhiteSpace(_currentSkillPath))
        {
            _statusLabel.Text = "Nenhuma skill selecionada para salvar.";
            return;
        }

        AplicarFormulario();
        var err = ResourceSaver.Save(_currentSkill, _currentSkillPath);
        if (err == Error.Ok)
        {
            _statusLabel.Text = $"Skill salva: {_currentSkillPath}";
            AtualizarListaFiltrada();
        }
        else
        {
            _statusLabel.Text = $"Erro ao salvar skill: {err}";
        }
    }

    private static string ToResourcePath(string globalPath)
    {
        string root = ProjectSettings.GlobalizePath("res://");
        string relative = Path.GetRelativePath(root, globalPath).Replace('\\', '/');
        return "res://" + relative;
    }

    private static string NormalizarResPath(string value)
    {
        value = (value ?? "").Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(value))
            return "";
        if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            return value;
        if (Path.IsPathRooted(value))
            return ToResourcePath(value);
        return value;
    }
}
