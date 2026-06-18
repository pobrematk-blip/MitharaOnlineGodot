using Godot;
using System;
using System.Collections.Generic;

public partial class EquipamentoComponent : Node
{
    [Signal] public delegate void EquipamentoAtualizadoEventHandler();

    // Dicionário que guarda qual item está em cada slot
    // Ex: <TipoEquipamento.Capacete, SlotLogico>
    public Dictionary<TipoEquipamento, SlotInventario> ItensEquipados = new Dictionary<TipoEquipamento, SlotInventario>();

    // Atributos Base
    private int _pontosDisponiveis = 10;
    private int _forca = 10;
    private int _agilidade = 10;
    private int _destreza = 10;
    private int _inteligencia = 10;

    // Bônus de equipamentos
    private int _bonusForca;
    private int _bonusAgilidade;
    private int _bonusDestreza;
    private int _bonusInteligencia;
    private int _bonusDanoFisico;
    private int _bonusDefesaFisica;
    private int _bonusHp;
    private int _bonusMana;
    private float _bonusVelocidadeMovimento;
    private float _bonusVelocidadeAtaque;
    private float _bonusChanceCritica;
    private float _bonusEvasao;
    private int _bonusDanoFisicoMin;
    private int _bonusDanoFisicoMax;
    private int _bonusDanoMagico;
    private int _bonusDanoMagicoMin;
    private int _bonusDanoMagicoMax;
    private int _bonusDefesaMagica;
    private float _bonusPrecisao;
    private float _bonusTenacidade;
    private int _bonusDanoPvp;
    private int _bonusDefesaPvp;
    private int _bonusPenetracaoArmadura;
    private float _bonusRegeneracaoVida;
    private float _bonusRegeneracaoMana;
    private float _bonusRouboVida;
    private float _bonusRouboMana;
    private float _bonusReducaoCooldown;
    private float _bonusBonusExperiencia;
    private int _bonusStamina;

    public int PontosDisponiveis => _pontosDisponiveis;
    public int Forca => _forca + _bonusForca;
    public int Agilidade => _agilidade + _bonusAgilidade;
    public int Destreza => _destreza + _bonusDestreza;
    public int Inteligencia => _inteligencia + _bonusInteligencia;

    // ============ STATUS DERIVADOS ============
    // Dano Físico com variação (por Força) - Base 8-12, aumenta com Força
    public int DanoFisicoMin => 8 + (Forca / 2) + _bonusDanoFisico + _bonusDanoFisicoMin;
    public int DanoFisicoMax => 12 + (Forca / 2) + _bonusDanoFisico + _bonusDanoFisicoMax;
    public string DanoFisico => $"{DanoFisicoMin}-{DanoFisicoMax}";
    
    // Dano Mágico com variação (por Inteligência) - Base 8-12, aumenta com Inteligência
    public int DanoMagicoMin => 8 + (Inteligencia / 2) + _bonusDanoMagico + _bonusDanoMagicoMin;
    public int DanoMagicoMax => 12 + (Inteligencia / 2) + _bonusDanoMagico + _bonusDanoMagicoMax;
    public string DanoMagico => $"{DanoMagicoMin}-{DanoMagicoMax}";
    
    // HP (por Força + bônus de itens)
    public int Hp => 100 + (Forca * 2) + _bonusHp;
    
    // Mana (por Inteligência + bônus de itens)
    public int Mana => 50 + (Inteligencia * 3) + _bonusMana;
    
    // Chance Crítica e Evasão (por Destreza + bônus de itens)
    public float ChanceCritica => MathF.Min(75f, (Destreza * 0.25f) + _bonusChanceCritica);
    public float Evasao => MathF.Min(40f, (Destreza * 0.3f) + _bonusEvasao);
    
    // Dano Crítico - Base 1.5x, aumenta APENAS com itens
    private float _bonusDanoCritico = 0f;
    public float DanoCritico => 1.5f + _bonusDanoCritico;
    
    // Velocidades (por Agilidade + bônus de itens)
    public float VelocidadeMovimento => 1.0f + (Agilidade * 0.05f) + _bonusVelocidadeMovimento;
    public float VelocidadeAtaque => MathF.Min(100f, 1.0f + (Agilidade * 0.03f) + _bonusVelocidadeAtaque);
    
    // Defesas
    public int DefesaFisica => (Agilidade / 2) + _bonusDefesaFisica;
    public int DefesaMagica => (Inteligencia / 3) + _bonusDefesaMagica;
    
    // Precisão (por Agilidade + Destreza) e Tenacidade
    public float Precisao => (Agilidade / 2f) + Destreza + _bonusPrecisao;
    public float Tenacidade => (Forca / 5f) + _bonusTenacidade;
    
    // PvP
    public int DanoPvp => (Forca + Inteligencia) / 2 + _bonusDanoPvp;
    public int DefesaPvp => (Agilidade + Destreza) / 2 + _bonusDefesaPvp;
    
    // Penetração e Absorção
    public int PenetracaoArmadura => Forca / 10 + _bonusPenetracaoArmadura;
    
    // Regeneração
    public float RegeneracaoVida => Forca / 20f + _bonusRegeneracaoVida;
    public float RegeneracaoMana => Inteligencia / 10f + _bonusRegeneracaoMana;
    
    // Roubo
    public float RouboVida => MathF.Min(15f, Destreza / 50f + _bonusRouboVida);
    public float RouboMana => MathF.Min(15f, Inteligencia / 20f + _bonusRouboMana);
    
    // Redução e Bônus
    public float ReducaoCooldown => MathF.Min(40f, Agilidade / 100f + _bonusReducaoCooldown);
    public float BonusExperiencia => Inteligencia / 100f + _bonusBonusExperiencia;
    public int Stamina => 100 + (Agilidade * 2) + _bonusStamina;

    public override void _Ready()
    {
        // Inicializamos todos os slots possíveis como vazios
        foreach (TipoEquipamento tipo in Enum.GetValues(typeof(TipoEquipamento)))
        {
            if (tipo == TipoEquipamento.Nenhum) continue;
            ItensEquipados[tipo] = new SlotInventario();
        }
    }

    public void Equipar(TipoEquipamento slot, SlotInventario slotVindoDoInventario)
    {
        ItemResource itemParaEquipar = slotVindoDoInventario.Item;
        ItemResource itemAntigo = ItensEquipados[slot].Item;

        ItensEquipados[slot].Item = itemParaEquipar;
        ItensEquipados[slot].Quantidade = 1;

        slotVindoDoInventario.Item = itemAntigo;
        slotVindoDoInventario.Quantidade = (itemAntigo != null) ? 1 : 0;

        RecalcularBonusEquipamentos();
        EmitSignal(SignalName.EquipamentoAtualizado);
        GD.Print($"Equipado {itemParaEquipar.Nome} no slot {slot}");
    }

    public void Desequipar(TipoEquipamento slot, InventarioComponent inventario)
    {
        ItemResource itemRemovido = ItensEquipados[slot].Item;
        if (itemRemovido == null) return;

        if (inventario.AdicionarItem(itemRemovido, 1))
        {
            ItensEquipados[slot].Item = null;
            ItensEquipados[slot].Quantidade = 0;
            RecalcularBonusEquipamentos();
            EmitSignal(SignalName.EquipamentoAtualizado);
        }
    }

    // Método para obter um slot específico
    public SlotInventario ObterSlot(TipoEquipamento tipo)
    {
        if (ItensEquipados.ContainsKey(tipo))
        {
            return ItensEquipados[tipo];
        }
        return null;
    }

    public int CalcularDanoFisicoAleatorio()
    {
        return (int)(GD.Randi() % (DanoFisicoMax - DanoFisicoMin + 1)) + DanoFisicoMin;
    }

    public int CalcularDanoMagicoAleatorio()
    {
        return (int)(GD.Randi() % (DanoMagicoMax - DanoMagicoMin + 1)) + DanoMagicoMin;
    }

    public void RecalcularBonusEquipamentos()
    {
        _bonusForca = 0;
        _bonusAgilidade = 0;
        _bonusDestreza = 0;
        _bonusInteligencia = 0;
        _bonusDanoFisico = 0;
        _bonusDanoFisicoMin = 0;
        _bonusDanoFisicoMax = 0;
        _bonusDanoMagico = 0;
        _bonusDanoMagicoMin = 0;
        _bonusDanoMagicoMax = 0;
        _bonusDefesaFisica = 0;
        _bonusDefesaMagica = 0;
        _bonusHp = 0;
        _bonusMana = 0;
        _bonusStamina = 0;
        _bonusVelocidadeMovimento = 0f;
        _bonusVelocidadeAtaque = 0f;
        _bonusChanceCritica = 0f;
        _bonusEvasao = 0f;
        _bonusDanoCritico = 0f;
        _bonusPrecisao = 0f;
        _bonusTenacidade = 0f;
        _bonusDanoPvp = 0;
        _bonusDefesaPvp = 0;
        _bonusPenetracaoArmadura = 0;
        _bonusRegeneracaoVida = 0f;
        _bonusRegeneracaoMana = 0f;
        _bonusRouboVida = 0f;
        _bonusRouboMana = 0f;
        _bonusReducaoCooldown = 0f;
        _bonusBonusExperiencia = 0f;

        foreach (var kv in ItensEquipados)
        {
            var item = kv.Value?.Item;
            if (item == null) continue;

            double refineMult = kv.Value.RefinoNivel switch
            {
                1 => 1.02,
                2 => 1.04,
                3 => 1.06,
                4 => 1.08,
                5 => 1.10,
                6 => 1.13,
                7 => 1.16,
                8 => 1.20,
                9 => 1.25,
                10 => 1.30,
                _ => 1.0,
            };

            _bonusForca += (int)(item.Forca * refineMult);
            _bonusAgilidade += (int)(item.Agilidade * refineMult);
            _bonusDestreza += (int)(item.Destreza * refineMult);
            _bonusInteligencia += (int)(item.Inteligencia * refineMult);
            _bonusDanoFisico += (int)(item.DanoFisico * refineMult);
            _bonusDanoFisicoMin += (int)(item.DanoFisicoMin * refineMult);
            _bonusDanoFisicoMax += (int)(item.DanoFisicoMax * refineMult);
            _bonusDanoMagico += (int)(item.DanoMagico * refineMult);
            _bonusDanoMagicoMin += (int)(item.DanoMagicoMin * refineMult);
            _bonusDanoMagicoMax += (int)(item.DanoMagicoMax * refineMult);
            _bonusDefesaFisica += (int)(item.DefesaFisica * refineMult);
            _bonusDefesaMagica += (int)(item.DefesaMagica * refineMult);
            _bonusHp += (int)(item.Hp * refineMult);
            _bonusMana += (int)(item.Mana * refineMult);
            _bonusStamina += (int)(item.Stamina * refineMult);
            _bonusVelocidadeMovimento += (float)(item.VelocidadeMovimento * refineMult);
            _bonusVelocidadeAtaque += (float)(item.VelocidadeAtaque * refineMult);
            _bonusChanceCritica += (float)(item.ChanceCritica * refineMult);
            _bonusEvasao += (float)(item.Evasao * refineMult);
            _bonusDanoCritico += (float)(item.DanoCriticoBonus * refineMult);
            _bonusPrecisao += (float)(item.Precisao * refineMult);
            _bonusTenacidade += (float)(item.Tenacidade * refineMult);
            _bonusDanoPvp += (int)(item.DanoPvp * refineMult);
            _bonusDefesaPvp += (int)(item.DefesaPvp * refineMult);
            _bonusPenetracaoArmadura += (int)(item.PenetracaoArmadura * refineMult);
            _bonusRegeneracaoVida += (float)(item.RegeneracaoVida * refineMult);
            _bonusRegeneracaoMana += (float)(item.RegeneracaoMana * refineMult);
            _bonusRouboVida += (float)(item.RouboVida * refineMult);
            _bonusRouboMana += (float)(item.RouboMana * refineMult);
            _bonusReducaoCooldown += (float)(item.ReducaoCooldown * refineMult);
            _bonusBonusExperiencia += (float)(item.BonusExperiencia * refineMult);
        }
    }

    public void AdicionarBonusDanoCritico(float bonusPercentual)
    {
        _bonusDanoCritico += bonusPercentual;
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void RemoverBonusDanoCritico(float bonusPercentual)
    {
        _bonusDanoCritico -= bonusPercentual;
        _bonusDanoCritico = Mathf.Max(_bonusDanoCritico, 0f);
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void AdicionarPontoForca()
    {
        RequestAllocateStat("forca");
    }

    public void AdicionarPontoAgilidade()
    {
        RequestAllocateStat("agilidade");
    }

    public void AdicionarPontoDestreza()
    {
        RequestAllocateStat("destreza");
    }

    public void AdicionarPontoInteligencia()
    {
        RequestAllocateStat("inteligencia");
    }

    private void RequestAllocateStat(string statName)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
        {
            gameNet.SendAllocateStat(statName);
            return;
        }

        GD.PrintErr("[EQUIPAMENTO] Alocacao de atributo local bloqueada. Use o servidor.");
    }

    public void AdicionarPontosDisponiveis(int qtd)
    {
        _pontosDisponiveis += qtd;
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public static bool PodeEquipar(ItemResource item, string nomeClasse)
    {
        if (item == null) return true;
        if (string.IsNullOrWhiteSpace(item.ClassesPermitidas)) return true;
        var classes = item.ClassesPermitidas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var c in classes)
        {
            if (string.Equals(c, nomeClasse, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void ImportarEstado(int forca, int agilidade, int destreza, int inteligencia, int pontosDisponiveis)
    {
        _forca = forca;
        _agilidade = agilidade;
        _destreza = destreza;
        _inteligencia = inteligencia;
        _pontosDisponiveis = pontosDisponiveis;
        RecalcularBonusEquipamentos();
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void ImportarEquipamentos(System.Collections.Generic.Dictionary<TipoEquipamento, int> equipados, ItemDatabase itemDB)
    {
        if (equipados == null || itemDB == null) return;
        foreach (var kv in equipados)
        {
            if (kv.Value <= 0) continue;
            var item = itemDB.GetItem(kv.Value);
            if (item == null) continue;

            ItensEquipados[kv.Key].Item = item;
            ItensEquipados[kv.Key].Quantidade = 1;
        }
        RecalcularBonusEquipamentos();
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public static void PreencherPreview(ClasseCustomResource classe, out int forca, out int agilidade, out int destreza, out int inteligencia)
    {
        forca = classe.Forca;
        agilidade = classe.Agilidade;
        destreza = classe.Destreza;
        inteligencia = classe.Inteligencia;

        if (classe.Raca != null)
        {
            forca += classe.Raca.BonusForca;
            agilidade += classe.Raca.BonusAgilidade;
            destreza += classe.Raca.BonusDestreza;
            inteligencia += classe.Raca.BonusInteligencia;
        }
    }
}
