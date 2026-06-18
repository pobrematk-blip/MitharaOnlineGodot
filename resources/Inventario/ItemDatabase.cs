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
            GD.PrintErr("[ItemDatabase] Pasta Itens/ nao encontrada.");
            return;
        }

        ScanDirRecursive(ItensDir);

        _scanned = true;
        GD.Print($"[ItemDatabase] Scan concluido: {_itemMap.Count} itens mapeados.");
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
            return null;

        var resource = GD.Load<ItemResource>(path);
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
}
