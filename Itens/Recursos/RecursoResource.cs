using Godot;

[GlobalClass]
public partial class RecursoResource : Resource
{
    [Export] public string Nome { get; set; } = "Recurso";
    [Export] public string Tipo { get; set; } = "Arvore";

    [ExportGroup("Aparencia")]
    [Export] public Texture2D TexturaFase1 { get; set; }
    [Export] public Texture2D TexturaFase2 { get; set; }
    [Export] public Texture2D TexturaFase3 { get; set; }

    [ExportGroup("Crescimento")]
    [Export] public float TempoFase1Para2 { get; set; } = 30f;
    [Export] public float TempoFase2Para3 { get; set; } = 30f;

    [ExportGroup("Drop")]
    [Export] public int ItemDropID { get; set; }
    [Export] public int QuantidadeMinima { get; set; } = 1;
    [Export] public int QuantidadeMaxima { get; set; } = 1;
}
