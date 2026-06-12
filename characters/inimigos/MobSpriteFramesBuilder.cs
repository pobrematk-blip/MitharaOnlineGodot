using Godot;
using System.Collections.Generic;

public static class MobSpriteFramesBuilder
{
    public const int FrameSize = 64;

    private static readonly Dictionary<string, MobSpriteConfig> Configs = new()
    {
        ["goblin"] = new MobSpriteConfig
        {
            SheetPath = "res://characters/Inimigos/SpriteInimigo/MonstroGoblim.png",
            Walk = new AnimRowConfig { Rows = [10, 9, 11, 8], StartCol = 1, FrameCount = 8, Speed = 5f },
            Idle = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 2, Speed = 3f,
                IdleDownFallback = true },
            Attack = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 6, Speed = 5f },
            Prefix = "goblin_",
        },
        ["lobo"] = new MobSpriteConfig
        {
            SheetPath = "res://characters/Inimigos/SpriteInimigo/MonstroLoboArqueiro.png",
            Walk = new AnimRowConfig { Rows = [10, 9, 11, 8], StartCol = 0, FrameCount = 9, Speed = 5f },
            Idle = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 2, Speed = 3f,
                IdleDownFallback = true },
            Attack = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 6, Speed = 5f },
            Prefix = "lobo_",
        },
        ["porco"] = new MobSpriteConfig
        {
            SheetPath = "res://characters/Inimigos/SpriteInimigo/MonstroPorco.png",
            Walk = new AnimRowConfig { Rows = [10, 9, 11, 8], StartCol = 0, FrameCount = 9, Speed = 5f },
            Idle = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 2, Speed = 3f,
                IdleDownFallback = true },
            Attack = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 6, Speed = 5f },
            Prefix = "porco_",
        },
        ["minotauro"] = new MobSpriteConfig
        {
            SheetPath = "res://characters/Inimigos/SpriteInimigo/Monstro Minotauro.png",
            Walk = new AnimRowConfig { Rows = [10, 9, 11, 8], StartCol = 0, FrameCount = 9, Speed = 5f },
            Idle = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 2, Speed = 3f,
                IdleDownFallback = true },
            Attack = new AnimRowConfig { Rows = [14, 13, 15, 12], StartCol = 0, FrameCount = 6, Speed = 5f },
            Prefix = "minotauro_",
        },
    };

    private static readonly string[] Direcoes = { "down", "left", "right", "up" };

    private static readonly Dictionary<string, SpriteFrames> _cache = new();

    public static SpriteFrames GetOrBuild(string mobType)
    {
        if (_cache.TryGetValue(mobType, out var cached))
            return cached;

        if (!Configs.TryGetValue(mobType, out var config))
        {
            GD.PrintErr($"[MobSpriteFramesBuilder] Config nao encontrado para: {mobType}");
            return new SpriteFrames();
        }

        if (!ResourceLoader.Exists(config.SheetPath))
        {
            GD.PrintErr($"[MobSpriteFramesBuilder] Sprite sheet nao encontrado: {config.SheetPath}");
            return new SpriteFrames();
        }

        var sheet = ResourceLoader.Load<Texture2D>(config.SheetPath);
        if (sheet == null)
        {
            GD.PrintErr($"[MobSpriteFramesBuilder] Falha ao carregar: {config.SheetPath}");
            return new SpriteFrames();
        }

        var frames = Build(config, sheet);
        _cache[mobType] = frames;
        return frames;
    }

    public static string ObterPrefixo(string mobType)
    {
        return Configs.TryGetValue(mobType, out var config) ? config.Prefix : "goblin_";
    }

    private static SpriteFrames Build(MobSpriteConfig cfg, Texture2D sheet)
    {
        var frames = new SpriteFrames();
        var size = sheet.GetSize();

        for (int d = 0; d < 4; d++)
        {
            string dir = Direcoes[d];
            int row = cfg.Walk.Rows[d];
            string walkName = $"{cfg.Prefix}walk_{dir}";
            AdicionarAnimacao(frames, sheet, size, walkName, row, cfg.Walk.StartCol, cfg.Walk.FrameCount, true, cfg.Walk.Speed);
        }

        for (int d = 0; d < 4; d++)
        {
            string dir = Direcoes[d];
            int row = cfg.Idle.Rows[d];

            string idleName = $"{cfg.Prefix}idle_{dir}";

            if (cfg.Idle.IdleDownFallback && dir == "down")
            {
                string walkAnim = $"{cfg.Prefix}walk_down";
                if (frames.HasAnimation(walkAnim))
                {
                    var fref = frames.GetFrameTexture(walkAnim, 0);
                    int x = cfg.Walk.StartCol * FrameSize;
                    int y = cfg.Walk.Rows[0] * FrameSize;
                    if (IsInBounds(x, y, size))
                    {
                        var atlas = CriarAtlas(sheet, cfg.Walk.StartCol, cfg.Walk.Rows[0]);
                        var atlas2 = CriarAtlas(sheet, cfg.Walk.StartCol + 1, cfg.Walk.Rows[0]);
                        if (atlas != null && atlas2 != null)
                        {
                            frames.AddAnimation(idleName);
                            frames.SetAnimationLoop(idleName, true);
                            frames.SetAnimationSpeed(idleName, cfg.Idle.Speed);
                            frames.AddFrame(idleName, atlas, 1.0f);
                            frames.AddFrame(idleName, atlas2, 1.0f);
                            continue;
                        }
                    }
                }
            }

            AdicionarAnimacao(frames, sheet, size, idleName, row, cfg.Idle.StartCol, cfg.Idle.FrameCount, true, cfg.Idle.Speed);
        }

        for (int d = 0; d < 4; d++)
        {
            string dir = Direcoes[d];
            int row = cfg.Attack.Rows[d];
            string atkName = $"{cfg.Prefix}attack_{dir}";
            AdicionarAnimacao(frames, sheet, size, atkName, row, cfg.Attack.StartCol, cfg.Attack.FrameCount, false, cfg.Attack.Speed);
        }

        return frames;
    }

    private static void AdicionarAnimacao(SpriteFrames frames, Texture2D sheet, Vector2 sheetSize,
        string nome, int row, int startCol, int frameCount, bool loop, float speed)
    {
        var atlas = CriarAtlas(sheet, startCol, row);
        if (atlas == null) return;

        frames.AddAnimation(nome);
        frames.SetAnimationLoop(nome, loop);
        frames.SetAnimationSpeed(nome, speed);

        for (int col = startCol; col < startCol + frameCount; col++)
        {
            int x = col * FrameSize;
            int y = row * FrameSize;
            if (x + FrameSize > sheetSize.X || y + FrameSize > sheetSize.Y)
                break;

            var f = CriarAtlas(sheet, col, row);
            if (f != null)
                frames.AddFrame(nome, f, 1.0f);
        }
    }

    private static AtlasTexture CriarAtlas(Texture2D sheet, int col, int row)
    {
        int x = col * FrameSize;
        int y = row * FrameSize;
        var size = sheet.GetSize();
        if (x + FrameSize > size.X || y + FrameSize > size.Y)
            return null;

        return new AtlasTexture
        {
            Atlas = sheet,
            Region = new Rect2(x, y, FrameSize, FrameSize),
        };
    }

    private static bool IsInBounds(int x, int y, Vector2 sheetSize)
    {
        return x + FrameSize <= sheetSize.X && y + FrameSize <= sheetSize.Y;
    }

    private struct MobSpriteConfig
    {
        public string SheetPath;
        public AnimRowConfig Walk;
        public AnimRowConfig Idle;
        public AnimRowConfig Attack;
        public string Prefix;
    }

    private struct AnimRowConfig
    {
        public int[] Rows;
        public int StartCol;
        public int FrameCount;
        public float Speed;
        public bool IdleDownFallback;
    }
}
