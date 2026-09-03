#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;
using System.Collections.Generic;

partial class GameNetwork
{
    [Signal] public delegate void OnProfessionInfoEventHandler(int alqLevel, long alqXp, int alqXpForNext, Godot.Collections.Array knownRecipes);
    [Signal] public delegate void OnLearnRecipeResultEventHandler(bool success, int recipeItemId, string message);
    [Signal] public delegate void OnCraftResultEventHandler(bool success, int recipeId, int producedItemId, string message);
    [Signal] public delegate void OnProfessionLevelUpEventHandler(byte professionType, byte newLevel);
    [Signal] public delegate void OnStationDataEventHandler(string profId, string profNome, int level, long xp, int xpForNext, float critChancePct, Godot.Collections.Array receitas, Godot.Collections.Array estoque);
    [Signal] public delegate void OnBuyRecipeResultEventHandler(bool success, int recipeId, int goldCost, string message);

    public void SendOpenStation(string profId)
    {
        _client?.SendPacket(PacketId.C2S_OpenStation, w =>
        {
            w.Put(profId);
        });
    }

    public void SendStationCraft(string profId, List<(int itemId, int quantity)> materiais)
    {
        _client?.SendPacket(PacketId.C2S_StationCraft, w =>
        {
            w.Put(profId);
            w.Put(materiais.Count);
            foreach (var (itemId, quantity) in materiais)
            {
                w.Put(itemId);
                w.Put(quantity);
            }
        });
    }

    public void SendLearnRecipe(int slot)
    {
        _client?.SendPacket(PacketId.C2S_LearnRecipe, w =>
        {
            w.Put(slot);
        });
    }

    public void SendCraftAlchemist(int recipeId)
    {
        _client?.SendPacket(PacketId.C2S_CraftAlchemist, w =>
        {
            w.Put(recipeId);
        });
    }

    public void SendBuyRecipe(int recipeId, string profId)
    {
        _client?.SendPacket(PacketId.C2S_BuyRecipe, w =>
        {
            w.Put(recipeId);
            w.Put(profId);
        });
    }

    public void SendProfessionInfo()
    {
        _client?.SendPacket(PacketId.C2S_ProfessionInfo, w => { });
    }

    private void HandleProfessionInfo(NetDataReader r)
    {
        bool hasAlq = r.GetBool();
        int alqLevel = 0;
        long alqXp = 0;
        int alqXpForNext = 0;
        if (hasAlq)
        {
            alqLevel = r.GetByte();
            alqXp = r.GetLong();
            alqXpForNext = r.GetInt();
        }

        int count = r.GetInt();
        var recipes = new Godot.Collections.Array();
        for (int i = 0; i < count; i++)
            recipes.Add(r.GetInt());

        GD.Print($"[GAME] Profissão recebida: Alquimista NV.{alqLevel} ({recipes.Count} receitas)");

        if (GetTree()?.CurrentScene != null)
        {
            var ui = GetTree().CurrentScene.FindChild("AlchemistUI", true, false) as AlchemistUI;
            if (ui == null)
            {
                var scene = ResourceLoader.Load<PackedScene>("res://ui/AlchemistUI.tscn");
                if (scene != null)
                {
                    ui = scene.Instantiate<AlchemistUI>();
                    GetTree().CurrentScene.AddChild(ui);
                }
            }
            ui?.Abrir();
        }

        EmitSignal(SignalName.OnProfessionInfo, alqLevel, alqXp, alqXpForNext, recipes);
    }

    private void HandleLearnRecipeResult(NetDataReader r)
    {
        bool success = r.GetBool();
        int recipeItemId = r.GetInt();
        string message = r.GetString();
        GD.Print($"[GAME] Aprender receita: success={success} msg={message}");
        EmitSignal(SignalName.OnLearnRecipeResult, success, recipeItemId, message);
    }

    private void HandleCraftResult(NetDataReader r)
    {
        bool success = r.GetBool();
        int recipeId = r.GetInt();
        int producedItemId = r.GetInt();
        string message = r.GetString();
        GD.Print($"[GAME] Craft resultado: success={success} msg={message}");
        EmitSignal(SignalName.OnCraftResult, success, recipeId, producedItemId, message);
    }

    private void HandleBuyRecipeResult(NetDataReader r)
    {
        bool success = r.GetBool();
        int recipeId = r.GetInt();
        int goldCost = r.GetInt();
        string message = r.GetString();
        GD.Print($"[GAME] Comprar receita: success={success} msg={message}");
        EmitSignal(SignalName.OnBuyRecipeResult, success, recipeId, goldCost, message);
    }

    private void HandleProfessionLevelUp(NetDataReader r)
    {
        byte professionType = r.GetByte();
        byte newLevel = r.GetByte();
        GD.Print($"[GAME] Profissão level up! type={professionType} level={newLevel}");
        EmitSignal(SignalName.OnProfessionLevelUp, professionType, newLevel);
    }

    private void HandleStationData(NetDataReader r)
    {
        string profId = r.GetString();
        string profNome = r.GetString();
        int level = r.GetByte();
        long xp = r.GetLong();
        int xpForNext = r.GetInt();
        float critChancePct = r.GetFloat();

        var receitas = new Godot.Collections.Array();
        int count = r.GetInt();
        for (int i = 0; i < count; i++)
        {
            var rec = new Godot.Collections.Dictionary
            {
                ["id"] = r.GetInt(),
                ["nome"] = r.GetString(),
                ["producedItemId"] = r.GetInt(),
                ["producedNome"] = r.GetString(),
                ["producedQuantity"] = (int)r.GetShort(),
                ["requiredLevel"] = r.GetInt(),
                ["xpReward"] = r.GetInt(),
                ["goldCost"] = r.GetInt(),
                ["successRate"] = r.GetInt(),
                ["critBonus"] = (int)r.GetShort(),
                ["known"] = r.GetBool(),
            };

            int ingCount = r.GetShort();
            var ings = new Godot.Collections.Array();
            for (int j = 0; j < ingCount; j++)
            {
                ings.Add(new Godot.Collections.Dictionary
                {
                    ["itemId"] = r.GetInt(),
                    ["quantity"] = (int)r.GetShort(),
                    ["nome"] = r.GetString(),
                });
            }
            rec["ingredientes"] = ings;
            receitas.Add(rec);
        }

        var estoque = new Godot.Collections.Array();
        int estCount = r.GetInt();
        for (int i = 0; i < estCount; i++)
        {
            estoque.Add(new Godot.Collections.Dictionary
            {
                ["itemId"] = r.GetInt(),
                ["quantidade"] = r.GetLong(),
                ["nome"] = r.GetString(),
            });
        }

        GD.Print($"[GAME] Mesa recebida: {profNome} NV.{level} ({receitas.Count} receitas)");

        if (GetTree()?.CurrentScene != null)
        {
            var ui = GetTree().CurrentScene.FindChild("StationUI", true, false) as StationUI;
            if (ui == null)
            {
                var scene = ResourceLoader.Load<PackedScene>("res://ui/StationUI.tscn");
                if (scene != null)
                {
                    ui = scene.Instantiate<StationUI>();
                    // CanvasLayer propria acima de tudo: sem isso a janela entra no
                    // canvas do mundo (presa no mapa e abaixo dos tilemaps z=5).
                    var layer = GetTree().CurrentScene.FindChild("StationUILayer", false, false) as CanvasLayer;
                    if (layer == null)
                    {
                        layer = new CanvasLayer { Name = "StationUILayer", Layer = 100 };
                        GetTree().CurrentScene.AddChild(layer);
                    }
                    layer.AddChild(ui);
                }
            }
            ui?.AbrirComDados(profId, profNome, level, xp, xpForNext, critChancePct, receitas, estoque);
        }

        EmitSignal(SignalName.OnStationData, profId, profNome, level, xp, xpForNext, critChancePct, receitas, estoque);
    }
}
