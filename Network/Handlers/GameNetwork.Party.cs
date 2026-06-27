#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public int PendingPartyId { get; private set; }
    public Godot.Collections.Array<Godot.Collections.Dictionary> PendingPartyMembers { get; } = new();
    public bool HasPendingPartyData => PendingPartyId > 0 && PendingPartyMembers.Count > 0;
    public int PartyXpBonusPercent => HasPendingPartyData ? Mathf.Clamp(PendingPartyMembers.Count * 5, 0, 25) : 0;

    public void SendPartyInvite(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_PartyInvite, w => w.Put(targetName));
    }

    public void SendPartyAccept()
    {
        _client?.SendPacket(PacketId.C2S_PartyAccept, w => { });
    }

    public void SendPartyLeave()
    {
        _client?.SendPacket(PacketId.C2S_PartyLeave, w => { });
    }

    public void SendPartyKick(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_PartyKick, w => w.Put(targetName));
    }

    public void SendPartyPromote(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_PartyPromote, w => w.Put(targetName));
    }

    private void HandlePartyData(NetDataReader r)
    {
        int partyId = r.GetInt();
        byte memberCount = r.GetByte();
        var members = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        for (int i = 0; i < memberCount; i++)
        {
            ulong eid = r.GetULong();
            string name = r.GetString();
            bool isLeader = r.GetBool();
            int hp = r.GetInt();
            int maxHp = r.GetInt();
            int mana = r.GetInt();
            int maxMana = r.GetInt();
            int level = r.GetInt();
            string charClass = r.GetString();
            var m = new Godot.Collections.Dictionary
            {
                ["entity_id"] = (long)eid,
                ["name"] = name,
                ["is_leader"] = isLeader,
                ["health"] = hp,
                ["max_health"] = maxHp,
                ["mana"] = mana,
                ["max_mana"] = maxMana,
                ["level"] = level,
                ["class"] = charClass,
            };
            members.Add(m);
        }

        PendingPartyId = partyId;
        PendingPartyMembers.Clear();
        foreach (var member in members)
            PendingPartyMembers.Add(member);

        Log($"[PARTY] Dados recebidos: party={partyId}, membros={memberCount}");
        EmitSignal(SignalName.OnPartyData, partyId, members);
    }

    private void HandlePartyMemberUpdate(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        string name = r.GetString();
        int hp = r.GetInt();
        int maxHp = r.GetInt();
        int mana = r.GetInt();
        int maxMana = r.GetInt();
        int level = r.GetInt();
        bool joined = r.GetBool();
        string charClass = r.GetString();
        ApplyPendingPartyMemberUpdate(entityId, name, hp, maxHp, mana, maxMana, level, joined, charClass);
        EmitSignal(SignalName.OnPartyMemberUpdate, entityId, name, hp, maxHp, mana, maxMana, level, joined, charClass);
    }

    private void HandlePartyLeaderUpdate(NetDataReader r)
    {
        ulong newLeaderId = r.GetULong();
        ApplyPendingPartyLeaderUpdate(newLeaderId);
        EmitSignal(SignalName.OnPartyLeaderUpdate, newLeaderId);
    }

    private void ApplyPendingPartyMemberUpdate(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined, string characterClass)
    {
        var existing = FindPendingPartyMember(entityId);
        if (!joined)
        {
            if (existing != null)
                PendingPartyMembers.Remove(existing);
            if (PendingPartyMembers.Count == 0)
                PendingPartyId = 0;
            return;
        }

        if (existing == null)
        {
            PendingPartyMembers.Add(new Godot.Collections.Dictionary
            {
                ["entity_id"] = (long)entityId,
                ["name"] = name,
                ["is_leader"] = false,
                ["health"] = health,
                ["max_health"] = maxHealth,
                ["mana"] = mana,
                ["max_mana"] = maxMana,
                ["level"] = level,
                ["class"] = characterClass,
            });
            return;
        }

        existing["name"] = name;
        existing["health"] = health;
        existing["max_health"] = maxHealth;
        existing["mana"] = mana;
        existing["max_mana"] = maxMana;
        existing["level"] = level;
        existing["class"] = characterClass;
    }

    private void ApplyPendingPartyLeaderUpdate(ulong newLeaderId)
    {
        foreach (var member in PendingPartyMembers)
            member["is_leader"] = (ulong)(long)member["entity_id"] == newLeaderId;
    }

    private Godot.Collections.Dictionary? FindPendingPartyMember(ulong entityId)
    {
        foreach (var member in PendingPartyMembers)
        {
            if ((ulong)(long)member["entity_id"] == entityId)
                return member;
        }
        return null;
    }

    private void HandlePartyInviteReceived(NetDataReader r)
    {
        string senderName = r.GetString();
        Log($"[PARTY] Received party invite from {senderName}");
        InvitePopupUI.ShowInvite("party", senderName);
    }
}
