using Godot;

public partial class SkillEffectVisual : Node2D
{
    [Export] public bool AutoPlay { get; set; } = true;
    [Export] public bool FreeOnAnimationFinished { get; set; } = true;
    [Export] public float LifetimeSeconds { get; set; } = 1.2f;

    private AnimatedSprite2D _sprite;
    private AudioStreamPlayer2D _audio;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")
            ?? FindChild("AnimatedSprite2D", true, false) as AnimatedSprite2D;
        _audio = GetNodeOrNull<AudioStreamPlayer2D>("AudioStreamPlayer2D")
            ?? FindChild("AudioStreamPlayer2D", true, false) as AudioStreamPlayer2D;

        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;

            if (AutoPlay)
                Play();
        }

        if (_audio != null && AutoPlay)
            _audio.Play();

        if (LifetimeSeconds > 0)
            GetTree().CreateTimer(LifetimeSeconds).Timeout += QueueFreeSafe;
    }

    public void Play()
    {
        if (_sprite == null || _sprite.SpriteFrames == null)
            return;

        string animation = _sprite.Animation.ToString();
        if (string.IsNullOrWhiteSpace(animation) || !_sprite.SpriteFrames.HasAnimation(animation))
            animation = _sprite.SpriteFrames.HasAnimation("default") ? "default" : string.Empty;

        if (string.IsNullOrWhiteSpace(animation))
            return;

        _sprite.Frame = 0;
        _sprite.Play(animation);
    }

    private void OnAnimationFinished()
    {
        if (FreeOnAnimationFinished)
            QueueFreeSafe();
    }

    private void QueueFreeSafe()
    {
        if (IsInsideTree() && !IsQueuedForDeletion())
            QueueFree();
    }
}
