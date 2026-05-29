using Godot;
using System;

public partial class ItemColetavel : Area2D
{
    // Arraste o seu arquivo "BolsaDeCouro.tres" para este campo no Inspetor da Godot!
    [Export] public ItemResource ItemContido { get; set; }

    public override void _Ready()
    {
        // Conecta o evento de quando o player encosta no item
        BodyEntered += OnBodyEntered;
        
        // Atualiza o visual do Sprite no chão com o ícone do próprio recurso
        if (ItemContido != null && HasNode("Sprite2D"))
        {
            GetNode<Sprite2D>("Sprite2D").Texture = ItemContido.Icone;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        // Verifica se quem encostou foi o Player e se ele tem o componente de inventário
        var inventario = body.GetNodeOrNull<InventarioComponent>("InventarioComponent");
        
        if (inventario != null && ItemContido != null)
        {
            // Tenta adicionar a bolsa ao inventário do player
            bool coletado = inventario.AdicionarItem(ItemContido, 1);
            
            if (coletado)
            {
                GD.Print($"[MUNDO] Player coletou o item: {ItemContido.Nome}!");
                QueueFree(); // Remove o item do chão do mapa
            }
        }
    }
}