namespace Mithara.Server.Entities;

public class NPCEntity : Entity
{
    public string PrefabId { get; set; } = "";
    public string DialogId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string Race { get; set; } = "";
    public string AnimPrefix { get; set; } = "";

    public NPCEntity()
    {
        Type = EntityType.NPC;
        Health = 1;
        MaxHealth = 1;
    }
}
