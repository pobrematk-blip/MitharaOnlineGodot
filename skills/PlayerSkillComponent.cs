using Godot;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    private GameNetwork _gameNet;

    public override void _Ready()
    {
        _player = GetParent() as Player;
        if (_player == null)
            GD.PrintErr("[SKILLCOMP] Player não encontrado como pai do componente de skills.");
        System.Array.Fill(ItemSlotIndexes, -1);
        CarregarCatalogoDeSkills();

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnSkillBarData += AplicarBarraServidor;
            if (_gameNet.PendingSkillBarData != null)
                AplicarBarraServidor(_gameNet.PendingSkillBarData);
        }
    }

    public override void _ExitTree()
    {
        if (_gameNet != null)
            _gameNet.OnSkillBarData -= AplicarBarraServidor;
        base._ExitTree();
    }

    public SkillResource ObterSkillPorId(int skillId)
    {
        return _skillCatalog.TryGetValue(skillId, out var skill) ? skill : null;
    }

    public void AplicarBarraServidor(Godot.Collections.Array<int> skillIds)
    {
        if (!IsInsideTree())
            return;

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
        AplicarFallbacksDoCatalogo();
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

    private void AplicarFallbacksDoCatalogo()
    {
        foreach (var grupo in _skillCatalog.Values
            .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Nome))
            .GroupBy(s => NormalizarNome(s.Nome)))
        {
            var modelo = grupo
                .OrderByDescending(PontuarSkillPreenchida)
                .FirstOrDefault();
            if (modelo == null)
                continue;

            foreach (var skill in grupo)
                PreencherCamposFaltantes(skill, modelo);
        }
    }

    private static int PontuarSkillPreenchida(SkillResource skill)
    {
        int score = 0;
        if (skill.Cooldown > 0) score += 10;
        if (skill.CustoMana > 0) score += 10;
        if (skill.Valor != 0) score += 8;
        if (skill.Duracao > 0) score += 6;
        if (!string.IsNullOrWhiteSpace(skill.DanoEscala)) score += 6;
        if (!string.IsNullOrWhiteSpace(skill.EfeitoPrincipal)) score += 5;
        if (!string.IsNullOrWhiteSpace(skill.BuffDebuff)) score += 4;
        if (!string.IsNullOrWhiteSpace(skill.Descricao)) score += 3;
        return score;
    }

    private static void PreencherCamposFaltantes(SkillResource skill, SkillResource modelo)
    {
        if (skill == null || modelo == null || ReferenceEquals(skill, modelo))
            return;

        if (skill.Cooldown <= 0f && modelo.Cooldown > 0f) skill.Cooldown = modelo.Cooldown;
        if (skill.CustoMana <= 0 && modelo.CustoMana > 0) skill.CustoMana = modelo.CustoMana;
        if (skill.Valor == 0 && modelo.Valor != 0) skill.Valor = modelo.Valor;
        if (skill.Duracao <= 0f && modelo.Duracao > 0f) skill.Duracao = modelo.Duracao;
        if (string.IsNullOrWhiteSpace(skill.Descricao) && !string.IsNullOrWhiteSpace(modelo.Descricao)) skill.Descricao = modelo.Descricao;
        if (string.IsNullOrWhiteSpace(skill.DanoEscala) && !string.IsNullOrWhiteSpace(modelo.DanoEscala)) skill.DanoEscala = modelo.DanoEscala;
        if (string.IsNullOrWhiteSpace(skill.EfeitoPrincipal) && !string.IsNullOrWhiteSpace(modelo.EfeitoPrincipal)) skill.EfeitoPrincipal = modelo.EfeitoPrincipal;
        if (string.IsNullOrWhiteSpace(skill.BuffDebuff) && !string.IsNullOrWhiteSpace(modelo.BuffDebuff)) skill.BuffDebuff = modelo.BuffDebuff;
        if (string.IsNullOrWhiteSpace(skill.DuracaoTexto) && !string.IsNullOrWhiteSpace(modelo.DuracaoTexto)) skill.DuracaoTexto = modelo.DuracaoTexto;
        if (string.IsNullOrWhiteSpace(skill.Progressao) && !string.IsNullOrWhiteSpace(modelo.Progressao)) skill.Progressao = modelo.Progressao;
        if (string.IsNullOrWhiteSpace(skill.Observacoes) && !string.IsNullOrWhiteSpace(modelo.Observacoes)) skill.Observacoes = modelo.Observacoes;
    }

    private static string NormalizarNome(string value)
    {
        string formD = (value ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (char c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Replace(" ", "");
    }
}
