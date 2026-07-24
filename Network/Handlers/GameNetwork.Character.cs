#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendCreateCharacter(
        string name,
        string className,
        string race,
        string cabeloPath = "",
        string barbaPath = "",
        string cabeloCor = "ffffff",
        string barbaCor = "ffffff")
    {
        _client?.SendPacket(PacketId.C2S_CreateCharacter, w =>
        {
            w.Put(name);
            w.Put(className);
            w.Put(race);
            w.Put(cabeloPath ?? "");
            w.Put(barbaPath ?? "");
            w.Put(string.IsNullOrWhiteSpace(cabeloCor) ? "ffffff" : cabeloCor);
            w.Put(string.IsNullOrWhiteSpace(barbaCor) ? "ffffff" : barbaCor);
        });
    }

    public void SendSelectCharacter(int slotIndex)
    {
        _client?.SendPacket(PacketId.C2S_SelectCharacter, w =>
        {
            w.Put(slotIndex);
        });
    }

    public void SendDeleteCharacter(int slotIndex)
    {
        _client?.SendPacket(PacketId.C2S_DeleteCharacter, w =>
        {
            w.Put(slotIndex);
        });
    }

    public void SendEnterWorld(string name, string characterClass, string race, float posX, float posY)
    {
        _client?.SendPacket(PacketId.C2S_EnterWorld, w =>
        {
            w.Put(0); // channelId
            w.Put(name);
            w.Put(characterClass);
            w.Put(race);
            w.Put(posX);
            w.Put(posY);
        });
    }

    public void SendLeaveWorld()
    {
        _client?.SendPacket(PacketId.C2S_LeaveWorld, _ => { });
    }

    private void HandleLeaveWorld(NetDataReader r)
    {
        int characterCount = r.GetInt();
        CharacterSlotLimit = r.AvailableBytes >= 4 ? r.GetInt() : 3;
        Characters.Clear();
        for (int i = 0; i < characterCount; i++)
        {
            var entry = new CharacterEntry
            {
                SlotIndex = r.GetInt(),
                Name = r.GetString(),
                Class = r.GetString(),
                Race = r.GetString(),
                Level = r.GetInt(),
            };
            ReadAppearanceFields(r, entry);
            Characters.Add(entry);
        }

        ClearAllEntities();
        GetNodeOrNull<EntityManager>("EntityManager")?.ClearAll();

        ClearCharacterScopedState();

        VisualStateReset.PrepararTelaSelecao(GetTree());

        var err = GetTree().ChangeSceneToFile(SceneConstants.SELECAO_PERSONAGEM);
        if (err != Error.Ok)
            LogError($"Falha ao voltar para a seleção de personagens: {err}");
        else
            Log("Saída do mundo confirmada pelo servidor.");
    }

    private void HandleCharacterList(NetDataReader r)
    {
        int count = r.GetInt();
        CharacterSlotLimit = r.AvailableBytes >= 4 ? r.GetInt() : 3;
        Characters.Clear();
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            int slot = r.GetInt();
            string name = r.GetString();
            int level = r.GetInt();
            string cls = r.GetString();
            string race = r.AvailableBytes > 0 ? r.GetString() : "";
            var entry = new CharacterEntry
            {
                SlotIndex = slot,
                Name = name,
                Class = cls,
                Race = race,
                Level = level,
            };
            ReadAppearanceFields(r, entry);
            Characters.Add(entry);

            var dict = new Godot.Collections.Dictionary
            {
                ["slot"] = slot,
                ["name"] = name,
                ["level"] = level,
                ["class_name"] = cls,
                ["race"] = race,
                ["cabelo_path"] = entry.CabeloPath,
                ["barba_path"] = entry.BarbaPath,
                ["cabelo_cor"] = entry.CabeloCor,
                ["barba_cor"] = entry.BarbaCor,
            };
            list.Add(dict);
        }
        EmitSignal(SignalName.OnCharacterList, list);
    }

    private void HandleCreateCharacterResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        Log($"[GAME] Criacao de personagem: {(success ? "OK" : "falhou")} - {message}");
        EmitSignal(SignalName.OnCreateCharacterResult, success, message);
    }

    private void HandleCharacterDeleted(NetDataReader r)
    {
        int count = r.GetInt();
        CharacterSlotLimit = r.AvailableBytes >= 4 ? r.GetInt() : 3;
        Characters.Clear();
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            int slot = r.GetInt();
            string name = r.GetString();
            string cls = r.GetString();
            string race = r.GetString();
            int level = r.GetInt();

            var entry = new CharacterEntry
            {
                SlotIndex = slot,
                Name = name,
                Class = cls,
                Race = race,
                Level = level,
            };
            ReadAppearanceFields(r, entry);
            Characters.Add(entry);

            list.Add(new Godot.Collections.Dictionary
            {
                ["slot"] = slot,
                ["name"] = name,
                ["level"] = level,
                ["class_name"] = cls,
                ["race"] = race,
                ["cabelo_path"] = entry.CabeloPath,
                ["barba_path"] = entry.BarbaPath,
                ["cabelo_cor"] = entry.CabeloCor,
                ["barba_cor"] = entry.BarbaCor,
            });
        }
        Log($"[GAME] Lista de personagens atualizada apos exclusao: {count} restantes");
        EmitSignal(SignalName.OnCharacterList, list);
    }

    private void HandleEnterWorld(NetDataReader r)
    {
        Log($"HandleEnterWorld: lendo pacote ({r.AvailableBytes} bytes disponiveis)");
        ClearCharacterScopedState();
        LocalPlayerId = r.GetULong();
        LocalChannelId = r.GetInt();
        float x = r.GetFloat();
        float y = r.GetFloat();
        PendingPlayerSpawn = new Vector2(x, y);
        int level = r.GetInt();
        long xp = r.GetLong();
        int forca = r.GetInt();
        int agilidade = r.GetInt();
        int destreza = r.GetInt();
        int inteligencia = r.GetInt();
        int health = r.GetInt();
        int maxHealth = r.GetInt();
        int mana = r.GetInt();
        int maxMana = r.GetInt();
        int baseAttack = r.GetInt();
        int defense = r.GetInt();
        int statPoints = r.GetInt();
        _pendingCabeloPath = r.AvailableBytes > 0 ? r.GetString() : "";
        _pendingBarbaPath = r.AvailableBytes > 0 ? r.GetString() : "";
        _pendingCabeloCor = r.AvailableBytes > 0 ? r.GetString() : "ffffff";
        _pendingBarbaCor = r.AvailableBytes > 0 ? r.GetString() : "ffffff";
        AplicarAparenciaPendenteNoPersonagemEscolhido();

        _pendingLevel = level;
        _pendingXp = xp;
        _pendingStatPoints = statPoints;
        _pendingBaseForca = forca;
        _pendingBaseAgilidade = agilidade;
        _pendingBaseDestreza = destreza;
        _pendingBaseInteligencia = inteligencia;

        Log($"Entrando no mundo! ID={LocalPlayerId} Canal={LocalChannelId} Lv={level} Pos=({x:F0},{y:F0})");
        Log("Sinalizando enterWorldPending para _Process fazer a troca de cena");
        _enterWorldPending = true;
    }

    private static void ReadAppearanceFields(NetDataReader r, CharacterEntry entry)
    {
        entry.CabeloPath = r.AvailableBytes > 0 ? r.GetString() : "";
        entry.BarbaPath = r.AvailableBytes > 0 ? r.GetString() : "";
        entry.CabeloCor = r.AvailableBytes > 0 ? r.GetString() : "ffffff";
        entry.BarbaCor = r.AvailableBytes > 0 ? r.GetString() : "ffffff";
    }

    private void AplicarAparenciaPendenteNoPersonagemEscolhido()
    {
        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        if (escolhido == null)
            return;

        escolhido.CabeloPath = _pendingCabeloPath;
        escolhido.BarbaPath = _pendingBarbaPath;
        escolhido.CabeloCor = ParseAppearanceColor(_pendingCabeloCor);
        escolhido.BarbaCor = ParseAppearanceColor(_pendingBarbaCor);
    }

    private static Color ParseAppearanceColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Colors.White;

        try
        {
            return Color.FromHtml(value.StartsWith("#") ? value : "#" + value);
        }
        catch
        {
            return Colors.White;
        }
    }
}
