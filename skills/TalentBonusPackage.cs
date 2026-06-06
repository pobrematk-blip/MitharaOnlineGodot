using Godot;

/// <summary>
/// Pacote de bônus aplicados por talentos desbloqueados
/// </summary>
public partial class TalentBonusPackage : Resource
{
    public int BonusForca { get; set; }
    public int BonusAgilidade { get; set; }
    public int BonusDestreza { get; set; }
    public int BonusInteligencia { get; set; }
    public float BonusDanoPercent { get; set; }
    public float BonusVelocidadePercent { get; set; }
    public float BonusVidaPercent { get; set; }
    public float BonusManaPercent { get; set; }

    public TalentBonusPackage() { }

    public override string ToString()
    {
        return $"[Bônus: F{BonusForca} A{BonusAgilidade} D{BonusDestreza} I{BonusInteligencia} " +
               $"Dano{BonusDanoPercent * 100}% Vel{BonusVelocidadePercent * 100}% " +
               $"Vida{BonusVidaPercent * 100}% Mana{BonusManaPercent * 100}%]";
    }
}
