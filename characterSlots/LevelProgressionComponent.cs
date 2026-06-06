using Godot;

public partial class LevelProgressionComponent : Node
{
    [Signal] public delegate void ProgressaoAtualizadaEventHandler();
    [Signal] public delegate void SubiuDeLevelEventHandler(int novoLevel);

    private int _nivel = LevelProgressionUtil.NivelInicial;
    private int _experienciaAtual;

    public int Nivel => _nivel;
    public int ExperienciaAtual => _experienciaAtual;
    public int ExperienciaProximoLevel => LevelProgressionUtil.XpParaProximoLevel(_nivel);

    public float ProgressoXp
    {
        get
        {
            int necessaria = ExperienciaProximoLevel;
            return necessaria <= 0 ? 0f : Mathf.Clamp(_experienciaAtual / (float)necessaria, 0f, 1f);
        }
    }

    public void DefinirProgresso(int nivel, int experiencia)
    {
        _nivel = Mathf.Max(nivel, LevelProgressionUtil.NivelInicial);
        _experienciaAtual = Mathf.Max(experiencia, 0);
        EmitSignal(SignalName.ProgressaoAtualizada);
    }

    public void AdicionarExperiencia(int quantidade, EquipamentoComponent equipamento)
    {
        if (quantidade <= 0) return;

        float bonusPercent = equipamento?.BonusExperiencia ?? 0f;
        int xpFinal = quantidade + (int)(quantidade * bonusPercent / 100f);
        if (xpFinal <= 0) return;

        _experienciaAtual += xpFinal;

        bool subiu = false;
        int xpNecessaria = ExperienciaProximoLevel;
        while (xpNecessaria > 0 && _experienciaAtual >= xpNecessaria)
        {
            _experienciaAtual -= xpNecessaria;
            _nivel++;
            subiu = true;
            xpNecessaria = ExperienciaProximoLevel;

            equipamento?.AdicionarPontosDisponiveis(LevelProgressionUtil.PontosPorLevel);
            EmitSignal(SignalName.SubiuDeLevel, _nivel);
            GD.Print($"[LEVEL] ★ Nível {_nivel}! +{LevelProgressionUtil.PontosPorLevel} pontos de atributo.");
        }

        EmitSignal(SignalName.ProgressaoAtualizada);

        if (!subiu)
            GD.Print($"[LEVEL] +{xpFinal} XP ({_experienciaAtual}/{ExperienciaProximoLevel})");
    }
}
