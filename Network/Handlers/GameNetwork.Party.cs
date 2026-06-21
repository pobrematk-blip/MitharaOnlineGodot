#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
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
        EmitSignal(SignalName.OnPartyMemberUpdate, entityId, name, hp, maxHp, mana, maxMana, level, joined, charClass);
    }

    private void HandlePartyLeaderUpdate(NetDataReader r)
    {
        ulong newLeaderId = r.GetULong();
        EmitSignal(SignalName.OnPartyLeaderUpdate, newLeaderId);
    }

    private void HandlePartyInviteReceived(NetDataReader r)
    {
        string senderName = r.GetString();
        Log($"[PARTY] Received party invite from {senderName}");
        InvitePopupUI.ShowInvite("party", senderName);
    }
}
