using Godot;
using System;
using System.Collections.Generic;
#nullable enable annotations

public partial class PlayerSkillComponent
{
    private const int LancaDeGeloSkillId = 11202;
    private const int TornadoSkillId = 11204;
    private const int TempestadeEletricaSkillId = 11206;
    private const int ExplosaoVulcanicaSkillId = 11207;
    private const int ChuvaMeteorosSkillId = 11209;
    private const int CuraEmAreaSkillId = 15103;
    private const int CarnificinaSkillId = 13107;
    private const int RessurreicaoSkillId = 15108;
    private const float LancaDeGeloMarkerRadius = 120f;
    private const float CuraEmAreaMarkerRadius = 32f * 10f;

    private GroundTargetMarker? _groundTargetMarker;
    private int _pendingGroundSkillSlot = -1;
    private SkillResource? _pendingGroundSkill;
    private float _pendingGroundSkillCharge = 1f;

    // Public activation entrypoint used by the UI.
    public void ActivateSlotIndex(int slotIndex)
    {
        ActivateSlotIndex(slotIndex, 1f);
    }

    public void ActivateSlotIndex(int slotIndex, float chargePercent)
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

        if (!TemManaParaAtivar(skill))
            return;

        if (EhSkillComMiraNoChao(skill.SkillId))
        {
            PrepararOuUsarSkillComMiraNoChao(slotIndex, skill, chargePercent);
            return;
        }

        string dir = _player.CurrentDirection ?? "down";
        Vector2 dirVec = DirectionUtil.DirectionToVector(dir);
        Vector2 targetPosition = _player.GlobalPosition + dirVec * 50f;
        bool isTiroExecucao = skill.SkillId == 10209 || skill.SkillId == 19 || string.Equals(skill.Nome?.Trim(), "Tiro da Execução", StringComparison.OrdinalIgnoreCase);
        bool isMarcaDaMorte = skill.SkillId == 12206
            || skill.SkillId == 13202
            || (skill.Nome?.Contains("Marca", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Morte", StringComparison.OrdinalIgnoreCase));
        bool isExecucaoFinal = skill.SkillId == 12209
            || (skill.Nome?.Contains("Execu", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Final", StringComparison.OrdinalIgnoreCase));
        bool isGolpeSombrio = skill.SkillId == 12201
            || (skill.Nome?.Contains("Golpe", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Sombrio", StringComparison.OrdinalIgnoreCase));
        bool isGolpeAtordoante = skill.SkillId == 12204
            || (skill.Nome?.Contains("Golpe", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Atordoante", StringComparison.OrdinalIgnoreCase));
        bool isChuteNaVirilha = skill.SkillId == 12210
            || (skill.Nome?.Contains("Chute", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Virilha", StringComparison.OrdinalIgnoreCase));
        bool isPunhaladaNasCostas = skill.SkillId == 12205
            || (skill.Nome?.Contains("Punhalada", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Costas", StringComparison.OrdinalIgnoreCase));
        bool isGolpeDeEscudo = skill.SkillId == 14103
            || (skill.Nome?.Contains("Golpe", StringComparison.OrdinalIgnoreCase) == true
                && skill.Nome.Contains("Escudo", StringComparison.OrdinalIgnoreCase));
        bool isEstocada = skill.SkillId == 14102
            || (skill.Nome?.Contains("Estocada", StringComparison.OrdinalIgnoreCase) == true);
        bool isSacerdoteDano = skill.SkillId == 15001 || skill.SkillId == 15102;
        bool isSangramentoMortal = skill.SkillId == 13001
            || string.Equals(skill.Nome?.Trim(), "Sangramento Mortal", StringComparison.OrdinalIgnoreCase);
        if (isTiroExecucao || isMarcaDaMorte || isExecucaoFinal || isGolpeSombrio || isGolpeAtordoante || isChuteNaVirilha || isPunhaladaNasCostas || isGolpeDeEscudo || isEstocada || isSacerdoteDano || isSangramentoMortal)
        {
            float range = isExecucaoFinal || isGolpeSombrio || isGolpeAtordoante || isChuteNaVirilha || isPunhaladaNasCostas
                ? 96f
                : (isGolpeDeEscudo ? 32f * 10f : (isEstocada ? 32f * 5f : (isSacerdoteDano ? 32f * 8f : (isMarcaDaMorte || isSangramentoMortal ? 32f * 10f : 900f))));
            if (!_player.TryEnsureSelectedEnemyTarget(range, out _, out targetPosition))
                GD.Print($"[SKILLCOMP] {skill.Nome} sem alvo: nenhum inimigo proximo encontrado.");
        }
        else if (skill.TargetType == SkillTargetType.Ally
            && _player.TryGetSelectedCombatTarget(out var selectedAllyNode)
            && selectedAllyNode != null
            && GodotObject.IsInstanceValid(selectedAllyNode)
            && selectedAllyNode.HasMeta("player_name"))
        {
            targetPosition = selectedAllyNode.GlobalPosition;
        }
        else if (skill.TargetType == SkillTargetType.Enemy && _player.TryGetSelectedTargetPosition(out var selectedTargetPosition))
        {
            targetPosition = selectedTargetPosition;
        }

        if (skill.SkillId == 10002)
        {
            bool tocouSalto = _player.TocarAnimacaoSaltoParaTrasArqueiro(targetPosition);
            if (!tocouSalto)
                _player.TocarAnimacaoSkillArqueiro(targetPosition);
        }
        else if (skill.SkillId == 10201 || skill.SkillId == 10203 || skill.SkillId == 10206 || skill.SkillId == 16 || skill.SkillId == 10209 || skill.SkillId == 19)
        {
            _player.FinalizarCarregamentoTiroPreciso(targetPosition);
        }
        else if (isExecucaoFinal || isGolpeSombrio || isGolpeAtordoante || isChuteNaVirilha || isPunhaladaNasCostas || isGolpeDeEscudo || isEstocada || isSangramentoMortal)
        {
            _player.TocarAnimacaoSkillMelee(targetPosition, 1.2f);
        }
        else
        {
            _player.TocarAnimacaoSkillArqueiro(targetPosition);
        }

        gameNet.SendSkillUse(slotIndex, skill.SkillId, targetPosition, chargePercent);
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

    public override void _Process(double delta)
    {
        if (_groundTargetMarker != null && IsInstanceValid(_groundTargetMarker) && _player != null)
            _groundTargetMarker.GlobalPosition = _player.GetGlobalMousePosition();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_pendingGroundSkill == null || _groundTargetMarker == null || !IsInstanceValid(_groundTargetMarker))
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                ConfirmarGroundTarget();
                GetViewport()?.SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                CancelarGroundTarget();
                GetViewport()?.SetInputAsHandled();
            }
        }
        else if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            CancelarGroundTarget();
            GetViewport()?.SetInputAsHandled();
        }
    }

    private void PrepararOuUsarSkillComMiraNoChao(int slotIndex, SkillResource skill, float chargePercent)
    {
        if (_player == null)
            return;

        var gameNet = _player.GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
            return;

        bool usarTargetDireto = skill.SkillId != CarnificinaSkillId
            && skill.SkillId != RessurreicaoSkillId
            && skill.SkillId != CuraEmAreaSkillId;
        if (usarTargetDireto
            && _player.TryGetSelectedCombatTarget(out var selectedTargetNode)
            && IsInstanceValid(selectedTargetNode)
            && selectedTargetNode is Inimigo)
        {
            Vector2 targetPosition = selectedTargetNode.GlobalPosition;
            _player.TocarAnimacaoSkillArqueiro(targetPosition);
            gameNet.SendSkillUse(slotIndex, skill.SkillId, targetPosition, chargePercent);
            CancelarGroundTarget();
            return;
        }

        _pendingGroundSkillSlot = slotIndex;
        _pendingGroundSkill = skill;
        _pendingGroundSkillCharge = chargePercent;
        MostrarGroundTargetMarker();
        GD.Print($"[SKILLCOMP] {skill.Nome}: clique no chão para escolher a área.");
    }

    private static bool EhSkillComMiraNoChao(int skillId)
    {
        return skillId == LancaDeGeloSkillId
            || skillId == TornadoSkillId
            || skillId == TempestadeEletricaSkillId
            || skillId == ExplosaoVulcanicaSkillId
            || skillId == ChuvaMeteorosSkillId
            || skillId == CuraEmAreaSkillId
            || skillId == CarnificinaSkillId
            || skillId == RessurreicaoSkillId;
    }

    private void ConfirmarGroundTarget()
    {
        if (_player == null || _pendingGroundSkill == null || _pendingGroundSkillSlot < 0)
        {
            CancelarGroundTarget();
            return;
        }

        var gameNet = _player.GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            CancelarGroundTarget();
            return;
        }

        Vector2 targetPosition = _groundTargetMarker != null && IsInstanceValid(_groundTargetMarker)
            ? _groundTargetMarker.GlobalPosition
            : _player.GetGlobalMousePosition();

        if (_pendingGroundSkill.SkillId != CarnificinaSkillId)
            _player.TocarAnimacaoSkillArqueiro(targetPosition);
        gameNet.SendSkillUse(_pendingGroundSkillSlot, _pendingGroundSkill.SkillId, targetPosition, _pendingGroundSkillCharge);
        CancelarGroundTarget();
    }

    private void MostrarGroundTargetMarker()
    {
        if (_groundTargetMarker != null && IsInstanceValid(_groundTargetMarker))
            _groundTargetMarker.QueueFree();

        _groundTargetMarker = new GroundTargetMarker
        {
            Radius = ObterRaioMarcacaoNoChao(_pendingGroundSkill?.SkillId ?? 0),
            FillColor = ObterCorMarcacaoNoChao(_pendingGroundSkill?.SkillId ?? 0, preenchimento: true),
            LineColor = ObterCorMarcacaoNoChao(_pendingGroundSkill?.SkillId ?? 0, preenchimento: false),
            GlobalPosition = _player?.GetGlobalMousePosition() ?? Vector2.Zero,
            ZIndex = 90,
            ZAsRelative = false,
        };

        Node? parent = GetTree()?.CurrentScene?.FindChild("World", true, false) ?? GetTree()?.CurrentScene;
        parent?.AddChild(_groundTargetMarker);
    }

    private static float ObterRaioMarcacaoNoChao(int skillId)
    {
        return skillId == CuraEmAreaSkillId ? CuraEmAreaMarkerRadius : LancaDeGeloMarkerRadius;
    }

    private static Color ObterCorMarcacaoNoChao(int skillId, bool preenchimento)
    {
        if (skillId == CuraEmAreaSkillId)
            return preenchimento
                ? new Color(1f, 0.82f, 0.05f, 0.18f)
                : new Color(1f, 0.88f, 0.12f, 0.95f);

        return preenchimento
            ? new Color(1f, 0.05f, 0.05f, 0.16f)
            : new Color(1f, 0.05f, 0.05f, 0.95f);
    }

    private void CancelarGroundTarget()
    {
        if (_groundTargetMarker != null && IsInstanceValid(_groundTargetMarker))
            _groundTargetMarker.QueueFree();

        _groundTargetMarker = null;
        _pendingGroundSkillSlot = -1;
        _pendingGroundSkill = null;
        _pendingGroundSkillCharge = 1f;
    }

    private sealed partial class GroundTargetMarker : Node2D
    {
        public float Radius { get; set; } = 120f;
        public Color FillColor { get; set; } = new(1f, 0.05f, 0.05f, 0.16f);
        public Color LineColor { get; set; } = new(1f, 0.05f, 0.05f, 0.95f);

        public override void _Draw()
        {
            var softLine = new Color(LineColor.R, LineColor.G, LineColor.B, 0.52f);
            DrawCircle(Vector2.Zero, Radius, FillColor);
            DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 128, LineColor, 3f, true);
            DrawArc(Vector2.Zero, Radius * 0.62f, 0f, Mathf.Tau, 128, softLine, 2f, true);
            DrawLine(new Vector2(-Radius, 0), new Vector2(Radius, 0), softLine, 1.5f, true);
            DrawLine(new Vector2(0, -Radius), new Vector2(0, Radius), softLine, 1.5f, true);
        }
    }

    private bool TemManaParaAtivar(SkillResource skill)
    {
        if (skill == null || skill.CustoMana <= 0 || _player == null)
            return true;

        if (_player.CurrentMana >= skill.CustoMana)
            return true;

        string nome = string.IsNullOrWhiteSpace(skill.Nome) ? "skill" : skill.Nome;
        string message = $"Mana insuficiente para usar {nome}.";
        var chat = _player.GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
        if (chat == null)
            chat = _player.GetNodeOrNull<ChatUI>("/root/Main/HUD/ChatUI");

        if (chat != null)
            chat.AddSystemMessage(message);
        else
            GameNetwork.Log(message);

        GD.Print($"[SKILLCOMP] {message} Mana atual={_player.CurrentMana}, custo={skill.CustoMana}");
        return false;
    }

    private Godot.Timer _skillTimer;

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

        _skillTimer = new Godot.Timer();
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
            inventorySlot = EncontrarSlotInventarioDoItem(item.ItemID);
            if (ItemSlotIndexes != null && slotIndex < ItemSlotIndexes.Length)
                ItemSlotIndexes[slotIndex] = inventorySlot;
        }

        if (inventorySlot < 0)
        {
            GD.PrintErr($"[SKILLCOMP] Consumivel '{item.Nome}' nao encontrado no inventario. Limpando atalho local.");
            ItemSlots[slotIndex] = null;
            if (ItemSlotIndexes != null && slotIndex < ItemSlotIndexes.Length)
                ItemSlotIndexes[slotIndex] = -1;
            NotificarSkillBarSlotLimpo(slotIndex);
            return;
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[SKILLCOMP] Sem conexao. Uso de item deve passar pelo servidor.");
            return;
        }

        if (item.ItemID == 100 || item.ItemID == 114)
        {
            int maxTent = item.ItemID == 114 ? 5 : 7;
            TentarCapturarPet(_player, inventorySlot, maxTent);
            return;
        }

        gameNet.SendUseItem(inventorySlot);
        GD.Print($"[SKILLCOMP] Pedido ao servidor para usar '{item.Nome}' do inventario slot {inventorySlot}.");
    }

    private int EncontrarSlotInventarioDoItem(int itemId)
    {
        var inventario = _player?.FindChild("InventarioComponent", true, false) as InventarioComponent
            ?? GetTree()?.CurrentScene?.FindChild("InventarioComponent", true, false) as InventarioComponent;
        if (inventario?.Slots == null)
            return -1;

        for (int i = 0; i < inventario.Slots.Count; i++)
        {
            var slot = inventario.Slots[i];
            if (slot?.Item != null && slot.Item.ItemID == itemId && slot.Quantidade > 0)
                return i;
        }

        return -1;
    }


    private void TentarCapturarPet(Player player, int inventorySlot, int maxTent)
    {
        if (player == null)
        {
            GD.PrintErr("[SKILLCOMP] Player nao encontrado para capturar pet.");
            return;
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[SKILLCOMP] Sem conexao com o servidor para capturar pet.");
            return;
        }

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
        else
            GetTree().CurrentScene?.AddChild(miniGame);

        miniGame.Connect(PetScrollMiniGame.SignalName.MiniGameConcluido, Callable.From((int capturedPetId, string capturedPetNome, bool sucesso) =>
        {
            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet != null && gameNet.IsConnected)
                gameNet.SendPetCapture(capturedPetId, capturedPetNome, inventorySlot, sucesso);

            if (sucesso && IsInstanceValid(alvo))
            {
                alvo.QueueFree();
                var chat = GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
                if (chat == null)
                    chat = GetNodeOrNull<ChatUI>("/root/Main/HUD/ChatUI");
                chat?.AddSystemMessage($"Pet '{capturedPetNome}' capturado com sucesso!");
                GD.Print($"[SKILLCOMP] Pet {capturedPetNome} capturado!");
            }

            if (IsInstanceValid(miniGame))
                miniGame.QueueFree();
        }));

        miniGame.IniciarMiniGame(petId, petNome, maxTent);
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
