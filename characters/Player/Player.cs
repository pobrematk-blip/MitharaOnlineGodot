using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Export] public float MaxSpeed = 185.0f;
    [Export] public float Acceleration = 1200.0f;
    [Export] public float Friction = 1500.0f;
    
    [Export] public int MaxHealth = 100;
    [Export] public int MaxMana = 50;

    // Campo para arrastar a cena do projétil (projetil.tscn) no Inspetor da Godot
    [Export] public PackedScene ProjetilScene;

    protected AnimatedSprite2D AnimatedSprite;
    protected string CurrentDirection = "down";
    protected bool IsAttacking = false;

    // Define qual classe esse script está controlando no momento
    protected string NomeDaClasse = "mago"; 

    public override void _Ready()
    {
        // Verifica se o nó AnimatedSprite realmente existe antes de usá-lo
        if (HasNode("AnimatedSprite"))
        {
            AnimatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite");
            AnimatedSprite.AnimationFinished += OnAnimationFinished;
        }
        else
        {
            GD.PrintErr("[PLAYER] Erro: O nó filho 'AnimatedSprite' não foi encontrado na cena do Player!");
        }

        InitClass(); 
    }

    public virtual void InitClass() { }

    public override void _PhysicsProcess(double delta)
    {
        // Se o sprite falhar ao carregar, não roda a física para evitar crashes
        if (AnimatedSprite == null) return;

        // Se estiver travado no ataque, não processa movimento
        if (IsAttacking) return;

        if (Input.IsActionJustPressed("atacar"))
        {
            // Verifica se o inventário está aberto - se estiver, NÃO ataca
            var inventarioUI = GetTree().CurrentScene.FindChild("InventarioUi", true, false) as InventarioUI;
            if (inventarioUI == null || !inventarioUI.Visible)
            {
                Atacar();
                return;
            }
        }

        Vector2 inputDirection = Vector2.Zero;
        if (Input.IsActionPressed("mover_direita"))   inputDirection.X += 1;
        if (Input.IsActionPressed("mover_esquerda"))  inputDirection.X -= 1;
        if (Input.IsActionPressed("mover_baixo"))     inputDirection.Y += 1;
        if (Input.IsActionPressed("mover_cima"))      inputDirection.Y -= 1;

        inputDirection = inputDirection.Normalized();
        Vector2 velocity = Velocity;

        if (inputDirection != Vector2.Zero)
            velocity = velocity.MoveToward(inputDirection * MaxSpeed, Acceleration * (float)delta);
        else
            velocity = velocity.MoveToward(Vector2.Zero, Friction * (float)delta);

        Velocity = velocity;
        MoveAndSlide();
        UpdateAnimation(velocity);
    }

    private void UpdateAnimation(Vector2 velocity)
    {
        if (AnimatedSprite == null) return;

        if (velocity.Length() > 10.0f)
        {
            if (Mathf.Abs(velocity.X) > Mathf.Abs(velocity.Y))
                CurrentDirection = velocity.X > 0 ? "right" : "left";
            else
                CurrentDirection = velocity.Y > 0 ? "down" : "up";

            AnimatedSprite.Play($"walk_{CurrentDirection}");
            AnimatedSprite.SpeedScale = velocity.Length() / MaxSpeed;
        }
        else
        {
            AnimatedSprite.Play($"idle_{CurrentDirection}");
            AnimatedSprite.SpeedScale = 1.0f; 
        }
    }

    public virtual void Atacar()
    {
        if (AnimatedSprite == null) return;

        string animacaoDeAtaque = $"{NomeDaClasse}_attack_{CurrentDirection}";

        // SEGURANÇA: Só ataca se a animação customizada existir no seu AnimatedSprite
        if (AnimatedSprite.SpriteFrames != null && AnimatedSprite.SpriteFrames.HasAnimation(animacaoDeAtaque))
        {
            IsAttacking = true;
            Velocity = Vector2.Zero;
            AnimatedSprite.Play(animacaoDeAtaque);

            // Se for uma classe à distância e a cena do projétil foi configurada, faz o disparo
            if ((NomeDaClasse == "mago" || NomeDaClasse == "arqueiro") && ProjetilScene != null)
            {
                DispararProjetil();
            }
        }
        else
        {
            GD.PrintErr($"[PLAYER] Erro: Crie a animação '{animacaoDeAtaque}' no seu AnimatedSprite!");
            
            // Força a liberação do estado de ataque caso a animação falte, pro player não travar
            IsAttacking = false; 
        }
    }

    private void DispararProjetil()
    {
        if (ProjetilScene == null) return;

        // Instancia de forma genérica como Node2D para o compilador C# não travar se o script do projétil estiver quebrado
        Node2D novoProjetil = ProjetilScene.Instantiate<Node2D>();
        if (novoProjetil == null) return;

        // Adiciona o projétil ao mapa antes de setar a posição global
        GetParent().AddChild(novoProjetil);
        novoProjetil.GlobalPosition = this.GlobalPosition;

        // Tenta aplicar as variáveis de velocidade/dano usando reflexão dinâmica de forma segura
        try 
        {
            dynamic proj = novoProjetil;
            
            if (NomeDaClasse == "mago")
            {
                proj.Speed = 200.0f;  // Bola de fogo mais lenta
                proj.Dano = 25;       // Mas dá muito mais dano!
            }
            else if (NomeDaClasse == "arqueiro")
            {
                proj.Speed = 450.0f;  // Flecha muito rápida!
                proj.Dano = 12;       // Menos dano por flecha
            }

            Vector2 direcaoDoVetor = Vector2.Down;
            switch (CurrentDirection)
            {
                case "up":    direcaoDoVetor = Vector2.Up; break;
                case "left":  direcaoDoVetor = Vector2.Left; break;
                case "right": direcaoDoVetor = Vector2.Right; break;
            }

            // Tenta chamar a função do projétil se ela existir lá dentro
            if (novoProjetil.HasMethod("DefinirDirecao"))
            {
                proj.DefinirDirecao(direcaoDoVetor);
            }
        }
        catch (Exception e)
        {
            GD.Print($"[PLAYER] Nota: Atributos do projétil injetados, mas o script de movimento dele gerou: {e.Message}");
        }
    }

    protected virtual void OnAnimationFinished()
    {
        if (AnimatedSprite == null) return;

        if (AnimatedSprite.Animation.ToString().Contains("attack"))
        {
            IsAttacking = false;
        }
    }
}