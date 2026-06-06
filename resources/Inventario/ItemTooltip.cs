using Godot;

public partial class ItemTooltip : PanelContainer
{
    private RichTextLabel _content;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f),
            BorderColor = new Color(0.3f, 0.3f, 0.4f, 1),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
        });

        _content = new RichTextLabel();
        _content.FitContent = true;
        _content.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _content.BbcodeEnabled = true;
        _content.AddThemeFontSizeOverride("font_size", 11);
        _content.AddThemeColorOverride("default_color", new Color(0.9f, 0.9f, 1));
        _content.CustomMinimumSize = new Vector2(180, 0);
        _content.Size = new Vector2(180, 0);
        AddChild(_content);

        ProcessPriority = int.MaxValue;
    }

    public void Mostrar(ItemResource item, Vector2 posicaoGlobal)
    {
        if (item == null) { Esconder(); return; }

        _content.Text = MontarDescricao(item);
        Visible = true;

        var window = GetWindow();
        if (window == null) return;

        float w = _content.Size.X + 16;
        float h = _content.Size.Y + 16;
        float mx = posicaoGlobal.X + 16;
        float my = posicaoGlobal.Y;

        if (mx + w > window.Size.X)
            mx = posicaoGlobal.X - w - 16;
        if (my + h > window.Size.Y)
            my = window.Size.Y - h - 8;
        if (mx < 0) mx = 0;
        if (my < 0) my = 0;

        Position = new Vector2(mx, my);
    }

    public void Esconder()
    {
        Visible = false;
    }

    private static string MontarDescricao(ItemResource item)
    {
        var lines = new System.Collections.Generic.List<string>();

        // Rarity color
        string corRaridade = item.Raridade switch
        {
            Raridade.Comum => "#ffffff",
            Raridade.Incomum => "#1eff00",
            Raridade.Raro => "#0070dd",
            Raridade.Epico => "#a335ee",
            Raridade.Lendario => "#ffcc00",
            Raridade.Mistico => "#ff4444",
            _ => "#ffffff",
        };

        string eliteTag = item.TipoItem == TipoItem.Elite ? " [color=#ff4444](Elite)[/color]" : "";
        lines.Add($"[color={corRaridade}][b]{item.Nome}[/b]{eliteTag}[/color]");

        string raridadeNome = item.Raridade switch
        {
            Raridade.Comum => "Comum",
            Raridade.Incomum => "Incomum",
            Raridade.Raro => "Raro",
            Raridade.Epico => "Épico",
            Raridade.Lendario => "Lendário",
            Raridade.Mistico => "Místico",
            _ => "",
        };
        lines.Add($"[color={corRaridade}]{raridadeNome}[/color]");

        if (item.Tipo != TipoEquipamento.Nenhum)
            lines.Add($"[color=#888888]{TraduzirTipo(item.Tipo)}[/color]");

        if (item.EhDuasMaos)
            lines.Add("[color=#cc8844]Duas Mãos[/color]");

        if (item.NivelRequerido > 1)
            lines.Add($"Nível requerido: [color=#ffcc00]{item.NivelRequerido}[/color]");

        if (!string.IsNullOrEmpty(item.Descricao))
            lines.Add($"\n[color=#aaaaaa]{item.Descricao}[/color]");

        bool temAtributo = false;
        if (item.Forca != 0) { lines.Add($"Força {item.Forca:+0;-0}"); temAtributo = true; }
        if (item.Agilidade != 0) { lines.Add($"Agilidade {item.Agilidade:+0;-0}"); temAtributo = true; }
        if (item.Destreza != 0) { lines.Add($"Destreza {item.Destreza:+0;-0}"); temAtributo = true; }
        if (item.Inteligencia != 0) { lines.Add($"Inteligência {item.Inteligencia:+0;-0}"); temAtributo = true; }

        bool temCombate = false;
        if (item.DanoFisico > 0) { if (temAtributo || temCombate) lines.Add(""); lines.Add($"Dano Físico: [color=#ff6666]{item.DanoFisico}[/color]"); temCombate = true; }
        if (item.DanoMagico > 0) { if (temAtributo || temCombate) lines.Add(""); lines.Add($"Dano Mágico: [color=#6666ff]{item.DanoMagico}[/color]"); temCombate = true; }
        if (item.DefesaFisica > 0) { if (temAtributo || temCombate) lines.Add(""); lines.Add($"Defesa Física: [color=#66ff66]{item.DefesaFisica}[/color]"); temCombate = true; }
        if (item.DefesaMagica > 0) { if (temAtributo || temCombate) lines.Add(""); lines.Add($"Defesa Mágica: [color=#66ff66]{item.DefesaMagica}[/color]"); temCombate = true; }

        // Bonus stats
        bool temBonus = false;
        if (item.ChanceCritica > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Chance Crítica: [color=#ff6666]{item.ChanceCritica:F1}%[/color]"); temBonus = true; }
        if (item.Evasao > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Evasão: [color=#66ff66]{item.Evasao:F1}%[/color]"); temBonus = true; }
        if (item.DanoCriticoBonus > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Dano Crítico: [color=#ff8000]+{item.DanoCriticoBonus:F2}x[/color]"); temBonus = true; }
        if (item.RouboVida > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Roubo de Vida: [color=#ff4444]{item.RouboVida:F1}%[/color]"); temBonus = true; }
        if (item.RouboMana > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Roubo de Mana: [color=#6666ff]{item.RouboMana:F1}%[/color]"); temBonus = true; }
        if (item.RegeneracaoVida > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Regen. Vida: [color=#66ff66]{item.RegeneracaoVida:F1}/s[/color]"); temBonus = true; }
        if (item.RegeneracaoMana > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Regen. Mana: [color=#6666ff]{item.RegeneracaoMana:F1}/s[/color]"); temBonus = true; }
        if (item.Hp > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Vida: [color=#66ff66]+{item.Hp}[/color]"); temBonus = true; }
        if (item.Mana > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Mana: [color=#6666ff]+{item.Mana}[/color]"); temBonus = true; }
        if (item.Stamina > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Stamina: [color=#66ff66]+{item.Stamina}[/color]"); temBonus = true; }
        if (item.VelocidadeMovimento > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Vel. Movimento: [color=#66ff66]+{item.VelocidadeMovimento:F2}x[/color]"); temBonus = true; }
        if (item.VelocidadeAtaque > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Vel. Ataque: [color=#ff6666]+{item.VelocidadeAtaque:F2}x[/color]"); temBonus = true; }
        if (item.Precisao > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Precisão: [color=#66ff66]+{item.Precisao}[/color]"); temBonus = true; }
        if (item.Tenacidade > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Tenacidade: [color=#66ff66]+{item.Tenacidade}[/color]"); temBonus = true; }
        if (item.DanoPvp > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Dano PvP: [color=#ff6666]+{item.DanoPvp}[/color]"); temBonus = true; }
        if (item.DefesaPvp > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Defesa PvP: [color=#66ff66]+{item.DefesaPvp}[/color]"); temBonus = true; }
        if (item.PenetracaoArmadura > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Pen. Armadura: [color=#ff6666]+{item.PenetracaoArmadura}[/color]"); temBonus = true; }
        if (item.ReducaoCooldown > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Red. Cooldown: [color=#66ff66]{item.ReducaoCooldown:F1}%[/color]"); temBonus = true; }
        if (item.BonusExperiencia > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Bônus XP: [color=#ffdd66]{item.BonusExperiencia:F1}%[/color]"); temBonus = true; }
        if (item.ChanceDropAumentada > 0) { if (temAtributo || temCombate || temBonus) lines.Add(""); lines.Add($"Chance Drop: [color=#ffdd66]{item.ChanceDropAumentada:F1}%[/color]"); temBonus = true; }

        if (!string.IsNullOrEmpty(item.AnimacaoUsar))
            lines.Add($"\n[color=#ffaa33][i]Anim. Uso: {item.AnimacaoUsar}[/i][/color]");

        if (!string.IsNullOrEmpty(item.AnimacaoEquipado))
            lines.Add($"[color=#ffaa33][i]Anim. Equip: {item.AnimacaoEquipado}[/i][/color]");

        if (item.EhBolsa)
            lines.Add($"\n[color=#ffaa33]Bolsa: +{item.SlotsAdicionais} slots[/color]");

        if (item.Acumulavel && item.QuantidadeMaximaPorSlot > 1)
            lines.Add($"\nMáx por slot: {item.QuantidadeMaximaPorSlot}");

        if (item.Valor > 0)
            lines.Add($"\n[color=#ffdd66]Valor: {item.Valor} moedas[/color]");

        if (item.TempoCooldown > 0)
            lines.Add($"\n[color=#88ccff]Cooldown: {item.TempoCooldown}s[/color]");

        lines.Add($"\n[color=#888888]Drop: {(item.PodeDropar ? "Sim" : "Não")}  |  Troca: {(item.PodeTrocar ? "Sim" : "Não")}  |  Venda: {(item.PodeVender ? "Sim" : "Não")}[/color]");
        if (item.PodeDropar)
            lines.Add($"[color=#888888]Desaparece em: {item.TempoDesaparecimento}s[/color]");
        if (item.DropEmPk)
            lines.Add($"[color=#ff6666]Drop em PK: {item.ChanceDrop}%[/color]");

        return string.Join("\n", lines);
    }

    private static string TraduzirTipo(TipoEquipamento tipo)
    {
        return tipo switch
        {
            TipoEquipamento.Capacete => "Capacete",
            TipoEquipamento.Peitoral => "Peitoral",
            TipoEquipamento.Cinto => "Cinto",
            TipoEquipamento.Luvas => "Luvas",
            TipoEquipamento.Calca => "Calças",
            TipoEquipamento.Botas => "Botas",
            TipoEquipamento.Arma => "Arma",
            TipoEquipamento.Escudo => "Escudo",
            TipoEquipamento.Colar => "Colar",
            TipoEquipamento.Anel => "Anel",
            TipoEquipamento.Brinco => "Brinco",
            TipoEquipamento.Runa => "Runa",
            TipoEquipamento.Asa => "Asa",
            TipoEquipamento.Montaria => "Montaria",
            TipoEquipamento.Pet => "Pet",
            TipoEquipamento.Skin => "Skin",
            TipoEquipamento.Consumivel => "Consumível",
            TipoEquipamento.Moeda => "Moeda",
            TipoEquipamento.Feitico => "Feitiço",
            _ => "",
        };
    }
}
