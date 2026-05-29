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

    // Status Derivados (calculados a partir dos atributos)
    public int Ataque => Forca + (Destreza / 2);
    public int Defesa => Agilidade / 2;
    public int Regen => Inteligencia / 5;
    public int VidaTotal => 100 + (Forca * 5);

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