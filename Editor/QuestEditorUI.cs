using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public partial class QuestEditorUI : Control
{
    private const string QuestCatalogPath = "res://server/data/quests.json";
    private const string NpcDefaultFolder = "res://characters/Npcs/";

    private Panel _panel;
    private ItemList _questList;
    private Label _statusLabel;

    private SpinBox _idSpin;
    private LineEdit _nameEdit;
    private SpinBox _requiredLevelSpin;
    private LineEdit _npcSceneEdit;
    private TextEdit _descriptionEdit;
    private TextEdit _questTextEdit;
    private TextEdit _offerDialogEdit;
    private TextEdit _acceptedDialogEdit;
    private TextEdit _progressDialogEdit;
    private TextEdit _completeDialogEdit;
    private TextEdit _deliveryDialogEdit;
    private SpinBox _rewardXpSpin;
    private SpinBox _rewardGoldSpin;
    private OptionButton _objectiveTypeDropdown;
    private LineEdit _objectiveTargetEdit;
    private SpinBox _objectiveCountSpin;
    private SpinBox _objectiveXSpin;
    private SpinBox _objectiveYSpin;
    private SpinBox _objectiveRadiusSpin;
    private ItemList _objectiveList;
    private SpinBox _rewardItemIdSpin;
    private SpinBox _rewardItemQuantitySpin;
    private ItemList _rewardItemList;

    private readonly List<QuestEditorDefinition> _quests = new();
    private int _selectedIndex = -1;
    private bool _loading;

    public override void _Ready()
    {
        BuildUi();
        _panel.Visible = false;
        CallDeferred(nameof(LoadCatalog));
    }

    private void BuildUi()
    {
        _panel = new Panel { Name = "Panel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_panel);

        var root = new VBoxContainer();
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
            Text = "Editor de Missoes",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });

        var addButton = new Button { Text = "Nova" };
        addButton.Pressed += AddQuest;
        header.AddChild(addButton);

        var duplicateButton = new Button { Text = "Duplicar" };
        duplicateButton.Pressed += DuplicateQuest;
        header.AddChild(duplicateButton);

        var removeButton = new Button { Text = "Remover" };
        removeButton.Pressed += RemoveSelectedQuest;
        header.AddChild(removeButton);

        var reloadButton = new Button { Text = "Recarregar" };
        reloadButton.Pressed += LoadCatalog;
        header.AddChild(reloadButton);

        var saveButton = new Button { Text = "Salvar Catalogo" };
        saveButton.Pressed += SaveCatalog;
        header.AddChild(saveButton);

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(body);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        body.AddChild(left);
        _questList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _questList.ItemSelected += OnQuestSelected;
        left.AddChild(_questList);

        _statusLabel = new Label
        {
            Text = "Selecione ou crie uma missao.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        left.AddChild(_statusLabel);

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

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        form.AddChild(tabs);

        BuildQuestTab(tabs);
        BuildNpcTab(tabs);
        BuildDialogTab(tabs);
        BuildDeliveryTab(tabs);
    }

    private void BuildQuestTab(TabContainer tabs)
    {
        var tab = CreateTab(tabs, "Quest");
        _idSpin = CreateSpin(tab, "ID", 1, 999999, 1);
        _nameEdit = CreateLine(tab, "Nome da missao");
        _requiredLevelSpin = CreateSpin(tab, "Nivel minimo", 1, 999, 1);
        _descriptionEdit = CreateText(tab, "Resumo curto", 72);
        _questTextEdit = CreateText(tab, "O que deve fazer quando pegar a quest", 120);

        AddSeparator(tab, "Objetivos");
        var row = new HBoxContainer();
        tab.AddChild(row);
        _objectiveTypeDropdown = new OptionButton();
        _objectiveTypeDropdown.AddItem("Matar", 0);
        _objectiveTypeDropdown.AddItem("Coletar", 1);
        _objectiveTypeDropdown.AddItem("Falar", 2);
        _objectiveTypeDropdown.AddItem("Dropar/Pegar item", 3);
        _objectiveTypeDropdown.AddItem("Chegar no local", 4);
        _objectiveTypeDropdown.AddItem("Interagir com objeto", 5);
        _objectiveTypeDropdown.AddItem("Usar item", 6);
        _objectiveTypeDropdown.AddItem("Escoltar NPC", 7);
        _objectiveTypeDropdown.AddItem("Fabricar item", 8);
        row.AddChild(_objectiveTypeDropdown);

        _objectiveTargetEdit = new LineEdit
        {
            PlaceholderText = "ID do alvo: slime, wolf_fur, npc_ferreiro...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(_objectiveTargetEdit);
        _objectiveCountSpin = new SpinBox { MinValue = 1, MaxValue = 9999, Value = 1, CustomMinimumSize = new Vector2(86, 0) };
        row.AddChild(_objectiveCountSpin);

        var addObjectiveButton = new Button { Text = "Adicionar" };
        addObjectiveButton.Pressed += AddObjective;
        row.AddChild(addObjectiveButton);

        var removeObjectiveButton = new Button { Text = "Remover" };
        removeObjectiveButton.Pressed += RemoveSelectedObjective;
        row.AddChild(removeObjectiveButton);

        _objectiveList = new ItemList { CustomMinimumSize = new Vector2(0, 130), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tab.AddChild(_objectiveList);

        var locationRow = new HBoxContainer();
        tab.AddChild(locationRow);
        locationRow.AddChild(new Label { Text = "Local X" });
        _objectiveXSpin = new SpinBox { MinValue = -999999, MaxValue = 999999, Step = 1, CustomMinimumSize = new Vector2(110, 0) };
        locationRow.AddChild(_objectiveXSpin);
        locationRow.AddChild(new Label { Text = "Y" });
        _objectiveYSpin = new SpinBox { MinValue = -999999, MaxValue = 999999, Step = 1, CustomMinimumSize = new Vector2(110, 0) };
        locationRow.AddChild(_objectiveYSpin);
        locationRow.AddChild(new Label { Text = "Raio" });
        _objectiveRadiusSpin = new SpinBox { MinValue = 8, MaxValue = 9999, Step = 1, Value = 48, CustomMinimumSize = new Vector2(100, 0) };
        locationRow.AddChild(_objectiveRadiusSpin);
        locationRow.AddChild(new Label
        {
            Text = "Usado em quest de chegar no local.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });

        WireDirty(_idSpin, _nameEdit, _requiredLevelSpin, _descriptionEdit, _questTextEdit);
    }

    private void BuildNpcTab(TabContainer tabs)
    {
        var tab = CreateTab(tabs, "NPC");
        tab.AddChild(new Label
        {
            Text = "Escolha a cena do NPC que entrega essa missao.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tab.AddChild(row);
        _npcSceneEdit = new LineEdit
        {
            PlaceholderText = "res://characters/Npcs/WorldNPC.tscn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(_npcSceneEdit);

        var browse = new Button { Text = "Escolher cena" };
        browse.Pressed += BrowseNpcScene;
        row.AddChild(browse);

        var clear = new Button { Text = "Limpar" };
        clear.Pressed += () => { _npcSceneEdit.Text = ""; StoreCurrentQuest(); };
        row.AddChild(clear);

        WireDirty(_npcSceneEdit);
    }

    private void BuildDialogTab(TabContainer tabs)
    {
        var tab = CreateTab(tabs, "Dialogos");
        _offerDialogEdit = CreateText(tab, "Fala antes de aceitar", 90);
        _acceptedDialogEdit = CreateText(tab, "Fala logo depois que o player pega a quest", 90);
        _progressDialogEdit = CreateText(tab, "Fala enquanto a quest esta em andamento", 90);
        _completeDialogEdit = CreateText(tab, "Fala quando o player completou e voltou ao NPC", 90);
        WireDirty(_offerDialogEdit, _acceptedDialogEdit, _progressDialogEdit, _completeDialogEdit);
    }

    private void BuildDeliveryTab(TabContainer tabs)
    {
        var tab = CreateTab(tabs, "Entrega");
        _deliveryDialogEdit = CreateText(tab, "Texto de entrega da quest", 88);
        _rewardXpSpin = CreateSpin(tab, "Recompensa em XP", 0, 999999999, 1);
        _rewardGoldSpin = CreateSpin(tab, "Recompensa em gold", 0, 999999999, 1);

        AddSeparator(tab, "Itens de recompensa");
        var row = new HBoxContainer();
        tab.AddChild(row);
        _rewardItemIdSpin = new SpinBox { MinValue = 1, MaxValue = 999999, Value = 1, CustomMinimumSize = new Vector2(120, 0) };
        row.AddChild(_rewardItemIdSpin);
        _rewardItemQuantitySpin = new SpinBox { MinValue = 1, MaxValue = 9999, Value = 1, CustomMinimumSize = new Vector2(90, 0) };
        row.AddChild(_rewardItemQuantitySpin);
        var addItem = new Button { Text = "Adicionar item" };
        addItem.Pressed += AddRewardItem;
        row.AddChild(addItem);
        var removeItem = new Button { Text = "Remover item" };
        removeItem.Pressed += RemoveSelectedRewardItem;
        row.AddChild(removeItem);

        _rewardItemList = new ItemList { CustomMinimumSize = new Vector2(0, 120), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tab.AddChild(_rewardItemList);

        WireDirty(_deliveryDialogEdit, _rewardXpSpin, _rewardGoldSpin);
    }

    private static VBoxContainer CreateTab(TabContainer tabs, string name)
    {
        var tab = new VBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        tabs.AddChild(tab);
        return tab;
    }

    private static void AddSeparator(VBoxContainer form, string text)
    {
        form.AddChild(new HSeparator());
        form.AddChild(new Label { Text = text });
    }

    private static LineEdit CreateLine(VBoxContainer form, string label)
    {
        form.AddChild(new Label { Text = label });
        var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddChild(edit);
        return edit;
    }

    private static TextEdit CreateText(VBoxContainer form, string label, float height)
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

    private static SpinBox CreateSpin(VBoxContainer form, string label, double min, double max, double step)
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

    private void WireDirty(params Control[] controls)
    {
        foreach (var control in controls)
        {
            if (control is LineEdit line) line.TextChanged += _ => StoreCurrentQuest();
            else if (control is TextEdit text) text.TextChanged += StoreCurrentQuest;
            else if (control is SpinBox spin) spin.ValueChanged += _ => StoreCurrentQuest();
        }
    }

    private void LoadCatalog()
    {
        _quests.Clear();
        _selectedIndex = -1;

        string absolutePath = ProjectSettings.GlobalizePath(QuestCatalogPath);
        if (File.Exists(absolutePath))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var catalog = JsonSerializer.Deserialize<QuestEditorCatalog>(File.ReadAllText(absolutePath), options);
                if (catalog?.Quests != null)
                    _quests.AddRange(catalog.Quests.OrderBy(q => q.Id));
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Erro ao ler catalogo: {ex.Message}";
            }
        }

        if (_quests.Count == 0)
            SeedDefaultQuests();

        RefreshQuestList();
        if (_quests.Count > 0)
        {
            _questList.Select(0);
            SelectQuest(0);
        }
    }

    private void SaveCatalog()
    {
        StoreCurrentQuest();

        string absolutePath = ProjectSettings.GlobalizePath(QuestCatalogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        var catalog = new QuestEditorCatalog
        {
            Version = 1,
            Quests = _quests.OrderBy(q => q.Id).ToList(),
        };
        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(absolutePath, json);

        _statusLabel.Text = $"Catalogo salvo: {QuestCatalogPath}";
    }

    private void SeedDefaultQuests()
    {
        _quests.Add(new QuestEditorDefinition
        {
            Id = 1,
            Name = "Caca aos Slimes",
            Description = "Elimine 5 slimes na floresta.",
            QuestText = "Derrote 5 slimes para ajudar a limpar a area inicial.",
            RequiredLevel = 1,
            NpcScenePath = "res://characters/Npcs/WorldNPC.tscn",
            OfferDialog = "Os slimes estao se multiplicando. Pode reduzir o numero deles?",
            AcceptedDialog = "Boa sorte. Volte quando tiver derrotado os slimes.",
            ProgressDialog = "Ainda existem slimes demais por perto.",
            CompleteDialog = "Muito bem. Voce ajudou a deixar a area mais segura.",
            DeliveryDialog = "Aqui esta sua recompensa.",
            Objectives = new List<QuestEditorObjective>
            {
                new() { Type = "Kill", TargetId = "slime", RequiredCount = 5 },
            },
            Reward = new QuestEditorReward { Experience = 35, Gold = 10 },
        });
    }

    private void RefreshQuestList()
    {
        _questList.Clear();
        for (int i = 0; i < _quests.Count; i++)
            _questList.AddItem($"{_quests[i].Id} - {_quests[i].Name}");
    }

    private void OnQuestSelected(long index)
    {
        SelectQuest((int)index);
    }

    private void SelectQuest(int index)
    {
        if (index < 0 || index >= _quests.Count) return;
        StoreCurrentQuest();
        _selectedIndex = index;
        LoadQuestIntoForm(_quests[index]);
    }

    private void LoadQuestIntoForm(QuestEditorDefinition quest)
    {
        _loading = true;
        _idSpin.Value = quest.Id;
        _nameEdit.Text = quest.Name ?? "";
        _requiredLevelSpin.Value = quest.RequiredLevel;
        _npcSceneEdit.Text = quest.NpcScenePath ?? "";
        _descriptionEdit.Text = quest.Description ?? "";
        _questTextEdit.Text = quest.QuestText ?? "";
        _offerDialogEdit.Text = quest.OfferDialog ?? "";
        _acceptedDialogEdit.Text = quest.AcceptedDialog ?? "";
        _progressDialogEdit.Text = quest.ProgressDialog ?? "";
        _completeDialogEdit.Text = quest.CompleteDialog ?? "";
        _deliveryDialogEdit.Text = quest.DeliveryDialog ?? "";
        _rewardXpSpin.Value = quest.Reward?.Experience ?? 0;
        _rewardGoldSpin.Value = quest.Reward?.Gold ?? 0;
        _loading = false;

        RefreshObjectiveList();
        RefreshRewardItemList();
        _statusLabel.Text = $"Editando missao {quest.Id}: {quest.Name}";
    }

    private void StoreCurrentQuest()
    {
        if (_loading || _selectedIndex < 0 || _selectedIndex >= _quests.Count) return;
        var quest = _quests[_selectedIndex];
        quest.Id = (int)_idSpin.Value;
        quest.Name = _nameEdit.Text.Trim();
        quest.RequiredLevel = (int)_requiredLevelSpin.Value;
        quest.NpcScenePath = _npcSceneEdit.Text.Trim();
        quest.Description = _descriptionEdit.Text.Trim();
        quest.QuestText = _questTextEdit.Text.Trim();
        quest.OfferDialog = _offerDialogEdit.Text.Trim();
        quest.AcceptedDialog = _acceptedDialogEdit.Text.Trim();
        quest.ProgressDialog = _progressDialogEdit.Text.Trim();
        quest.CompleteDialog = _completeDialogEdit.Text.Trim();
        quest.DeliveryDialog = _deliveryDialogEdit.Text.Trim();
        quest.Reward ??= new QuestEditorReward();
        quest.Reward.Experience = (long)_rewardXpSpin.Value;
        quest.Reward.Gold = (int)_rewardGoldSpin.Value;
        RefreshQuestListPreservingSelection();
    }

    private void RefreshQuestListPreservingSelection()
    {
        int selected = _selectedIndex;
        RefreshQuestList();
        if (selected >= 0 && selected < _questList.ItemCount)
            _questList.Select(selected);
    }

    private void AddQuest()
    {
        StoreCurrentQuest();
        int nextId = _quests.Count == 0 ? 1 : _quests.Max(q => q.Id) + 1;
        _quests.Add(new QuestEditorDefinition
        {
            Id = nextId,
            Name = $"Nova Missao {nextId}",
            RequiredLevel = 1,
            NpcScenePath = "res://characters/Npcs/WorldNPC.tscn",
            QuestText = "Descreva aqui o que o jogador deve fazer.",
            OfferDialog = "Texto inicial do NPC.",
            AcceptedDialog = "Texto depois que o jogador aceita a missao.",
            ProgressDialog = "Texto enquanto a missao esta em andamento.",
            CompleteDialog = "Texto quando a missao esta completa.",
            DeliveryDialog = "Texto de entrega da recompensa.",
            Reward = new QuestEditorReward(),
        });
        RefreshQuestList();
        _questList.Select(_quests.Count - 1);
        SelectQuest(_quests.Count - 1);
    }

    private void DuplicateQuest()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _quests.Count) return;
        StoreCurrentQuest();
        var copy = JsonSerializer.Deserialize<QuestEditorDefinition>(JsonSerializer.Serialize(_quests[_selectedIndex]))!;
        copy.Id = _quests.Max(q => q.Id) + 1;
        copy.Name += " Copia";
        _quests.Add(copy);
        RefreshQuestList();
        _questList.Select(_quests.Count - 1);
        SelectQuest(_quests.Count - 1);
    }

    private void RemoveSelectedQuest()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _quests.Count) return;
        _quests.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        RefreshQuestList();
        if (_quests.Count > 0)
        {
            _questList.Select(0);
            SelectQuest(0);
        }
        else
        {
            _statusLabel.Text = "Nenhuma missao no catalogo.";
        }
    }

    private void AddObjective()
    {
        if (_selectedIndex < 0) return;
        StoreCurrentQuest();
        var quest = _quests[_selectedIndex];
        quest.Objectives.Add(new QuestEditorObjective
        {
            Type = ObjectiveTypeFromId(_objectiveTypeDropdown.GetSelectedId()),
            TargetId = _objectiveTargetEdit.Text.Trim(),
            RequiredCount = Math.Max(1, (int)_objectiveCountSpin.Value),
            TargetX = (float)_objectiveXSpin.Value,
            TargetY = (float)_objectiveYSpin.Value,
            Radius = Math.Max(8f, (float)_objectiveRadiusSpin.Value),
        });
        RefreshObjectiveList();
    }

    private void RemoveSelectedObjective()
    {
        if (_selectedIndex < 0 || _objectiveList.GetSelectedItems().Length == 0) return;
        int index = _objectiveList.GetSelectedItems()[0];
        var objectives = _quests[_selectedIndex].Objectives;
        if (index >= 0 && index < objectives.Count)
            objectives.RemoveAt(index);
        RefreshObjectiveList();
    }

    private void RefreshObjectiveList()
    {
        _objectiveList.Clear();
        if (_selectedIndex < 0) return;
        foreach (var obj in _quests[_selectedIndex].Objectives)
        {
            string extra = obj.Type == "ReachLocation"
                ? $" | X:{obj.TargetX:0} Y:{obj.TargetY:0} R:{obj.Radius:0}"
                : "";
            _objectiveList.AddItem($"{TranslateObjectiveType(obj.Type)} | {obj.TargetId} | {obj.RequiredCount}{extra}");
        }
    }

    private void AddRewardItem()
    {
        if (_selectedIndex < 0) return;
        StoreCurrentQuest();
        var quest = _quests[_selectedIndex];
        quest.Reward ??= new QuestEditorReward();
        quest.Reward.Items.Add(new QuestEditorRewardItem
        {
            ItemId = (int)_rewardItemIdSpin.Value,
            Quantity = Math.Max(1, (int)_rewardItemQuantitySpin.Value),
        });
        RefreshRewardItemList();
    }

    private void RemoveSelectedRewardItem()
    {
        if (_selectedIndex < 0 || _rewardItemList.GetSelectedItems().Length == 0) return;
        int index = _rewardItemList.GetSelectedItems()[0];
        var items = _quests[_selectedIndex].Reward.Items;
        if (index >= 0 && index < items.Count)
            items.RemoveAt(index);
        RefreshRewardItemList();
    }

    private void RefreshRewardItemList()
    {
        _rewardItemList.Clear();
        if (_selectedIndex < 0) return;
        foreach (var item in _quests[_selectedIndex].Reward.Items)
            _rewardItemList.AddItem($"Item {item.ItemId} x{item.Quantity}");
    }

    private void BrowseNpcScene()
    {
        var dialog = new FileDialog
        {
            Title = "Escolher cena do NPC",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Resources,
            CurrentDir = NpcDefaultFolder,
        };
        dialog.Filters = new[] { "*.tscn ; Cena Godot" };
        AddChild(dialog);
        dialog.FileSelected += path =>
        {
            _npcSceneEdit.Text = path;
            StoreCurrentQuest();
            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered(new Vector2I(820, 540));
    }

    private static string TranslateObjectiveType(string type)
    {
        return type switch
        {
            "Collect" => "Coletar",
            "Talk" => "Falar",
            "DropItem" => "Dropar/Pegar item",
            "ReachLocation" => "Chegar no local",
            "InteractObject" => "Interagir com objeto",
            "UseItem" => "Usar item",
            "Escort" => "Escoltar NPC",
            "Craft" => "Fabricar item",
            _ => "Matar",
        };
    }

    private static string ObjectiveTypeFromId(long id)
    {
        return id switch
        {
            1 => "Collect",
            2 => "Talk",
            3 => "DropItem",
            4 => "ReachLocation",
            5 => "InteractObject",
            6 => "UseItem",
            7 => "Escort",
            8 => "Craft",
            _ => "Kill",
        };
    }

    private sealed class QuestEditorCatalog
    {
        public int Version { get; set; } = 1;
        public List<QuestEditorDefinition> Quests { get; set; } = new();
    }

    private sealed class QuestEditorDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string QuestText { get; set; } = "";
        public int RequiredLevel { get; set; } = 1;
        public string NpcScenePath { get; set; } = "";
        public string OfferDialog { get; set; } = "";
        public string AcceptedDialog { get; set; } = "";
        public string ProgressDialog { get; set; } = "";
        public string CompleteDialog { get; set; } = "";
        public string DeliveryDialog { get; set; } = "";
        public List<QuestEditorObjective> Objectives { get; set; } = new();
        public QuestEditorReward Reward { get; set; } = new();
    }

    private sealed class QuestEditorObjective
    {
        public string Type { get; set; } = "Kill";
        public string TargetId { get; set; } = "";
        public int RequiredCount { get; set; } = 1;
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float Radius { get; set; } = 48f;
    }

    private sealed class QuestEditorReward
    {
        public long Experience { get; set; }
        public int Gold { get; set; }
        public List<QuestEditorRewardItem> Items { get; set; } = new();
    }

    private sealed class QuestEditorRewardItem
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
