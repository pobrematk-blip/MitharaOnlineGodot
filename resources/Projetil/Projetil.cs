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

    private double _tempoDeVida = 0;

    public override void _Process(double delta)
    {
        _tempoDeVida += delta;
    }

    private void OnBodyEntered(Node2D body)
    {
        var collBody = body as CollisionObject2D;
        GD.Print($"[PROJETIL] Colidiu com: {body.Name} (tipo={body.GetType().Name}, layer={(collBody?.CollisionLayer ?? 0)}, grupo Inimigos={body.IsInGroup("Inimigos")})");

        if (body is Player)
        {
            if (_tempoDeVida < 0.15)
                return;
            QueueFree();
            return;
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        bool online = gameNet != null && gameNet.IsConnected;

        if (body.IsInGroup("Inimigos"))
        {
            if (online)
            {
                ulong? targetId = null;
                if (body.HasMeta("network_id"))
                {
                    targetId = (ulong)body.GetMeta("network_id");
                    GD.Print($"[PROJETIL] Online - enviando C2S_Attack para target {targetId.Value}");
                    gameNet.SendAttack(targetId.Value);
                }
                else
                {
                    GD.PrintErr("[PROJETIL] Inimigo sem network_id! Nao e possivel atacar online.");
                }
            }
            else
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