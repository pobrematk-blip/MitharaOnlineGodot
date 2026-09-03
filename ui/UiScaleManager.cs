using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public partial class UiScaleManager : Node
{
    public static UiScaleManager Instance { get; private set; }
    public event Action ScalesChanged;

    private const string SettingsPath = "user://settings.cfg";
    private const string Section = "UIScale";
    private readonly Dictionary<string, float> _specificScales = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, Vector2> _originalScales = new();
    private readonly Dictionary<ulong, string> _registeredControls = new();
    private float _globalScale = 1f;

    public float GlobalScale => _globalScale;

    public override void _Ready()
    {
        Instance = this;
        Load();
        GetTree().NodeAdded += OnNodeAdded;
        CallDeferred(MethodName.ScanTree);
    }

    public override void _ExitTree()
    {
        if (GetTree() != null)
            GetTree().NodeAdded -= OnNodeAdded;
        if (Instance == this)
            Instance = null;
    }

    public IReadOnlyList<string> GetAllCategories()
    {
        var names = GetType().Assembly.GetTypes()
            .Where(IsScalableType)
            .Select(t => t.Name)
            .Append("Tooltips")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(GetDisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return names;
    }

    public string GetDisplayName(string category)
    {
        string name = category
            .Replace("UI", "", StringComparison.Ordinal)
            .Replace("Hud", " HUD", StringComparison.Ordinal)
            .Replace("HUD", " HUD", StringComparison.Ordinal);
        return string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(name[i - 1]) ? " " + c : c.ToString())).Trim();
    }

    public float GetSpecificScale(string category)
    {
        return _specificScales.TryGetValue(category, out float value)
            ? value
            : string.Equals(category, "Tooltips", StringComparison.Ordinal) ? 0.8f : 1f;
    }

    public float GetCombinedScale(string category)
    {
        return Mathf.Clamp(_globalScale * GetSpecificScale(category), 0.35f, 2.25f);
    }

    public void SetGlobalScale(float value)
    {
        _globalScale = Mathf.Clamp(value, 0.5f, 1.5f);
        ApplyAll();
        Save();
        ScalesChanged?.Invoke();
    }

    public void SetSpecificScale(string category, float value)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;
        _specificScales[category] = Mathf.Clamp(value, 0.5f, 1.5f);
        ApplyAll();
        Save();
        ScalesChanged?.Invoke();
    }

    private void OnNodeAdded(Node node)
    {
        Callable.From(() => TryRegister(node)).CallDeferred();
    }

    private void ScanTree()
    {
        TryRegisterRecursive(GetTree().Root);
        ApplyAll();
    }

    private void TryRegisterRecursive(Node node)
    {
        TryRegister(node);
        foreach (Node child in node.GetChildren())
            TryRegisterRecursive(child);
    }

    private void TryRegister(Node node)
    {
        if (node is not Control control || !IsScalableType(node.GetType()))
            return;

        for (Node parent = node.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is Control && IsScalableType(parent.GetType()))
                return;

        ulong id = control.GetInstanceId();
        if (_registeredControls.ContainsKey(id))
        {
            Apply(control, node.GetType().Name);
            return;
        }
        if (!_originalScales.ContainsKey(id))
            _originalScales[id] = control.Scale;
        _registeredControls[id] = node.GetType().Name;
        control.TreeExiting += () =>
        {
            _registeredControls.Remove(id);
            _originalScales.Remove(id);
        };
        Apply(control, node.GetType().Name);
    }

    private void ApplyAll()
    {
        foreach (var pair in _registeredControls.ToArray())
        {
            var instance = GodotObject.InstanceFromId(pair.Key) as Control;
            if (instance == null || !IsInstanceValid(instance))
            {
                _registeredControls.Remove(pair.Key);
                _originalScales.Remove(pair.Key);
                continue;
            }
            Apply(instance, pair.Value);
        }
    }

    private void Apply(Control control, string category)
    {
        Vector2 original = _originalScales.TryGetValue(control.GetInstanceId(), out Vector2 scale)
            ? scale
            : Vector2.One;
        float combined = GetCombinedScale(category);
        control.PivotOffset = Vector2.Zero;
        control.Scale = original * combined;
    }

    private static bool IsScalableType(Type type)
    {
        if (!typeof(Control).IsAssignableFrom(type) || type.IsAbstract)
            return false;
        string name = type.Name;
        return name.EndsWith("UI", StringComparison.Ordinal) ||
               name.EndsWith("Hud", StringComparison.Ordinal) ||
               name.EndsWith("HUD", StringComparison.Ordinal) ||
               name is "MiniMapa" or "PlayerContextMenu" or "BossHPBar";
    }

    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) != Error.Ok)
            return;
        if (!cfg.HasSection(Section))
            return;

        _globalScale = Mathf.Clamp((float)cfg.GetValue(Section, "global", 1.0).AsDouble(), 0.5f, 1.5f);
        foreach (string key in cfg.GetSectionKeys(Section))
        {
            if (!key.StartsWith("window_", StringComparison.Ordinal))
                continue;
            _specificScales[key["window_".Length..]] =
                Mathf.Clamp((float)cfg.GetValue(Section, key, 1.0).AsDouble(), 0.5f, 1.5f);
        }
    }

    private void Save()
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath);
        cfg.SetValue(Section, "global", _globalScale);
        foreach (var pair in _specificScales)
            cfg.SetValue(Section, $"window_{pair.Key}", pair.Value);
        cfg.Save(SettingsPath);
    }
}
