using Godot;
using System;

public partial class SlotEquipamentoUI : Control
{
    // Qual tipo de item este slot específico do corpo aceita
    [Export] public TipoEquipamento TipoDeSlot { get; set; } = TipoEquipamento.Nenhum;

    private TextureRect _icone;
    private Texture2D _texturaFundoPadrao;
    
    // Referência para o slot lógico dentro do EquipamentoComponent
    public SlotInventario SlotLogico { get; private set; }

    public override void _Ready()
    {
        _icone = GetNode<TextureRect>("Icone"); // Certifique-se de ter um TextureRect com esse nome dentro dele
        if (_icone != null)
        {
            _texturaFundoPadrao = _icone.Texture; // Guarda o desenho fantasma (ex: desenho do capacete vazio)
        }

        // Adiciona ao grupo para fácil localização
        AddToGroup("SlotEquipamentoUI");
    }

    // Atualiza o visual do slot (chamado pela UI principal do personagem)
    public void AtualizarSlot(SlotInventario slotLogico)
    {
        SlotLogico = slotLogico;

        if (_icone == null) return;

        if (slotLogico == null || slotLogico.Item == null)
        {
            // Volta para a imagem fantasma padrão do slot do corpo
            _icone.Texture = _texturaFundoPadrao;
            _icone.SelfModulate = new Color(1, 1, 1, 0.4f); // Deixa meio transparente para parecer vazio
        }
        else
        {
            // Mostra o item equipado
            _icone.Texture = slotLogico.Item.Icone;
            _icone.SelfModulate = new Color(1, 1, 1, 1); // Opacidade total
        }
    }

    // DRAG: Permite tirar o item do corpo arrastando
    public override Variant _GetDragData(Vector2 position)
    {
        if (SlotLogico == null || SlotLogico.Item == null) return default;

        TextureRect preview = new TextureRect();
        preview.Texture = SlotLogico.Item.Icone;
        preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        preview.CustomMinimumSize = new Vector2(40, 40);
        preview.Modulate = new Color(1, 1, 1, 0.7f);
        
        SetDragPreview(preview);
        return this; // Passa a si mesmo como dado do arrasto
    }

    // CAN DROP: Só aceita se o item que está vindo for do mesmo tipo do slot!
    public override bool _CanDropData(Vector2 position, Variant data)
    {
        // Se o item estiver vindo de um slot de inventário comum
        if (data.AsGodotObject() is SlotUI slotOrigem)
        {
            if (slotOrigem.SlotInterno == null || slotOrigem.SlotInterno.Item == null) return false;

            // REGRA CRÍTICA: O tipo do item precisa bater EXATAMENTE com o tipo deste slot do corpo
            return slotOrigem.SlotInterno.Item.Tipo == this.TipoDeSlot;
        }
        return false;
    }

    // DROP: Quando solta o item do inventário em cima deste slot do corpo
    public override void _DropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SlotUI slotOrigem)
        {
            if (slotOrigem.SlotInterno == null || slotOrigem.SlotInterno.Item == null) return;

            var player = GetTree().CurrentScene.FindChild("Player", true, false);
            var equipamentos = player?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;

            if (equipamentos != null)
            {
                // Manda o componente lógico equipar o item e resolver a troca
                equipamentos.Equipar(this.TipoDeSlot, slotOrigem.SlotInterno);
            }
        }
    }
}