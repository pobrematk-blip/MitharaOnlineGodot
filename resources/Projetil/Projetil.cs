using Godot;
using System;

public partial class Projetil : Area2D
{
    // Valores padrão (serão alterados pelo Player.cs dependendo da classe)
    public float Speed = 250.0f; 
    public int Dano = 10;
    
    private Vector2 _direcao = Vector2.Zero;

    public override void _Ready()
    {
        // Conecta o sinal para saber quando o projétil bateu em algo
        BodyEntered += OnBodyEntered;

        // Se o projétil não bater em nada, ele se destrói sozinho em 3 segundos
        GetTree().CreateTimer(3.0f).Timeout += QueueFree;
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
        // COMETÁRIO DE SEGURANÇA: Removemos a menção direta ao InimigoBase por enquanto
        // para evitar o erro de compilação. Usaremos Grupos no futuro!
        if (body.IsInGroup("Inimigos"))
        {
            // body.Call("LevarDano", Dano); // Exemplo de como chamaremos depois
            GD.Print($"Projétil deu dano em um monstro!");
        }
        
        GD.Print($"Projétil acertou: {body.Name}");
        
        // Destrói o projétil ao impactar
        QueueFree();
    }
}