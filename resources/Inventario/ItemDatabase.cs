#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.IO;

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
        ScanFallback();

        if (!DirAccess.DirExistsAbsolute(ItensDir))
        {
            GD.PrintErr("[ItemDatabase] DirAccess falhou (build exportada?); usando fallback.");
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
            if (TryGetItemIdFromPath(path, out int itemId) && !_itemMap.ContainsKey(itemId))
                _itemMap[itemId] = path;
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

            if (TryGetItemIdFromPath(fullPath, out int parsedId))
            {
                _itemMap.TryAdd(parsedId, fullPath);
                continue;
            }

            var res = ResourceLoader.Load(fullPath);
            if (res is ItemResource item && item.ItemID > 0)
            {
                _itemMap.TryAdd(item.ItemID, fullPath);
            }
        }
        dir.ListDirEnd();
    }

    public void Refresh() => ScanItensFolder();

    public ItemResource? GetItem(int itemId)
    {
        if (itemId < 0)
            return null;

        if (!_scanned) ScanItensFolder();

        if (_cache.TryGetValue(itemId, out var cached))
            return cached;

        if (!_itemMap.TryGetValue(itemId, out var path))
            return CriarItemFallback(itemId);

        var resource = GD.Load<ItemResource>(path);
        if (resource == null)
            resource = CriarItemFallback(itemId, path);

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

    private static bool TryGetItemIdFromPath(string path, out int itemId)
    {
        itemId = 0;
        string file = Path.GetFileName(path);
        int dash = file.IndexOf('-');
        if (dash <= 0) return false;
        return int.TryParse(file[..dash], out itemId) && itemId > 0;
    }

    private static string NomeDoArquivo(string? path, int itemId)
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"Item #{itemId}";

        string file = Path.GetFileNameWithoutExtension(path);
        int firstDash = file.IndexOf('-');
        int lastDash = file.LastIndexOf('-');
        if (firstDash >= 0 && lastDash > firstDash)
            return file[(firstDash + 1)..lastDash].Trim();
        return firstDash >= 0 ? file[(firstDash + 1)..].Trim() : file.Trim();
    }

    private static string IconeEquipamentoFallback(int itemId)
    {
        if (itemId is >= 1001 and <= 1021)
            return "res://Itens/Incones/Arco 1.png";
        if (itemId is >= 1051 and <= 1071)
            return "res://Itens/Incones/Alvaja de Arqueiro Goblin.png";
        if (itemId is >= 2001 and <= 2021)
            return "res://Itens/Incones/Adaga 1.png";
        if (itemId is >= 2051 and <= 2071)
            return "res://Itens/Incones/Adagas de Pirata 1.png";
        if (itemId is >= 3001 and <= 3021)
            return "res://Itens/Incones/Machado 1.png";
        if (itemId is >= 3051 and <= 3071)
            return "res://Itens/Incones/Machados Perdisos 1.png";
        if (itemId is >= 4001 and <= 4021)
            return "res://Itens/Incones/Espada 1.png";
        if (itemId is >= 4051 and <= 4071)
            return "res://Itens/Incones/Escudo de Goglin.png";
        if (itemId is >= 5001 and <= 5021)
            return "res://Itens/Incones/Cajado 1.png";
        if (itemId is >= 5051 and <= 5071)
            return "res://Itens/Incones/Escudo Magico 1.png";
        if (itemId is >= 6001 and <= 6021)
            return "res://Itens/Incones/Martelo quebrada.png";
        if (itemId is >= 6051 and <= 6071)
            return "res://Itens/Incones/Escudo de Goglin.png";

        if (itemId >= 300000 && itemId < 301000)
        {
            int slot = (itemId - 300000) % 12;
            return slot switch
            {
                0 or 1 => "res://Itens/Incones/Capacete Perdido.png",
                2 or 3 => "res://Itens/Incones/Peitoral de Monstro 1.png",
                4 or 5 => "res://Itens/Incones/Calca de montros 1.png",
                6 or 7 => "res://Itens/Incones/Luva de couro.png",
                8 or 9 => "res://Itens/Incones/Botas de Montros.png",
                10 or 11 => "res://Itens/Incones/Cinto de Troll.png",
                _ => "res://Itens/Incones/Bag 3.png",
            };
        }

        return "res://Itens/Incones/Bag 3.png";
    }

    private ItemResource CriarItemFallback(int itemId, string? knownPath = null)
    {
        string iconPath = itemId switch
        {
            0 => "res://Itens/Incones/Moeda de Gold.png",
            100 => "res://Itens/Incones/Pergaminho de Captura de Pet.png",
            102 => "res://Itens/Incones/Pergaminho de Criação de Guild.png",
            103 => "res://Itens/Incones/Vip 1.png",
            104 => "res://Itens/Incones/Vip 2.png",
            105 => "res://Itens/Incones/vip 3.png",
            107 => "res://Itens/Incones/Fragmento Estelar.png",
            108 or 109 or 112 => "res://Itens/Incones/Bau surpresa 1.png",
            110 => "res://Itens/Incones/Porcao de Vida.png",
            111 => "res://Itens/Incones/Porcao de Mana.png",
            113 => "res://Itens/Incones/Pergaminho de Criação de Guild.png",
            114 => "res://Itens/Incones/Pergaminho de Captura de Pet.png",
            115 or 116 or 117 or 118 => "res://Itens/Incones/Bag 3.png",
            121 => "res://Itens/Incones/Pergaminho de Captura de Pet.png",
            _ => IconeEquipamentoFallback(itemId),
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
            113 => "Pergaminho de Reset de Personagem",
            114 => "Pergaminho de Captura de Pet",
            115 => "Bolsa de 6 Slots",
            116 => "Bolsa de 12 Slots",
            117 => "Bolsa de 18 Slots",
            118 => "Bolsa de 24 Slots",
            121 => "Coleira de Pet (30 Dias)",
            _ => NomeEquipamentoFallback(itemId, knownPath),
        };

        var fallback = new ItemResource
        {
            ItemID = itemId,
            Nome = nome,
            Descricao = "Item recebido do servidor. O recurso visual definitivo ainda não está no cliente.",
            Icone = ResourceLoader.Exists(iconPath)
                ? GD.Load<Texture2D>(iconPath)
                : ResourceLoader.Exists("res://Itens/Incones/Bag 3.png")
                    ? GD.Load<Texture2D>("res://Itens/Incones/Bag 3.png")
                    : null,
            Acumulavel = true,
            QuantidadeMaximaPorSlot = 99,
        };

        AplicarMetadadosEquipamentoFallback(fallback, itemId, knownPath);

        if (itemId is 115 or 116 or 117 or 118)
        {
            fallback.Acumulavel = false;
            fallback.QuantidadeMaximaPorSlot = 1;
            fallback.EhBolsa = true;
            fallback.SlotsAdicionais = itemId switch
            {
                115 => 6,
                116 => 12,
                117 => 18,
                118 => 24,
                _ => 0,
            };
        }

        _cache[itemId] = fallback;
        GD.PrintErr($"[ItemDatabase] Fallback criado para itemId={itemId}. Verifique se o .tres desse item foi exportado.");
        return fallback;
    }

    private static void AplicarMetadadosEquipamentoFallback(ItemResource item, int itemId, string? knownPath)
    {
        item.NivelRequerido = InferirNivelEquipamentoFallback(itemId, knownPath);

        if (itemId >= 300000 && itemId < 301000)
        {
            item.Acumulavel = false;
            item.QuantidadeMaximaPorSlot = 1;
            item.Tipo = InferirTipoArmaduraFallback(itemId, knownPath);
            item.CategoriaPeso = InferirPesoArmaduraFallback(knownPath);
            return;
        }

        item.Tipo = itemId switch
        {
            >= 1001 and <= 1021 => TipoEquipamento.Arma,
            >= 1051 and <= 1071 => TipoEquipamento.Escudo,
            >= 2001 and <= 2021 => TipoEquipamento.Arma,
            >= 2051 and <= 2071 => TipoEquipamento.Escudo,
            >= 3001 and <= 3021 => TipoEquipamento.Arma,
            >= 3051 and <= 3071 => TipoEquipamento.Escudo,
            >= 4001 and <= 4021 => TipoEquipamento.Arma,
            >= 4051 and <= 4071 => TipoEquipamento.Escudo,
            >= 5001 and <= 5021 => TipoEquipamento.Arma,
            >= 5051 and <= 5071 => TipoEquipamento.Escudo,
            >= 6001 and <= 6021 => TipoEquipamento.Arma,
            >= 6051 and <= 6071 => TipoEquipamento.Escudo,
            _ => item.Tipo,
        };

        if (item.Tipo != TipoEquipamento.Nenhum)
        {
            item.Acumulavel = false;
            item.QuantidadeMaximaPorSlot = 1;
        }
    }

    private static TipoEquipamento InferirTipoArmaduraFallback(int itemId, string? knownPath)
    {
        string path = (knownPath ?? "").Replace('\\', '/').ToLowerInvariant();
        if (path.Contains("/capacetes/")) return TipoEquipamento.Capacete;
        if (path.Contains("/peitorais/")) return TipoEquipamento.Peitoral;
        if (path.Contains("/cintos/")) return TipoEquipamento.Cinto;
        if (path.Contains("/luvas/")) return TipoEquipamento.Luvas;
        if (path.Contains("/calcas/")) return TipoEquipamento.Calca;
        if (path.Contains("/botas/")) return TipoEquipamento.Botas;

        int slot = Math.Abs(itemId - 300000) % 12;
        return slot switch
        {
            0 or 1 => TipoEquipamento.Capacete,
            2 or 3 => TipoEquipamento.Peitoral,
            4 or 5 => TipoEquipamento.Calca,
            6 or 7 => TipoEquipamento.Luvas,
            8 or 9 => TipoEquipamento.Botas,
            10 or 11 => TipoEquipamento.Cinto,
            _ => TipoEquipamento.Nenhum,
        };
    }

    private static PesoItem InferirPesoArmaduraFallback(string? knownPath)
    {
        string path = (knownPath ?? "").Replace('\\', '/').ToLowerInvariant();
        if (path.Contains("/leves/")) return PesoItem.Leve;
        if (path.Contains("/medias/")) return PesoItem.Medio;
        if (path.Contains("/pesadas/")) return PesoItem.Pesado;
        return PesoItem.Medio;
    }

    private static int InferirNivelEquipamentoFallback(int itemId, string? knownPath)
    {
        string path = knownPath ?? "";
        var match = System.Text.RegularExpressions.Regex.Match(path, @"(?:^|[^0-9])Lv(?:el)?\s*(\d+)(?:[^0-9]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int nivelPath))
            return Math.Max(1, nivelPath);

        if (itemId >= 300000 && itemId < 301000)
        {
            int tier = ((itemId - 300000) / 12) % 11;
            return tier == 0 ? 1 : tier * 10;
        }

        if (itemId is >= 1001 and <= 6071)
        {
            int index = Math.Abs(itemId % 50);
            if (index <= 1) return 1;
            return Math.Max(1, (index - 1) * 5);
        }

        return 1;
    }

    private static string NomeEquipamentoFallback(int itemId, string? knownPath)
    {
        return itemId switch
        {
            >= 1001 and <= 1021 => "Arco",
            >= 1051 and <= 1071 => "Aljava",
            >= 2001 and <= 2021 => "Adaga",
            >= 2051 and <= 2071 => "Adaga Secundaria",
            >= 3001 and <= 3021 => "Machado",
            >= 3051 and <= 3071 => "Bumerangue",
            >= 4001 and <= 4021 => "Espada",
            >= 4051 and <= 4071 => "Escudo",
            >= 5001 and <= 5021 => "Cajado",
            >= 5051 and <= 5071 => "Orbe",
            >= 6001 and <= 6021 => "Martelo",
            >= 6051 and <= 6071 => "Escudo Sagrado",
            >= 300000 and < 301000 => "Equipamento Normal",
            _ => NomeDoArquivo(knownPath, itemId),
        };
    }
}
