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
            AdicionarAnimacao(frames, sheet, $"walk_{dir}", walkRow, 9, true, 10f);
            AdicionarAnimacao(frames, sheet, $"run_{dir}", RowRun + d, 8, true, 12f);
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
        var sheetPadrao = sheetBase ?? sheetAtaque;
        if (sheetPadrao == null) return new SpriteFrames();

        prefixoAtaque = string.IsNullOrWhiteSpace(prefixoAtaque) ? "mago" : prefixoAtaque.Trim().ToLowerInvariant();
        string ataquePath = sheetAtaque?.ResourcePath ?? "";
        string basePath = sheetBase?.ResourcePath ?? "";
        string cacheKey = $"equip:{basePath}:{ataquePath}:{prefixoAtaque}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new SpriteFrames();
        var (atkRowBase, atkFrameCount, atkSpeed) = MapaAtaque.GetValueOrDefault(prefixoAtaque, (RowSlash, 6, 5f));
        bool usarMapaArco = EhSpritesheetArco(sheetPadrao) || EhSpritesheetArco(sheetAtaque);
        int[] movimentoRows = usarMapaArco ? new[] { 54, 55, 57, 61 } : new[] { RowWalk, RowWalk + 1, RowWalk + 2, RowWalk + 3 };
        int[] ataqueRows = usarMapaArco ? new[] { 16, 17, 18, 19 } : new[] { atkRowBase, atkRowBase + 1, atkRowBase + 2, atkRowBase + 3 };

        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            int row = movimentoRows[d];
            AdicionarAnimacao(frames, sheetPadrao, $"walk_{dir}", row, 9, true, 10f);
            AdicionarAnimacao(frames, sheetPadrao, $"run_{dir}", row, 8, true, 12f);
            AdicionarAnimacao(frames, sheetPadrao, $"idle_{dir}", row, 2, true, 3f);
        }

        var sheetAtaqueFinal = sheetAtaque ?? sheetPadrao;
        for (int d = 0; d < Direcoes.Length; d++)
        {
            string dir = Direcoes[d];
            AdicionarAnimacao(frames, sheetAtaqueFinal, $"{prefixoAtaque}_attack_{dir}", ataqueRows[d], atkFrameCount, false, atkSpeed);
        }

        for (int d = 0; d < Direcoes.Length; d++)
            AdicionarAnimacao(frames, sheetPadrao, $"jump_{Direcoes[d]}", movimentoRows[d], 5, false, 10f);

        AdicionarAnimacao(frames, sheetPadrao, "death", RowHurt, 6, false, 5f);

        _cache[cacheKey] = frames;
        return frames;
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
        if (frames.HasAnimation(nome)) return;

        var lista = new List<Texture2D>();
        for (int col = 0; col < frameCount; col++)
        {
            var tex = CriarAtlas(sheet, col, row);
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
        int x = col * FrameSize;
        int y = row * FrameSize;
        Vector2 size = sheet.GetSize();
        if (x + FrameSize > size.X || y + FrameSize > size.Y)
            return null;

        return new AtlasTexture
        {
            Atlas = sheet,
            Region = new Rect2(x, y, FrameSize, FrameSize)
        };
    }
}
