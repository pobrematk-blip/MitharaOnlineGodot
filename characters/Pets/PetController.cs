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

        _hudLayer = ObterHudLayer();
        if (_hudLayer != null)
        {
            _hudPanel.ZIndex = 210;
            _hudLayer.AddChild(_hudPanel);
        }
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
        GetTree().Root.SizeChanged += OnRootSizeChanged;
        OnEquipamentoAtualizado();
    }

    private void CentralizarHUD()
    {
        if (_hudPanel == null) return;
        Vector2 tela = GetViewport().GetVisibleRect().Size;
        _hudPanel.Position = new Vector2(Mathf.Max(12f, tela.X - 260f), 330f);
    }

    private CanvasLayer ObterHudLayer()
    {
        string[] paths = { "/root/Main/HUD", "/root/main/HUD" };
        foreach (var path in paths)
        {
            var hud = GetNodeOrNull<CanvasLayer>(path);
            if (hud != null)
                return hud;
        }

        var scene = GetTree()?.CurrentScene;
        return scene?.FindChild("HUD", true, false) as CanvasLayer;
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

    public void OnEquipamentoAtualizado()
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
            else if (_petNode.PetID != petId)
            {
                DespawnPet();
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

        _petResource = CarregarPetResource(petId, petNome);

        _petNode = GD.Load<PackedScene>("res://characters/Pets/PetNode.tscn")?.Instantiate<PetNode>();
        if (_petNode == null) return;

        _petNode.PetID = petId;
        _petNode.NomePet = _petResource?.Nome ?? petNome;

        string mobType = NormalizarMobType(_petResource?.AnimPrefix, _petResource?.Nome ?? petNome);

        if (_petResource != null)
        {
            _petNode.AnimPrefix = mobType;
            _petNode.TipoPet = _petResource.Tipo;
            _petNode.Velocidade = _petResource.Speed;
            _petNode.AtaqueRange = _petResource.AttackRange;
            _petNode.AtaqueCooldown = _petResource.AttackCooldown;
            _petNode.AtaqueDano = _petResource.AttackDamage;
            _petNode.ColetaRange = _petResource.ColetaRange;
            _petNode.GuardaRange = _petResource.GuardRange;
        }
        else
        {
            _petNode.AnimPrefix = mobType;
            _petNode.TipoPet = TipoPet.Combate;
            _petNode.Velocidade = 250f;
            _petNode.AtaqueRange = 60f;
            _petNode.AtaqueCooldown = 0.8f;
            _petNode.AtaqueDano = 8;
            _petNode.GuardaRange = 200f;
        }

        var sprite = _petNode.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite != null)
        {
            var frames = MobSpriteFramesBuilder.GetOrBuild(mobType);
            if (frames == null || frames.GetAnimationNames().Length == 0)
            {
                frames = CarregarSpriteFramesFallback(mobType, _petResource?.Nome ?? petNome);
            }
            if ((frames == null || frames.GetAnimationNames().Length == 0) && _petResource != null)
            {
                frames = CriarFramesSimples(_petResource, mobType);
            }
            if (frames != null && frames.GetAnimationNames().Length > 0)
            {
                sprite.SpriteFrames = frames;
                sprite.Visible = true;
                sprite.ZIndex = 1;
                TocarPrimeiraAnimacao(sprite);
            }
            else
            {
                CriarVisualFallback(_petNode, _petResource, petNome);
            }
        }
        else
        {
            CriarVisualFallback(_petNode, _petResource, petNome);
        }

        Vector2 spawnPos = _player.GlobalPosition + new Vector2(
            (float)GD.RandRange(-60, 60),
            (float)GD.RandRange(-60, 60)
        );

        _petNode.Scale = new Vector2(2, 2);
        var parent = GetTree().CurrentScene?.FindChild("World", true, false) as Node;
        if (parent == null)
            parent = _player.GetParent();
        parent?.AddChild(_petNode);
        if (_petNode.GetParent() == null)
            AddChild(_petNode);
        _petNode.GlobalPosition = spawnPos;
        _petNode.DefinirModo(PetMode.Seguir);

        AtualizarHUD();
        _hudPanel.Visible = true;

        GD.Print($"[PET] {petNome} invocado! id={petId} parent={_petNode.GetParent()?.Name} pos={_petNode.GlobalPosition} frames={(sprite?.SpriteFrames?.GetAnimationNames().Length ?? 0)}");
    }

    private static void TocarPrimeiraAnimacao(AnimatedSprite2D sprite)
    {
        if (sprite?.SpriteFrames == null) return;
        var names = sprite.SpriteFrames.GetAnimationNames();
        if (names.Length == 0) return;

        string chosen = "";
        foreach (var name in names)
        {
            string s = name.ToString();
            if (s.Contains("idle") || s.Contains("Idle"))
            {
                chosen = s;
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(chosen))
            chosen = names[0];
        sprite.Play(chosen);
    }

    private static void CriarVisualFallback(PetNode petNode, PetResource resource, string petNome)
    {
        if (petNode == null) return;

        Texture2D tex = resource?.SpriteAtlas ?? resource?.Icone;
        if (tex != null)
        {
            var sprite = new Sprite2D
            {
                Name = "PetFallbackSprite",
                Texture = tex,
                Centered = true,
                Scale = new Vector2(0.5f, 0.5f),
                ZIndex = 2,
            };
            petNode.AddChild(sprite);
        }
        else
        {
            var label = new Label
            {
                Name = "PetFallbackLabel",
                Text = string.IsNullOrWhiteSpace(petNome) ? "Pet" : petNome,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(-24, -36),
                ZIndex = 3,
            };
            label.AddThemeFontSizeOverride("font_size", 10);
            label.AddThemeColorOverride("font_color", Colors.Gold);
            petNode.AddChild(label);
        }
    }

    private PetResource CarregarPetResource(int petId, string petNome)
    {
        string dir = "res://Pets/";
        var dirAccess = DirAccess.Open(dir);
        if (dirAccess != null)
        {
            dirAccess.ListDirBegin();
            string fileName = dirAccess.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                {
                    string path = dir + fileName;
                    var res = ResourceLoader.Load<PetResource>(path);
                    if (res != null && res.PetID == petId)
                    {
                        dirAccess.ListDirEnd();
                        return res;
                    }
                }
                fileName = dirAccess.GetNext();
            }
            dirAccess.ListDirEnd();
        }

        string petFileName = petNome.Replace(" ", "");
        string[] searchPaths = {
            $"res://Pets/{petFileName}.tres",
            $"res://Pets/{petFileName}.res",
        };

        foreach (var p in searchPaths)
        {
            if (ResourceLoader.Exists(p))
            {
                var res = ResourceLoader.Load<PetResource>(p);
                if (res != null) return res;
            }
        }

        GD.PrintErr($"[PET] Recurso do pet ID {petId} ('{petNome}') nao encontrado em res://Pets/.");
        return null;
    }

    private static string NormalizarMobType(string animPrefix, string petNome)
    {
        string value = string.IsNullOrWhiteSpace(animPrefix) ? petNome : animPrefix;
        return value.Trim().TrimEnd('_').Replace(" ", "").ToLowerInvariant();
    }

    private static SpriteFrames CriarFramesSimples(PetResource resource, string prefix)
    {
        var tex = resource.SpriteAtlas ?? resource.Icone;
        if (tex == null)
            return new SpriteFrames();

        var frames = new SpriteFrames();
        string[] names =
        {
            $"{prefix}_idle_down", $"{prefix}_idle_up", $"{prefix}_idle_left", $"{prefix}_idle_right",
            $"{prefix}_walk_down", $"{prefix}_walk_up", $"{prefix}_walk_left", $"{prefix}_walk_right",
            $"{prefix}_attack_down", $"{prefix}_attack_up", $"{prefix}_attack_left", $"{prefix}_attack_right",
        };

        foreach (var name in names)
        {
            frames.AddAnimation(name);
            frames.SetAnimationLoop(name, true);
            frames.SetAnimationSpeed(name, 1f);
            frames.AddFrame(name, tex);
        }

        return frames;
    }

    private SpriteFrames CarregarSpriteFramesFallback(string mobType, string petNome)
    {
        string petFile = petNome.Replace(" ", "");
        string[] framePaths = {
            $"res://characters/Pets/{petFile}PetFrames.tres",
            $"res://characters/Pets/{petFile}PetFrames.res",
        };
        foreach (var p in framePaths)
        {
            if (ResourceLoader.Exists(p))
            {
                var loaded = ResourceLoader.Load<SpriteFrames>(p);
                if (loaded != null && loaded.GetAnimationNames().Length > 0)
                    return loaded;
            }
        }
        string mobScene = $"res://characters/Inimigos/SpriteInimigo/{petFile}.tscn";
        if (ResourceLoader.Exists(mobScene))
        {
            var scene = ResourceLoader.Load<PackedScene>(mobScene);
            if (scene != null)
            {
                var temp = scene.Instantiate();
                var animSprite = temp?.FindChild("AnimatedSprite2D", true, false) as AnimatedSprite2D;
                if (animSprite?.SpriteFrames != null)
                {
                    var original = animSprite.SpriteFrames;
                    var prefix = _petResource?.AnimPrefix ?? mobType;
                    if (MontarFramesDoMob(original, prefix, out var novo))
                    {
                        temp.QueueFree();
                        return novo;
                    }
                }
                temp?.QueueFree();
            }
        }
        return new SpriteFrames();
    }

    private static bool MontarFramesDoMob(SpriteFrames original, string prefix, out SpriteFrames novo)
    {
        novo = new SpriteFrames();
        string[] anims = original.GetAnimationNames();
        bool achou = false;
        foreach (var oldName in anims)
        {
            int idx = oldName.IndexOf('_');
            if (idx < 0) continue;
            string newName = prefix + oldName.Substring(idx);
            novo.AddAnimation(newName);
            novo.SetAnimationLoop(newName, original.GetAnimationLoop(oldName));
            novo.SetAnimationSpeed(newName, original.GetAnimationSpeed(oldName));
            int frameCount = original.GetFrameCount(oldName);
            for (int f = 0; f < frameCount; f++)
            {
                var tex = original.GetFrameTexture(oldName, f);
                float duration = original.GetFrameDuration(oldName, f);
                novo.AddFrame(newName, tex, duration);
            }
            achou = true;
        }
        return achou;
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

    private void OnRootSizeChanged()
    {
        CallDeferred(MethodName.CentralizarHUD);
    }

    public override void _ExitTree()
    {
        GetTree().Root.SizeChanged -= OnRootSizeChanged;
        if (_hudPanel != null && IsInstanceValid(_hudPanel))
        {
            _hudPanel.QueueFree();
            _hudPanel = null;
        }
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado -= OnEquipamentoAtualizado;
    }
}
