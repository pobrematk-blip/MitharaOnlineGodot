using Godot;
using System;
using System.Text;

public partial class PetController : Node
{
    private Player _player;
    private PetNode _petNode;
    private PetResource _petResource;
    private EquipamentoComponent _equipamento;
    private Panel _hudPanel;
    private Panel _hudTitleBar;
    private TextureRect _hudPetIcon;
    private Label _hudNameLabel;
    private Label _hudModeLabel;
    private Button _btnSeguir;
    private Button _btnGuarda;
    private Button _btnAtacar;
    private HBoxContainer _coletaRow;
    private CheckBox _chkColeta;
    private PetCollarSlot _coleiraSlot;
    private TextureRect _coleiraIcon;
    private Label _coleiraStatusLabel;
    private Timer _coleiraTimer;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private CanvasLayer _hudLayer;

    private static readonly Color HudBg = new(0.015f, 0.018f, 0.026f, 0.92f);
    private static readonly Color HudTitleBg = new(0.025f, 0.032f, 0.046f, 0.96f);
    private static readonly Color HudBorder = new(0.20f, 0.60f, 0.86f, 0.95f);
    private static readonly Color HudBorderHot = new(1.0f, 0.84f, 0.18f, 1f);
    private static readonly Color HudText = new(0.86f, 0.91f, 1f, 1f);

    public bool TemPetAtivo => _petNode != null && IsInstanceValid(_petNode) && _petNode.Ativo;

    public bool PetAtivoEh(int petId)
    {
        return TemPetAtivo && _petNode.PetID == petId;
    }

    public void SetPetVisualAlpha(float alpha)
    {
        if (_petNode == null || !IsInstanceValid(_petNode))
            return;

        _petNode.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }

    public void InvocarPet(int petId, string petNome)
    {
        if (petId <= 0)
        {
            GD.PrintErr($"[PET] ID invalido para invocar pet: {petId}");
            return;
        }

        if (PetAtivoEh(petId))
        {
            if (_hudPanel != null)
                _hudPanel.Visible = true;
            return;
        }

        DespawnPet();
        SpawnPet(petId, petNome);
    }

    public void RemoverPetAtivo()
    {
        DespawnPet();
    }

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _equipamento = _player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado += OnEquipamentoAtualizado;

        _hudPanel = new Panel();
        _hudPanel.Name = "PetHUD";
        _hudPanel.CustomMinimumSize = new Vector2(306, 190);
        _hudPanel.Size = _hudPanel.CustomMinimumSize;
        _hudPanel.Visible = false;
        _hudPanel.AddThemeStyleboxOverride("panel", CriarStyle(HudBg, HudBorder, 7, 1));
        _hudPanel.MouseFilter = Control.MouseFilterEnum.Stop;

        _hudLayer = ObterHudLayer();
        if (_hudLayer != null)
        {
            _hudPanel.ZIndex = 210;
            _hudLayer.AddChild(_hudPanel);
        }
        else
            AddChild(_hudPanel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 6);
        vbox.AddThemeConstantOverride("separation", 6);
        _hudPanel.AddChild(vbox);

        _hudTitleBar = new Panel();
        _hudTitleBar.CustomMinimumSize = new Vector2(0, 26);
        _hudTitleBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _hudTitleBar.MouseDefaultCursorShape = Control.CursorShape.Move;
        _hudTitleBar.GuiInput += OnTitleBarGuiInput;
        _hudTitleBar.AddThemeStyleboxOverride("panel", CriarStyle(HudTitleBg, HudBorder, 6, 1));
        vbox.AddChild(_hudTitleBar);

        var titleHBox = new HBoxContainer();
        titleHBox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 4);
        _hudTitleBar.AddChild(titleHBox);

        _hudNameLabel = new Label();
        _hudNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _hudNameLabel.VerticalAlignment = VerticalAlignment.Center;
        _hudNameLabel.AddThemeFontSizeOverride("font_size", 13);
        _hudNameLabel.AddThemeColorOverride("font_color", HudText);
        titleHBox.AddChild(_hudNameLabel);

        var btnFechar = new Button();
        btnFechar.Text = "X";
        btnFechar.CustomMinimumSize = new Vector2(20, 20);
        btnFechar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        AplicarBotaoPequeno(btnFechar);
        btnFechar.Pressed += () => _hudPanel.Visible = false;
        titleHBox.AddChild(btnFechar);

        var bodyHBox = new HBoxContainer();
        bodyHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bodyHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        bodyHBox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(bodyHBox);

        var iconPanel = new Panel();
        iconPanel.CustomMinimumSize = new Vector2(82, 82);
        iconPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        iconPanel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        iconPanel.AddThemeStyleboxOverride("panel", CriarStyle(new Color(0.01f, 0.012f, 0.018f, 0.95f), HudBorder, 6, 1));
        bodyHBox.AddChild(iconPanel);

        _hudPetIcon = new TextureRect();
        _hudPetIcon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 6);
        _hudPetIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _hudPetIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconPanel.AddChild(_hudPetIcon);

        var rightVBox = new VBoxContainer();
        rightVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightVBox.AddThemeConstantOverride("separation", 5);
        bodyHBox.AddChild(rightVBox);

        var modeHBox = new HBoxContainer();
        modeHBox.Alignment = BoxContainer.AlignmentMode.Center;
        modeHBox.AddThemeConstantOverride("separation", 4);
        rightVBox.AddChild(modeHBox);

        _btnSeguir = new Button();
        _btnSeguir.Text = "Seguir";
        _btnSeguir.CustomMinimumSize = new Vector2(58, 28);
        AplicarBotaoModo(_btnSeguir);
        _btnSeguir.Pressed += () => DefinirModo(PetMode.Seguir);
        modeHBox.AddChild(_btnSeguir);

        _btnGuarda = new Button();
        _btnGuarda.Text = "Parado";
        _btnGuarda.CustomMinimumSize = new Vector2(58, 28);
        AplicarBotaoModo(_btnGuarda);
        _btnGuarda.Pressed += () => DefinirModo(PetMode.Guarda);
        modeHBox.AddChild(_btnGuarda);

        _btnAtacar = new Button();
        _btnAtacar.Text = "Atacar";
        _btnAtacar.CustomMinimumSize = new Vector2(58, 28);
        AplicarBotaoModo(_btnAtacar);
        _btnAtacar.Pressed += () => DefinirModo(PetMode.Atacar);
        modeHBox.AddChild(_btnAtacar);

        _hudModeLabel = new Label();
        _hudModeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _hudModeLabel.AddThemeFontSizeOverride("font_size", 10);
        _hudModeLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.84f, 0.26f, 1f));
        rightVBox.AddChild(_hudModeLabel);

        _coletaRow = new HBoxContainer();
        _coletaRow.Alignment = BoxContainer.AlignmentMode.Center;
        _coletaRow.Visible = false;
        rightVBox.AddChild(_coletaRow);

        var coletaLabel = new Label();
        coletaLabel.Text = "Coleta";
        coletaLabel.AddThemeFontSizeOverride("font_size", 11);
        coletaLabel.AddThemeColorOverride("font_color", HudText);
        _coletaRow.AddChild(coletaLabel);

        _chkColeta = new CheckBox();
        _chkColeta.ButtonPressed = true;
        _chkColeta.Toggled += OnColetaToggled;
        _coletaRow.AddChild(_chkColeta);

        var coleiraPanel = new Panel();
        coleiraPanel.CustomMinimumSize = new Vector2(0, 56);
        coleiraPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        coleiraPanel.AddThemeStyleboxOverride("panel", CriarStyle(new Color(0.012f, 0.015f, 0.022f, 0.92f), new Color(0.12f, 0.30f, 0.42f, 0.95f), 5, 1));
        vbox.AddChild(coleiraPanel);

        var coleiraRow = new HBoxContainer();
        coleiraRow.Alignment = BoxContainer.AlignmentMode.Center;
        coleiraRow.AddThemeConstantOverride("separation", 10);
        coleiraRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 5);
        coleiraPanel.AddChild(coleiraRow);

        _coleiraSlot = new PetCollarSlot();
        _coleiraSlot.CustomMinimumSize = new Vector2(48, 48);
        _coleiraSlot.AddThemeStyleboxOverride("panel", CriarStyle(new Color(0.01f, 0.012f, 0.018f, 0.95f), HudBorder, 5, 1));
        _coleiraSlot.TooltipText = "Arraste uma Coleira de Pet para ativar a coleta automatica.";
        _coleiraSlot.OnPetCollarDropped += OnColeiraDropped;
        coleiraRow.AddChild(_coleiraSlot);

        _coleiraIcon = new TextureRect();
        _coleiraIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _coleiraIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _coleiraIcon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, margin: 4);
        _coleiraSlot.AddChild(_coleiraIcon);

        _coleiraStatusLabel = new Label();
        _coleiraStatusLabel.Text = "Sem coleira";
        _coleiraStatusLabel.VerticalAlignment = VerticalAlignment.Center;
        _coleiraStatusLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _coleiraStatusLabel.AddThemeFontSizeOverride("font_size", 11);
        _coleiraStatusLabel.AddThemeColorOverride("font_color", HudText);
        _coleiraStatusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        coleiraRow.AddChild(_coleiraStatusLabel);

        _coleiraTimer = new Timer();
        _coleiraTimer.WaitTime = 1.0;
        _coleiraTimer.OneShot = false;
        _coleiraTimer.Timeout += AtualizarColeiraHUD;
        AddChild(_coleiraTimer);
        _coleiraTimer.Start();

        CallDeferred(MethodName.CentralizarHUD);
        GetTree().Root.SizeChanged += OnRootSizeChanged;
        OnEquipamentoAtualizado();
    }

    private void CentralizarHUD()
    {
        if (_hudPanel == null) return;
        Vector2 tela = GetViewport().GetVisibleRect().Size;
        _hudPanel.Size = _hudPanel.CustomMinimumSize;
        _hudPanel.Position = new Vector2(Mathf.Max(12f, tela.X - 318f), 330f);
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
            // Equipamento pode chegar do servidor em etapas. Nao remova o pet em
            // refresh temporario sem slot; ele so deve sumir ao morrer ou quando
            // houver uma remocao explicita.
            GD.Print("[PET] Slot de pet vazio no refresh; pet ativo mantido.");
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
        _petNode.DefinirDono(_player);

        string mobType = NormalizarMobType(_petResource?.AnimPrefix, _petResource?.Nome ?? petNome);

        if (_petResource != null)
        {
            var petEntry = ObterEntradaPet(petId);
            bool isBossPet = petEntry?.IsBossPet == true;
            int petLevel = petEntry?.Level ?? Mathf.Max(1, _petResource.Level);
            _petNode.AnimPrefix = mobType;
            _petNode.TipoPet = _petResource.Tipo;
            _petNode.Velocidade = _petResource.Speed;
            _petNode.AtaqueRange = Mathf.Clamp(_petResource.AttackRange, 1f, PetNode.OneTileAttackRange);
            _petNode.AtaqueCooldown = _petResource.AttackCooldown;
            _petNode.AtaqueDano = Mathf.Max(1, Mathf.CeilToInt(_petResource.AttackDamage * (isBossPet ? 0.30f : 0.20f)));
            _petNode.ColetaRange = PetNode.PetLootRange;
            _petNode.GuardaRange = _petResource.GuardRange;
            _petNode.ConfigurarAtributos(CalcularPetHpVisual(_petResource.HP, petLevel, isBossPet), _petResource.Defense);
        }
        else
        {
            _petNode.AnimPrefix = mobType;
            _petNode.TipoPet = TipoPet.Combate;
            _petNode.Velocidade = 250f;
            _petNode.AtaqueRange = PetNode.OneTileAttackRange;
            _petNode.AtaqueCooldown = 0.8f;
            _petNode.AtaqueDano = 4;
            _petNode.ColetaRange = PetNode.PetLootRange;
            _petNode.GuardaRange = 200f;
            _petNode.ConfigurarAtributos(240, 15);
        }

        var sprite = _petNode.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite != null)
        {
            var frames = CarregarSpriteFramesFallback(mobType, _petResource?.Nome ?? petNome);
            if (frames == null || frames.GetAnimationNames().Length == 0)
            {
                frames = MobSpriteFramesBuilder.GetOrBuild(mobType);
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
                RemoverVisualFallback(_petNode);
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

        _petNode.Scale = new Vector2(1.4f, 1.4f);
        var parent = _player.GetParent();
        if (parent == null)
            parent = GetTree().CurrentScene?.FindChild("World", true, false) as Node;
        parent?.AddChild(_petNode);
        if (_petNode.GetParent() == null)
            AddChild(_petNode);
        _petNode.GlobalPosition = spawnPos;
        _petNode.Visible = true;
        _petNode.ZIndex = _player.ZIndex + 1;
        _petNode.DefinirModo(PetMode.Seguir);

        AtualizarHUD();
        AtualizarColeiraHUD();
        NotificarServidorPetInvocado(petId, _petNode.NomePet, mobType);
        _hudPanel.Visible = true;

        GD.Print($"[PET] {petNome} invocado! id={petId} parent={_petNode.GetParent()?.Name} pos={_petNode.GlobalPosition} frames={(sprite?.SpriteFrames?.GetAnimationNames().Length ?? 0)}");
    }

    private PetColecaoComponent.PetColecaoEntry ObterEntradaPet(int petId)
    {
        var colecao = _player?.FindChild("PetColecaoComponent", true, false) as PetColecaoComponent;
        if (colecao == null)
            return null;

        foreach (var pet in colecao.GetPets())
        {
            if (pet.PetID == petId)
                return pet;
        }

        return null;
    }

    private static int CalcularPetHpVisual(int baseHp, int level, bool isBossPet)
    {
        return Mathf.Max(40, baseHp + Mathf.Max(0, level - 1) * (isBossPet ? 10 : 7));
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
        RemoverVisualFallback(petNode);

        Texture2D tex = resource?.Icone ?? resource?.SpriteAtlas;
        if (tex != null)
        {
            var sprite = new Sprite2D
            {
                Name = "PetFallbackSprite",
                Texture = tex,
                Centered = true,
                Scale = new Vector2(1.2f, 1.2f),
                ZIndex = 10,
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

    private static void RemoverVisualFallback(PetNode petNode)
    {
        if (petNode == null)
            return;

        var fallbackSprite = petNode.GetNodeOrNull<Node>("PetFallbackSprite");
        fallbackSprite?.QueueFree();
        var fallbackLabel = petNode.GetNodeOrNull<Node>("PetFallbackLabel");
        fallbackLabel?.QueueFree();
    }

    private PetResource CarregarPetResource(int petId, string petNome)
    {
        string dir = "res://Pets/";
        string nomeNormalizado = NormalizarTexto(petNome);
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
                    bool mesmoId = res != null && res.PetID == petId;
                    bool mesmoNome = res != null
                        && !string.IsNullOrWhiteSpace(nomeNormalizado)
                        && (NormalizarTexto(res.Nome) == nomeNormalizado
                            || NormalizarTexto(System.IO.Path.GetFileNameWithoutExtension(fileName)) == nomeNormalizado);
                    if (mesmoId || mesmoNome)
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

    private static string NormalizarTexto(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
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

        foreach (string mobScene in ObterCenasPossiveisDoPet(mobType, petFile))
        {
            if (!ResourceLoader.Exists(mobScene))
                continue;

            var scene = ResourceLoader.Load<PackedScene>(mobScene);
            if (scene != null)
            {
                var temp = scene.Instantiate();
                var animSprite = temp?.FindChild("AnimatedSprite2D", true, false) as AnimatedSprite2D;
                if (animSprite?.SpriteFrames != null)
                {
                    var original = animSprite.SpriteFrames;
                    if (original.GetAnimationNames().Length > 0)
                    {
                        temp.QueueFree();
                        return original.Duplicate(true) as SpriteFrames ?? original;
                    }
                }
                temp?.QueueFree();
            }
        }
        return new SpriteFrames();
    }

    private static string[] ObterCenasPossiveisDoPet(string mobType, string petFile)
    {
        return mobType switch
        {
            "slime" => new[]
            {
                "res://characters/Inimigos/SpriteInimigo/Slime.tscn",
                $"res://characters/Inimigos/SpriteInimigo/{petFile}.tscn",
            },
            "cogumelo" => new[]
            {
                "res://characters/Inimigos/SpriteInimigo/Cogumelo.tscn",
                $"res://characters/Inimigos/SpriteInimigo/{petFile}.tscn",
            },
            "plantacarnivora" => new[]
            {
                "res://characters/Inimigos/SpriteInimigo/PlanTaCarnivora.tscn",
                "res://characters/Inimigos/SpriteInimigo/PlantaCarnivora.tscn",
                $"res://characters/Inimigos/SpriteInimigo/{petFile}.tscn",
            },
            _ => new[]
            {
                $"res://characters/Inimigos/SpriteInimigo/{petFile}.tscn",
            },
        };
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
            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet?.IsConnected == true)
                gameNet.SendPetSummon(0, "", "");
        }
        _petResource = null;
        _hudPanel.Visible = false;
        GD.Print("[PET] Pet removido.");
    }

    private void AtualizarHUD()
    {
        if (_petNode == null) return;

        _hudNameLabel.Text = _petNode.NomePet;
        if (_hudPetIcon != null)
        {
            _hudPetIcon.Texture = ObterIconePet(_petResource);
            _hudPetIcon.TooltipText = _hudPetIcon.Texture == null
                ? "Icone do pet nao encontrado."
                : _petNode.NomePet;
        }
        AtualizarTooltipPet();

        _coletaRow.Visible = true;
        AtualizarColeiraHUD();

        DefinirModo(_petNode.ModoAtual);
    }

    private void OnColetaToggled(bool pressed)
    {
        if (_petNode == null) return;

        bool podeColetar = ColeiraAtiva();
        _petNode.ColetaAtiva = pressed && podeColetar;
        if (pressed && !podeColetar)
            GD.Print("[PET] Coleta bloqueada: equipe/use uma Coleira de Pet ativa.");
        GD.Print($"[PET] Coleta {(pressed ? "ativada" : "desativada")}");
    }

    public void DefinirModo(PetMode modo)
    {
        if (_petNode == null || !IsInstanceValid(_petNode)) return;
        _petNode.DefinirModo(modo);

        _btnSeguir.Modulate = modo == PetMode.Seguir ? Colors.Yellow : Colors.White;
        _btnGuarda.Modulate = modo == PetMode.Guarda ? Colors.Yellow : Colors.White;
        _btnAtacar.Modulate = modo == PetMode.Atacar ? Colors.Yellow : Colors.White;
        if (_hudModeLabel != null)
            _hudModeLabel.Text = $"Modo: {NomeModo(modo)}";
        AtualizarTooltipPet();

        GD.Print($"[PET] Modo alterado para: {modo}");
    }

    private void OnColeiraDropped(int inventorySlot)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected)
        {
            GD.PrintErr("[PET] Nao foi possivel usar a coleira: sem conexao com o servidor.");
            return;
        }

        gameNet.SendUseItem(inventorySlot);
    }

    private bool ColeiraAtiva()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        return gameNet?.HasActivePetCollar == true;
    }

    private void AtualizarColeiraHUD()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        long expiry = gameNet?.PetCollarExpiryUnix ?? 0L;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool ativa = expiry > now;

        if (_coleiraIcon != null && _coleiraIcon.Texture == null)
        {
            var itemDb = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase")
                ?? GetNodeOrNull<GameNetwork>("/root/GameNetwork")?.ItemDB;
            _coleiraIcon.Texture = itemDb?.GetItem(PetCollarSlot.ColeiraPetItemId)?.Icone;
        }

        if (_coleiraIcon != null)
            _coleiraIcon.Modulate = ativa ? Colors.White : new Color(0.35f, 0.35f, 0.35f, 0.75f);

        if (_coleiraStatusLabel != null)
        {
            if (!ativa)
                _coleiraStatusLabel.Text = "Sem coleira";
            else
            {
                var remaining = TimeSpan.FromSeconds(expiry - now);
                _coleiraStatusLabel.Text = remaining.TotalDays >= 1
                    ? $"{Mathf.CeilToInt((float)remaining.TotalDays)}d"
                    : $"{Mathf.Max(1, Mathf.CeilToInt((float)remaining.TotalHours))}h";
            }
        }

        if (_chkColeta != null)
            _chkColeta.Disabled = !ativa;

        if (_petNode != null && IsInstanceValid(_petNode))
            _petNode.ColetaAtiva = ativa && (_chkColeta?.ButtonPressed ?? true);

        AtualizarTooltipPet();
    }

    private void AtualizarTooltipPet()
    {
        if (_hudPanel == null)
            return;

        if (_petNode == null || !IsInstanceValid(_petNode))
        {
            _hudPanel.TooltipText = "";
            return;
        }

        int level = _petResource?.Level ?? 1;
        int mana = _petResource?.Mana ?? 0;
        int forca = _petResource?.Forca ?? 0;
        int agilidade = _petResource?.Agilidade ?? 0;
        int destreza = _petResource?.Destreza ?? 0;
        int inteligencia = _petResource?.Inteligencia ?? 0;
        bool coleta = _petNode.ColetaAtiva;
        string tipo = (_petResource?.Tipo ?? _petNode.TipoPet) == TipoPet.Combate ? "Combate" : "Coleta";

        _hudPanel.TooltipText =
            $"{_petNode.NomePet} | Nv. {level}\n" +
            $"Tipo: {tipo} | Modo: {NomeModo(_petNode.ModoAtual)}\n" +
            $"Vida: {_petNode.VidaAtual}/{_petNode.VidaMaxima} | Mana: {mana}\n" +
            $"Dano: {_petNode.AtaqueDano} | Defesa: {_petNode.Defesa}\n" +
            $"Forca: {forca} | Agilidade: {agilidade} | Destreza: {destreza} | Inteligencia: {inteligencia}\n" +
            $"Velocidade: {_petNode.Velocidade:0} | Alcance: {_petNode.AtaqueRange:0}\n" +
            $"Raio de coleta: {PetNode.PetLootRange / PetNode.TileSize:0} tiles\n" +
            $"Coleta automatica: {(coleta ? "Ativa" : "Inativa")}";
    }

    private static string NomeModo(PetMode modo)
    {
        return modo switch
        {
            PetMode.Seguir => "Seguir",
            PetMode.Guarda => "Parado",
            PetMode.Atacar => "Atacar",
            _ => modo.ToString()
        };
    }

    private static Texture2D ObterIconePet(PetResource resource)
    {
        if (resource == null)
            return null;
        if (resource.Icone != null)
            return resource.Icone;
        if (resource.SpriteAtlas == null)
            return null;

        float w = Mathf.Min(64f, resource.SpriteAtlas.GetWidth());
        float h = Mathf.Min(64f, resource.SpriteAtlas.GetHeight());
        return new AtlasTexture
        {
            Atlas = resource.SpriteAtlas,
            Region = new Rect2(0, 0, w, h)
        };
    }

    private static void AplicarBotaoModo(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 10);
        button.AddThemeStyleboxOverride("normal", CriarStyle(new Color(0.025f, 0.032f, 0.046f, 0.94f), new Color(0.18f, 0.34f, 0.48f, 0.95f), 5, 1));
        button.AddThemeStyleboxOverride("hover", CriarStyle(new Color(0.05f, 0.07f, 0.095f, 0.98f), HudBorder, 5, 1));
        button.AddThemeStyleboxOverride("pressed", CriarStyle(new Color(0.08f, 0.075f, 0.035f, 1f), HudBorderHot, 5, 1));
    }

    private static void AplicarBotaoPequeno(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeStyleboxOverride("normal", CriarStyle(new Color(0.10f, 0.02f, 0.025f, 0.95f), new Color(0.55f, 0.12f, 0.16f, 0.9f), 4, 1));
        button.AddThemeStyleboxOverride("hover", CriarStyle(new Color(0.20f, 0.035f, 0.045f, 1f), new Color(1f, 0.25f, 0.30f, 1f), 4, 1));
    }

    private static StyleBoxFlat CriarStyle(Color bg, Color border, int radius, int borderWidth)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
        };
        return style;
    }

    private void NotificarServidorPetInvocado(int petId, string petNome, string animPrefix)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet?.IsConnected == true)
            gameNet.SendPetSummon(petId, petNome, animPrefix);
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
