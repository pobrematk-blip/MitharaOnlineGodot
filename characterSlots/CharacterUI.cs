using Godot;
using System;
using System.Collections.Generic;

public partial class CharacterUI : Control
{
    private EquipamentoComponent _equipamento;
    private Label _pontosDisponiveisLabel;
    private Dictionary<string, Label> _atributosValores = new();
    private Dictionary<string, Button> _atributosBotoes = new();
    private Dictionary<string, Label> _statusDerivados = new();
    
    // Referências aos slots da tela
    private List<SlotEquipamentoUI> _todosOsSlots = new();

    public override void _Ready()
    {
        // Busca as labels de atributos
        if (HasNode("%ForcaValorLabel")) _atributosValores["Forca"] = GetNode<Label>("%ForcaValorLabel");
        if (HasNode("%AgilidadeValorLabel")) _atributosValores["Agilidade"] = GetNode<Label>("%AgilidadeValorLabel");
        if (HasNode("%DestrexaValorLabel")) _atributosValores["Destreza"] = GetNode<Label>("%DestrexaValorLabel");
        if (HasNode("%InteligenciaValorLabel")) _atributosValores["Inteligencia"] = GetNode<Label>("%InteligenciaValorLabel");

        // Busca os botões de atributos
        if (HasNode("%ForcaBotaoPlus")) _atributosBotoes["Forca"] = GetNode<Button>("%ForcaBotaoPlus");
        if (HasNode("%AgilidadeBotaoPlus")) _atributosBotoes["Agilidade"] = GetNode<Button>("%AgilidadeBotaoPlus");
        if (HasNode("%DestrexaBotaoPlus")) _atributosBotoes["Destreza"] = GetNode<Button>("%DestrexaBotaoPlus");
        if (HasNode("%InteligenciaBotaoPlus")) _atributosBotoes["Inteligencia"] = GetNode<Button>("%InteligenciaBotaoPlus");

        // Busca os labels de status derivados
        if (HasNode("%AtaqueLabel")) _statusDerivados["Ataque"] = GetNode<Label>("%AtaqueLabel");
        if (HasNode("%DefesaLabel")) _statusDerivados["Defesa"] = GetNode<Label>("%DefesaLabel");
        if (HasNode("%RegenLabel")) _statusDerivados["Regen"] = GetNode<Label>("%RegenLabel");
        if (HasNode("%VidaTotalLabel")) _statusDerivados["VidaTotal"] = GetNode<Label>("%VidaTotalLabel");

        if (HasNode("%PontosDisponiveisLabel"))
            _pontosDisponiveisLabel = GetNode<Label>("%PontosDisponiveisLabel");

        // Busca todos os slots de equipamento
        BuscarTodosOsSlots();

        // Conecta os botões aos eventos
        foreach (var kvp in _atributosBotoes)
        {
            string atributo = kvp.Key;
            Button botao = kvp.Value;
            botao.Pressed += () => OnAtributoPlus(atributo);
        }

        // Busca o equipamento do Player
        CallDeferred(MethodName.ConectarEquipamento);
    }

    private void BuscarTodosOsSlots()
    {
        _todosOsSlots.Clear();
        
        // Busca recursivamente todos os nós SlotEquipamentoUI
        var todosOsNos = GetTree().GetNodesInGroup("SlotEquipamentoUI");
        foreach (Node no in todosOsNos)
        {
            if (no is SlotEquipamentoUI slot)
            {
                _todosOsSlots.Add(slot);
            }
        }

        GD.Print($"[CHARACTER UI] 📦 {_todosOsSlots.Count} slots de equipamento encontrados!");
    }

    private void ConectarEquipamento()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player != null)
        {
            _equipamento = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
            if (_equipamento != null)
            {
                GD.Print("[CHARACTER UI] ✅ Conectado ao EquipamentoComponent!");
                _equipamento.EquipamentoAtualizado += AtualizarTela;
                AtualizarTela();
            }
            else
            {
                GD.PrintErr("[CHARACTER UI] ❌ Player não tem EquipamentoComponent!");
            }
        }
        else
        {
            GD.PrintErr("[CHARACTER UI] ❌ Player não encontrado!");
        }
    }

    private void AtualizarTela()
    {
        if (_equipamento == null) return;

        // Atualiza pontos disponíveis
        if (_pontosDisponiveisLabel != null)
        {
            _pontosDisponiveisLabel.Text = $"Pontos Disponíveis: {_equipamento.PontosDisponiveis}";
        }

        // Atualiza valores dos atributos
        foreach (var kvp in _atributosValores)
        {
            string atributo = kvp.Key;
            Label label = kvp.Value;
            
            int valor = atributo switch
            {
                "Forca" => _equipamento.Forca,
                "Agilidade" => _equipamento.Agilidade,
                "Destreza" => _equipamento.Destreza,
                "Inteligencia" => _equipamento.Inteligencia,
                _ => 0
            };

            label.Text = valor.ToString();
        }

        // Atualiza status derivados
        if (_statusDerivados.ContainsKey("Ataque"))
            _statusDerivados["Ataque"].Text = $"Ataque: {_equipamento.Ataque}";
        if (_statusDerivados.ContainsKey("Defesa"))
            _statusDerivados["Defesa"].Text = $"Defesa: {_equipamento.Defesa}";
        if (_statusDerivados.ContainsKey("Regen"))
            _statusDerivados["Regen"].Text = $"Regen: {_equipamento.Regen}";
        if (_statusDerivados.ContainsKey("VidaTotal"))
            _statusDerivados["VidaTotal"].Text = $"Vida Total: {_equipamento.VidaTotal}";

        // Atualiza os slots visuais
        AtualizarSlotsDaTela();
    }

    private void AtualizarSlotsDaTela()
    {
        if (_equipamento == null) return;

        foreach (var slot in _todosOsSlots)
        {
            // Encontra o slot lógico correspondente
            var slotLogico = _equipamento.ObterSlot(slot.TipoDeSlot);
            slot.AtualizarSlot(slotLogico);
        }
    }

    private void OnAtributoPlus(string atributo)
    {
        if (_equipamento == null || _equipamento.PontosDisponiveis <= 0) return;

        switch (atributo)
        {
            case "Forca":
                _equipamento.AdicionarPontoForca();
                break;
            case "Agilidade":
                _equipamento.AdicionarPontoAgilidade();
                break;
            case "Destreza":
                _equipamento.AdicionarPontoDestreza();
                break;
            case "Inteligencia":
                _equipamento.AdicionarPontoInteligencia();
                break;
        }

        AtualizarTela();
    }
}
