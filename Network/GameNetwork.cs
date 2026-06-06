#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;
using System.Collections.Generic;

public partial class GameNetwork : Node
{
    public static bool AutoLogin = false;

    private NetClient? _client;
    private readonly Dictionary<ulong, Node2D> _entities = new();

    public int AccountId { get; private set; }
    public bool LoggedIn { get; private set; }
    public List<CharacterEntry> Characters { get; } = new();
    public ulong LocalPlayerId { get; private set; }
    public int LocalChannelId { get; private set; }
    public int Gold { get; set; }

    [Signal] public delegate void OnConnectedEventHandler();
    [Signal] public delegate void OnDisconnectedEventHandler();
    [Signal] public delegate void OnLoginResultEventHandler(bool success, string message);
    [Signal] public delegate void OnRegisterResultEventHandler(bool success, string message);
    [Signal] public delegate void OnSecurityQuestionEventHandler(bool found, string questionOrError);
    [Signal] public delegate void OnRecoverResultEventHandler(bool success, string message);
    [Signal] public delegate void OnCharacterListEventHandler(Godot.Collections.Array<Godot.Collections.Dictionary> characters);
    [Signal] public delegate void OnEnterWorldEventHandler();
    [Signal] public delegate void OnEntitySpawnedEventHandler(ulong entityId, string entityType, string name, float x, float y, int level, int health, int maxHealth, string extraData1, string extraData2, string extraData3);
    [Signal] public delegate void OnChatMessageEventHandler(byte channel, string senderName, string message, string language);
    [Signal] public delegate void OnCombatResultEventHandler(ulong attackerId, ulong targetId, int damage, bool isCrit, int targetHealth, int targetMaxHealth);
    [Signal] public delegate void OnEntityDiedEventHandler(ulong entityId, ulong killerId);
    [Signal] public delegate void OnGainExpEventHandler(ulong entityId, int amount, long totalExp);
    [Signal] public delegate void OnLevelUpEventHandler(ulong entityId, int newLevel);
    [Signal] public delegate void OnInventoryDataEventHandler(Godot.Collections.Array<Godot.Collections.Dictionary> items, Godot.Collections.Array<Godot.Collections.Dictionary> equipment);
    [Signal] public delegate void OnEquipUpdateEventHandler(int equipSlot, int itemId, int quantity, bool hasUnequip, int invSlot, int unequipItemId, int unequipQuantity);

    [Signal] public delegate void OnItemUpdateEventHandler(int slot, int itemId, int quantity);

    [Signal] public delegate void OnPartyDataEventHandler(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members);
    [Signal] public delegate void OnPartyMemberUpdateEventHandler(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined);
    [Signal] public delegate void OnPartyLeaderUpdateEventHandler(ulong newLeaderId);

    [Signal] public delegate void OnGuildDataEventHandler(int guildId, string guildName, Godot.Collections.Array<Godot.Collections.Dictionary> members, int level, int xp, int skillPoints, Godot.Collections.Array<Godot.Collections.Dictionary> skills);
    [Signal] public delegate void OnGuildMemberUpdateEventHandler(ulong entityId, string name, int rank, bool joined);
    [Signal] public delegate void OnGuildRankUpdateEventHandler(ulong entityId, int newRank);
    [Signal] public delegate void OnGuildSkillUpdateEventHandler(string skillId, int newLevel);

    [Signal] public delegate void OnLootSpawnEventHandler(ulong lootId, float x, float y, int itemId, int quantity);
    [Signal] public delegate void OnLootDespawnEventHandler(ulong lootId);
    [Signal] public delegate void OnGoldUpdateEventHandler(int gold);

    public new bool IsConnected => _client?.IsConnected ?? false;
    public int ServerPing => _client?.Ping ?? 0;

    public override void _Ready()
    {
        _client = new NetClient();
        AddChild(_client);

        var entityManager = new EntityManager();
        entityManager.Name = "EntityManager";
        AddChild(entityManager);

        ItemDB = new ItemDatabase();
        ItemDB.Name = "ItemDatabase";
        AddChild(ItemDB);
        _client.Connected += () =>
        {
            GD.Print("[GAME] Conectado ao servidor!");
            if (AutoLogin)
            {
                GD.Print("[GAME] Auto-login...");
                SendLogin("teste", "123456");
            }
            EmitSignal(SignalName.OnConnected);
        };
        _client.Disconnected += (info) =>
        {
            GD.Print($"[GAME] Desconectado: {info.Reason}");
            LoggedIn = false;
            EmitSignal(SignalName.OnDisconnected);
        };
        _client.PacketReceived += OnPacket;

        if (AutoLogin)
            CallDeferred(nameof(ConnectToServer));
    }

    public void ConnectToServer(string host = "127.0.0.1", int port = 7777)
    {
        _client?.ConnectToServer(host, port);
    }

    public void DisconnectFromServer()
    {
        _client?.DisconnectFromServer();
    }

    private void OnPacket(PacketId id, NetDataReader r)
    {
        switch (id)
        {
            case PacketId.S2C_LoginResult:
                HandleLoginResult(r);
                break;
            case PacketId.S2C_RegisterResult:
                HandleRegisterResult(r);
                break;
            case PacketId.S2C_CharacterList:
                HandleCharacterList(r);
                break;
            case PacketId.S2C_EnterWorld:
                HandleEnterWorld(r);
                break;
            case PacketId.S2C_SpawnEntity:
                HandleSpawnEntity(r);
                break;
            case PacketId.S2C_DespawnEntity:
                HandleDespawnEntity(r);
                break;
            case PacketId.S2C_EntityMove:
                HandleEntityMove(r);
                break;
            case PacketId.S2C_EntityUpdate:
                HandleEntityUpdate(r);
                break;
            case PacketId.S2C_Chat:
                HandleChat(r);
                break;
            case PacketId.S2C_ChannelList:
                HandleChannelList(r);
                break;
            case PacketId.S2C_SecurityQuestion:
                HandleSecurityQuestion(r);
                break;
            case PacketId.S2C_CombatResult:
                HandleCombatResult(r);
                break;
            case PacketId.S2C_EntityDied:
                HandleEntityDied(r);
                break;
            case PacketId.S2C_GainExp:
                HandleGainExp(r);
                break;
            case PacketId.S2C_InventoryData:
                HandleInventoryData(r);
                break;
            case PacketId.S2C_EquipUpdate:
                HandleEquipUpdate(r);
                break;
            case PacketId.S2C_ItemUpdate:
                HandleItemUpdate(r);
                break;
            case PacketId.S2C_LevelUp:
                HandleLevelUp(r);
                break;
            case PacketId.S2C_RecoverResult:
                HandleRecoverResult(r);
                break;
            case PacketId.S2C_PartyData:
                HandlePartyData(r);
                break;
            case PacketId.S2C_PartyMemberUpdate:
                HandlePartyMemberUpdate(r);
                break;
            case PacketId.S2C_PartyLeaderUpdate:
                HandlePartyLeaderUpdate(r);
                break;
            case PacketId.S2C_GuildData:
                HandleGuildData(r);
                break;
            case PacketId.S2C_GuildMemberUpdate:
                HandleGuildMemberUpdate(r);
                break;
            case PacketId.S2C_GuildRankUpdate:
                HandleGuildRankUpdate(r);
                break;
            case PacketId.S2C_GuildSkillUpdate:
                HandleGuildSkillUpdate(r);
                break;
            case PacketId.S2C_LootSpawn:
                HandleLootSpawn(r);
                break;
            case PacketId.S2C_LootDespawn:
                HandleLootDespawn(r);
                break;
            case PacketId.S2C_NpcDialog:
                HandleNpcDialog(r);
                break;
            case PacketId.S2C_NpcShopItems:
                HandleNpcShopItems(r);
                break;
            case PacketId.S2C_NpcBuyResult:
                HandleNpcBuyResult(r);
                break;
            case PacketId.S2C_NpcSellResult:
                HandleNpcSellResult(r);
                break;
            case PacketId.S2C_BankData:
                HandleBankData(r);
                break;
            case PacketId.S2C_BankResult:
                HandleBankResult(r);
                break;
            case PacketId.S2C_GoldUpdate:
                HandleGoldUpdate(r);
                break;
        }
    }

    public void RemoveEntity(ulong entityId)
    {
        if (_entities.TryGetValue(entityId, out var node) && IsInstanceValid(node))
        {
            node.QueueFree();
        }
        _entities.Remove(entityId);
    }

    public void RegisterEntity(ulong entityId, Node2D node)
    {
        _entities[entityId] = node;
    }

    public IEnumerable<KeyValuePair<ulong, Node2D>> GetAllEntities()
    {
        return _entities;
    }

    public Node2D? GetEntity(ulong entityId)
    {
        _entities.TryGetValue(entityId, out var node);
        return node;
    }

    public ItemDatabase? ItemDB { get; private set; }

    public void ApplyPendingInventory()
    {
        if (PendingInventoryData == null && PendingEquipmentData == null)
            return;

        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        if (player == null) return;

        if (PendingInventoryData != null)
        {
            var inv = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
            if (inv != null && ItemDB != null)
            {
                inv.AplicarDadosServidor(PendingInventoryData, ItemDB);
                GD.Print("[GAME] Pending inventory applied");
            }
        }

        if (PendingEquipmentData != null)
        {
            var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
            if (equip != null && ItemDB != null)
            {
                foreach (var entry in PendingEquipmentData)
                {
                    int slotVal = (int)entry["slot"];
                    int itemId = (int)entry["item_id"];
                    int qty = (int)entry["quantity"];
                    var resource = ItemDB.GetItem(itemId);
                    if (resource != null)
                    {
                        var tipo = (TipoEquipamento)slotVal;
                        equip.ItensEquipados[tipo] = new SlotInventario(resource, qty);
                    }
                }
                equip.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
                GD.Print("[GAME] Pending equipment applied");
            }
        }
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary>? PendingInventoryData { get; private set; }
    public Godot.Collections.Array<Godot.Collections.Dictionary>? PendingEquipmentData { get; private set; }

    public void ClearAllEntities()
    {
        foreach (var kvp in _entities)
        {
            if (IsInstanceValid(kvp.Value))
                kvp.Value.QueueFree();
        }
        _entities.Clear();
    }

    public override void _ExitTree()
    {
        ClearAllEntities();
    }
}

public class CharacterEntry
{
    public int SlotIndex { get; set; }
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public string Race { get; set; } = "";
    public int Level { get; set; }
}
