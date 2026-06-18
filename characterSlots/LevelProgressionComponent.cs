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
        GD.PrintErr("[LEVEL] XP local bloqueado. Progresso deve vir do servidor.");
    }
}
