using Godot;
using System;

public partial class SlotUI : Control
{
    private TextureRect _iconeSprite;
    private Label _quantidadeTexto;

    public override void _Ready()
    {
        // Busca os nós filhos de exibição visual
        _iconeSprite = GetNode<TextureRect>("Icone");
        _quantidadeTexto = GetNode<Label>("Quantidade");

        // Começa completamente limpo/vazio
        LimparSlot();
    }

    // Atualiza o quadradinho com as informações do item real
    public void AtualizarSlot(SlotInventario dadosSlot)
    {
        if (dadosSlot == null || dadosSlot.Item == null)
        {
            LimparSlot();
            return;
        }

        _iconeSprite.Texture = dadosSlot.Item.Icone;
        _iconeSprite.Visible = true;

        if (dadosSlot.Item.Acumulavel && dadosSlot.Quantidade > 1)
        {
            _quantidadeTexto.Text = dadosSlot.Quantidade.ToString();
            _quantidadeTexto.Visible = true;
        }
        else
        {
            _quantidadeTexto.Visible = false;
        }
    }

    public void LimparSlot()
    {
        if (_iconeSprite != null) _iconeSprite.Visible = false;
        if (_quantidadeTexto != null) _quantidadeTexto.Visible = false;
    }
}