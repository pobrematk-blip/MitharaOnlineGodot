using Mithara.Server.Database;
using Mithara.Server.Entities;

namespace Mithara.Server.Tests.Database;

public class DatabaseManagerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseManager _db;

    public DatabaseManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "mithara_tests_" + Guid.NewGuid().ToString("N") + ".db");
        _db = new DatabaseManager(_dbPath);
        _db.Initialize();
    }

    public void Dispose()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    [Fact]
    public void Initialize_CreatesTables()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        Assert.Contains("accounts", tables);
        Assert.Contains("characters", tables);
        Assert.Contains("items", tables);
        Assert.Contains("guilds", tables);
        Assert.Contains("guild_members", tables);
        Assert.Contains("guild_skills", tables);
    }

    [Fact]
    public void CreateAccount_ValidData_ReturnsId()
    {
        var id = _db.CreateAccount("testuser", "123456");

        Assert.NotNull(id);
        Assert.True(id.Value > 0);
    }

    [Fact]
    public void CreateAccount_DuplicateUsername_ReturnsNull()
    {
        _db.CreateAccount("duplicate", "123456");
        var second = _db.CreateAccount("duplicate", "789012");

        Assert.Null(second);
    }

    [Fact]
    public void LoginAccount_ValidCredentials_ReturnsId()
    {
        _db.CreateAccount("loginuser", "mypassword");
        var id = _db.LoginAccount("loginuser", "mypassword");

        Assert.NotNull(id);
        Assert.True(id.Value > 0);
    }

    [Fact]
    public void LoginAccount_WrongPassword_ReturnsNull()
    {
        _db.CreateAccount("secureuser", "correctpass");
        var id = _db.LoginAccount("secureuser", "wrongpass");

        Assert.Null(id);
    }

    [Fact]
    public void LoginAccount_NonexistentUser_ReturnsNull()
    {
        var id = _db.LoginAccount("nouser", "password");

        Assert.Null(id);
    }

    [Fact]
    public void HashPassword_ProducesConsistentHash()
    {
        var method = typeof(DatabaseManager).GetMethod("HashPassword",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var hash1 = method.Invoke(null, ["hello", ""]);
        var hash2 = method.Invoke(null, ["hello", ""]);
        var hash3 = method.Invoke(null, ["world", ""]);

        Assert.NotNull(hash1);
        Assert.NotNull(hash2);
        Assert.NotNull(hash3);
        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
    }

    [Fact]
    public void HashPassword_WithDifferentSalt_ProducesDifferentHash()
    {
        var method = typeof(DatabaseManager).GetMethod("HashPassword",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var hash1 = method.Invoke(null, ["hello", "salt1"]);
        var hash2 = method.Invoke(null, ["hello", "salt2"]);

        Assert.NotNull(hash1);
        Assert.NotNull(hash2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CreateCharacter_ValidData_ReturnsId()
    {
        var accountId = _db.CreateAccount("charowner", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "Hero", "Guerreiro", "Humano");

        Assert.True(charId > 0);
    }

    [Fact]
    public void CreateCharacter_SameAccount_AutoIncrementsSlot()
    {
        var accountId = _db.CreateAccount("multiaccount", "pass")!.Value;

        var char1Id = _db.CreateCharacter(accountId, "CharA", "Mago", "Elfo");
        var char2Id = _db.CreateCharacter(accountId, "CharB", "Arqueiro", "Humano");

        Assert.True(char1Id > 0);
        Assert.True(char2Id > 0);
        Assert.NotEqual(char1Id, char2Id);
    }

    [Fact]
    public void GetCharacters_ReturnsAllCharactersForAccount()
    {
        var accountId = _db.CreateAccount("accwithchars", "pass")!.Value;
        _db.CreateCharacter(accountId, "ToonA", "Ladino", "Orc");
        _db.CreateCharacter(accountId, "ToonB", "Priest", "Dark Elfo");

        var chars = _db.GetCharacters(accountId);

        Assert.Equal(2, chars.Count);
        Assert.Contains(chars, c => c.Name == "ToonA");
        Assert.Contains(chars, c => c.Name == "ToonB");
    }

    [Fact]
    public void GetCharacters_EmptyAccount_ReturnsEmptyList()
    {
        var accountId = _db.CreateAccount("emptymom", "pass")!.Value;
        var chars = _db.GetCharacters(accountId);

        Assert.Empty(chars);
    }

    [Fact]
    public void GetSecurityQuestion_StoredQuestion_ReturnsIt()
    {
        _db.CreateAccount("secureq", "pass", "Qual seu pet?", "Rex");
        var question = _db.GetSecurityQuestion("secureq");

        Assert.Equal("Qual seu pet?", question);
    }

    [Fact]
    public void GetSecurityQuestion_NoQuestion_ReturnsNull()
    {
        _db.CreateAccount("noquestion", "pass");
        var question = _db.GetSecurityQuestion("noquestion");

        Assert.Null(question);
    }

    [Fact]
    public void GetSecurityQuestion_NonexistentUser_ReturnsNull()
    {
        var question = _db.GetSecurityQuestion("ghost");

        Assert.Null(question);
    }

    [Fact]
    public void RecoverPassword_CorrectAnswer_ChangesPassword()
    {
        _db.CreateAccount("recoverme", "oldpass", "Cor?", "Azul");

        var success = _db.RecoverPassword("recoverme", "Azul", "newpass");

        Assert.True(success);
        Assert.NotNull(_db.LoginAccount("recoverme", "newpass"));
        Assert.Null(_db.LoginAccount("recoverme", "oldpass"));
    }

    [Fact]
    public void RecoverPassword_WrongAnswer_ReturnsFalse()
    {
        _db.CreateAccount("cantrecover", "pass", "Cidade?", "SP");

        var success = _db.RecoverPassword("cantrecover", "RJ", "newpass");

        Assert.False(success);
    }

    [Fact]
    public void SaveAndLoadItems_PersistsCorrectly()
    {
        var accountId = _db.CreateAccount("itemowner", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "ItemGuy", "Mago", "Elfo");

        var item = new ItemInstance
        {
            ItemId = 1,
            Slot = 3,
            Quantity = 5,
        };
        _db.SaveItem(charId, item);

        var loaded = _db.LoadItems(charId);

        Assert.Single(loaded);
        Assert.Equal(1, loaded[0].ItemId);
        Assert.Equal(3, loaded[0].Slot);
        Assert.Equal(5, loaded[0].Quantity);
        Assert.True(loaded[0].DbId > 0);
    }

    [Fact]
    public void SaveItem_UpdateExisting_PreservesDbId()
    {
        var accountId = _db.CreateAccount("updateitem", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "Updater", "Mago", "Elfo");

        var item = new ItemInstance { ItemId = 2, Slot = 0, Quantity = 1 };
        _db.SaveItem(charId, item);

        item.Quantity = 10;
        item.Slot = 5;
        _db.SaveItem(charId, item);

        var loaded = _db.LoadItems(charId);
        Assert.Single(loaded);
        Assert.Equal(10, loaded[0].Quantity);
        Assert.Equal(5, loaded[0].Slot);
    }

    [Fact]
    public void DeleteItem_RemovesItem()
    {
        var accountId = _db.CreateAccount("delitem", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "Deleter", "Mago", "Elfo");

        var item = new ItemInstance { ItemId = 1, Slot = 0, Quantity = 1 };
        _db.SaveItem(charId, item);
        _db.DeleteItem(charId, item.DbId);

        var loaded = _db.LoadItems(charId);
        Assert.Empty(loaded);
    }

    [Fact]
    public void DeleteItemBySlot_RemovesCorrectSlot()
    {
        var accountId = _db.CreateAccount("delslot", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "SlotDel", "Mago", "Elfo");

        _db.SaveItem(charId, new ItemInstance { ItemId = 1, Slot = 0, Quantity = 1 });
        _db.SaveItem(charId, new ItemInstance { ItemId = 2, Slot = 1, Quantity = 2 });

        _db.DeleteItemBySlot(charId, 0);

        var loaded = _db.LoadItems(charId);
        Assert.Single(loaded);
        Assert.Equal(1, loaded[0].Slot);
    }

    [Fact]
    public void SaveCharacterXp_UpdatesCorrectly()
    {
        var accountId = _db.CreateAccount("xptest", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "XPMan", "Mago", "Elfo");

        _db.SaveCharacterXp(charId, 1500);

        var chars = _db.GetCharacters(accountId);
        Assert.Equal(1500, chars[0].Xp);
    }

    [Fact]
    public void SaveCharacterLevel_UpdatesCorrectly()
    {
        var accountId = _db.CreateAccount("lvltest", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "LevelMan", "Mago", "Elfo");

        _db.SaveCharacterLevel(charId, 10);

        var chars = _db.GetCharacters(accountId);
        Assert.Equal(10, chars[0].Level);
    }

    [Fact]
    public void SaveCharacterPosition_UpdatesCorrectly()
    {
        var accountId = _db.CreateAccount("postest", "pass")!.Value;
        var charId = _db.CreateCharacter(accountId, "PosMan", "Mago", "Elfo");

        _db.SaveCharacterPosition(charId, 500f, 1200f);

        var chars = _db.GetCharacters(accountId);
        Assert.Equal(500f, chars[0].PosX);
        Assert.Equal(1200f, chars[0].PosY);
    }

    [Fact]
    public void GuildLifecycle_CreateSaveLoad()
    {
        _db.CreateAccount("guildacc", "pass");

        _db.SaveGuild(1, "OsHerois", 2, 300, 1);
        _db.SaveGuildMember(1, 100, "Leader", 0);
        _db.SaveGuildMember(1, 101, "Member1", 4);
        _db.SaveGuildSkill(1, "tax_bonus", 2);

        var loadedGuilds = new List<(int id, string name, int level, int xp, int sp)>();
        var loadedMembers = new List<(int guildId, ulong entityId, string name, int rank)>();
        var loadedSkills = new List<(int guildId, string skillId, int level)>();

        _db.LoadAllGuilds(
            (id, name, level, xp, sp) => loadedGuilds.Add((id, name, level, xp, sp)),
            (gid, eid, name, rank) => loadedMembers.Add((gid, eid, name, rank)),
            (gid, sid, lvl) => loadedSkills.Add((gid, sid, lvl))
        );

        Assert.Single(loadedGuilds);
        Assert.Equal(2, loadedGuilds[0].level);
        Assert.Equal(300, loadedGuilds[0].xp);

        Assert.Equal(2, loadedMembers.Count);
        Assert.Contains(loadedMembers, m => m.entityId == 100 && m.rank == 0);

        Assert.Single(loadedSkills);
        Assert.Equal("tax_bonus", loadedSkills[0].skillId);
    }

    [Fact]
    public void DeleteGuildMember_RemovesCorrectly()
    {
        _db.SaveGuild(1, "GuildTest", 1, 0, 0);
        _db.SaveGuildMember(1, 50, "User1", 4);
        _db.DeleteGuildMember(1, 50);

        var members = new List<(int, ulong, string, int)>();
        _db.LoadAllGuilds((_, _, _, _, _) => { },
            (gid, eid, name, rank) => members.Add((gid, eid, name, rank)),
            (_, _, _) => { });

        Assert.Empty(members);
    }

    [Fact]
    public void DeleteGuild_RemovesGuild()
    {
        _db.SaveGuild(99, "DeleteMe", 1, 0, 0);
        _db.DeleteGuild(99);

        var guilds = new List<int>();
        _db.LoadAllGuilds((id, _, _, _, _) => guilds.Add(id),
            (_, _, _, _) => { }, (_, _, _) => { });

        Assert.DoesNotContain(99, guilds);
    }
}
