using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class TalentTreeComponent : Node
{
    [Signal] public delegate void TalentoDesbloqueadoEventHandler(TalentNodeResource node);
    [Signal] public delegate void BonusAplicadoEventHandler(string nomeBonus, float valor);
    [Signal] public delegate void EstadoAtualizadoEventHandler();

    [ExportGroup("Configuração")]
    [Export] public TalentTreeResource TalentTree { get; set; }

    [ExportGroup("Progresso")]
    [Export] public int PontosDisponiveis { get; set; }

    private readonly List<string> _nosDesbloqueados = new();
    private Player _player;

    public IReadOnlyList<string> NosDesbloqueados => _nosDesbloqueados;
    public IEnumerable<TalentNodeResource> NosAtivos => _nosDesbloqueados.Select(id => TalentTree?.ObterNo(id)).Where(n => n != null);

    public override void _Ready()
    {
        _player = GetParent() as Player;
        if (_player == null)
            GD.PrintErr("[TALENT TREE] ✘ Player não encontrado!");

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null)
        {
            gameNet.OnTalentData += AplicarEstadoServidor;
            if (gameNet.PendingTalentPoints.HasValue && gameNet.PendingTalentNodes != null)
                AplicarEstadoServidor(gameNet.PendingTalentPoints.Value, gameNet.PendingTalentNodes);
        }
    }

    public bool TemNoDesbloqueado(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) && _nosDesbloqueados.Contains(nodeId);
    }

    public bool PodeDesbloquear(string nodeId, int nivelAtual)
    {
        if (TalentTree == null) return false;
        var node = TalentTree.ObterNo(nodeId);
        if (node == null) return false;
        if (PontosDisponiveis < node.CustoPontos) return false;
        return TalentTree.PodeDesbloquear(node, _nosDesbloqueados, nivelAtual);
    }

    public bool DesbloquearNo(string nodeId, int nivelAtual)
    {
        GD.PrintErr("[TALENT TREE] Desbloqueio local bloqueado. O servidor deve validar o talento.");
        return false;
    }

    public void SolicitarDesbloqueioServidor(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[TALENT TREE] Sem conexão. Talentos só podem ser desbloqueados pelo servidor.");
            return;
        }

        gameNet.SendTalentUnlock(nodeId);
    }

    private void AplicarEstadoServidor(int pontosDisponiveis, Godot.Collections.Array<string> nosDesbloqueados)
    {
        _nosDesbloqueados.Clear();
        foreach (string nodeId in nosDesbloqueados)
        {
            if (!string.IsNullOrWhiteSpace(nodeId) && !_nosDesbloqueados.Contains(nodeId))
                _nosDesbloqueados.Add(nodeId);
        }

        PontosDisponiveis = pontosDisponiveis;
        GD.Print($"[TALENT TREE] Estado do servidor aplicado: pontos={PontosDisponiveis}, desbloqueados={_nosDesbloqueados.Count}");
        EmitSignal(SignalName.EstadoAtualizado);
    }

    private void AplicarBonusDoTalento(TalentNodeResource node)
    {
        if (_player == null) return;

        // Aplicar atributos
        if (node.BonusForca > 0)
        {
            GD.Print($"[TALENT] +{node.BonusForca} Força");
            EmitSignal(SignalName.BonusAplicado, "Força", node.BonusForca);
        }
        if (node.BonusAgilidade > 0)
        {
            GD.Print($"[TALENT] +{node.BonusAgilidade} Agilidade");
            EmitSignal(SignalName.BonusAplicado, "Agilidade", node.BonusAgilidade);
        }
        if (node.BonusDestreza > 0)
        {
            GD.Print($"[TALENT] +{node.BonusDestreza} Destreza");
            EmitSignal(SignalName.BonusAplicado, "Destreza", node.BonusDestreza);
        }
        if (node.BonusInteligencia > 0)
        {
            GD.Print($"[TALENT] +{node.BonusInteligencia} Inteligência");
            EmitSignal(SignalName.BonusAplicado, "Inteligência", node.BonusInteligencia);
        }

        // Logs de bônus percentuais
        if (node.BonusDanoPercent > 0)
            GD.Print($"[TALENT] +{node.BonusDanoPercent * 100}% Dano");
        if (node.BonusVelocidadePercent > 0)
            GD.Print($"[TALENT] +{node.BonusVelocidadePercent * 100}% Velocidade");
        if (node.BonusVidaPercent > 0)
            GD.Print($"[TALENT] +{node.BonusVidaPercent * 100}% Vida");
        if (node.BonusManaPercent > 0)
            GD.Print($"[TALENT] +{node.BonusManaPercent * 100}% Mana");
    }

    /// <summary>
    /// Retorna os bônus totais de todos os talentos desbloqueados
    /// </summary>
    public TalentBonusPackage ObterBonusTotais()
    {
        var bonus = new TalentBonusPackage();

        foreach (var nodeAtivo in NosAtivos)
        {
            if (nodeAtivo == null) continue;

            bonus.BonusForca += nodeAtivo.BonusForca;
            bonus.BonusAgilidade += nodeAtivo.BonusAgilidade;
            bonus.BonusDestreza += nodeAtivo.BonusDestreza;
            bonus.BonusInteligencia += nodeAtivo.BonusInteligencia;
            bonus.BonusDanoPercent += nodeAtivo.BonusDanoPercent;
            bonus.BonusVelocidadePercent += nodeAtivo.BonusVelocidadePercent;
            bonus.BonusVidaPercent += nodeAtivo.BonusVidaPercent;
            bonus.BonusManaPercent += nodeAtivo.BonusManaPercent;
        }

        return bonus;
    }

    public void AdicionarPontos(int quantidade)
    {
        if (quantidade <= 0) return;
        PontosDisponiveis += quantidade;
    }

    public TalentNodeResource[] ObterNosDisponiveis(int nivelAtual)
    {
        if (TalentTree == null) return System.Array.Empty<TalentNodeResource>();
        return TalentTree.ObterNosDisponiveis(_nosDesbloqueados, nivelAtual).ToArray();
    }

    public void ResetarArvore()
    {
        _nosDesbloqueados.Clear();
        PontosDisponiveis = 0;
    }
}
