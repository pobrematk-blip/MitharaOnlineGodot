using Godot;
using System;

public partial class SlotUI : Control
{
    private TextureRect _icone;
    private Label _quantidadeTexto;
    private Texture2D _texturaFundoPadrao;

    public SlotInventario SlotInterno { get; private set; }

    public override void _Ready()
    {
        _icone = GetNode<TextureRect>("Icone");
        _quantidadeTexto = GetNode<Label>("Quantidade");

        if (_icone != null)
        {
            _texturaFundoPadrao = _icone.Texture;
        }

        // Conecta sinais de mouse para feedback visual
        MouseEntered += OnMouseEnteredSlot;
        MouseExited += OnMouseExitedSlot;
    }

    private void OnMouseEnteredSlot()
    {
        // Se é um slot de bolsa com item, destaca
        if (Name.ToString().StartsWith("SlotBolsa_") && SlotInterno?.Item != null && SlotInterno.Item.EhBolsa)
        {
            _icone.SelfModulate = new Color(1.2f, 1.2f, 0.8f, 1); // Destaca em amarelo
        }
    }

    private void OnMouseExitedSlot()
    {
        // Volta à cor normal
        if (SlotInterno?.Item != null)
        {
            _icone.SelfModulate = new Color(1, 1, 1, 1);
        }
    }

    public void CustomMinimumSizeAjustado() { }

    public void AtualizarSlot(SlotInventario slotLogico)
    {
        SlotInterno = slotLogico;

        if (_icone == null || _quantidadeTexto == null) return;

        Visible = true; 
        _icone.Visible = true;

        if (slotLogico == null || slotLogico.Item == null)
        {
            if (_texturaFundoPadrao != null)
            {
                _icone.Texture = _texturaFundoPadrao;
                _icone.SelfModulate = new Color(1, 1, 1, 1);
            }
            else
            {
                _icone.Texture = GD.Load<Texture2D>("res://icon.svg"); 
                _icone.SelfModulate = new Color(0.2f, 0.2f, 0.2f, 0.6f); 
            }
            
            _quantidadeTexto.Text = "";
            _quantidadeTexto.Visible = false;
        }
        else
        {
            _icone.Texture = slotLogico.Item.Icone;
            _icone.SelfModulate = new Color(1, 1, 1, 1); 
            
            if (slotLogico.Quantidade > 1)
            {
                _quantidadeTexto.Text = slotLogico.Quantidade.ToString();
                _quantidadeTexto.Visible = true;
            }
            else
            {
                _quantidadeTexto.Text = "";
                _quantidadeTexto.Visible = false;
            }

            // Se é um slot de bolsa COM um item, adiciona uma tooltip/aviso
            if (Name.ToString().StartsWith("SlotBolsa_") && slotLogico.Item != null && slotLogico.Item.EhBolsa)
            {
                TooltipText = $"🎒 {slotLogico.Item.Nome}\n\n✓ Clique DIREITO para remover\n✓ Ou arraste para fora\n\n+{slotLogico.Item.SlotsAdicionais} slots";
            }
        }
    }

    public override Variant _GetDragData(Vector2 position)
    {
        if (SlotInterno == null || SlotInterno.Item == null) return default;

        TextureRect preview = new TextureRect();
        preview.Texture = SlotInterno.Item.Icone;
        preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        preview.CustomMinimumSize = new Vector2(40, 40);
        preview.Modulate = new Color(1, 1, 1, 0.7f);
        
        SetDragPreview(preview);
        return this;
    }

    public override void _Input(InputEvent @event)
    {
        // Clique direito em slot de bolsa para remover rápido
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
        {
            if (Name.ToString().StartsWith("SlotBolsa_") && SlotInterno?.Item != null)
            {
                int indexBolsa = int.Parse(Name.ToString().Split('_')[1]);
                var player = GetTree().CurrentScene.FindChild("Player", true, false);
                var inventario = player?.FindChild("InventarioComponent", true, false) as InventarioComponent;

                if (inventario != null)
                {
                    GD.Print($"[INVENTÁRIO] 🗑️ Removendo bolsa do slot {indexBolsa}...");
                    
                    // Busca um slot vazio na grade principal para colocar a bolsa
                    bool colocouEmAlgumLugar = false;
                    var slots = inventario.Slots;
                    
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (slots[i].Item == null)
                        {
                            slots[i].Item = SlotInterno.Item;
                            slots[i].Quantidade = SlotInterno.Quantidade;
                            colocouEmAlgumLugar = true;
                            GD.Print($"[INVENTÁRIO] ✅ Bolsa movida para slot {i} da grade principal!");
                            break;
                        }
                    }

                    if (!colocouEmAlgumLugar)
                    {
                        GD.Print("[INVENTÁRIO] ❌ Inventário cheio! Não há espaço para retirar a bolsa.");
                        return;
                    }

                    // Remove a bolsa do slot de bolsa
                    inventario.DesequiparBolsaNoSlot(indexBolsa);
                    SlotInterno.Item = null;
                    SlotInterno.Quantidade = 0;
                    
                    inventario.NotificarMudancaExterna();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SlotUI slotOrigem)
        {
            if (slotOrigem.SlotInterno == null || slotOrigem.SlotInterno.Item == null) return false;

            if (Name.ToString().StartsWith("SlotBolsa_"))
            {
                return slotOrigem.SlotInterno.Item.EhBolsa;
            }
            return true;
        }
        return false;
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SlotUI slotOrigem)
        {
            if (slotOrigem.SlotInterno == null || slotOrigem.SlotInterno.Item == null) return;

            var player = GetTree().CurrentScene.FindChild("Player", true, false);
            var inventario = player?.FindChild("InventarioComponent", true, false) as InventarioComponent;
            
            if (inventario == null) return;

            // =========================================================================
            // CASO A: Equipar Bolsa no Rodapé manual
            // =========================================================================
            if (Name.ToString().StartsWith("SlotBolsa_"))
            {
                int indexBolsa = int.Parse(Name.ToString().Split('_')[1]);

                // Guarda se tinha uma bolsa antiga voltando
                var itemAntigoNaBolsa = inventario.SlotsDasBolsasEquipadas[indexBolsa].Item;

                // 1. Aplica a alteração lógica no componente do Player
                inventario.EquiparBolsaNoSlot(slotOrigem.SlotInterno, indexBolsa);

                // 2. Limpa ou substitui o slot de origem lá em cima ANTES do redesenho ocorrer
                if (itemAntigoNaBolsa == null)
                {
                    slotOrigem.SlotInterno.Item = null;
                    slotOrigem.SlotInterno.Quantidade = 0;
                }

                // 3. Agora sim forçamos a interface a atualizar com tudo nos conformes!
                inventario.NotificarMudancaExterna();
            }
            // =========================================================================
            // CASO B: Desequipar Bolsa do Rodapé para a Grade de cima
            // =========================================================================
            else if (slotOrigem.Name.ToString().StartsWith("SlotBolsa_"))
            {
                int indexBolsaOrigem = int.Parse(slotOrigem.Name.ToString().Split('_')[1]);

                // Destino está vazio: movemos para cima e esvaziamos embaixo
                if (this.SlotInterno != null && this.SlotInterno.Item == null)
                {
                    this.SlotInterno.Item = slotOrigem.SlotInterno.Item;
                    this.SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;

                    inventario.DesequiparBolsaNoSlot(indexBolsaOrigem);

                    slotOrigem.SlotInterno.Item = null;
                    slotOrigem.SlotInterno.Quantidade = 0;
                }
                // Destino já tem outra bolsa: faz a troca justa de posições
                else if (this.SlotInterno != null && this.SlotInterno.Item.EhBolsa)
                {
                    var itemTemp = this.SlotInterno.Item;
                    int quantTemp = this.SlotInterno.Quantidade;

                    this.SlotInterno.Item = slotOrigem.SlotInterno.Item;
                    this.SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;

                    slotOrigem.SlotInterno.Item = itemTemp;
                    slotOrigem.SlotInterno.Quantidade = quantTemp;

                    inventario.EquiparBolsaNoSlot(this.SlotInterno, indexBolsaOrigem);
                }

                inventario.NotificarMudancaExterna();
            }
            // =========================================================================
            // CASO C: Movimentação padrão entre slots comuns
            // =========================================================================
            else
            {
                if (this.SlotInterno == null) return;

                var itemTemp = this.SlotInterno.Item;
                int quantTemp = this.SlotInterno.Quantidade;

                this.SlotInterno.Item = slotOrigem.SlotInterno.Item;
                this.SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;

                slotOrigem.SlotInterno.Item = itemTemp;
                slotOrigem.SlotInterno.Quantidade = quantTemp;

                inventario.NotificarMudancaExterna();
            }
        }
    }
}