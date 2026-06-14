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
    public int Hp => 100 + (Forca * 5) + _bonusHp;
    
    // Mana (por Inteligência + bônus de itens)
    public int Mana => 50 + (Inteligencia * 3) + _bonusMana;
    
    // Chance Crítica e Evasão (por Destreza + bônus de itens)
    public float ChanceCritica => (Destreza * 0.5f) + _bonusChanceCritica;
    public float Evasao => (Destreza * 0.3f) + _bonusEvasao;
    
    // Dano Crítico - Base 1.5x, aumenta APENAS com itens
    private float _bonusDanoCritico = 0f;
    public float DanoCritico => 1.5f + _bonusDanoCritico;
    
    // Velocidades (por Agilidade + bônus de itens)
    public float VelocidadeMovimento => 1.0f + (Agilidade * 0.05f) + _bonusVelocidadeMovimento;
    public float VelocidadeAtaque => 1.0f + (Agilidade * 0.03f) + _bonusVelocidadeAtaque;
    
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
    public float RouboVida => Destreza / 50f + _bonusRouboVida;
    public float RouboMana => Inteligencia / 20f + _bonusRouboMana;
    
    // Redução e Bônus
    public float ReducaoCooldown => Agilidade / 100f + _bonusReducaoCooldown;
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

    private void RecalcularBonusEquipamentos()
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

            _bonusForca += item.Forca;
            _bonusAgilidade += item.Agilidade;
            _bonusDestreza += item.Destreza;
            _bonusInteligencia += item.Inteligencia;
            _bonusDanoFisico += item.DanoFisico;
            _bonusDanoFisicoMin += item.DanoFisicoMin;
            _bonusDanoFisicoMax += item.DanoFisicoMax;
            _bonusDanoMagico += item.DanoMagico;
            _bonusDanoMagicoMin += item.DanoMagicoMin;
            _bonusDanoMagicoMax += item.DanoMagicoMax;
            _bonusDefesaFisica += item.DefesaFisica;
            _bonusDefesaMagica += item.DefesaMagica;
            _bonusHp += item.Hp;
            _bonusMana += item.Mana;
            _bonusStamina += item.Stamina;
            _bonusVelocidadeMovimento += item.VelocidadeMovimento;
            _bonusVelocidadeAtaque += item.VelocidadeAtaque;
            _bonusChanceCritica += item.ChanceCritica;
            _bonusEvasao += item.Evasao;
            _bonusDanoCritico += item.DanoCriticoBonus;
            _bonusPrecisao += item.Precisao;
            _bonusTenacidade += item.Tenacidade;
            _bonusDanoPvp += item.DanoPvp;
            _bonusDefesaPvp += item.DefesaPvp;
            _bonusPenetracaoArmadura += item.PenetracaoArmadura;
            _bonusRegeneracaoVida += item.RegeneracaoVida;
            _bonusRegeneracaoMana += item.RegeneracaoMana;
            _bonusRouboVida += item.RouboVida;
            _bonusRouboMana += item.RouboMana;
            _bonusReducaoCooldown += item.ReducaoCooldown;
            _bonusBonusExperiencia += item.BonusExperiencia;
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
        if (_pontosDisponiveis > 0)
        {
            _forca++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] Forca aumentada! Novo valor: {_forca}. Pontos restantes: {_pontosDisponiveis}");
            SendAllocateStat("forca");
        }
    }

    public void AdicionarPontoAgilidade()
    {
        if (_pontosDisponiveis > 0)
        {
            _agilidade++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] Agilidade aumentada! Novo valor: {_agilidade}. Pontos restantes: {_pontosDisponiveis}");
            SendAllocateStat("agilidade");
        }
    }

    public void AdicionarPontoDestreza()
    {
        if (_pontosDisponiveis > 0)
        {
            _destreza++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] Destreza aumentada! Novo valor: {_destreza}. Pontos restantes: {_pontosDisponiveis}");
            SendAllocateStat("destreza");
        }
    }

    public void AdicionarPontoInteligencia()
    {
        if (_pontosDisponiveis > 0)
        {
            _inteligencia++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] Inteligencia aumentada! Novo valor: {_inteligencia}. Pontos restantes: {_pontosDisponiveis}");
            SendAllocateStat("inteligencia");
        }
    }

    private void SendAllocateStat(string statName)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
            gameNet.SendAllocateStat(statName);
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
