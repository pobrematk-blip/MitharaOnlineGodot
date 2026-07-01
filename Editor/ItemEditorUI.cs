using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ItemEditorUI : Control
{
    private Panel _panel;
    private Tree _itemTree;
    private LineEdit _itemSearch;
    private Label _previewStatus;

    private SpinBox _itemIdSpin;
    private LineEdit _nomeEdit;
    private TextEdit _descricaoEdit;
    private TextureRect _iconePreview;
    private TextureRect _spritesheetPreview;
    private LineEdit _classesEdit;
    private LineEdit _animacaoUsarEdit;
    private LineEdit _animacaoEquipadoEdit;
    private CheckBox _acumulavelCheck;
    private SpinBox _qtdMaxSpin;
    private CheckBox _ehDuasMaosCheck;
    private CheckBox _ehBolsaCheck;
    private SpinBox _slotsAdicionaisSpin;
    private OptionButton _tipoDropdown;
    private OptionButton _subcategoriaDropdown;
    private OptionButton _categoriaPesoDropdown;
    private SpinBox _nivelReqSpin;
    private SpinBox _valorSpin;
    private SpinBox _forcaMinSpin;
    private SpinBox _forcaMaxSpin;
    private SpinBox _agilidadeMinSpin;
    private SpinBox _agilidadeMaxSpin;
    private SpinBox _destrezaMinSpin;
    private SpinBox _destrezaMaxSpin;
    private SpinBox _inteligenciaMinSpin;
    private SpinBox _inteligenciaMaxSpin;
    private SpinBox _danoFisicoMinSpin;
    private SpinBox _danoFisicoMaxSpin;
    private SpinBox _danoMagicoMinSpin;
    private SpinBox _danoMagicoMaxSpin;
    private SpinBox _defesaFisicaMinSpin;
    private SpinBox _defesaFisicaMaxSpin;
    private SpinBox _defesaMagicaMinSpin;
    private SpinBox _defesaMagicaMaxSpin;
    private OptionButton _tipoItemDropdown;
    private OptionButton _raridadeDropdown;
    private CheckBox _statsAleatoriosCheck;
    private SpinBox _chanceCriticaMinSpin;
    private SpinBox _chanceCriticaMaxSpin;
    private SpinBox _evasaoMinSpin;
    private SpinBox _evasaoMaxSpin;
    private SpinBox _danoCriticoBonusMinSpin;
    private SpinBox _danoCriticoBonusMaxSpin;
    private SpinBox _rouboVidaMinSpin;
    private SpinBox _rouboVidaMaxSpin;
    private SpinBox _rouboManaMinSpin;
    private SpinBox _rouboManaMaxSpin;
    private SpinBox _regeneracaoVidaMinSpin;
    private SpinBox _regeneracaoVidaMaxSpin;
    private SpinBox _regeneracaoManaMinSpin;
    private SpinBox _regeneracaoManaMaxSpin;
    private SpinBox _hpMinSpin;
    private SpinBox _hpMaxSpin;
    private SpinBox _manaMinSpin;
    private SpinBox _manaMaxSpin;
    private SpinBox _staminaMinSpin;
    private SpinBox _staminaMaxSpin;
    private SpinBox _velocidadeMovimentoMinSpin;
    private SpinBox _velocidadeMovimentoMaxSpin;
    private SpinBox _velocidadeAtaqueMinSpin;
    private SpinBox _velocidadeAtaqueMaxSpin;
    private SpinBox _precisaoMinSpin;
    private SpinBox _precisaoMaxSpin;
    private SpinBox _tenacidadeMinSpin;
    private SpinBox _tenacidadeMaxSpin;
    private SpinBox _danoPvpMinSpin;
    private SpinBox _danoPvpMaxSpin;
    private SpinBox _defesaPvpMinSpin;
    private SpinBox _defesaPvpMaxSpin;
    private SpinBox _penetracaoArmaduraMinSpin;
    private SpinBox _penetracaoArmaduraMaxSpin;
    private SpinBox _reducaoCooldownMinSpin;
    private SpinBox _reducaoCooldownMaxSpin;
    private SpinBox _bonusExperienciaMinSpin;
    private SpinBox _bonusExperienciaMaxSpin;
    private SpinBox _chanceDropAumentadaMinSpin;
    private SpinBox _chanceDropAumentadaMaxSpin;
    private CheckBox _ehPvpCheck;

    private CheckBox _podeDroparCheck;
    private SpinBox _tempoCooldownSpin;
    private CheckBox _podeTrocarCheck;
    private CheckBox _podeVenderCheck;
    private CheckBox _podeArmazenarBancoCheck;
    private CheckBox _podeArmazenarBancoGuildCheck;
    private CheckBox _dropEmPkCheck;
    private SpinBox _chanceDropSpin;
    private SpinBox _tempoDesaparecimentoSpin;

    private LineEdit _afixosPoolEdit;

    private ItemResource _currentItem;
    private string _currentItemPath;
    private bool _ignorarEventosUi;
    private static readonly string ItensDir = "res://Itens/";
    private static readonly string IconesDir = "res://Itens/Incones/";

    private readonly string[] _tiposEquipamento = Enum.GetNames<TipoEquipamento>();

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        var fechar = _panel.GetNode<Button>("Header/CloseButton");
        var header = _panel.GetNode<Control>("Header");
        fechar.Pressed += () => { _panel.Visible = false; };
        header.GuiInput += OnHeaderDrag;

        _itemTree = GetNode<Tree>("%ItemTree");
        _itemSearch = GetNode<LineEdit>("%ItemSearch");
        _previewStatus = GetNode<Label>("%PreviewStatus");

        _itemIdSpin = GetNode<SpinBox>("%ItemIdSpin");
        _nomeEdit = GetNode<LineEdit>("%NomeEdit");
        _descricaoEdit = GetNode<TextEdit>("%DescricaoEdit");
        _iconePreview = GetNode<TextureRect>("%IconePreview");
        _spritesheetPreview = GetNode<TextureRect>("%SpritesheetPreview");
        _classesEdit = GetNode<LineEdit>("%ClassesEdit");
        _animacaoUsarEdit = GetNode<LineEdit>("%AnimacaoUsarEdit");
        _animacaoEquipadoEdit = GetNode<LineEdit>("%AnimacaoEquipadoEdit");
        _acumulavelCheck = GetNode<CheckBox>("%AcumulavelCheck");
        _qtdMaxSpin = GetNode<SpinBox>("%QtdMaxSpin");
        _ehDuasMaosCheck = GetNode<CheckBox>("%DuasMaosCheck");
        _ehPvpCheck = GetNode<CheckBox>("%EhPvpCheck");
        _ehBolsaCheck = GetNode<CheckBox>("%EhBolsaCheck");
        _slotsAdicionaisSpin = GetNode<SpinBox>("%SlotsAdicionaisSpin");
        _tipoDropdown = GetNode<OptionButton>("%TipoDropdown");
        _subcategoriaDropdown = GetNode<OptionButton>("%SubcategoriaDropdown");
        _categoriaPesoDropdown = GetNode<OptionButton>("%CategoriaPesoDropdown");
        _nivelReqSpin = GetNode<SpinBox>("%NivelReqSpin");
        _valorSpin = GetNode<SpinBox>("%ValorSpin");
        _tipoItemDropdown = GetNode<OptionButton>("%TipoItemDropdown");
        _raridadeDropdown = GetNode<OptionButton>("%RaridadeDropdown");

        _forcaMinSpin = GetNode<SpinBox>("%ForcaMinSpin");
        _forcaMaxSpin = GetNode<SpinBox>("%ForcaMaxSpin");
        _agilidadeMinSpin = GetNode<SpinBox>("%AgilidadeMinSpin");
        _agilidadeMaxSpin = GetNode<SpinBox>("%AgilidadeMaxSpin");
        _destrezaMinSpin = GetNode<SpinBox>("%DestrezaMinSpin");
        _destrezaMaxSpin = GetNode<SpinBox>("%DestrezaMaxSpin");
        _inteligenciaMinSpin = GetNode<SpinBox>("%InteligenciaMinSpin");
        _inteligenciaMaxSpin = GetNode<SpinBox>("%InteligenciaMaxSpin");
        _danoFisicoMinSpin = GetNode<SpinBox>("%DanoFisicoMinSpin");
        _danoFisicoMaxSpin = GetNode<SpinBox>("%DanoFisicoMaxSpin");
        _danoMagicoMinSpin = GetNode<SpinBox>("%DanoMagicoMinSpin");
        _danoMagicoMaxSpin = GetNode<SpinBox>("%DanoMagicoMaxSpin");
        _defesaFisicaMinSpin = GetNode<SpinBox>("%DefesaFisicaMinSpin");
        _defesaFisicaMaxSpin = GetNode<SpinBox>("%DefesaFisicaMaxSpin");
        _defesaMagicaMinSpin = GetNode<SpinBox>("%DefesaMagicaMinSpin");
        _defesaMagicaMaxSpin = GetNode<SpinBox>("%DefesaMagicaMaxSpin");
        _chanceCriticaMinSpin = GetNode<SpinBox>("%ChanceCriticaMinSpin");
        _chanceCriticaMaxSpin = GetNode<SpinBox>("%ChanceCriticaMaxSpin");
        _evasaoMinSpin = GetNode<SpinBox>("%EvasaoMinSpin");
        _evasaoMaxSpin = GetNode<SpinBox>("%EvasaoMaxSpin");
        _danoCriticoBonusMinSpin = GetNode<SpinBox>("%DanoCriticoBonusMinSpin");
        _danoCriticoBonusMaxSpin = GetNode<SpinBox>("%DanoCriticoBonusMaxSpin");
        _rouboVidaMinSpin = GetNode<SpinBox>("%RouboVidaMinSpin");
        _rouboVidaMaxSpin = GetNode<SpinBox>("%RouboVidaMaxSpin");
        _rouboManaMinSpin = GetNode<SpinBox>("%RouboManaMinSpin");
        _rouboManaMaxSpin = GetNode<SpinBox>("%RouboManaMaxSpin");
        _regeneracaoVidaMinSpin = GetNode<SpinBox>("%RegeneracaoVidaMinSpin");
        _regeneracaoVidaMaxSpin = GetNode<SpinBox>("%RegeneracaoVidaMaxSpin");
        _regeneracaoManaMinSpin = GetNode<SpinBox>("%RegeneracaoManaMinSpin");
        _regeneracaoManaMaxSpin = GetNode<SpinBox>("%RegeneracaoManaMaxSpin");
        _hpMinSpin = GetNode<SpinBox>("%HpMinSpin");
        _hpMaxSpin = GetNode<SpinBox>("%HpMaxSpin");
        _manaMinSpin = GetNode<SpinBox>("%ManaMinSpin");
        _manaMaxSpin = GetNode<SpinBox>("%ManaMaxSpin");
        _staminaMinSpin = GetNode<SpinBox>("%StaminaMinSpin");
        _staminaMaxSpin = GetNode<SpinBox>("%StaminaMaxSpin");
        _velocidadeMovimentoMinSpin = GetNode<SpinBox>("%VelocidadeMovimentoMinSpin");
        _velocidadeMovimentoMaxSpin = GetNode<SpinBox>("%VelocidadeMovimentoMaxSpin");
        _velocidadeAtaqueMinSpin = GetNode<SpinBox>("%VelocidadeAtaqueMinSpin");
        _velocidadeAtaqueMaxSpin = GetNode<SpinBox>("%VelocidadeAtaqueMaxSpin");
        _precisaoMinSpin = GetNode<SpinBox>("%PrecisaoMinSpin");
        _precisaoMaxSpin = GetNode<SpinBox>("%PrecisaoMaxSpin");
        _tenacidadeMinSpin = GetNode<SpinBox>("%TenacidadeMinSpin");
        _tenacidadeMaxSpin = GetNode<SpinBox>("%TenacidadeMaxSpin");
        _danoPvpMinSpin = GetNode<SpinBox>("%DanoPvpMinSpin");
        _danoPvpMaxSpin = GetNode<SpinBox>("%DanoPvpMaxSpin");
        _defesaPvpMinSpin = GetNode<SpinBox>("%DefesaPvpMinSpin");
        _defesaPvpMaxSpin = GetNode<SpinBox>("%DefesaPvpMaxSpin");
        _penetracaoArmaduraMinSpin = GetNode<SpinBox>("%PenetracaoArmaduraMinSpin");
        _penetracaoArmaduraMaxSpin = GetNode<SpinBox>("%PenetracaoArmaduraMaxSpin");
        _reducaoCooldownMinSpin = GetNode<SpinBox>("%ReducaoCooldownMinSpin");
        _reducaoCooldownMaxSpin = GetNode<SpinBox>("%ReducaoCooldownMaxSpin");
        _bonusExperienciaMinSpin = GetNode<SpinBox>("%BonusExperienciaMinSpin");
        _bonusExperienciaMaxSpin = GetNode<SpinBox>("%BonusExperienciaMaxSpin");
        _chanceDropAumentadaMinSpin = GetNode<SpinBox>("%ChanceDropAumentadaMinSpin");
        _chanceDropAumentadaMaxSpin = GetNode<SpinBox>("%ChanceDropAumentadaMaxSpin");

        _podeDroparCheck = GetNode<CheckBox>("%PodeDroparCheck");
        _tempoCooldownSpin = GetNode<SpinBox>("%TempoCooldownSpin");
        _podeTrocarCheck = GetNode<CheckBox>("%PodeTrocarCheck");
        _podeVenderCheck = GetNode<CheckBox>("%PodeVenderCheck");
        _podeArmazenarBancoCheck = GetNode<CheckBox>("%PodeArmazenarBancoCheck");
        _podeArmazenarBancoGuildCheck = GetNode<CheckBox>("%PodeArmazenarBancoGuildCheck");
        _dropEmPkCheck = GetNode<CheckBox>("%DropEmPkCheck");
        _chanceDropSpin = GetNode<SpinBox>("%ChanceDropSpin");
        _tempoDesaparecimentoSpin = GetNode<SpinBox>("%TempoDesaparecimentoSpin");

        GetNode<Button>("%BtnBrowseIcone").Pressed += OnBrowseIcone;
        GetNode<Button>("%BtnBrowseSpritesheet").Pressed += OnBrowseSpritesheet;
        GetNode<Button>("%BtnSalvar").Pressed += OnSalvar;
        GetNode<Button>("%BtnAddItem").Pressed += OnAddItem;
        GetNode<Button>("%BtnRemoveItem").Pressed += OnRemoveItem;
        GetNode<Button>("%BtnReload").Pressed += OnReload;
        GetNode<Button>("%BtnCriarTodos").Pressed += OnCriarTodosFaltantes;
        GetNode<Button>("%BtnGerarValores").Pressed += OnGerarValores;

        _itemTree.ItemSelected += OnItemSelected;
        _itemSearch.TextChanged += _ => CarregarListaItens();

        foreach (var tipo in _tiposEquipamento)
            _tipoDropdown.AddItem(tipo);
        AtualizarSubcategorias();

        _categoriaPesoDropdown.AddItem("Nenhum");
        _categoriaPesoDropdown.AddItem("Leve");
        _categoriaPesoDropdown.AddItem("Medio");
        _categoriaPesoDropdown.AddItem("Pesado");

        _tipoItemDropdown.AddItem("Normal");
        _tipoItemDropdown.AddItem("Elite");
        _tipoItemDropdown.ItemSelected += OnTipoItemChanged;
        AtualizarRaridades();

        _statsAleatoriosCheck = new CheckBox();
        _statsAleatoriosCheck.Name = "StatsAleatoriosCheck";
        _statsAleatoriosCheck.Text = "Stats Aleatórios";
        _statsAleatoriosCheck.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        var statsHBox = _tipoItemDropdown.GetParent() as HBoxContainer;
        if (statsHBox != null)
            statsHBox.AddChild(_statsAleatoriosCheck);
        else
            _tipoItemDropdown.AddSibling(_statsAleatoriosCheck);

        _afixosPoolEdit = new LineEdit();
        _afixosPoolEdit.Name = "AfixosPoolEdit";
        _afixosPoolEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _afixosPoolEdit.PlaceholderText = "ChanceCritica, DanoCriticoBonus, Precisao, ...";
        var classesRow = _classesEdit.GetParent() as HBoxContainer;
        var classesRowParent = classesRow?.GetParent() as Container;
        if (classesRowParent != null)
        {
            var afixosRow = new HBoxContainer();
            var label = new Label();
            label.Text = "Pool de Afixos";
            label.CustomMinimumSize = new Vector2(120, 0);
            afixosRow.AddChild(label);
            afixosRow.AddChild(_afixosPoolEdit);
            classesRowParent.AddChild(afixosRow);
        }

        var formFields = new Control[]
        {
            _itemIdSpin, _nomeEdit, _acumulavelCheck, _ehDuasMaosCheck, _ehPvpCheck, _ehBolsaCheck,
            _qtdMaxSpin, _slotsAdicionaisSpin, _nivelReqSpin, _valorSpin,
            _forcaMinSpin, _forcaMaxSpin,
            _agilidadeMinSpin, _agilidadeMaxSpin,
            _destrezaMinSpin, _destrezaMaxSpin,
            _inteligenciaMinSpin, _inteligenciaMaxSpin,
            _danoFisicoMinSpin, _danoFisicoMaxSpin,
            _danoMagicoMinSpin, _danoMagicoMaxSpin,
            _defesaFisicaMinSpin, _defesaFisicaMaxSpin,
            _defesaMagicaMinSpin, _defesaMagicaMaxSpin,
            _chanceCriticaMinSpin, _chanceCriticaMaxSpin,
            _evasaoMinSpin, _evasaoMaxSpin,
            _danoCriticoBonusMinSpin, _danoCriticoBonusMaxSpin,
            _rouboVidaMinSpin, _rouboVidaMaxSpin,
            _rouboManaMinSpin, _rouboManaMaxSpin,
            _regeneracaoVidaMinSpin, _regeneracaoVidaMaxSpin,
            _regeneracaoManaMinSpin, _regeneracaoManaMaxSpin,
            _hpMinSpin, _hpMaxSpin,
            _manaMinSpin, _manaMaxSpin,
            _staminaMinSpin, _staminaMaxSpin,
            _velocidadeMovimentoMinSpin, _velocidadeMovimentoMaxSpin,
            _velocidadeAtaqueMinSpin, _velocidadeAtaqueMaxSpin,
            _precisaoMinSpin, _precisaoMaxSpin,
            _tenacidadeMinSpin, _tenacidadeMaxSpin,
            _danoPvpMinSpin, _danoPvpMaxSpin,
            _defesaPvpMinSpin, _defesaPvpMaxSpin,
            _penetracaoArmaduraMinSpin, _penetracaoArmaduraMaxSpin,
            _reducaoCooldownMinSpin, _reducaoCooldownMaxSpin,
            _bonusExperienciaMinSpin, _bonusExperienciaMaxSpin,
            _chanceDropAumentadaMinSpin, _chanceDropAumentadaMaxSpin,
            _animacaoUsarEdit, _animacaoEquipadoEdit,
            _classesEdit, _afixosPoolEdit,
            _podeDroparCheck, _podeTrocarCheck, _podeVenderCheck,
            _podeArmazenarBancoCheck, _podeArmazenarBancoGuildCheck,
            _dropEmPkCheck, _chanceDropSpin, _tempoDesaparecimentoSpin,
            _tempoCooldownSpin
        };
        foreach (var f in formFields)
        {
            if (f is SpinBox sb) sb.ValueChanged += _ => OnFormDirty();
            else if (f is LineEdit le) le.TextChanged += _ => OnFormDirty();
            else if (f is CheckBox cb) cb.Toggled += _ => OnFormDirty();
        }
        _tipoDropdown.ItemSelected += _ => { AtualizarSubcategorias(); AtualizarEstadoCategoriaPeso(); OnFormDirty(); };
        _categoriaPesoDropdown.ItemSelected += _ => OnFormDirty();
        _tipoItemDropdown.ItemSelected += _ => OnFormDirty();
        _raridadeDropdown.ItemSelected += _ => OnFormDirty();
        _descricaoEdit.TextChanged += OnFormDirty;

        _panel.Visible = false;
        CallDeferred(nameof(CarregarListaItens));
    }

    public void Mostrar() => _panel.Visible = true;
    public void Esconder() => _panel.Visible = false;
    public bool EstaVisivel => _panel?.Visible ?? false;

    private void OnHeaderDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            GetViewport().SetInputAsHandled();
    }

    private void CarregarListaItens()
    {
        _itemTree.Clear();
        _currentItem = null;
        _currentItemPath = null;

        if (!DirAccess.DirExistsAbsolute(ItensDir))
        {
            _previewStatus.Text = "Pasta Itens/ n?o encontrada.";
            return;
        }

        var paths = new List<string>();
        ScanDirRecursive(ItensDir, paths);
        paths.Sort();

        var root = _itemTree.CreateItem();
        _itemTree.HideRoot = true;
        var folders = new Dictionary<string, TreeItem> { [""] = root };
        TreeItem EnsureFolder(string categoryPath, string physicalPath = null)
        {
            string parentPath = "";
            TreeItem parent = root;
            foreach (string part in categoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                string currentPath = string.IsNullOrEmpty(parentPath) ? part : parentPath + "/" + part;
                if (!folders.TryGetValue(currentPath, out var folder))
                {
                    folder = _itemTree.CreateItem(parent);
                    folder.SetText(0, part);
                    folder.Collapsed = parent != root;
                    folders[currentPath] = folder;
                }
                parent = folder;
                parentPath = currentPath;
            }
            if (!string.IsNullOrWhiteSpace(physicalPath))
                parent.SetMetadata(0, "folder:" + physicalPath.Trim('/'));
            return parent;
        }

        EnsureFolder("Armas/Arcos", "Armas/Arcos");
        EnsureFolder("Armas/Adagas", "Armas/Adagas");
        EnsureFolder("Armas/Machados", "Armas/Machados");
        EnsureFolder("Armas/Espadas", "Armas/Espadas");
        EnsureFolder("Armas/Cajados", "Armas/Cajados");
        EnsureFolder("Armas/Martelos", "Armas/Martelos");
        EnsureFolder("Armas/Mao Secundaria/Adagas Secundarias", "Escudos/AdagasSecundarias");
        EnsureFolder("Armas/Mao Secundaria/Escudos de Guardiao", "Escudos/EscudosGuardiao");
        EnsureFolder("Armas/Mao Secundaria/Escudos de Clerigo", "Escudos/EscudosClerigo");
        foreach (string peso in new[] { "Leves", "Medias", "Pesadas" })
        foreach (string slot in new[] { "Capacetes", "Peitorais", "Calcas", "Luvas", "Botas", "Cintos" })
            EnsureFolder($"Armaduras/{peso}/{slot}", $"Armaduras/{peso}/{slot}");
        EnsureFolder("Acessorios/Colares", "Colares");
        EnsureFolder("Acessorios/Aneis", "Aneis");
        EnsureFolder("Acessorios/Brincos", "Brincos");
        EnsureFolder("Companheiros/Montarias", "Montarias");
        EnsureFolder("Companheiros/Pets", "Pets");
        EnsureFolder("Consumiveis", "Consumiveis");
        EnsureFolder("Itens de Quest", "ItensDeQuest");
        EnsureFolder("Materiais", "Materiais");
        EnsureFolder("Bolsas", "Bolsas");
        EnsureFolder("Recursos", "Recursos");
        EnsureFolder("Moedas", "Moedas");
        EnsureFolder("Runas", "Runas");
        EnsureFolder("Feiticos", "Feiticos");
        EnsureFolder("Cosmeticos", "Cosmeticos");
        EnsureFolder("Outros", "Outros");
        string filter = _itemSearch.Text.Trim();
        int itemCount = 0;

        foreach (var p in paths)
        {
            string relativePath = p.Replace(ItensDir, "");
            Resource resource;
            try
            {
                resource = ResourceLoader.Load(p, cacheMode: ResourceLoader.CacheMode.Replace);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"Item ignorado por erro de carregamento: {p} ({ex.Message})");
                continue;
            }
            if (resource is not ItemResource item) continue;
            if (!string.IsNullOrWhiteSpace(filter)
                && !item.Nome.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !item.ItemID.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            string categoryPath = ObterCategoriaEditor(relativePath, item);
            string physicalFolder = relativePath.Replace('\\', '/');
            physicalFolder = physicalFolder.Contains('/') ? physicalFolder[..physicalFolder.LastIndexOf('/')] : "Outros";
            TreeItem parent = EnsureFolder(categoryPath, physicalFolder);

            var leaf = _itemTree.CreateItem(parent);
            leaf.SetText(0, $"{item.ItemID} | {item.Nome} ({item.TipoItem})");
            leaf.SetMetadata(0, relativePath);
            if (item.Icone != null)
            {
                leaf.SetIcon(0, item.Icone);
                leaf.SetIconMaxWidth(0, 28);
            }
            leaf.SetCustomMinimumHeight(32);
            itemCount++;
        }

        _previewStatus.Text = $"Encontrados {itemCount} item(ns). Selecione um.";
    }

    private static string ObterCategoriaEditor(string relativePath, ItemResource item)
    {
        string normalized = relativePath.Replace('\\', '/');
        string folder = normalized.Contains('/') ? normalized[..normalized.LastIndexOf('/')] : "Outros";

        if (item.EhBolsa) return "Bolsas";
        if (folder.StartsWith("Armas", StringComparison.OrdinalIgnoreCase)) return folder;
        if (folder.StartsWith("Escudos", StringComparison.OrdinalIgnoreCase))
        {
            string subtype = folder["Escudos".Length..].Trim('/');
            subtype = subtype switch
            {
                "AdagasSecundarias" => "Adagas Secundarias",
                "EscudosGuardiao" => "Escudos de Guardiao",
                "EscudosClerigo" => "Escudos de Clerigo",
                _ => subtype,
            };
            return "Armas/Mao Secundaria/" + subtype;
        }
        if (folder.StartsWith("Armaduras/", StringComparison.OrdinalIgnoreCase)) return folder;
        if (folder is "Capacetes" or "Peitorais" or "Cintos" or "Luvas" or "Calcas" or "Botas") return "Armaduras/Medias/" + folder;
        if (folder is "Colares" or "Aneis" or "Brincos") return "Acessorios/" + folder;
        if (folder is "Montarias" or "Pets") return "Companheiros/" + folder;
        if (folder is "Asas" or "Skins") return "Cosmeticos/" + folder;
        if (folder.Contains("Quest", StringComparison.OrdinalIgnoreCase) || folder.Contains("Missao", StringComparison.OrdinalIgnoreCase)) return "Itens de Quest";
        return folder;
    }

    private void ScanDirRecursive(string dirPath, List<string> results)
    {
        var dir = DirAccess.Open(dirPath);
        if (dir == null) return;

        dir.ListDirBegin();
        string entry;
        while ((entry = dir.GetNext()) != "")
        {
            if (entry == "." || entry == "..") continue;
            string full = dirPath.TrimEnd('/') + "/" + entry;
            if (dir.CurrentIsDir())
            {
                ScanDirRecursive(full, results);
            }
            else if ((entry.EndsWith(".tres") || entry.EndsWith(".res")) && entry != "ItemColetavel.tscn")
            {
                results.Add(full);
            }
        }
        dir.ListDirEnd();
    }

    private void OnItemSelected()
    {
        if (_ignorarEventosUi) return;
        var selected = _itemTree.GetSelected();
        if (selected == null) return;

        string relativePath = selected.GetMetadata(0).AsString();
        if (string.IsNullOrWhiteSpace(relativePath) && selected.GetFirstChild() != null)
        {
            selected.Collapsed = !selected.Collapsed;
            return;
        }
        if (relativePath.StartsWith("folder:", StringComparison.Ordinal))
        {
            selected.Collapsed = !selected.Collapsed;
            _previewStatus.Text = $"Pasta selecionada: {relativePath[7..]}. Use Novo Item para criar aqui.";
            return;
        }
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        string path = ItensDir + relativePath;
        if (!ResourceLoader.Exists(path))
        {
            _previewStatus.Text = "Arquivo n?o encontrado: " + path;
            return;
        }

        var item = ResourceLoader.Load<ItemResource>(path);
        if (item == null)
        {
            _previewStatus.Text = "Erro ao carregar item.";
            return;
        }

        _currentItem = item;
        _currentItemPath = path;
        CarregarItemNoFormulario();
    }

    private void CarregarItemNoFormulario()
    {
        if (_currentItem == null) return;
        _ignorarEventosUi = true;

        _itemIdSpin.Value = _currentItem.ItemID;
        _nomeEdit.Text = _currentItem.Nome ?? "";
        _descricaoEdit.Text = _currentItem.Descricao ?? "";
        _acumulavelCheck.ButtonPressed = _currentItem.Acumulavel;
        _qtdMaxSpin.Value = _currentItem.QuantidadeMaximaPorSlot;
        _ehDuasMaosCheck.ButtonPressed = _currentItem.EhDuasMaos;
        _ehBolsaCheck.ButtonPressed = _currentItem.EhBolsa;
        _slotsAdicionaisSpin.Value = _currentItem.SlotsAdicionais;
        _tipoDropdown.Select((int)_currentItem.Tipo);
        string currentFolder = _currentItemPath?.Replace(ItensDir, "").Replace('\\', '/') ?? "";
        var parts = currentFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        AtualizarSubcategorias(parts.Length >= 3 ? parts[1] : null);
        SelecionarPeso(_currentItem.CategoriaPeso);
        AtualizarEstadoCategoriaPeso();
        _tipoItemDropdown.Select((int)_currentItem.TipoItem);
        AtualizarRaridades();
        _raridadeDropdown.Select((int)_currentItem.Raridade);
        _nivelReqSpin.Value = _currentItem.NivelRequerido;
        _valorSpin.Value = _currentItem.Valor;
        _forcaMinSpin.Value = _currentItem.ForcaMin;
        _forcaMaxSpin.Value = _currentItem.ForcaMax;
        _agilidadeMinSpin.Value = _currentItem.AgilidadeMin;
        _agilidadeMaxSpin.Value = _currentItem.AgilidadeMax;
        _destrezaMinSpin.Value = _currentItem.DestrezaMin;
        _destrezaMaxSpin.Value = _currentItem.DestrezaMax;
        _inteligenciaMinSpin.Value = _currentItem.InteligenciaMin;
        _inteligenciaMaxSpin.Value = _currentItem.InteligenciaMax;
        _danoFisicoMinSpin.Value = _currentItem.DanoFisicoMin;
        _danoFisicoMaxSpin.Value = _currentItem.DanoFisicoMax;
        _danoMagicoMinSpin.Value = _currentItem.DanoMagicoMin;
        _danoMagicoMaxSpin.Value = _currentItem.DanoMagicoMax;
        _defesaFisicaMinSpin.Value = _currentItem.DefesaFisicaMin;
        _defesaFisicaMaxSpin.Value = _currentItem.DefesaFisicaMax;
        _defesaMagicaMinSpin.Value = _currentItem.DefesaMagicaMin;
        _defesaMagicaMaxSpin.Value = _currentItem.DefesaMagicaMax;
        _chanceCriticaMinSpin.Value = (double)_currentItem.ChanceCriticaMin;
        _chanceCriticaMaxSpin.Value = (double)_currentItem.ChanceCriticaMax;
        _evasaoMinSpin.Value = (double)_currentItem.EvasaoMin;
        _evasaoMaxSpin.Value = (double)_currentItem.EvasaoMax;
        _danoCriticoBonusMinSpin.Value = (double)_currentItem.DanoCriticoBonusMin;
        _danoCriticoBonusMaxSpin.Value = (double)_currentItem.DanoCriticoBonusMax;
        _rouboVidaMinSpin.Value = (double)_currentItem.RouboVidaMin;
        _rouboVidaMaxSpin.Value = (double)_currentItem.RouboVidaMax;
        _rouboManaMinSpin.Value = (double)_currentItem.RouboManaMin;
        _rouboManaMaxSpin.Value = (double)_currentItem.RouboManaMax;
        _regeneracaoVidaMinSpin.Value = (double)_currentItem.RegeneracaoVidaMin;
        _regeneracaoVidaMaxSpin.Value = (double)_currentItem.RegeneracaoVidaMax;
        _regeneracaoManaMinSpin.Value = (double)_currentItem.RegeneracaoManaMin;
        _regeneracaoManaMaxSpin.Value = (double)_currentItem.RegeneracaoManaMax;
        _hpMinSpin.Value = _currentItem.HpMin;
        _hpMaxSpin.Value = _currentItem.HpMax;
        _manaMinSpin.Value = _currentItem.ManaMin;
        _manaMaxSpin.Value = _currentItem.ManaMax;
        _staminaMinSpin.Value = _currentItem.StaminaMin;
        _staminaMaxSpin.Value = _currentItem.StaminaMax;
        _velocidadeMovimentoMinSpin.Value = (double)_currentItem.VelocidadeMovimentoMin;
        _velocidadeMovimentoMaxSpin.Value = (double)_currentItem.VelocidadeMovimentoMax;
        _velocidadeAtaqueMinSpin.Value = (double)_currentItem.VelocidadeAtaqueMin;
        _velocidadeAtaqueMaxSpin.Value = (double)_currentItem.VelocidadeAtaqueMax;
        _precisaoMinSpin.Value = _currentItem.PrecisaoMin;
        _precisaoMaxSpin.Value = _currentItem.PrecisaoMax;
        _tenacidadeMinSpin.Value = _currentItem.TenacidadeMin;
        _tenacidadeMaxSpin.Value = _currentItem.TenacidadeMax;
        _danoPvpMinSpin.Value = _currentItem.DanoPvpMin;
        _danoPvpMaxSpin.Value = _currentItem.DanoPvpMax;
        _defesaPvpMinSpin.Value = _currentItem.DefesaPvpMin;
        _defesaPvpMaxSpin.Value = _currentItem.DefesaPvpMax;
        _penetracaoArmaduraMinSpin.Value = _currentItem.PenetracaoArmaduraMin;
        _penetracaoArmaduraMaxSpin.Value = _currentItem.PenetracaoArmaduraMax;
        _reducaoCooldownMinSpin.Value = (double)_currentItem.ReducaoCooldownMin;
        _reducaoCooldownMaxSpin.Value = (double)_currentItem.ReducaoCooldownMax;
        _bonusExperienciaMinSpin.Value = (double)_currentItem.BonusExperienciaMin;
        _bonusExperienciaMaxSpin.Value = (double)_currentItem.BonusExperienciaMax;
        _chanceDropAumentadaMinSpin.Value = (double)_currentItem.ChanceDropAumentadaMin;
        _chanceDropAumentadaMaxSpin.Value = (double)_currentItem.ChanceDropAumentadaMax;
        _ehPvpCheck.ButtonPressed = _currentItem.EhPvp;

        if (_iconePreview != null)
            _iconePreview.Texture = _currentItem.Icone;

        if (_spritesheetPreview != null)
            _spritesheetPreview.Texture = _currentItem.SpritesheetEquipamento;
        _classesEdit.Text = _currentItem.ClassesPermitidas ?? "";
        _afixosPoolEdit.Text = _currentItem.PoolDeAfixos ?? "";
        _animacaoUsarEdit.Text = _currentItem.AnimacaoUsar ?? "";
        _animacaoEquipadoEdit.Text = _currentItem.AnimacaoEquipado ?? "";

        _podeDroparCheck.ButtonPressed = _currentItem.PodeDropar;
        _podeTrocarCheck.ButtonPressed = _currentItem.PodeTrocar;
        _podeVenderCheck.ButtonPressed = _currentItem.PodeVender;
        _podeArmazenarBancoCheck.ButtonPressed = _currentItem.PodeArmazenarBanco;
        _podeArmazenarBancoGuildCheck.ButtonPressed = _currentItem.PodeArmazenarBancoGuild;
        _dropEmPkCheck.ButtonPressed = _currentItem.DropEmPk;
        _chanceDropSpin.Value = (double)_currentItem.ChanceDrop;
        _tempoDesaparecimentoSpin.Value = (double)_currentItem.TempoDesaparecimento;
        _tempoCooldownSpin.Value = (double)_currentItem.TempoCooldown;

        _ignorarEventosUi = false;
        _previewStatus.Text = $"Editando: {_currentItem.Nome} (ID: {_currentItem.ItemID})";
    }

    private void LimparFormulario()
    {
        _ignorarEventosUi = true;
        _currentItem = null;
        _currentItemPath = null;

        _itemIdSpin.Value = 0;
        _nomeEdit.Text = "";
        _descricaoEdit.Text = "";
        _acumulavelCheck.ButtonPressed = false;
        _qtdMaxSpin.Value = 99;
        _ehDuasMaosCheck.ButtonPressed = false;
        _ehBolsaCheck.ButtonPressed = false;
        _slotsAdicionaisSpin.Value = 10;
        _tipoDropdown.Select(0);
        SelecionarPeso(PesoItem.Nenhum);
        AtualizarEstadoCategoriaPeso();
        _tipoItemDropdown.Select(0);
        AtualizarRaridades();
        _raridadeDropdown.Select(0);
        _nivelReqSpin.Value = 1;
        _valorSpin.Value = 0;
        _forcaMinSpin.Value = 0;
        _forcaMaxSpin.Value = 0;
        _agilidadeMinSpin.Value = 0;
        _agilidadeMaxSpin.Value = 0;
        _destrezaMinSpin.Value = 0;
        _destrezaMaxSpin.Value = 0;
        _inteligenciaMinSpin.Value = 0;
        _inteligenciaMaxSpin.Value = 0;
        _danoFisicoMinSpin.Value = 0;
        _danoFisicoMaxSpin.Value = 0;
        _danoMagicoMinSpin.Value = 0;
        _danoMagicoMaxSpin.Value = 0;
        _defesaFisicaMinSpin.Value = 0;
        _defesaFisicaMaxSpin.Value = 0;
        _defesaMagicaMinSpin.Value = 0;
        _defesaMagicaMaxSpin.Value = 0;
        _chanceCriticaMinSpin.Value = 0;
        _chanceCriticaMaxSpin.Value = 0;
        _evasaoMinSpin.Value = 0;
        _evasaoMaxSpin.Value = 0;
        _danoCriticoBonusMinSpin.Value = 0;
        _danoCriticoBonusMaxSpin.Value = 0;
        _rouboVidaMinSpin.Value = 0;
        _rouboVidaMaxSpin.Value = 0;
        _rouboManaMinSpin.Value = 0;
        _rouboManaMaxSpin.Value = 0;
        _regeneracaoVidaMinSpin.Value = 0;
        _regeneracaoVidaMaxSpin.Value = 0;
        _regeneracaoManaMinSpin.Value = 0;
        _regeneracaoManaMaxSpin.Value = 0;
        _hpMinSpin.Value = 0;
        _hpMaxSpin.Value = 0;
        _manaMinSpin.Value = 0;
        _manaMaxSpin.Value = 0;
        _staminaMinSpin.Value = 0;
        _staminaMaxSpin.Value = 0;
        _velocidadeMovimentoMinSpin.Value = 0;
        _velocidadeMovimentoMaxSpin.Value = 0;
        _velocidadeAtaqueMinSpin.Value = 0;
        _velocidadeAtaqueMaxSpin.Value = 0;
        _precisaoMinSpin.Value = 0;
        _precisaoMaxSpin.Value = 0;
        _tenacidadeMinSpin.Value = 0;
        _tenacidadeMaxSpin.Value = 0;
        _danoPvpMinSpin.Value = 0;
        _danoPvpMaxSpin.Value = 0;
        _defesaPvpMinSpin.Value = 0;
        _defesaPvpMaxSpin.Value = 0;
        _penetracaoArmaduraMinSpin.Value = 0;
        _penetracaoArmaduraMaxSpin.Value = 0;
        _reducaoCooldownMinSpin.Value = 0;
        _reducaoCooldownMaxSpin.Value = 0;
        _bonusExperienciaMinSpin.Value = 0;
        _bonusExperienciaMaxSpin.Value = 0;
        _chanceDropAumentadaMinSpin.Value = 0;
        _chanceDropAumentadaMaxSpin.Value = 0;

        _ehPvpCheck.ButtonPressed = false;

        if (_iconePreview != null)
            _iconePreview.Texture = null;
        if (_spritesheetPreview != null)
            _spritesheetPreview.Texture = null;
        _classesEdit.Text = "";
        _afixosPoolEdit.Text = "";
        _animacaoUsarEdit.Text = "";
        _animacaoEquipadoEdit.Text = "";

        _podeDroparCheck.ButtonPressed = true;
        _podeTrocarCheck.ButtonPressed = true;
        _podeVenderCheck.ButtonPressed = true;
        _podeArmazenarBancoCheck.ButtonPressed = true;
        _podeArmazenarBancoGuildCheck.ButtonPressed = true;
        _dropEmPkCheck.ButtonPressed = false;
        _chanceDropSpin.Value = 0;
        _tempoDesaparecimentoSpin.Value = 30;
        _tempoCooldownSpin.Value = 0;

        _ignorarEventosUi = false;
        _previewStatus.Text = "Nenhum item selecionado.";
    }

    private void OnFormDirty()
    {
        if (_ignorarEventosUi || _currentItem == null) return;
        AplicarFormularioAoItem();
    }

    private void AplicarFormularioAoItem()
    {
        if (_currentItem == null) return;

        _currentItem.ItemID = (int)_itemIdSpin.Value;
        _currentItem.Nome = _nomeEdit.Text.Trim();
        _currentItem.Descricao = _descricaoEdit.Text.Trim();
        _currentItem.Acumulavel = _acumulavelCheck.ButtonPressed;
        _currentItem.QuantidadeMaximaPorSlot = (int)_qtdMaxSpin.Value;
        _currentItem.EhDuasMaos = _ehDuasMaosCheck.ButtonPressed;
        _currentItem.EhBolsa = _ehBolsaCheck.ButtonPressed;
        _currentItem.SlotsAdicionais = (int)_slotsAdicionaisSpin.Value;
        _currentItem.Tipo = (TipoEquipamento)_tipoDropdown.Selected;
        _currentItem.CategoriaPeso = TipoUsaCategoriaPeso(_currentItem.Tipo)
            ? ObterPesoSelecionado()
            : PesoItem.Nenhum;
        _currentItem.TipoItem = (TipoItem)_tipoItemDropdown.Selected;
        _currentItem.Raridade = (Raridade)_raridadeDropdown.Selected;
        _currentItem.NivelRequerido = (int)_nivelReqSpin.Value;
        _currentItem.Valor = (int)_valorSpin.Value;
        _currentItem.ForcaMin = (int)_forcaMinSpin.Value;
        _currentItem.ForcaMax = (int)_forcaMaxSpin.Value;
        _currentItem.AgilidadeMin = (int)_agilidadeMinSpin.Value;
        _currentItem.AgilidadeMax = (int)_agilidadeMaxSpin.Value;
        _currentItem.DestrezaMin = (int)_destrezaMinSpin.Value;
        _currentItem.DestrezaMax = (int)_destrezaMaxSpin.Value;
        _currentItem.InteligenciaMin = (int)_inteligenciaMinSpin.Value;
        _currentItem.InteligenciaMax = (int)_inteligenciaMaxSpin.Value;
        _currentItem.DanoFisicoMin = (int)_danoFisicoMinSpin.Value;
        _currentItem.DanoFisicoMax = (int)_danoFisicoMaxSpin.Value;
        _currentItem.DanoMagicoMin = (int)_danoMagicoMinSpin.Value;
        _currentItem.DanoMagicoMax = (int)_danoMagicoMaxSpin.Value;
        _currentItem.DefesaFisicaMin = (int)_defesaFisicaMinSpin.Value;
        _currentItem.DefesaFisicaMax = (int)_defesaFisicaMaxSpin.Value;
        _currentItem.DefesaMagicaMin = (int)_defesaMagicaMinSpin.Value;
        _currentItem.DefesaMagicaMax = (int)_defesaMagicaMaxSpin.Value;
        _currentItem.ChanceCriticaMin = (float)_chanceCriticaMinSpin.Value;
        _currentItem.ChanceCriticaMax = (float)_chanceCriticaMaxSpin.Value;
        _currentItem.EvasaoMin = (float)_evasaoMinSpin.Value;
        _currentItem.EvasaoMax = (float)_evasaoMaxSpin.Value;
        _currentItem.DanoCriticoBonusMin = (float)_danoCriticoBonusMinSpin.Value;
        _currentItem.DanoCriticoBonusMax = (float)_danoCriticoBonusMaxSpin.Value;
        _currentItem.RouboVidaMin = (float)_rouboVidaMinSpin.Value;
        _currentItem.RouboVidaMax = (float)_rouboVidaMaxSpin.Value;
        _currentItem.RouboManaMin = (float)_rouboManaMinSpin.Value;
        _currentItem.RouboManaMax = (float)_rouboManaMaxSpin.Value;
        _currentItem.RegeneracaoVidaMin = (float)_regeneracaoVidaMinSpin.Value;
        _currentItem.RegeneracaoVidaMax = (float)_regeneracaoVidaMaxSpin.Value;
        _currentItem.RegeneracaoManaMin = (float)_regeneracaoManaMinSpin.Value;
        _currentItem.RegeneracaoManaMax = (float)_regeneracaoManaMaxSpin.Value;
        _currentItem.HpMin = (int)_hpMinSpin.Value;
        _currentItem.HpMax = (int)_hpMaxSpin.Value;
        _currentItem.ManaMin = (int)_manaMinSpin.Value;
        _currentItem.ManaMax = (int)_manaMaxSpin.Value;
        _currentItem.StaminaMin = (int)_staminaMinSpin.Value;
        _currentItem.StaminaMax = (int)_staminaMaxSpin.Value;
        _currentItem.VelocidadeMovimentoMin = (float)_velocidadeMovimentoMinSpin.Value;
        _currentItem.VelocidadeMovimentoMax = (float)_velocidadeMovimentoMaxSpin.Value;
        _currentItem.VelocidadeAtaqueMin = (float)_velocidadeAtaqueMinSpin.Value;
        _currentItem.VelocidadeAtaqueMax = (float)_velocidadeAtaqueMaxSpin.Value;
        _currentItem.PrecisaoMin = (int)_precisaoMinSpin.Value;
        _currentItem.PrecisaoMax = (int)_precisaoMaxSpin.Value;
        _currentItem.TenacidadeMin = (int)_tenacidadeMinSpin.Value;
        _currentItem.TenacidadeMax = (int)_tenacidadeMaxSpin.Value;
        _currentItem.DanoPvpMin = (int)_danoPvpMinSpin.Value;
        _currentItem.DanoPvpMax = (int)_danoPvpMaxSpin.Value;
        _currentItem.DefesaPvpMin = (int)_defesaPvpMinSpin.Value;
        _currentItem.DefesaPvpMax = (int)_defesaPvpMaxSpin.Value;
        _currentItem.PenetracaoArmaduraMin = (int)_penetracaoArmaduraMinSpin.Value;
        _currentItem.PenetracaoArmaduraMax = (int)_penetracaoArmaduraMaxSpin.Value;
        _currentItem.ReducaoCooldownMin = (float)_reducaoCooldownMinSpin.Value;
        _currentItem.ReducaoCooldownMax = (float)_reducaoCooldownMaxSpin.Value;
        _currentItem.BonusExperienciaMin = (float)_bonusExperienciaMinSpin.Value;
        _currentItem.BonusExperienciaMax = (float)_bonusExperienciaMaxSpin.Value;
        _currentItem.ChanceDropAumentadaMin = (float)_chanceDropAumentadaMinSpin.Value;
        _currentItem.ChanceDropAumentadaMax = (float)_chanceDropAumentadaMaxSpin.Value;

        _currentItem.EhPvp = _ehPvpCheck.ButtonPressed;

        _currentItem.ClassesPermitidas = _classesEdit.Text.Trim();
        _currentItem.PoolDeAfixos = _afixosPoolEdit.Text.Trim();
        _currentItem.AnimacaoUsar = _animacaoUsarEdit.Text.Trim();
        _currentItem.AnimacaoEquipado = _animacaoEquipadoEdit.Text.Trim();

        _currentItem.PodeDropar = _podeDroparCheck.ButtonPressed;
        _currentItem.PodeTrocar = _podeTrocarCheck.ButtonPressed;
        _currentItem.PodeVender = _podeVenderCheck.ButtonPressed;
        _currentItem.PodeArmazenarBanco = _podeArmazenarBancoCheck.ButtonPressed;
        _currentItem.PodeArmazenarBancoGuild = _podeArmazenarBancoGuildCheck.ButtonPressed;
        _currentItem.DropEmPk = _dropEmPkCheck.ButtonPressed;
        _currentItem.ChanceDrop = (float)_chanceDropSpin.Value;
        _currentItem.TempoDesaparecimento = (float)_tempoDesaparecimentoSpin.Value;
        _currentItem.TempoCooldown = (float)_tempoCooldownSpin.Value;

        _previewStatus.Text = $"Editando: {_currentItem.Nome} (ID: {_currentItem.ItemID})";
    }

    private void OnBrowseSpritesheet()
    {
        var dialog = new FileDialog();
        dialog.Title = "Selecionar Spritesheet do Equipamento";
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        dialog.CurrentDir = "res://";

        if (_currentItem?.SpritesheetEquipamento != null && !string.IsNullOrEmpty(_currentItem.SpritesheetEquipamento.ResourcePath))
            dialog.CurrentFile = _currentItem.SpritesheetEquipamento.ResourcePath;

        dialog.FileSelected += path =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                _currentItem.SpritesheetEquipamento = tex;
                if (_spritesheetPreview != null)
                    _spritesheetPreview.Texture = tex;
            }
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private void OnBrowseIcone()
    {
        var dialog = new FileDialog();
        dialog.Title = "Selecionar ?cone do item";
        dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        dialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" };
        dialog.Access = FileDialog.AccessEnum.Resources;
        dialog.CurrentDir = IconesDir;

        if (_currentItem?.Icone != null && !string.IsNullOrEmpty(_currentItem.Icone.ResourcePath))
            dialog.CurrentFile = _currentItem.Icone.ResourcePath;

        dialog.FileSelected += path =>
        {
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                _currentItem.Icone = tex;
                if (_iconePreview != null)
                    _iconePreview.Texture = tex;
            }
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private static string ObterPastaPorTipo(TipoEquipamento tipo)
    {
        return tipo switch
        {
            TipoEquipamento.Arma => "Armas",
            TipoEquipamento.Escudo => "Escudos",
            TipoEquipamento.Capacete => "Capacetes",
            TipoEquipamento.Peitoral => "Peitorais",
            TipoEquipamento.Cinto => "Cintos",
            TipoEquipamento.Luvas => "Luvas",
            TipoEquipamento.Calca => "Calcas",
            TipoEquipamento.Botas => "Botas",
            TipoEquipamento.Colar => "Colares",
            TipoEquipamento.Anel => "Aneis",
            TipoEquipamento.Brinco => "Brincos",
            TipoEquipamento.Runa => "Runas",
            TipoEquipamento.Asa => "Asas",
            TipoEquipamento.Montaria => "Montarias",
            TipoEquipamento.Pet => "Pets",
            TipoEquipamento.Skin => "Skins",
            TipoEquipamento.Consumivel => "Consumiveis",
            TipoEquipamento.Feitico => "Feiticos",
            TipoEquipamento.Moeda => "Moedas",
            _ => "Outros",
        };
    }

    private static bool TipoUsaCategoriaPeso(TipoEquipamento tipo)
    {
        return tipo is TipoEquipamento.Capacete or TipoEquipamento.Peitoral
            or TipoEquipamento.Cinto or TipoEquipamento.Luvas
            or TipoEquipamento.Calca or TipoEquipamento.Botas;
    }

    private PesoItem ObterPesoSelecionado()
    {
        return _categoriaPesoDropdown.Selected switch
        {
            1 => PesoItem.Leve,
            2 => PesoItem.Medio,
            3 => PesoItem.Pesado,
            _ => PesoItem.Nenhum,
        };
    }

    private void SelecionarPeso(PesoItem peso)
    {
        int index = peso switch
        {
            PesoItem.Leve => 1,
            PesoItem.Medio => 2,
            PesoItem.Pesado => 3,
            _ => 0,
        };
        _categoriaPesoDropdown.Select(index);
    }

    private void AtualizarEstadoCategoriaPeso(bool ajustarSelecao = true)
    {
        if (_categoriaPesoDropdown == null || _tipoDropdown == null) return;

        bool usaPeso = TipoUsaCategoriaPeso((TipoEquipamento)_tipoDropdown.Selected);
        _categoriaPesoDropdown.Disabled = !usaPeso;

        if (!ajustarSelecao) return;

        if (!usaPeso)
            SelecionarPeso(PesoItem.Nenhum);
        else if (ObterPesoSelecionado() == PesoItem.Nenhum)
            SelecionarPeso(PesoItem.Medio);
    }

    private void AtualizarSubcategorias(string selecionar = null)
    {
        if (_subcategoriaDropdown == null || _tipoDropdown == null) return;
        string[] options = (TipoEquipamento)_tipoDropdown.Selected switch
        {
            TipoEquipamento.Arma => new[] { "Arcos", "Adagas", "Machados", "Espadas", "Cajados", "Martelos", "Outros" },
            TipoEquipamento.Escudo => new[] { "AdagasSecundarias", "EscudosGuardiao", "EscudosClerigo", "Outros" },
            _ => new[] { "Padrao" },
        };

        _subcategoriaDropdown.Clear();
        foreach (string option in options)
            _subcategoriaDropdown.AddItem(option);

        if (!string.IsNullOrWhiteSpace(selecionar))
        {
            int index = Array.FindIndex(options, option => string.Equals(option, selecionar, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _subcategoriaDropdown.Select(index);
        }
    }

    private void OnAddItem()
    {
        var item = new ItemResource();
        item.Nome = "Item Novo";

        var tipoAtual = (TipoEquipamento)_tipoDropdown.Selected;
        item.Tipo = tipoAtual;
        if (TipoUsaCategoriaPeso(tipoAtual))
        {
            item.CategoriaPeso = ObterPesoSelecionado();
            if (item.CategoriaPeso == PesoItem.Nenhum)
                item.CategoriaPeso = PesoItem.Medio;
        }
        else
        {
            item.CategoriaPeso = PesoItem.Nenhum;
        }
        string pasta = ObterPastaPorTipo(tipoAtual);
        string subcategoria = _subcategoriaDropdown.GetItemText(_subcategoriaDropdown.Selected);
        string dirPasta;
        string selectedMetadata = _itemTree.GetSelected()?.GetMetadata(0).AsString() ?? "";
        if (selectedMetadata.StartsWith("folder:", StringComparison.Ordinal))
            dirPasta = ItensDir + selectedMetadata[7..].Trim('/') + "/";
        else
        {
            bool armorSlot = TipoUsaCategoriaPeso(tipoAtual);
            if (armorSlot)
            {
                PesoItem pesoSelecionado = ObterPesoSelecionado();
                if (pesoSelecionado == PesoItem.Nenhum)
                    pesoSelecionado = PesoItem.Medio;

                string peso = pesoSelecionado switch
                {
                    PesoItem.Leve => "Leves",
                    PesoItem.Pesado => "Pesadas",
                    _ => "Medias",
                };
                dirPasta = $"{ItensDir}Armaduras/{peso}/{pasta}/";
            }
            else
                dirPasta = ItensDir + pasta + "/";

            if (!armorSlot && (tipoAtual == TipoEquipamento.Arma || tipoAtual == TipoEquipamento.Escudo) && subcategoria != "Padrao")
                dirPasta += subcategoria + "/";
        }

        if (!DirAccess.DirExistsAbsolute(dirPasta))
            DirAccess.MakeDirAbsolute(dirPasta);

        string baseName = "ItemNovo";
        string basePath = dirPasta + baseName + ".tres";
        string path = basePath;
        int counter = 1;
        while (ResourceLoader.Exists(path))
        {
            path = dirPasta + baseName + counter + ".tres";
            counter++;
        }

        var err = ResourceSaver.Save(item, path);
        if (err != Error.Ok)
        {
            _previewStatus.Text = $"Erro ao criar item: {err}";
            return;
        }

        _currentItem = item;
        _currentItemPath = path;
        CarregarItemNoFormulario();
        CarregarListaItens();
        AtualizarDatabase();
        _previewStatus.Text = $"Item criado em {path}. Preencha os dados e salve.";
    }

    private void OnRemoveItem()
    {
        if (_currentItem == null || string.IsNullOrEmpty(_currentItemPath)) return;

        var dir = DirAccess.Open(ItensDir);
        if (dir != null)
        {
            string fileName = _currentItemPath.Replace(ItensDir, "");
            dir.Remove(fileName);
        }

        LimparFormulario();
        CarregarListaItens();
        AtualizarDatabase();
    }

    private void OnSalvar()
    {
        AplicarFormularioAoItem();
        if (_currentItem == null || string.IsNullOrEmpty(_currentItemPath))
        {
            _previewStatus.Text = "Nenhum item carregado para salvar.";
            return;
        }

        var err = ResourceSaver.Save(_currentItem, _currentItemPath);
        if (err == Error.Ok)
        {
            AtualizarDatabase();
            var network = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            network?.SendAdminUpdateItemDefinition(_currentItem);
            _previewStatus.Text = network?.IsConnected == true
                ? $"Salvo e enviado ao servidor: {_currentItem.Nome}"
                : $"Salvo em {_currentItemPath}; servidor desconectado.";
        }
        else
        {
            _previewStatus.Text = $"Erro ao salvar: {err}";
        }
    }

    private void OnReload()
    {
        LimparFormulario();
        CarregarListaItens();
    }

    private void AtualizarDatabase()
    {
        var db = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase");
        if (db != null)
            db.Refresh();
    }

    private void OnTipoItemChanged(long index)
    {
        AtualizarRaridades();
        OnFormDirty();
    }

    private void AtualizarRaridades()
    {
        _raridadeDropdown.Clear();
        bool elite = _tipoItemDropdown.Selected == 1;
        _raridadeDropdown.AddItem("Comum");
        _raridadeDropdown.AddItem("Incomum");
        _raridadeDropdown.AddItem("Raro");
        if (elite)
        {
            _raridadeDropdown.AddItem("Epico");
            _raridadeDropdown.AddItem("Lendario");
            _raridadeDropdown.AddItem("Mistico");
        }
    }

    private void OnGerarValores()
    {
        if (_currentItem == null) return;
        if (ItemGerador.ItemDeCatalogoOficial(_currentItem.ItemID))
        {
            _previewStatus.Text = "Item oficial: altere as faixas manualmente; o servidor fará a rolagem ao obter o item.";
            return;
        }
        _ignorarEventosUi = true;

        if (_statsAleatoriosCheck.ButtonPressed)
        {
            Raridade raridade = ItemGerador.SortearRaridade();
            _raridadeDropdown.Select((int)raridade);
            ItemGerador.GerarStatsAleatorios(_currentItem, raridade);
            AtualizarSpinsDoItem();
            _ignorarEventosUi = false;
            AplicarFormularioAoItem();
            _previewStatus.Text = $"Stats aleatorios gerados para {_currentItem.Nome} (Raridade: {raridade})";
            return;
        }

        var rng = new Random();
        int rarityIndex = _raridadeDropdown.Selected;
        string tipoStr = _tipoDropdown.Text;
        bool isArmor = TipoUsaCategoriaPeso((TipoEquipamento)_tipoDropdown.Selected);
        string classes = _currentItem.ClassesPermitidas ?? "";

        var (mainMin, mainMax, bonusChance) = rarityIndex switch
        {
            0 => (1, 3, 0.25f),
            1 => (3, 6, 0.40f),
            2 => (6, 9, 0.55f),
            3 => (9, 12, 0.70f),
            4 => (12, 15, 0.85f),
            5 => (15, 18, 0.95f),
            _ => (1, 3, 0.25f),
        };

        ResetAllStatSpins();

        // --- Fixed base stats (always present) weighted by armor category only ---
        PesoItem peso = isArmor ? ObterPesoSelecionado() : PesoItem.Nenhum;
        if (isArmor && peso == PesoItem.Nenhum)
            peso = PesoItem.Medio;
        if (!isArmor)
            _currentItem.CategoriaPeso = PesoItem.Nenhum;
        int hpBase, manaBase, staminaBase, defFisicaBase, defMagicaBase;
        if (peso == PesoItem.Pesado)
        {
            hpBase = 25 + rarityIndex * 15;
            defFisicaBase = 12 + rarityIndex * 8;
            defMagicaBase = 8 + rarityIndex * 6;
        }
        else if (peso == PesoItem.Leve)
        {
            hpBase = 10 + rarityIndex * 8;
            defFisicaBase = 6 + rarityIndex * 5;
            defMagicaBase = 18 + rarityIndex * 12;
        }
        else // Medio
        {
            hpBase = 15 + rarityIndex * 10;
            defFisicaBase = 9 + rarityIndex * 7;
            defMagicaBase = 9 + rarityIndex * 7;
        }
        manaBase = 5 + rarityIndex * 4;
        staminaBase = 5 + rarityIndex * 4;

        int hpMinVal = hpBase, hpMaxVal = hpBase + 5 + rarityIndex * 2;
        _hpMinSpin.Value = hpMinVal; _hpMaxSpin.Value = hpMaxVal;
        _currentItem.Hp = rng.Next(hpMinVal, hpMaxVal + 1);

        int manaMinVal = manaBase, manaMaxVal = manaBase + 3 + rarityIndex * 2;
        _manaMinSpin.Value = manaMinVal; _manaMaxSpin.Value = manaMaxVal;
        _currentItem.Mana = rng.Next(manaMinVal, manaMaxVal + 1);

        int staminaMinVal = staminaBase, staminaMaxVal = staminaBase + 3 + rarityIndex * 2;
        _staminaMinSpin.Value = staminaMinVal; _staminaMaxSpin.Value = staminaMaxVal;
        _currentItem.Stamina = rng.Next(staminaMinVal, staminaMaxVal + 1);

        int defFisicaMin = defFisicaBase, defFisicaMax = defFisicaBase + 2 + rarityIndex;
        _defesaFisicaMinSpin.Value = defFisicaMin; _defesaFisicaMaxSpin.Value = defFisicaMax;
        _currentItem.DefesaFisica = rng.Next(defFisicaMin, defFisicaMax + 1);

        int defMagicaMin = defMagicaBase, defMagicaMax = defMagicaBase + 2 + rarityIndex;
        _defesaMagicaMinSpin.Value = defMagicaMin; _defesaMagicaMaxSpin.Value = defMagicaMax;
        _currentItem.DefesaMagica = rng.Next(defMagicaMin, defMagicaMax + 1);

        // --- Determine item type ---
        bool isBootGloveHelm = tipoStr is "Capacete" or "Botas" or "Luvas";
        bool isArcherAssassin = classes.Contains("Arqueiro") || classes.Contains("Ladino");

        void RollInt(SpinBox minSpin, SpinBox maxSpin, Action<int> setter, int minVal, int maxVal, float mult = 1.0f)
        {
            if (rng.NextDouble() < bonusChance * mult)
            {
                minSpin.Value = minVal;
                maxSpin.Value = maxVal;
                setter(rng.Next(minVal, maxVal + 1));
            }
        }

        void RollPct(SpinBox minSpin, SpinBox maxSpin, Action<float> setter, float minVal, float maxVal, float mult = 1.0f)
        {
            if (rng.NextDouble() < bonusChance * mult)
            {
                minSpin.Value = minVal;
                maxSpin.Value = maxVal;
                setter((float)Math.Round(rng.NextDouble() * (maxVal - minVal) + minVal, 2));
            }
        }

        if (isArmor)
        {
            RollInt(_forcaMinSpin, _forcaMaxSpin, v => _currentItem.Forca = v, mainMin, mainMax);
            RollInt(_destrezaMinSpin, _destrezaMaxSpin, v => _currentItem.Destreza = v, mainMin, mainMax);
            RollInt(_agilidadeMinSpin, _agilidadeMaxSpin, v => _currentItem.Agilidade = v, mainMin, mainMax);
            RollInt(_danoFisicoMinSpin, _danoFisicoMaxSpin, v => _currentItem.DanoFisico = v, mainMin, mainMax);
            RollInt(_danoCriticoBonusMinSpin, _danoCriticoBonusMaxSpin, v => _currentItem.DanoCriticoBonus = v, mainMin, mainMax);

            RollPct(_precisaoMinSpin, _precisaoMaxSpin, v => _currentItem.Precisao = v, 0.1f, 0.8f);
            RollPct(_chanceCriticaMinSpin, _chanceCriticaMaxSpin, v => _currentItem.ChanceCritica = v, 0.1f, 0.8f);

            if (isBootGloveHelm)
                RollPct(_velocidadeAtaqueMinSpin, _velocidadeAtaqueMaxSpin, v => _currentItem.VelocidadeAtaque = v, 0.1f, 0.8f);

            if (isArcherAssassin || peso == PesoItem.Medio)
                RollPct(_evasaoMinSpin, _evasaoMaxSpin, v => _currentItem.Evasao = v, 0.1f, 0.8f);

            RollPct(_tenacidadeMinSpin, _tenacidadeMaxSpin, v => _currentItem.Tenacidade = v, 0.1f, 0.8f, 0.6f);
            RollPct(_reducaoCooldownMinSpin, _reducaoCooldownMaxSpin, v => _currentItem.ReducaoCooldown = v, 0.1f, 0.8f, 0.6f);
        }
        else
        {
            bool isMagePrist = classes.Contains("Mago") || classes.Contains("Prist");

            RollInt(_forcaMinSpin, _forcaMaxSpin, v => _currentItem.Forca = v, mainMin, mainMax, 0.7f);
            RollInt(_agilidadeMinSpin, _agilidadeMaxSpin, v => _currentItem.Agilidade = v, mainMin, mainMax, 0.7f);
            RollInt(_destrezaMinSpin, _destrezaMaxSpin, v => _currentItem.Destreza = v, mainMin, mainMax, 0.7f);
            if (isMagePrist)
            {
                RollInt(_inteligenciaMinSpin, _inteligenciaMaxSpin, v => _currentItem.Inteligencia = v, mainMin, mainMax, 0.7f);
                RollInt(_danoMagicoMinSpin, _danoMagicoMaxSpin, v => _currentItem.DanoMagico = v, mainMin, mainMax, 0.7f);
            }
            RollInt(_danoFisicoMinSpin, _danoFisicoMaxSpin, v => _currentItem.DanoFisico = v, mainMin, mainMax, 0.8f);
            RollInt(_danoCriticoBonusMinSpin, _danoCriticoBonusMaxSpin, v => _currentItem.DanoCriticoBonus = v, mainMin, mainMax, 0.5f);

            RollPct(_chanceCriticaMinSpin, _chanceCriticaMaxSpin, v => _currentItem.ChanceCritica = v, 0.1f, 0.8f, 0.7f);
            RollPct(_precisaoMinSpin, _precisaoMaxSpin, v => _currentItem.Precisao = v, 0.1f, 0.8f, 0.6f);
            RollPct(_evasaoMinSpin, _evasaoMaxSpin, v => _currentItem.Evasao = v, 0.1f, 0.8f, 0.4f);
            RollPct(_velocidadeAtaqueMinSpin, _velocidadeAtaqueMaxSpin, v => _currentItem.VelocidadeAtaque = v, 0.1f, 0.8f, 0.5f);
            RollPct(_tenacidadeMinSpin, _tenacidadeMaxSpin, v => _currentItem.Tenacidade = v, 0.1f, 0.8f, 0.4f);
            RollPct(_reducaoCooldownMinSpin, _reducaoCooldownMaxSpin, v => _currentItem.ReducaoCooldown = v, 0.1f, 0.8f, 0.4f);
        }

        // Universal bonus stats (small chance on any item)
        RollPct(_velocidadeMovimentoMinSpin, _velocidadeMovimentoMaxSpin, v => _currentItem.VelocidadeMovimento = v, 0.02f, 0.12f, 0.3f);
        RollPct(_rouboVidaMinSpin, _rouboVidaMaxSpin, v => _currentItem.RouboVida = v, 0.2f, 1.5f, 0.3f);
        RollPct(_rouboManaMinSpin, _rouboManaMaxSpin, v => _currentItem.RouboMana = v, 0.2f, 1.5f, 0.3f);
        RollPct(_regeneracaoVidaMinSpin, _regeneracaoVidaMaxSpin, v => _currentItem.RegeneracaoVida = v, 0.3f, 2.0f, 0.25f);
        RollPct(_regeneracaoManaMinSpin, _regeneracaoManaMaxSpin, v => _currentItem.RegeneracaoMana = v, 0.3f, 2.0f, 0.25f);
        RollPct(_bonusExperienciaMinSpin, _bonusExperienciaMaxSpin, v => _currentItem.BonusExperiencia = v, 0.5f, 5.0f, 0.2f);
        RollPct(_chanceDropAumentadaMinSpin, _chanceDropAumentadaMaxSpin, v => _currentItem.ChanceDropAumentada = v, 0.5f, 4.0f, 0.2f);

        // PvP stats only if item is PvP
        if (_ehPvpCheck.ButtonPressed)
        {
            if (rng.NextDouble() < bonusChance * 0.5f)
            {
                _danoPvpMinSpin.Value = 1 + rarityIndex * 2; _danoPvpMaxSpin.Value = 5 + rarityIndex * 4;
                _currentItem.DanoPvp = rng.Next((int)_danoPvpMinSpin.Value, (int)_danoPvpMaxSpin.Value + 1);
            }
            if (rng.NextDouble() < bonusChance * 0.5f)
            {
                _defesaPvpMinSpin.Value = 1 + rarityIndex; _defesaPvpMaxSpin.Value = 3 + rarityIndex * 2;
                _currentItem.DefesaPvp = rng.Next((int)_defesaPvpMinSpin.Value, (int)_defesaPvpMaxSpin.Value + 1);
            }
            if (rng.NextDouble() < bonusChance * 0.4f)
            {
                _penetracaoArmaduraMinSpin.Value = 1 + rarityIndex; _penetracaoArmaduraMaxSpin.Value = 3 + rarityIndex * 2;
                _currentItem.PenetracaoArmadura = rng.Next((int)_penetracaoArmaduraMinSpin.Value, (int)_penetracaoArmaduraMaxSpin.Value + 1);
            }
        }

        _ignorarEventosUi = false;
        AplicarFormularioAoItem();
        _previewStatus.Text = $"Valores gerados para {_currentItem.Nome} (Raridade: {_raridadeDropdown.GetItemText(_raridadeDropdown.Selected)})";
    }

    private void AtualizarSpinsDoItem()
    {
        if (_currentItem == null) return;

        _forcaMinSpin.Value = _currentItem.Forca; _forcaMaxSpin.Value = _currentItem.Forca;
        _agilidadeMinSpin.Value = _currentItem.Agilidade; _agilidadeMaxSpin.Value = _currentItem.Agilidade;
        _destrezaMinSpin.Value = _currentItem.Destreza; _destrezaMaxSpin.Value = _currentItem.Destreza;
        _inteligenciaMinSpin.Value = _currentItem.Inteligencia; _inteligenciaMaxSpin.Value = _currentItem.Inteligencia;
        _danoFisicoMinSpin.Value = _currentItem.DanoFisico; _danoFisicoMaxSpin.Value = _currentItem.DanoFisico;
        _danoMagicoMinSpin.Value = _currentItem.DanoMagico; _danoMagicoMaxSpin.Value = _currentItem.DanoMagico;
        _defesaFisicaMinSpin.Value = _currentItem.DefesaFisica; _defesaFisicaMaxSpin.Value = _currentItem.DefesaFisica;
        _defesaMagicaMinSpin.Value = _currentItem.DefesaMagica; _defesaMagicaMaxSpin.Value = _currentItem.DefesaMagica;
        _chanceCriticaMinSpin.Value = (double)_currentItem.ChanceCritica; _chanceCriticaMaxSpin.Value = (double)_currentItem.ChanceCritica;
        _evasaoMinSpin.Value = (double)_currentItem.Evasao; _evasaoMaxSpin.Value = (double)_currentItem.Evasao;
        _danoCriticoBonusMinSpin.Value = (double)_currentItem.DanoCriticoBonus; _danoCriticoBonusMaxSpin.Value = (double)_currentItem.DanoCriticoBonus;
        _rouboVidaMinSpin.Value = (double)_currentItem.RouboVida; _rouboVidaMaxSpin.Value = (double)_currentItem.RouboVida;
        _rouboManaMinSpin.Value = (double)_currentItem.RouboMana; _rouboManaMaxSpin.Value = (double)_currentItem.RouboMana;
        _regeneracaoVidaMinSpin.Value = (double)_currentItem.RegeneracaoVida; _regeneracaoVidaMaxSpin.Value = (double)_currentItem.RegeneracaoVida;
        _regeneracaoManaMinSpin.Value = (double)_currentItem.RegeneracaoMana; _regeneracaoManaMaxSpin.Value = (double)_currentItem.RegeneracaoMana;
        _hpMinSpin.Value = _currentItem.Hp; _hpMaxSpin.Value = _currentItem.Hp;
        _manaMinSpin.Value = _currentItem.Mana; _manaMaxSpin.Value = _currentItem.Mana;
        _staminaMinSpin.Value = _currentItem.Stamina; _staminaMaxSpin.Value = _currentItem.Stamina;
        _velocidadeMovimentoMinSpin.Value = (double)_currentItem.VelocidadeMovimento; _velocidadeMovimentoMaxSpin.Value = (double)_currentItem.VelocidadeMovimento;
        _velocidadeAtaqueMinSpin.Value = (double)_currentItem.VelocidadeAtaque; _velocidadeAtaqueMaxSpin.Value = (double)_currentItem.VelocidadeAtaque;
        _precisaoMinSpin.Value = (double)_currentItem.Precisao; _precisaoMaxSpin.Value = (double)_currentItem.Precisao;
        _tenacidadeMinSpin.Value = (double)_currentItem.Tenacidade; _tenacidadeMaxSpin.Value = (double)_currentItem.Tenacidade;
        _danoPvpMinSpin.Value = _currentItem.DanoPvp; _danoPvpMaxSpin.Value = _currentItem.DanoPvp;
        _defesaPvpMinSpin.Value = _currentItem.DefesaPvp; _defesaPvpMaxSpin.Value = _currentItem.DefesaPvp;
        _penetracaoArmaduraMinSpin.Value = _currentItem.PenetracaoArmadura; _penetracaoArmaduraMaxSpin.Value = _currentItem.PenetracaoArmadura;
        _reducaoCooldownMinSpin.Value = (double)_currentItem.ReducaoCooldown; _reducaoCooldownMaxSpin.Value = (double)_currentItem.ReducaoCooldown;
        _bonusExperienciaMinSpin.Value = (double)_currentItem.BonusExperiencia; _bonusExperienciaMaxSpin.Value = (double)_currentItem.BonusExperiencia;
        _chanceDropAumentadaMinSpin.Value = (double)_currentItem.ChanceDropAumentada; _chanceDropAumentadaMaxSpin.Value = (double)_currentItem.ChanceDropAumentada;
    }

    private void ResetAllStatSpins()
    {
        _forcaMinSpin.Value = 0; _forcaMaxSpin.Value = 0;
        _agilidadeMinSpin.Value = 0; _agilidadeMaxSpin.Value = 0;
        _destrezaMinSpin.Value = 0; _destrezaMaxSpin.Value = 0;
        _inteligenciaMinSpin.Value = 0; _inteligenciaMaxSpin.Value = 0;
        _danoFisicoMinSpin.Value = 0; _danoFisicoMaxSpin.Value = 0;
        _danoMagicoMinSpin.Value = 0; _danoMagicoMaxSpin.Value = 0;
        _chanceCriticaMinSpin.Value = 0; _chanceCriticaMaxSpin.Value = 0;
        _evasaoMinSpin.Value = 0; _evasaoMaxSpin.Value = 0;
        _danoCriticoBonusMinSpin.Value = 0; _danoCriticoBonusMaxSpin.Value = 0;
        _rouboVidaMinSpin.Value = 0; _rouboVidaMaxSpin.Value = 0;
        _rouboManaMinSpin.Value = 0; _rouboManaMaxSpin.Value = 0;
        _regeneracaoVidaMinSpin.Value = 0; _regeneracaoVidaMaxSpin.Value = 0;
        _regeneracaoManaMinSpin.Value = 0; _regeneracaoManaMaxSpin.Value = 0;
        _hpMinSpin.Value = 0; _hpMaxSpin.Value = 0;
        _manaMinSpin.Value = 0; _manaMaxSpin.Value = 0;
        _staminaMinSpin.Value = 0; _staminaMaxSpin.Value = 0;
        _velocidadeMovimentoMinSpin.Value = 0; _velocidadeMovimentoMaxSpin.Value = 0;
        _velocidadeAtaqueMinSpin.Value = 0; _velocidadeAtaqueMaxSpin.Value = 0;
        _precisaoMinSpin.Value = 0; _precisaoMaxSpin.Value = 0;
        _tenacidadeMinSpin.Value = 0; _tenacidadeMaxSpin.Value = 0;
        _danoPvpMinSpin.Value = 0; _danoPvpMaxSpin.Value = 0;
        _defesaPvpMinSpin.Value = 0; _defesaPvpMaxSpin.Value = 0;
        _penetracaoArmaduraMinSpin.Value = 0; _penetracaoArmaduraMaxSpin.Value = 0;
        _reducaoCooldownMinSpin.Value = 0; _reducaoCooldownMaxSpin.Value = 0;
        _bonusExperienciaMinSpin.Value = 0; _bonusExperienciaMaxSpin.Value = 0;
        _chanceDropAumentadaMinSpin.Value = 0; _chanceDropAumentadaMaxSpin.Value = 0;

        _currentItem.Forca = 0; _currentItem.Agilidade = 0;
        _currentItem.Destreza = 0; _currentItem.Inteligencia = 0;
        _currentItem.DanoFisico = 0; _currentItem.DanoMagico = 0;
        _currentItem.DefesaFisica = 0; _currentItem.DefesaMagica = 0;
        _currentItem.ChanceCritica = 0; _currentItem.Evasao = 0;
        _currentItem.DanoCriticoBonus = 0; _currentItem.RouboVida = 0;
        _currentItem.RouboMana = 0; _currentItem.RegeneracaoVida = 0;
        _currentItem.RegeneracaoMana = 0; _currentItem.Hp = 0;
        _currentItem.Mana = 0; _currentItem.Stamina = 0;
        _currentItem.VelocidadeMovimento = 0; _currentItem.VelocidadeAtaque = 0;
        _currentItem.Precisao = 0; _currentItem.Tenacidade = 0;
        _currentItem.ReducaoCooldown = 0; _currentItem.BonusExperiencia = 0;
        _currentItem.ChanceDropAumentada = 0;
        _currentItem.DanoPvp = 0; _currentItem.DefesaPvp = 0;
        _currentItem.PenetracaoArmadura = 0;
    }

    private void OnCriarTodosFaltantes()
    {
        if (_itemTree != null)
        {
            CarregarListaItens();
            var ids = new HashSet<int>();
            int duplicates = 0;
            int missingIcons = 0;
            var paths = new List<string>();
            ScanDirRecursive(ItensDir, paths);
            foreach (string path in paths)
            {
                var item = ResourceLoader.Load<ItemResource>(path);
                if (item == null) continue;
                if (!ids.Add(item.ItemID)) duplicates++;
                if (item.Icone == null) missingIcons++;
            }
            _previewStatus.Text = $"Catalogo: {ids.Count} IDs, {duplicates} duplicado(s), {missingIcons} sem icone.";
            return;
        }

        var serverDefs = new (int id, string name, string tipoStr, int forca, int agilidade, int destreza, int inteligencia, int dano, int defesa, int maxStack, bool isBag, int extraSlots, string classes)[]
        {
            (1, "Pocao de Vida", "Nenhum", 0, 0, 0, 0, 0, 0, 99, false, 0, ""),
            (2, "Pocao de Mana", "Nenhum", 0, 0, 0, 0, 0, 0, 99, false, 0, ""),
            (3, "Mochila de Couro", "Nenhum", 0, 0, 0, 0, 0, 0, 1, true, 6, ""),
            (10, "Espada Curta", "Arma", 0, 0, 0, 0, 5, 0, 1, false, 0, ""),
            (11, "Espada Longa", "Arma", 0, 0, 0, 0, 10, 0, 1, false, 0, ""),
            (12, "Cajado de Madeira", "Arma", 0, 0, 0, 5, 3, 0, 1, false, 0, "Mago,Prist"),
            (13, "Arco Curto", "Arma", 0, 3, 0, 0, 6, 0, 1, false, 0, "Arqueiro"),
            (20, "Capacete de Ferro", "Capacete", 0, 0, 0, 0, 0, 3, 1, false, 0, "Guerreiro"),
            (21, "Peitoral de Couro", "Peitoral", 0, 0, 0, 0, 0, 5, 1, false, 0, "Arqueiro,Ladino"),
            (22, "Calcas de Couro", "Calca", 0, 0, 0, 0, 0, 3, 1, false, 0, "Arqueiro,Ladino"),
            (23, "Botas de Couro", "Botas", 0, 2, 0, 0, 0, 2, 1, false, 0, "Arqueiro,Ladino"),
            (24, "Luvas de Couro", "Luvas", 0, 0, 2, 0, 0, 1, 1, false, 0, "Arqueiro,Ladino"),
            (30, "Colar da Sabedoria", "Colar", 0, 0, 0, 5, 0, 0, 1, false, 0, ""),
            (31, "Anel de Forca", "Anel", 3, 0, 0, 0, 0, 0, 1, false, 0, ""),
        };

        int criados = 0;
        foreach (var (id, name, tipoStr, forca, agilidade, destreza, inteligencia, dano, defesa, maxStack, isBag, extraSlots, classes) in serverDefs)
        {
            string fileName = $"{name.Replace(" ", "")}.tres";
            string path = ItensDir + fileName;
            if (ResourceLoader.Exists(path)) continue;

            var item = new ItemResource
            {
                ItemID = id,
                Nome = name,
                Acumulavel = id <= 3 || maxStack > 1,
                QuantidadeMaximaPorSlot = maxStack,
                EhBolsa = isBag,
                SlotsAdicionais = extraSlots,
                Tipo = (TipoEquipamento)Enum.Parse(typeof(TipoEquipamento), tipoStr),
                Forca = forca,
                Agilidade = agilidade,
                Destreza = destreza,
                Inteligencia = inteligencia,
                DanoFisico = dano,
                DefesaFisica = defesa,
                ClassesPermitidas = classes,
            };

            var err = ResourceSaver.Save(item, path);
            if (err == Error.Ok) criados++;
        }

        CarregarListaItens();
        AtualizarDatabase();
        _previewStatus.Text = $"{criados} item(ns) criados dos {serverDefs.Length} definidos no servidor.";
    }
}
