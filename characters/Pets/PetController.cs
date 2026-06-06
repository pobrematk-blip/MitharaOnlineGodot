using Godot;
using System;

public partial class PetController : Node
{
    private Player _player;
    private PetNode _petNode;
    private EquipamentoComponent _equipamento;
    private Control _modeUIPanel;
    private Button _btnSeguir;
    private Button _btnGuarda;
    private Button _btnAtacar;
    private Label _petNameLabel;

    public bool TemPetAtivo => _petNode != null && IsInstanceValid(_petNode) && _petNode.Ativo;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _equipamento = _player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado += OnEquipamentoAtualizado;

        CriarModeUI();
        _modeUIPanel.Visible = false;

        OnEquipamentoAtualizado();
    }

    private void CriarModeUI()
    {
        _modeUIPanel = new Control();
        _modeUIPanel.Name = "PetModeUI";
        _modeUIPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _modeUIPanel.OffsetTop = -80;
        _modeUIPanel.OffsetBottom = 0;

        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _modeUIPanel.AddChild(hbox);

        _petNameLabel = new Label();
        _petNameLabel.CustomMinimumSize = new Vector2(100, 30);
        _petNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hbox.AddChild(_petNameLabel);

        _btnSeguir = new Button();
        _btnSeguir.Text = "Seguir";
        _btnSeguir.Pressed += () => DefinirModo(PetMode.Seguir);
        hbox.AddChild(_btnSeguir);

        _btnGuarda = new Button();
        _btnGuarda.Text = "Guarda";
        _btnGuarda.Pressed += () => DefinirModo(PetMode.Guarda);
        hbox.AddChild(_btnGuarda);

        _btnAtacar = new Button();
        _btnAtacar.Text = "Atacar";
        _btnAtacar.Pressed += () => DefinirModo(PetMode.Atacar);
        hbox.AddChild(_btnAtacar);

        AddChild(_modeUIPanel);
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

        PetResource petRes = null;
        foreach (var p in searchPaths)
        {
            if (ResourceLoader.Exists(p))
            {
                petRes = ResourceLoader.Load<PetResource>(p);
                if (petRes != null) break;
            }
        }

        _petNode = GD.Load<PackedScene>("res://characters/Pets/PetNode.tscn")?.Instantiate<PetNode>();
        if (_petNode == null) return;

        _petNode.PetID = petId;
        _petNode.NomePet = petNome;

        if (petRes != null)
        {
            _petNode.TipoPet = petRes.Tipo;
            _petNode.Velocidade = petRes.Speed;
            _petNode.AtaqueRange = petRes.AttackRange;
            _petNode.AtaqueCooldown = petRes.AttackCooldown;
            _petNode.AtaqueDano = petRes.AttackDamage;
            _petNode.ColetaRange = petRes.ColetaRange;
            _petNode.GuardaRange = petRes.GuardRange;
        }

        _petNode.GlobalPosition = _player.GlobalPosition + new Vector2(
            (float)GD.RandRange(-60, 60),
            (float)GD.RandRange(-60, 60)
        );

        _player.GetParent().AddChild(_petNode);
        _petNode.DefinirModo(PetMode.Seguir);
        _modeUIPanel.Visible = true;
        _petNameLabel.Text = petNome;

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
        _modeUIPanel.Visible = false;
        GD.Print("[PET] Pet removido.");
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
}
