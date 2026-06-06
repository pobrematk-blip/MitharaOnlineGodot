using Godot;
using System.Collections.Generic;

public partial class PlayerSkillComponent : Node
{
    [Export]
    public SkillResource[] SkillSlots { get; set; } = new SkillResource[20];

    [Export]
    public ItemResource[] ItemSlots { get; set; } = new ItemResource[20];

    [Export]
    public SkillResource[] SkillsDisponiveis { get; set; } = System.Array.Empty<SkillResource>();

    // Internal state used by the Extra partial
    private readonly Dictionary<string, double> _cooldownTimers = new();
    private readonly List<object> _activeBuffs = new();
    private Player _player;

    public override void _Ready()
    {
        _player = GetParent() as Player;
        if (_player == null)
            GD.PrintErr("[SKILLCOMP] Player não encontrado como pai do componente de skills.");
    }
}
