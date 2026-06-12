using Godot;
using System;

public partial class PetController : Node
{
    private Player _player;
    private PetNode _petNode;
    private PetResource _petResource;
    private EquipamentoComponent _equipamento;
    private Panel _hudPanel;
    private Panel _hudTitleBar;
    private Label _hudNameLabel;
    private Button _btnSeguir;
    private Button _btnGuarda;
    private Button _btnAtacar;
    private HBoxContainer _coletaRow;
    private CheckBox _chkColeta;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private CanvasLayer _hudLayer;

    public bool TemPetAtivo => _petNode != null && IsInstanceValid(_petNode) && _petNode.Ativo;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _equipamento = _player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado += OnEquipamentoAtualizado;

        _hudPanel = new Panel();
        _hudPanel.Name = "PetHUD";
        _hudPanel.CustomMinimumSize = new Vector2(200, 0);
        _hudPanel.Visible = false;

        _hudLayer = GetNodeOrNull<CanvasLayer>("/root/main/HUD");
        if (_hudLayer != null)
            _hudLayer.AddChild(_hudPanel);
        else
            AddChild(_hudPanel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 4);
        _hudPanel.AddChild(vbox);

        _hudTitleBar = new Panel();
        _hudTitleBar.CustomMinimumSize = new Vector2(0, 24);
        _hudTitleBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _hudTitleBar.MouseDefaultCursorShape = Control.CursorShape.Move;
        _hudTitleBar.GuiInput += OnTitleBarGuiInput;
        vbox.AddChild(_hudTitleBar);

        var titleHBox = new HBoxContainer();
        titleHBox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 2);
        _hudTitleBar.AddChild(titleHBox);

        _hudNameLabel = new Label();
        _hudNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _hudNameLabel.AddThemeFontSizeOverride("font_size", 12);
        titleHBox.AddChild(_hudNameLabel);

        var btnFechar = new Button();
        btnFechar.Text = "X";
        btnFechar.CustomMinimumSize = new Vector2(20, 20);
        btnFechar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        btnFechar.Pressed += () => _hudPanel.Visible = false;
        titleHBox.AddChild(btnFechar);

        var modeHBox = new HBoxContainer();
        modeHBox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(modeHBox);

        _btnSeguir = new Button();
        _btnSeguir.Text = "Seguir";
        _btnSeguir.Pressed += () => DefinirModo(PetMode.Seguir);
        modeHBox.AddChild(_btnSeguir);

        _btnGuarda = new Button();
        _btnGuarda.Text = "Guarda";
        _btnGuarda.Pressed += () => DefinirModo(PetMode.Guarda);
        modeHBox.AddChild(_btnGuarda);

        _btnAtacar = new Button();
        _btnAtacar.Text = "Atacar";
        _btnAtacar.Pressed += () => DefinirModo(PetMode.Atacar);
        modeHBox.AddChild(_btnAtacar);

        _coletaRow = new HBoxContainer();
        _coletaRow.Alignment = BoxContainer.AlignmentMode.Center;
        _coletaRow.Visible = false;
        vbox.AddChild(_coletaRow);

        var coletaLabel = new Label();
        coletaLabel.Text = "Coleta";
        _coletaRow.AddChild(coletaLabel);

        _chkColeta = new CheckBox();
        _chkColeta.ButtonPressed = true;
        _chkColeta.Toggled += OnColetaToggled;
        _coletaRow.AddChild(_chkColeta);

        CallDeferred(MethodName.CentralizarHUD);
        GetTree().Root.SizeChanged += () => CallDeferred(MethodName.CentralizarHUD);
        OnEquipamentoAtualizado();
    }

    private void CentralizarHUD()
    {
        if (_hudPanel == null) return;
        Vector2 tela = GetViewport().GetVisibleRect().Size;
        _hudPanel.Position = new Vector2(tela.X - _hudPanel.Size.X - 80, 120);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _hudPanel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void OnEquipamentoAtualizado()
    {
        if (_equipamento == null) return;

        var slotPet = _equipamento.ObterSlot(TipoEquipamento.Pet);
        if (slotPet != null && slotPet.Item != null)
        {
            string petNome = slotPet.Item.Nome;
            int petId = slotPet.Item.ItemID - 200;
            if (petId <= 0) petId = 1;

            if (_petNode == null || !IsInstanceValid(_petNode))
            {
                SpawnPet(petId, petNome);
            }
        }
        else
        {
            DespawnPet();
        }
    }

    private void SpawnPet(int petId, string petNome)
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        string petFileName = petNome.Replace(" ", "");
        string[] searchPaths = {
            $"res://Pets/{petFileName}.tres",
            $"res://Pets/{petFileName}.res",
        };

        _petResource = null;
        foreach (var p in searchPaths)
        {
            if (ResourceLoader.Exists(p))
            {
                _petResource = ResourceLoader.Load<PetResource>(p);
                if (_petResource != null) break;
            }
        }

        _petNode = GD.Load<PackedScene>("res://characters/Pets/PetNode.tscn")?.Instantiate<PetNode>();
        if (_petNode == null) return;

        _petNode.PetID = petId;
        _petNode.NomePet = petNome;

        if (_petResource != null)
        {
            _petNode.AnimPrefix = _petResource.AnimPrefix;
            _petNode.TipoPet = _petResource.Tipo;
            _petNode.Velocidade = _petResource.Speed;
            _petNode.AtaqueRange = _petResource.AttackRange;
            _petNode.AtaqueCooldown = _petResource.AttackCooldown;
            _petNode.AtaqueDano = _petResource.AttackDamage;
            _petNode.ColetaRange = _petResource.ColetaRange;
            _petNode.GuardaRange = _petResource.GuardRange;
        }

        _petNode.GlobalPosition = _player.GlobalPosition + new Vector2(
            (float)GD.RandRange(-60, 60),
            (float)GD.RandRange(-60, 60)
        );

        _petNode.Scale = new Vector2(2, 2);
        _player.GetParent().AddChild(_petNode);
        _petNode.DefinirModo(PetMode.Seguir);

        AtualizarHUD();
        _hudPanel.Visible = true;

        GD.Print($"[PET] {petNome} invocado!");
    }

    private void DespawnPet()
    {
        if (_petNode != null && IsInstanceValid(_petNode))
        {
            _petNode.Ativo = false;
            _petNode.QueueFree();
            _petNode = null;
        }
        _petResource = null;
        _hudPanel.Visible = false;
        GD.Print("[PET] Pet removido.");
    }

    private void AtualizarHUD()
    {
        if (_petNode == null) return;

        _hudNameLabel.Text = _petNode.NomePet;

        bool isLoot = _petNode.TipoPet == TipoPet.Loot;
        _coletaRow.Visible = isLoot;

        DefinirModo(_petNode.ModoAtual);
    }

    private void OnColetaToggled(bool pressed)
    {
        if (_petNode == null) return;

        _petNode.ColetaAtiva = pressed;
        GD.Print($"[PET] Coleta {(pressed ? "ativada" : "desativada")}");
    }

    public void DefinirModo(PetMode modo)
    {
        if (_petNode == null || !IsInstanceValid(_petNode)) return;
        _petNode.DefinirModo(modo);

        _btnSeguir.Modulate = modo == PetMode.Seguir ? Colors.Yellow : Colors.White;
        _btnGuarda.Modulate = modo == PetMode.Guarda ? Colors.Yellow : Colors.White;
        _btnAtacar.Modulate = modo == PetMode.Atacar ? Colors.Yellow : Colors.White;

        GD.Print($"[PET] Modo alterado para: {modo}");
    }

    public override void _ExitTree()
    {
        if (_hudPanel != null && IsInstanceValid(_hudPanel))
        {
            _hudPanel.QueueFree();
            _hudPanel = null;
        }
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado -= OnEquipamentoAtualizado;
    }
}
