using Godot;
using System.Collections.Generic;

public partial class PlayerSkillComponent : Node
{
    [Signal] public delegate void SkillSlotsAtualizadosEventHandler();

    [Export]
    public SkillResource[] SkillSlots { get; set; } = new SkillResource[20];

    [Export]
    public ItemResource[] ItemSlots { get; set; } = new ItemResource[20];

    public int[] ItemSlotIndexes { get; set; } = new int[20];

    [Export]
    public SkillResource[] SkillsDisponiveis { get; set; } = System.Array.Empty<SkillResource>();

    // Internal state used by the Extra partial
    private readonly Dictionary<string, double> _cooldownTimers = new();
    private readonly List<object> _activeBuffs = new();
    private readonly Dictionary<int, SkillResource> _skillCatalog = new();
    private Player _player;

    public override void _Ready()
    {
        _player = GetParent() as Player;
        if (_player == null)
            GD.PrintErr("[SKILLCOMP] Player não encontrado como pai do componente de skills.");
        System.Array.Fill(ItemSlotIndexes, -1);
        CarregarCatalogoDeSkills();

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null)
        {
            gameNet.OnSkillBarData += AplicarBarraServidor;
            if (gameNet.PendingSkillBarData != null)
                AplicarBarraServidor(gameNet.PendingSkillBarData);
        }
    }

    public SkillResource ObterSkillPorId(int skillId)
    {
        return _skillCatalog.TryGetValue(skillId, out var skill) ? skill : null;
    }

    public void AplicarBarraServidor(Godot.Collections.Array<int> skillIds)
    {
        for (int i = 0; i < SkillSlots.Length; i++)
        {
            int skillId = i < skillIds.Count ? skillIds[i] : 0;
            SkillSlots[i] = skillId > 0 ? ObterSkillPorId(skillId) : null;
            if (SkillSlots[i] != null && ItemSlots != null && i < ItemSlots.Length)
            {
                ItemSlots[i] = null;
                if (ItemSlotIndexes != null && i < ItemSlotIndexes.Length)
                    ItemSlotIndexes[i] = -1;
            }
        }

        GD.Print($"[SKILLCOMP] Barra do servidor aplicada: {skillIds.Count} slots");
        EmitSignal(SignalName.SkillSlotsAtualizados);
    }

    private void CarregarCatalogoDeSkills()
    {
        _skillCatalog.Clear();
        CarregarCatalogoDeSkillsEm("res://skills/habilidades");
        GD.Print($"[SKILLCOMP] Catálogo local de skills carregado: {_skillCatalog.Count}");
    }

    private void CarregarCatalogoDeSkillsEm(string path)
    {
        using var dir = DirAccess.Open(path);
        if (dir == null)
            return;

        dir.ListDirBegin();
        while (true)
        {
            string entry = dir.GetNext();
            if (string.IsNullOrEmpty(entry))
                break;
            if (entry == "." || entry == "..")
                continue;

            string childPath = $"{path}/{entry}";
            if (dir.CurrentIsDir())
            {
                CarregarCatalogoDeSkillsEm(childPath);
                continue;
            }

            if (!entry.EndsWith(".tres", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var skill = ResourceLoader.Load<SkillResource>(childPath);
            if (skill != null && skill.SkillId > 0)
                _skillCatalog[skill.SkillId] = skill;
        }
    }
}
