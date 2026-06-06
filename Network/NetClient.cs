#nullable enable
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using Mithara.Network;

public partial class NetClient : Node
{
    private NetManager _netManager = null!;
    private NetPeer? _serverPeer;
    private EventBasedNetListener _listener = null!;
    private readonly Queue<Action> _pendingActions = new();

    public new bool IsConnected => _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;
    public int Ping => _serverPeer?.Ping ?? 0;

    public event Action? Connected;
    public event Action<DisconnectInfo>? Disconnected;
    public event Action<PacketId, NetDataReader>? PacketReceived;

    public override void _Ready()
    {
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener)
        {
            AutoRecycle = true,
            IPv6Enabled = false,
            UpdateTime = 15,
            UnsyncedEvents = true,
        };
        _netManager.Start();

        _listener.PeerConnectedEvent += peer =>
        {
            _serverPeer = peer;
            _pendingActions.Enqueue(() => Connected?.Invoke());
        };

        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            _serverPeer = null;
            _pendingActions.Enqueue(() => Disconnected?.Invoke(info));
        };

        _listener.NetworkReceiveEvent += (peer, reader, channel, delivery) =>
        {
            if (reader.AvailableBytes < 2) return;
            var packetId = (PacketId)reader.GetUShort();
            byte[] data = reader.GetRemainingBytes();
            _pendingActions.Enqueue(() => PacketReceived?.Invoke(packetId, new NetDataReader(data)));
        };
    }

    public void ConnectToServer(string host, int port)
    {
        _netManager.Connect(host, port, "MitharaMMO");
    }

    public void DisconnectFromServer()
    {
        _serverPeer?.Disconnect();
    }

    public void SendPacket(PacketId id, Action<NetDataWriter> writePayload)
    {
        if (!IsConnected) return;
        var writer = new NetDataWriter();
        writer.Put((ushort)id);
        writePayload(writer);
        _serverPeer!.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendPacketUnreliable(PacketId id, Action<NetDataWriter> writePayload)
    {
        if (!IsConnected) return;
        var writer = new NetDataWriter();
        writer.Put((ushort)id);
        writePayload(writer);
        _serverPeer!.Send(writer, DeliveryMethod.Unreliable);
    }

    public override void _Process(double delta)
    {
        _netManager?.PollEvents();

        while (_pendingActions.TryDequeue(out var action))
            action();
    }

    public override void _ExitTree()
    {
        _netManager?.Stop();
    }
}
