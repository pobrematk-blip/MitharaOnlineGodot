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

    public int PontosDisponiveis => _pontosDisponiveis;
    public int Forca => _forca;
    public int Agilidade => _agilidade;
    public int Destreza => _destreza;
    public int Inteligencia => _inteligencia;

    // ============ STATUS DERIVADOS ============
    // Dano Físico com variação (por Força) - Base 8-12, aumenta com Força
    public int DanoFisicoMin => 8 + (Forca / 2);
    public int DanoFisicoMax => 12 + (Forca / 2);
    public string DanoFisico => $"{DanoFisicoMin}-{DanoFisicoMax}";  // Exibição: "10-14"
    
    // Dano Mágico com variação (por Inteligência) - Base 8-12, aumenta com Inteligência
    public int DanoMagicoMin => 8 + (Inteligencia / 2);
    public int DanoMagicoMax => 12 + (Inteligencia / 2);
    public string DanoMagico => $"{DanoMagicoMin}-{DanoMagicoMax}";  // Exibição: "10-14"
    
    // HP (por Força)
    public int Hp => 100 + (Forca * 5);
    
    // Mana (por Inteligência)
    public int Mana => 50 + (Inteligencia * 3);
    
    // Chance Crítica e Evasão (por Destreza)
    public float ChanceCritica => Destreza * 0.5f;              // Percentual de chance de golpe crítico
    public float Evasao => Destreza * 0.3f;                     // Percentual de chance de esquivar
    
    // Dano Crítico - Base 1.5x, aumenta APENAS com itens
    private float _bonusDanoCritico = 0f;                       // Bônus acumulado de items
    public float DanoCritico => 1.5f + _bonusDanoCritico;       // Multiplicador de dano crítico (1.5x base + bônus de items)
    
    // Velocidades (por Agilidade)
    public float VelocidadeMovimento => 1.0f + (Agilidade * 0.05f);
    public float VelocidadeAtaque => 1.0f + (Agilidade * 0.03f);
    
    // Defesas
    public int DefesaFisica => (int)(Agilidade / 2);
    public int DefesaMagica => (int)(Inteligencia / 3);
    
    // Precisão (por Agilidade + Destreza) e Tenacidade
    public int Precisao => (Agilidade / 2) + Destreza;
    public int Tenacidade => (int)(Forca / 5);
    
    // PvP
    public int DanoPvp => (Forca + Inteligencia) / 2;
    public int DefesaPvp => (Agilidade + Destreza) / 2;
    
    // Penetração e Absorção
    public int PenetracacaoArmadura => (int)(Forca / 10);
    
    // Regeneração
    public int RegeneracaoVida => (int)(Forca / 20);
    public int RegeneracaoMana => (int)(Inteligencia / 10);
    
    // Roubo
    public float RouboVida => Destreza / 50f;
    public float RouboMana => Inteligencia / 20f;
    
    // Redução e Bônus
    public float ReducaoCooldown => Agilidade / 100f;
    public float BonusExperiencia => (int)(Inteligencia / 100);

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
        // Se já houver algo no slot, a gente troca (devolve pro inventário)
        ItemResource itemParaEquipar = slotVindoDoInventario.Item;
        ItemResource itemAntigo = ItensEquipados[slot].Item;

        // Coloca o novo item no corpo
        ItensEquipados[slot].Item = itemParaEquipar;
        ItensEquipados[slot].Quantidade = 1;

        // Devolve o antigo para o slot de onde veio a nova peça
        slotVindoDoInventario.Item = itemAntigo;
        slotVindoDoInventario.Quantidade = (itemAntigo != null) ? 1 : 0;

        EmitSignal(SignalName.EquipamentoAtualizado);
        GD.Print($"Equipado {itemParaEquipar.Nome} no slot {slot}");
    }

    public void Desequipar(TipoEquipamento slot, InventarioComponent inventario)
    {
        ItemResource itemRemovido = ItensEquipados[slot].Item;
        if (itemRemovido == null) return;

        // Tenta colocar de volta no inventário
        if (inventario.AdicionarItem(itemRemovido, 1))
        {
            ItensEquipados[slot].Item = null;
            ItensEquipados[slot].Quantidade = 0;
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

    /// <summary>
    /// Calcula o dano físico final com variação aleatória
    /// </summary>
    public int CalcularDanoFisicoAleatorio()
    {
        return (int)(GD.Randi() % (DanoFisicoMax - DanoFisicoMin + 1)) + DanoFisicoMin;
    }

    /// <summary>
    /// Calcula o dano mágico final com variação aleatória
    /// </summary>
    public int CalcularDanoMagicoAleatorio()
    {
        return (int)(GD.Randi() % (DanoMagicoMax - DanoMagicoMin + 1)) + DanoMagicoMin;
    }

    /// <summary>
    /// Adiciona bônus de dano crítico de um item equipado
    /// </summary>
    public void AdicionarBonusDanoCritico(float bonusPercentual)
    {
        _bonusDanoCritico += bonusPercentual;
        EmitSignal(SignalName.EquipamentoAtualizado);
        GD.Print($"[EQUIPAMENTO] 🔥 Dano Crítico aumentado! Novo bônus: +{bonusPercentual:F2}x (Total: {DanoCritico:F2}x)");
    }

    /// <summary>
    /// Remove bônus de dano crítico de um item desequipado
    /// </summary>
    public void RemoverBonusDanoCritico(float bonusPercentual)
    {
        _bonusDanoCritico -= bonusPercentual;
        _bonusDanoCritico = Mathf.Max(_bonusDanoCritico, 0f);  // Não pode ser negativo
        EmitSignal(SignalName.EquipamentoAtualizado);
        GD.Print($"[EQUIPAMENTO] 📉 Dano Crítico reduzido! Novo bônus: +{_bonusDanoCritico:F2}x (Total: {DanoCritico:F2}x)");
    }

    // Métodos para adicionar pontos nos atributos
    public void AdicionarPontoForca()
    {
        if (_pontosDisponiveis > 0)
        {
            _forca++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] 💪 Força aumentada! Novo valor: {_forca}. Pontos restantes: {_pontosDisponiveis}");
        }
    }

    public void AdicionarPontoAgilidade()
    {
        if (_pontosDisponiveis > 0)
        {
            _agilidade++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] ⚡ Agilidade aumentada! Novo valor: {_agilidade}. Pontos restantes: {_pontosDisponiveis}");
        }
    }

    public void AdicionarPontoDestreza()
    {
        if (_pontosDisponiveis > 0)
        {
            _destreza++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] 🎯 Destreza aumentada! Novo valor: {_destreza}. Pontos restantes: {_pontosDisponiveis}");
        }
    }

    public void AdicionarPontoInteligencia()
    {
        if (_pontosDisponiveis > 0)
        {
            _inteligencia++;
            _pontosDisponiveis--;
            EmitSignal(SignalName.EquipamentoAtualizado);
            GD.Print($"[EQUIPAMENTO] 🧠 Inteligência aumentada! Novo valor: {_inteligencia}. Pontos restantes: {_pontosDisponiveis}");
        }
    }
}