using Godot;
using System;

public static class ItemGerador
{
    private static readonly Random _rng = new();

    public static void GerarStatsAleatorios(ItemResource item, Raridade raridade)
    {
        if (item == null) return;

        int rarityIndex = (int)raridade;
        var (mainMin, mainMax, bonusChance) = rarityIndex switch
        {
            0 => (1, 3, 0.25f),
            1 => (3, 6, 0.40f),
            2 => (6, 9, 0.55f),
            3 => (9, 12, 0.70f),
            4 => (12, 15, 0.85f),
            5 => (15, 18, 0.95f),
            _ => (1, 3, 0.25f),
        };

        item.Raridade = raridade;

        PesoItem peso = item.CategoriaPeso;
        int hpBase, defFisicaBase, defMagicaBase;
        if (peso == PesoItem.Pesado)
        {
            hpBase = 25 + rarityIndex * 15;
            defFisicaBase = 12 + rarityIndex * 8;
            defMagicaBase = 8 + rarityIndex * 6;
        }
        else if (peso == PesoItem.Leve)
        {
            hpBase = 10 + rarityIndex * 8;
            defFisicaBase = 6 + rarityIndex * 5;
            defMagicaBase = 18 + rarityIndex * 12;
        }
        else
        {
            hpBase = 15 + rarityIndex * 10;
            defFisicaBase = 9 + rarityIndex * 7;
            defMagicaBase = 9 + rarityIndex * 7;
        }

        int hpMin = hpBase, hpMax = hpBase + 5 + rarityIndex * 2;
        item.Hp = _rng.Next(hpMin, hpMax + 1);

        int defFisicaMin = defFisicaBase, defFisicaMax = defFisicaBase + 2 + rarityIndex;
        item.DefesaFisica = _rng.Next(defFisicaMin, defFisicaMax + 1);

        int defMagicaMin = defMagicaBase, defMagicaMax = defMagicaBase + 2 + rarityIndex;
        item.DefesaMagica = _rng.Next(defMagicaMin, defMagicaMax + 1);

        bool isArmor = item.Tipo switch
        {
            TipoEquipamento.Capacete or TipoEquipamento.Peitoral or TipoEquipamento.Cinto
                or TipoEquipamento.Calca or TipoEquipamento.Botas or TipoEquipamento.Luvas => true,
            _ => false
        };

        bool isBootGloveHelm = item.Tipo is TipoEquipamento.Capacete or TipoEquipamento.Botas or TipoEquipamento.Luvas;

        string classes = item.ClassesPermitidas ?? "";
        bool isArcherAssassin = classes.Contains("Arqueiro") || classes.Contains("Ladino");

        void RollInt(Action<int> setter, int minVal, int maxVal, float mult = 1.0f)
        {
            if (_rng.NextDouble() < bonusChance * mult)
                setter(_rng.Next(minVal, maxVal + 1));
        }

        void RollPct(Action<float> setter, float minVal, float maxVal, float mult = 1.0f)
        {
            if (_rng.NextDouble() < bonusChance * mult)
                setter((float)Math.Round(_rng.NextDouble() * (maxVal - minVal) + minVal, 2));
        }

        if (isArmor)
        {
            RollInt(v => item.Forca = v, mainMin, mainMax);
            RollInt(v => item.Destreza = v, mainMin, mainMax);
            RollInt(v => item.Agilidade = v, mainMin, mainMax);
            RollInt(v => item.DanoFisico = v, mainMin, mainMax);
            RollInt(v => item.DanoCriticoBonus = v, mainMin, mainMax);

            RollPct(v => item.Precisao = v, 0.1f, 0.8f);
            RollPct(v => item.ChanceCritica = v, 0.1f, 0.8f);

            if (isBootGloveHelm)
                RollPct(v => item.VelocidadeAtaque = v, 0.1f, 0.8f);

            if (isArcherAssassin || peso == PesoItem.Medio)
                RollPct(v => item.Evasao = v, 0.1f, 0.8f);

            RollPct(v => item.Tenacidade = v, 0.1f, 0.8f, 0.6f);
            RollPct(v => item.ReducaoCooldown = v, 0.1f, 0.8f, 0.6f);
        }
        else
        {
            bool isMagePrist = classes.Contains("Mago") || classes.Contains("Prist");

            RollInt(v => item.Forca = v, mainMin, mainMax, 0.7f);
            RollInt(v => item.Agilidade = v, mainMin, mainMax, 0.7f);
            RollInt(v => item.Destreza = v, mainMin, mainMax, 0.7f);
            if (isMagePrist)
            {
                RollInt(v => item.Inteligencia = v, mainMin, mainMax, 0.7f);
                RollInt(v => item.DanoMagico = v, mainMin, mainMax, 0.7f);
            }
            RollInt(v => item.DanoFisico = v, mainMin, mainMax, 0.8f);
            RollInt(v => item.DanoCriticoBonus = v, mainMin, mainMax, 0.5f);

            RollPct(v => item.ChanceCritica = v, 0.1f, 0.8f, 0.7f);
            RollPct(v => item.Precisao = v, 0.1f, 0.8f, 0.6f);
            RollPct(v => item.Evasao = v, 0.1f, 0.8f, 0.4f);
            RollPct(v => item.VelocidadeAtaque = v, 0.1f, 0.8f, 0.5f);
            RollPct(v => item.Tenacidade = v, 0.1f, 0.8f, 0.4f);
            RollPct(v => item.ReducaoCooldown = v, 0.1f, 0.8f, 0.4f);
        }

        RollPct(v => item.VelocidadeMovimento = v, 0.02f, 0.12f, 0.3f);
        RollPct(v => item.RouboVida = v, 0.2f, 1.5f, 0.3f);
        RollPct(v => item.RouboMana = v, 0.2f, 1.5f, 0.3f);
        RollPct(v => item.RegeneracaoVida = v, 0.3f, 2.0f, 0.25f);
        RollPct(v => item.RegeneracaoMana = v, 0.3f, 2.0f, 0.25f);
        RollPct(v => item.BonusExperiencia = v, 0.5f, 5.0f, 0.2f);
        RollPct(v => item.ChanceDropAumentada = v, 0.5f, 4.0f, 0.2f);
    }

    public static Raridade SortearRaridade()
    {
        double rolagem = _rng.NextDouble();
        if (rolagem < 0.40) return Raridade.Comum;
        if (rolagem < 0.65) return Raridade.Incomum;
        if (rolagem < 0.82) return Raridade.Raro;
        if (rolagem < 0.93) return Raridade.Epico;
        if (rolagem < 0.98) return Raridade.Lendario;
        return Raridade.Mistico;
    }
}
