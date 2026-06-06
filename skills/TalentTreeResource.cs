using Godot;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class TalentTreeResource : Resource
{
    [ExportGroup("Identidade")]
    [Export] public string NomeArvore { get; set; } = "Árvore de Talentos";

    [Export] public TalentNodeResource[] Nodes { get; set; } = System.Array.Empty<TalentNodeResource>();
    [Export] public string[] RootNodeIds { get; set; } = System.Array.Empty<string>();

    [ExportGroup("Layout")]
    [Export] public bool UsaLayoutPersonalizado { get; set; } = false;
    [Export] public Vector2 BoardSize { get; set; } = new Vector2(1200, 900);

    public TalentNodeResource ObterNo(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || Nodes == null) return null;
        return System.Array.Find(Nodes, n => n != null && n.NodeId == nodeId);
    }

    public IEnumerable<TalentNodeResource> ObterFilhos(string nodeId)
    {
        if (Nodes == null) yield break;
        foreach (var node in Nodes)
        {
            if (node == null || node.Requisitos == null) continue;
            if (System.Array.Exists(node.Requisitos, r => r == nodeId))
                yield return node;
        }
    }

    public IEnumerable<TalentNodeResource> ObterNosRaiz()
    {
        if (Nodes == null) yield break;
        if (RootNodeIds != null && RootNodeIds.Length > 0)
        {
            foreach (var id in RootNodeIds)
            {
                var node = ObterNo(id);
                if (node != null) yield return node;
            }
            yield break;
        }

        foreach (var node in Nodes)
        {
            if (node == null || node.TemRequisitos) continue;
            yield return node;
        }
    }

    public bool PodeDesbloquear(TalentNodeResource node, IReadOnlyCollection<string> desbloqueados, int nivelAtual)
    {
        if (node == null || desbloqueados == null) return false;
        if (desbloqueados.Contains(node.NodeId)) return false;
        if (nivelAtual < node.NivelMinimo) return false;

        if (node.TemRequisitos)
        {
            foreach (var requisito in node.Requisitos)
            {
                if (!desbloqueados.Contains(requisito))
                    return false;
            }
        }
        return true;
    }

    public IEnumerable<TalentNodeResource> ObterNosDisponiveis(IReadOnlyCollection<string> desbloqueados, int nivelAtual)
    {
        if (Nodes == null) yield break;
        foreach (var node in Nodes)
        {
            if (PodeDesbloquear(node, desbloqueados, nivelAtual))
                yield return node;
        }
    }
}
