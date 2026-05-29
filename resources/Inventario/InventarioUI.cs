using Godot;
using System;
using System.Collections.Generic;

public partial class InventarioUI : Control
{
    [Export] public PackedScene SlotUIPrefab; // Lembre de conferir se está arrastado no Inspetor!
    
    private Panel _panel; 
    private GridContainer _gridContainer;
    private InventarioComponent _inventarioAlvo;
    private List<SlotUI> _slotsVisuais = new List<SlotUI>();

    // Referências para o container e uma lista das bolsas visuais
    private HBoxContainer _containerBolsas;
    private List<SlotUI> _slotsBolsasVisuais = new List<SlotUI>();

    private bool _arrastando = false;
    private Vector2 _pontoCliqueOriginal;

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
        
        Visible = true;
        
        if (_panel != null) 
        {
            _panel.Visible = false;
            _panel.GuiInput += OnPanelGuiInput;

            // Conecta o botão X para fechar o inventário
            if (_panel.HasNode("CloseButton"))
            {
                var closeButton = _panel.GetNode<Button>("CloseButton");
                closeButton.Pressed += OnCloseButtonPressed;
                GD.Print("[INVENTÁRIO UI] ✅ CloseButton conectado com sucesso!");
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

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;

        if (@event.IsActionPressed("inventario", false) || 
            (@event is InputEventKey eventKey && eventKey.Pressed && !eventKey.Echo && eventKey.Keycode == Key.I))
        {
            _panel.Visible = !_panel.Visible;

            if (_panel.Visible)
            {
                DesenharInterface();
                GD.Print("[INVENTÁRIO] Painel aberto!");
            }
            else
            {
                _arrastando = false; 
                GD.Print("[INVENTÁRIO] Painel fechado!");
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                // Verifica se o clique foi no botão X para evitar conflito de arrasto
                if (_panel.HasNode("CloseButton"))
                {
                    var closeButton = _panel.GetNode<Button>("CloseButton");
                    if (closeButton.GetGlobalRect().HasPoint(mouseEvent.Position + _panel.GlobalPosition))
                    {
                        return; // Ignora este evento se foi no botão
                    }
                }

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
}