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

    public static SpriteFrames Construir(Texture2D sheet, string prefixoAtaque)
    {
        var frames = new SpriteFrames();
        if (sheet == null) return frames;

        prefixoAtaque = string.IsNullOrWhiteSpace(prefixoAtaque) ? "mago" : prefixoAtaque.Trim().ToLowerInvariant();
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

        return frames;
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
