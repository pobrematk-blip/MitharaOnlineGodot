using Godot;
using System;
using System.Text.Json;

public class SlotInventario
{
    public ItemResource Item { get; set; }
    public int Quantidade { get; set; }
    public int RefinoNivel { get; set; }
    public string DadosInstancia { get; set; } = "";

    // Construtor 1: Vazio
    public SlotInventario()
    {
        Item = null;
        Quantidade = 0;
        RefinoNivel = 0;
    }

    // Construtor 2: Com parâmetros (Evita o erro da linha 20 do Componente!)
    public SlotInventario(ItemResource item, int quantidade, int refinoNivel = 0, string dadosInstancia = "")
    {
        Item = CriarRecursoDaInstancia(item, dadosInstancia);
        Quantidade = quantidade;
        RefinoNivel = refinoNivel;
        DadosInstancia = dadosInstancia ?? "";
    }

    private static ItemResource CriarRecursoDaInstancia(ItemResource baseItem, string json)
    {
        if (baseItem == null || string.IsNullOrWhiteSpace(json)) return baseItem;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("IsRolled", out var rolled) || !rolled.GetBoolean()) return baseItem;

            var item = (ItemResource)baseItem.Duplicate(true);
            item.Raridade = (Raridade)root.GetProperty("Rarity").GetInt32();
            item.Forca = GetInt(root, "Forca");
            item.Agilidade = GetInt(root, "Agilidade");
            item.Destreza = GetInt(root, "Destreza");
            item.Inteligencia = GetInt(root, "Inteligencia");
            var baseAttack = GetInt(root, "BaseAttack");
            if (UsaDanoMagicoComoAtaqueBase(item))
            {
                item.DanoFisico = 0;
                item.DanoMagico = baseAttack;
            }
            else
            {
                item.DanoFisico = baseAttack;
                item.DanoMagico = 0;
            }
            item.DefesaFisica = GetInt(root, "Defense");
            item.DefesaMagica = GetInt(root, "MagicDefense");
            item.Hp = GetInt(root, "Hp");
            item.Mana = GetInt(root, "Mana");
            item.Evasao = GetFloat(root, "Evasion");

            if (root.TryGetProperty("Affixes", out var affixes))
                foreach (var affix in affixes.EnumerateObject()) AplicarAfixo(item, affix.Name, affix.Value.GetSingle());
            return item;
        }
        catch (JsonException)
        {
            return baseItem;
        }
    }

    private static int GetInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.GetInt32() : 0;
    private static float GetFloat(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.GetSingle() : 0f;

    private static bool UsaDanoMagicoComoAtaqueBase(ItemResource item)
    {
        if (item == null) return false;

        int id = item.ItemID;
        if ((id >= 1066 && id <= 1076)
            || (id >= 11066 && id <= 11076)
            || (id >= 1077 && id <= 1087)
            || (id >= 11077 && id <= 11087)
            || (id >= 5001 && id <= 5021)
            || (id >= 6001 && id <= 6021))
            return true;

        string nome = item.Nome?.ToLowerInvariant() ?? "";
        string classes = item.ClassesPermitidas?.ToLowerInvariant() ?? "";
        bool classeMagica = classes.Contains("mago") || classes.Contains("prist") || classes.Contains("clerigo") || classes.Contains("clérigo");
        return classeMagica && (nome.Contains("cajado") || nome.Contains("martelo") || nome.Contains("maca") || nome.Contains("maça"));
    }

    private static void AplicarAfixo(ItemResource item, string nome, float valor)
    {
        switch (nome.ToLowerInvariant())
        {
            case "forca": item.Forca += (int)MathF.Round(valor); break;
            case "agilidade": item.Agilidade += (int)MathF.Round(valor); break;
            case "destreza": item.Destreza += (int)MathF.Round(valor); break;
            case "inteligencia": item.Inteligencia += (int)MathF.Round(valor); break;
            case "hp": item.Hp += (int)MathF.Round(valor); break;
            case "mana": item.Mana += (int)MathF.Round(valor); break;
            case "defesafisica": item.DefesaFisica += (int)MathF.Round(valor); break;
            case "defesamagica": item.DefesaMagica += (int)MathF.Round(valor); break;
            case "chancecritica": item.ChanceCritica += valor; break;
            case "danocriticobonus": item.DanoCriticoBonus += valor; break;
            case "precisao": item.Precisao += valor; break;
            case "velocidadeataque": item.VelocidadeAtaque += valor; break;
            case "penetracaoarmadura": item.PenetracaoArmadura += (int)MathF.Round(valor); break;
            case "evasao": item.Evasao += valor; break;
            case "velocidademovimento": item.VelocidadeMovimento += valor; break;
            case "roubovida": item.RouboVida += valor; break;
            case "roubomana": item.RouboMana += valor; break;
            case "tenacidade": item.Tenacidade += valor; break;
            case "regeneracaovida": item.RegeneracaoVida += valor; break;
            case "regeneracaomana": item.RegeneracaoMana += valor; break;
            case "reducaocooldown": item.ReducaoCooldown += valor; break;
            case "danomagico": item.DanoMagico += (int)MathF.Round(valor); break;
            case "reflexao" or "reflexaodano": item.ReflexaoDano += (int)MathF.Round(valor); break;
            case "resistenciacontrole": item.ResistenciaControle += valor; break;
        }
    }
}
