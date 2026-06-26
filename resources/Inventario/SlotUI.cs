using Godot;
using System;
using System.Collections.Generic;
#nullable enable annotations

public partial class SlotUI : Control
{
    private enum TipoContainerUi { Nenhum, Inventario, Banco }

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
    private Label _quantidadeTexto;
    private ColorRect _fundoEscuro;
    private ColorRect _rarityGlow;
    private const float IconPadding = 2f;

    public SlotInventario SlotInterno { get; private set; }
    public int SlotIndex { get; set; }

    public override void _Ready()
    {
        _icone = GetNode<TextureRect>("Icone");
        _quantidadeTexto = GetNode<Label>("Quantidade");

        if (_icone != null)
        {
            _icone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icone.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            AtualizarDimensoesIcone();
        }

        CriarFundoEscuro();

        _rarityGlow = GetNodeOrNull<ColorRect>("RarityGlow");

        MouseEntered += OnMouseEnteredSlot;
        MouseExited += OnMouseExitedSlot;
    }

    private Vector2 ObterTamanhoSlot()
    {
        float slotWidth = Size.X > 0 ? Size.X : CustomMinimumSize.X;
        float slotHeight = Size.Y > 0 ? Size.Y : CustomMinimumSize.Y;

        if (slotWidth <= 0) slotWidth = 42f;
        if (slotHeight <= 0) slotHeight = 42f;

        return new Vector2(slotWidth, slotHeight);
    }

    private Vector2 ObterTamanhoIcone()
    {
        Vector2 slotSize = ObterTamanhoSlot();
        float iconWidth = Mathf.Max(1f, slotSize.X - IconPadding);
        float iconHeight = Mathf.Max(1f, slotSize.Y - IconPadding);
        return new Vector2(iconWidth, iconHeight);
    }

    private void AtualizarDimensoesIcone()
    {
        if (_icone == null) return;

        Vector2 slotSize = ObterTamanhoSlot();
        Vector2 iconSize = ObterTamanhoIcone();

        _icone.Position = (slotSize - iconSize) / 2f;
        _icone.Size = iconSize;
        _icone.CustomMinimumSize = Vector2.Zero;
        _icone.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
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
        _rarityGlow.Set("theme_override_styles/normal", style);
    }

    private TipoContainerUi ObterContainerUi()
    {
        Node n = this;
        while (n != null)
        {
            if (n is BancoUI) return TipoContainerUi.Banco;
            if (n is InventarioUI) return TipoContainerUi.Inventario;
            n = n.GetParent();
        }
        return TipoContainerUi.Nenhum;
    }

    private bool EhSlotBolsaInventario => Name.ToString().StartsWith("SlotBolsa_") && !Name.ToString().StartsWith("SlotBolsaBanco_");
    private bool EhSlotBolsaBanco => Name.ToString().StartsWith("SlotBolsaBanco_");
    private bool EhQualquerSlotBolsa => EhSlotBolsaInventario || EhSlotBolsaBanco;

    private int ObterIndiceBolsa()
    {
        var partes = Name.ToString().Split('_');
        if (partes.Length < 2) return -1;
        return int.Parse(partes[^1]);
    }

    private InventarioComponent ObterInventario()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        return player?.FindChild("InventarioComponent", true, false) as InventarioComponent;
    }

    private BancoComponent ObterBanco()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        return player?.FindChild("BancoComponent", true, false) as BancoComponent;
    }

    private void OnMouseEnteredSlot()
    {
        _mouseSobre = true;

        if (SlotInterno?.Item == null) return;

        if (EhQualquerSlotBolsa && SlotInterno.Item.EhBolsa)
            _icone.SelfModulate = new Color(1.2f, 1.2f, 0.8f, 1);

        MostrarTooltip();
    }

    private void OnMouseExitedSlot()
    {
        _mouseSobre = false;

        if (SlotInterno?.Item != null)
            _icone.SelfModulate = new Color(1, 1, 1, 1);

        EsconderTooltip();
    }

    private ItemTooltip ObterTooltip()
    {
        return GetNodeOrNull<ItemTooltip>("/root/main/UI/ItemTooltip");
    }

    private void MostrarTooltip()
    {
        if (SlotInterno?.Item == null) return;
        var tip = ObterTooltip();
        if (tip == null) return;

        if (EhSlotBolsaInventario && PodeComparar(SlotInterno.Item.Tipo))
        {
            var itemNovo = SlotInterno.Item;
            var equip = ObterEquipamentoComponent();
            if (equip != null && equip.ItensEquipados.TryGetValue(itemNovo.Tipo, out var slotEquipado)
                && slotEquipado?.Item != null)
            {
                tip.MostrarComparacao(itemNovo, slotEquipado.Item, GetGlobalMousePosition(), SlotInterno.RefinoNivel, slotEquipado.RefinoNivel);
                return;
            }
        }

        tip.Mostrar(SlotInterno.Item, GetGlobalMousePosition(), SlotInterno.RefinoNivel);
    }

    private void EsconderTooltip()
    {
        var tip = ObterTooltip();
        tip?.Esconder();
    }

    private EquipamentoComponent ObterEquipamentoComponent()
    {
        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        return player?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
    }

    private static bool PodeComparar(TipoEquipamento tipo)
    {
        return tipo switch
        {
            TipoEquipamento.Arma => true,
            TipoEquipamento.Capacete => true,
            TipoEquipamento.Peitoral => true,
            TipoEquipamento.Cinto => true,
            TipoEquipamento.Luvas => true,
            TipoEquipamento.Calca => true,
            TipoEquipamento.Botas => true,
            TipoEquipamento.Escudo => true,
            TipoEquipamento.Colar => true,
            TipoEquipamento.Anel => true,
            TipoEquipamento.Brinco => true,
            _ => false,
        };
    }

    public void AtualizarSlot(SlotInventario slotLogico)
    {
        SlotInterno = slotLogico;

        if (_icone == null || _quantidadeTexto == null) return;

        Visible = true;
        _icone.Visible = true;

        if (slotLogico == null || slotLogico.Item == null)
        {
            AtualizarGlow(null);

            _icone.Texture = null;
            _icone.SelfModulate = new Color(1, 1, 1, 1);

            _quantidadeTexto.Text = "";
            _quantidadeTexto.Visible = false;
            TooltipText = "";
        }
        else
        {
            AtualizarGlow(slotLogico.Item);

            AtualizarDimensoesIcone();
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

            if (EhQualquerSlotBolsa && slotLogico.Item.EhBolsa)
                TooltipText = $"ðŸŽ’ {slotLogico.Item.Nome}\n+{slotLogico.Item.SlotsAdicionais} slots no {(EhSlotBolsaBanco ? "banco" : "inventário")}";
        }
    }

    public override Variant _GetDragData(Vector2 position)
    {
        if (SlotInterno == null || SlotInterno.Item == null) return default;

        Vector2 iconSize = ObterTamanhoIcone();
        var preview = new TextureRect
        {
            Texture = SlotInterno.Item.Icone,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = iconSize,
            Size = iconSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color(1, 1, 1, 0.7f)
        };

        SetDragPreview(preview);
        return this;
    }

    private double _ultimoCliqueEsquerdo = 0;
    private const double IntervaloDuploClique = 300;
    private bool _mouseSobre = false;

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

            GD.Print($"[SLOT] Duplo clique no slot {SlotIndex}");
            if (SlotInterno?.Item == null)
            {
                GD.Print("[SLOT] Slot vazio.");
                return;
            }
            if (EhQualquerSlotBolsa)
            {
                GD.Print("[SLOT] Slot de bolsa, ignorando.");
                return;
            }

            EquiparItemDoSlot();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseEvent.ButtonIndex != MouseButton.Right || !_mouseSobre)
            return;

        if (SlotInterno?.Item == null) return;

        if (EhQualquerSlotBolsa)
        {
            int indexBolsa = ObterIndiceBolsa();
            if (indexBolsa < 0) return;

            if (EhSlotBolsaInventario)
                RemoverBolsaParaInventario(indexBolsa);
            else if (EhSlotBolsaBanco)
                RemoverBolsaParaBanco(indexBolsa);

            GetViewport().SetInputAsHandled();
            return;
        }

        if (SlotInterno.Item.ItemID >= 100 && SlotInterno.Item.ItemID < 200)
        {
            UsarItemNoSlot();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EquiparItemDoSlot()
    {
        var item = SlotInterno.Item;
        if (item.Tipo == TipoEquipamento.Nenhum
            || item.Tipo == TipoEquipamento.Consumivel
            || item.Tipo == TipoEquipamento.Moeda
            || item.Tipo == TipoEquipamento.Feitico
            || item.EhBolsa)
            return;

        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        if (!EquipamentoComponent.PodeEquipar(item, player.NomeDaClasse))
        {
            GD.Print($"[SLOT] Classe {player.NomeDaClasse} não pode equipar {item.Nome}");
            return;
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
        {
            gameNet.SendEquipItem(SlotIndex, (int)item.Tipo);
            GD.Print($"[SLOT] Pedido online para equipar '{item.Nome}' enviado.");
            return;
        }

        GD.PrintErr("[SLOT] Equipar local bloqueado. Conecte ao servidor para equipar itens.");
    }

    private void UsarItemNoSlot()
    {
        if (SlotInterno?.Item == null) return;

        int itemId = SlotInterno.Item.ItemID;
        string itemNome = SlotInterno.Item.Nome;

        if (itemId == 110 || itemId == 111)
        {
            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet == null || !gameNet.IsConnected)
            {
                GD.PrintErr("[CONSUMÍVEL] Uso local bloqueado. Conecte ao servidor.");
                return;
            }

            gameNet.SendUseItem(SlotIndex);
        }
        else if (itemId == 101)
        {
            TentarReviverAliadoComPergaminho();
        }
        else if (itemId == 100 || itemId == 114 || (itemNome != null && itemNome.IndexOf("Pergaminho", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            int maxTent = itemId == 114 ? 5 : 7;
            TentarCapturarPetComPergaminho(maxTent);
        }
    }

    private void TentarCapturarPetComPergaminho(int maxTent = 7)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[PERGAMINHO] Sem conexão com o servidor para capturar pet.");
            return;
        }

        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Node2D;
        if (player == null) return;

        var inimigos = GetTree().GetNodesInGroup("Inimigos");
        Inimigo alvo = null;
        float menorDist = 200f;

        foreach (var node in inimigos)
        {
            if (node is Inimigo inimigo)
            {
                float dist = player.GlobalPosition.DistanceTo(inimigo.GlobalPosition);
                if (dist < menorDist && inimigo.VidaAtual > 0 && inimigo.VidaAtual <= inimigo.VidaMax * 0.5f)
                {
                    menorDist = dist;
                    alvo = inimigo;
                }
            }
        }

        if (alvo == null)
        {
            GD.Print("[PERGAMINHO] Nenhum inimigo com menos de 50% de vida por perto.");
            return;
        }

        if (alvo.PetID <= 0)
        {
            GD.Print("[PERGAMINHO] Este inimigo não pode ser capturado.");
            return;
        }

        var inventario = ObterInventario();
        if (inventario == null) return;

        string petNome = alvo.NomeDoInimigo;
        int petId = alvo.PetID;

        var miniGame = GD.Load<PackedScene>("res://ui/Pets/PetScrollMiniGame.tscn").Instantiate<PetScrollMiniGame>();
        var hud = GetNodeOrNull<CanvasLayer>("/root/main/HUD");
        if (hud == null)
        {
            var root = GetTree()?.Root;
            if (root != null)
            {
                for (int i = 0; i < root.GetChildCount(); i++)
                {
                    var h = root.GetChild(i).FindChild("HUD", true, false) as CanvasLayer;
                    if (h != null)
                    {
                        hud = h;
                        break;
                    }
                }
            }
        }
        if (hud != null)
            hud.AddChild(miniGame);

        miniGame.Connect(PetScrollMiniGame.SignalName.MiniGameConcluido, Callable.From((int capturedPetId, string capturedPetNome, bool sucesso) =>
        {
            // Consome o pergaminho (sucesso ou falha)
            SlotInterno.Quantidade--;
            if (SlotInterno.Quantidade <= 0)
            {
                SlotInterno.Item = null;
                SlotInterno.Quantidade = 0;
            }
            inventario.NotificarMudancaExterna();

            if (sucesso && IsInstanceValid(alvo))
            {
                // Registra o pet na coleção
                var player = GetTree().CurrentScene?.FindChild("Player", true, false);
                var colecao = player?.FindChild("PetColecaoComponent", true, false) as PetColecaoComponent;
                if (colecao != null)
                    colecao.RegistrarCaptura(capturedPetId, capturedPetNome);

                var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
                if (gameNet != null && gameNet.IsConnected)
                    gameNet.SendPetCapture(capturedPetId, capturedPetNome);

                alvo.QueueFree();
                var chat = GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
                chat?.AddSystemMessage($"Pet '{capturedPetNome}' capturado com sucesso!");
                GD.Print($"[PERGAMINHO] Pet {capturedPetNome} capturado!");
            }

            if (IsInstanceValid(miniGame))
                miniGame.QueueFree();
        }));

        miniGame.IniciarMiniGame(petId, petNome, maxTent);
    }

    private void TentarReviverAliadoComPergaminho()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.Print("[PERGAMINHO] Sem conexão para reviver.");
            return;
        }

        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Node2D;
        if (player == null) return;

        var downedNodes = GetTree()?.GetNodesInGroup("PlayersDowned");
        if (downedNodes == null || downedNodes.Count == 0)
        {
            GD.Print("[PERGAMINHO] Nenhum aliado caído por perto.");
            return;
        }

        Node2D? nearest = null;
        float nearestDist = 200f;
        foreach (Node node in downedNodes)
        {
            if (node is Node2D n2d)
            {
                float d = player.GlobalPosition.DistanceTo(n2d.GlobalPosition);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = n2d;
                }
            }
        }

        if (nearest == null || !nearest.HasMeta("network_id"))
        {
            GD.Print("[PERGAMINHO] Nenhum aliado caído válido encontrado.");
            return;
        }

        ulong targetId = (ulong)nearest.GetMeta("network_id").AsInt64();
        GD.Print($"[PERGAMINHO] Revivendo aliado {targetId} (dist={nearestDist:F1})");

        gameNet.SendRevivePlayer(targetId);
    }

    private void RemoverBolsaParaInventario(int indexBolsa)
    {
        GD.PrintErr("[INVENTARIO] Bolsa local bloqueada. Operacao deve passar pelo servidor.");
    }

    private void RemoverBolsaParaBanco(int indexBolsa)
    {
        GD.PrintErr("[BANCO] Bolsa local bloqueada. Operacao deve passar pelo servidor.");
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SkillBarSlotUI) return true;

        if (data.AsGodotObject() is SlotEquipamentoUI)
            return SlotInterno != null && SlotInterno.Item == null && ObterContainerUi() == TipoContainerUi.Inventario;

        if (data.AsGodotObject() is not SlotUI slotOrigem
            || slotOrigem.SlotInterno?.Item == null)
            return false;

        if (EhQualquerSlotBolsa)
            return slotOrigem.SlotInterno.Item.EhBolsa;

        if (ObterContainerUi() != slotOrigem.ObterContainerUi())
            return SlotInterno != null && SlotInterno.Item == null;

        return true;
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SkillBarSlotUI skillBarSlot)
        {
            GD.PrintErr("[SLOT] Retornar item da barra localmente foi bloqueado.");
            skillBarSlot.Clear();
            return;
        }

        if (data.AsGodotObject() is SlotEquipamentoUI slotEquipOrigem)
        {
            if (slotEquipOrigem.SlotLogico?.Item == null) return;

            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet != null && gameNet.IsConnected)
            {
                gameNet.SendUnequipItem((int)slotEquipOrigem.TipoDeSlot, SlotIndex);
                return;
            }

            GD.PrintErr("[SLOT] Desequipar local bloqueado. Conecte ao servidor para desequipar itens.");

            return;
        }

        if (data.AsGodotObject() is not SlotUI slotOrigem
            || slotOrigem.SlotInterno?.Item == null)
            return;

        var containerDestino = ObterContainerUi();
        var containerOrigem = slotOrigem.ObterContainerUi();

        // Equipar bolsa no inventário
        if (EhSlotBolsaInventario)
        {
            GD.PrintErr("[INVENTARIO] Equipar bolsa local bloqueado. Operacao deve passar pelo servidor.");
            return;
        }

        // Equipar bolsa no banco
        if (EhSlotBolsaBanco)
        {
            GD.PrintErr("[BANCO] Equipar bolsa local bloqueado. Operacao deve passar pelo servidor.");
            return;
        }

        // Transferência entre inventário e banco
        if (containerOrigem != containerDestino)
        {
            GD.PrintErr("[SLOT] Transfer?ncia invent?rio/banco local bloqueada. Use pacotes do servidor.");
            return;
        }

        // Movimentação dentro do mesmo container
        if (containerOrigem == TipoContainerUi.Inventario)
        {
            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet != null && gameNet.IsConnected)
                gameNet.SendMoveItem(slotOrigem.SlotIndex, SlotIndex);
            else
                GD.PrintErr("[INVENTARIO] Mover item local bloqueado. Conecte ao servidor.");
        }
        else if (containerOrigem == TipoContainerUi.Banco)
        {
            GD.PrintErr("[BANCO] Mover item local bloqueado. Banco deve passar pelo servidor.");
        }
    }

    private void MoverDentroDoInventario(SlotUI slotOrigem)
    {
        var inventario = ObterInventario();
        if (inventario == null || SlotInterno == null) return;

        if (slotOrigem.EhSlotBolsaInventario)
        {
            int indexOrigem = slotOrigem.ObterIndiceBolsa();

            if (SlotInterno.Item == null)
            {
                if (!inventario.DesequiparBolsaNoSlot(indexOrigem))
                    return;
                SlotInterno.Item = slotOrigem.SlotInterno.Item;
                SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;
                slotOrigem.SlotInterno.Item = null;
                slotOrigem.SlotInterno.Quantidade = 0;
            }
            else if (SlotInterno.Item.EhBolsa)
            {
                TrocarItens(slotOrigem);
                inventario.EquiparBolsaNoSlot(SlotInterno, indexOrigem);
            }
        }
        else
        {
            TrocarItens(slotOrigem);
        }

        inventario.NotificarMudancaExterna();
    }

    private void MoverDentroDoBanco(SlotUI slotOrigem)
    {
        var banco = ObterBanco();
        if (banco == null || SlotInterno == null) return;

        if (slotOrigem.EhSlotBolsaBanco)
        {
            int indexOrigem = slotOrigem.ObterIndiceBolsa();

            if (SlotInterno.Item == null)
            {
                if (!banco.DesequiparBolsaNoSlot(indexOrigem))
                    return;
                SlotInterno.Item = slotOrigem.SlotInterno.Item;
                SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;
                slotOrigem.SlotInterno.Item = null;
                slotOrigem.SlotInterno.Quantidade = 0;
            }
            else if (SlotInterno.Item.EhBolsa)
            {
                TrocarItens(slotOrigem);
                banco.EquiparBolsaNoSlot(SlotInterno, indexOrigem);
            }
        }
        else
        {
            TrocarItens(slotOrigem);
        }

        banco.NotificarMudancaExterna();
    }

    private void TrocarItens(SlotUI slotOrigem)
    {
        var itemTemp = SlotInterno.Item;
        int quantTemp = SlotInterno.Quantidade;

        SlotInterno.Item = slotOrigem.SlotInterno.Item;
        SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;

        slotOrigem.SlotInterno.Item = itemTemp;
        slotOrigem.SlotInterno.Quantidade = quantTemp;
    }
}
