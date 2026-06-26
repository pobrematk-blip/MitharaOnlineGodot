using Godot;
using System;
using System.Collections.Generic;
#nullable enable annotations

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

        if (_player == null)
            return;

        var gameNet = _player.GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[SKILLCOMP] Uso local de skill bloqueado. Conecte ao servidor.");
            return;
        }

        string dir = _player.CurrentDirection ?? "down";
        Vector2 dirVec = DirectionUtil.DirectionToVector(dir);
        gameNet.SendSkillUse(slotIndex, skill.SkillId, _player.GlobalPosition + dirVec * 50f);
        bool localSkillEffectsEnabled = false;
        if (!localSkillEffectsEnabled)
            return;

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
        if (item == null)
            return;

        int inventorySlot = ItemSlotIndexes != null && slotIndex < ItemSlotIndexes.Length ? ItemSlotIndexes[slotIndex] : -1;
        if (inventorySlot < 0)
        {
            GD.PrintErr("[SKILLCOMP] Slot de inventario do item da barra nao encontrado.");
            return;
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[SKILLCOMP] Sem conexao. Uso de item deve passar pelo servidor.");
            return;
        }

        gameNet.SendUseItem(inventorySlot);
        GD.Print($"[SKILLCOMP] Pedido ao servidor para usar '{item.Nome}' do inventario slot {inventorySlot}.");
    }


    private void TentarCapturarPet(Player player, ItemResource item)
    {
        GD.PrintErr("[SKILLCOMP] Captura local de pet bloqueada. Captura deve ser validada pelo servidor.");
        bool localPetCaptureEnabled = false;
        if (!localPetCaptureEnabled)
            return;

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

        if (alvo.PetID <= 0)
        {
            GD.Print("[SKILLCOMP] Este inimigo não pode ser capturado.");
            return;
        }

        string petNome = alvo.NomeDoInimigo;
        int petId = alvo.PetID;

        var miniGame = GD.Load<PackedScene>("res://ui/Pets/PetScrollMiniGame.tscn").Instantiate<PetScrollMiniGame>();
        var hud = GetNodeOrNull<CanvasLayer>("/root/main/HUD");
        if (hud == null)
        {
            var root = GetTree()?.Root;
            if (root != null)
            {
                for (int i = 0; i < root.GetChildCount(); i++)
                {
                    var h = root.GetChild(i).FindChild("HUD", true, false) as CanvasLayer;
                    if (h != null)
                    {
                        hud = h;
                        break;
                    }
                }
            }
        }
        if (hud != null)
            hud.AddChild(miniGame);

        miniGame.Connect(PetScrollMiniGame.SignalName.MiniGameConcluido, Callable.From((int capturedPetId, string capturedPetNome, bool sucesso) =>
        {
            if (sucesso && IsInstanceValid(alvo))
            {
                var colecao = _player?.FindChild("PetColecaoComponent", true, false) as PetColecaoComponent;
                if (colecao != null)
                    colecao.RegistrarCaptura(capturedPetId, capturedPetNome);

                var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
                if (gameNet != null && gameNet.IsConnected)
                    gameNet.SendPetCapture(capturedPetId, capturedPetNome);

                alvo.QueueFree();
                var chat = GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
                chat?.AddSystemMessage($"Pet '{capturedPetNome}' capturado com sucesso!");
                GD.Print($"[SKILLCOMP] Pet {capturedPetNome} capturado!");
            }

            if (IsInstanceValid(miniGame))
                miniGame.QueueFree();
        }));

        miniGame.IniciarMiniGame(petId, petNome);
    }

    private void TentarReviverAliado(Player player)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.Print("[SKILLCOMP] Sem conexão para reviver.");
            return;
        }

        var downedNodes = GetTree()?.GetNodesInGroup("PlayersDowned");
        if (downedNodes == null || downedNodes.Count == 0)
        {
            GD.Print("[SKILLCOMP] Nenhum aliado caído por perto.");
            return;
        }

        Node2D? nearest = null;
        float nearestDist = 200f;
        foreach (Node node in downedNodes)
        {
            if (node is Node2D n2d)
            {
                float d = player.GlobalPosition.DistanceTo(n2d.GlobalPosition);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = n2d;
                }
            }
        }

        if (nearest == null || !nearest.HasMeta("network_id"))
        {
            GD.Print("[SKILLCOMP] Nenhum aliado caído válido encontrado.");
            return;
        }

        ulong targetId = (ulong)nearest.GetMeta("network_id").AsInt64();
        GD.Print($"[SKILLCOMP] Revivendo aliado {targetId} (dist={nearestDist:F1})");
        gameNet.SendRevivePlayer(targetId);
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
