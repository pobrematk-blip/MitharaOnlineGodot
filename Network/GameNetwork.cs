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
    private static string? _logPath;

    private bool _enterWorldPending;
    internal int _pendingStatPoints;
    internal int _pendingBaseForca;
    internal int _pendingBaseAgilidade;
    internal int _pendingBaseDestreza;
    internal int _pendingBaseInteligencia;
    internal int _pendingLevel;
    internal long _pendingXp;
    private bool _pendingInventoryApplyLogged;

    public static void Log(string msg)
    {
        try
        {
            _logPath ??= Godot.ProjectSettings.GlobalizePath("user://client.log");
            var line = $"[{System.DateTime.Now:HH:mm:ss}] {msg}";
            GD.Print(line);
            System.IO.File.AppendAllText(_logPath, line + System.Environment.NewLine);
        }
        catch { }
    }

    public static void LogError(string msg, string? detail = null)
    {
        Log($"[ERRO] {msg}");
        if (detail != null)
            Log($"  -> {detail}");
    }

    public int AccountId { get; set; }
    public bool LoggedIn { get; set; }
    public List<CharacterEntry> Characters { get; } = new();
    public ulong LocalPlayerId { get; private set; }
    public int LocalChannelId { get; private set; }
    public int Gold { get; set; }
    public int GuildId { get; set; } = -1;
    public string GuildName { get; set; } = "";
    public string GuildTag { get; set; } = "";
    public int GuildEmblem { get; set; } = -1;
    public bool IsGuildLeader { get; set; }
    public Vector2 PendingPlayerSpawn { get; private set; }

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
    [Signal] public delegate void OnLevelUpEventHandler(ulong entityId, int newLevel, int remainingXp);
    [Signal] public delegate void OnInventoryDataEventHandler(Godot.Collections.Array<Godot.Collections.Dictionary> items, Godot.Collections.Array<Godot.Collections.Dictionary> equipment);
    [Signal] public delegate void OnEquipUpdateEventHandler(int equipSlot, int itemId, int quantity, bool hasUnequip, int invSlot, int unequipItemId, int unequipQuantity);

    [Signal] public delegate void OnItemUpdateEventHandler(int slot, int itemId, int quantity);

    [Signal] public delegate void OnPartyDataEventHandler(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members);
    [Signal] public delegate void OnPartyMemberUpdateEventHandler(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined, string characterClass);
    [Signal] public delegate void OnPartyLeaderUpdateEventHandler(ulong newLeaderId);

    [Signal] public delegate void OnGuildDataEventHandler(int guildId, string guildName, string guildTag, int guildEmblem, Godot.Collections.Array<Godot.Collections.Dictionary> members, int level, int xp, int skillPoints, Godot.Collections.Array<Godot.Collections.Dictionary> skills);
    [Signal] public delegate void OnGuildMemberUpdateEventHandler(ulong entityId, string name, int rank, bool joined);
    [Signal] public delegate void OnGuildRankUpdateEventHandler(ulong entityId, int newRank);
    [Signal] public delegate void OnGuildSkillUpdateEventHandler(string skillId, int newLevel);

    [Signal] public delegate void OnEntityHealthUpdateEventHandler(ulong entityId, int health, int maxHealth);
    [Signal] public delegate void OnRespawnEventHandler(ulong entityId, float x, float y, int health, int maxHealth);
    [Signal] public delegate void OnTeleportEventHandler(ulong entityId, float x, float y);
    [Signal] public delegate void OnLootSpawnEventHandler(ulong lootId, float x, float y, int itemId, int quantity);
    [Signal] public delegate void OnLootDespawnEventHandler(ulong lootId);
    [Signal] public delegate void OnGoldUpdateEventHandler(int gold);
    [Signal] public delegate void OnStatUpdateEventHandler(int baseForca, int baseAgilidade, int baseDestreza, int baseInteligencia, int statPoints, int totalForca, int totalAgilidade, int totalDestreza, int totalInteligencia, int maxHealth, int maxMana);
    [Signal] public delegate void OnOpenGuildFormEventHandler();
    [Signal] public delegate void OnGuildCreateResultEventHandler(int guildId, bool success, string message);
    [Signal] public delegate void OnGuildClearedEventHandler();
    [Signal] public delegate void OnDuelRequestedEventHandler(string senderName);
    [Signal] public delegate void OnPartyInviteReceivedEventHandler(string senderName);
    [Signal] public delegate void OnGuildInviteReceivedEventHandler(string senderName);
    [Signal] public delegate void OnTradeRequestedEventHandler(string senderName);
    [Signal] public delegate void OnTradeStartEventHandler(ulong partnerId, string partnerName);
    [Signal] public delegate void OnTradeOfferUpdateEventHandler(ulong playerSide, Godot.Collections.Array<Godot.Collections.Dictionary> offers);
    [Signal] public delegate void OnTradePartnerConfirmEventHandler(ulong playerSide, bool confirmed);
    [Signal] public delegate void OnTradeEndEventHandler(bool success);
    [Signal] public delegate void OnCashShopResultEventHandler(bool success, string message);
    [Signal] public delegate void OnRefineResultEventHandler(bool success, int newLevel, string message);
    [Signal] public delegate void OnDuelStartEventHandler(ulong opponentId, string opponentName);
    [Signal] public delegate void OnDuelEndEventHandler(bool won);
    [Signal] public delegate void OnProjectileSpawnEventHandler(ulong entityId, float originX, float originY, float dirX, float dirY, byte projectileType);

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

    public override void _Process(double delta)
    {
        if (!_enterWorldPending) return;
        _enterWorldPending = false;
        CallDeferred(nameof(ApplyEnterWorld));
    }

    private void ApplyEnterWorld()
    {
        try
        {
            Log("ApplyEnterWorld: ChangeSceneToFile...");
            var err = GetTree().ChangeSceneToFile(SceneConstants.MAIN);
            if (err != Error.Ok)
            {
                LogError($"ApplyEnterWorld: ChangeSceneToFile retornou erro {err}");
                return;
            }
            Log("ApplyEnterWorld: ChangeSceneToFile concluido!");

            // Ativar HUD - busca robusta
            var hud = GetNodeOrNull<CanvasLayer>("/root/main/HUD");
            if (hud == null)
            {
                var root = GetTree()?.Root;
                if (root != null)
                {
                    for (int i = 0; i < root.GetChildCount(); i++)
                    {
                        var h = root.GetChild(i).FindChild("HUD", true, false) as CanvasLayer;
                        if (h != null)
                        {
                            hud = h;
                            break;
                        }
                    }
                }
            }

            if (hud != null)
            {
                hud.Visible = true;
                Log("HUD ativado de ApplyEnterWorld");
            }
            else
            {
                Log("HUD n?o encontrado");
            }

            var tree = GetTree();
            if (tree != null)
            {
                var timer = tree.CreateTimer(0.2);
                timer.Timeout += EmitOnEnterWorld;
            }
            else
            {
                CallDeferred(nameof(EmitOnEnterWorld));
            }
        }
        catch (System.Exception ex)
        {
            LogError("ApplyEnterWorld: ChangeSceneToFile falhou", ex.ToString());
        }
    }

    private void EmitOnEnterWorld()
    {
        EmitSignal(SignalName.OnEnterWorld);
        CallDeferred(nameof(ApplyPendingInventory));
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
        try { switch (id)
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
            case PacketId.S2C_PlayerAction:
                HandlePlayerAction(r);
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
            case PacketId.S2C_Respawn:
                HandleRespawn(r);
                break;
            case PacketId.S2C_Teleport:
                HandleTeleport(r);
                break;
            case PacketId.S2C_RecoverResult:
                HandleRecoverResult(r);
                break;
            case PacketId.S2C_CharacterDeleted:
                HandleCharacterDeleted(r);
                break;
            case PacketId.S2C_LeaveWorld:
                HandleLeaveWorld(r);
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
            case PacketId.S2C_PartyInviteReceived:
                HandlePartyInviteReceived(r);
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
            case PacketId.S2C_GuildInviteReceived:
                HandleGuildInviteReceived(r);
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
            case PacketId.S2C_OpenGuildForm:
                GD.Print("[NET] S2C_OpenGuildForm RECEBIDO! Emitindo OnOpenGuildForm...");
                EmitSignal(SignalName.OnOpenGuildForm);
                GD.Print("[NET] OnOpenGuildForm emitido!");
                break;
            case PacketId.S2C_GuildCreateResult:
                HandleGuildCreateResult(r);
                break;
            case PacketId.S2C_GuildClear:
                HandleGuildClear();
                break;
            case PacketId.S2C_PetData:
                HandlePetData(r);
                break;
            case PacketId.S2C_StatUpdate:
                HandleStatUpdate(r);
                break;
            case PacketId.S2C_DuelRequested:
                HandleDuelRequested(r);
                break;
            case PacketId.S2C_DuelStart:
                HandleDuelStart(r);
                break;
            case PacketId.S2C_DuelEnd:
                HandleDuelEnd(r);
                break;
            case PacketId.S2C_ProjectileSpawn:
                HandleProjectileSpawn(r);
                break;
            case PacketId.S2C_VipStatus:
                HandleVipStatus(r);
                break;
            case PacketId.S2C_TradeRequested:
                HandleTradeRequested(r);
                break;
            case PacketId.S2C_TradeStart:
                HandleTradeStart(r);
                break;
            case PacketId.S2C_TradeOfferUpdate:
                HandleTradeOfferUpdate(r);
                break;
            case PacketId.S2C_TradePartnerConfirm:
                HandleTradePartnerConfirm(r);
                break;
            case PacketId.S2C_TradeEnd:
                HandleTradeEnd(r);
                break;
            case PacketId.S2C_CashShopResult:
                HandleCashShopResult(r);
                break;
            case PacketId.S2C_RefineResult:
                HandleRefineResult(r);
                break;
        } } catch (System.Exception ex)
        {
            LogError($"Erro processando pacote {id}", ex.ToString());
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
        if (PendingInventoryData == null && PendingEquipmentData == null && PendingPetData == null)
            return;

        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        if (player == null)
        {
            if (!_pendingInventoryApplyLogged)
            {
                Log("ApplyPendingInventory: Player ainda n?o est? pronto; mantendo dados pendentes.");
                _pendingInventoryApplyLogged = true;
            }
            return;
        }

        if (PendingInventoryData != null)
        {
            var inv = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
            if (inv != null && ItemDB != null)
            {
                inv.AplicarDadosServidor(PendingInventoryData, ItemDB);
                GD.Print("[GAME] Pending inventory applied");
                PendingInventoryData = null;
            }
            else
            {
                Log("ApplyPendingInventory: InventarioComponent ou ItemDB n?o encontrado; mantendo invent?rio pendente.");
            }
        }

        if (PendingEquipmentData != null)
        {
            var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
            if (equip != null && ItemDB != null)
            {
                equip.ItensEquipados.Clear();
                foreach (var entry in PendingEquipmentData)
                {
                    int slotVal = (int)entry["slot"];
                    int itemId = (int)entry["item_id"];
                    int qty = (int)entry["quantity"];
                    int refineLevel = entry.ContainsKey("refine_level") ? (int)entry["refine_level"] : 0;
                    string instanceData = entry.ContainsKey("instance_data") ? (string)entry["instance_data"] : "";
                    var resource = ItemDB.GetItem(itemId);
                    if (resource == null)
                    {
                        ItemDB.Refresh();
                        resource = ItemDB.GetItem(itemId);
                    }
                    if (resource != null)
                    {
                        var tipo = (TipoEquipamento)slotVal;
                        equip.ItensEquipados[tipo] = new SlotInventario(resource, qty, refineLevel, instanceData);
                    }
                    else
                        LogError($"Item equipado {itemId} não existe no catálogo do cliente.");
                }
                equip.RecalcularBonusEquipamentos();
                equip.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
                GD.Print("[GAME] Pending equipment applied");
                PendingEquipmentData = null;
            }
            else
            {
                Log("ApplyPendingInventory: EquipamentoComponent ou ItemDB n?o encontrado; mantendo equipamento pendente.");
            }
        }

        if (PendingPetData != null)
        {
            var colecao = player.FindChild("PetColecaoComponent", true, false) as PetColecaoComponent;
            if (colecao != null)
            {
                colecao.Limpar();
                foreach (var entry in PendingPetData)
                {
                    int petId = (int)entry["pet_id"];
                    string petName = (string)entry["pet_name"];
                    colecao.RegistrarCaptura(petId, petName);
                }
                GD.Print("[GAME] Pending pet data applied");
                PendingPetData = null;
            }
            else
            {
                Log("ApplyPendingInventory: PetColecaoComponent n?o encontrado; mantendo pets pendentes.");
            }
        }
    }

    internal void ResetPendingInventoryApplyLog()
    {
        _pendingInventoryApplyLogged = false;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary>? PendingInventoryData { get; private set; }
    public Godot.Collections.Array<Godot.Collections.Dictionary>? PendingEquipmentData { get; private set; }
    public Godot.Collections.Array<Godot.Collections.Dictionary>? PendingPetData { get; private set; }

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

    private void HandlePetData(NetDataReader r)
    {
        int count = r.GetInt();
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            int petId = r.GetInt();
            string petName = r.GetString();
            list.Add(new Godot.Collections.Dictionary
            {
                ["pet_id"] = petId,
                ["pet_name"] = petName,
            });
        }
        PendingPetData = list;
        GD.Print($"[GAME] Received pet data: {count} pets");
    }

    public void SendPetCapture(int petId, string petName)
    {
        _client?.SendPacket(PacketId.C2S_PetCapture, w =>
        {
            w.Put(petId);
            w.Put(petName);
        });
        GD.Print($"[GAME] Sent pet capture: {petName} (ID:{petId})");
    }

    public void SendCollectLocalItem(int itemId, int quantity, Vector2 worldPosition)
    {
        _client?.SendPacket(PacketId.C2S_CollectLocalItem, w =>
        {
            w.Put(itemId);
            w.Put(quantity);
            w.Put(worldPosition.X);
            w.Put(worldPosition.Y);
        });
    }

    public void SendAdminUpdateItemDefinition(ItemResource item)
    {
        if (item == null || _client == null || !_client.IsConnected)
            return;

        static int Mid(int min, int max, int fallback) =>
            min == 0 && max == 0 ? fallback : (min + max) / 2;

        _client.SendPacket(PacketId.C2S_AdminUpdateItemDefinition, w =>
        {
            w.Put(item.ItemID);
            w.Put(item.Nome ?? "");
            w.Put((int)item.Tipo);
            w.Put(item.QuantidadeMaximaPorSlot);
            w.Put(item.Acumulavel);
            w.Put(item.EhBolsa);
            w.Put(item.SlotsAdicionais);
            w.Put(Mid(item.ForcaMin, item.ForcaMax, item.Forca));
            w.Put(Mid(item.AgilidadeMin, item.AgilidadeMax, item.Agilidade));
            w.Put(Mid(item.DestrezaMin, item.DestrezaMax, item.Destreza));
            w.Put(Mid(item.InteligenciaMin, item.InteligenciaMax, item.Inteligencia));
            w.Put(Mid(item.DanoFisicoMin, item.DanoFisicoMax, item.DanoFisico));
            w.Put(Mid(item.DefesaFisicaMin, item.DefesaFisicaMax, item.DefesaFisica));
            w.Put(Mid(item.DefesaMagicaMin, item.DefesaMagicaMax, item.DefesaMagica));
            w.Put(Mid(item.HpMin, item.HpMax, item.Hp));
            w.Put(Mid(item.ManaMin, item.ManaMax, item.Mana));
            w.Put((item.EvasaoMin == 0 && item.EvasaoMax == 0) ? item.Evasao : (item.EvasaoMin + item.EvasaoMax) / 2f);
            w.Put(item.Valor);
            w.Put(item.PoolDeAfixos ?? "");
            w.Put(item.NivelRequerido);
            w.Put(item.TipoItem == TipoItem.Elite);
            w.Put(item.ClassesPermitidas ?? "");
            w.Put(item.ForcaMin); w.Put(item.ForcaMax);
            w.Put(item.AgilidadeMin); w.Put(item.AgilidadeMax);
            w.Put(item.DestrezaMin); w.Put(item.DestrezaMax);
            w.Put(item.InteligenciaMin); w.Put(item.InteligenciaMax);
            w.Put(item.DanoFisicoMin); w.Put(item.DanoFisicoMax);
            w.Put(item.DefesaFisicaMin); w.Put(item.DefesaFisicaMax);
            w.Put(item.DefesaMagicaMin); w.Put(item.DefesaMagicaMax);
            w.Put(item.HpMin); w.Put(item.HpMax);
            w.Put(item.ManaMin); w.Put(item.ManaMax);
            w.Put(item.EvasaoMin); w.Put(item.EvasaoMax);
        });
    }

    private void HandleProjectileSpawn(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        float originX = r.GetFloat();
        float originY = r.GetFloat();
        float dirX = r.GetFloat();
        float dirY = r.GetFloat();
        byte projectileType = r.GetByte();
        EmitSignal(SignalName.OnProjectileSpawn, entityId, originX, originY, dirX, dirY, projectileType);
    }

    public void SendProjectileFire(float originX, float originY, float dirX, float dirY, byte projectileType)
    {
        if (_client == null || !_client.IsConnected) return;
        _client.SendPacket(PacketId.C2S_ProjectileFire, w =>
        {
            w.Put(originX);
            w.Put(originY);
            w.Put(dirX);
            w.Put(dirY);
            w.Put(projectileType);
        });
    }

    public long VipExpiryBinary { get; private set; }
    public bool IsVipActive
    {
        get
        {
            if (VipExpiryBinary == 0) return false;
            var expiry = System.DateTime.FromBinary(VipExpiryBinary);
            return expiry > System.DateTime.UtcNow;
        }
    }

    private void HandleVipStatus(NetDataReader r)
    {
        long binary = r.GetLong();
        VipExpiryBinary = binary;
        var expiry = System.DateTime.FromBinary(binary);
        GD.Print($"[VIP] Status recebido: expiry={expiry:yyyy-MM-dd HH:mm:ss}, ativo={IsVipActive}");
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
