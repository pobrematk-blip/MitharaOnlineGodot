using Mithara.Server.World;

namespace Mithara.Server.Tests.World;

public class GuildTests
{
    [Fact]
    public void Guild_Constructor_SetsProperties()
    {
        var guild = new Guild("OsHerois", 42);

        Assert.Equal("OsHerois", guild.Name);
        Assert.Equal(42ul, guild.LeaderEntityId);
        Assert.Single(guild.Members);
        Assert.Contains(42ul, guild.Members);
        Assert.Equal(0, guild.GetRank(42));
        Assert.Equal(1, guild.Level);
        Assert.Equal(0, guild.Xp);
        Assert.Equal(0, guild.SkillPoints);
    }

    [Fact]
    public void Guild_AddXp_LevelsUp()
    {
        var guild = new Guild("LevelUp", 1);

        guild.AddXp(350);

        Assert.Equal(3, guild.Level);
        Assert.Equal(0, guild.Xp);
        Assert.Equal(2, guild.SkillPoints);
    }

    [Fact]
    public void Guild_AddXp_PartialLevel()
    {
        var guild = new Guild("Partial", 1);

        guild.AddXp(50);

        Assert.Equal(1, guild.Level);
        Assert.Equal(50, guild.Xp);
    }

    [Fact]
    public void Guild_AddXp_MaxLevel_NoMoreLevels()
    {
        var guild = new Guild("Maxed", 1);

        guild.AddXp(999999);

        Assert.Equal(5, guild.Level);
    }

    [Fact]
    public void Guild_GetRank_DefaultMember_IsNovato()
    {
        var guild = new Guild("Test", 1);
        guild.Members.Add(100);

        var rank = guild.GetRank(100);

        Assert.Equal(4, rank);
    }

    [Fact]
    public void Guild_GetRank_NonMember_ReturnsNovato()
    {
        var guild = new Guild("Test", 1);

        var rank = guild.GetRank(999);

        Assert.Equal(4, rank);
    }

    [Fact]
    public void Guild_SetRank_UpdatesRank()
    {
        var guild = new Guild("Test", 1);
        guild.Members.Add(100);

        guild.SetRank(100, 2);

        Assert.Equal(2, guild.GetRank(100));
    }

    [Fact]
    public void Guild_SetRank_NonMember_DoesNothing()
    {
        var guild = new Guild("Test", 1);

        guild.SetRank(999, 1);

        Assert.Equal(4, guild.GetRank(999));
    }

    [Fact]
    public void Guild_TryBuySkill_WithPoints_Succeeds()
    {
        var guild = new Guild("Test", 1);
        guild.SkillPoints = 2;

        var result = guild.TryBuySkill("attack_bonus");

        Assert.True(result);
        Assert.Equal(1, guild.GetSkillLevel("attack_bonus"));
        Assert.Equal(1, guild.SkillPoints);
    }

    [Fact]
    public void Guild_TryBuySkill_NoPoints_Fails()
    {
        var guild = new Guild("Test", 1);
        guild.SkillPoints = 0;

        var result = guild.TryBuySkill("attack_bonus");

        Assert.False(result);
        Assert.Equal(0, guild.GetSkillLevel("attack_bonus"));
    }

    [Fact]
    public void Guild_TryBuySkill_MaxLevel_Fails()
    {
        var guild = new Guild("Test", 1);
        guild.SkillPoints = 5;
        guild.SkillLevels["attack_bonus"] = 5;

        var result = guild.TryBuySkill("attack_bonus");

        Assert.False(result);
        Assert.Equal(5, guild.GetSkillLevel("attack_bonus"));
    }

    [Fact]
    public void Guild_XpForNextLevel_ExceedsArray_ReturnsLargeValue()
    {
        var guild = new Guild("Test", 1);
        guild.Level = 10;

        Assert.Equal(999999, guild.XpForNextLevel);
    }
}

public class GuildManagerTests
{
    [Fact]
    public void CreateGuild_ValidData_Succeeds()
    {
        var mgr = new GuildManager();

        var guild = mgr.CreateGuild("Legiao", 1);

        Assert.NotNull(guild);
        Assert.Equal("Legiao", guild!.Name);
        Assert.Equal(1ul, guild.LeaderEntityId);
        Assert.NotNull(mgr.GetPlayerGuildId(1));
    }

    [Fact]
    public void CreateGuild_PlayerAlreadyInGuild_ReturnsNull()
    {
        var mgr = new GuildManager();
        mgr.CreateGuild("First", 1);

        var second = mgr.CreateGuild("Second", 1);

        Assert.Null(second);
    }

    [Fact]
    public void CreateGuild_DuplicateName_ReturnsNull()
    {
        var mgr = new GuildManager();
        mgr.CreateGuild("Unica", 1);

        var duplicate = mgr.CreateGuild("unica", 2);

        Assert.Null(duplicate);
    }

    [Fact]
    public void AddMember_ValidInvite_Succeeds()
    {
        var mgr = new GuildManager();
        var guild = mgr.CreateGuild("Guild", 1);

        var added = mgr.AddMember(guild!.Id, 2);

        Assert.True(added);
        Assert.Equal(guild.Id, mgr.GetPlayerGuildId(2));
    }

    [Fact]
    public void AddMember_PlayerAlreadyInGuild_Fails()
    {
        var mgr = new GuildManager();
        var guild = mgr.CreateGuild("Guild", 1);
        mgr.AddMember(guild!.Id, 2);

        var added = mgr.AddMember(guild.Id, 2);

        Assert.False(added);
    }

    [Fact]
    public void AddMember_NonExistentGuild_Fails()
    {
        var mgr = new GuildManager();

        var added = mgr.AddMember(999, 1);

        Assert.False(added);
    }

    [Fact]
    public void RemoveMember_RemovesAndTransfersLeadership()
    {
        var mgr = new GuildManager();
        var guild = mgr.CreateGuild("Guild", 1);
        mgr.AddMember(guild!.Id, 2);
        mgr.AddMember(guild.Id, 3);

        mgr.RemoveMember(1);

        Assert.Null(mgr.GetPlayerGuildId(1));
        Assert.Equal(2ul, mgr.GetGuild(guild.Id)!.LeaderEntityId);
    }

    [Fact]
    public void RemoveMember_LastMember_DeletesGuild()
    {
        var mgr = new GuildManager();
        var guild = mgr.CreateGuild("Guild", 1);

        mgr.RemoveMember(1);

        Assert.Null(mgr.GetGuild(guild!.Id));
    }

    [Fact]
    public void GetMemberEntityIds_ReturnsAllMembers()
    {
        var mgr = new GuildManager();
        var guild = mgr.CreateGuild("Guild", 1);
        mgr.AddMember(guild!.Id, 2);
        mgr.AddMember(guild.Id, 3);

        var members = mgr.GetMemberEntityIds(guild.Id);

        Assert.Equal(3, members.Count);
        Assert.Contains(1ul, members);
        Assert.Contains(2ul, members);
        Assert.Contains(3ul, members);
    }

    [Fact]
    public void GetPlayerGuild_ReturnsGuild()
    {
        var mgr = new GuildManager();
        mgr.CreateGuild("FindMe", 1);

        var guild = mgr.GetPlayerGuild(1);

        Assert.NotNull(guild);
        Assert.Equal("FindMe", guild!.Name);
    }

    [Fact]
    public void GetPlayerGuild_NotInGuild_ReturnsNull()
    {
        var mgr = new GuildManager();

        var guild = mgr.GetPlayerGuild(999);

        Assert.Null(guild);
    }

    [Fact]
    public void LoadGuild_AddsToManager()
    {
        var mgr = new GuildManager();
        var guild = new Guild("Loaded", 1);
        guild.Id = 50;

        mgr.LoadGuild(guild);

        Assert.Same(guild, mgr.GetGuild(50));
    }

    [Fact]
    public void LoadMember_AddsToExistingGuild()
    {
        var mgr = new GuildManager();
        var guild = new Guild("Test", 1);
        guild.Id = 10;
        mgr.LoadGuild(guild);

        mgr.LoadMember(10, 99, 3);

        Assert.Equal(10, mgr.GetPlayerGuildId(99));
        Assert.Equal(3, guild.GetRank(99));
    }

    [Fact]
    public void LoadSkill_AddsToExistingGuild()
    {
        var mgr = new GuildManager();
        var guild = new Guild("Test", 1);
        guild.Id = 10;
        mgr.LoadGuild(guild);

        mgr.LoadSkill(10, "defense_bonus", 3);

        Assert.Equal(3, guild.GetSkillLevel("defense_bonus"));
    }
}
