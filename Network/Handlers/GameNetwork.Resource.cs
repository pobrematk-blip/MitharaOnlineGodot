using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

public partial class GameNetwork : Node
{
    public void SendResourceGather(string resourceName, Vector2 position)
    {
        _client?.SendPacket(PacketId.C2S_ResourceGather, w =>
        {
            w.Put(resourceName);
            w.Put(position.X);
            w.Put(position.Y);
        });
    }

    private void HandleResourceGatherResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string resourceName = r.GetString();
        int itemId = r.GetInt();
        int quantity = r.GetInt();
        string message = r.GetString();

        GD.Print($"[RESOURCE] Resultado: success={success} resource={resourceName} item={itemId}x{quantity} msg={message}");

        EmitSignal(SignalName.OnResourceGatherResult, success, resourceName, itemId, quantity, message);
    }

    private void HandleResourceStateChange(NetDataReader r)
    {
        float x = r.GetFloat();
        float y = r.GetFloat();
        string resourceName = r.GetString();
        int newPhase = r.GetByte();

        GD.Print($"[RESOURCE] Estado alterado: {resourceName} @ ({x:F0},{y:F0}) fase={newPhase}");

        EmitSignal(SignalName.OnResourceStateChanged, x, y, resourceName, newPhase);
    }

    [Signal]
    public delegate void OnResourceGatherResultEventHandler(bool success, string resourceName, int itemId, int quantity, string message);

    [Signal]
    public delegate void OnResourceStateChangedEventHandler(float x, float y, string resourceName, int newPhase);
}
