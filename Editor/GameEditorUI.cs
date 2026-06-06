using Godot;
using System;
using System.Collections.Generic;

public partial class GameEditorUI : Control
{
    private Panel _panel;
    private TabContainer _tabs;
    private readonly List<Control> _pendingEditors = new();

    public bool PainelVisivel => _panel != null && _panel.Visible;
    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        var fechar = _panel.GetNode<Button>("Header/CloseButton");
        var header = _panel.GetNode<Control>("Header");
        fechar.Pressed += () => { _panel.Visible = false; };
        header.GuiInput += OnHeaderDrag;
        _tabs = _panel.GetNode<TabContainer>("Tabs");

        EnsureInputAction();
        _panel.Visible = false;
        CallDeferred(nameof(CarregarEditores));
    }

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction("game_editor")) return;
        InputMap.AddAction("game_editor");
        InputMap.ActionAddEvent("game_editor", new InputEventKey { PhysicalKeycode = Key.F10, Keycode = Key.F10, Unicode = 0, Pressed = false });
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("game_editor"))
            _panel.Visible = !_panel.Visible;
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

    private void CarregarEditores()
    {
        CarregarEditor("Classes", "res://Editor/ClasseEditorUI.tscn");
        CarregarEditor("Talentos", "res://Editor/TalentNodeEditorUI.tscn");
        CarregarEditor("Itens", "res://Editor/ItemEditorUI.tscn");
        CarregarEditor("Pets", "res://Editor/PetEditorUI.tscn");
        CarregarEditor("Admin", "res://Editor/AdminPanelUI.tscn");

        if (_pendingEditors.Count > 0)
            CallDeferred(nameof(AjustarEditoresAposReady));
    }

    private void CarregarEditor(string nome, string cenaPath)
    {
        var scene = ResourceLoader.Load<PackedScene>(cenaPath);
        if (scene == null) return;

        var instancia = scene.Instantiate<Control>();
        if (instancia == null) return;

        instancia.Name = nome;
        _tabs.AddChild(instancia);
        _pendingEditors.Add(instancia);
    }

    private void AjustarEditoresAposReady()
    {
        foreach (var instancia in _pendingEditors)
        {
            if (!GodotObject.IsInstanceValid(instancia)) continue;

            var panel = instancia.GetNodeOrNull<Panel>("Panel");
            if (panel != null)
            {
                panel.Visible = true;
                panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                panel.OffsetTop = 0;
                panel.OffsetBottom = 0;
                panel.OffsetLeft = 0;
                panel.OffsetRight = 0;
            }
        }
        _pendingEditors.Clear();
    }

    public void AbrirEditor(int index = 0)
    {
        _panel.Visible = true;
        if (_tabs.GetChildCount() > index)
            _tabs.CurrentTab = index;
    }
}
