using Godot;
using System;
using System.Collections.Generic;

public partial class InventarioUI : Control
{
    [Export] public PackedScene SlotUIPrefab; // Arraste a ceninha do slot aqui no inspetor
    
    private GridContainer _gridContainer;
    private InventarioComponent _inventarioAlvo;
    private List<SlotUI> _slotsVisuais = new List<SlotUI>();

    public override void _Ready()
    {
        _gridContainer = GetNode<GridContainer>("Panel/GridContainer");
        
        // Busca o componente de inventário que anexamos ao Player anteriormente
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player != null)
        {
            _inventarioAlvo = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
            
            if (_inventarioAlvo != null)
            {
                // Conecta o sinal do C# para atualizar a tela quando ganhar itens
                _inventarioAlvo.InventarioAtualizado += DesenharInterface;
                InicializarGrade();
            }
        }
        
        // Começa escondido (Aperte 'I' para abrir/fechar)
        Visible = false;
        // Garantir que o node processe _Process mesmo estando invisível,
        // para capturar a tecla globalmente e alternar a visibilidade.
        SetProcess(true);
    }
    public override void _Input(InputEvent @event)
    {
        // Mantemos o _Input funcionando caso o node esteja visível e receba eventos GUI
        if (@event.IsActionPressed("inventario") || 
           (@event is InputEventKey eventKey && eventKey.Pressed && eventKey.Keycode == Key.I))
        {
            Visible = !Visible;
            if (Visible)
            {
                DesenharInterface();
                GD.Print("[INVENTÁRIO] Painel aberto visualmente!");
            }
            else
            {
                GD.Print("[INVENTÁRIO] Painel fechado!");
            }
        }
    }

    private bool _prevIState = false;
    public override void _Process(double delta)
    {
        // Verifica ação do input globalmente mesmo com o Control invisível
        bool currentI = Input.IsKeyPressed(Key.I);
        if (Input.IsActionJustPressed("inventario") || (currentI && !_prevIState))
        {
            Visible = !Visible;
            if (Visible)
            {
                DesenharInterface();
                GD.Print("[INVENTÁRIO] Painel aberto visualmente! (via _Process)");
            }
            else
            {
                GD.Print("[INVENTÁRIO] Painel fechado! (via _Process)");
            }
        }
        _prevIState = currentI;
    }

    private void InicializarGrade()
    {
        if (_inventarioAlvo == null || SlotUIPrefab == null) return;

        // Limpa lixos antigos do editor
        foreach (Node child in _gridContainer.GetChildren())
        {
            child.QueueFree();
        }
        _slotsVisuais.Clear();

        // Cria a quantidade exata de quadradinhos físicos na tela
        for (int i = 0; i < _inventarioAlvo.TamanhoDoInventario; i++)
        {
            SlotUI novoSlotUI = SlotUIPrefab.Instantiate<SlotUI>();
            _gridContainer.AddChild(novoSlotUI);
            _slotsVisuais.Add(novoSlotUI);
        }
    }

    private void DesenharInterface()
    {
        if (_inventarioAlvo == null || _slotsVisuais.Count == 0) return;

        // Passa de slot em slot atualizando as imagens
        for (int i = 0; i < _inventarioAlvo.Slots.Count; i++)
        {
            if (i < _slotsVisuais.Count)
            {
                _slotsVisuais[i].AtualizarSlot(_inventarioAlvo.Slots[i]);
            }
        }
    }
}