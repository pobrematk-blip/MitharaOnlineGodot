using Godot;
using System;

[GlobalClass]
public partial class PetResource : Resource
{
    [Export] public int PetID { get; set; }
    [Export] public string Nome { get; set; } = "";
    [Export] public string Descricao { get; set; } = "";
    [Export] public Texture2D Icone { get; set; }
    [Export] public Texture2D SpriteAtlas { get; set; }
    [Export] public TipoPet Tipo { get; set; } = TipoPet.Loot;

    [Export] public int Level { get; set; } = 1;
    [Export] public int HP { get; set; } = 50;
    [Export] public int Mana { get; set; } = 20;
    [Export] public int AttackDamage { get; set; } = 5;
    [Export] public int Forca { get; set; } = 1;
    [Export] public int Agilidade { get; set; } = 1;
    [Export] public int Destreza { get; set; } = 1;
    [Export] public int Inteligencia { get; set; } = 1;
    [Export] public float Speed { get; set; } = 300f;
    [Export] public float AttackRange { get; set; } = 60f;
    [Export] public float AttackCooldown { get; set; } = 0.8f;

    [Export] public float ColetaRange { get; set; } = 150f;
    [Export] public float GuardRange { get; set; } = 200f;
    [Export] public string AnimPrefix { get; set; } = "";
}

public enum TipoPet
{
    Loot,
    Combate
}
