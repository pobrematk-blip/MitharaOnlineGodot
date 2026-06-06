using Mithara.Server.World;

namespace Mithara.Server.Tests.World;

public class PartyManagerTests
{
    [Fact]
    public void CreateParty_ValidLeader_Succeeds()
    {
        var mgr = new PartyManager();

        var party = mgr.CreateParty(1);

        Assert.NotNull(party);
        Assert.Equal(1ul, party!.LeaderEntityId);
        Assert.Single(party.Members);
        Assert.Contains(1ul, party.Members);
    }

    [Fact]
    public void CreateParty_LeaderAlreadyInParty_ReturnsNull()
    {
        var mgr = new PartyManager();
        mgr.CreateParty(1);

        var second = mgr.CreateParty(1);

        Assert.Null(second);
    }

    [Fact]
    public void AddMember_ValidInvite_Succeeds()
    {
        var mgr = new PartyManager();
        var party = mgr.CreateParty(1);

        var added = mgr.AddMember(party!.Id, 2);

        Assert.True(added);
        Assert.Equal(party.Id, mgr.GetPlayerPartyId(2));
    }

    [Fact]
    public void AddMember_PlayerAlreadyInParty_Fails()
    {
        var mgr = new PartyManager();
        var party = mgr.CreateParty(1);
        mgr.AddMember(party!.Id, 2);

        var added = mgr.AddMember(party.Id, 2);

        Assert.False(added);
    }

    [Fact]
    public void AddMember_NonExistentParty_Fails()
    {
        var mgr = new PartyManager();

        var added = mgr.AddMember(999, 1);

        Assert.False(added);
    }

    [Fact]
    public void RemoveMember_TransfersLeadership()
    {
        var mgr = new PartyManager();
        var party = mgr.CreateParty(1);
        mgr.AddMember(party!.Id, 2);
        mgr.AddMember(party.Id, 3);

        mgr.RemoveMember(1);

        Assert.Null(mgr.GetPlayerPartyId(1));
        Assert.Equal(2ul, party.LeaderEntityId);
    }

    [Fact]
    public void RemoveMember_LastMember_DeletesParty()
    {
        var mgr = new PartyManager();
        var party = mgr.CreateParty(1);

        mgr.RemoveMember(1);

        Assert.Null(mgr.GetParty(party!.Id));
    }

    [Fact]
    public void RemoveMember_OnlyTwoMembers_RemovesAndDeletesParty()
    {
        var mgr = new PartyManager();
        var party = mgr.CreateParty(1);
        mgr.AddMember(party!.Id, 2);

        mgr.RemoveMember(2);

        Assert.Null(mgr.GetParty(party.Id));
        Assert.Null(mgr.GetPlayerPartyId(2));
        Assert.Null(mgr.GetPlayerPartyId(1));
    }

    [Fact]
    public void GetMemberEntityIds_ReturnsAll()
    {
        var mgr = new PartyManager();
        var party = mgr.CreateParty(1);
        mgr.AddMember(party!.Id, 2);
        mgr.AddMember(party.Id, 3);

        var ids = mgr.GetMemberEntityIds(party.Id);

        Assert.Equal(3, ids.Count);
        Assert.Contains(1ul, ids);
        Assert.Contains(2ul, ids);
        Assert.Contains(3ul, ids);
    }

    [Fact]
    public void GetMemberEntityIds_NonExistentParty_ReturnsEmpty()
    {
        var mgr = new PartyManager();

        var ids = mgr.GetMemberEntityIds(999);

        Assert.Empty(ids);
    }

    [Fact]
    public void GetPlayerPartyId_NotInParty_ReturnsNull()
    {
        var mgr = new PartyManager();

        var id = mgr.GetPlayerPartyId(999);

        Assert.Null(id);
    }

    [Fact]
    public void RemoveMember_NotInParty_DoesNothing()
    {
        var mgr = new PartyManager();

        mgr.RemoveMember(999);

        Assert.Null(mgr.GetPlayerPartyId(999));
    }
}
