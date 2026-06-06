using Mithara.Server.Entities;

namespace Mithara.Server.World;

public class WorldManager
{
    private readonly List<Channel> _channels = new();
    private readonly object _lock = new();

    public NpcManager Npcs { get; } = new();
    public PartyManager Parties { get; } = new();
    public GuildManager Guilds { get; } = new();

    public WorldManager(int channelCount = 4)
    {
        for (int i = 0; i < channelCount; i++)
        {
            var channel = new Channel(i, $"Mundo {i + 1}");
            channel.SpawnNpcs(Npcs);
            _channels.Add(channel);
        }
    }

    public Channel? GetChannel(int channelId)
    {
        return channelId >= 0 && channelId < _channels.Count ? _channels[channelId] : null;
    }

    public Channel GetOrCreateChannel(int channelId)
    {
        lock (_lock)
        {
            while (_channels.Count <= channelId)
                _channels.Add(new Channel(_channels.Count, $"Mundo {_channels.Count + 1}"));
            return _channels[channelId];
        }
    }

    public List<Channel> GetAllChannels() => _channels;

    public ulong SpawnPlayerInChannel(int channelId, PlayerEntity player, object peer)
    {
        var channel = GetOrCreateChannel(channelId);
        return channel.AddEntity(player, peer as LiteNetLib.NetPeer);
    }

    public void RemoveFromChannel(int channelId, ulong entityId)
    {
        var channel = GetChannel(channelId);
        channel?.RemoveEntity(entityId);
    }

    public void UpdateAll(float dt, double gameTime)
    {
        foreach (var channel in _channels)
            channel.Update(dt, gameTime);
    }
}
