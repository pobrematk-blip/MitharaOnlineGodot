using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ContaData : Resource
{
    [Export] public int Diamantes { get; set; } = 100;
    [Export] public int SlotsComprados { get; set; } = 0;
    [Export] public int SlotsBase { get; set; } = 3;
    [Export] public int UltimoSlotSelecionado { get; set; } = 0;
    [Export] public bool AdminAtivado { get; set; }

    public int SlotsMaximos => SlotsBase + SlotsComprados;
    public int PrecoSlot { get; set; } = 500;

    public List<string> AdminAccountsList { get; set; } = new();
}
