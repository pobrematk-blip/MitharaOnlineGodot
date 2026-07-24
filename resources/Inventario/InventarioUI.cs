using Godot;
using System;
using System.Collections.Generic;

public partial class InventarioUI : Control
{
    [Export] public PackedScene SlotUIPrefab; // Lembre de conferir se está arrastado no Inspetor!
    
    private Panel _panel;
    private Panel _titleBar;
    private GridContainer _gridContainer;
    private InventarioComponent _inventarioAlvo;
    private List<SlotUI> _slotsVisuais = new List<SlotUI>();

    // Referências para o container e uma lista das bolsas visuais
    private HBoxContainer _containerBolsas;
    private List<SlotUI> _slotsBolsasVisuais = new List<SlotUI>();

    private bool _arrastando = false;
    private Vector2 _pontoCliqueOriginal;
    private Button _closeButton;
    private Label _infoCapacidade;
    private Label _goldLabel;
    private Label _diamondLabel;
    private GameNetwork _gameNet;
    private CashManager _cashManager;

    // Propriedade pública para verificar se o painel do inventário está visível
    public bool PainelVisivel => _panel != null && _panel.Visible;

    public void AbrirPainel()
    {
        if (_panel == null)
            return;

        _panel.Visible = true;
        _arrastando = false;
        DesenharInterface();
        AtualizarMoedas();
        MoveToFront();
        _panel.MoveToFront();
    }

    public void PosicionarPainel(Vector2 position)
    {
        if (_panel == null)
            return;

        _panel.Position = position;
        ResponsiveUI.ClampInsideViewport(_panel, 4f);
    }

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        
        // Busca o GridContainer usando o nome único (%), ignorando caminhos de pastas!
        if (HasNode("%GridContainer"))
        {
            _gridContainer = GetNode<GridContainer>("%GridContainer");
        }
        else
        {
            GD.PrintErr("[INVENTÁRIO UI] ❌ ERRO: GridContainer não encontrado. Verifique se ativou o 'Acesso Único ao Nó' (símbolo de %) nele no editor!");
        }
        
        // Captura o HBoxContainer que guarda a fileira de 6 mochilas manuais
        if (HasNode("%ContainerBolsas"))
        {
            _containerBolsas = GetNode<HBoxContainer>("%ContainerBolsas");
        }
        else
        {
            GD.Print("[INVENTÁRIO UI] ⚠️ Aviso: ContainerBolsas não encontrado por Acesso Único. Verifique se o nome está correto e com a % ativa.");
        }

        _goldLabel = GetNodeOrNull<Label>("%GoldLabel");
        _diamondLabel = GetNodeOrNull<Label>("%DiamondLabel");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnGoldUpdate += OnGoldUpdate;
            if (_goldLabel != null && _gameNet.Gold > 0)
                _goldLabel.Text = $"Ouro: {_gameNet.Gold}";
        }

        _cashManager = GetNodeOrNull<CashManager>("/root/CashManager");
        if (_cashManager != null && _diamondLabel != null)
            _diamondLabel.Text = $"Diamantes: {_cashManager.Diamantes}";
        
        Visible = true;
        
        if (_panel != null) 
        {
            _panel.Visible = false;
            _titleBar = _panel.GetNode<Panel>("TitleBar");
            _titleBar.GuiInput += OnTitleBarGuiInput;

            _infoCapacidade = GetNode<Label>("%InfoCapacidade");

            // Conecta o botão X para fechar o inventário
            if (_panel.HasNode("CloseButton"))
            {
                _closeButton = _panel.GetNode<Button>("CloseButton");
                _closeButton.Pressed += OnCloseButtonPressed;
                GD.Print("[INVENTÁRIO UI] ✅ CloseButton conectado com sucesso!");
                GD.Print($"[INVENTÁRIO UI] CloseButton nome: {_closeButton.Name}");
                GD.Print($"[INVENTÁRIO UI] CloseButton rect: {_closeButton.GetRect()}");
            }
            else
            {
                GD.PrintErr("[INVENTÁRIO UI] ❌ CloseButton não encontrado no painel!");
            }

            // Centraliza o painel na tela
            CallDeferred(MethodName.CentralizarPainelNaTela);
        }

        // Espera a árvore inteira do jogo estar pronta antes de buscar o Player!
        CallDeferred(MethodName.ConectarComponenteInventario);
    }

    private void OnGoldUpdate(int gold)
    {
        if (_goldLabel != null)
            _goldLabel.Text = $"Ouro: {gold}";
    }

    private void OnCloseButtonPressed()
    {
        GD.Print("[INVENTÁRIO UI] ❌ Botão X clicado - FECHANDO INVENTÁRIO!");
        if (_panel != null)
        {
            _panel.Visible = false;
            _arrastando = false;
            GD.Print("[INVENTÁRIO] Inventário fechado!");
        }
    }

    private void ConectarComponenteInventario()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player != null)
        {
            _inventarioAlvo = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
            
            if (_inventarioAlvo != null)
            {
                // Mapeia os slots fixos do rodapé uma única vez para evitar concorrência de dados
                MapearSlotsBolsasDoEditor();

                _inventarioAlvo.InventarioAtualizado += DesenharInterface;
                InicializarGrade();
                GD.Print("[INVENTÁRIO] ✅ Conectado com sucesso ao InventarioComponent do Player!");
            }
            else
            {
                GD.PrintErr("[INVENTÁRIO] ❌ Erro: Player encontrado, mas ele não tem o InventarioComponent!");
            }
        }
        else
        {
            GD.PrintErr("[INVENTÁRIO] ❌ Erro: Não foi possível encontrar o nó 'Player' na cena atual!");
        }
    }

    private void CentralizarPainelNaTela()
    {
        if (_panel == null) return;

        Vector2 tamanhoDaTela = GetViewportRect().Size;
        Vector2 tamanhoDoPainel = _panel.Size;

        _panel.Position = (tamanhoDaTela / 2) - (tamanhoDoPainel / 2);
    }

    private void AtualizarMoedas()
    {
        if (_goldLabel != null && _gameNet != null)
            _goldLabel.Text = $"Ouro: {_gameNet.Gold}";
        if (_diamondLabel != null && _cashManager != null)
            _diamondLabel.Text = $"Diamantes: {_cashManager.Diamantes}";
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (@event.IsActionPressed("inventario", false) || 
            (@event is InputEventKey eventKey && eventKey.Pressed && !eventKey.Echo && eventKey.Keycode == Key.I))
        {
            _panel.Visible = !_panel.Visible;

            if (_panel.Visible)
            {
                DesenharInterface();
                AtualizarMoedas();
                GD.Print("[INVENTÁRIO] 📖 Painel aberto!");
                GD.Print($"[INVENTÁRIO] Panel.Visible = {_panel.Visible}, PainelVisivel = {PainelVisivel}");
            }
            else
            {
                _arrastando = false; 
                GD.Print("[INVENTÁRIO] 📖 Painel fechado!");
                GD.Print($"[INVENTÁRIO] Panel.Visible = {_panel.Visible}, PainelVisivel = {PainelVisivel}");
            }

            GetViewport().SetInputAsHandled();
        }

        // Detecta clique no botão X se o inventário está visível
        if (_panel.Visible && @event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (_closeButton != null && _closeButton.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
            {
                GD.Print("[INVENTÁRIO] 🔴 Clique detectado no botão X!");
                _panel.Visible = false;
                _arrastando = false;
                GD.Print($"[INVENTÁRIO] ✅ Inventário fechado! Panel.Visible = {_panel.Visible}, PainelVisivel = {PainelVisivel}");
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed)
                _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void MapearSlotsBolsasDoEditor()
    {
        if (_containerBolsas == null || _inventarioAlvo == null) return;

        _slotsBolsasVisuais.Clear();
        GD.Print($"[INVENTÁRIO UI] 🔗 Mapeando slots de bolsa no container...");

        for (int i = 0; i < 6; i++)
        {
            string nomeSlotManual = $"SlotBolsa_{i}";

            if (_containerBolsas.HasNode(nomeSlotManual))
            {
                SlotUI slotManual = _containerBolsas.GetNode<SlotUI>(nomeSlotManual);
                slotManual.SlotIndex = -10 - i;
                _slotsBolsasVisuais.Add(slotManual);
                
                GD.Print($"[INVENTÁRIO UI]   ✅ {nomeSlotManual} encontrado e mapeado");

                // Força o vínculo inicial lógico <-> visual
                if (_inventarioAlvo.SlotsDasBolsasEquipadas != null && i < _inventarioAlvo.SlotsDasBolsasEquipadas.Count)
                {
                    slotManual.AtualizarSlot(_inventarioAlvo.SlotsDasBolsasEquipadas[i]);
                }
            }
            else
            {
                GD.PrintErr($"[INVENTÁRIO UI] ❌ Não achei o slot manual '{nomeSlotManual}' no ContainerBolsas!");
            }
        }
        
        GD.Print($"[INVENTÁRIO UI] ✅ Mapeamento de {_slotsBolsasVisuais.Count} slots completado!");
    }

    private void InicializarGrade()
    {
        if (_inventarioAlvo == null || SlotUIPrefab == null || _gridContainer == null) return;

        // Limpa os slots dinâmicos da grade principal de cima
        foreach (Node child in _gridContainer.GetChildren())
        {
            child.QueueFree();
        }
        _slotsVisuais.Clear();

        // Recria a grade baseado no tamanho atualizado
        for (int i = 0; i < _inventarioAlvo.TamanhoDoInventario; i++)
        {
            SlotUI novoSlotUI = SlotUIPrefab.Instantiate<SlotUI>();
            novoSlotUI.SlotIndex = i;
            _gridContainer.AddChild(novoSlotUI);
            _slotsVisuais.Add(novoSlotUI);
        }

        // Sincroniza imediatamente os dados nos novos slots criados
        ForçarAtualizacaoDosDadosDosSlots();
    }

    private void ForçarAtualizacaoDosDadosDosSlots()
    {
        if (_inventarioAlvo == null) return;

        // 1. Atualiza as imagens da grade superior comum
        for (int i = 0; i < _inventarioAlvo.Slots.Count; i++)
        {
            if (i < _slotsVisuais.Count)
            {
                _slotsVisuais[i].AtualizarSlot(_inventarioAlvo.Slots[i]);
            }
        }

        // 2. Atualiza as imagens do rodapé fixo de bolsas
        GD.Print($"[INVENTÁRIO UI] 🔄 Atualizando {_slotsBolsasVisuais.Count} slots de bolsa...");
        for (int i = 0; i < _slotsBolsasVisuais.Count; i++)
        {
            if (_inventarioAlvo.SlotsDasBolsasEquipadas != null && i < _inventarioAlvo.SlotsDasBolsasEquipadas.Count)
            {
                var bolsaLogica = _inventarioAlvo.SlotsDasBolsasEquipadas[i];
                var nomeItem = bolsaLogica?.Item?.Nome ?? "VAZIO";
                
                GD.Print($"[INVENTÁRIO UI]   Slot {i}: {nomeItem}");
                _slotsBolsasVisuais[i].AtualizarSlot(bolsaLogica);
            }
        }

        if (_infoCapacidade != null)
            _infoCapacidade.Text = $"Capacidade: {_inventarioAlvo.TamanhoDoInventario} slots";
    }

    private void DesenharInterface()
    {
        if (_inventarioAlvo == null) return;

        // Se o tamanho mudou na lógica, reconstrói a grade de cima de forma segura
        if (_slotsVisuais.Count != _inventarioAlvo.TamanhoDoInventario)
        {
            // Usamos CallDeferred para dar tempo da Godot processar o término do Drag antes de recriar nós
            CallDeferred(MethodName.InicializarGrade);
        }
        else
        {
            // Se o tamanho não mudou, apenas atualiza as imagens de forma leve
            ForçarAtualizacaoDosDadosDosSlots();
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_gameNet != null)
            _gameNet.OnGoldUpdate -= OnGoldUpdate;
    }
}
