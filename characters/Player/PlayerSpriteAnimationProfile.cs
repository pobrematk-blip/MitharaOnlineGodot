using Godot;

[GlobalClass]
public partial class PlayerSpriteAnimationProfile : Resource
{
    [ExportGroup("Grade")]
    [Export] public int FrameWidth { get; set; } = 128;
    [Export] public int FrameHeight { get; set; } = 128;

    [ExportGroup("Movimento")]
    [Export] public int WalkUpRow { get; set; } = 8;
    [Export] public int WalkLeftRow { get; set; } = 9;
    [Export] public int WalkDownRow { get; set; } = 10;
    [Export] public int WalkRightRow { get; set; } = 11;
    [Export] public int WalkUpStartCol { get; set; } = 1;
    [Export] public int WalkLeftStartCol { get; set; } = 0;
    [Export] public int WalkDownStartCol { get; set; } = 1;
    [Export] public int WalkRightStartCol { get; set; } = 0;
    [Export] public int WalkUpFrames { get; set; } = 8;
    [Export] public int WalkLeftFrames { get; set; } = 9;
    [Export] public int WalkDownFrames { get; set; } = 8;
    [Export] public int WalkRightFrames { get; set; } = 9;
    [Export] public float WalkSpeed { get; set; } = 10f;

    [ExportGroup("Corrida")]
    [Export] public bool UseWalkAsRun { get; set; } = true;
    [Export] public float RunSpeed { get; set; } = 12f;

    [ExportGroup("Ataque")]
    [Export] public int AttackFrameWidth { get; set; } = 0;
    [Export] public int AttackFrameHeight { get; set; } = 0;
    [Export] public int AttackUpRow { get; set; } = 27;
    [Export] public int AttackLeftRow { get; set; } = 28;
    [Export] public int AttackDownRow { get; set; } = 29;
    [Export] public int AttackRightRow { get; set; } = 30;
    [Export] public int AttackUpStartCol { get; set; } = 0;
    [Export] public int AttackLeftStartCol { get; set; } = 0;
    [Export] public int AttackDownStartCol { get; set; } = 0;
    [Export] public int AttackRightStartCol { get; set; } = 0;
    [Export] public int AttackUpFrames { get; set; } = 6;
    [Export] public int AttackLeftFrames { get; set; } = 6;
    [Export] public int AttackDownFrames { get; set; } = 6;
    [Export] public int AttackRightFrames { get; set; } = 6;
    [Export] public float AttackSpeed { get; set; } = 8f;

    public int GetWalkRow(int directionIndex) => directionIndex switch
    {
        0 => WalkUpRow,
        1 => WalkLeftRow,
        2 => WalkDownRow,
        _ => WalkRightRow
    };

    public int GetWalkStartCol(int directionIndex) => directionIndex switch
    {
        0 => WalkUpStartCol,
        1 => WalkLeftStartCol,
        2 => WalkDownStartCol,
        _ => WalkRightStartCol
    };

    public int GetWalkFrameCount(int directionIndex) => directionIndex switch
    {
        0 => WalkUpFrames,
        1 => WalkLeftFrames,
        2 => WalkDownFrames,
        _ => WalkRightFrames
    };

    public int GetAttackRow(int directionIndex) => directionIndex switch
    {
        0 => AttackUpRow,
        1 => AttackLeftRow,
        2 => AttackDownRow,
        _ => AttackRightRow
    };

    public int GetAttackStartCol(int directionIndex) => directionIndex switch
    {
        0 => AttackUpStartCol,
        1 => AttackLeftStartCol,
        2 => AttackDownStartCol,
        _ => AttackRightStartCol
    };

    public int GetAttackFrameCount(int directionIndex) => directionIndex switch
    {
        0 => AttackUpFrames,
        1 => AttackLeftFrames,
        2 => AttackDownFrames,
        _ => AttackRightFrames
    };
}
