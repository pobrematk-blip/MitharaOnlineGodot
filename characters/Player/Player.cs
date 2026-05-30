using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Signal] public delegate void StatusAtualizadoEventHandler();

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
    private float _attackTimeoutCounter = 0f;
    private float _maxAttackDuration = 0.8f; // Timeout reduzido para animações rápidas

    public int CurrentHealth { get; private set; }
    public int CurrentMana { get; private set; }
    [Export] public float MeleeAttackRange = 48.0f;
    [Export] public float AttackDotThreshold = 0.5f;
    
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

        CurrentHealth = MaxHealth;
        CurrentMana = MaxMana;
        InitClass(); 
    }

    public virtual void InitClass() { }

    public override void _PhysicsProcess(double delta)
    {
        // Se o sprite falhar ao carregar, não roda a física para evitar crashes
        if (AnimatedSprite == null) return;

        // Gerencia timeout de ataque para evitar travamento
        if (IsAttacking)
        {
            _attackTimeoutCounter += (float)delta;
            if (_attackTimeoutCounter >= _maxAttackDuration)
            {
                GD.PrintErr("[PLAYER] ⚠️ TIMEOUT: Ataque demorou muito, liberando manualmente!");
                IsAttacking = false;
                _attackTimeoutCounter = 0f;
            }
        }

        // Permite movimento durante o ataque (removido o if (IsAttacking) return;)
        if (Input.IsActionJustPressed("atacar"))
        {
            // Só inicia novo ataque se não estiver atacando
            if (!IsAttacking)
            {
                // Verifica se inventário ou tela de personagem estão abertos
                var inventarioUI = GetTree().CurrentScene.FindChild("InventarioUi", true, false) as InventarioUI;
                var characterUI = GetTree().CurrentScene.FindChild("CharacterUI", true, false) as CharacterUI;
                var bancoUI = GetTree().CurrentScene.FindChild("BancoUi", true, false) as BancoUI;
                bool inventarioAberto = inventarioUI != null && inventarioUI.PainelVisivel;
                bool characterAberto = characterUI != null && characterUI.PainelVisivel;
                bool bancoAberto = bancoUI != null && bancoUI.PainelVisivel;
                
                if (!inventarioAberto && !characterAberto && !bancoAberto)
                {
                    Atacar();
                    return;
                }
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

        // Não sobrescrever a animação de ataque enquanto ela estiver em execução.
        if (IsAttacking && AnimatedSprite.Animation.ToString().Contains("attack"))
        {
            return;
        }

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
            _attackTimeoutCounter = 0f;
            AnimatedSprite.Play(animacaoDeAtaque);
            AnimatedSprite.SpeedScale = 2.0f; // Ataque 2x mais rápido
            GD.Print($"[PLAYER] ⚔️ Iniciando ataque: '{animacaoDeAtaque}'");

            if (NomeDaClasse == "mago" || NomeDaClasse == "arqueiro")
            {
                if (ProjetilScene != null)
                    DispararProjetil();
                else
                    GD.PrintErr("[PLAYER] Erro: ProjetilScene não configurada para ataque à distância!");
            }
            else
            {
                ExecutarAtaqueMelee();
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

        var novoProjetil = ProjetilScene.Instantiate<Node2D>();
        if (novoProjetil == null)
        {
            GD.PrintErr("[PLAYER] Erro: não foi possível instanciar o projetil.");
            return;
        }

        Vector2 direcaoDoVetor = CurrentDirection switch
        {
            "up" => Vector2.Up,
            "left" => Vector2.Left,
            "right" => Vector2.Right,
            _ => Vector2.Down
        };

        // Posiciona o projétil um pouco à frente do player para evitar colisão instantânea com o próprio corpo.
        novoProjetil.GlobalPosition = GlobalPosition + direcaoDoVetor * 18;
        GetParent().AddChild(novoProjetil);

        GD.Print($"[PLAYER] Projetil instanciado em {novoProjetil.GlobalPosition}, direcao: {direcaoDoVetor}");

        if (novoProjetil is Projetil proj)
        {
            proj.DefinirDirecao(direcaoDoVetor);
            if (NomeDaClasse == "mago")
            {
                proj.Speed = 200.0f;
                proj.DanoMin = 20;
                proj.DanoMax = 30;
                proj.EhDanoMagico = true;
            }
            else if (NomeDaClasse == "arqueiro")
            {
                proj.Speed = 450.0f;
                proj.DanoMin = 10;
                proj.DanoMax = 16;
                proj.EhDanoMagico = false;
            }
        }
        else
        {
            GD.PrintErr("[PLAYER] Erro: O projétil instanciado não é do tipo Projetil.");
        }
    }

    private void ExecutarAtaqueMelee()
    {
        int dano = CalcularDanoFisico();
        Vector2 direcaoAtaque = CurrentDirection switch
        {
            "up" => Vector2.Up,
            "down" => Vector2.Down,
            "left" => Vector2.Left,
            "right" => Vector2.Right,
            _ => Vector2.Down
        };

        bool acertou = false;
        var inimigos = GetTree()?.GetNodesInGroup("Inimigos");
        if (inimigos != null)
        {
            foreach (Node item in inimigos)
            {
                if (item is Inimigo inimigo)
                {
                    float distancia = GlobalPosition.DistanceTo(inimigo.GlobalPosition);
                    if (distancia <= MeleeAttackRange)
                    {
                        Vector2 paraInimigo = (inimigo.GlobalPosition - GlobalPosition).Normalized();
                        if (direcaoAtaque.Dot(paraInimigo) >= AttackDotThreshold)
                        {
                            inimigo.LevarDano(dano);
                            GD.Print($"[PLAYER] Acertou {inimigo.NomeDoInimigo}! Dano: {dano}");
                            acertou = true;
                            break;
                        }
                    }
                }
            }
        }

        if (!acertou)
            GD.Print("[PLAYER] Ataque melee não acertou nenhum inimigo.");
    }

    private int CalcularDanoFisico()
    {
        var equipamento = FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        return equipamento != null ? equipamento.CalcularDanoFisicoAleatorio() : 12;
    }

    public void LevarDano(int quantidade)
    {
        if (quantidade <= 0) return;

        CurrentHealth -= quantidade;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);
        GD.Print($"[PLAYER] Levou {quantidade} de dano! Vida restante: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
            Morrer();

        EmitSignal(SignalName.StatusAtualizado);
    }

    private void Morrer()
    {
        GD.Print("[PLAYER] 💀 O Player foi derrotado!");
        IsAttacking = false;
        Velocity = Vector2.Zero;
        SetPhysicsProcess(false);
        SetProcess(false);

        if (AnimatedSprite != null && AnimatedSprite.SpriteFrames != null && AnimatedSprite.SpriteFrames.HasAnimation("death"))
            AnimatedSprite.Play("death");
    }

    protected virtual void OnAnimationFinished()
    {
        if (AnimatedSprite == null) return;

        if (AnimatedSprite.Animation.ToString().Contains("attack"))
        {
            GD.Print($"[PLAYER] ✅ Animação de ataque '{AnimatedSprite.Animation}' terminou. Liberando IsAttacking.");
            IsAttacking = false;
            _attackTimeoutCounter = 0f;
            AnimatedSprite.SpeedScale = 1.0f; // Reseta para velocidade normal
        }
    }
}