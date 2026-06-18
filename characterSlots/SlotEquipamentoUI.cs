using Godot;
using System;
using System.Collections.Generic;

public partial class SlotEquipamentoUI : Control
{
    [Export] public TipoEquipamento TipoDeSlot { get; set; } = TipoEquipamento.Nenhum;

    private static readonly Dictionary<Raridade, Color> RarityColors = new()
    {
        [Raridade.Comum] = Color.FromHtml("#ffffff"),
        [Raridade.Incomum] = Color.FromHtml("#1eff00"),
        [Raridade.Raro] = Color.FromHtml("#0070dd"),
        [Raridade.Epico] = Color.FromHtml("#a335ee"),
        [Raridade.Lendario] = Color.FromHtml("#ffcc00"),
        [Raridade.Mistico] = Color.FromHtml("#ff4444"),
    };

    private TextureRect _icone;
    private ColorRect _fundoEscuro;
    private ColorRect _rarityGlow;

    public SlotInventario SlotLogico { get; private set; }

    private bool _mouseSobre;
    private double _ultimoCliqueEsquerdo;
    private const double IntervaloDuploClique = 300;

    public override void _Ready()
    {
        _icone = GetNode<TextureRect>("Icone");

        if (_icone != null)
        {
            _icone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icone.ExpandMode = TextureRect.ExpandModeEnum.FitBoth;
            _icone.CustomMinimumSize = new Vector2(40, 40);
            _icone.Size = new Vector2(40, 40);
        }

        CriarFundoEscuro();

        _rarityGlow = GetNodeOrNull<ColorRect>("RarityGlow");

        AddToGroup("SlotEquipamentoUI");

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        TooltipText = TipoDeSlot switch
        {
            TipoEquipamento.Capacete => "Capacete",
            TipoEquipamento.Peitoral => "Peitoral",
            TipoEquipamento.Cinto => "Cinto",
            TipoEquipamento.Luvas => "Luvas",
            TipoEquipamento.Calca => "Calças",
            TipoEquipamento.Botas => "Botas",
            TipoEquipamento.Arma => "Arma",
            TipoEquipamento.Escudo => "Escudo",
            TipoEquipamento.Colar => "Colar",
            TipoEquipamento.Anel => "Anel",
            TipoEquipamento.Brinco => "Brinco",
            TipoEquipamento.Runa => "Runa",
            TipoEquipamento.Asa => "Asa",
            TipoEquipamento.Montaria => "Montaria",
            TipoEquipamento.Pet => "Pet",
            TipoEquipamento.Skin => "Skin",
            _ => "",
        };
    }

    private void AtualizarGlow(ItemResource item)
    {
        if (item == null)
        {
            if (_rarityGlow != null)
                _rarityGlow.Visible = false;
            return;
        }

        if (_rarityGlow == null)
        {
            _rarityGlow = new ColorRect();
            _rarityGlow.Name = "RarityGlow";
            _rarityGlow.MouseFilter = MouseFilterEnum.Ignore;
            _rarityGlow.CustomMinimumSize = new Vector2(42, 42);
            _rarityGlow.Size = new Vector2(42, 42);
            _rarityGlow.Position = new Vector2(0, 0);
            _rarityGlow.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _rarityGlow.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            AddChild(_rarityGlow);
        }

        _rarityGlow.Visible = true;
        Color cor = RarityColors.GetValueOrDefault(item.Raridade, Colors.White);
        _rarityGlow.Color = Colors.Transparent;
        var style = new StyleBoxFlat();
        style.BgColor = Colors.Transparent;
        style.BorderWidthTop = 2;
        style.BorderWidthBottom = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;
        style.BorderColor = cor;
        style.CornerRadiusTopLeft = 3;
        style.CornerRadiusTopRight = 3;
        style.CornerRadiusBottomLeft = 3;
        style.CornerRadiusBottomRight = 3;
        _rarityGlow.AddThemeStyleboxOverride("normal", style);
    }

    private void OnMouseEntered()
    {
        _mouseSobre = true;
        MostrarTooltip();
    }

    private void OnMouseExited()
    {
        _mouseSobre = false;
        EsconderTooltip();
    }

    private ItemTooltip ObterTooltip()
    {
        return GetNodeOrNull<ItemTooltip>("/root/main/UI/ItemTooltip");
    }

    private void MostrarTooltip()
    {
        if (SlotLogico?.Item == null) return;
        var tip = ObterTooltip();
        if (tip == null) return;
        tip.Mostrar(SlotLogico.Item, GetGlobalMousePosition(), SlotLogico.RefinoNivel);
    }

    private void EsconderTooltip()
    {
        var tip = ObterTooltip();
        tip?.Esconder();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent || !mouseEvent.Pressed)
            return;

        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            double agora = Time.GetTicksMsec();
            bool duploClique = (agora - _ultimoCliqueEsquerdo) < IntervaloDuploClique;
            _ultimoCliqueEsquerdo = agora;

            if (!duploClique || !_mouseSobre)
                return;

            if (SlotLogico?.Item == null)
                return;

            DesequiparItem();
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void DesequiparItem()
    {
        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip == null) return;

        var inventario = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
        if (inventario == null) return;

        var item = SlotLogico.Item;
        if (inventario.AdicionarItem(item, 1))
        {
            SlotLogico.Item = null;
            SlotLogico.Quantidade = 0;
            equip.RecalcularBonusEquipamentos();
            equip.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
            inventario.NotificarMudancaExterna();

            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet != null && gameNet.IsConnected)
                gameNet.SendUnequipItem((int)TipoDeSlot, -1);

            GD.Print($"[EQUIP SLOT] Item '{item.Nome}' desequipado por duplo clique.");
        }
    }

    public void AtualizarSlot(SlotInventario slotLogico)
    {
        SlotLogico = slotLogico;

        if (_icone == null) return;

        if (slotLogico == null || slotLogico.Item == null)
        {
            AtualizarGlow(null);
            _icone.Texture = null;
            _icone.SelfModulate = new Color(1, 1, 1, 1);
        }
        else
        {
            AtualizarGlow(slotLogico.Item);
            _icone.CustomMinimumSize = new Vector2(40, 40);
            _icone.Texture = slotLogico.Item.Icone;
            _icone.SetDeferred("size", new Vector2(40, 40));
            _icone.SelfModulate = new Color(1, 1, 1, 1);
            GD.Print($"[EQUIP DEBUG] Icone Size={_icone.Size} MinSize={_icone.CustomMinimumSize} Expand={_icone.ExpandMode} Stretch={_icone.StretchMode} TexSize={(slotLogico.Item.Icone?.GetSize() ?? Vector2.Zero)}");
        }
    }

    private void CriarFundoEscuro()
    {
        _fundoEscuro = new ColorRect();
        _fundoEscuro.Name = "FundoEscuro";
        _fundoEscuro.MouseFilter = MouseFilterEnum.Ignore;
        _fundoEscuro.Size = new Vector2(42, 42);
        _fundoEscuro.Position = new Vector2(0, 0);
        _fundoEscuro.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _fundoEscuro.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _fundoEscuro.Color = new Color(0.06f, 0.06f, 0.08f, 0.85f);

        AddChild(_fundoEscuro);
        MoveChild(_fundoEscuro, 0);
    }

    public override Variant _GetDragData(Vector2 position)
    {
        if (SlotLogico == null || SlotLogico.Item == null) return default;

        TextureRect preview = new TextureRect();
        preview.Texture = SlotLogico.Item.Icone;
        preview.ExpandMode = TextureRect.ExpandModeEnum.FitBoth;
        preview.CustomMinimumSize = new Vector2(40, 40);
        preview.Size = new Vector2(40, 40);
        preview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        preview.Modulate = new Color(1, 1, 1, 0.7f);

        SetDragPreview(preview);
        return this;
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SlotUI slotOrigem)
        {
            if (slotOrigem.SlotInterno == null || slotOrigem.SlotInterno.Item == null) return false;

            if (slotOrigem.SlotInterno.Item.Tipo != this.TipoDeSlot) return false;

            var player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
            if (player != null && !EquipamentoComponent.PodeEquipar(slotOrigem.SlotInterno.Item, player.NomeDaClasse))
            {
                GD.Print($"[SLOT] Classe {player.NomeDaClasse} não pode equipar {slotOrigem.SlotInterno.Item.Nome} (drag barrado)");
                return false;
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

            var player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
            if (player != null && !EquipamentoComponent.PodeEquipar(slotOrigem.SlotInterno.Item, player.NomeDaClasse))
            {
                GD.Print($"[SLOT] Classe {player.NomeDaClasse} não pode equipar {slotOrigem.SlotInterno.Item.Nome}");
                return;
            }

            var equipamentos = player?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;

            if (equipamentos != null)
            {
                equipamentos.Equipar(this.TipoDeSlot, slotOrigem.SlotInterno);

                var inventario = player?.FindChild("InventarioComponent", true, false) as InventarioComponent;
                inventario?.NotificarMudancaExterna();
            }

            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet != null && gameNet.IsConnected)
                gameNet.SendEquipItem(slotOrigem.SlotIndex, (int)TipoDeSlot);
        }
    }
}
