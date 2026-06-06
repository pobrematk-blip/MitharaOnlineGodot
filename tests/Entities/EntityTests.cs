using Mithara.Server.Entities;

namespace Mithara.Server.Tests.Entities;

public class EntityTests
{
    [Fact]
    public void Entity_DefaultValues()
    {
        var e = new Entity();

        Assert.Equal(0ul, e.Id);
        Assert.Equal(EntityType.Player, e.Type);
        Assert.Equal("", e.Name);
        Assert.Equal(0f, e.X);
        Assert.Equal(0f, e.Y);
        Assert.Equal(0, e.Level);
        Assert.Equal(0, e.Health);
        Assert.Equal(0, e.MaxHealth);
        Assert.False(e.Moving);
    }

    [Fact]
    public void Entity_GridCell_CalculatedCorrectly()
    {
        var e = new Entity { X = 150f, Y = 450f };

        Assert.Equal(0, e.GridCellX);
        Assert.Equal(2, e.GridCellY);
    }

    [Fact]
    public void Entity_GridCell_NegativeCoordinates()
    {
        var e = new Entity { X = -50f, Y = -250f };

        Assert.Equal(-1, e.GridCellX);
        Assert.Equal(-2, e.GridCellY);
    }
}

public class PlayerEntityTests
{
    [Fact]
    public void PlayerEntity_DefaultType_IsPlayer()
    {
        var p = new PlayerEntity();
        Assert.Equal(EntityType.Player, p.Type);
    }

    [Fact]
    public void PlayerEntity_PartyAndGuild_DefaultToNegative()
    {
        var p = new PlayerEntity();

        Assert.Equal(-1, p.PartyId);
        Assert.Equal(-1, p.GuildId);
        Assert.Equal("", p.GuildName);
    }

    [Fact]
    public void CalculateAttackDamage_BaseFormula()
    {
        var p = new PlayerEntity { BaseAttack = 10, Forca = 5 };

        var damage = p.CalculateAttackDamage();

        Assert.Equal(20, damage);
    }

    [Fact]
    public void CalculateAttackDamage_MinimumOne()
    {
        var p = new PlayerEntity { BaseAttack = 0, Forca = -10 };

        var damage = p.CalculateAttackDamage();

        Assert.Equal(1, damage);
    }

    [Fact]
    public void CalculateDefense_Formulas()
    {
        var p = new PlayerEntity { Defense = 15, Agilidade = 5 };

        var def = p.CalculateDefense();

        Assert.Equal(20, def);
    }

    [Fact]
    public void FindEmptyInventorySlot_NoItems_ReturnsZero()
    {
        var p = new PlayerEntity();

        var slot = p.FindEmptyInventorySlot();

        Assert.Equal(0, slot);
    }

    [Fact]
    public void FindEmptyInventorySlot_WithItems_ReturnsFirstGap()
    {
        var p = new PlayerEntity();
        p.Items.Add(new ItemInstance { Slot = 0 });
        p.Items.Add(new ItemInstance { Slot = 1 });
        p.Items.Add(new ItemInstance { Slot = 3 });

        var slot = p.FindEmptyInventorySlot();

        Assert.Equal(2, slot);
    }

    [Fact]
    public void FindEmptyInventorySlot_FullInventory_ReturnsNegative()
    {
        var p = new PlayerEntity();
        for (int i = 0; i < 40; i++)
            p.Items.Add(new ItemInstance { Slot = i });

        var slot = p.FindEmptyInventorySlot();

        Assert.Equal(-1, slot);
    }

    [Fact]
    public void Equipment_Dictionary_StoresItemsBySlot()
    {
        var p = new PlayerEntity();
        var weapon = new ItemInstance { ItemId = 10, Slot = 7 };

        p.Equipment[7] = weapon;

        Assert.Single(p.Equipment);
        Assert.Equal(10, p.Equipment[7].ItemId);
    }
}

public class MonsterEntityTests
{
    [Fact]
    public void MonsterEntity_DefaultType_IsMonster()
    {
        var m = new MonsterEntity();
        Assert.Equal(EntityType.Monster, m.Type);
    }

    [Fact]
    public void MonsterEntity_DefaultValues()
    {
        var m = new MonsterEntity();

        Assert.False(m.IsBoss);
        Assert.Equal(0, m.ExperienceReward);
        Assert.Equal("", m.PrefabId);
        Assert.Equal(0, m.AttackDamage);
        Assert.Equal(40f, m.AttackRange);
        Assert.Equal(300f, m.AggroRange);
        Assert.Equal(1.5f, m.AttackCooldown);
        Assert.Null(m.TargetEntityId);
    }

    [Fact]
    public void CalculateAttackDamage_WithForca()
    {
        var m = new MonsterEntity { AttackDamage = 15, Forca = 3 };

        var damage = m.CalculateAttackDamage();

        Assert.Equal(18, damage);
    }

    [Fact]
    public void CalculateAttackDamage_MinimumOne()
    {
        var m = new MonsterEntity { AttackDamage = 0, Forca = -5 };

        var damage = m.CalculateAttackDamage();

        Assert.Equal(1, damage);
    }

    [Fact]
    public void CalculateDefense_UsesAgilidade()
    {
        var m = new MonsterEntity { Agilidade = 8 };

        var def = m.CalculateDefense();

        Assert.Equal(8, def);
    }
}

public class ItemInstanceTests
{
    [Fact]
    public void ItemInstance_Definition_ReturnsItem()
    {
        var item = new ItemInstance { ItemId = 10 };

        var def = item.Definition;

        Assert.NotNull(def);
        Assert.Equal("Espada Curta", def!.Name);
        Assert.Equal(ItemType.Weapon, def.Type);
    }

    [Fact]
    public void ItemInstance_Definition_UnknownItem_ReturnsNull()
    {
        var item = new ItemInstance { ItemId = 999 };

        var def = item.Definition;

        Assert.Null(def);
    }

    [Fact]
    public void ItemInstance_Name_FromDefinition()
    {
        var item = new ItemInstance { ItemId = 1 };

        var name = item.Name;

        Assert.Equal("Poção de Vida", name);
    }

    [Fact]
    public void ItemInstance_Name_UnknownItem_Fallback()
    {
        var item = new ItemInstance { ItemId = 999 };

        var name = item.Name;

        Assert.Equal("Item#999", name);
    }

    [Fact]
    public void ItemInstance_Type_FromDefinition()
    {
        var item = new ItemInstance { ItemId = 20 };

        var type = item.Type;

        Assert.Equal(ItemType.Helmet, type);
    }

    [Fact]
    public void ItemInstance_IsStackable_Consumable()
    {
        var item = new ItemInstance { ItemId = 1 };

        Assert.True(item.IsStackable);
        Assert.Equal(99, item.MaxStack);
    }

    [Fact]
    public void ItemInstance_IsStackable_Equipment()
    {
        var item = new ItemInstance { ItemId = 10 };

        Assert.False(item.IsStackable);
        Assert.Equal(1, item.MaxStack);
    }
}

public class ItemDefinitionTests
{
    [Fact]
    public void ItemDefinitions_Get_ExistingItem()
    {
        var def = ItemDefinitions.Get(1);

        Assert.NotNull(def);
        Assert.Equal("Poção de Vida", def!.Name);
        Assert.Equal(ItemType.Consumable, def.Type);
    }

    [Fact]
    public void ItemDefinitions_Get_NonExistentItem_ReturnsNull()
    {
        var def = ItemDefinitions.Get(9999);

        Assert.Null(def);
    }

    [Fact]
    public void ItemDefinitions_Exists_ChecksCorrectly()
    {
        Assert.True(ItemDefinitions.Exists(1));
        Assert.True(ItemDefinitions.Exists(10));
        Assert.True(ItemDefinitions.Exists(31));
        Assert.False(ItemDefinitions.Exists(999));
    }

    [Fact]
    public void ItemDefinitions_Register_AddsNewItem()
    {
        ItemDefinitions.Register(new ItemDefinition
        {
            Id = 100,
            Name = "Item de Teste",
            Type = ItemType.Material,
        });

        var def = ItemDefinitions.Get(100);
        Assert.NotNull(def);
        Assert.Equal("Item de Teste", def!.Name);
        Assert.Equal(ItemType.Material, def.Type);
    }

    [Fact]
    public void ItemDefinitions_AllRegisteredItems()
    {
        Assert.True(ItemDefinitions.Exists(1));
        Assert.True(ItemDefinitions.Exists(2));
        Assert.True(ItemDefinitions.Exists(3));
        Assert.True(ItemDefinitions.Exists(10));
        Assert.True(ItemDefinitions.Exists(11));
        Assert.True(ItemDefinitions.Exists(12));
        Assert.True(ItemDefinitions.Exists(13));
        Assert.True(ItemDefinitions.Exists(20));
        Assert.True(ItemDefinitions.Exists(21));
        Assert.True(ItemDefinitions.Exists(22));
        Assert.True(ItemDefinitions.Exists(23));
        Assert.True(ItemDefinitions.Exists(24));
        Assert.True(ItemDefinitions.Exists(30));
        Assert.True(ItemDefinitions.Exists(31));
    }

    [Fact]
    public void ItemDefinition_BagProperties()
    {
        var def = ItemDefinitions.Get(3);

        Assert.NotNull(def);
        Assert.True(def!.IsBag);
        Assert.Equal(6, def.ExtraSlots);
    }

    [Fact]
    public void ItemDefinition_WeaponStats()
    {
        var shortSword = ItemDefinitions.Get(10);
        var longSword = ItemDefinitions.Get(11);
        var staff = ItemDefinitions.Get(12);

        Assert.Equal(5, shortSword!.BaseAttack);
        Assert.Equal(10, longSword!.BaseAttack);
        Assert.Equal(3, staff!.BaseAttack);
        Assert.Equal(5, staff.Inteligencia);
    }

    [Fact]
    public void ItemDefinition_ArmorStats()
    {
        var helmet = ItemDefinitions.Get(20);
        var chest = ItemDefinitions.Get(21);
        var boots = ItemDefinitions.Get(23);

        Assert.Equal(3, helmet!.Defense);
        Assert.Equal(5, chest!.Defense);
        Assert.Equal(2, boots!.Defense);
        Assert.Equal(2, boots.Agilidade);
    }
}
