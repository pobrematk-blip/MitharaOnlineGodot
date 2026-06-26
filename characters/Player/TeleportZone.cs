using Godot;
using Mithara.Network;

public partial class TeleportZone : Area2D
{
    [Export] public string TargetScene = "";
    [Export] public string TeleportId = "";
    [Export] public float TargetX;
    [Export] public float TargetY;

    private GameNetwork _network;

    public override void _Ready()
    {
        _network = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player) return;
        if (_network == null || !_network.IsConnected) return;

        _network.SendSceneTeleport(TargetScene, TeleportId, TargetX, TargetY, GlobalPosition.X, GlobalPosition.Y);
    }
}
