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
                if (!RequisitoSatisfeito(requisito, desbloqueados))
                    return false;
        }

        if (!EspecializacaoPermitida(node, desbloqueados))
            return false;

        return true;
    }

    private bool EspecializacaoPermitida(TalentNodeResource node, IReadOnlyCollection<string> desbloqueados)
    {
        string nodeSpec = ObterEspecializacao(node?.NodeId);
        if (string.IsNullOrWhiteSpace(nodeSpec))
            return true;

        foreach (string unlockedId in desbloqueados)
        {
            string unlockedSpec = ObterEspecializacao(unlockedId);
            if (string.IsNullOrWhiteSpace(unlockedSpec))
                continue;
            return unlockedSpec == nodeSpec;
        }

        return true;
    }

    private static string ObterEspecializacao(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return "";

        string[] parts = nodeId.Split('_', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            return "";

        string key = parts[1];
        return key.Equals("base", System.StringComparison.OrdinalIgnoreCase) ? "" : key;
    }

    private bool RequisitoSatisfeito(string requisito, IReadOnlyCollection<string> desbloqueados)
    {
        if (desbloqueados.Contains(requisito)) return true;

        var requisitoNode = ObterNo(requisito);
        if (requisitoNode == null || !requisitoNode.TemEscolhaDeStatus)
            return false;

        if (requisitoNode.Requisitos == null || requisitoNode.Requisitos.Length == 0)
            return true;

        foreach (var parent in requisitoNode.Requisitos)
            if (!RequisitoSatisfeito(parent, desbloqueados))
                return false;
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
