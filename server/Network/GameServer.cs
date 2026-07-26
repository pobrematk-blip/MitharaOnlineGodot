using System.Collections.Concurrent;
using System.Net.Sockets;
using Mithara.Server.Database;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.Quests;
using Mithara.Server.World;
using Mithara.Server.World.Pathfinding;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Mithara.Server.Network;

public enum ChatChannel : byte
{
    Global = 0,
    Whisper = 1,
    Group = 2,
    Guild = 3,
    System = 4,
}

public partial class GameServer : INetEventListener
{
    internal readonly ServerConfig _config;
    internal readonly NetManager _netManager;
    internal readonly WorldManager _world;
    internal readonly DatabaseManager _db;
    internal readonly Dictionary<NetPeer, PlayerSession> _sessions = new();
    internal readonly Dictionary<int, NetPeer> _activeAccounts = new();
    internal readonly ConcurrentQueue<Action> _mainThreadActions = new();
    internal readonly Dictionary<ulong, ulong> _partyInvites = new();
    internal readonly Dictionary<ulong, ulong> _guildInvites = new();
    internal readonly Dictionary<ulong, GuildLeaderPromotion> _guildLeaderPromotions = new();
    internal readonly Dictionary<ulong, ulong> _tradeInvites = new();
    internal readonly Dictionary<ulong, TradeSession> _activeTrades = new();
    internal readonly List<PendingProjectileHit> _pendingProjectileHits = new();
    internal readonly List<PendingProjectileFire> _pendingProjectileFires = new();
    internal readonly List<PendingMonsterProjectileHit> _pendingMonsterProjectileHits = new();
    internal readonly List<PendingDotTick> _pendingDotTicks = new();
    internal readonly List<PendingHealTick> _pendingHealTicks = new();
    internal readonly List<PendingAreaSkillTick> _pendingAreaSkillTicks = new();
    internal readonly List<PendingFreneticStrike> _pendingFreneticStrikes = new();
    internal readonly List<ActiveBastionArea> _activeBastionAreas = new();
    internal readonly Dictionary<ulong, double> _lastPartyMemberUpdateSentAt = new();
    internal int _nextTradeId = 1;
    internal readonly QuestManager _questManager = new();
    internal readonly Dictionary<string, int> _loginAttempts = new();
    internal readonly Dictionary<string, double> _loginCooldowns = new();
    internal readonly Dictionary<ulong, double> _lastMoveValidationLog = new();

    internal volatile bool _running;
    internal double _gameTime;
    private const double AutoSaveInterval = 60.0;
    private const double PresenceUpdateInterval = 30.0;
    private const double EntityBroadcastInterval = 0.10;
    private const double PartyMemberBroadcastInterval = 0.20;
    private double _lastAutoSaveTime;
    private double _nextPresenceUpdateTime;
    private double _nextEntityBroadcastTime;

    public WorldManager World => _world;

    public GameServer(ServerConfig config, DatabaseManager db)
    {
        _config = config;
        _db = db;
        _netManager = new NetManager(this)
        {
            AutoRecycle = true,
            IPv6Enabled = false,
            UnsyncedEvents = true,
            UpdateTime = 15,
            DisconnectTimeout = 10000,
            PingInterval = 1000,
        };
        _world = new WorldManager(config.ChannelCount);
    }

    public void Start()
    {
        if (!_netManager.Start(_config.Port))
            throw new InvalidOperationException($"Não foi possível iniciar o servidor: a porta {_config.Port} já está em uso.");
        _running = true;

        var pathGrid = new PathfindingGrid(
            _config.PathfindingGridWidth,
            _config.PathfindingGridHeight,
            _config.PathfindingCellSize,
            _config.PathfindingOriginX,
            _config.PathfindingOriginY
        );
        pathGrid.ApplyBlockedAreas(_config.BlockedAreas);

        if (_config.NpcSpawnPoints.Count > 0)
            _world.Npcs.ConfigureSpawnPoints(_config.NpcSpawnPoints);

        foreach (var ch in _world.GetAllChannels())
        {
            ch.OnMonsterAttack += HandleMonsterAIAttack;
            ch.OnMonsterSpecial += HandleMonsterSpecial;
            ch.AoiRadius = Math.Clamp(_config.AoiRadius, 600f, 1600f);
            ch.NoMobZones = _config.NoMobZones;
            if (_config.SpawnPoints.Count > 0)
                ch.Spawner.ConfigureSpawnPoints(_config.SpawnPoints);
            ch.PathGrid = pathGrid;
            int npcCount = ch.SpawnNpcs(_world.Npcs);
            Logger.Info($"Canal {ch.Id}: {npcCount} NPC(s), {_config.SpawnPoints.Count} spot(s) de mob configurado(s).");
        }

        LoadGuildsFromDb();
        _world.LoadLojinhas(_db);
        LoadTileData();

        Logger.Info($"Iniciado na porta {_config.Port}");
        Logger.Info($"Canais: {_config.ChannelCount}");
        Logger.Info($"Tick rate: {_config.TickRate} Hz");
    }

    private void LoadGuildsFromDb()
    {
        int repairedGuilds = _db.RepairMissingGuildVisualData();
        if (repairedGuilds > 0)
            Logger.Info($"Guildas antigas sem sigla/emblema reparadas: {repairedGuilds}.");

        int repairedLeaders = _db.RepairGuildsWithoutLeader();
        if (repairedLeaders > 0)
            Logger.Info($"Guildas sem lider reparadas: {repairedLeaders}.");

        _db.LoadAllGuilds(
            onGuild: (id, name, level, xp, skillPoints, tag, emblem) =>
            {
                var guild = new Guild(name, 0, tag, emblem);
                guild.Id = id;
                guild.Level = level;
                guild.Xp = xp;
                guild.SkillPoints = skillPoints;
                _world.Guilds.LoadGuild(guild);
            },
            onMember: (guildId, entityId, name, rank) =>
            {
                _world.Guilds.LoadMember(guildId, entityId, rank);
            },
            onSkill: (guildId, skillId, level) =>
            {
                _world.Guilds.LoadSkill(guildId, skillId, level);
            }
        );
        Logger.Info("Guildas carregadas do banco.");
    }

    public void Stop()
    {
        _running = false;
        _netManager.Stop();
        Logger.Info("Servidor parado.");
    }

    public void PollEvents()
    {
        _netManager.PollEvents();

        while (_mainThreadActions.TryDequeue(out var action))
            action();
    }

    public void Update(float dt)
    {
        _gameTime += dt;
        _world.UpdateAll(dt, _gameTime);
        ProcessPendingProjectileFires();
        ProcessPendingProjectileHits();
        ProcessPendingMonsterProjectileHits();
        ProcessPendingDotTicks();
        ProcessPendingHealTicks();
        ProcessPendingFreneticStrikes();
        ProcessPendingAreaSkillTicks();
        ProcessActiveBastionAreas();
        ProcessActiveDuels();
        UpdateOnlinePresence();

        foreach (var ch in _world.GetAllChannels())
        {
            var expired = ch.DrainExpiredLoot();
            if (expired.Count == 0) continue;
            foreach (var lootId in expired)
            {
                var w = PacketSerializer.WritePacket(PacketId.S2C_LootDespawn);
                w.Put(lootId);
                foreach (var kv in ch.GetAllEntities())
                {
                    var peer = ch.GetPlayerPeer(kv.Key);
                    if (peer != null)
                        peer.Send(w, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        if (_gameTime >= _nextEntityBroadcastTime)
        {
            _nextEntityBroadcastTime = _gameTime + EntityBroadcastInterval;
            BroadcastEntityUpdates();
        }

        if (_gameTime - _lastAutoSaveTime >= AutoSaveInterval)
        {
            _lastAutoSaveTime = _gameTime;
            AutoSaveAllPlayers();
        }
    }

    private void AutoSaveAllPlayers()
    {
        foreach (var ch in _world.GetAllChannels())
        {
            foreach (var kv in ch.GetAllEntities())
            {
                if (kv.Value is PlayerEntity player)
                {
                    var session = _sessions.Values.FirstOrDefault(s => s.EntityId == kv.Key);
                    if (session?.SelectedCharacter != null)
                    {
                        _db.SaveCharacterPosition(session.SelectedCharacter.Id, player.X, player.Y, session.CurrentMap);
                        _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);
                        _db.SaveCharacterXp(session.SelectedCharacter.Id, player.Experience);
                        _db.SaveCharacterLevel(session.SelectedCharacter.Id, player.Level);
                        _db.SaveCharacterStats(session.SelectedCharacter.Id, player.BaseForca, player.BaseAgilidade, player.BaseDestreza, player.BaseInteligencia, player.StatPoints);
                        session.SelectedCharacter.Gold = player.Gold;
                        session.SelectedCharacter.Xp = player.Experience;
                        session.SelectedCharacter.Level = player.Level;
                        session.SelectedCharacter.Forca = player.BaseForca;
                        session.SelectedCharacter.Agilidade = player.BaseAgilidade;
                        session.SelectedCharacter.Destreza = player.BaseDestreza;
                        session.SelectedCharacter.Inteligencia = player.BaseInteligencia;
                        session.SelectedCharacter.StatPoints = player.StatPoints;
                    }
                }
            }
        }
    }

    private void UpdateOnlinePresence()
    {
        if (_gameTime < _nextPresenceUpdateTime)
            return;

        _nextPresenceUpdateTime = _gameTime + PresenceUpdateInterval;

        foreach (int accountId in _sessions.Values
                     .Where(s => s.AccountId > 0)
                     .Select(s => s.AccountId)
                     .Distinct())
        {
            try
            {
                string characterName = _sessions.Values.FirstOrDefault(s => s.AccountId == accountId)?.SelectedCharacter?.Name ?? "";
                _db.UpdateAccountLastSeen(accountId, characterName);
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateOnlinePresence", ex);
            }
        }
    }

    public bool IsRunning => _running;

    void INetEventListener.OnPeerConnected(NetPeer peer)
    {
        Logger.Info($"Conexão: {peer.Address}:{peer.Port}");
        var session = new PlayerSession { Peer = peer };
        _mainThreadActions.Enqueue(() => _sessions[peer] = session);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Logger.Info($"Desconexão: {peer.Address}:{peer.Port} motivo={disconnectInfo.Reason}");
        _mainThreadActions.Enqueue(() => HandleDisconnectCleanup(peer));
    }

    private void HandleDisconnectCleanup(NetPeer peer)
    {
        try
        {
            if (_sessions.Remove(peer, out var session))
            {
                if (session.AccountId > 0 &&
                    _activeAccounts.TryGetValue(session.AccountId, out var activePeer) &&
                    activePeer == peer)
                    _activeAccounts.Remove(session.AccountId);
                if (session.EntityId > 0 && session.ChannelId >= 0)
                {
                    foreach (var ch in _world.GetAllChannels())
                    {
                        var entity = ch.GetEntity(session.EntityId);
                        if (entity is PlayerEntity player)
                        {
                            if (session.SelectedCharacter != null)
                                _db.SaveCharacterFull(session.SelectedCharacter.Id, player, session.SelectedCharacter.BankGold);
                            if (player.PartyId >= 0)
                                HandlePartyLeave(player);
                        }
                    }

                    _world.RemoveFromChannel(session.ChannelId, session.EntityId);
                    var channel = _world.GetChannel(session.ChannelId);
                    if (channel != null)
                        BroadcastDespawn(channel, session.EntityId);
                }
                _partyInvites.Remove(session.EntityId);
                _guildInvites.Remove(session.EntityId);
                _guildLeaderPromotions.Remove(session.EntityId);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("HandleDisconnectCleanup", ex);
        }
    }

    void INetEventListener.OnNetworkError(System.Net.IPEndPoint endPoint, SocketError socketError)
    {
        Logger.Info($"Erro de rede [{endPoint}]: {socketError}");
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (!PacketSerializer.TryReadPacket(reader, out var packetId, out var data))
            return;
        _mainThreadActions.Enqueue(() => HandlePacket(peer, packetId, data));
    }

    void INetEventListener.OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    void INetEventListener.OnConnectionRequest(ConnectionRequest request)
    {
        if (_netManager.ConnectedPeersCount >= _config.MaxConnections)
        {
            request.Reject();
            return;
        }
        request.AcceptIfKey("MitharaMMO");
    }

    private void HandlePacket(NetPeer peer, PacketId packetId, byte[] data)
    {
        try
        {
            var reader = new NetDataReader(data);

            switch (packetId)
            {
            case PacketId.C2S_Register:
                HandleRegister(peer, reader);
                break;
            case PacketId.C2S_Login:
                HandleLogin(peer, reader);
                break;
            case PacketId.C2S_CreateCharacter:
                HandleCreateCharacter(peer, reader);
                break;
            case PacketId.C2S_SelectCharacter:
                HandleSelectCharacter(peer, reader);
                break;
            case PacketId.C2S_EnterWorld:
                HandleEnterWorld(peer, reader);
                break;
            case PacketId.C2S_PlayerMove:
                HandlePlayerMove(peer, reader);
                break;
            case PacketId.C2S_PlayerStop:
                HandlePlayerStop(peer, reader);
                break;
            case PacketId.C2S_PlayerAction:
                HandlePlayerAction(peer, reader);
                break;
            case PacketId.C2S_ChannelSwitch:
                HandleChannelSwitch(peer, reader);
                break;
            case PacketId.C2S_Chat:
                HandleChat(peer, reader);
                break;
            case PacketId.C2S_Ping:
                SendPong(peer);
                break;
            case PacketId.C2S_InventoryRequest:
                HandleInventoryRequest(peer);
                break;
            case PacketId.C2S_EquipItem:
                HandleEquipItem(peer, reader);
                break;
            case PacketId.C2S_UnequipItem:
                HandleUnequipItem(peer, reader);
                break;
            case PacketId.C2S_MoveItem:
                HandleMoveItem(peer, reader);
                break;
            case PacketId.C2S_DropItem:
                HandleDropItem(peer, reader);
                break;
            case PacketId.C2S_UseItem:
                HandleUseItem(peer, reader);
                break;
            case PacketId.C2S_SplitItemStack:
                HandleSplitItemStack(peer, reader);
                break;
            case PacketId.C2S_Attack:
                HandleAttack(peer, reader);
                break;
            case PacketId.C2S_SkillUse:
                HandleSkillUse(peer, reader);
                break;
            case PacketId.C2S_TalentUnlock:
                HandleTalentUnlock(peer, reader);
                break;
            case PacketId.C2S_SetSkillSlot:
                HandleSetSkillSlot(peer, reader);
                break;
            case PacketId.C2S_Respawn:
                HandleRespawn(peer);
                break;
            case PacketId.C2S_RevivePlayer:
                HandleRevivePlayer(peer, reader);
                break;
            case PacketId.C2S_GetSecurityQuestion:
                HandleGetSecurityQuestion(peer, reader);
                break;
            case PacketId.C2S_RecoverPassword:
                HandleRecoverPassword(peer, reader);
                break;
            case PacketId.C2S_DeleteCharacter:
                HandleDeleteCharacter(peer, reader);
                break;
            case PacketId.C2S_LeaveWorld:
                HandleLeaveWorld(peer);
                break;
            case PacketId.C2S_PartyInvite:
                HandlePartyInvitePacket(peer, reader);
                break;
            case PacketId.C2S_PartyAccept:
                HandlePartyAcceptPacket(peer, reader);
                break;
            case PacketId.C2S_PartyLeave:
                HandlePartyLeavePacket(peer, reader);
                break;
            case PacketId.C2S_PartyKick:
                HandlePartyKickPacket(peer, reader);
                break;
            case PacketId.C2S_PartyPromote:
                HandlePartyPromotePacket(peer, reader);
                break;
            case PacketId.C2S_GuildCreate:
                HandleGuildCreatePacket(peer, reader);
                break;
            case PacketId.C2S_GuildInvite:
                HandleGuildInvitePacket(peer, reader);
                break;
            case PacketId.C2S_GuildAccept:
                HandleGuildAcceptPacket(peer, reader);
                break;
            case PacketId.C2S_GuildLeave:
                HandleGuildLeavePacket(peer, reader);
                break;
            case PacketId.C2S_GuildKick:
                HandleGuildKickPacket(peer, reader);
                break;
            case PacketId.C2S_GuildPromote:
                HandleGuildPromotePacket(peer, reader);
                break;
            case PacketId.C2S_GuildDemote:
                HandleGuildDemotePacket(peer, reader);
                break;
            case PacketId.C2S_GuildBuySkill:
                HandleGuildBuySkillPacket(peer, reader);
                break;
            case PacketId.C2S_GuildPromoteLeaderAccept:
                HandleGuildPromoteLeaderAcceptPacket(peer, reader);
                break;
            case PacketId.C2S_GuildPromoteLeaderDecline:
                HandleGuildPromoteLeaderDeclinePacket(peer, reader);
                break;
            case PacketId.C2S_LootPickup:
                HandleLootPickup(peer, reader);
                break;
            case PacketId.C2S_CollectLocalItem:
                HandleCollectLocalItem(peer, reader);
                break;
            case PacketId.C2S_QuestList:
                HandleQuestList(peer, reader);
                break;
            case PacketId.C2S_QuestClaimReward:
                HandleQuestClaimReward(peer, reader);
                break;
            case PacketId.C2S_NpcInteract:
                HandleNpcInteract(peer, reader);
                break;
            case PacketId.C2S_NpcSelectOption:
                HandleNpcSelectOption(peer, reader);
                break;
            case PacketId.C2S_NpcBuyItem:
                HandleNpcBuyItem(peer, reader);
                break;
            case PacketId.C2S_NpcSellItem:
                HandleNpcSellItem(peer, reader);
                break;
            case PacketId.C2S_BankDeposit:
                HandleBankDeposit(peer, reader);
                break;
            case PacketId.C2S_BankWithdraw:
                HandleBankWithdraw(peer, reader);
                break;
            case PacketId.C2S_BankRequest:
                HandleBankRequest(peer, reader);
                break;
            case PacketId.C2S_BankDepositItem:
                HandleBankDepositItem(peer, reader);
                break;
            case PacketId.C2S_BankWithdrawItem:
                HandleBankWithdrawItem(peer, reader);
                break;
            case PacketId.C2S_BankMoveItem:
                HandleBankMoveItem(peer, reader);
                break;
            case PacketId.C2S_MobDropConfig:
                Logger.Info("C2S_MobDropConfig ignorado: drop table e autoridade do servidor.");
                break;
            case PacketId.C2S_PetCapture:
                HandlePetCapture(peer, reader);
                break;
            case PacketId.C2S_PetSummon:
                HandlePetSummon(peer, reader);
                break;
            case PacketId.C2S_AllocateStat:
                HandleAllocateStat(peer, reader);
                break;
            case PacketId.C2S_AdminUpdateItemDefinition:
                HandleAdminUpdateItemDefinition(peer, reader);
                break;
            case PacketId.C2S_DuelRequest:
                HandleDuelRequestPacket(peer, reader);
                break;
            case PacketId.C2S_DuelAccept:
                HandleDuelAcceptPacket(peer, reader);
                break;
            case PacketId.C2S_DuelDecline:
                HandleDuelDeclinePacket(peer, reader);
                break;
            case PacketId.C2S_ProjectileFire:
                HandleProjectileFire(peer, reader);
                break;
            case PacketId.C2S_TradeRequest:
                HandleTradeRequestPacket(peer, reader);
                break;
            case PacketId.C2S_TradeAccept:
                HandleTradeAcceptPacket(peer, reader);
                break;
            case PacketId.C2S_TradeDecline:
                HandleTradeDeclinePacket(peer, reader);
                break;
            case PacketId.C2S_TradeUpdateOffer:
                HandleTradeUpdateOfferPacket(peer, reader);
                break;
            case PacketId.C2S_TradeConfirm:
                HandleTradeConfirmPacket(peer, reader);
                break;
            case PacketId.C2S_TradeCancel:
                HandleTradeCancelPacket(peer, reader);
                break;
            case PacketId.C2S_TradeRemoveOffer:
                HandleTradeRemoveOfferPacket(peer, reader);
                break;
            case PacketId.C2S_TradeUpdateGold:
                HandleTradeUpdateGoldPacket(peer, reader);
                break;
            case PacketId.C2S_CashShopBuy:
                HandleCashShopBuy(peer, reader);
                break;
            case PacketId.C2S_RefineItem:
                HandleRefineItem(peer, reader);
                break;
            case PacketId.C2S_LojinhaOpen:
                HandleLojinhaOpen(peer, reader);
                break;
            case PacketId.C2S_LojinhaAddItem:
                HandleLojinhaAddItem(peer, reader);
                break;
            case PacketId.C2S_LojinhaRemoveItem:
                HandleLojinhaRemoveItem(peer, reader);
                break;
            case PacketId.C2S_LojinhaBuyItem:
                HandleLojinhaBuyItem(peer, reader);
                break;
            case PacketId.C2S_LojinhaCollect:
                HandleLojinhaCollect(peer, reader);
                break;
            case PacketId.C2S_LojinhaClose:
                HandleLojinhaClose(peer, reader);
                break;
            case PacketId.C2S_LojinhaListRequest:
                HandleLojinhaListRequest(peer);
                break;
            case PacketId.C2S_LojinhaRequestItems:
                HandleLojinhaConfigure(peer, reader);
                break;
            case PacketId.C2S_SceneTeleport:
                HandleSceneTeleport(peer, reader);
                break;
            case PacketId.C2S_MarketplaceListRequest:
                HandleMarketplaceListRequest(peer, reader);
                break;
            case PacketId.C2S_MarketplaceCreateItemListing:
                HandleMarketplaceCreateItemListing(peer, reader);
                break;
            case PacketId.C2S_MarketplaceCreateGoldListing:
                HandleMarketplaceCreateGoldListing(peer, reader);
                break;
            case PacketId.C2S_MarketplaceClaimGold:
                HandleMarketplaceClaimGold(peer, reader);
                break;
            case PacketId.C2S_MarketplaceCancelListing:
                HandleMarketplaceCancelListing(peer, reader);
                break;
            case PacketId.C2S_MarketplaceBuyListing:
                HandleMarketplaceBuyListing(peer, reader);
                break;

            case PacketId.C2S_MapEditorPlaceTile:
                HandleMapEditorPlaceTile(peer, reader);
                break;
            case PacketId.C2S_MapEditorRequestTiles:
                HandleMapEditorRequestTiles(peer, reader);
                break;

            case PacketId.C2S_MapMarkerPlace:
                HandleMapMarkerPlace(peer, reader);
                break;
            case PacketId.C2S_MapMarkerRemove:
                HandleMapMarkerRemove(peer, reader);
                break;
            case PacketId.C2S_MapMarkerRequest:
                HandleMapMarkerRequest(peer, reader);
                break;

            }
        }
        catch (Exception ex)
        {
            Logger.Error($"HandlePacket ({packetId})", ex);
        }
    }

    private void HandlePetCapture(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        int petId = reader.GetInt();
        string petName = reader.GetString();
        int scrollSlot = reader.AvailableBytes >= 4 ? reader.GetInt() : -1;
        bool sucesso = reader.AvailableBytes >= 1 ? reader.GetBool() : true;

        bool IsPetScroll(ItemInstance item) =>
            (item.ItemId == ItemDefinitions.PergaminhoDoPet || item.ItemId == ItemDefinitions.PergaminhoDoPet5)
            && item.Quantity > 0;

        var scroll = scrollSlot >= 0
            ? player.Items.FirstOrDefault(i => i.Slot == scrollSlot && IsPetScroll(i))
            : player.Items.FirstOrDefault(IsPetScroll);

        if (scroll == null)
        {
            Logger.Info($"[PET] {session.SelectedCharacter.Name} tentou capturar sem pergaminho.");
            SendSystemMessage(peer, "Voce nao possui o pergaminho de captura de pet.");
            return;
        }

        scroll.Quantity--;
        if (scroll.Quantity <= 0)
        {
            player.Items.Remove(scroll);
            if (scroll.DbId > 0)
                _db.DeleteItem(session.SelectedCharacter.Id, scroll.DbId);
            else
                _db.DeleteItemBySlot(session.SelectedCharacter.Id, scroll.Slot);
        }
        else
        {
            _db.SaveItem(session.SelectedCharacter.Id, scroll);
        }

        if (sucesso)
        {
            _db.SavePet(session.SelectedCharacter.Id, petId, petName);
            SendPetData(peer, session.SelectedCharacter.Id);
            SendSystemMessage(peer, $"Pet '{petName}' capturado com sucesso!");
            Logger.Info($"[PET] {session.SelectedCharacter.Name} capturou pet '{petName}' (ID:{petId}) usando slot {scroll.Slot}");
        }
        else
        {
            SendSystemMessage(peer, $"A captura de '{petName}' falhou.");
            Logger.Info($"[PET] {session.SelectedCharacter.Name} falhou ao capturar pet '{petName}' (ID:{petId}) usando slot {scroll.Slot}");
        }

        SendInventoryData(peer, player);
    }

    private void HandleProjectileFire(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (entity == null || entity.Health <= 0) return;
        if (!ClassePodeUsarProjetilBasico(entity.CharacterClass)) return;
        if (_gameTime < entity.NextBasicAttackTime) return;
        entity.NextBasicAttackTime = _gameTime + GetBasicAttackCooldown(entity);

        float originX = reader.GetFloat();
        float originY = reader.GetFloat();
        float dirX = reader.GetFloat();
        float dirY = reader.GetFloat();
        byte projectileType = reader.GetByte();

        float length = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (length > 0.001f)
        {
            dirX /= length;
            dirY /= length;
        }

        FireBasicProjectile(channel, entity, session, originX, originY, dirX, dirY, projectileType);
    }

    private static bool ClassePodeUsarProjetilBasico(string? classe)
    {
        if (string.IsNullOrWhiteSpace(classe)) return false;
        string cls = classe.Trim().ToLowerInvariant();
        return cls == "mago" || cls == "arqueiro";
    }

    private void SendPetData(NetPeer peer, int characterId)
    {
        var pets = _db.LoadPets(characterId);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_PetData);
        var collarExpiry = _db.LoadPetCollarExpiry(characterId);
        writer.Put(new DateTimeOffset(collarExpiry).ToUnixTimeSeconds());
        writer.Put(pets.Count);
        foreach (var (petId, petName) in pets)
        {
            writer.Put(petId);
            writer.Put(petName);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandlePetSummon(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        int petId = reader.GetInt();
        string petName = reader.GetString().Trim();
        string animPrefix = reader.GetString().Trim();

        if (petId <= 0)
        {
            foreach (ulong removedId in channel.RemovePetsOwnedBy(player.Id))
                BroadcastDespawn(channel, removedId);
            return;
        }

        var capturedPets = _db.LoadPets(session.SelectedCharacter.Id);
        bool isSherigan = petId == 999 && petName.Equals("Sherigan", StringComparison.OrdinalIgnoreCase);
        if (!isSherigan && !capturedPets.Any(p => p.petId == petId))
        {
            SendSystemMessage(peer, "Esse pet nao pertence ao seu personagem.");
            return;
        }

        foreach (ulong removedId in channel.RemovePetsOwnedBy(player.Id))
            BroadcastDespawn(channel, removedId);

        var pet = new PetEntity
        {
            OwnerEntityId = player.Id,
            OwnerName = player.Name,
            PetId = petId,
            Name = string.IsNullOrWhiteSpace(petName) ? "Pet" : petName,
            AnimPrefix = string.IsNullOrWhiteSpace(animPrefix) ? petName : animPrefix,
            X = player.X - 48f,
            Y = player.Y + 32f,
            DirX = player.DirX,
            DirY = player.DirY,
            Level = player.Level,
            FactionId = player.FactionId,
        };

        channel.AddEntity(pet);
        Logger.Info($"[PET] {player.Name} invocou pet replicado '{pet.Name}' (petId={pet.PetId}).");
    }
}

public class PlayerSession
{
    public NetPeer Peer { get; set; } = null!;
    public int AccountId { get; set; }
    public CharacterRow? SelectedCharacter { get; set; }
    public ulong EntityId { get; set; }
    public int ChannelId { get; set; } = -1;
    public bool IsAdmin { get; set; }
    public double LastChatTime { get; set; } = -1;
    public double LastActionTime { get; set; } = -1;
    public HashSet<ulong> SpawnedEntities { get; set; } = new();
    public HashSet<ulong> SpawnedLoot { get; set; } = new();
    public float TeleportEntryX { get; set; }
    public float TeleportEntryY { get; set; }
    public string CurrentMap { get; set; } = "main";
    public double LastTeleportTime { get; set; } = -1;
    public bool IsTransitioning { get; set; }
    public double TransitionStartTime { get; set; }
    public double NextPassiveTeleportCheck { get; set; }
    public string SuppressedTeleportScene { get; set; } = "";
    public int SuppressedTeleportTileX { get; set; } = int.MinValue;
    public int SuppressedTeleportTileY { get; set; } = int.MinValue;

    public bool IsAdminOrAdminMode(ServerConfig config)
    {
        return IsAdmin || config.AdminMode;
    }
}

internal class GuildLeaderPromotion
{
    public ulong LeaderId { get; }
    public int GuildId { get; }

    public GuildLeaderPromotion(ulong leaderId, int guildId)
    {
        LeaderId = leaderId;
        GuildId = guildId;
    }
}
