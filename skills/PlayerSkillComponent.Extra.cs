using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerSkillComponent
{
    // Public activation entrypoint used by the UI.
    public void ActivateSlotIndex(int slotIndex)
    {
        if (SkillSlots == null)
        {
            GD.Print("[SKILLCOMP] Nenhuma tabela de skills definida.");
            return;
        }

        if (slotIndex < 0 || slotIndex >= SkillSlots.Length)
        {
            GD.Print($"[SKILLCOMP] Slot inválido: {slotIndex}");
            return;
        }

        // Se o slot tiver um item consumível, usa-o do inventário
        if (ItemSlots != null && slotIndex >= 0 && slotIndex < ItemSlots.Length && ItemSlots[slotIndex] != null)
        {
            UsarItemDoSlot(slotIndex);
            return;
        }

        var skill = SkillSlots[slotIndex];
        if (skill == null)
        {
            GD.Print($"[SKILLCOMP] Nenhum skill atribuído ao slot {slotIndex}.");
            return;
        }

        // Ignore passive skills
        if (skill.IsPassive)
        {
            GD.Print($"[SKILLCOMP] Skill '{skill.Nome}' é passiva e não pode ser ativada.");
            return;
        }

        // Check cooldown
        if (_cooldownTimers.TryGetValue(skill.SkillName, out double remaining) && remaining > 0)
        {
            GD.Print($"[SKILLCOMP] Skill '{skill.SkillName}' em cooldown ({remaining:F1}s).");
            return;
        }

        // Verificar mana/recursos do jogador
        if (_player != null && skill.ManaCost > 0)
        {
            var tryConsume = _player.GetType().GetMethod("TryConsumeMana");
            if (tryConsume != null)
            {
                bool ok = (bool)tryConsume.Invoke(_player, new object[] { skill.ManaCost });
                if (!ok)
                {
                    GD.Print($"[SKILLCOMP] Mana insuficiente para '{skill.Nome}'.");
                    return;
                }
            }
        }

        // Aplicar cooldown
        EnsureCooldownTimer();
        _cooldownTimers[skill.SkillName] = skill.Cooldown;

        // Trigger simple effect placeholder
        GD.Print($"[SKILLCOMP] Ativando skill '{skill.SkillName}' (Tipo: {skill.EffectType})");

        // Efeitos simples por tipo (placeholder):
        switch (skill.EffectType)
        {
            case SkillEffectType.Heal:
                if (_player != null)
                {
                    var healMethod = _player.GetType().GetMethod("Heal");
                    if (healMethod != null) healMethod.Invoke(_player, new object[] { skill.Power });
                }
                break;
            case SkillEffectType.Dash:
                // sinalizar dash para o player (implementar na lógica do Player)
                _player?.CallDeferred("DoDash", skill.Power);
                break;
            case SkillEffectType.Revive:
                // procura por aliados mortos próximos e tenta reviver
                var nodes = GetTree().GetNodesInGroup("player");
                foreach (Node n in nodes)
                {
                    if (n is Player p && p != _player && p.IsDead)
                    {
                        double dist = (_player as Node2D)?.GlobalPosition.DistanceTo(p.GlobalPosition) ?? double.MaxValue;
                        if (dist <= 150f)
                        {
                            _player?.CallDeferred("AttemptRevive", p);
                            GD.Print($"[SKILLCOMP] Tentando reviver {p.Name}");
                            break;
                        }
                    }
                }
                break;
            case SkillEffectType.Stun:
                _player?.CallDeferred("ApplyTemporaryBuff", "stun", (float)(skill.Duration > 0 ? skill.Duration : 3f), skill.Power);
                break;
            case SkillEffectType.Bleed:
                // add active bleed buff handled by component ticks
                _activeBuffsList.Add(new ActiveBuff { Type = "bleed", Remaining = (float)(skill.Duration > 0 ? skill.Duration : 8f), Power = skill.Power, TickInterval = 1f, TickTimer = 0f });
                _player?.CallDeferred("ApplyTemporaryBuff", "bleed", (float)(skill.Duration > 0 ? skill.Duration : 8f), skill.Power);
                break;
            case SkillEffectType.Shield:
                _player?.CallDeferred("ApplyTemporaryBuff", "shield", (float)(skill.Duration > 0 ? skill.Duration : 30f), skill.Power);
                break;
            case SkillEffectType.Silence:
                _player?.CallDeferred("ApplyTemporaryBuff", "silence", (float)(skill.Duration > 0 ? skill.Duration : 5f), skill.Power);
                break;
            case SkillEffectType.Slow:
                _player?.CallDeferred("ApplyTemporaryBuff", "slow", (float)(skill.Duration > 0 ? skill.Duration : 5f), skill.Power);
                break;
            case SkillEffectType.Root:
                _player?.CallDeferred("ApplyTemporaryBuff", "root", (float)(skill.Duration > 0 ? skill.Duration : 3f), skill.Power);
                break;
            case SkillEffectType.Taunt:
                _player?.CallDeferred("ApplyTemporaryBuff", "taunt", (float)(skill.Duration > 0 ? skill.Duration : 5f), skill.Power);
                break;
            case SkillEffectType.Burn:
                _activeBuffsList.Add(new ActiveBuff { Type = "burn", Remaining = (float)(skill.Duration > 0 ? skill.Duration : 8f), Power = skill.Power, TickInterval = 1f, TickTimer = 0f });
                _player?.CallDeferred("ApplyTemporaryBuff", "burn", (float)(skill.Duration > 0 ? skill.Duration : 8f), skill.Power);
                break;
            case SkillEffectType.Freeze:
                _player?.CallDeferred("ApplyTemporaryBuff", "freeze", (float)(skill.Duration > 0 ? skill.Duration : 3f), skill.Power);
                break;
            case SkillEffectType.Fear:
                _player?.CallDeferred("ApplyTemporaryBuff", "fear", (float)(skill.Duration > 0 ? skill.Duration : 3f), skill.Power);
                break;
            case SkillEffectType.Confusion:
                _player?.CallDeferred("ApplyTemporaryBuff", "confusion", (float)(skill.Duration > 0 ? skill.Duration : 4f), skill.Power);
                break;
            case SkillEffectType.Sleep:
                _player?.CallDeferred("ApplyTemporaryBuff", "sleep", (float)(skill.Duration > 0 ? skill.Duration : 5f), skill.Power);
                break;
            case SkillEffectType.Prison:
                _player?.CallDeferred("ApplyTemporaryBuff", "prison", (float)(skill.Duration > 0 ? skill.Duration : 5f), skill.Power);
                break;
            case SkillEffectType.Invincibility:
                _player?.CallDeferred("ApplyTemporaryBuff", "invincibility", (float)(skill.Duration > 0 ? skill.Duration : 4f), skill.Power);
                break;
            case SkillEffectType.Blindness:
                _player?.CallDeferred("ApplyTemporaryBuff", "blindness", (float)(skill.Duration > 0 ? skill.Duration : 6f), skill.Power);
                break;
            case SkillEffectType.Reflect:
                _player?.CallDeferred("ApplyTemporaryBuff", "reflect", (float)(skill.Duration > 0 ? skill.Duration : 10f), skill.Power);
                break;
            case SkillEffectType.Poison:
                _activeBuffsList.Add(new ActiveBuff { Type = "poison", Remaining = (float)(skill.Duration > 0 ? skill.Duration : 10f), Power = skill.Power, TickInterval = 1f, TickTimer = 0f });
                _player?.CallDeferred("ApplyTemporaryBuff", "poison", (float)(skill.Duration > 0 ? skill.Duration : 10f), skill.Power);
                break;
            case SkillEffectType.Curse:
                _player?.CallDeferred("ApplyTemporaryBuff", "curse", (float)(skill.Duration > 0 ? skill.Duration : 12f), skill.Power);
                break;
            case SkillEffectType.Invisibility:
                float dur = (float)(skill.Duration > 0 ? skill.Duration : 5f);
                _player?.CallDeferred("BecomeInvisible", dur);
                break;
            case SkillEffectType.Buff:
                // Aplica buff no jogador
                float bdur = (float)(skill.Duration > 0 ? skill.Duration : 30f);
                _player?.CallDeferred("ApplyTemporaryBuff", skill.BuffType ?? skill.Nome, bdur, skill.Power);
                break;
            case SkillEffectType.Summon:
                if (!string.IsNullOrEmpty(skill.SummonScenePath))
                {
                    // preferir usar método do player para summon de pets
                    _player?.CallDeferred("SummonPet", skill.SummonScenePath);
                }
                break;
        }
    }

    private Timer _skillTimer;

    private class ActiveBuff
    {
        public string Type;
        public float Remaining;
        public int Power;
        public float TickInterval;
        public float TickTimer;
    }

    private readonly List<ActiveBuff> _activeBuffsList = new();

    private void EnsureCooldownTimer()
    {
        if (_skillTimer != null && _skillTimer.IsInsideTree()) return;

        _skillTimer = new Timer();
        _skillTimer.WaitTime = 0.5f;
        _skillTimer.OneShot = false;
        _skillTimer.Autostart = true;
        AddChild(_skillTimer);
        _skillTimer.Timeout += OnCooldownTick;
    }

    private void OnCooldownTick()
    {
        double tick = _skillTimer.WaitTime;

        // Cooldowns
        if (_cooldownTimers.Count > 0)
        {
            var keys = new List<string>(_cooldownTimers.Keys);
            foreach (var k in keys)
            {
                _cooldownTimers[k] -= tick;
                if (_cooldownTimers[k] <= 0) _cooldownTimers.Remove(k);
            }
        }

        // Active buffs (bleed ticks, durations)
        if (_activeBuffsList.Count == 0) return;

        var remove = new List<ActiveBuff>();
        foreach (var b in _activeBuffsList)
        {
            b.Remaining -= (float)tick;
            b.TickTimer += (float)tick;

            if (b.TickInterval > 0 && b.TickTimer >= b.TickInterval)
            {
                b.TickTimer = 0f;
                if (_player != null)
                {
                    if (string.Equals(b.Type, "bleed", StringComparison.Ordinal) || string.Equals(b.Type, "burn", StringComparison.Ordinal) || string.Equals(b.Type, "poison", StringComparison.Ordinal))
                    {
                        _player.CallDeferred("PerformSkillDamage", b.Power);
                    }
                }
            }

            if (b.Remaining <= 0)
            {
                // notify player to remove buff effects
                _player?.CallDeferred("RemoveTemporaryBuff", b.Type);
                remove.Add(b);
            }
        }

        foreach (var r in remove) _activeBuffsList.Remove(r);
    }

    private void UsarItemDoSlot(int slotIndex)
    {
        var item = ItemSlots[slotIndex];
        if (item == null) return;

        // Encontra o jogador e o inventário
        if (_player == null)
        {
            GD.Print("[SKILLCOMP] Player não encontrado para usar item.");
            return;
        }

        var inv = _player.FindChild("InventarioComponent", true, false) as InventarioComponent;
        if (inv == null)
        {
            GD.Print("[SKILLCOMP] InventarioComponent não encontrado.");
            return;
        }

        // Procura o item no inventário
        for (int i = 0; i < inv.Slots.Count; i++)
        {
            var slot = inv.Slots[i];
            if (slot.Item != null && slot.Item.ItemID == item.ItemID && slot.Quantidade > 0)
            {
                GD.Print($"[SKILLCOMP] Usando item '{item.Nome}' do inventário (slot {i}).");

                if (item.ItemID == 100 || (item.Nome != null && item.Nome.IndexOf("Pergaminho", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    TentarCapturarPet(_player, inv, slot);
                    // Limpa o slot da skill bar
                    ItemSlots[slotIndex] = null;
                    NotificarSkillBarSlotLimpo(slotIndex);
                    return;
                }

                // Consome uma unidade (poções etc.)
                slot.Quantidade--;
                if (slot.Quantidade <= 0)
                {
                    slot.Item = null;
                    slot.Quantidade = 0;
                }

                inv.NotificarMudancaExterna();

                // Se for poção de vida (ItemID 1), cura o jogador
                if (item.ItemID == 1)
                {
                    _player.CallDeferred("Heal", 50);
                }
                // Se for poção de mana (ItemID 2), restaura mana
                else if (item.ItemID == 2)
                {
                    _player.CallDeferred("RestoreMana", 30);
                }

                return;
            }
        }

        GD.Print($"[SKILLCOMP] Item '{item.Nome}' não encontrado no inventário.");
    }

    private void TentarCapturarPet(Player player, InventarioComponent inv, SlotInventario slot)
    {
        var inimigos = GetTree().GetNodesInGroup("Inimigos");
        Inimigo alvo = null;
        float menorDist = 200f;

        foreach (var node in inimigos)
        {
            if (node is Inimigo inimigo)
            {
                float dist = player.GlobalPosition.DistanceTo(inimigo.GlobalPosition);
                if (dist < menorDist && inimigo.VidaAtual > 0 && inimigo.VidaAtual <= inimigo.VidaMax * 0.5f)
                {
                    menorDist = dist;
                    alvo = inimigo;
                }
            }
        }

        if (alvo == null)
        {
            GD.Print("[SKILLCOMP] Nenhum inimigo com menos de 50% de vida por perto.");
            return;
        }

        // Consome o pergaminho AGORA (antes do minigame)
        slot.Quantidade--;
        if (slot.Quantidade <= 0)
        {
            slot.Item = null;
            slot.Quantidade = 0;
        }
        inv.NotificarMudancaExterna();

        string petNome = alvo.NomeDoInimigo;
        int petId = 3;

        var miniGame = GD.Load<PackedScene>("res://ui/Pets/PetScrollMiniGame.tscn").Instantiate<PetScrollMiniGame>();
        var root = GetTree().CurrentScene;
        if (root != null)
            root.AddChild(miniGame);

        miniGame.Connect(PetScrollMiniGame.SignalName.MiniGameConcluido, Callable.From((int capturedPetId, string capturedPetNome, bool sucesso) =>
        {
            if (sucesso && IsInstanceValid(alvo))
            {
                var itemPet = new ItemResource
                {
                    ItemID = 200 + capturedPetId,
                    Nome = capturedPetNome,
                    Descricao = $"Pet capturado: {capturedPetNome}",
                    Tipo = TipoEquipamento.Pet,
                    Acumulavel = false,
                    QuantidadeMaximaPorSlot = 1,
                };
                inv.AdicionarItem(itemPet, 1);
                inv.NotificarMudancaExterna();

                alvo.QueueFree();
                GD.Print($"[SKILLCOMP] Pet {capturedPetNome} capturado!");
            }

            if (IsInstanceValid(miniGame))
                miniGame.QueueFree();
        }));

        miniGame.IniciarMiniGame(petId, petNome);
    }

    private void NotificarSkillBarSlotLimpo(int slotIndex)
    {
        var skillBar = GetTree()?.CurrentScene?.FindChild("SkillBarUI", true, false) as SkillBarUI;
        if (skillBar == null) return;
        int row = slotIndex / 10;
        int col = slotIndex % 10;
        skillBar.ClearSlot(row, col);
    }
}
