using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class AdminPanelUI : Control
{
    private Panel _panel;
    private TabContainer _tabs;
    private Label _statusLabel;
    private Label _playerInfoLabel;
    private Label _entityCountLabel;
    private ItemList _entityList;
    private Button _toggleAdminBtn;
    private Button _godModeBtn;
    private Button _healBtn;
    private SpinBox _levelSpin;
    private Button _setLevelBtn;
    private SpinBox _xpSpin;
    private Button _setXpBtn;
    private SpinBox _speedSpin;
    private Button _setSpeedBtn;
    private Button _noclipBtn;
    private SpinBox _tpXSpin;
    private SpinBox _tpYSpin;
    private Button _tpBtn;
    private Button _tpToCenterBtn;
    private SpinBox _spawnItemIdSpin;
    private SpinBox _spawnItemQtySpin;
    private Button _spawnItemBtn;
    private LineEdit _spawnMobInput;
    private Button _spawnMobBtn;
    private LineEdit _broadcastInput;
    private Button _broadcastBtn;
    private LineEdit _kickInput;
    private Button _kickBtn;
    private Button _refreshPlayersBtn;
    private RichTextLabel _serverLog;
    private ItemList _adminAccountList;
    private LineEdit _adminAccountInput;
    private Button _adminAccountAddBtn;
    private Button _adminAccountRemoveBtn;
    private bool _godModeAtivo;
    private bool _noclipAtivo;
    private float _speedOriginal;
    private uint _noclipOriginalCollisionLayer;
    private uint _noclipOriginalCollisionMask;

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        var fechar = _panel.GetNode<Button>("Header/CloseButton");
        var header = _panel.GetNode<Control>("Header");
        fechar.Pressed += () => { _panel.Visible = false; };
        header.GuiInput += OnHeaderDrag;
        _tabs = _panel.GetNode<TabContainer>("Tabs");

        _statusLabel = GetNode<Label>("%StatusLabel");
        _playerInfoLabel = GetNode<Label>("%PlayerInfoLabel");
        _entityCountLabel = GetNode<Label>("%EntityCountLabel");
        _entityList = GetNode<ItemList>("%EntityList");
        _toggleAdminBtn = GetNode<Button>("%ToggleAdminBtn");
        _godModeBtn = GetNode<Button>("%GodModeBtn");
        _healBtn = GetNode<Button>("%HealBtn");
        _levelSpin = GetNode<SpinBox>("%LevelSpin");
        _setLevelBtn = GetNode<Button>("%SetLevelBtn");
        _xpSpin = GetNode<SpinBox>("%XpSpin");
        _setXpBtn = GetNode<Button>("%SetXpBtn");
        _speedSpin = GetNode<SpinBox>("%SpeedSpin");
        _setSpeedBtn = GetNode<Button>("%SetSpeedBtn");
        _noclipBtn = GetNode<Button>("%NoclipBtn");
        _tpXSpin = GetNode<SpinBox>("%TpXSpin");
        _tpYSpin = GetNode<SpinBox>("%TpYSpin");
        _tpBtn = GetNode<Button>("%TpBtn");
        _tpToCenterBtn = GetNode<Button>("%TpToCenterBtn");
        _spawnItemIdSpin = GetNode<SpinBox>("%SpawnItemIdSpin");
        _spawnItemQtySpin = GetNode<SpinBox>("%SpawnItemQtySpin");
        _spawnItemBtn = GetNode<Button>("%SpawnItemBtn");
        _spawnMobInput = GetNode<LineEdit>("%SpawnMobInput");
        _spawnMobBtn = GetNode<Button>("%SpawnMobBtn");
        _broadcastInput = GetNode<LineEdit>("%BroadcastInput");
        _broadcastBtn = GetNode<Button>("%BroadcastBtn");
        _kickInput = GetNode<LineEdit>("%KickInput");
        _kickBtn = GetNode<Button>("%KickBtn");
        _refreshPlayersBtn = GetNode<Button>("%RefreshPlayersBtn");
        _serverLog = GetNode<RichTextLabel>("%ServerLog");
        _adminAccountList = GetNode<ItemList>("%AdminAccountList");
        _adminAccountInput = GetNode<LineEdit>("%AdminAccountInput");
        _adminAccountAddBtn = GetNode<Button>("%AdminAccountAddBtn");
        _adminAccountRemoveBtn = GetNode<Button>("%AdminAccountRemoveBtn");

        _toggleAdminBtn.Pressed += OnToggleAdmin;
        _godModeBtn.Pressed += OnGodMode;
        _healBtn.Pressed += OnHeal;
        _setLevelBtn.Pressed += OnSetLevel;
        _setXpBtn.Pressed += OnSetXp;
        _setSpeedBtn.Pressed += OnSetSpeed;
        _noclipBtn.Pressed += OnNoclip;
        _tpBtn.Pressed += OnTeleport;
        _tpToCenterBtn.Pressed += OnTeleportCenter;
        _spawnItemBtn.Pressed += OnSpawnItem;
        _spawnMobBtn.Pressed += OnSpawnMob;
        _broadcastBtn.Pressed += OnBroadcast;
        _kickBtn.Pressed += OnKick;
        _refreshPlayersBtn.Pressed += OnRefreshPlayers;
        _adminAccountAddBtn.Pressed += OnAdminAccountAdd;
        _adminAccountRemoveBtn.Pressed += OnAdminAccountRemove;

        AtualizarTudo();
    }

    private Player ObterPlayer()
    {
        return GetTree().CurrentScene.FindChild("Player", true, false) as Player;
    }

    private SaveManager ObterSave()
    {
        return GetNodeOrNull<SaveManager>("/root/SaveManager");
    }

    private GameNetwork ObterNet()
    {
        return GetNodeOrNull<GameNetwork>("/root/GameNetwork");
    }

    private bool IsAdmin => ObterSave()?.IsAdmin ?? false;

    public void AtualizarTudo()
    {
        AtualizarStatus();
        AtualizarPlayerInfo();
        AtualizarEntidades();
    }

    private void AtualizarStatus()
    {
        bool admin = IsAdmin;
        _statusLabel.Text = admin ? "ATIVO" : "INATIVO";
        _statusLabel.AddThemeColorOverride("font_color", admin ? new Color(0, 1, 0) : new Color(1, 0.3f, 0.3f));
        _toggleAdminBtn.Text = admin ? "Desativar Admin" : "Ativar Admin";
    }

    private void AtualizarPlayerInfo()
    {
        var player = ObterPlayer();
        if (player != null)
        {
            var prog = player.GetNodeOrNull("LevelProgressionComponent");
            int nivel = 0;
            int xp = 0;
            if (prog != null)
            {
                var t = prog.GetType();
                nivel = (int)(t.GetProperty("Nivel")?.GetValue(prog) ?? 0);
                xp = (t.GetProperty("ExperienciaAtual")?.GetValue(prog) as int?) ?? 0;
            }
            string god = _godModeAtivo ? " [GOD]" : "";
            string noclip = _noclipAtivo ? " [NOCLIP]" : "";
            string admin = IsAdmin ? " [ADMIN]" : "";
            _playerInfoLabel.Text =
                $"Player: {player.Name}{admin}{god}{noclip}\n" +
                $"Nível: {nivel} | XP: {xp}\n" +
                $"HP: {player.CurrentHealth}/{player.MaxHealth} | MP: {player.CurrentMana}/{player.MaxMana}\n" +
                $"Pos: ({player.GlobalPosition.X:F0}, {player.GlobalPosition.Y:F0})\n" +
                $"Speed: {player.MaxSpeed:F1} | Fric: {player.Friction:F0} | Acel: {player.Acceleration:F0}";

            _levelSpin.Value = nivel;
            _speedSpin.Value = player.MaxSpeed;
        }
        else
        {
            _playerInfoLabel.Text = "Nenhum player encontrado na cena.";
        }
    }

    private void AtualizarEntidades()
    {
        var net = ObterNet();
        _entityList.Clear();
        int count = 0;
        if (net != null)
        {
            foreach (var kv in net.GetAllEntities())
            {
                var node = kv.Value;
                string info = $"[{kv.Key}] {node.Name} ({node.GetType().Name}) @ ({node.GlobalPosition.X:F0},{node.GlobalPosition.Y:F0})";
                _entityList.AddItem(info);
                count++;
            }
        }
        _entityCountLabel.Text = $"Entidades: {count}";
    }

    private void Log(string msg)
    {
        _serverLog.Text += $"\n{msg}";
        _serverLog.ScrollToLine(_serverLog.GetLineCount() - 1);
    }

    private void EnviarComandoServidor(string cmd)
    {
        var net = ObterNet();
        if (net != null && net.LoggedIn)
        {
            net.SendChat(0, "", cmd, "pt");
            Log($">>> {cmd}");
        }
        else
        {
            Log("! Servidor não conectado. Comando não enviado.");
        }
    }

    private void OnToggleAdmin()
    {
        var save = ObterSave();
        if (save == null) return;
        bool novo = !save.IsAdmin;
        save.SetAdmin(novo);
        AtualizarTudo();
        Log($"Modo admin {(novo ? "ativado" : "desativado")}.");
    }

    private void OnGodMode()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }

        _godModeAtivo = !_godModeAtivo;
        if (_godModeAtivo)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var field = typeof(Player).GetField("_isInvincible", flags);
            if (field != null) field.SetValue(player, true);
            player.Heal(player.MaxHealth);
            Log("God Mode ATIVO (invulnerável).");
        }
        else
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var field = typeof(Player).GetField("_isInvincible", flags);
            if (field != null) field.SetValue(player, false);
            Log("God Mode DESATIVADO.");
        }
        _godModeBtn.Text = _godModeAtivo ? "Desativar God Mode" : "Ativar God Mode";
        AtualizarPlayerInfo();
    }

    private void OnHeal()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }

        player.Heal(player.MaxHealth);
        var manaProp = typeof(Player).GetProperty("CurrentMana", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (manaProp != null && manaProp.CanWrite)
            manaProp.SetValue(player, player.MaxMana);
        Log($"Vida restaurada: {player.CurrentHealth}/{player.MaxHealth} | Mana: {player.CurrentMana}/{player.MaxMana}");
        AtualizarPlayerInfo();
    }

    private void OnSetLevel()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }
        int newLevel = (int)_levelSpin.Value;
        var prog = player.GetNodeOrNull("LevelProgressionComponent");
        if (prog != null)
        {
            var t = prog.GetType();
            t.GetMethod("DefinirProgresso")?.Invoke(prog, new object[] { newLevel, 0 });
            Log($"Nível alterado para {newLevel}.");
        }
        else
        {
            var nivelField = typeof(Player).GetField("_nivel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nivelField != null)
            {
                nivelField.SetValue(player, newLevel);
                Log($"Nível alterado para {newLevel} (fallback).");
            }
        }
        AtualizarPlayerInfo();
    }

    private void OnSetXp()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }
        long newXp = (long)_xpSpin.Value;
        var prog = player.GetNodeOrNull("LevelProgressionComponent");
        if (prog != null)
        {
            var t = prog.GetType();
            int nivelAtual = (int)(t.GetProperty("Nivel")?.GetValue(prog) ?? 1);
            t.GetMethod("DefinirProgresso")?.Invoke(prog, new object[] { nivelAtual, newXp });
            Log($"XP alterado para {newXp}.");
        }
        else
        {
            Log("! LevelProgressionComponent não encontrado.");
        }
        AtualizarPlayerInfo();
    }

    private void OnSetSpeed()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }
        float newSpeed = (float)_speedSpin.Value;
        if (_speedOriginal <= 0) _speedOriginal = player.MaxSpeed;
        player.MaxSpeed = newSpeed;
        Log($"Velocidade alterada para {newSpeed} (original: {_speedOriginal}).");
        AtualizarPlayerInfo();
    }

    private void OnNoclip()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }

        _noclipAtivo = !_noclipAtivo;
        if (_noclipAtivo)
        {
            _noclipOriginalCollisionLayer = player.CollisionLayer;
            _noclipOriginalCollisionMask = player.CollisionMask;
            player.CollisionLayer = 0u;
            player.CollisionMask = 0u;
            Log("Noclip ATIVO (colisões desligadas).");
        }
        else
        {
            player.CollisionLayer = _noclipOriginalCollisionLayer;
            player.CollisionMask = _noclipOriginalCollisionMask;
            Log("Noclip DESATIVADO (colisões restauradas).");
        }
        _noclipBtn.Text = _noclipAtivo ? "Desativar Noclip" : "Ativar Noclip";
    }

    private void OnTeleport()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }
        float x = (float)_tpXSpin.Value;
        float y = (float)_tpYSpin.Value;
        player.GlobalPosition = new Vector2(x, y);
        Log($"Teleportado para ({x:F0}, {y:F0}).");
        AtualizarPlayerInfo();
    }

    private void OnTeleportCenter()
    {
        var player = ObterPlayer();
        if (player == null) { Log("! Player não encontrado."); return; }
        var viewport = GetViewport();
        if (viewport == null) return;
        var cam = viewport.GetCamera2D();
        if (cam != null)
            player.GlobalPosition = cam.GlobalPosition;
        else
            player.GlobalPosition = Vector2.Zero;
        Log("Teleportado para o centro da tela.");
        AtualizarPlayerInfo();
    }

    private void OnSpawnItem()
    {
        int itemId = (int)_spawnItemIdSpin.Value;
        int qty = (int)_spawnItemQtySpin.Value;
        EnviarComandoServidor($"/item {itemId} {qty}");
    }

    private void OnSpawnMob()
    {
        string prefab = _spawnMobInput.Text.Trim().ToLower();
        if (string.IsNullOrEmpty(prefab)) { Log("! Digite um prefab ID (ex: slime, goblin, wolf)."); return; }
        EnviarComandoServidor($"/summon {prefab}");
    }

    private void OnBroadcast()
    {
        string msg = _broadcastInput.Text.Trim();
        if (string.IsNullOrEmpty(msg)) { Log("! Digite uma mensagem."); return; }
        EnviarComandoServidor($"/broadcast {msg}");
        _broadcastInput.Text = "";
    }

    private void OnKick()
    {
        string nome = _kickInput.Text.Trim();
        if (string.IsNullOrEmpty(nome)) { Log("! Digite um nome de jogador."); return; }
        EnviarComandoServidor($"/kick {nome}");
        _kickInput.Text = "";
    }

    private void OnRefreshPlayers()
    {
        EnviarComandoServidor("/players");
        AtualizarEntidades();
    }

    private void OnAdminAccountAdd()
    {
        string account = _adminAccountInput.Text.Trim();
        if (string.IsNullOrEmpty(account)) { Log("! Digite um ID de conta."); return; }

        var save = ObterSave();
        if (save != null)
        {
            var accounts = save.Conta.AdminAccountsList;
            if (!accounts.Contains(account))
            {
                accounts.Add(account);
                save.SalvarConta();
                Log($"Conta '{account}' adicionada como admin (local).");
            }
        }

        EnviarComandoServidor($"/admin add {account}");
        _adminAccountInput.Text = "";
        AtualizarAdminAccounts();
    }

    private void OnAdminAccountRemove()
    {
        var selected = _adminAccountList.GetSelectedItems();
        if (selected.Length == 0) { Log("! Selecione uma conta na lista."); return; }

        string account = _adminAccountList.GetItemText(selected[0]);
        var save = ObterSave();
        if (save != null)
        {
            save.Conta.AdminAccountsList.Remove(account);
            save.SalvarConta();
        }

        EnviarComandoServidor($"/admin remove {account}");
        AtualizarAdminAccounts();
        Log($"Conta '{account}' removida dos admins.");
    }

    public void AtualizarAdminAccounts()
    {
        _adminAccountList.Clear();
        var save = ObterSave();
        if (save == null) return;

        var accounts = save.Conta.AdminAccountsList;
        foreach (var a in accounts)
            _adminAccountList.AddItem(a);
    }

    private void OnHeaderDrag(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            GetViewport().SetInputAsHandled();
    }
}
