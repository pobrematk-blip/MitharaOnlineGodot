using Godot;
using System.Collections.Generic;

public partial class CharacterUI : Control
{
    private Panel _panel;
    private Control _titleBar;
    private Button _closeButton;

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private EquipamentoComponent _equipamento;
    private Player _player;
    private Label _pontosDisponiveisLabel;
    private readonly Dictionary<string, Label> _atributosValores = new();
    private readonly Dictionary<string, Button> _atributosBotoes = new();
    private readonly List<SlotEquipamentoUI> _todosOsSlots = new();

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Control>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");

        if (HasNode("%ForcaValorLabel")) _atributosValores["Forca"] = GetNode<Label>("%ForcaValorLabel");
        if (HasNode("%AgilidadeValorLabel")) _atributosValores["Agilidade"] = GetNode<Label>("%AgilidadeValorLabel");
        if (HasNode("%DestrexaValorLabel")) _atributosValores["Destreza"] = GetNode<Label>("%DestrexaValorLabel");
        if (HasNode("%InteligenciaValorLabel")) _atributosValores["Inteligencia"] = GetNode<Label>("%InteligenciaValorLabel");

        if (HasNode("%ForcaBotaoPlus")) _atributosBotoes["Forca"] = GetNode<Button>("%ForcaBotaoPlus");
        if (HasNode("%AgilidadeBotaoPlus")) _atributosBotoes["Agilidade"] = GetNode<Button>("%AgilidadeBotaoPlus");
        if (HasNode("%DestrexaBotaoPlus")) _atributosBotoes["Destreza"] = GetNode<Button>("%DestrexaBotaoPlus");
        if (HasNode("%InteligenciaBotaoPlus")) _atributosBotoes["Inteligencia"] = GetNode<Button>("%InteligenciaBotaoPlus");

        if (HasNode("%PontosDisponiveisLabel"))
            _pontosDisponiveisLabel = GetNode<Label>("%PontosDisponiveisLabel");


        foreach (var kvp in _atributosBotoes)
        {
            string atributo = kvp.Key;
            Button botao = kvp.Value;
            botao.Pressed += () => OnAtributoPlus(atributo);
        }

        Visible = true;
        _panel.Visible = false;

        _closeButton.Pressed += OnCloseButtonPressed;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        BuscarTodosOsSlots();
        CallDeferred(MethodName.CentralizarPainelNaTela);
        CallDeferred(MethodName.ConectarEquipamento);
        CallDeferred(MethodName.ConectarPlayerStatus);
    }

    private void OnCloseButtonPressed()
    {
        FecharPainel();
    }

    private void FecharPainel()
    {
        if (_panel == null) return;

        _panel.Visible = false;
        _arrastando = false;
        GD.Print("[CHARACTER UI] ❌ Tela de equipamentos fechada!");
    }

    private void CentralizarPainelNaTela()
    {
        if (_panel == null) return;

        Vector2 tamanhoDaTela = GetViewportRect().Size;
        _panel.Position = (tamanhoDaTela / 2) - (_panel.Size / 2);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                _arrastando = true;
                _pontoCliqueOriginal = mouseEvent.Position;
            }
            else
            {
                _arrastando = false;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void BuscarTodosOsSlots()
    {
        _todosOsSlots.Clear();

        if (_panel == null) return;

        foreach (Node no in _panel.FindChildren("*", recursive: true))
        {
            if (no is SlotEquipamentoUI slot)
                _todosOsSlots.Add(slot);
        }

        GD.Print($"[CHARACTER UI] 📦 {_todosOsSlots.Count} slots de equipamento encontrados nesta cena!");
    }

    private void ConectarEquipamento()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player == null)
        {
            GD.PrintErr("[CHARACTER UI] ❌ Player não encontrado!");
            return;
        }

        _equipamento = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (_equipamento == null)
        {
            GD.PrintErr("[CHARACTER UI] ❌ Player não tem EquipamentoComponent!");
            return;
        }

        GD.Print("[CHARACTER UI] ✅ Conectado ao EquipamentoComponent!");
        _equipamento.EquipamentoAtualizado += AtualizarTela;
        AtualizarTela();
    }

    private void ConectarPlayerStatus()
    {
        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        if (_player == null)
        {
            GD.PrintErr("[CHARACTER UI] ❌ Player não encontrado para status de vida/mana!");
            return;
        }

        _player.StatusAtualizado += AtualizarTela;
        GD.Print("[CHARACTER UI] ✅ Conectado ao Player para status de vida/mana!");
    }

    private void AtualizarTela()
    {
        if (_equipamento == null) return;

        if (_pontosDisponiveisLabel != null)
            _pontosDisponiveisLabel.Text = $"Pontos Disponíveis: {_equipamento.PontosDisponiveis}";

        foreach (var kvp in _atributosValores)
        {
            int valor = kvp.Key switch
            {
                "Forca" => _equipamento.Forca,
                "Agilidade" => _equipamento.Agilidade,
                "Destreza" => _equipamento.Destreza,
                "Inteligencia" => _equipamento.Inteligencia,
                _ => 0
            };

            kvp.Value.Text = valor.ToString();
        }

        BuscarEAtualizarTodosOsStatus();
        AtualizarSlotsDaTela();
    }

    private void BuscarEAtualizarTodosOsStatus()
    {
        if (_equipamento == null) return;

        AtualizarStatusLabel("DanoFisico", $"Dano Físico: {_equipamento.DanoFisico}");
        AtualizarStatusLabel("DanoMagico", $"Dano Mágico: {_equipamento.DanoMagico}");

        if (_player != null)
        {
            AtualizarStatusLabel("Hp", $"Vida: {_player.CurrentHealth}/{_player.MaxHealth}");
            AtualizarStatusLabel("Mana", $"Mana: {_player.CurrentMana}/{_player.MaxMana}");
        }
        else
        {
            AtualizarStatusLabel("Hp", $"Vida: {_equipamento.Hp}");
            AtualizarStatusLabel("Mana", $"Mana: {_equipamento.Mana}");
        }
        AtualizarStatusLabel("ChanceCritica", $"Chance Crítica: {_equipamento.ChanceCritica:F1}%");
        AtualizarStatusLabel("DanoCritico", $"Dano Crítico: {_equipamento.DanoCritico:F2}x");
        AtualizarStatusLabel("Evasao", $"Evasão: {_equipamento.Evasao:F1}%");
        AtualizarStatusLabel("VelocidadeMovimento", $"Vel. Movimento: {_equipamento.VelocidadeMovimento:F2}x");
        AtualizarStatusLabel("VelocidadeAtaque", $"Vel. Ataque: {_equipamento.VelocidadeAtaque:F2}x");
        AtualizarStatusLabel("DefesaFisica", $"Defesa Física: {_equipamento.DefesaFisica}");
        AtualizarStatusLabel("DefesaMagica", $"Defesa Mágica: {_equipamento.DefesaMagica}");
        AtualizarStatusLabel("Precisao", $"Precisão: {_equipamento.Precisao}");
        AtualizarStatusLabel("Tenacidade", $"Tenacidade: {_equipamento.Tenacidade}");
        AtualizarStatusLabel("DanoPvp", $"Dano PvP: {_equipamento.DanoPvp}");
        AtualizarStatusLabel("DefesaPvp", $"Defesa PvP: {_equipamento.DefesaPvp}");
        AtualizarStatusLabel("PenetracacaoArmadura", $"Pen. Armadura: {_equipamento.PenetracacaoArmadura}");
        AtualizarStatusLabel("RegeneracaoVida", $"Regen Vida: {_equipamento.RegeneracaoVida}/s");
        AtualizarStatusLabel("RegeneracaoMana", $"Regen Mana: {_equipamento.RegeneracaoMana}/s");
        AtualizarStatusLabel("RouboVida", $"Roubo Vida: {_equipamento.RouboVida:F2}%");
        AtualizarStatusLabel("RouboMana", $"Roubo Mana: {_equipamento.RouboMana:F2}%");
        AtualizarStatusLabel("ReducaoCooldown", $"Red. Cooldown: {_equipamento.ReducaoCooldown:F2}%");
        AtualizarStatusLabel("BonusExperiencia", $"Bônus XP: {_equipamento.BonusExperiencia}%");
    }

    private void AtualizarStatusLabel(string nomeStatus, string texto)
    {
        string labelName = $"%{nomeStatus}Label";
        if (HasNode(labelName))
            GetNode<Label>(labelName).Text = texto;
    }

    private void AtualizarSlotsDaTela()
    {
        if (_equipamento == null) return;

        foreach (var slot in _todosOsSlots)
            slot.AtualizarSlot(_equipamento.ObterSlot(slot.TipoDeSlot));
    }

    private void OnAtributoPlus(string atributo)
    {
        if (_equipamento == null || _equipamento.PontosDisponiveis <= 0) return;

        switch (atributo)
        {
            case "Forca": _equipamento.AdicionarPontoForca(); break;
            case "Agilidade": _equipamento.AdicionarPontoAgilidade(); break;
            case "Destreza": _equipamento.AdicionarPontoDestreza(); break;
            case "Inteligencia": _equipamento.AdicionarPontoInteligencia(); break;
        }

        AtualizarTela();
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;

        if (@event.IsActionPressed("equipamento"))
        {
            _panel.Visible = !_panel.Visible;
            _arrastando = false;
            GetViewport().SetInputAsHandled();

            if (_panel.Visible)
            {
                GD.Print("[CHARACTER UI] 🎒 Tela de equipamentos aberta!");
                AtualizarTela();
            }
            else
            {
                GD.Print("[CHARACTER UI] ❌ Tela de equipamentos fechada!");
            }
        }

        if (_panel.Visible && @event is InputEventMouseButton mouseEvent
            && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left
            && _closeButton.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
        {
            FecharPainel();
            GetViewport().SetInputAsHandled();
        }
    }
}
