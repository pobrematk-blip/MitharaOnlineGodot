namespace Mithara.Server.Entities;

public enum EntityType : byte
{
    Player = 0,
    Monster = 1,
    Boss = 2,
    NPC = 3,
    Pet = 4,
}

public class PetEntity : Entity
{
    public ulong OwnerEntityId { get; set; }
    public int PetId { get; set; }
    public string AnimPrefix { get; set; } = "";
    public string OwnerName { get; set; } = "";

    public PetEntity()
    {
        Type = EntityType.Pet;
        Speed = 260f;
        MaxHealth = 1;
        Health = 1;
        MaxMana = 0;
        Mana = 0;
    }
}

public class Entity
{
    public ulong Id { get; set; }
    public EntityType Type { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public float Speed { get; set; }
    public int Level { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }
    public int Forca { get; set; }
    public int Agilidade { get; set; }
    public int Destreza { get; set; }
    public int Inteligencia { get; set; }
    public long Experience { get; set; }
    public bool Moving { get; set; }
    public bool Sprinting { get; set; }
    public double LastMoveTime { get; set; }
    public string FactionId { get; set; } = "";
    public double LastCombatTime { get; set; }

    public int GridCellX => (int)Math.Floor(X / World.SpatialGrid.CellSize);
    public int GridCellY => (int)Math.Floor(Y / World.SpatialGrid.CellSize);
}
