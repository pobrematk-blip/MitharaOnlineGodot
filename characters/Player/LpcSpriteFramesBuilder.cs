using Godot;
using System;
using System.Collections.Generic;

public static class LpcSpriteFramesBuilder
{
    public const int FrameSize = 64;
    public const string PastaSpritesRaca = "res://characters/Player/SpritePlayer/";
    public const string PastaSpritesNpc = "res://characters/Npcs/";

    private const int RowSpellcast = 0;
    private const int RowThrust = 4;
    private const int RowWalk = 8;
    private const int RowSlash = 12;
    private const int RowShoot = 16;
    private const int RowHurt = 20;
    private const int RowIdle = 22;
    private const int RowJump = 26;
    private const int RowRun = 38;

    private static readonly string[] Direcoes = { "up", "left", "down", "right" };
    private static readonly int[] WalkStartCols = { 1, 0, 1, 0 };
    private static readonly int[] WalkFrameCounts = { 8, 9, 8, 9 };
    private static readonly int[] RunStartCols = { 1, 0, 1, 0 };
    private static readonly int[] RunFrameCounts = { 8, 9, 8, 9 };

    private static readonly Dictionary<string, (int row, int frames, float speed)> MapaAtaque = new()
    {
        ["mago"] = (RowSpellcast, 7, 5f),
        ["arqueiro"] = (RowShoot, 13, 10f),
        ["ladino"] = (RowSlash, 6, 5f),
        ["guerreiro"] = (RowSlash, 6, 5f),
    };

    private static readonly Dictionary<string, SpriteFrames> _cache = new();

    public static SpriteFrames Construir(Texture2D sheet, string prefixoAtaque)
    {
        if (sheet == null) return new SpriteFrames();

        prefixoAtaque = string.IsNullOrWhiteSpace(prefixoAtaque) ? "mago" : prefixoAtaque.Trim().ToLowerInvariant();

        string cacheKey = $"{sheet.ResourcePath}:{prefixoAtaque}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new SpriteFrames();
        var (atkRowBase, atkFrameCount, atkSpeed) = MapaAtaque.GetValueOrDefault(prefixoAtaque, (RowSlash, 6, 5f));

        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            int walkRow = RowWalk + d;
            AdicionarAnimacao(frames, sheet, $"walk_{dir}", walkRow, WalkFrameCounts[d], true, 10f, FrameSize, FrameSize, WalkStartCols[d]);
            AdicionarAnimacao(frames, sheet, $"run_{dir}", RowRun + d, RunFrameCounts[d], true, 12f, FrameSize, FrameSize, RunStartCols[d]);
            AdicionarAnimacao(frames, sheet, $"idle_{dir}", RowIdle + d, 2, true, 3f);
        }

        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            int row = atkRowBase + d;
            string animName = $"{prefixoAtaque}_attack_{dir}";
            AdicionarAnimacao(frames, sheet, animName, row, atkFrameCount, false, atkSpeed);
        }

        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            AdicionarAnimacao(frames, sheet, $"jump_{dir}", RowJump + d, 5, false, 10f);
        }

        AdicionarAnimacao(frames, sheet, "death", RowHurt, 6, false, 5f);

        _cache[cacheKey] = frames;
        return frames;
    }

    public static SpriteFrames ConstruirEquipamento(Texture2D sheetBase, Texture2D sheetAtaque, string prefixoAtaque)
    {
        bool usarMapaArco = EhSpritesheetArco(sheetBase) || EhSpritesheetArco(sheetAtaque);
        int[] movimentoRows = usarMapaArco ? new[] { 27, 28, 29, 30 } : null;
        int[] ataqueRows = usarMapaArco ? new[] { 16, 17, 18, 19 } : null;
        int movimentoFrameSize = usarMapaArco ? 128 : FrameSize;
        return ConstruirEquipamento(sheetBase, sheetAtaque, prefixoAtaque, movimentoRows, movimentoFrameSize, ataqueRows, FrameSize);
    }

    public static SpriteFrames ConstruirEquipamento(
        Texture2D sheetBase,
        Texture2D sheetAtaque,
        string prefixoAtaque,
        PlayerSpriteAnimationProfile profile)
    {
        if (profile == null)
            return ConstruirEquipamento(sheetBase, sheetAtaque, prefixoAtaque);

        var sheetPadrao = sheetBase ?? sheetAtaque;
        if (sheetPadrao == null) return new SpriteFrames();

        prefixoAtaque = string.IsNullOrWhiteSpace(prefixoAtaque) ? "mago" : prefixoAtaque.Trim().ToLowerInvariant();
        string ataquePath = sheetAtaque?.ResourcePath ?? "";
        string basePath = sheetBase?.ResourcePath ?? "";
        string cacheKey = $"equip-profile:{basePath}:{ataquePath}:{prefixoAtaque}:{profile.ResourcePath}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new SpriteFrames();
        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            int walkRow = profile.GetWalkRow(d);
            int walkStartCol = profile.GetWalkStartCol(d);
            int walkFrames = profile.GetWalkFrameCount(d);
            AdicionarAnimacao(frames, sheetPadrao, $"walk_{dir}", walkRow, walkFrames, true, profile.WalkSpeed, profile.FrameWidth, profile.FrameHeight, walkStartCol);
            AdicionarAnimacao(frames, sheetPadrao, $"run_{dir}", walkRow, walkFrames, true, profile.RunSpeed, profile.FrameWidth, profile.FrameHeight, walkStartCol);
        }

        var sheetAtaqueFinal = sheetAtaque ?? sheetPadrao;
        int attackFrameWidth = profile.AttackFrameWidth > 0 ? profile.AttackFrameWidth : profile.FrameWidth;
        int attackFrameHeight = profile.AttackFrameHeight > 0 ? profile.AttackFrameHeight : profile.FrameHeight;
        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            AdicionarAnimacao(
                frames,
                sheetAtaqueFinal,
                $"{prefixoAtaque}_attack_{dir}",
                profile.GetAttackRow(d),
                profile.GetAttackFrameCount(d),
                false,
                profile.AttackSpeed,
                attackFrameWidth,
                attackFrameHeight,
                profile.GetAttackStartCol(d));
        }

        _cache[cacheKey] = frames;
        return frames;
    }

    public static SpriteFrames ConstruirEquipamento(
        Texture2D sheetBase,
        Texture2D sheetAtaque,
        string prefixoAtaque,
        int[] movimentoRows,
        int movimentoFrameSize,
        int[] ataqueRows,
        int ataqueFrameSize,
        int ataqueFrameCountOverride = 0)
    {
        var sheetPadrao = sheetBase ?? sheetAtaque;
        if (sheetPadrao == null) return new SpriteFrames();

        prefixoAtaque = string.IsNullOrWhiteSpace(prefixoAtaque) ? "mago" : prefixoAtaque.Trim().ToLowerInvariant();
        string ataquePath = sheetAtaque?.ResourcePath ?? "";
        string basePath = sheetBase?.ResourcePath ?? "";
        string cacheKey = $"equip:{basePath}:{ataquePath}:{prefixoAtaque}:{movimentoFrameSize}:{ataqueFrameSize}:{ataqueFrameCountOverride}:{RowsKey(movimentoRows)}:{RowsKey(ataqueRows)}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new SpriteFrames();
        var (atkRowBase, atkFrameCount, atkSpeed) = MapaAtaque.GetValueOrDefault(prefixoAtaque, (RowSlash, 6, 5f));
        if (ataqueFrameCountOverride > 0)
            atkFrameCount = ataqueFrameCountOverride;
        movimentoRows = NormalizarRows(movimentoRows, RowWalk);
        int[] idleRows = NormalizarRows(null, RowIdle);
        int[] corridaRows = movimentoFrameSize == FrameSize
            ? NormalizarRows(null, RowRun)
            : movimentoRows;
        ataqueRows = NormalizarRows(ataqueRows, atkRowBase);
        movimentoFrameSize = movimentoFrameSize > 0 ? movimentoFrameSize : FrameSize;
        ataqueFrameSize = ataqueFrameSize > 0 ? ataqueFrameSize : FrameSize;

        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            int row = movimentoRows[d];
            AdicionarAnimacao(frames, sheetPadrao, $"walk_{dir}", row, WalkFrameCounts[d], true, 10f, movimentoFrameSize, movimentoFrameSize, WalkStartCols[d]);
            AdicionarAnimacao(frames, sheetPadrao, $"run_{dir}", corridaRows[d], RunFrameCounts[d], true, 12f, movimentoFrameSize, movimentoFrameSize, RunStartCols[d]);
            AdicionarAnimacao(frames, sheetPadrao, $"idle_{dir}", idleRows[d], 2, true, 3f, movimentoFrameSize, movimentoFrameSize);
        }

        var sheetAtaqueFinal = sheetAtaque ?? sheetPadrao;
        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            AdicionarAnimacao(frames, sheetAtaqueFinal, $"{prefixoAtaque}_attack_{dir}", ataqueRows[d], atkFrameCount, false, atkSpeed, ataqueFrameSize, ataqueFrameSize);
        }

        for (int d = 0; d < Direcoes.Length; d++)
            AdicionarAnimacao(frames, sheetPadrao, $"jump_{Direcoes[d]}", movimentoRows[d], 5, false, 10f, movimentoFrameSize, movimentoFrameSize);

        AdicionarAnimacao(frames, sheetPadrao, "death", RowHurt, 6, false, 5f);

        _cache[cacheKey] = frames;
        return frames;
    }

    private static int[] NormalizarRows(int[] rows, int rowBase)
    {
        if (rows != null && rows.Length >= Direcoes.Length)
            return rows;

        return new[] { rowBase, rowBase + 1, rowBase + 2, rowBase + 3 };
    }

    private static string RowsKey(int[] rows)
    {
        return rows == null ? "default" : string.Join("-", rows);
    }

    private static bool EhSpritesheetArco(Texture2D sheet)
    {
        if (sheet == null)
            return false;

        Vector2 size = sheet.GetSize();
        return size.X >= 1152 && size.Y >= 3968 && sheet.ResourcePath.Contains("Arco", StringComparison.OrdinalIgnoreCase);
    }

    private static void AdicionarAnimacao(
        SpriteFrames frames, Texture2D sheet, string nome, int row, int frameCount, bool loop, float speed)
    {
        AdicionarAnimacao(frames, sheet, nome, row, frameCount, loop, speed, FrameSize, FrameSize);
    }

    private static void AdicionarAnimacao(
        SpriteFrames frames, Texture2D sheet, string nome, int row, int frameCount, bool loop, float speed, int frameWidth, int frameHeight, int startCol = 0)
    {
        if (frames.HasAnimation(nome)) return;

        var lista = new List<Texture2D>();
        for (int i = 0; i < frameCount; i++)
        {
            int col = startCol + i;
            var tex = CriarAtlas(sheet, col, row, frameWidth, frameHeight);
            if (tex != null)
                lista.Add(tex);
        }

        if (lista.Count == 0)
            return;

        frames.AddAnimation(nome);
        frames.SetAnimationLoop(nome, loop);
        frames.SetAnimationSpeed(nome, speed);
        foreach (var tex in lista)
            frames.AddFrame(nome, tex, 1.0f);
    }

    private static AtlasTexture CriarAtlas(Texture2D sheet, int col, int row)
    {
        return CriarAtlas(sheet, col, row, FrameSize, FrameSize);
    }

    private static AtlasTexture CriarAtlas(Texture2D sheet, int col, int row, int frameWidth, int frameHeight)
    {
        int x = col * frameWidth;
        int y = row * frameHeight;
        Vector2 size = sheet.GetSize();
        if (x + frameWidth > size.X || y + frameHeight > size.Y)
            return null;

        return new AtlasTexture
        {
            Atlas = sheet,
            Region = new Rect2(x, y, frameWidth, frameHeight)
        };
    }
}
