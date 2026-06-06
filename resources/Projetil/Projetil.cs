using Godot;
using System;

public partial class Projetil : Area2D
{
    // Valores padrão (serão alterados pelo Player.cs dependendo da classe)
    public float Speed = 250.0f; 
    public int DanoMin = 8;              // Dano mínimo
    public int DanoMax = 12;             // Dano máximo
    public bool EhDanoMagico = false;    // Se for true, usa DanoMagico, se false usa DanoFisico
    
    private Vector2 _direcao = Vector2.Zero;
    private EquipamentoComponent _equipamentoDoPlayer;  // Referência ao sistema de status do Player

    public override void _Ready()
    {
        // Conecta o sinal para saber quando o projétil bateu em algo
        BodyEntered += OnBodyEntered;

        // Se o projétil não bater em nada, ele se destrói sozinho em 3 segundos
        GetTree().CreateTimer(3.0f).Timeout += QueueFree;
        
        // Tenta encontrar o EquipamentoComponent do Player na cena
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player != null)
        {
            _equipamentoDoPlayer = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Usa GlobalPosition para alinhar perfeitamente com o mundo
        GlobalPosition += _direcao * Speed * (float)delta;
    }

    // Função que recebe a direção de quem disparou
    public void DefinirDirecao(Vector2 direcao)
    {
        _direcao = direcao.Normalized();
        
        // Rotaciona o sprite para apontar para onde está voando
        Rotation = _direcao.Angle();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
        {
            QueueFree();
            return;
        }

        if (body.IsInGroup("Inimigos"))
        {
            int danoFinal = CalcularDanoComCritico();
            bool ehCritico = danoFinal > DanoMax;
            if (body is Inimigo inimigo)
                inimigo.LevarDano(danoFinal);
            else
                body.Call("LevarDano", danoFinal);

            if (ehCritico)
                GD.Print($"Critico! {danoFinal} de dano em {body.Name}!");
            else
                GD.Print($"Projetil acertou {body.Name}! {danoFinal} de dano.");
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
        {
            ulong? targetId = null;
            if (body.HasMeta("network_id"))
                targetId = (ulong)body.GetMeta("network_id");

            if (targetId.HasValue)
                gameNet.SendAttack(targetId.Value);
        }

        QueueFree();
    }

    /// <summary>
    /// Calcula o dano final aplicando variação, chance de crítico e multiplicador de dano crítico
    /// </summary>
    private int CalcularDanoComCritico()
    {
        // Se não temos acesso ao EquipamentoComponent, retorna dano variado base
        if (_equipamentoDoPlayer == null)
        {
            return (int)(GD.Randi() % (DanoMax - DanoMin + 1)) + DanoMin;
        }

        // Calcula dano base com variação (min + random entre 0 e max-min)
        int danoBase = (int)(GD.Randi() % (DanoMax - DanoMin + 1)) + DanoMin;

        // Calcula a chance de crítico (em percentual)
        float chanceCritica = _equipamentoDoPlayer.ChanceCritica;  // Ex: 5% com 10 de Destreza
        
        // Gera número aleatório entre 0 e 100
        float random = GD.Randf() * 100;
        
        // Se for crítico
        if (random < chanceCritica)
        {
            // Aplica o multiplicador de dano crítico sobre o dano variado
            float multiplicadorCritico = _equipamentoDoPlayer.DanoCritico;  // Ex: 2.0x com itens
            return (int)(danoBase * multiplicadorCritico);
        }
        
        // Retorna dano base variado se não for crítico
        return danoBase;
    }
}