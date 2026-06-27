#nullable enable
using Godot;
using System.Collections.Generic;

public partial class ItemDatabase : Node
{
    private readonly Dictionary<int, string> _itemMap = new();
    private readonly Dictionary<int, ItemResource?> _cache = new();
    private bool _scanned;

    private static readonly string ItensDir = "res://Itens/";

    public override void _Ready()
    {
        ScanItensFolder();
    }

    public void ScanItensFolder()
    {
        _itemMap.Clear();
        _cache.Clear();

        if (!DirAccess.DirExistsAbsolute(ItensDir))
        {
            GD.PrintErr("[ItemDatabase] DirAccess falhou (build exportada?); usando fallback.");
            ScanFallback();
            _scanned = true;
            GD.Print($"[ItemDatabase] Fallback concluido: {_itemMap.Count} itens mapeados.");
            return;
        }

        ScanDirRecursive(ItensDir);

        _scanned = true;
        GD.Print($"[ItemDatabase] Scan concluido: {_itemMap.Count} itens mapeados.");
    }

    private void ScanFallback()
    {
        foreach (string path in _fallbackPaths)
        {
            var res = GD.Load<ItemResource>(path);
            if (res is ItemResource item && item.ItemID > 0 && !_itemMap.ContainsKey(item.ItemID))
            {
                _itemMap[item.ItemID] = path;
            }
        }
    }

    private void ScanDirRecursive(string dirPath)
    {
        var dir = DirAccess.Open(dirPath);
        if (dir == null) return;

        dir.ListDirBegin();
        string entryName;
        while ((entryName = dir.GetNext()) != "")
        {
            if (entryName == "." || entryName == "..")
                continue;

            string fullPath = dirPath.TrimEnd('/') + "/" + entryName;

            if (dir.CurrentIsDir())
            {
                ScanDirRecursive(fullPath);
                continue;
            }

            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;

            var res = ResourceLoader.Load(fullPath);
            if (res is ItemResource item && item.ItemID > 0 && !_itemMap.ContainsKey(item.ItemID))
            {
                _itemMap[item.ItemID] = fullPath;
            }
        }
        dir.ListDirEnd();
    }

    public void Refresh() => ScanItensFolder();

    public ItemResource? GetItem(int itemId)
    {
        if (!_scanned) ScanItensFolder();

        if (_cache.TryGetValue(itemId, out var cached))
            return cached;

        if (!_itemMap.TryGetValue(itemId, out var path))
            return CriarItemFallback(itemId);

        var resource = GD.Load<ItemResource>(path);
        if (resource == null)
            resource = CriarItemFallback(itemId);

        _cache[itemId] = resource;
        return resource;
    }

    public string GetItemName(int itemId)
    {
        return GetItem(itemId)?.Nome ?? $"Item #{itemId}";
    }

    public bool HasItem(int itemId)
    {
        if (!_scanned) ScanItensFolder();
        return _itemMap.ContainsKey(itemId);
    }

    private ItemResource CriarItemFallback(int itemId)
    {
        string iconPath = itemId switch
        {
            0 => "res://Itens/Incones/Moeda de Gold.png",
            100 => "res://Itens/Incones/Pergaminho de Captura de Pet.png",
            102 => "res://Itens/Incones/Pergaminho de Criação de Guild.png",
            103 => "res://Itens/Incones/Vip 1.png",
            104 => "res://Itens/Incones/Vip 2.png",
            105 => "res://Itens/Incones/vip 3.png",
            107 => "res://Itens/Incones/Poeira Estelar.png",
            108 or 109 or 112 => "res://Itens/Incones/Bau surpresa 1.png",
            110 => "res://Itens/Incones/Porcao de Vida.png",
            111 => "res://Itens/Incones/Porcao de Mana.png",
            113 => "res://Itens/Incones/Pergaminho de Reset.png",
            114 => "res://Itens/Incones/Pergaminho de Captura de Pet.png",
            _ => "res://Itens/Incones/bagitem.png",
        };

        string nome = itemId switch
        {
            0 => "Gold",
            100 => "Pergaminho de Captura de Pet",
            102 => "Pergaminho de Criação de Guild",
            103 => "VIP 7 dias",
            104 => "VIP 15 dias",
            105 => "VIP 30 dias",
            107 => "Poeira Estelar",
            108 => "Lojinha Pequena",
            109 => "Lojinha Média",
            110 => "Poção de Vida",
            111 => "Poção de Mana",
            112 => "Lojinha Grande",
            113 => "Pergaminho de Reset de Talentos",
            114 => "Pergaminho de Captura de Pet",
            _ => $"Item #{itemId}",
        };

        var fallback = new ItemResource
        {
            ItemID = itemId,
            Nome = nome,
            Descricao = "Item recebido do servidor. O recurso visual definitivo ainda não está no cliente.",
            Icone = ResourceLoader.Exists(iconPath)
                ? GD.Load<Texture2D>(iconPath)
                : ResourceLoader.Exists("res://Itens/Incones/bagitem.png")
                    ? GD.Load<Texture2D>("res://Itens/Incones/bagitem.png")
                    : null,
            Acumulavel = true,
            QuantidadeMaximaPorSlot = 99,
        };

        _cache[itemId] = fallback;
        GD.PrintErr($"[ItemDatabase] Fallback criado para itemId={itemId}. Verifique se o .tres desse item foi exportado.");
        return fallback;
    }
}
