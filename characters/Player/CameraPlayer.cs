using Godot;
using System;

public partial class CameraPlayer : Camera2D
{
    // Velocidade do amortecimento (quanto menor, mais suave o deslize da câmera)
    [Export] public float Suavidade = 10.0f;

    private Node2D _alvo;

    public override void _Ready()
    {
        // Como a câmera é filha do Player, o "Pai" dela é o nosso alvo!
        _alvo = GetParent() as Node2D;

        if (_alvo != null)
        {
            GD.Print("[CMERA] Alvo travado no Player com sucesso!");
            
            // Força a câmera a começar exatamente em cima do Player
            GlobalPosition = _alvo.GlobalPosition;
            MakeCurrent();
        }
        else
        {
            GD.PrintErr("[CMERA] Erro: Não foi possível encontrar o nó do Player acima da câmera!");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_alvo == null) return;

        // Interpolador (Lerp) faz o cálculo matemático para a câmera deslizar suavemente até o Player
        Vector2 posicaoDesejada = _alvo.GlobalPosition;
        GlobalPosition = GlobalPosition.Lerp(posicaoDesejada, Suavidade * (float)delta);
    }
}