using System.Collections.Concurrent;
using System.Net.Sockets;
using Mithara.Server.Database;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.Quests;
using Mithara.Server.World;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Mithara.Server.Network;

public enum ChatChannel : byte
{
    Global = 0,
    Whisper = 1,
    Group = 2,
    Guild = 3,
}

public partial class GameServer : INetEventListener
{
    internal readonly ServerConfig _config;
    internal readonly NetManager _netManager;
    internal readonly WorldManager _world;
    internal readonly DatabaseManager _db;
    internal readonly Dictionary<NetPeer, PlayerSession> _sessions = new();
    internal readonly ConcurrentQueue<Action> _mainThreadActions = new();
    internal readonly Dictionary<ulong, ulong> _partyInvites = new();
    internal readonly Dictionary<ulong, ulong> _guildInvites = new();
    internal readonly QuestManager _questManager = new();

    internal volatile bool _running;
    internal double _gameTime;

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
        };
        _world = new WorldManager(config.ChannelCount);
    }

    public void Start()
    {
        _netManager.Start(_config.Port);
        _running = true;

        foreach (var ch in _world.GetAllChannels())
            ch.EntitySpawned += OnChannelEntitySpawned;

        LoadGuildsFromDb();

        Console.WriteLine($"[SERVER] Iniciado na porta {_config.Port}");
        Console.WriteLine($"[SERVER] Canais: {_config.ChannelCount}");
        Console.WriteLine($"[SERVER] Tick rate: {_config.TickRate} Hz");
    }

    private void LoadGuildsFromDb()
    {
        _db.LoadAllGuilds(
            onGuild: (id, name, level, xp, skillPoints) =>
            {
                var guild = new Guild(name, 0);
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
        Console.WriteLine("[SERVER] Guildas carregadas do banco.");
    }

    public void Stop()
    {
        _running = false;
        _netManager.Stop();
        Console.WriteLine("[SERVER] Parado.");
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
        BroadcastEntityUpdates();
    }

    public bool IsRunning => _running;

    void INetEventListener.OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"[SERVER] Conexão: {peer.Address}:{peer.Port}");
        _sessions[peer] = new PlayerSession { Peer = peer };
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine($"[SERVER] Desconexão: {peer.Address}:{peer.Port} motivo={disconnectInfo.Reason}");

        if (_sessions.Remove(peer, out var session))
        {
            if (session.EntityId > 0 && session.ChannelId >= 0)
            {
                foreach (var ch in _world.GetAllChannels())
                {
                    var entity = ch.GetEntity(session.EntityId);
                    if (entity is PlayerEntity player)
                    {
                        if (player.PartyId >= 0)
                            _world.Parties.RemoveMember(session.EntityId);
                        if (player.GuildId >= 0)
                        {
                            int gid = player.GuildId;
                            _world.Guilds.RemoveMember(session.EntityId);
                            var remaining = _world.Guilds.GetGuild(gid);
                            if (remaining == null)
                                _db.DeleteGuild(gid);
                            else
                                _db.DeleteGuildMember(gid, session.EntityId);
                        }
                    }
                }

                _world.RemoveFromChannel(session.ChannelId, session.EntityId);
                var channel = _world.GetChannel(session.ChannelId);
                if (channel != null)
                    BroadcastDespawn(channel, session.EntityId);
            }
            _partyInvites.Remove(session.EntityId);
            _guildInvites.Remove(session.EntityId);
        }
    }

    void INetEventListener.OnNetworkError(System.Net.IPEndPoint endPoint, SocketError socketError)
    {
        Console.WriteLine($"[SERVER] Erro de rede [{endPoint}]: {socketError}");
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
            case PacketId.C2S_Attack:
                HandleAttack(peer, reader);
                break;
            case PacketId.C2S_GetSecurityQuestion:
                HandleGetSecurityQuestion(peer, reader);
                break;
            case PacketId.C2S_RecoverPassword:
                HandleRecoverPassword(peer, reader);
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
            case PacketId.C2S_LootPickup:
                HandleLootPickup(peer, reader);
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
        }
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

    public bool IsAdminOrAdminMode(ServerConfig config)
    {
        return IsAdmin || config.AdminMode;
    }
}
