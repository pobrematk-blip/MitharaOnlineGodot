using Godot;

[GlobalClass]
public partial class SpritePresetResource : Resource
{
    [Export] public string NomePreset { get; set; } = "Mago";
    [Export] public string PrefixoAnimacao { get; set; } = "mago";
    [Export] public SpriteFrames SpriteFramesRecurso { get; set; }
    [Export(PropertyHint.MultilineText)] public string Descricao { get; set; } = "";
}
