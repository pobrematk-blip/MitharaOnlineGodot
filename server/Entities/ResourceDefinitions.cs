namespace Mithara.Server.Entities;

public class ResourceDrop
{
    public int ItemId { get; init; }
    public int QtyMin { get; init; } = 1;
    public int QtyMax { get; init; } = 1;
}

public class ResourceDefinition
{
    public string Name { get; init; } = "";
    public int RequiredToolItemId { get; init; }
    public float InteractionRange { get; init; } = 120f;
    public float CooldownSeconds { get; init; } = 30f;
    public List<ResourceDrop> Drops { get; init; } = new();
}

public static class ResourceDefinitions
{
    private static readonly Dictionary<string, ResourceDefinition> _byName =
        new(StringComparer.OrdinalIgnoreCase);

    static ResourceDefinitions()
    {
        Register(new ResourceDefinition
        {
            Name = "Arvore",
            RequiredToolItemId = 610,
            InteractionRange = 140f,
            CooldownSeconds = 60f,
            Drops = new()
            {
                new() { ItemId = 502, QtyMin = 2, QtyMax = 5 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "ArvoreDeMadeira",
            RequiredToolItemId = 610,
            InteractionRange = 140f,
            CooldownSeconds = 60f,
            Drops = new()
            {
                new() { ItemId = 502, QtyMin = 2, QtyMax = 5 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "Palmeira",
            RequiredToolItemId = 610,
            InteractionRange = 140f,
            CooldownSeconds = 60f,
            Drops = new()
            {
                new() { ItemId = 502, QtyMin = 2, QtyMax = 4 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "ErvaVital",
            RequiredToolItemId = 630,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 401, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "FlorDeMana",
            RequiredToolItemId = 631,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 402, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "FolhaEnergetica",
            RequiredToolItemId = 630,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 403, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "RaizRubra",
            RequiredToolItemId = 631,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 404, QtyMin = 1, QtyMax = 2 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "FlorArcana",
            RequiredToolItemId = 630,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 405, QtyMin = 1, QtyMax = 2 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "FragmentoDeFerro",
            RequiredToolItemId = 620,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 406, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "CristalArcano",
            RequiredToolItemId = 631,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 407, QtyMin = 1, QtyMax = 2 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "FolhaDoVento",
            RequiredToolItemId = 630,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 408, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "PenaDeFalcon",
            RequiredToolItemId = 630,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 409, QtyMin = 1, QtyMax = 2 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "TrevoMistico",
            RequiredToolItemId = 631,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 410, QtyMin = 1, QtyMax = 2 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "NoDePedra",
            RequiredToolItemId = 620,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 411, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "NoDeCarvao",
            RequiredToolItemId = 621,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 412, QtyMin = 1, QtyMax = 3 },
            }
        });

        Register(new ResourceDefinition
        {
            Name = "NoDeBronze",
            RequiredToolItemId = 621,
            InteractionRange = 100f,
            CooldownSeconds = 45f,
            Drops = new()
            {
                new() { ItemId = 413, QtyMin = 1, QtyMax = 3 },
            }
        });
    }

    private static void Register(ResourceDefinition def)
    {
        _byName[def.Name] = def;
    }

    public static ResourceDefinition? Get(string name)
    {
        return _byName.TryGetValue(name, out var def) ? def : null;
    }
}
